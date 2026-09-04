using System.Globalization;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;
using NeraWorksheet = NeraSpreadSheet.Core.Worksheet;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlWorksheetAutoFilterCodec
{
    private const int MaxFilterValuesPerColumn = 100_000;
    private const long MaxXmlCharacters = 256L * 1024L * 1024L;
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly Dictionary<string, int> WorksheetOrder =
        CreateOrder([
            "sheetPr", "dimension", "sheetViews", "sheetFormatPr", "cols",
            "sheetData", "sheetCalcPr", "sheetProtection", "protectedRanges",
            "scenarios", "autoFilter", "sortState", "dataConsolidate",
            "customSheetViews", "mergeCells", "phoneticPr",
            "conditionalFormatting", "dataValidations", "hyperlinks",
            "printOptions", "pageMargins", "pageSetup", "headerFooter",
            "rowBreaks", "colBreaks", "customProperties", "cellWatches",
            "ignoredErrors", "smartTags", "drawing", "legacyDrawing",
            "legacyDrawingHF", "picture", "oleObjects", "controls",
            "webPublishItems", "tableParts", "extLst",
        ]);

    public static void ReadWorksheetFilter(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet,
        IReadOnlyList<CellStylePatch> differentialStyles,
        bool preserveUnsupportedMarkup,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(worksheet);
        cancellationToken.ThrowIfCancellationRequested();
        var root = LoadWorksheetRoot(worksheetPart);
        var elements = root.Elements(SpreadsheetNamespace + "autoFilter").ToArray();
        if (elements.Length > 1)
        {
            throw new InvalidDataException(
                "The XLSX worksheet contains duplicate autoFilter elements.");
        }
        if (elements.Length == 0)
        {
            return;
        }

        try
        {
            worksheet.SetAutoFilter(ParseAutoFilter(elements[0], differentialStyles, preserveUnsupportedMarkup, cancellationToken));
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            throw new InvalidDataException(
                "The XLSX worksheet contains an invalid direct AutoFilter.",
                exception);
        }
    }

    public static void WriteWorksheetFilter(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet,
        OpenXmlConditionalFormattingExportPlan exportPlan)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(worksheet);
        var document = LoadPartXml(worksheetPart);
        var root = RequireWorksheetRoot(document);
        root.Elements(SpreadsheetNamespace + "autoFilter").Remove();
        if (worksheet.AutoFilter is not { } autoFilter)
        {
            SavePartXml(worksheetPart, document);
            return;
        }
        if (!autoFilter.HasHeaderRow)
        {
            throw new InvalidOperationException(
                "SpreadsheetML worksheet AutoFilter requires a header row.");
        }

        var element = new XElement(
            SpreadsheetNamespace + "autoFilter",
            new XAttribute("ref", ToA1Range(autoFilter.Range)));
        foreach (var column in autoFilter.Columns.OrderBy(static item => item.ColumnOffset))
        {
            element.Add(BuildFilterColumn(column, worksheet, exportPlan));
        }
        var sortState = OpenXmlAutoFilterCriteriaCodec.BuildSortState(
            autoFilter.SortState,
            autoFilter.DataRange ?? autoFilter.Range,
            color => exportPlan.GetColorStyleId(worksheet, color));
        if (sortState is not null) element.Add(sortState);
        InsertInSchemaOrder(root, element);
        SavePartXml(worksheetPart, document);
    }

    public static void PatchPreservedFilter(
        XElement preservedWorksheetRoot,
        XElement generatedWorksheetRoot)
    {
        EnsureWorksheetRoot(preservedWorksheetRoot);
        EnsureWorksheetRoot(generatedWorksheetRoot);
        var preserved = preservedWorksheetRoot
            .Elements(SpreadsheetNamespace + "autoFilter").ToArray();
        var generated = generatedWorksheetRoot
            .Elements(SpreadsheetNamespace + "autoFilter").ToArray();
        if (preserved.Length > 1 || generated.Length > 1)
        {
            throw new InvalidDataException(
                "A worksheet cannot contain duplicate autoFilter elements.");
        }

        XElement? replacement = generated.Length == 0
            ? null
            : new XElement(generated[0]);
        if (replacement is not null && preserved.Length == 1)
        {
            PreserveFilterMarkup(preserved[0], replacement);
        }
        foreach (var element in preserved)
        {
            element.Remove();
        }
        if (replacement is not null)
        {
            InsertInSchemaOrder(preservedWorksheetRoot, replacement);
        }
    }

    internal static void PreserveFilterMarkup(XElement preserved, XElement replacement)
    {
        PreserveOpaqueAttributes(preserved, replacement);
        PreserveExtensionList(preserved, replacement);
        PreserveColumnExtensions(preserved, replacement);
        PreserveSortExtensions(preserved, replacement);
        PreserveUnownedChildren(preserved, replacement);
    }

    private static WorksheetAutoFilter ParseAutoFilter(
        XElement element,
        IReadOnlyList<CellStylePatch> differentialStyles,
        bool preserveUnsupportedMarkup,
        CancellationToken cancellationToken)
    {
        EnsureOnlyAttributes(element, "ref");
        var unsupported = element.Elements().FirstOrDefault(child =>
            child.Name != SpreadsheetNamespace + "filterColumn" &&
            child.Name != SpreadsheetNamespace + "sortState" &&
            child.Name != SpreadsheetNamespace + "extLst");
        if (unsupported is not null)
        {
            throw new InvalidDataException(
                $"Unsupported worksheet AutoFilter element '{unsupported.Name.LocalName}'.");
        }

        var range = ParseRange(RequiredAttribute(element, "ref"));
        var columns = new List<WorksheetAutoFilterColumn>();
        var seenOffsets = new HashSet<int>();
        foreach (var filterColumn in element.Elements(
                     SpreadsheetNamespace + "filterColumn"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var offset = ReadColumnOffset(filterColumn, range);
            if (!seenOffsets.Add(offset))
            {
                throw new InvalidDataException(
                    "The worksheet AutoFilter contains duplicate column indexes.");
            }
            try
            {
                columns.Add(ParseFilterColumn(filterColumn, offset, differentialStyles));
            }
            catch (InvalidDataException) when (preserveUnsupportedMarkup)
            {
                // The package envelope retains this producer-owned criterion.
            }
        }
        SpreadsheetFilterSortState? sortState;
        try
        {
            sortState = OpenXmlAutoFilterCriteriaCodec.ParseSortState(
                element,
                range,
                (id, cellColor) => ResolveColor(differentialStyles, id, cellColor));
        }
        catch (InvalidDataException) when (preserveUnsupportedMarkup)
        {
            sortState = null;
        }
        return new WorksheetAutoFilter(range, columns, hasHeaderRow: true, sortState);
    }

    private static WorksheetAutoFilterColumn ParseFilterColumn(
        XElement filterColumn,
        int columnOffset,
        IReadOnlyList<CellStylePatch> differentialStyles)
    {
        var parsed = OpenXmlAutoFilterCriteriaCodec.Parse(
            filterColumn,
            (id, cellColor) => ResolveColor(differentialStyles, id, cellColor));
        return new WorksheetAutoFilterColumn(
            columnOffset,
            parsed.Values,
            parsed.IncludeBlank,
            parsed.FirstCondition,
            parsed.SecondCondition,
            parsed.CombineWithAnd,
            parsed.DateGroups,
            parsed.TopBottom,
            parsed.DynamicFilter,
            parsed.ColorFilter,
            parsed.IconFilter);
    }

    private static WorksheetAutoFilterColumn ParseValueFilters(
        XElement filters,
        int columnOffset)
    {
        EnsureOnlyAttributes(filters, "blank");
        var children = filters.Elements().ToArray();
        if (children.Any(child => child.Name != SpreadsheetNamespace + "filter") ||
            children.Length > MaxFilterValuesPerColumn)
        {
            throw new InvalidDataException(
                "The worksheet value-filter collection is unsupported or too large.");
        }
        var values = children.Select(ParseValueFilter).ToArray();
        var includeBlank = ReadBooleanAttribute(filters, "blank", false);
        if (values.Length == 0 && !includeBlank)
        {
            throw new InvalidDataException(
                "A worksheet value filter requires values or blank matching.");
        }
        CellValue[] effectiveValues = values.Length == 0 && includeBlank
            ? [CellValue.Blank]
            : values;
        return new WorksheetAutoFilterColumn(
            columnOffset,
            effectiveValues,
            includeBlank);
    }

    private static WorksheetAutoFilterColumn ParseCustomFilters(
        XElement filters,
        int columnOffset)
    {
        EnsureOnlyAttributes(filters, "and");
        var children = filters.Elements().ToArray();
        if (children.Length is < 1 or > 2 ||
            children.Any(child =>
                child.Name != SpreadsheetNamespace + "customFilter"))
        {
            throw new InvalidDataException(
                "A worksheet custom filter requires one or two supported conditions.");
        }
        var conditions = children.Select(ParseCustomFilter).ToArray();
        return new WorksheetAutoFilterColumn(
            columnOffset,
            firstCondition: conditions[0],
            secondCondition: conditions.Length == 2 ? conditions[1] : null,
            combineWithAnd: ReadBooleanAttribute(filters, "and", false));
    }

    private static CellValue ParseValueFilter(XElement element)
    {
        EnsureOnlyAttributes(element, "val");
        return ParseFilterValue(RequiredAttribute(element, "val"));
    }

    private static TableFilterCondition ParseCustomFilter(XElement element)
    {
        EnsureOnlyAttributes(element, "operator", "val");
        var operatorText = (string?)element.Attribute("operator") ?? "equal";
        var valueText = RequiredAttributeAllowEmpty(element, "val");
        if (operatorText is "equal" or "notEqual")
        {
            if (valueText.Length == 0)
            {
                return new TableFilterCondition(
                    operatorText == "equal"
                        ? TableFilterComparisonOperator.IsBlank
                        : TableFilterComparisonOperator.IsNotBlank,
                    CellValue.Blank);
            }
            if (TryParseWildcard(valueText, out var wildcardOperator, out var value))
            {
                if (operatorText == "notEqual")
                {
                    if (wildcardOperator != TableFilterComparisonOperator.Contains)
                    {
                        throw new InvalidDataException(
                            "Only not-equal contains wildcard filters are currently supported.");
                    }
                    wildcardOperator = TableFilterComparisonOperator.DoesNotContain;
                }
                return new TableFilterCondition(
                    wildcardOperator,
                    CellValue.FromText(value));
            }
        }

        var comparisonOperator = operatorText switch
        {
            "equal" => TableFilterComparisonOperator.Equal,
            "notEqual" => TableFilterComparisonOperator.NotEqual,
            "greaterThan" => TableFilterComparisonOperator.GreaterThan,
            "greaterThanOrEqual" => TableFilterComparisonOperator.GreaterThanOrEqual,
            "lessThan" => TableFilterComparisonOperator.LessThan,
            "lessThanOrEqual" => TableFilterComparisonOperator.LessThanOrEqual,
            _ => throw new InvalidDataException(
                $"Unsupported worksheet custom-filter operator '{operatorText}'."),
        };
        return new TableFilterCondition(
            comparisonOperator,
            ParseFilterValue(valueText));
    }

    private static XElement BuildFilterColumn(
        WorksheetAutoFilterColumn column,
        NeraWorksheet worksheet,
        OpenXmlConditionalFormattingExportPlan exportPlan)
    {
        var result = new XElement(
            SpreadsheetNamespace + "filterColumn",
            new XAttribute("colId", column.ColumnOffset));
        result.Add(OpenXmlAutoFilterCriteriaCodec.Build(
            column.Criteria,
            color => exportPlan.GetColorStyleId(worksheet, color)));
        return result;
    }

    private static SpreadsheetColorFilter ResolveColor(
        IReadOnlyList<CellStylePatch> differentialStyles,
        uint id,
        bool cellColor)
    {
        if (id >= differentialStyles.Count)
            throw new InvalidDataException("An AutoFilter color references an unavailable differential style.");
        var patch = differentialStyles[checked((int)id)];
        if (cellColor && patch.Fill is { } fill)
            return new SpreadsheetColorFilter(SpreadsheetFilterColorKind.Fill, fill.Color);
        if (!cellColor && patch.FontColor is { } fontColor)
            return new SpreadsheetColorFilter(SpreadsheetFilterColorKind.Font, fontColor);
        throw new InvalidDataException("An AutoFilter differential style does not define the requested color.");
    }

    private static XElement BuildCustomFilter(TableFilterCondition condition)
    {
        var (operatorText, valueText) = condition.Operator switch
        {
            TableFilterComparisonOperator.Equal =>
                ("equal", FormatFilterValue(condition.Value)),
            TableFilterComparisonOperator.NotEqual =>
                ("notEqual", FormatFilterValue(condition.Value)),
            TableFilterComparisonOperator.GreaterThan =>
                ("greaterThan", FormatFilterValue(condition.Value)),
            TableFilterComparisonOperator.GreaterThanOrEqual =>
                ("greaterThanOrEqual", FormatFilterValue(condition.Value)),
            TableFilterComparisonOperator.LessThan =>
                ("lessThan", FormatFilterValue(condition.Value)),
            TableFilterComparisonOperator.LessThanOrEqual =>
                ("lessThanOrEqual", FormatFilterValue(condition.Value)),
            TableFilterComparisonOperator.BeginsWith =>
                ("equal", BuildWildcard(condition.Value, false, true)),
            TableFilterComparisonOperator.EndsWith =>
                ("equal", BuildWildcard(condition.Value, true, false)),
            TableFilterComparisonOperator.Contains =>
                ("equal", BuildWildcard(condition.Value, true, true)),
            TableFilterComparisonOperator.DoesNotContain =>
                ("notEqual", BuildWildcard(condition.Value, true, true)),
            TableFilterComparisonOperator.IsBlank => ("equal", string.Empty),
            TableFilterComparisonOperator.IsNotBlank => ("notEqual", string.Empty),
            _ => throw new InvalidOperationException(
                $"Worksheet AutoFilter operator '{condition.Operator}' requires unsupported dynamic or date-group markup."),
        };
        return new XElement(
            SpreadsheetNamespace + "customFilter",
            new XAttribute("operator", operatorText),
            new XAttribute("val", valueText));
    }

    private static bool TryParseWildcard(
        string pattern,
        out TableFilterComparisonOperator comparisonOperator,
        out string value)
    {
        var literal = new StringBuilder(pattern.Length);
        var leading = false;
        var trailing = false;
        var wildcardCount = 0;
        for (var index = 0; index < pattern.Length; index++)
        {
            var current = pattern[index];
            if (current == '~')
            {
                literal.Append(index + 1 < pattern.Length ? pattern[++index] : '~');
                continue;
            }
            if (current == '?')
            {
                throw new InvalidDataException(
                    "Single-character wildcard worksheet filters are not supported.");
            }
            if (current != '*')
            {
                literal.Append(current);
                continue;
            }

            wildcardCount++;
            if (index == 0)
            {
                leading = true;
            }
            else if (index == pattern.Length - 1)
            {
                trailing = true;
            }
            else
            {
                throw new InvalidDataException(
                    "Only leading and trailing worksheet wildcard filters are supported.");
            }
        }

        if (wildcardCount == 0)
        {
            comparisonOperator = default;
            value = string.Empty;
            return false;
        }
        if (wildcardCount > (leading && trailing ? 2 : 1) || literal.Length == 0)
        {
            throw new InvalidDataException(
                "The worksheet wildcard filter pattern is unsupported.");
        }
        comparisonOperator = leading && trailing
            ? TableFilterComparisonOperator.Contains
            : leading
                ? TableFilterComparisonOperator.EndsWith
                : TableFilterComparisonOperator.BeginsWith;
        value = literal.ToString();
        return true;
    }

    private static string BuildWildcard(
        CellValue value,
        bool leading,
        bool trailing)
    {
        var source = value.Kind == CellValueKind.Text
            ? (string)value.RawValue!
            : value.ToString();
        var escaped = source
            .Replace("~", "~~", StringComparison.Ordinal)
            .Replace("*", "~*", StringComparison.Ordinal)
            .Replace("?", "~?", StringComparison.Ordinal);
        return string.Concat(
            leading ? "*" : string.Empty,
            escaped,
            trailing ? "*" : string.Empty);
    }

    private static int ReadColumnOffset(XElement element, CellRange range)
    {
        var text = RequiredAttribute(element, "colId");
        if (!int.TryParse(
                text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var offset) ||
            offset < 0 || offset >= range.ColumnCount)
        {
            throw new InvalidDataException(
                "A worksheet AutoFilter column index is invalid.");
        }
        return offset;
    }

    private static CellRange ParseRange(string reference)
    {
        var separator = reference.IndexOf(':');
        if (separator < 0)
        {
            if (CellAddress.TryParseA1(reference, out var address))
            {
                return new CellRange(address, address);
            }
            throw InvalidRange(reference);
        }
        if (separator == 0 || separator == reference.Length - 1 ||
            reference.IndexOf(':', separator + 1) >= 0 ||
            !CellAddress.TryParseA1(reference[..separator], out var first) ||
            !CellAddress.TryParseA1(reference[(separator + 1)..], out var second) ||
            first.RowIndex > second.RowIndex ||
            first.ColumnIndex > second.ColumnIndex)
        {
            throw InvalidRange(reference);
        }
        return new CellRange(first, second);
    }

    private static InvalidDataException InvalidRange(string reference) =>
        new($"'{reference}' is not a valid worksheet AutoFilter range.");

    private static CellValue ParseFilterValue(string value)
    {
        if (double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number) && double.IsFinite(number))
        {
            return CellValue.FromNumber(number);
        }
        return bool.TryParse(value, out var boolean)
            ? CellValue.FromBoolean(boolean)
            : CellValue.FromText(value);
    }

    private static string FormatFilterValue(CellValue value) =>
        value.Kind switch
        {
            CellValueKind.Blank => string.Empty,
            CellValueKind.Number => ((double)value.RawValue!).ToString(
                "R", CultureInfo.InvariantCulture),
            CellValueKind.Boolean => (bool)value.RawValue! ? "1" : "0",
            CellValueKind.DateTime => ((DateTime)value.RawValue!).ToOADate()
                .ToString("R", CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

    private static void EnsureOnlyAttributes(
        XElement element,
        params string[] allowedNames)
    {
        var allowed = allowedNames.ToHashSet(StringComparer.Ordinal);
        var unsupported = element.Attributes().FirstOrDefault(attribute =>
            !attribute.IsNamespaceDeclaration &&
            attribute.Name.Namespace == XNamespace.None &&
            !allowed.Contains(attribute.Name.LocalName));
        if (unsupported is not null)
        {
            throw new InvalidDataException(
                $"Unsupported worksheet AutoFilter attribute '{unsupported.Name.LocalName}'.");
        }
    }

    private static bool ReadBooleanAttribute(
        XElement element,
        string name,
        bool defaultValue)
    {
        var text = (string?)element.Attribute(name);
        return text switch
        {
            null => defaultValue,
            "1" or "true" => true,
            "0" or "false" => false,
            _ => throw new InvalidDataException(
                $"Worksheet AutoFilter attribute '{name}' is not a valid boolean."),
        };
    }

    private static string RequiredAttribute(XElement element, string name)
    {
        var value = RequiredAttributeAllowEmpty(element, name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Required worksheet AutoFilter attribute '{name}' is missing.");
        }
        return value;
    }

    private static string RequiredAttributeAllowEmpty(
        XElement element,
        string name) =>
        element.Attribute(name)?.Value
        ?? throw new InvalidDataException(
            $"Required worksheet AutoFilter attribute '{name}' is missing.");

    private static void PreserveOpaqueAttributes(
        XElement preserved,
        XElement replacement)
    {
        foreach (var attribute in preserved.Attributes())
        {
            if ((attribute.IsNamespaceDeclaration ||
                 attribute.Name.Namespace != XNamespace.None) &&
                replacement.Attribute(attribute.Name) is null)
            {
                replacement.Add(new XAttribute(attribute));
            }
        }
    }

    private static void PreserveExtensionList(
        XElement preserved,
        XElement replacement)
    {
        var preservedExtensions = preserved
            .Elements(SpreadsheetNamespace + "extLst").ToArray();
        var generatedExtensions = replacement
            .Elements(SpreadsheetNamespace + "extLst").ToArray();
        if (preservedExtensions.Length > 1 || generatedExtensions.Length > 1)
        {
            throw new InvalidDataException(
                "Worksheet AutoFilter contains duplicate extLst elements.");
        }
        if (preservedExtensions.Length == 1 && generatedExtensions.Length == 0)
        {
            replacement.Add(new XElement(preservedExtensions[0]));
        }
    }

    private static void PreserveColumnExtensions(XElement preserved, XElement replacement)
    {
        var generatedById = replacement.Elements(SpreadsheetNamespace + "filterColumn")
            .ToDictionary(element => (string?)element.Attribute("colId") ?? string.Empty, StringComparer.Ordinal);
        foreach (var oldColumn in preserved.Elements(SpreadsheetNamespace + "filterColumn"))
        {
            var id = (string?)oldColumn.Attribute("colId") ?? string.Empty;
            if (!generatedById.TryGetValue(id, out var newColumn))
            {
                var sort = replacement.Element(SpreadsheetNamespace + "sortState");
                if (sort is null) replacement.Add(new XElement(oldColumn));
                else sort.AddBeforeSelf(new XElement(oldColumn));
                continue;
            }
            PreserveOpaqueAttributes(oldColumn, newColumn);
            PreserveExtensionList(oldColumn, newColumn);
            var oldDefinition = oldColumn.Elements().FirstOrDefault(element => element.Name != SpreadsheetNamespace + "extLst");
            var newDefinition = newColumn.Elements().FirstOrDefault(element => element.Name != SpreadsheetNamespace + "extLst");
            if (oldDefinition is not null && newDefinition is not null && oldDefinition.Name == newDefinition.Name)
            {
                PreserveOpaqueAttributes(oldDefinition, newDefinition);
                PreserveExtensionList(oldDefinition, newDefinition);
            }
        }
    }

    private static void PreserveSortExtensions(XElement preserved, XElement replacement)
    {
        var oldSort = preserved.Element(SpreadsheetNamespace + "sortState");
        var newSort = replacement.Element(SpreadsheetNamespace + "sortState");
        if (oldSort is null) return;
        if (newSort is null)
        {
            var extensions = replacement.Element(SpreadsheetNamespace + "extLst");
            if (extensions is null) replacement.Add(new XElement(oldSort));
            else extensions.AddBeforeSelf(new XElement(oldSort));
            return;
        }
        PreserveOpaqueAttributes(oldSort, newSort);
        PreserveExtensionList(oldSort, newSort);
        var oldConditions = oldSort.Elements(SpreadsheetNamespace + "sortCondition").ToArray();
        var newConditions = newSort.Elements(SpreadsheetNamespace + "sortCondition").ToArray();
        for (var index = 0; index < Math.Min(oldConditions.Length, newConditions.Length); index++)
        {
            PreserveOpaqueAttributes(oldConditions[index], newConditions[index]);
            PreserveExtensionList(oldConditions[index], newConditions[index]);
        }
    }

    private static void PreserveUnownedChildren(XElement preserved, XElement replacement)
    {
        foreach (var child in preserved.Elements().Where(element =>
                     element.Name.Namespace != SpreadsheetNamespace))
        {
            if (!replacement.Elements(child.Name).Any()) replacement.Add(new XElement(child));
        }
    }

    private static XElement LoadWorksheetRoot(OpenXmlPart part) =>
        RequireWorksheetRoot(LoadPartXml(part));

    private static XElement RequireWorksheetRoot(XDocument document)
    {
        var root = document.Root
            ?? throw new InvalidDataException(
                "The XLSX worksheet part is missing its root element.");
        EnsureWorksheetRoot(root);
        return root;
    }

    private static void EnsureWorksheetRoot(XElement root)
    {
        if (root.Name != SpreadsheetNamespace + "worksheet")
        {
            throw new InvalidDataException(
                "The XLSX package contains invalid worksheet markup.");
        }
    }

    private static Dictionary<string, int> CreateOrder(
        IReadOnlyList<string> names)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < names.Count; index++)
        {
            result.Add(names[index], index);
        }
        return result;
    }

    private static void InsertInSchemaOrder(XElement root, XElement element)
    {
        var targetRank = WorksheetOrder[element.Name.LocalName];
        var following = root.Elements().FirstOrDefault(candidate =>
            candidate.Name.Namespace == SpreadsheetNamespace &&
            WorksheetOrder.TryGetValue(
                candidate.Name.LocalName,
                out var rank) && rank > targetRank);
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
                XmlResolver = null,
                MaxCharactersInDocument = MaxXmlCharacters,
            });
        try
        {
            return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException exception)
        {
            throw new InvalidDataException(
                $"The worksheet part '{part.Uri}' does not contain valid XML.",
                exception);
        }
    }

    private static void SavePartXml(OpenXmlPart part, XDocument document)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(
            stream,
            new XmlWriterSettings
            {
                Encoding = new UTF8Encoding(false),
                Indent = false,
                OmitXmlDeclaration = false,
            });
        document.Save(writer, SaveOptions.DisableFormatting);
    }

    private static string ToA1Range(CellRange range) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{range.TopLeft.ToA1()}:{range.BottomRight.ToA1()}");
}
