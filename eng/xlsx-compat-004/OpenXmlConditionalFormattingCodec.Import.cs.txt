using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;
using NeraWorksheet = NeraSpreadSheet.Core.Worksheet;

namespace NeraSpreadSheet.OpenXml;

internal static partial class OpenXmlConditionalFormattingCodec
{
    public static IReadOnlyList<CellStylePatch> ReadDifferentialStyles(WorkbookPart workbookPart,
        bool preserveUnsupportedMarkup = false, WorkbookTheme? theme = null)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        if (workbookPart.WorkbookStylesPart is not { } part) return new OpenXmlDifferentialImport(0);
        var root = LoadPartXml(part).Root ?? throw OpenXmlImportDiagnostics.Invalid("Missing style-table root.", "dxfs");
        if (root.Name != SpreadsheetNamespace + "styleSheet") throw OpenXmlImportDiagnostics.Invalid("Invalid style-table root.", "dxfs");
        var collections = root.Elements(SpreadsheetNamespace + "dxfs").ToArray();
        if (collections.Length > 1) throw OpenXmlImportDiagnostics.Invalid("Duplicate dxfs collections.", "dxfs");
        if (collections.Length == 0) return new OpenXmlDifferentialImport(0);
        var elements = collections[0].Elements(SpreadsheetNamespace + "dxf").Take(MaxDifferentialStyles + 1).ToArray();
        if (elements.Length > MaxDifferentialStyles)
            throw OpenXmlImportDiagnostics.Invalid($"The style table exceeds the differential-style limit of {MaxDifferentialStyles}.", "dxfs", limit: true);
        ValidateDeclaredCount(collections[0], elements.Length, "differential style");
        if (collections[0].Elements().Any(element => element.Name != SpreadsheetNamespace + "dxf"))
            throw OpenXmlImportDiagnostics.Invalid("Invalid child in dxfs collection.", "dxfs");
        var result = new OpenXmlDifferentialImport(elements.Length);
        for (var index = 0; index < elements.Length; index++)
        {
            var feature = OpenXmlDxfCompatibility.UnmodeledFeature(elements[index]);
            if (feature is not null)
            {
                OpenXmlDxfCompatibility.ValidateOpaque(elements[index], differentialStyle: true);
                var warning = OpenXmlImportDiagnostics.Unsupported(feature, dxf: index);
                if (!preserveUnsupportedMarkup) throw OpenXmlImportDiagnostics.Failure(warning);
                result.Set(index, new CellStylePatch(), opaque: true);
                result.Warn(warning);
            }
            else
            {
                // Known malformed data still throws. Do not convert arbitrary
                // InvalidDataException (bad colors/sizes/counts/etc.) to an empty patch.
                result.Set(index, ReadDifferentialStyle(elements[index], theme ?? WorkbookTheme.Office));
            }
        }
        return result;
    }

    public static void ReadWorksheetRules(WorksheetPart worksheetPart, NeraWorksheet worksheet,
        IReadOnlyList<CellStylePatch> workbookDifferentialStyles, bool preserveUnsupportedMarkup,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(workbookDifferentialStyles);
        var root = LoadPartXml(worksheetPart).Root ?? throw OpenXmlImportDiagnostics.Invalid("Missing worksheet root.", "conditionalFormatting", worksheet.Name);
        if (root.Name != SpreadsheetNamespace + "worksheet") throw OpenXmlImportDiagnostics.Invalid("Invalid worksheet root.", "conditionalFormatting", worksheet.Name);
        const int maximum = WorksheetConditionalFormattingCollection.MaxRulesPerWorksheet;
        var containers = root.Elements(SpreadsheetNamespace + "conditionalFormatting").ToArray();
        var count = containers.SelectMany(container => container.Elements(SpreadsheetNamespace + "cfRule")).Take(maximum + 1).Count();
        var extendedRules = root.Elements(SpreadsheetNamespace + "extLst").Descendants()
            .Where(element => element.Name.LocalName == "cfRule").Take(maximum + 1).Count();
        if (count + extendedRules > maximum)
            throw OpenXmlImportDiagnostics.Invalid($"The worksheet exceeds the conditional-formatting rule limit of {maximum}, including opaque rules.", "conditionalFormatting", worksheet.Name, limit: true);
        var state = workbookDifferentialStyles as OpenXmlDifferentialImport;
        if (root.Elements(SpreadsheetNamespace + "extLst").Descendants().Any(element => element.Name.LocalName is "conditionalFormatting" or "cfRule"))
            Opaque(OpenXmlImportDiagnostics.Unsupported("conditionalFormatting/extension", worksheet.Name));
        var parsed = new List<ParsedRule>();
        var priorities = new HashSet<int>();
        var identifiers = new HashSet<Guid>();
        foreach (var container in containers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reference = (string?)container.Attribute("sqref");
            ValidateReferenceBudget(reference, worksheet.Name);
            var ranges = ParseRanges(reference);
            foreach (var element in container.Elements(SpreadsheetNamespace + "cfRule"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var type = (string?)element.Attribute("type");
                if (string.IsNullOrWhiteSpace(type)) throw OpenXmlImportDiagnostics.Invalid("Missing conditional-formatting rule type.", "conditionalFormatting", worksheet.Name);
                var priority = ParsePositiveInt((string?)element.Attribute("priority"), "conditional-formatting priority");
                if (!priorities.Add(priority)) throw OpenXmlImportDiagnostics.Invalid("Duplicate conditional-formatting priority.", "conditionalFormatting", worksheet.Name);
                int? style = element.Attribute("dxfId") is { } dxf ? ParseNonNegativeInt(dxf.Value, "conditional-formatting dxfId") : null;
                if (style is { } styleId && styleId >= workbookDifferentialStyles.Count)
                    throw OpenXmlImportDiagnostics.Invalid("Conditional-formatting dxfId is outside the style table.", "conditionalFormatting/dxfId", worksheet.Name);
                _ = ParseOptionalBoolean((string?)element.Attribute("stopIfTrue"));
                if (!IsSupportedRuleType(type) || OpenXmlDxfCompatibility.UnmodeledRule(container, element))
                {
                    OpenXmlDxfCompatibility.ValidateOpaque(container, differentialStyle: false, worksheet.Name);
                    Opaque(OpenXmlImportDiagnostics.Unsupported("conditionalFormatting/" + type, worksheet.Name, reference, priority, style));
                    continue;
                }
                if (style is null)
                {
                    OpenXmlDxfCompatibility.ValidateOpaque(container, differentialStyle: false, worksheet.Name);
                    Opaque(OpenXmlImportDiagnostics.Unsupported("conditionalFormatting/no-dxf", worksheet.Name, reference, priority));
                    continue;
                }
                // Parse known formula/operator requirements even when its style is
                // opaque. A supported rule must not hide malformed known metadata.
                var rule = ParseRule(element, ranges, workbookDifferentialStyles, worksheet.Name);
                if (!identifiers.Add(rule.Id)) throw OpenXmlImportDiagnostics.Invalid("Duplicate conditional-formatting rule.", "conditionalFormatting", worksheet.Name);
                if (state?.IsOpaque(style.Value) == true || rule.Style.IsEmpty || rule.Style.Apply(CellStyle.Default) == CellStyle.Default)
                {
                    Opaque(OpenXmlImportDiagnostics.Unsupported("conditionalFormatting/opaque-dxf", worksheet.Name, reference, priority, style));
                    continue;
                }
                parsed.Add(rule);
            }
        }
        if (parsed.Count == 0) return;
        var catalog = new DifferentialStyleCatalog();
        var rules = new List<ConditionalFormattingRule>(parsed.Count);
        foreach (var rule in parsed.OrderBy(rule => rule.Priority))
            rules.Add(new ConditionalFormattingRule(rule.Id, rule.Ranges, rule.Type, rule.Operator,
                rule.Formula1, rule.Formula2, catalog.Intern(rule.Style), rule.Priority, rule.StopIfTrue));
        worksheet.DifferentialStyles.Restore(catalog.Snapshot());
        foreach (var rule in rules) worksheet.AddConditionalFormattingRule(rule);

        void Opaque(OpenXmlImportWarning warning)
        {
            if (!preserveUnsupportedMarkup) throw OpenXmlImportDiagnostics.Failure(warning);
            if (state is null) throw new InvalidOperationException("Compatibility import is missing its original dxf slot state.");
            state.Warn(warning);
        }
    }

    private static void ValidateReferenceBudget(string? reference, string sheet)
    {
        if (reference is null) return;
        var count = 0; var inToken = false;
        foreach (var value in reference)
        {
            if (char.IsWhiteSpace(value)) inToken = false;
            else if (!inToken)
            {
                inToken = true;
                if (++count > ConditionalFormattingRule.MaxRangesPerRule)
                    throw OpenXmlImportDiagnostics.Invalid("Conditional-formatting sqref exceeds the permitted range count.", "conditionalFormatting/sqref", sheet, limit: true);
            }
        }
    }
    private static bool IsSupportedRuleType(string? value) => value is "cellIs" or "expression";
}
