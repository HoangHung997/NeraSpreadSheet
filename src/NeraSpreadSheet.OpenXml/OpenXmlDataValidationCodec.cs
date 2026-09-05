using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;
using NeraWorksheet = NeraSpreadSheet.Core.Worksheet;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlDataValidationCodec
{
    private const long MaxXmlCharacters = 256L * 1024L * 1024L;

    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly IReadOnlyDictionary<string, int> WorksheetOrder =
        CreateOrder([
            "sheetPr",
            "dimension",
            "sheetViews",
            "sheetFormatPr",
            "cols",
            "sheetData",
            "sheetCalcPr",
            "sheetProtection",
            "protectedRanges",
            "scenarios",
            "autoFilter",
            "sortState",
            "dataConsolidate",
            "customSheetViews",
            "mergeCells",
            "phoneticPr",
            "conditionalFormatting",
            "dataValidations",
            "hyperlinks",
            "printOptions",
            "pageMargins",
            "pageSetup",
            "headerFooter",
            "rowBreaks",
            "colBreaks",
            "customProperties",
            "cellWatches",
            "ignoredErrors",
            "smartTags",
            "drawing",
            "legacyDrawing",
            "legacyDrawingHF",
            "picture",
            "oleObjects",
            "controls",
            "webPublishItems",
            "tableParts",
            "extLst",
        ]);

    public static void ReadWorksheetRules(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(worksheet);

        var document = LoadPartXml(worksheetPart);
        var root = document.Root
            ?? throw new InvalidDataException(
                "The XLSX worksheet part is missing its root element.");
        if (root.Name != SpreadsheetNamespace + "worksheet")
        {
            throw new InvalidDataException(
                "The XLSX worksheet part contains invalid markup.");
        }

        var containers = root
            .Elements(SpreadsheetNamespace + "dataValidations")
            .ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidDataException(
                "The XLSX worksheet contains duplicate dataValidations collections.");
        }

        if (containers.Length == 0)
        {
            return;
        }

        var elements = containers[0]
            .Elements(SpreadsheetNamespace + "dataValidation")
            .ToArray();
        if (elements.Length > WorksheetDataValidationCollection.MaxRulesPerWorksheet)
        {
            throw new InvalidDataException(
                $"The XLSX worksheet exceeds the data-validation rule limit " +
                $"of {WorksheetDataValidationCollection.MaxRulesPerWorksheet}.");
        }

        ValidateDeclaredCount(containers[0], elements.Length);
        var parsed = new List<DataValidationRule>(elements.Length);
        var identifiers = new HashSet<Guid>();
        for (var index = 0; index < elements.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rule = ParseRule(
                elements[index],
                worksheet.Name,
                index);
            if (!identifiers.Add(rule.Id))
            {
                throw new InvalidDataException(
                    "The XLSX worksheet contains duplicate data-validation rules.");
            }

            parsed.Add(rule);
        }

        try
        {
            foreach (var rule in parsed)
            {
                worksheet.AddDataValidationRule(rule);
            }
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException(
                "The XLSX worksheet contains invalid or overlapping data-validation rules.",
                exception);
        }
    }

    public static void WriteWorksheetRules(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(worksheet);

        var document = LoadPartXml(worksheetPart);
        var root = document.Root
            ?? throw new InvalidDataException(
                "The generated XLSX worksheet is missing its root element.");
        root.Elements(SpreadsheetNamespace + "dataValidations").Remove();
        if (worksheet.DataValidationRuleCount == 0)
        {
            SavePartXml(worksheetPart, document);
            return;
        }

        var rules = worksheet.DataValidationRules
            .OrderBy(static rule => rule.Ranges[0].Top)
            .ThenBy(static rule => rule.Ranges[0].Left)
            .ThenBy(static rule => rule.Id)
            .ToArray();
        var container = new XElement(
            SpreadsheetNamespace + "dataValidations",
            new XAttribute("count", rules.Length));
        foreach (var rule in rules)
        {
            var element = new XElement(
                SpreadsheetNamespace + "dataValidation",
                new XAttribute("type", WriteType(rule.Type)),
                new XAttribute(
                    "sqref",
                    string.Join(
                        " ",
                        rule.Ranges.Select(static range => range.ToString()))),
                new XAttribute("allowBlank", rule.AllowBlank ? 1 : 0),
                new XAttribute(
                    "showInputMessage",
                    rule.ShowInputMessage ? 1 : 0),
                new XAttribute(
                    "showErrorMessage",
                    rule.ShowErrorMessage ? 1 : 0),
                new XAttribute(
                    "showDropDown",
                    rule.ShowDropDown ? 0 : 1),
                new XAttribute(
                    "errorStyle",
                    WriteErrorStyle(rule.ErrorStyle)));
            if (rule.Operator is { } @operator)
            {
                element.Add(new XAttribute(
                    "operator",
                    WriteOperator(@operator)));
            }
            if (rule.PromptTitle is not null)
            {
                element.Add(new XAttribute("promptTitle", rule.PromptTitle));
            }
            if (rule.Prompt is not null)
            {
                element.Add(new XAttribute("prompt", rule.Prompt));
            }
            if (rule.ErrorTitle is not null)
            {
                element.Add(new XAttribute("errorTitle", rule.ErrorTitle));
            }
            if (rule.Error is not null)
            {
                element.Add(new XAttribute("error", rule.Error));
            }

            element.Add(new XElement(
                SpreadsheetNamespace + "formula1",
                TrimFormulaPrefix(rule.Formula1)));
            if (rule.Formula2 is not null)
            {
                element.Add(new XElement(
                    SpreadsheetNamespace + "formula2",
                    TrimFormulaPrefix(rule.Formula2)));
            }
            container.Add(element);
        }

        InsertInSchemaOrder(root, container, WorksheetOrder);
        SavePartXml(worksheetPart, document);
    }

    private static DataValidationRule ParseRule(
        XElement element,
        string worksheetName,
        int ordinal)
    {
        EnsureOnlySupportedChildren(element);
        var type = ParseType((string?)element.Attribute("type"));
        var ranges = ParseRanges((string?)element.Attribute("sqref"));
        var formulas1 = element
            .Elements(SpreadsheetNamespace + "formula1")
            .Select(static formula => formula.Value)
            .ToArray();
        var formulas2 = element
            .Elements(SpreadsheetNamespace + "formula2")
            .Select(static formula => formula.Value)
            .ToArray();
        if (formulas1.Length != 1 || formulas2.Length > 1)
        {
            throw new InvalidDataException(
                "Data-validation rules require exactly one formula1 and at most one formula2.");
        }

        DataValidationOperator? @operator = null;
        if (type is not DataValidationType.List and
            not DataValidationType.Custom)
        {
            @operator = ParseOperator(
                (string?)element.Attribute("operator"));
        }
        else if (element.Attribute("operator") is not null)
        {
            throw new InvalidDataException(
                "List and custom data-validation rules cannot declare an operator.");
        }

        var formula2 = formulas2.Length == 0
            ? null
            : formulas2[0];
        var expectedSecondFormula = @operator is
            DataValidationOperator.Between or
            DataValidationOperator.NotBetween;
        if (expectedSecondFormula != (formula2 is not null))
        {
            throw new InvalidDataException(
                expectedSecondFormula
                    ? "Between data-validation rules require formula2."
                    : "This data-validation operator does not accept formula2.");
        }

        var allowBlank = ParseBoolean(
            (string?)element.Attribute("allowBlank"),
            defaultValue: false,
            "allowBlank");
        var showInputMessage = ParseBoolean(
            (string?)element.Attribute("showInputMessage"),
            defaultValue: false,
            "showInputMessage");
        var showErrorMessage = ParseBoolean(
            (string?)element.Attribute("showErrorMessage"),
            defaultValue: false,
            "showErrorMessage");
        var hideDropDown = ParseBoolean(
            (string?)element.Attribute("showDropDown"),
            defaultValue: false,
            "showDropDown");
        var errorStyle = ParseErrorStyle(
            (string?)element.Attribute("errorStyle"));
        var id = CreateDeterministicId(
            worksheetName,
            ordinal,
            element);

        try
        {
            return new DataValidationRule(
                id,
                ranges,
                type,
                @operator,
                formulas1[0],
                formula2,
                allowBlank,
                showInputMessage,
                (string?)element.Attribute("promptTitle"),
                (string?)element.Attribute("prompt"),
                showErrorMessage,
                errorStyle,
                (string?)element.Attribute("errorTitle"),
                (string?)element.Attribute("error"),
                showDropDown: !hideDropDown);
        }
        catch (Exception exception) when (exception is ArgumentException)
        {
            throw new InvalidDataException(
                "The XLSX worksheet contains an invalid data-validation rule.",
                exception);
        }
    }

    private static void EnsureOnlySupportedChildren(XElement element)
    {
        foreach (var child in element.Elements())
        {
            if (child.Name != SpreadsheetNamespace + "formula1" &&
                child.Name != SpreadsheetNamespace + "formula2")
            {
                throw new InvalidDataException(
                    $"Unsupported data-validation child '{child.Name.LocalName}'.");
            }
        }
    }

    private static CellRange[] ParseRanges(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new InvalidDataException(
                "Data-validation sqref cannot be empty.");
        }

        var tokens = reference.Split(
            [' ', '\t', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 ||
            tokens.Length > DataValidationRule.MaxRangesPerRule)
        {
            throw new InvalidDataException(
                $"Data-validation sqref must contain between 1 and " +
                $"{DataValidationRule.MaxRangesPerRule} ranges.");
        }

        var ranges = new CellRange[tokens.Length];
        for (var index = 0; index < tokens.Length; index++)
        {
            var separator = tokens[index].IndexOf(':');
            var firstText = separator < 0
                ? tokens[index]
                : tokens[index][..separator];
            var secondText = separator < 0
                ? tokens[index]
                : tokens[index][(separator + 1)..];
            if (!CellAddress.TryParseA1(firstText, out var first) ||
                !CellAddress.TryParseA1(secondText, out var second))
            {
                throw new InvalidDataException(
                    $"'{tokens[index]}' is not a valid data-validation range.");
            }

            ranges[index] = new CellRange(first, second);
        }

        return ranges;
    }

    private static DataValidationType ParseType(string? value) =>
        value switch
        {
            "whole" => DataValidationType.Whole,
            "decimal" => DataValidationType.Decimal,
            "list" => DataValidationType.List,
            "date" => DataValidationType.Date,
            "time" => DataValidationType.Time,
            "textLength" => DataValidationType.TextLength,
            "custom" => DataValidationType.Custom,
            _ => throw new InvalidDataException(
                $"Unsupported data-validation type '{value ?? "<missing>"}'."),
        };

    private static string WriteType(DataValidationType type) =>
        type switch
        {
            DataValidationType.Whole => "whole",
            DataValidationType.Decimal => "decimal",
            DataValidationType.List => "list",
            DataValidationType.Date => "date",
            DataValidationType.Time => "time",
            DataValidationType.TextLength => "textLength",
            DataValidationType.Custom => "custom",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    private static DataValidationOperator ParseOperator(string? value) =>
        value switch
        {
            null or "between" => DataValidationOperator.Between,
            "notBetween" => DataValidationOperator.NotBetween,
            "equal" => DataValidationOperator.Equal,
            "notEqual" => DataValidationOperator.NotEqual,
            "greaterThan" => DataValidationOperator.GreaterThan,
            "lessThan" => DataValidationOperator.LessThan,
            "greaterThanOrEqual" => DataValidationOperator.GreaterThanOrEqual,
            "lessThanOrEqual" => DataValidationOperator.LessThanOrEqual,
            _ => throw new InvalidDataException(
                $"Unsupported data-validation operator '{value}'."),
        };

    private static string WriteOperator(DataValidationOperator @operator) =>
        @operator switch
        {
            DataValidationOperator.Between => "between",
            DataValidationOperator.NotBetween => "notBetween",
            DataValidationOperator.Equal => "equal",
            DataValidationOperator.NotEqual => "notEqual",
            DataValidationOperator.GreaterThan => "greaterThan",
            DataValidationOperator.LessThan => "lessThan",
            DataValidationOperator.GreaterThanOrEqual => "greaterThanOrEqual",
            DataValidationOperator.LessThanOrEqual => "lessThanOrEqual",
            _ => throw new ArgumentOutOfRangeException(nameof(@operator)),
        };

    private static DataValidationErrorStyle ParseErrorStyle(string? value) =>
        value switch
        {
            null or "stop" => DataValidationErrorStyle.Stop,
            "warning" => DataValidationErrorStyle.Warning,
            "information" => DataValidationErrorStyle.Information,
            _ => throw new InvalidDataException(
                $"Unsupported data-validation error style '{value}'."),
        };

    private static string WriteErrorStyle(DataValidationErrorStyle style) =>
        style switch
        {
            DataValidationErrorStyle.Stop => "stop",
            DataValidationErrorStyle.Warning => "warning",
            DataValidationErrorStyle.Information => "information",
            _ => throw new ArgumentOutOfRangeException(nameof(style)),
        };

    private static bool ParseBoolean(
        string? value,
        bool defaultValue,
        string attributeName) =>
        value switch
        {
            null => defaultValue,
            "1" or "true" or "TRUE" => true,
            "0" or "false" or "FALSE" => false,
            _ => throw new InvalidDataException(
                $"Data-validation attribute '{attributeName}' is not a valid boolean."),
        };

    private static Guid CreateDeterministicId(
        string worksheetName,
        int ordinal,
        XElement element)
    {
        var payload = string.Create(
            CultureInfo.InvariantCulture,
            $"{worksheetName}\n{ordinal}\n{element}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        var bytes = hash[..16];
        var id = new Guid(bytes);
        return id == Guid.Empty
            ? new Guid("00000000-0000-0000-0000-000000000001")
            : id;
    }

    private static void ValidateDeclaredCount(
        XElement container,
        int actualCount)
    {
        var raw = (string?)container.Attribute("count");
        if (raw is null)
        {
            return;
        }

        if (!uint.TryParse(
                raw,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var declared) ||
            declared != actualCount)
        {
            throw new InvalidDataException(
                "The dataValidations count does not match its child elements.");
        }
    }

    private static string TrimFormulaPrefix(string formula) =>
        formula.StartsWith('=')
            ? formula[1..]
            : formula;

    private static IReadOnlyDictionary<string, int> CreateOrder(
        IReadOnlyList<string> names)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < names.Count; index++)
        {
            result.Add(names[index], index);
        }
        return result;
    }

    private static void InsertInSchemaOrder(
        XElement root,
        XElement element,
        IReadOnlyDictionary<string, int> schemaOrder)
    {
        var targetRank = schemaOrder[element.Name.LocalName];
        var following = root.Elements().FirstOrDefault(candidate =>
            candidate.Name.Namespace == SpreadsheetNamespace &&
            schemaOrder.TryGetValue(
                candidate.Name.LocalName,
                out var candidateRank) &&
            candidateRank > targetRank);
        if (following is null)
        {
            root.Add(element);
        }
        else
        {
            following.AddBeforeSelf(element);
        }
    }

    private static XDocument LoadPartXml(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                MaxCharactersInDocument = MaxXmlCharacters,
                XmlResolver = null,
            });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static void SavePartXml(
        OpenXmlPart part,
        XDocument document)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(
            stream,
            new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                Indent = false,
                OmitXmlDeclaration = false,
            });
        document.Save(writer, SaveOptions.DisableFormatting);
    }
}
