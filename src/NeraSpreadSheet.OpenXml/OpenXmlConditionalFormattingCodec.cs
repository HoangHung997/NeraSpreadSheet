using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;
using NeraWorksheet = NeraSpreadSheet.Core.Worksheet;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlConditionalFormattingCodec
{
    private const int MaxDifferentialStyles = 100_000;
    private const long MaxXmlCharacters = 256L * 1024L * 1024L;
    private const uint FirstCustomNumberFormatId = 164U;

    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly IReadOnlyDictionary<string, int> StylesheetOrder =
        CreateOrder([
            "numFmts",
            "fonts",
            "fills",
            "borders",
            "cellStyleXfs",
            "cellXfs",
            "cellStyles",
            "dxfs",
            "tableStyles",
            "colors",
            "extLst",
        ]);

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

    public static IReadOnlyList<CellStylePatch> ReadDifferentialStyles(
        WorkbookPart workbookPart,
        bool preserveUnsupportedMarkup = false,
        WorkbookTheme? theme = null)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        var stylesPart = workbookPart.WorkbookStylesPart;
        if (stylesPart is null)
        {
            return [];
        }

        var document = LoadPartXml(stylesPart);
        var root = document.Root
            ?? throw new InvalidDataException(
                "The XLSX style part is missing its root element.");
        if (root.Name != SpreadsheetNamespace + "styleSheet")
        {
            throw new InvalidDataException(
                "The XLSX style part contains invalid markup.");
        }

        var containers = root
            .Elements(SpreadsheetNamespace + "dxfs")
            .ToArray();
        if (containers.Length > 1)
        {
            throw new InvalidDataException(
                "The XLSX style table contains duplicate dxfs collections.");
        }

        if (containers.Length == 0)
        {
            return [];
        }

        var elements = containers[0]
            .Elements(SpreadsheetNamespace + "dxf")
            .ToArray();
        if (elements.Length > MaxDifferentialStyles)
        {
            throw new InvalidDataException(
                $"The XLSX style table exceeds the differential-style " +
                $"limit of {MaxDifferentialStyles}.");
        }

        ValidateDeclaredCount(
            containers[0],
            elements.Length,
            "differential style");
        var result = new CellStylePatch[elements.Length];
        for (var index = 0; index < elements.Length; index++)
        {
            try
            {
                result[index] = ReadDifferentialStyle(
                    elements[index],
                    theme ?? WorkbookTheme.Office);
            }
            catch (InvalidDataException) when (preserveUnsupportedMarkup)
            {
                result[index] = new CellStylePatch();
            }
        }

        return result;
    }

    public static void ReadWorksheetRules(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet,
        IReadOnlyList<CellStylePatch> workbookDifferentialStyles,
        bool preserveUnsupportedMarkup,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(workbookDifferentialStyles);

        var document = LoadPartXml(worksheetPart);
        var root = document.Root
            ?? throw new InvalidDataException(
                "The XLSX worksheet part is missing its root element.");
        if (root.Name != SpreadsheetNamespace + "worksheet")
        {
            throw new InvalidDataException(
                "The XLSX worksheet part contains invalid markup.");
        }

        var parsed = new List<ParsedRule>();
        var priorities = new HashSet<int>();
        var identifiers = new HashSet<Guid>();
        foreach (var conditionalFormatting in root
                     .Elements(SpreadsheetNamespace + "conditionalFormatting"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var ranges = ParseRanges(
                (string?)conditionalFormatting.Attribute("sqref"));
            foreach (var ruleElement in conditionalFormatting
                         .Elements(SpreadsheetNamespace + "cfRule"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (preserveUnsupportedMarkup &&
                    !IsSupportedRuleType(
                        (string?)ruleElement.Attribute("type")))
                {
                    continue;
                }
                if (parsed.Count >=
                    WorksheetConditionalFormattingCollection.MaxRulesPerWorksheet)
                {
                    throw new InvalidDataException(
                        $"The XLSX worksheet exceeds the conditional-formatting " +
                        $"rule limit of " +
                        $"{WorksheetConditionalFormattingCollection.MaxRulesPerWorksheet}.");
                }

                var parsedRule = ParseRule(
                    ruleElement,
                    ranges,
                    workbookDifferentialStyles,
                    worksheet.Name);
                if (!priorities.Add(parsedRule.Priority))
                {
                    throw new InvalidDataException(
                        $"The XLSX worksheet contains duplicate " +
                        $"conditional-formatting priority " +
                        $"{parsedRule.Priority}.");
                }

                if (!identifiers.Add(parsedRule.Id))
                {
                    throw new InvalidDataException(
                        "The XLSX worksheet contains duplicate conditional-formatting rules.");
                }

                parsed.Add(parsedRule);
            }
        }

        if (parsed.Count == 0)
        {
            return;
        }

        var localCatalog = new DifferentialStyleCatalog();
        var materialized = new List<ConditionalFormattingRule>(parsed.Count);
        foreach (var parsedRule in parsed
                     .OrderBy(static rule => rule.Priority))
        {
            var localStyleId = localCatalog.Intern(parsedRule.Style);
            materialized.Add(new ConditionalFormattingRule(
                parsedRule.Id,
                parsedRule.Ranges,
                parsedRule.Type,
                parsedRule.Operator,
                parsedRule.Formula1,
                parsedRule.Formula2,
                localStyleId,
                parsedRule.Priority,
                parsedRule.StopIfTrue));
        }

        worksheet.DifferentialStyles.Restore(localCatalog.Snapshot());
        foreach (var rule in materialized)
        {
            worksheet.AddConditionalFormattingRule(rule);
        }
    }

    private static bool IsSupportedRuleType(string? value) =>
        value is "cellIs" or "expression";

    public static OpenXmlConditionalFormattingExportPlan WriteDifferentialStyles(
        WorkbookPart workbookPart,
        NeraWorkbook workbook)
    {
        ArgumentNullException.ThrowIfNull(workbookPart);
        ArgumentNullException.ThrowIfNull(workbook);
        var stylesPart = workbookPart.WorkbookStylesPart
            ?? throw new InvalidDataException(
                "The generated XLSX package does not contain a style table.");

        var globalStyles = new List<CellStylePatch>();
        var globalIds = new Dictionary<CellStylePatch, uint>();
        var worksheetMaps = new Dictionary<
            NeraWorksheet,
            IReadOnlyDictionary<int, uint>>();
        var worksheetColorMaps = new Dictionary<
            NeraWorksheet,
            IReadOnlyDictionary<SpreadsheetColorFilter, uint>>();
        foreach (var worksheet in workbook.Worksheets)
        {
            var localMap = new Dictionary<int, uint>();
            foreach (var rule in worksheet.ConditionalFormattingRules
                         .OrderBy(static rule => rule.Priority))
            {
                if (localMap.ContainsKey(rule.DifferentialStyleId))
                {
                    continue;
                }

                var patch = worksheet.DifferentialStyles.Get(
                    rule.DifferentialStyleId);
                if (!globalIds.TryGetValue(patch, out var globalId))
                {
                    if (globalStyles.Count >= MaxDifferentialStyles)
                    {
                        throw new InvalidOperationException(
                            $"The workbook exceeds the differential-style " +
                            $"limit of {MaxDifferentialStyles}.");
                    }

                    globalId = checked((uint)globalStyles.Count);
                    globalStyles.Add(patch);
                    globalIds.Add(patch, globalId);
                }

                localMap.Add(rule.DifferentialStyleId, globalId);
            }

            var colorMap = new Dictionary<SpreadsheetColorFilter, uint>();
            foreach (var color in EnumerateFilterColors(worksheet).Distinct())
            {
                var patch = color.Kind == SpreadsheetFilterColorKind.Fill
                    ? new CellStylePatch
                    {
                        Fill = new CellFillStyle
                        {
                            IsVisible = true,
                            Pattern = CellFillPattern.Solid,
                            Color = color.Color,
                            BackgroundColor = color.Color,
                        },
                    }
                    : new CellStylePatch { FontColor = color.Color };
                if (!globalIds.TryGetValue(patch, out var globalId))
                {
                    if (globalStyles.Count >= MaxDifferentialStyles)
                    {
                        throw new InvalidOperationException(
                            $"The workbook exceeds the differential-style limit of {MaxDifferentialStyles}.");
                    }
                    globalId = checked((uint)globalStyles.Count);
                    globalStyles.Add(patch);
                    globalIds.Add(patch, globalId);
                }
                colorMap.Add(color, globalId);
            }

            worksheetMaps.Add(worksheet, localMap);
            worksheetColorMaps.Add(worksheet, colorMap);
        }

        var document = LoadPartXml(stylesPart);
        var root = document.Root
            ?? throw new InvalidDataException(
                "The generated XLSX style part is missing its root element.");
        root.Elements(SpreadsheetNamespace + "dxfs").Remove();
        if (globalStyles.Count > 0)
        {
            var dxfs = new XElement(
                SpreadsheetNamespace + "dxfs",
                new XAttribute("count", globalStyles.Count));
            for (var index = 0; index < globalStyles.Count; index++)
            {
                dxfs.Add(WriteDifferentialStyle(
                    globalStyles[index],
                    checked(FirstCustomNumberFormatId + (uint)index)));
            }

            InsertInSchemaOrder(root, dxfs, StylesheetOrder);
        }

        SavePartXml(stylesPart, document);
        return new OpenXmlConditionalFormattingExportPlan(worksheetMaps, worksheetColorMaps);
    }

    private static IEnumerable<SpreadsheetColorFilter> EnumerateFilterColors(
        NeraWorksheet worksheet)
    {
        foreach (var column in worksheet.AutoFilter?.Columns ?? [])
        {
            if (column.ColorFilter is { } color) yield return color;
        }
        foreach (var condition in worksheet.AutoFilter?.SortState?.Conditions ?? [])
        {
            if (condition.Color is { } color) yield return color;
        }
        foreach (var table in worksheet.Tables)
        {
            foreach (var column in table.AutoFilter?.Columns ?? [])
            {
                if (column.ColorFilter is { } color) yield return color;
            }
            foreach (var condition in table.AutoFilter?.SortState?.Conditions ?? [])
            {
                if (condition.Color is { } color) yield return color;
            }
        }
    }

    public static void WriteWorksheetRules(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet,
        OpenXmlConditionalFormattingExportPlan exportPlan)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(exportPlan);

        var document = LoadPartXml(worksheetPart);
        var root = document.Root
            ?? throw new InvalidDataException(
                "The generated XLSX worksheet is missing its root element.");
        root.Elements(
            SpreadsheetNamespace + "conditionalFormatting").Remove();
        foreach (var rule in worksheet.ConditionalFormattingRules
                     .OrderBy(static rule => rule.Priority))
        {
            var container = new XElement(
                SpreadsheetNamespace + "conditionalFormatting",
                new XAttribute(
                    "sqref",
                    string.Join(
                        " ",
                        rule.Ranges.Select(static range => range.ToString()))));
            var ruleElement = new XElement(
                SpreadsheetNamespace + "cfRule",
                new XAttribute(
                    "type",
                    WriteRuleType(rule.Type)),
                new XAttribute(
                    "dxfId",
                    exportPlan.GetDifferentialStyleId(
                        worksheet,
                        rule.DifferentialStyleId)),
                new XAttribute("priority", rule.Priority));
            if (rule.StopIfTrue)
            {
                ruleElement.Add(new XAttribute("stopIfTrue", 1));
            }

            if (rule.Type == ConditionalFormattingRuleType.CellIs)
            {
                ruleElement.Add(new XAttribute(
                    "operator",
                    WriteOperator(rule.Operator)));
            }

            ruleElement.Add(new XElement(
                SpreadsheetNamespace + "formula",
                TrimFormulaPrefix(rule.Formula1)));
            if (rule.Formula2 is not null)
            {
                ruleElement.Add(new XElement(
                    SpreadsheetNamespace + "formula",
                    TrimFormulaPrefix(rule.Formula2)));
            }

            container.Add(ruleElement);
            InsertInSchemaOrder(root, container, WorksheetOrder);
        }

        SavePartXml(worksheetPart, document);
    }

    private static ParsedRule ParseRule(
        XElement element,
        CellRange[] ranges,
        IReadOnlyList<CellStylePatch> workbookDifferentialStyles,
        string worksheetName)
    {
        var type = ParseRuleType((string?)element.Attribute("type"));
        var priority = ParsePositiveInt(
            (string?)element.Attribute("priority"),
            "conditional-formatting priority");
        var dxfId = ParseNonNegativeInt(
            (string?)element.Attribute("dxfId"),
            "conditional-formatting dxfId");
        if (dxfId >= workbookDifferentialStyles.Count)
        {
            throw new InvalidDataException(
                $"Conditional-formatting rule priority {priority} references " +
                $"differential style {dxfId}, but the style table contains " +
                $"only {workbookDifferentialStyles.Count} entries.");
        }

        var formulas = element
            .Elements(SpreadsheetNamespace + "formula")
            .Select(static formula => formula.Value)
            .ToArray();
        var @operator = ConditionalFormattingOperator.Equal;
        string formula1;
        string? formula2 = null;
        if (type == ConditionalFormattingRuleType.Expression)
        {
            if (formulas.Length != 1)
            {
                throw new InvalidDataException(
                    "Expression conditional-formatting rules require exactly one formula.");
            }

            formula1 = formulas[0];
        }
        else
        {
            @operator = ParseOperator(
                (string?)element.Attribute("operator"));
            var expectedFormulaCount = @operator is
                ConditionalFormattingOperator.Between or
                ConditionalFormattingOperator.NotBetween
                ? 2
                : 1;
            if (formulas.Length != expectedFormulaCount)
            {
                throw new InvalidDataException(
                    $"Cell-is conditional-formatting operator '{@operator}' " +
                    $"requires {expectedFormulaCount} formula value(s).");
            }

            formula1 = formulas[0];
            formula2 = formulas.Length == 2
                ? formulas[1]
                : null;
        }

        var stopIfTrue = ParseOptionalBoolean(
            (string?)element.Attribute("stopIfTrue"));
        var id = CreateDeterministicIdentifier(
            worksheetName,
            ranges,
            type,
            @operator,
            formula1,
            formula2,
            priority,
            stopIfTrue,
            dxfId);
        return new ParsedRule(
            id,
            ranges,
            type,
            @operator,
            formula1,
            formula2,
            workbookDifferentialStyles[dxfId],
            priority,
            stopIfTrue);
    }

    private static CellStylePatch ReadDifferentialStyle(
        XElement element,
        WorkbookTheme theme)
    {
        foreach (var child in element.Elements())
        {
            if (child.Name.Namespace != SpreadsheetNamespace ||
                child.Name.LocalName is not (
                    "font" or
                    "numFmt" or
                    "fill" or
                    "alignment" or
                    "border"))
            {
                throw new InvalidDataException(
                    $"Differential style contains unsupported element " +
                    $"'{child.Name}'.");
            }
        }

        var font = GetSingleChild(element, "font");
        var numberFormat = GetSingleChild(element, "numFmt");
        var fill = GetSingleChild(element, "fill");
        var alignment = GetSingleChild(element, "alignment");
        var border = GetSingleChild(element, "border");
        var patch = new CellStylePatch
        {
            FontFamily = font is null
                ? null
                : ReadOptionalStringValue(font, "name"),
            FontSize = font is null
                ? null
                : ReadOptionalDoubleValue(font, "sz"),
            FontWeight = font is null
                ? null
                : ReadOptionalOnOff(font, "b") is { } bold
                    ? bold ? 700 : 400
                    : null,
            FontItalic = font is null
                ? null
                : ReadOptionalOnOff(font, "i"),
            FontUnderline = font is null
                ? null
                : ReadOptionalUnderline(font),
            FontColor = font is null
                ? null
                : ReadOptionalColor(font, "color", theme),
            Fill = fill is null
                ? null
                : ReadFill(fill, theme),
            Border = border is null
                ? null
                : ReadBorder(border, theme),
            HorizontalAlignment = alignment is null
                ? null
                : ReadHorizontalAlignment(alignment),
            VerticalAlignment = alignment is null
                ? null
                : ReadVerticalAlignment(alignment),
            WrapText = alignment?.Attribute("wrapText") is null
                ? null
                : ParseOptionalBoolean(
                    (string?)alignment.Attribute("wrapText")),
            TextRotationDegrees = alignment?.Attribute("textRotation") is null
                ? null
                : ReadRotation(alignment),
            NumberFormatCode = numberFormat is null
                ? null
                : ReadRequiredAttribute(numberFormat, "formatCode"),
        };
        if (patch.IsEmpty)
        {
            throw new InvalidDataException(
                "Differential styles cannot be empty.");
        }

        // Workbook dxfs also serve Table formatting. Explicit default-valued
        // overrides (for example numFmt General) are valid and must retain
        // their original index even when they do not change CellStyle.Default.
        // The managed conditional-rule catalog enforces its own contract later.
        _ = new CellStyleCatalog().Intern(patch.Apply(CellStyle.Default));
        return patch;
    }

    private static XElement WriteDifferentialStyle(
        CellStylePatch patch,
        uint numberFormatId)
    {
        var result = new XElement(SpreadsheetNamespace + "dxf");
        var font = WriteFont(patch);
        if (font is not null)
        {
            result.Add(font);
        }

        if (patch.NumberFormatCode is not null)
        {
            result.Add(new XElement(
                SpreadsheetNamespace + "numFmt",
                new XAttribute("numFmtId", numberFormatId),
                new XAttribute("formatCode", patch.NumberFormatCode)));
        }

        if (patch.Fill is not null)
        {
            result.Add(WriteFill(patch.Fill));
        }

        var alignment = WriteAlignment(patch);
        if (alignment is not null)
        {
            result.Add(alignment);
        }

        if (patch.Border is not null)
        {
            result.Add(WriteBorder(patch.Border));
        }

        return result;
    }

    private static XElement? WriteFont(CellStylePatch patch)
    {
        if (patch.FontFamily is null &&
            patch.FontSize is null &&
            patch.FontWeight is null &&
            patch.FontItalic is null &&
            patch.FontUnderline is null &&
            patch.FontColor is null)
        {
            return null;
        }

        var font = new XElement(SpreadsheetNamespace + "font");
        if (patch.FontWeight is { } weight)
        {
            font.Add(WriteOnOff("b", weight >= 600));
        }

        if (patch.FontItalic is { } italic)
        {
            font.Add(WriteOnOff("i", italic));
        }

        if (patch.FontUnderline is { } underline)
        {
            font.Add(new XElement(
                SpreadsheetNamespace + "u",
                new XAttribute("val", underline ? "single" : "none")));
        }

        if (patch.FontSize is { } size)
        {
            font.Add(new XElement(
                SpreadsheetNamespace + "sz",
                new XAttribute(
                    "val",
                    size.ToString("R", CultureInfo.InvariantCulture))));
        }

        if (patch.FontColor is { } color)
        {
            font.Add(WriteColor("color", color));
        }

        if (patch.FontFamily is { } family)
        {
            font.Add(new XElement(
                SpreadsheetNamespace + "name",
                new XAttribute("val", family)));
        }

        return font;
    }

    private static XElement WriteFill(CellFillStyle fill)
    {
        var pattern = new XElement(
            SpreadsheetNamespace + "patternFill",
            new XAttribute(
                "patternType",
                fill.IsVisible ? "solid" : "none"));
        if (fill.IsVisible)
        {
            pattern.Add(WriteColor("fgColor", fill.Color));
            pattern.Add(new XElement(
                SpreadsheetNamespace + "bgColor",
                new XAttribute("indexed", 64)));
        }

        return new XElement(
            SpreadsheetNamespace + "fill",
            pattern);
    }

    private static XElement? WriteAlignment(CellStylePatch patch)
    {
        if (patch.HorizontalAlignment is null &&
            patch.VerticalAlignment is null &&
            patch.WrapText is null &&
            patch.TextRotationDegrees is null)
        {
            return null;
        }

        var alignment = new XElement(
            SpreadsheetNamespace + "alignment");
        if (patch.HorizontalAlignment is { } horizontal)
        {
            alignment.Add(new XAttribute(
                "horizontal",
                horizontal switch
                {
                    CellHorizontalAlignment.Left => "left",
                    CellHorizontalAlignment.Center => "center",
                    CellHorizontalAlignment.Right => "right",
                    _ => "general",
                }));
        }

        if (patch.VerticalAlignment is { } vertical)
        {
            alignment.Add(new XAttribute(
                "vertical",
                vertical switch
                {
                    CellVerticalAlignment.Top => "top",
                    CellVerticalAlignment.Center => "center",
                    _ => "bottom",
                }));
        }

        if (patch.WrapText is { } wrapText)
        {
            alignment.Add(new XAttribute(
                "wrapText",
                wrapText ? 1 : 0));
        }

        if (patch.TextRotationDegrees is { } rotation)
        {
            alignment.Add(new XAttribute(
                "textRotation",
                rotation < 0 ? 90 - rotation : rotation));
        }

        return alignment;
    }

    private static XElement WriteBorder(CellBorderStyle border) =>
        new(
            SpreadsheetNamespace + "border",
            WriteBorderSide("left", border.Left),
            WriteBorderSide("right", border.Right),
            WriteBorderSide("top", border.Top),
            WriteBorderSide("bottom", border.Bottom),
            new XElement(SpreadsheetNamespace + "diagonal"));

    private static XElement WriteBorderSide(
        string localName,
        CellBorderSide side)
    {
        var element = new XElement(
            SpreadsheetNamespace + localName);
        var style = WriteBorderStyle(side.Style);
        if (style is not null)
        {
            element.Add(new XAttribute("style", style));
            element.Add(WriteColor("color", side.Color));
        }

        return element;
    }

    private static XElement WriteOnOff(
        string localName,
        bool value) =>
        new(
            SpreadsheetNamespace + localName,
            new XAttribute("val", value ? 1 : 0));

    private static XElement WriteColor(
        string localName,
        ColorRgba color) =>
        new(
            SpreadsheetNamespace + localName,
            new XAttribute("rgb", ToArgb(color)));

    private static CellFillStyle ReadFill(
        XElement fill,
        WorkbookTheme theme)
    {
        var pattern = GetSingleChild(fill, "patternFill")
            ?? throw new InvalidDataException(
                "Differential fills must contain patternFill markup.");
        var patternType = (string?)pattern.Attribute("patternType");
        return patternType switch
        {
            "none" => new CellFillStyle(),
            "solid" => new CellFillStyle
            {
                IsVisible = true,
                Color = ReadExcelDifferentialFillColor(pattern, theme),
            },
            null or "" when
                ReadOptionalColor(pattern, "fgColor", theme) is not null ||
                ReadOptionalColor(pattern, "bgColor", theme) is not null =>
                new CellFillStyle
                {
                    IsVisible = true,
                    Color = ReadExcelDifferentialFillColor(pattern, theme),
                },
            null or "" => new CellFillStyle(),
            _ => throw new InvalidDataException(
                $"Differential fill pattern '{patternType}' is not supported."),
        };
    }

    private static ColorRgba ReadExcelDifferentialFillColor(
        XElement pattern,
        WorkbookTheme theme) =>
        ReadOptionalColor(pattern, "fgColor", theme) ??
        ReadOptionalColor(pattern, "bgColor", theme) ??
        throw new InvalidDataException(
            "Differential solid fill is missing an fgColor or bgColor color.");

    private static CellBorderStyle ReadBorder(
        XElement border,
        WorkbookTheme theme)
    {
        foreach (var child in border.Elements())
        {
            if (child.Name.Namespace != SpreadsheetNamespace ||
                child.Name.LocalName is not (
                    "left" or
                    "right" or
                    "top" or
                    "bottom" or
                    "diagonal"))
            {
                throw new InvalidDataException(
                    $"Differential border contains unsupported element " +
                    $"'{child.Name}'.");
            }
        }

        return new CellBorderStyle
        {
            Left = ReadBorderSide(GetSingleChild(border, "left"), theme),
            Right = ReadBorderSide(GetSingleChild(border, "right"), theme),
            Top = ReadBorderSide(GetSingleChild(border, "top"), theme),
            Bottom = ReadBorderSide(GetSingleChild(border, "bottom"), theme),
        };
    }

    private static CellBorderSide ReadBorderSide(
        XElement? element,
        WorkbookTheme theme)
    {
        var styleText = (string?)element?.Attribute("style");
        if (string.IsNullOrWhiteSpace(styleText))
        {
            return new CellBorderSide();
        }

        var style = ParseBorderStyle(styleText);
        return new CellBorderSide
        {
            Style = style,
            Width = style switch
            {
                CellBorderLineStyle.Medium => 2d,
                CellBorderLineStyle.Thick => 3d,
                CellBorderLineStyle.DoubleLine => 2d,
                _ => 1d,
            },
            Color = element is null
                ? ColorRgba.Black
                : ReadOptionalColor(element, "color", theme)
                    ?? ColorRgba.Black,
        };
    }

    private static CellHorizontalAlignment? ReadHorizontalAlignment(
        XElement alignment)
    {
        var value = (string?)alignment.Attribute("horizontal");
        return value switch
        {
            null => null,
            "general" => CellHorizontalAlignment.General,
            "left" => CellHorizontalAlignment.Left,
            "center" => CellHorizontalAlignment.Center,
            "right" => CellHorizontalAlignment.Right,
            _ => throw new InvalidDataException(
                $"Differential horizontal alignment '{value}' is not supported."),
        };
    }

    private static CellVerticalAlignment? ReadVerticalAlignment(
        XElement alignment)
    {
        var value = (string?)alignment.Attribute("vertical");
        return value switch
        {
            null => null,
            "top" => CellVerticalAlignment.Top,
            "center" => CellVerticalAlignment.Center,
            "bottom" => CellVerticalAlignment.Bottom,
            _ => throw new InvalidDataException(
                $"Differential vertical alignment '{value}' is not supported."),
        };
    }

    private static int ReadRotation(XElement alignment)
    {
        var raw = ParseNonNegativeInt(
            (string?)alignment.Attribute("textRotation"),
            "differential text rotation");
        if (raw > 180)
        {
            throw new InvalidDataException(
                "Differential text rotation must be between 0 and 180.");
        }

        return raw <= 90 ? raw : 90 - raw;
    }

    private static string? ReadOptionalStringValue(
        XElement parent,
        string localName)
    {
        var element = GetSingleChild(parent, localName);
        return element is null
            ? null
            : ReadRequiredAttribute(element, "val");
    }

    private static double? ReadOptionalDoubleValue(
        XElement parent,
        string localName)
    {
        var element = GetSingleChild(parent, localName);
        if (element is null)
        {
            return null;
        }

        var raw = ReadRequiredAttribute(element, "val");
        if (!double.TryParse(
                raw,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value) ||
            !double.IsFinite(value) ||
            value <= 0d)
        {
            throw new InvalidDataException(
                $"Differential font size '{raw}' is invalid.");
        }

        return value;
    }

    private static bool? ReadOptionalOnOff(
        XElement parent,
        string localName)
    {
        var element = GetSingleChild(parent, localName);
        return element is null
            ? null
            : ParseOptionalBoolean((string?)element.Attribute("val"), true);
    }

    private static bool? ReadOptionalUnderline(XElement font)
    {
        var element = GetSingleChild(font, "u");
        if (element is null)
        {
            return null;
        }

        var value = (string?)element.Attribute("val");
        return value switch
        {
            null or "single" => true,
            "none" => false,
            _ => throw new InvalidDataException(
                $"Differential underline style '{value}' is not supported."),
        };
    }

    private static ColorRgba? ReadOptionalColor(
        XElement parent,
        string localName,
        WorkbookTheme theme)
    {
        var element = GetSingleChild(parent, localName);
        return element is null
            ? null
            : ReadColor(element, theme);
    }

    private static ColorRgba ReadRequiredColor(
        XElement parent,
        string localName,
        WorkbookTheme theme) =>
        ReadOptionalColor(parent, localName, theme)
        ?? throw new InvalidDataException(
            $"Differential style is missing required {localName} color.");

    private static ColorRgba ReadColor(
        XElement color,
        WorkbookTheme theme)
    {
        var rgb = (string?)color.Attribute("rgb");
        if (rgb is null && uint.TryParse(
                (string?)color.Attribute("theme"),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var themeIndex) &&
            themeIndex <= 11U)
        {
            var themeColor = themeIndex switch
            {
                0U => WorkbookThemeColor.Light1,
                1U => WorkbookThemeColor.Dark1,
                2U => WorkbookThemeColor.Light2,
                3U => WorkbookThemeColor.Dark2,
                4U => WorkbookThemeColor.Accent1,
                5U => WorkbookThemeColor.Accent2,
                6U => WorkbookThemeColor.Accent3,
                7U => WorkbookThemeColor.Accent4,
                8U => WorkbookThemeColor.Accent5,
                9U => WorkbookThemeColor.Accent6,
                10U => WorkbookThemeColor.Hyperlink,
                _ => WorkbookThemeColor.FollowedHyperlink,
            };
            var tintText = (string?)color.Attribute("tint");
            var tint = 0d;
            if (tintText is not null &&
                (!double.TryParse(
                    tintText,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out tint) ||
                 !double.IsFinite(tint) ||
                 tint is < -1d or > 1d))
            {
                throw new InvalidDataException(
                    $"Differential theme tint '{tintText}' is invalid.");
            }
            return TableStyleColor.FromTheme(themeColor, tint).Resolve(theme);
        }
        if (string.IsNullOrWhiteSpace(rgb))
        {
            throw new InvalidDataException(
                "Only explicit RGB differential colors are currently supported.");
        }

        var normalized = rgb.Length switch
        {
            6 => $"FF{rgb}",
            8 => rgb,
            _ => null,
        };
        if (normalized is null ||
            !uint.TryParse(
                normalized,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var argb))
        {
            throw new InvalidDataException(
                $"Differential RGB color '{rgb}' is invalid.");
        }

        return new ColorRgba(
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF),
            (byte)((argb >> 24) & 0xFF));
    }

    private static CellRange[] ParseRanges(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                "Conditional formatting is missing its sqref range list.");
        }

        var tokens = value.Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        if (tokens.Length == 0 ||
            tokens.Length > ConditionalFormattingRule.MaxRangesPerRule)
        {
            throw new InvalidDataException(
                $"Conditional-formatting sqref must contain between 1 and " +
                $"{ConditionalFormattingRule.MaxRangesPerRule} ranges.");
        }

        var ranges = new CellRange[tokens.Length];
        for (var index = 0; index < tokens.Length; index++)
        {
            ranges[index] = ParseRange(tokens[index]);
        }

        return ranges
            .Distinct()
            .OrderBy(static range => range.Top)
            .ThenBy(static range => range.Left)
            .ThenBy(static range => range.Bottom)
            .ThenBy(static range => range.Right)
            .ToArray();
    }

    private static CellRange ParseRange(string token)
    {
        var separator = token.IndexOf(':');
        if (separator < 0)
        {
            if (!CellAddress.TryParseA1(token, out var address))
            {
                throw new InvalidDataException(
                    $"Conditional-formatting range '{token}' is invalid.");
            }

            return new CellRange(address, address);
        }

        if (separator == 0 ||
            separator != token.LastIndexOf(':') ||
            separator == token.Length - 1 ||
            !CellAddress.TryParseA1(token[..separator], out var first) ||
            !CellAddress.TryParseA1(token[(separator + 1)..], out var second))
        {
            throw new InvalidDataException(
                $"Conditional-formatting range '{token}' is invalid.");
        }

        return new CellRange(first, second);
    }

    private static ConditionalFormattingRuleType ParseRuleType(string? value) =>
        value switch
        {
            "cellIs" => ConditionalFormattingRuleType.CellIs,
            "expression" => ConditionalFormattingRuleType.Expression,
            null or "" => throw new InvalidDataException(
                "Conditional-formatting rule type is missing."),
            _ => throw new InvalidDataException(
                $"Conditional-formatting rule type '{value}' is not supported."),
        };

    private static string WriteRuleType(
        ConditionalFormattingRuleType type) =>
        type switch
        {
            ConditionalFormattingRuleType.CellIs => "cellIs",
            ConditionalFormattingRuleType.Expression => "expression",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

    private static ConditionalFormattingOperator ParseOperator(string? value) =>
        value switch
        {
            "equal" => ConditionalFormattingOperator.Equal,
            "notEqual" => ConditionalFormattingOperator.NotEqual,
            "greaterThan" => ConditionalFormattingOperator.GreaterThan,
            "greaterThanOrEqual" =>
                ConditionalFormattingOperator.GreaterThanOrEqual,
            "lessThan" => ConditionalFormattingOperator.LessThan,
            "lessThanOrEqual" =>
                ConditionalFormattingOperator.LessThanOrEqual,
            "between" => ConditionalFormattingOperator.Between,
            "notBetween" => ConditionalFormattingOperator.NotBetween,
            null or "" => throw new InvalidDataException(
                "Cell-is conditional-formatting operator is missing."),
            _ => throw new InvalidDataException(
                $"Conditional-formatting operator '{value}' is not supported."),
        };

    private static string WriteOperator(
        ConditionalFormattingOperator @operator) =>
        @operator switch
        {
            ConditionalFormattingOperator.Equal => "equal",
            ConditionalFormattingOperator.NotEqual => "notEqual",
            ConditionalFormattingOperator.GreaterThan => "greaterThan",
            ConditionalFormattingOperator.GreaterThanOrEqual =>
                "greaterThanOrEqual",
            ConditionalFormattingOperator.LessThan => "lessThan",
            ConditionalFormattingOperator.LessThanOrEqual =>
                "lessThanOrEqual",
            ConditionalFormattingOperator.Between => "between",
            ConditionalFormattingOperator.NotBetween => "notBetween",
            _ => throw new ArgumentOutOfRangeException(nameof(@operator)),
        };

    private static CellBorderLineStyle ParseBorderStyle(string value) =>
        value switch
        {
            "thin" => CellBorderLineStyle.Thin,
            "medium" => CellBorderLineStyle.Medium,
            "thick" => CellBorderLineStyle.Thick,
            "dashed" => CellBorderLineStyle.Dashed,
            "dotted" => CellBorderLineStyle.Dotted,
            "double" => CellBorderLineStyle.DoubleLine,
            _ => throw new InvalidDataException(
                $"Differential border style '{value}' is not supported."),
        };

    private static string? WriteBorderStyle(CellBorderLineStyle style) =>
        style switch
        {
            CellBorderLineStyle.None => null,
            CellBorderLineStyle.Thin => "thin",
            CellBorderLineStyle.Medium => "medium",
            CellBorderLineStyle.Thick => "thick",
            CellBorderLineStyle.Dashed => "dashed",
            CellBorderLineStyle.Dotted => "dotted",
            CellBorderLineStyle.DoubleLine => "double",
            _ => throw new ArgumentOutOfRangeException(nameof(style)),
        };

    private static Guid CreateDeterministicIdentifier(
        string worksheetName,
        IEnumerable<CellRange> ranges,
        ConditionalFormattingRuleType type,
        ConditionalFormattingOperator @operator,
        string formula1,
        string? formula2,
        int priority,
        bool stopIfTrue,
        int dxfId)
    {
        var text = string.Join(
            "\u001F",
            worksheetName,
            string.Join(" ", ranges),
            type,
            @operator,
            formula1,
            formula2 ?? string.Empty,
            priority.ToString(CultureInfo.InvariantCulture),
            stopIfTrue ? "1" : "0",
            dxfId.ToString(CultureInfo.InvariantCulture));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static XElement? GetSingleChild(
        XElement parent,
        string localName)
    {
        var elements = parent
            .Elements(SpreadsheetNamespace + localName)
            .ToArray();
        if (elements.Length > 1)
        {
            throw new InvalidDataException(
                $"OpenXml element '{parent.Name}' contains duplicate " +
                $"'{localName}' children.");
        }

        return elements.FirstOrDefault();
    }

    private static string ReadRequiredAttribute(
        XElement element,
        string localName)
    {
        var value = (string?)element.Attribute(localName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"OpenXml element '{element.Name}' is missing required " +
                $"attribute '{localName}'.");
        }

        return value;
    }

    private static int ParsePositiveInt(
        string? value,
        string description)
    {
        var parsed = ParseNonNegativeInt(value, description);
        if (parsed == 0)
        {
            throw new InvalidDataException(
                $"The {description} must be greater than zero.");
        }

        return parsed;
    }

    private static int ParseNonNegativeInt(
        string? value,
        string description)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var parsed) ||
            parsed < 0)
        {
            throw new InvalidDataException(
                $"The {description} '{value}' is invalid.");
        }

        return parsed;
    }

    private static bool ParseOptionalBoolean(
        string? value,
        bool defaultValue = false) =>
        value switch
        {
            null or "" => defaultValue,
            "1" or "true" or "on" => true,
            "0" or "false" or "off" => false,
            _ => throw new InvalidDataException(
                $"OpenXml boolean value '{value}' is invalid."),
        };

    private static void ValidateDeclaredCount(
        XElement container,
        int actualCount,
        string description)
    {
        var raw = (string?)container.Attribute("count");
        if (raw is null)
        {
            return;
        }

        if (!int.TryParse(
                raw,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var declared) ||
            declared != actualCount)
        {
            throw new InvalidDataException(
                $"The declared {description} count '{raw}' does not match " +
                $"the actual count {actualCount}.");
        }
    }

    private static string TrimFormulaPrefix(string formula) =>
        formula.StartsWith('=')
            ? formula[1..]
            : formula;

    private static string ToArgb(ColorRgba color) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}");

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
        using var stream = part.GetStream(
            FileMode.Open,
            FileAccess.Read);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false,
                MaxCharactersInDocument = MaxXmlCharacters,
            });
        try
        {
            return XDocument.Load(
                reader,
                LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException(
                $"The OpenXml part '{part.Uri}' does not contain valid XML.",
                exception);
        }
    }

    private static void SavePartXml(
        OpenXmlPart part,
        XDocument document)
    {
        using var stream = part.GetStream(
            FileMode.Create,
            FileAccess.Write);
        using var writer = XmlWriter.Create(
            stream,
            new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                Indent = false,
                CloseOutput = false,
            });
        document.Save(writer);
    }

    private static Dictionary<string, int> CreateOrder(
        IReadOnlyList<string> elementNames)
    {
        var result = new Dictionary<string, int>(
            elementNames.Count,
            StringComparer.Ordinal);
        for (var index = 0; index < elementNames.Count; index++)
        {
            result.Add(elementNames[index], index);
        }

        return result;
    }

    private sealed record ParsedRule(
        Guid Id,
        CellRange[] Ranges,
        ConditionalFormattingRuleType Type,
        ConditionalFormattingOperator Operator,
        string Formula1,
        string? Formula2,
        CellStylePatch Style,
        int Priority,
        bool StopIfTrue);
}

