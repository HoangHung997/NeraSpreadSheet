using System.Globalization;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using NeraSpreadSheet.Core;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;
using NeraWorksheet = NeraSpreadSheet.Core.Worksheet;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlWorksheetPrintSettingsCodec
{
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static readonly string[] WorksheetElementOrder =
    [
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
    ];

    public static void Read(
        SpreadsheetDocument document,
        NeraWorkbook workbook,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(workbook);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException(
                "The XLSX package does not contain a workbook part.");
        var sheets = workbookPart.Workbook?
            .GetFirstChild<Sheets>()?
            .Elements<Sheet>()
            .ToArray() ?? [];
        var definedNames = ReadDefinedNames(workbookPart);
        var count = Math.Min(sheets.Length, workbook.Worksheets.Count);
        for (var index = 0; index < count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relationshipId = sheets[index].Id?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId) ||
                workbookPart.GetPartById(relationshipId) is not
                    WorksheetPart worksheetPart)
            {
                continue;
            }

            var worksheet = workbook.Worksheets[index];
            var settings = ReadWorksheet(
                worksheetPart,
                worksheet.Name,
                definedNames.GetValueOrDefault(index));
            worksheet.SetPrintSettings(settings);
        }
    }

    public static byte[] Patch(
        byte[] packageBytes,
        NeraWorkbook workbook,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(packageBytes);
        ArgumentNullException.ThrowIfNull(workbook);
        using var buffer = new MemoryStream();
        buffer.Write(packageBytes);
        buffer.Position = 0L;
        using (var document = SpreadsheetDocument.Open(buffer, true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new InvalidDataException(
                    "The XLSX package does not contain a workbook part.");
            var sheets = workbookPart.Workbook?
                .GetFirstChild<Sheets>()?
                .Elements<Sheet>()
                .ToArray() ?? [];
            if (sheets.Length != workbook.Worksheets.Count)
            {
                throw new InvalidOperationException(
                    "Worksheet print settings cannot be written because " +
                    "the package/workbook worksheet topology differs.");
            }

            for (var index = 0; index < sheets.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relationshipId = sheets[index].Id?.Value;
                if (string.IsNullOrWhiteSpace(relationshipId) ||
                    workbookPart.GetPartById(relationshipId) is not
                        WorksheetPart worksheetPart)
                {
                    throw new InvalidDataException(
                        "The XLSX worksheet relationship is invalid.");
                }
                WriteWorksheet(
                    worksheetPart,
                    workbook.Worksheets[index].GetPrintSettings());
            }

            WriteDefinedNames(workbookPart, workbook);
            workbookPart.Workbook?.Save();
        }
        return buffer.ToArray();
    }

    private static WorksheetPrintSettings ReadWorksheet(
        WorksheetPart worksheetPart,
        string worksheetName,
        DefinedPrintNames? definedNames)
    {
        var document = LoadPartXml(worksheetPart);
        var root = document.Root
            ?? throw new InvalidDataException(
                "The XLSX worksheet XML has no root element.");
        if (root.Name != SpreadsheetNamespace + "worksheet")
        {
            throw new InvalidDataException(
                "The XLSX worksheet contains invalid root markup.");
        }

        var margins = ReadMargins(
            root.Element(SpreadsheetNamespace + "pageMargins"));
        var pageSetup = root.Element(SpreadsheetNamespace + "pageSetup");
        var printOptions = root.Element(
            SpreadsheetNamespace + "printOptions");
        var headerFooter = root.Element(
            SpreadsheetNamespace + "headerFooter");
        CellRange? printArea =
            definedNames?.PrintArea is { Length: > 0 } area
                ? ParsePrintArea(area, worksheetName)
                : null;
        var repeatTitles = definedNames?.PrintTitles is { Length: > 0 } titles
            ? ParsePrintTitles(titles, worksheetName, printArea)
            : default;

        var orientation = string.Equals(
                (string?)pageSetup?.Attribute("orientation"),
                "landscape",
                StringComparison.OrdinalIgnoreCase)
            ? SpreadsheetPageOrientation.Landscape
            : SpreadsheetPageOrientation.Portrait;
        var scale = ReadPositiveDouble(
            pageSetup?.Attribute("scale"),
            100d);
        if (scale is < 10d or > 400d)
        {
            throw new InvalidDataException(
                "The XLSX page scale must be between 10 and 400 percent.");
        }
        var fitWide = ReadPositiveInt(pageSetup?.Attribute("fitToWidth"));
        var fitTall = ReadPositiveInt(pageSetup?.Attribute("fitToHeight"));
        var paper = ReadPaperSize(pageSetup?.Attribute("paperSize"));
        return new WorksheetPrintSettings
        {
            PrintArea = printArea,
            PageSetup = new SpreadsheetPageSetup
            {
                PaperSize = paper,
                Orientation = orientation,
                Margins = margins,
                ScalePercent = scale,
                FitToPagesWide = fitWide,
                FitToPagesTall = fitTall,
                RepeatTitles = repeatTitles,
                CenterHorizontally = ReadBoolean(
                    printOptions?.Attribute("horizontalCentered")),
                CenterVertically = ReadBoolean(
                    printOptions?.Attribute("verticalCentered")),
                PrintGridlines = ReadBoolean(
                    printOptions?.Attribute("gridLines")),
                PrintHeadings = ReadBoolean(
                    printOptions?.Attribute("headings")),
                OddHeader = headerFooter?
                    .Element(SpreadsheetNamespace + "oddHeader")?
                    .Value,
                OddFooter = headerFooter?
                    .Element(SpreadsheetNamespace + "oddFooter")?
                    .Value,
            },
        };
    }

    private static void WriteWorksheet(
        WorksheetPart worksheetPart,
        WorksheetPrintSettings settings)
    {
        var document = LoadPartXml(worksheetPart);
        var root = document.Root
            ?? throw new InvalidDataException(
                "The XLSX worksheet XML has no root element.");
        var setup = settings.PageSetup;

        var printOptions = GetOrCreateElement(root, "printOptions");
        SetBooleanAttribute(
            printOptions,
            "horizontalCentered",
            setup.CenterHorizontally);
        SetBooleanAttribute(
            printOptions,
            "verticalCentered",
            setup.CenterVertically);
        SetBooleanAttribute(
            printOptions,
            "gridLines",
            setup.PrintGridlines);
        SetBooleanAttribute(
            printOptions,
            "headings",
            setup.PrintHeadings);
        if (!printOptions.HasAttributes && !printOptions.HasElements)
        {
            printOptions.Remove();
        }

        var margins = GetOrCreateElement(root, "pageMargins");
        margins.SetAttributeValue(
            "left",
            FormatDouble(setup.Margins.LeftInches));
        margins.SetAttributeValue(
            "right",
            FormatDouble(setup.Margins.RightInches));
        margins.SetAttributeValue(
            "top",
            FormatDouble(setup.Margins.TopInches));
        margins.SetAttributeValue(
            "bottom",
            FormatDouble(setup.Margins.BottomInches));
        margins.SetAttributeValue(
            "header",
            FormatDouble(setup.Margins.HeaderInches));
        margins.SetAttributeValue(
            "footer",
            FormatDouble(setup.Margins.FooterInches));

        var pageSetup = GetOrCreateElement(root, "pageSetup");
        pageSetup.SetAttributeValue(
            "orientation",
            setup.Orientation == SpreadsheetPageOrientation.Landscape
                ? "landscape"
                : "portrait");
        pageSetup.SetAttributeValue(
            "scale",
            Math.Clamp(
                (int)Math.Round(
                    setup.ScalePercent,
                    MidpointRounding.AwayFromZero),
                10,
                400));
        pageSetup.SetAttributeValue(
            "fitToWidth",
            setup.FitToPagesWide);
        pageSetup.SetAttributeValue(
            "fitToHeight",
            setup.FitToPagesTall);
        pageSetup.SetAttributeValue(
            "paperSize",
            GetPaperSizeCode(setup.PaperSize));

        var headerFooter = root.Element(
            SpreadsheetNamespace + "headerFooter");
        if (!string.IsNullOrEmpty(setup.OddHeader) ||
            !string.IsNullOrEmpty(setup.OddFooter) ||
            headerFooter is not null)
        {
            headerFooter ??= GetOrCreateElement(root, "headerFooter");
            SetChildText(headerFooter, "oddHeader", setup.OddHeader);
            SetChildText(headerFooter, "oddFooter", setup.OddFooter);
            if (!headerFooter.HasAttributes && !headerFooter.HasElements)
            {
                headerFooter.Remove();
            }
        }

        SavePartXml(worksheetPart, document);
    }

    private static Dictionary<int, DefinedPrintNames> ReadDefinedNames(
        WorkbookPart workbookPart)
    {
        var document = LoadPartXml(workbookPart);
        var result = new Dictionary<int, DefinedPrintNames>();
        foreach (var element in document.Root?
                     .Element(SpreadsheetNamespace + "definedNames")?
                     .Elements(SpreadsheetNamespace + "definedName") ?? [])
        {
            if (!int.TryParse(
                    (string?)element.Attribute("localSheetId"),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var sheetIndex) ||
                sheetIndex < 0)
            {
                continue;
            }
            var name = (string?)element.Attribute("name");
            if (!string.Equals(
                    name,
                    "_xlnm.Print_Area",
                    StringComparison.Ordinal) &&
                !string.Equals(
                    name,
                    "_xlnm.Print_Titles",
                    StringComparison.Ordinal))
            {
                continue;
            }

            result.TryGetValue(sheetIndex, out var current);
            current ??= new DefinedPrintNames();
            if (name == "_xlnm.Print_Area")
            {
                current.PrintArea = element.Value;
            }
            else
            {
                current.PrintTitles = element.Value;
            }
            result[sheetIndex] = current;
        }
        return result;
    }

    private static void WriteDefinedNames(
        WorkbookPart workbookPart,
        NeraWorkbook workbook)
    {
        var document = LoadPartXml(workbookPart);
        var root = document.Root
            ?? throw new InvalidDataException(
                "The XLSX workbook XML has no root element.");
        var container = root.Element(
            SpreadsheetNamespace + "definedNames");
        container ??= new XElement(
            SpreadsheetNamespace + "definedNames");
        if (container.Parent is null)
        {
            var calculationProperties = root.Element(
                SpreadsheetNamespace + "calcPr");
            var extensionList = root.Element(
                SpreadsheetNamespace + "extLst");
            if (calculationProperties is not null)
            {
                calculationProperties.AddBeforeSelf(container);
            }
            else if (extensionList is not null)
            {
                extensionList.AddBeforeSelf(container);
            }
            else
            {
                root.Add(container);
            }
        }

        for (var index = 0; index < workbook.Worksheets.Count; index++)
        {
            var localIndex = index.ToString(CultureInfo.InvariantCulture);
            container.Elements(SpreadsheetNamespace + "definedName")
                .Where(element =>
                    (string?)element.Attribute("localSheetId") == localIndex &&
                    ((string?)element.Attribute("name") ==
                        "_xlnm.Print_Area" ||
                     (string?)element.Attribute("name") ==
                        "_xlnm.Print_Titles"))
                .Remove();

            var worksheet = workbook.Worksheets[index];
            var settings = worksheet.GetPrintSettings();
            if (settings.PrintArea is { } printArea)
            {
                container.Add(new XElement(
                    SpreadsheetNamespace + "definedName",
                    new XAttribute("name", "_xlnm.Print_Area"),
                    new XAttribute("localSheetId", localIndex),
                    FormatPrintArea(worksheet.Name, printArea)));
            }
            var titles = FormatPrintTitles(
                worksheet.Name,
                settings.PageSetup.RepeatTitles);
            if (titles is not null)
            {
                container.Add(new XElement(
                    SpreadsheetNamespace + "definedName",
                    new XAttribute("name", "_xlnm.Print_Titles"),
                    new XAttribute("localSheetId", localIndex),
                    titles));
            }
        }

        if (!container.HasElements)
        {
            container.Remove();
        }
        SavePartXml(workbookPart, document);
    }

    private static SpreadsheetPageMargins ReadMargins(XElement? element)
    {
        var defaults = SpreadsheetPageMargins.Normal;
        return new SpreadsheetPageMargins(
            ReadNonNegativeDouble(
                element?.Attribute("left"),
                defaults.LeftInches),
            ReadNonNegativeDouble(
                element?.Attribute("right"),
                defaults.RightInches),
            ReadNonNegativeDouble(
                element?.Attribute("top"),
                defaults.TopInches),
            ReadNonNegativeDouble(
                element?.Attribute("bottom"),
                defaults.BottomInches),
            ReadNonNegativeDouble(
                element?.Attribute("header"),
                defaults.HeaderInches),
            ReadNonNegativeDouble(
                element?.Attribute("footer"),
                defaults.FooterInches));
    }

    private static SpreadsheetPaperSize ReadPaperSize(XAttribute? attribute)
    {
        if (!int.TryParse(
                attribute?.Value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var code))
        {
            return SpreadsheetPaperSize.A4;
        }
        return code switch
        {
            1 => SpreadsheetPaperSize.Letter,
            5 => SpreadsheetPaperSize.Legal,
            8 => SpreadsheetPaperSize.A3,
            9 => SpreadsheetPaperSize.A4,
            _ => SpreadsheetPaperSize.A4,
        };
    }

    private static int? GetPaperSizeCode(SpreadsheetPaperSize size)
    {
        if (Approximately(size, SpreadsheetPaperSize.Letter))
        {
            return 1;
        }
        if (Approximately(size, SpreadsheetPaperSize.Legal))
        {
            return 5;
        }
        if (Approximately(size, SpreadsheetPaperSize.A3))
        {
            return 8;
        }
        if (Approximately(size, SpreadsheetPaperSize.A4))
        {
            return 9;
        }
        return null;
    }

    private static bool Approximately(
        SpreadsheetPaperSize left,
        SpreadsheetPaperSize right) =>
        Math.Abs(left.WidthInches - right.WidthInches) < 0.0001d &&
        Math.Abs(left.HeightInches - right.HeightInches) < 0.0001d;

    private static CellRange ParsePrintArea(
        string formula,
        string worksheetName)
    {
        var terms = SplitFormulaTerms(formula);
        if (terms.Count != 1)
        {
            throw new InvalidDataException(
                "NeraSpreadSheet currently supports one print area per worksheet.");
        }
        var reference = ExtractLocalReference(terms[0], worksheetName);
        if (!TryParseAbsoluteCellRange(reference, out var range))
        {
            throw new InvalidDataException(
                "The XLSX print-area defined name is invalid.");
        }
        return range;
    }

    private static SpreadsheetRepeatTitles ParsePrintTitles(
        string formula,
        string worksheetName,
        CellRange? printArea)
    {
        CellRange? rows = null;
        CellRange? columns = null;
        foreach (var term in SplitFormulaTerms(formula))
        {
            var reference = ExtractLocalReference(term, worksheetName)
                .Replace("$", string.Empty, StringComparison.Ordinal);
            var parts = reference.Split(':');
            if (parts.Length != 2)
            {
                throw new InvalidDataException(
                    "The XLSX print-title defined name is invalid.");
            }
            if (int.TryParse(
                    parts[0],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var firstRow) &&
                int.TryParse(
                    parts[1],
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var lastRow))
            {
                firstRow--;
                lastRow--;
                if (firstRow < 0 || lastRow < firstRow ||
                    lastRow >= SpreadsheetLimits.MaxRows)
                {
                    throw new InvalidDataException(
                        "The XLSX repeated-row title range is invalid.");
                }
                rows = new CellRange(
                    new CellAddress(
                        firstRow,
                        printArea?.Left ?? 0),
                    new CellAddress(
                        lastRow,
                        printArea?.Right ??
                            SpreadsheetLimits.MaxColumns - 1));
                continue;
            }
            if (TryParseColumn(parts[0], out var firstColumn) &&
                TryParseColumn(parts[1], out var lastColumn) &&
                lastColumn >= firstColumn)
            {
                columns = new CellRange(
                    new CellAddress(
                        printArea?.Top ?? 0,
                        firstColumn),
                    new CellAddress(
                        printArea?.Bottom ??
                            SpreadsheetLimits.MaxRows - 1,
                        lastColumn));
                continue;
            }
            throw new InvalidDataException(
                "The XLSX print-title defined name is invalid.");
        }
        return new SpreadsheetRepeatTitles(rows, columns);
    }

    private static string FormatPrintArea(
        string worksheetName,
        CellRange range) =>
        $"{QuoteSheetName(worksheetName)}!" +
        $"{ToAbsoluteA1(range.TopLeft)}:" +
        $"{ToAbsoluteA1(range.BottomRight)}";

    private static string? FormatPrintTitles(
        string worksheetName,
        SpreadsheetRepeatTitles titles)
    {
        var prefix = $"{QuoteSheetName(worksheetName)}!";
        var terms = new List<string>(2);
        if (titles.Rows is { } rows)
        {
            terms.Add(
                $"{prefix}${rows.Top + 1}:${rows.Bottom + 1}");
        }
        if (titles.Columns is { } columns)
        {
            terms.Add(
                $"{prefix}${ToColumnName(columns.Left)}:" +
                $"${ToColumnName(columns.Right)}");
        }
        return terms.Count == 0
            ? null
            : string.Join(',', terms);
    }

    private static List<string> SplitFormulaTerms(string formula)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inSheetQuote = false;
        for (var index = 0; index < formula.Length; index++)
        {
            var character = formula[index];
            if (character == '\'')
            {
                current.Append(character);
                if (inSheetQuote && index + 1 < formula.Length &&
                    formula[index + 1] == '\'')
                {
                    current.Append(formula[++index]);
                }
                else
                {
                    inSheetQuote = !inSheetQuote;
                }
                continue;
            }
            if (character == ',' && !inSheetQuote)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }
            current.Append(character);
        }
        if (inSheetQuote)
        {
            throw new InvalidDataException(
                "The XLSX defined name contains an unterminated sheet quote.");
        }
        if (current.Length > 0)
        {
            result.Add(current.ToString().Trim());
        }
        return result;
    }

    private static string ExtractLocalReference(
        string term,
        string worksheetName)
    {
        var separator = FindLastUnquoted(term, '!');
        if (separator < 0)
        {
            return term.Trim();
        }
        var sheet = UnquoteSheetName(term[..separator].Trim());
        if (!string.Equals(
                sheet,
                worksheetName,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "A worksheet print defined name references another worksheet.");
        }
        return term[(separator + 1)..].Trim();
    }

    private static int FindLastUnquoted(string value, char requested)
    {
        var inQuote = false;
        var result = -1;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '\'')
            {
                if (inQuote && index + 1 < value.Length &&
                    value[index + 1] == '\'')
                {
                    index++;
                }
                else
                {
                    inQuote = !inQuote;
                }
                continue;
            }
            if (!inQuote && value[index] == requested)
            {
                result = index;
            }
        }
        return result;
    }

    private static string QuoteSheetName(string name) =>
        $"'{name.Replace("'", "''", StringComparison.Ordinal)}'";

    private static string UnquoteSheetName(string name)
    {
        if (name.Length >= 2 && name[0] == '\'' && name[^1] == '\'')
        {
            return name[1..^1].Replace(
                "''",
                "'",
                StringComparison.Ordinal);
        }
        return name;
    }

    private static bool TryParseAbsoluteCellRange(
        string reference,
        out CellRange range)
    {
        var normalized = reference.Replace(
            "$",
            string.Empty,
            StringComparison.Ordinal);
        var parts = normalized.Split(':');
        if (parts.Length == 2 &&
            CellAddress.TryParseA1(parts[0], out var first) &&
            CellAddress.TryParseA1(parts[1], out var second) &&
            second.RowIndex >= first.RowIndex &&
            second.ColumnIndex >= first.ColumnIndex)
        {
            range = new CellRange(first, second);
            return true;
        }
        range = default;
        return false;
    }

    private static bool TryParseColumn(
        string value,
        out int columnIndex)
    {
        columnIndex = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        foreach (var character in value)
        {
            if (!char.IsAsciiLetter(character))
            {
                columnIndex = 0;
                return false;
            }
            columnIndex = checked(
                (columnIndex * 26) +
                (char.ToUpperInvariant(character) - 'A' + 1));
        }
        columnIndex--;
        return columnIndex >= 0 &&
               columnIndex < SpreadsheetLimits.MaxColumns;
    }

    private static string ToAbsoluteA1(CellAddress address) =>
        $"${ToColumnName(address.ColumnIndex)}${address.RowIndex + 1}";

    private static string ToColumnName(int index)
    {
        if (index < 0 || index >= SpreadsheetLimits.MaxColumns)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }
        var result = new StringBuilder();
        var current = index + 1;
        while (current > 0)
        {
            current--;
            result.Insert(0, (char)('A' + (current % 26)));
            current /= 26;
        }
        return result.ToString();
    }

    private static XElement GetOrCreateElement(
        XElement root,
        string localName)
    {
        var name = SpreadsheetNamespace + localName;
        var existing = root.Element(name);
        if (existing is not null)
        {
            return existing;
        }
        var created = new XElement(name);
        var requestedIndex = Array.IndexOf(
            WorksheetElementOrder,
            localName);
        var insertBefore = root.Elements()
            .FirstOrDefault(element =>
            {
                var index = Array.IndexOf(
                    WorksheetElementOrder,
                    element.Name.LocalName);
                return index >= 0 && index > requestedIndex;
            });
        if (insertBefore is null)
        {
            root.Add(created);
        }
        else
        {
            insertBefore.AddBeforeSelf(created);
        }
        return created;
    }

    private static void SetBooleanAttribute(
        XElement element,
        string name,
        bool value)
    {
        if (value)
        {
            element.SetAttributeValue(name, "1");
        }
        else
        {
            element.Attribute(name)?.Remove();
        }
    }

    private static void SetChildText(
        XElement parent,
        string localName,
        string? value)
    {
        var element = parent.Element(
            SpreadsheetNamespace + localName);
        if (string.IsNullOrEmpty(value))
        {
            element?.Remove();
            return;
        }
        if (element is null)
        {
            var created = new XElement(
                SpreadsheetNamespace + localName,
                value);
            var order = new[]
            {
                "oddHeader",
                "oddFooter",
                "evenHeader",
                "evenFooter",
                "firstHeader",
                "firstFooter",
            };
            var requestedIndex = Array.IndexOf(order, localName);
            var insertBefore = parent.Elements()
                .FirstOrDefault(candidate =>
                {
                    var candidateIndex = Array.IndexOf(
                        order,
                        candidate.Name.LocalName);
                    return candidateIndex >= 0 &&
                           candidateIndex > requestedIndex;
                });
            if (insertBefore is null)
            {
                parent.Add(created);
            }
            else
            {
                insertBefore.AddBeforeSelf(created);
            }
        }
        else
        {
            element.Value = value;
        }
    }

    private static bool ReadBoolean(XAttribute? attribute) =>
        string.Equals(attribute?.Value, "1", StringComparison.Ordinal) ||
        string.Equals(
            attribute?.Value,
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static double ReadPositiveDouble(
        XAttribute? attribute,
        double fallback) =>
        double.TryParse(
            attribute?.Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var result) &&
        double.IsFinite(result) &&
        result > 0d
            ? result
            : fallback;

    private static double ReadNonNegativeDouble(
        XAttribute? attribute,
        double fallback) =>
        double.TryParse(
            attribute?.Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var result) &&
        double.IsFinite(result) &&
        result >= 0d
            ? result
            : fallback;

    private static int? ReadPositiveInt(XAttribute? attribute) =>
        int.TryParse(
            attribute?.Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var result) && result > 0
            ? result
            : null;

    private static string FormatDouble(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static XDocument LoadPartXml(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static void SavePartXml(
        OpenXmlPart part,
        XDocument document)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        document.Save(stream, SaveOptions.DisableFormatting);
    }

    private sealed class DefinedPrintNames
    {
        public string? PrintArea { get; set; }

        public string? PrintTitles { get; set; }
    }
}