internal sealed class OpenXmlConditionalFormattingExportPlan
{
    private readonly IReadOnlyDictionary<
        NeraWorksheet,
        IReadOnlyDictionary<int, uint>> _worksheetMaps;
    private readonly IReadOnlyDictionary<
        NeraWorksheet,
        IReadOnlyDictionary<SpreadsheetColorFilter, uint>> _worksheetColorMaps;

    public OpenXmlConditionalFormattingExportPlan(
        IReadOnlyDictionary<
            NeraWorksheet,
            IReadOnlyDictionary<int, uint>> worksheetMaps,
        IReadOnlyDictionary<NeraWorksheet, IReadOnlyDictionary<SpreadsheetColorFilter, uint>> worksheetColorMaps)
    {
        _worksheetMaps = worksheetMaps;
        _worksheetColorMaps = worksheetColorMaps;
    }

    public uint GetDifferentialStyleId(
        NeraWorksheet worksheet,
        int localStyleId)
    {
        if (!_worksheetMaps.TryGetValue(worksheet, out var localMap) ||
            !localMap.TryGetValue(localStyleId, out var globalStyleId))
        {
            throw new InvalidOperationException(
                "Conditional-formatting export plan does not contain the " +
                "requested differential style.");
        }

        return globalStyleId;
    }

    public uint GetColorStyleId(NeraWorksheet worksheet, SpreadsheetColorFilter color)
    {
        if (!_worksheetColorMaps.TryGetValue(worksheet, out var map) || !map.TryGetValue(color, out var id))
        {
            throw new InvalidOperationException("The export plan does not contain the requested filter color.");
        }
        return id;
    }
}
