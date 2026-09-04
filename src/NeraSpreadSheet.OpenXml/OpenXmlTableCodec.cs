using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;
using NeraWorksheet = NeraSpreadSheet.Core.Worksheet;
using OpenXmlTablePartReference = DocumentFormat.OpenXml.Spreadsheet.TablePart;
using OpenXmlTableParts = DocumentFormat.OpenXml.Spreadsheet.TableParts;

namespace NeraSpreadSheet.OpenXml;

internal static class OpenXmlTableCodec
{
    private const int MaxTablesPerWorksheet = 10_000;
    private const int MaxColumnsPerTable = SpreadsheetLimits.MaxColumns;
    private const int MaxFilterValuesPerColumn = 100_000;
    private const string RelationshipIdPrefix = "rIdNeraTable";
    private const string ColumnUniqueNamePrefix = "nera:";
    private const long MaxXmlCharacters = 64L * 1024L * 1024L;
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static void ReadWorksheetTables(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet,
        IReadOnlyList<CellStylePatch> differentialStyles,
        bool preserveUnsupportedMarkup,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(worksheet);
        var containers = worksheetPart.Worksheet?
            .Elements<OpenXmlTableParts>()
            .ToArray() ?? [];
        if (containers.Length > 1)
        {
            throw new InvalidDataException(
                "A worksheet cannot contain multiple tableParts collections.");
        }
        if (containers.Length == 0)
        {
            if (worksheetPart.TableDefinitionParts.Any())
            {
                throw new InvalidDataException(
                    "The worksheet has unreferenced table-definition parts.");
            }
            return;
        }

        var references = containers[0]
            .Elements<OpenXmlTablePartReference>()
            .ToArray();
        if (references.Length > MaxTablesPerWorksheet)
        {
            throw new InvalidDataException(
                $"A worksheet cannot contain more than {MaxTablesPerWorksheet} tables.");
        }
        if (containers[0].Count?.Value is uint declaredCount &&
            declaredCount != references.Length)
        {
            throw new InvalidDataException(
                "The tableParts count does not match its tablePart children.");
        }

        var seenRelationshipIds = new HashSet<string>(
            StringComparer.Ordinal);
        foreach (var reference in references)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relationshipId = reference.Id?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId) ||
                !seenRelationshipIds.Add(relationshipId))
            {
                throw new InvalidDataException(
                    "A tablePart relationship identifier is missing or duplicated.");
            }

            TableDefinitionPart tablePart;
            try
            {
                tablePart = worksheetPart.GetPartById(relationshipId)
                    as TableDefinitionPart
                    ?? throw new InvalidDataException(
                        "A tablePart relationship does not target a table-definition part.");
            }
            catch (ArgumentOutOfRangeException exception)
            {
                throw new InvalidDataException(
                    "A tablePart relationship cannot be resolved.",
                    exception);
            }
            catch (KeyNotFoundException exception)
            {
                throw new InvalidDataException(
                    "A tablePart relationship cannot be resolved.",
                    exception);
            }

            worksheet.AddTable(ReadTableDefinition(
                tablePart,
                relationshipId,
                differentialStyles,
                preserveUnsupportedMarkup));
        }

        if (worksheetPart.TableDefinitionParts.Count() != references.Length)
        {
            throw new InvalidDataException(
                "The worksheet contains unreferenced table-definition parts.");
        }
    }

    public static void WriteWorksheetTables(
        WorksheetPart worksheetPart,
        NeraWorksheet worksheet,
        OpenXmlConditionalFormattingExportPlan exportPlan,
        ref uint nextTableId)
    {
        ArgumentNullException.ThrowIfNull(worksheetPart);
        ArgumentNullException.ThrowIfNull(worksheet);
        if (worksheet.TableCount == 0)
        {
            return;
        }
        if (worksheet.TableCount > MaxTablesPerWorksheet)
        {
            throw new InvalidOperationException(
                $"A worksheet cannot contain more than {MaxTablesPerWorksheet} tables.");
        }

        var tableParts = new OpenXmlTableParts
        {
            Count = checked((uint)worksheet.TableCount),
        };
        foreach (var table in worksheet.Tables)
        {
            if (nextTableId == uint.MaxValue)
            {
                throw new InvalidOperationException(
                    "The XLSX table identifier space is exhausted.");
            }

            var relationshipId = CreateRelationshipId(table.Id);
            var tablePart = worksheetPart
                .AddNewPart<TableDefinitionPart>(relationshipId);
            SavePartXml(
                tablePart,
                BuildTableDocument(table, worksheet, exportPlan, nextTableId++));
            tableParts.Append(new OpenXmlTablePartReference
            {
                Id = relationshipId,
            });
        }

        worksheetPart.Worksheet!.Append(tableParts);
        worksheetPart.Worksheet.Save();
    }

    private static SpreadsheetTable ReadTableDefinition(
        TableDefinitionPart tablePart,
        string relationshipId,
        IReadOnlyList<CellStylePatch> differentialStyles,
        bool preserveUnsupportedMarkup)
    {
        var document = LoadPartXml(tablePart);
        var root = document.Root
            ?? throw new InvalidDataException(
                "A table-definition part is missing its root element.");
        if (root.Name != SpreadsheetNamespace + "table")
        {
            throw new InvalidDataException(
                "A table-definition part has invalid root markup.");
        }

        var name = RequiredAttribute(root, "displayName");
        var internalName = (string?)root.Attribute("name");
        if (internalName is not null &&
            !string.Equals(
                internalName,
                name,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Nera requires table name and displayName to match.");
        }
        var range = ParseRange(RequiredAttribute(root, "ref"));
        var headerRowCount = ReadUIntAttribute(
            root,
            "headerRowCount",
            defaultValue: 1U);
        if (headerRowCount > 1U)
        {
            throw new InvalidDataException(
                "Only zero or one table header row is supported.");
        }
        var totalsRowCount = ReadUIntAttribute(
            root,
            "totalsRowCount",
            defaultValue: 0U);
        if (totalsRowCount > 1U)
        {
            throw new InvalidDataException(
                "Only zero or one table totals row is supported.");
        }
        var totalsRowShown = ReadBooleanAttribute(
            root,
            "totalsRowShown",
            defaultValue: false);
        var hasTotalsRow = totalsRowCount == 1U || totalsRowShown;

        var tableColumnsElements = root
            .Elements(SpreadsheetNamespace + "tableColumns")
            .ToArray();
        if (tableColumnsElements.Length != 1)
        {
            throw new InvalidDataException(
                "A table must contain exactly one tableColumns collection.");
        }
        var tableColumnElements = tableColumnsElements[0]
            .Elements(SpreadsheetNamespace + "tableColumn")
            .ToArray();
        if (tableColumnElements.Length == 0 ||
            tableColumnElements.Length > MaxColumnsPerTable ||
            tableColumnElements.Length != range.ColumnCount)
        {
            throw new InvalidDataException(
                "The table-column count is invalid for the table range.");
        }
        if (ReadUIntAttribute(
                tableColumnsElements[0],
                "count",
                checked((uint)tableColumnElements.Length)) !=
            tableColumnElements.Length)
        {
            throw new InvalidDataException(
                "The tableColumns count does not match its children.");
        }

        var tableId = ParseTableGuid(
            relationshipId,
            tablePart.Uri.ToString());
        var seenColumnIds = new HashSet<uint>();
        var columns = new SpreadsheetTableColumn[
            tableColumnElements.Length];
        for (var index = 0;
             index < tableColumnElements.Length;
             index++)
        {
            var element = tableColumnElements[index];
            var numericId = ReadUIntAttribute(
                element,
                "id",
                defaultValue: 0U);
            if (numericId == 0U ||
                !seenColumnIds.Add(numericId))
            {
                throw new InvalidDataException(
                    "Table-column identifiers must be non-zero and unique.");
            }

            ValidateColumnChildren(element);
            columns[index] = new SpreadsheetTableColumn(
                ParseColumnGuid(
                    (string?)element.Attribute("uniqueName"),
                    tableId,
                    numericId),
                RequiredAttribute(element, "name"),
                NormalizeImportedFormula(element.Element(
                    SpreadsheetNamespace + "calculatedColumnFormula")?.Value),
                NormalizeImportedFormula(element.Element(
                    SpreadsheetNamespace + "totalsRowFormula")?.Value),
                (string?)element.Attribute("totalsRowLabel"));
        }

        var styleElements = root
            .Elements(SpreadsheetNamespace + "tableStyleInfo")
            .ToArray();
        if (styleElements.Length > 1)
        {
            throw new InvalidDataException(
                "A table cannot contain multiple tableStyleInfo elements.");
        }
        var style = styleElements.SingleOrDefault();
        var autoFilter = ReadAutoFilter(
            root,
            range,
            hasTotalsRow,
            columns,
            differentialStyles,
            preserveUnsupportedMarkup);
        ValidateTableChildren(root);
        return new SpreadsheetTable(
            tableId,
            name,
            range,
            columns,
            hasHeaders: headerRowCount == 1U,
            hasTotalsRow,
            styleName: (string?)style?.Attribute("name"),
            showFirstColumn: ReadBooleanAttribute(
                style,
                "showFirstColumn",
                false),
            showLastColumn: ReadBooleanAttribute(
                style,
                "showLastColumn",
                false),
            showRowStripes: ReadBooleanAttribute(
                style,
                "showRowStripes",
                true),
            showColumnStripes: ReadBooleanAttribute(
                style,
                "showColumnStripes",
                false),
            autoFilter);
    }

    private static TableAutoFilter? ReadAutoFilter(
        XElement tableRoot,
        CellRange tableRange,
        bool hasTotalsRow,
        SpreadsheetTableColumn[] columns,
        IReadOnlyList<CellStylePatch> differentialStyles,
        bool preserveUnsupportedMarkup)
    {
        var elements = tableRoot
            .Elements(SpreadsheetNamespace + "autoFilter")
            .ToArray();
        if (elements.Length > 1)
        {
            throw new InvalidDataException(
                "A table cannot contain multiple autoFilter elements.");
        }
        if (elements.Length == 0)
        {
            return null;
        }

        var autoFilter = elements[0];
        var expectedBottom = tableRange.Bottom -
                             (hasTotalsRow ? 1 : 0);
        if (expectedBottom < tableRange.Top)
        {
            throw new InvalidDataException(
                "The table does not have a valid AutoFilter range.");
        }
        var expectedRange = new CellRange(
            tableRange.TopLeft,
            new CellAddress(expectedBottom, tableRange.Right));
        var declaredReference = (string?)autoFilter.Attribute("ref");
        if (declaredReference is not null &&
            ParseRange(declaredReference) != expectedRange)
        {
            throw new InvalidDataException(
                "The table AutoFilter range does not match the table range.");
        }
        var unsupported = autoFilter.Elements()
            .Where(element =>
                element.Name != SpreadsheetNamespace + "filterColumn" &&
                element.Name != SpreadsheetNamespace + "sortState" &&
                element.Name != SpreadsheetNamespace + "extLst")
            .FirstOrDefault();
        if (unsupported is not null)
        {
            throw new InvalidDataException(
                $"Unsupported AutoFilter element '{unsupported.Name.LocalName}'.");
        }

        var filters = new List<TableFilterColumn>();
        var seenColumnIndexes = new HashSet<uint>();
        foreach (var filterColumn in autoFilter.Elements(
                     SpreadsheetNamespace + "filterColumn"))
        {
            var columnIndex = ReadUIntAttribute(
                filterColumn,
                "colId",
                uint.MaxValue);
            if (columnIndex >= columns.Length ||
                !seenColumnIndexes.Add(columnIndex))
            {
                throw new InvalidDataException(
                    "An AutoFilter column index is invalid or duplicated.");
            }
            try
            {
                filters.Add(ReadFilterColumn(
                    filterColumn,
                    columns[checked((int)columnIndex)].Id,
                    differentialStyles));
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
                autoFilter,
                expectedRange,
                (id, cellColor) => ResolveColor(differentialStyles, id, cellColor));
        }
        catch (InvalidDataException) when (preserveUnsupportedMarkup)
        {
            sortState = null;
        }
        return filters.Count == 0 && sortState is null && !preserveUnsupportedMarkup
            ? null
            : new TableAutoFilter(filters, sortState);
    }

    private static TableFilterColumn ReadFilterColumn(
        XElement filterColumn,
        Guid columnId,
        IReadOnlyList<CellStylePatch> differentialStyles)
    {
        var parsed = OpenXmlAutoFilterCriteriaCodec.Parse(
            filterColumn,
            (id, cellColor) => ResolveColor(differentialStyles, id, cellColor));
        return new TableFilterColumn(
            columnId,
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

    private static TableFilterCondition ReadCustomFilter(XElement element)
    {
        var operatorText = (string?)element.Attribute("operator") ?? "equal";
        var @operator = operatorText switch
        {
            "equal" => TableFilterComparisonOperator.Equal,
            "notEqual" => TableFilterComparisonOperator.NotEqual,
            "greaterThan" => TableFilterComparisonOperator.GreaterThan,
            "greaterThanOrEqual" =>
                TableFilterComparisonOperator.GreaterThanOrEqual,
            "lessThan" => TableFilterComparisonOperator.LessThan,
            "lessThanOrEqual" =>
                TableFilterComparisonOperator.LessThanOrEqual,
            _ => throw new InvalidDataException(
                $"Unsupported custom filter operator '{operatorText}'."),
        };
        return new TableFilterCondition(
            @operator,
            ParseFilterValue(RequiredAttribute(element, "val")));
    }

    private static XDocument BuildTableDocument(
        SpreadsheetTable table,
        NeraWorksheet worksheet,
        OpenXmlConditionalFormattingExportPlan exportPlan,
        uint numericTableId)
    {
        var root = new XElement(
            SpreadsheetNamespace + "table",
            new XAttribute("id", numericTableId),
            new XAttribute("name", table.Name),
            new XAttribute("displayName", table.Name),
            new XAttribute("ref", ToA1Range(table.Range)),
            new XAttribute("headerRowCount", table.HasHeaders ? 1 : 0),
            new XAttribute("totalsRowCount", table.HasTotalsRow ? 1 : 0),
            new XAttribute("totalsRowShown", table.HasTotalsRow ? 1 : 0));

        if (table.AutoFilter is { })
        {
            root.Add(BuildAutoFilter(table, worksheet, exportPlan));
        }

        var columns = new XElement(
            SpreadsheetNamespace + "tableColumns",
            new XAttribute("count", table.Columns.Count));
        for (var index = 0; index < table.Columns.Count; index++)
        {
            var column = table.Columns[index];
            var element = new XElement(
                SpreadsheetNamespace + "tableColumn",
                new XAttribute("id", index + 1),
                new XAttribute("name", column.Name),
                new XAttribute(
                    "uniqueName",
                    ColumnUniqueNamePrefix + column.Id.ToString("N")));
            if (column.TotalsRowLabel is not null)
            {
                element.SetAttributeValue(
                    "totalsRowLabel",
                    column.TotalsRowLabel);
            }
            if (column.CalculatedColumnFormula is not null)
            {
                element.Add(new XElement(
                    SpreadsheetNamespace + "calculatedColumnFormula",
                    StripFormulaPrefix(column.CalculatedColumnFormula)));
            }
            if (column.TotalsRowFormula is not null)
            {
                element.Add(new XElement(
                    SpreadsheetNamespace + "totalsRowFormula",
                    StripFormulaPrefix(column.TotalsRowFormula)));
            }
            columns.Add(element);
        }
        root.Add(columns);

        if (table.StyleName is not null)
        {
            root.Add(new XElement(
                SpreadsheetNamespace + "tableStyleInfo",
                new XAttribute("name", table.StyleName),
                new XAttribute("showFirstColumn", table.ShowFirstColumn ? 1 : 0),
                new XAttribute("showLastColumn", table.ShowLastColumn ? 1 : 0),
                new XAttribute("showRowStripes", table.ShowRowStripes ? 1 : 0),
                new XAttribute("showColumnStripes", table.ShowColumnStripes ? 1 : 0)));
        }
        return new XDocument(
            new XDeclaration("1.0", "utf-8", "yes"),
            root);
    }

    private static XElement BuildAutoFilter(
        SpreadsheetTable table,
        NeraWorksheet worksheet,
        OpenXmlConditionalFormattingExportPlan exportPlan)
    {
        var bottom = table.Range.Bottom -
                     (table.HasTotalsRow ? 1 : 0);
        var element = new XElement(
            SpreadsheetNamespace + "autoFilter",
            new XAttribute(
                "ref",
                ToA1Range(new CellRange(
                    table.Range.TopLeft,
                    new CellAddress(bottom, table.Range.Right)))));
        foreach (var filter in table.AutoFilter!.Columns)
        {
            var columnIndex = table.GetColumnIndex(filter.ColumnId);
            var filterColumn = new XElement(
                SpreadsheetNamespace + "filterColumn",
                new XAttribute("colId", columnIndex));
            filterColumn.Add(OpenXmlAutoFilterCriteriaCodec.Build(
                filter,
                color => exportPlan.GetColorStyleId(worksheet, color)));
            element.Add(filterColumn);
        }
        var dataRange = table.DataRange ?? new CellRange(
            new CellAddress(table.Range.Top, table.Range.Left),
            new CellAddress(bottom, table.Range.Right));
        var sortState = OpenXmlAutoFilterCriteriaCodec.BuildSortState(
            table.AutoFilter.SortState,
            dataRange,
            color => exportPlan.GetColorStyleId(worksheet, color));
        if (sortState is not null) element.Add(sortState);
        return element;
    }

    private static SpreadsheetColorFilter ResolveColor(
        IReadOnlyList<CellStylePatch> differentialStyles,
        uint id,
        bool cellColor)
    {
        if (id >= differentialStyles.Count)
        {
            throw new InvalidDataException("An AutoFilter color references an unavailable differential style.");
        }
        var patch = differentialStyles[checked((int)id)];
        if (cellColor && patch.Fill is { } fill)
        {
            return new SpreadsheetColorFilter(SpreadsheetFilterColorKind.Fill, fill.Color);
        }
        if (!cellColor && patch.FontColor is { } fontColor)
        {
            return new SpreadsheetColorFilter(SpreadsheetFilterColorKind.Font, fontColor);
        }
        throw new InvalidDataException("An AutoFilter differential style does not define the requested color.");
    }

    private static XElement BuildCustomFilter(
        TableFilterCondition condition) =>
        new(
            SpreadsheetNamespace + "customFilter",
            new XAttribute(
                "operator",
                condition.Operator switch
                {
                    TableFilterComparisonOperator.Equal => "equal",
                    TableFilterComparisonOperator.NotEqual => "notEqual",
                    TableFilterComparisonOperator.GreaterThan => "greaterThan",
                    TableFilterComparisonOperator.GreaterThanOrEqual =>
                        "greaterThanOrEqual",
                    TableFilterComparisonOperator.LessThan => "lessThan",
                    TableFilterComparisonOperator.LessThanOrEqual =>
                        "lessThanOrEqual",
                    _ => throw new InvalidOperationException(
                        "Unsupported table-filter comparison operator."),
                }),
            new XAttribute("val", FormatFilterValue(condition.Value)));

    private static void ValidateTableChildren(XElement root)
    {
        var unsupported = root.Elements().FirstOrDefault(element =>
            element.Name != SpreadsheetNamespace + "autoFilter" &&
            element.Name != SpreadsheetNamespace + "tableColumns" &&
            element.Name != SpreadsheetNamespace + "tableStyleInfo" &&
            element.Name != SpreadsheetNamespace + "extLst");
        if (unsupported is not null)
        {
            throw new InvalidDataException(
                $"Unsupported table element '{unsupported.Name.LocalName}'.");
        }
    }

    private static void ValidateColumnChildren(XElement element)
    {
        var unsupported = element.Elements().FirstOrDefault(child =>
            child.Name != SpreadsheetNamespace + "calculatedColumnFormula" &&
            child.Name != SpreadsheetNamespace + "totalsRowFormula" &&
            child.Name != SpreadsheetNamespace + "extLst");
        if (unsupported is not null)
        {
            throw new InvalidDataException(
                $"Unsupported table-column element '{unsupported.Name.LocalName}'.");
        }
        if (element.Elements(
                SpreadsheetNamespace + "calculatedColumnFormula").Count() > 1 ||
            element.Elements(
                SpreadsheetNamespace + "totalsRowFormula").Count() > 1)
        {
            throw new InvalidDataException(
                "A table column contains duplicate formula elements.");
        }
    }

    private static string CreateRelationshipId(Guid tableId) =>
        RelationshipIdPrefix + tableId.ToString("N");

    private static Guid ParseTableGuid(
        string relationshipId,
        string partUri)
    {
        if (relationshipId.StartsWith(
                RelationshipIdPrefix,
                StringComparison.Ordinal) &&
            Guid.TryParseExact(
                relationshipId[RelationshipIdPrefix.Length..],
                "N",
                out var parsed))
        {
            return parsed;
        }
        return CreateDeterministicGuid(
            string.Create(
                CultureInfo.InvariantCulture,
                $"table|{partUri}|{relationshipId}"));
    }

    private static Guid ParseColumnGuid(
        string? uniqueName,
        Guid tableId,
        uint numericId)
    {
        if (uniqueName is not null &&
            uniqueName.StartsWith(
                ColumnUniqueNamePrefix,
                StringComparison.Ordinal) &&
            Guid.TryParseExact(
                uniqueName[ColumnUniqueNamePrefix.Length..],
                "N",
                out var parsed))
        {
            return parsed;
        }
        return CreateDeterministicGuid(
            string.Create(
                CultureInfo.InvariantCulture,
                $"column|{tableId:N}|{numericId}"));
    }

    private static Guid CreateDeterministicGuid(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        Span<byte> guidBytes = stackalloc byte[16];
        bytes.AsSpan(0, guidBytes.Length).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }

    private static CellRange ParseRange(string reference)
    {
        var separator = reference.IndexOf(':');
        if (separator < 0)
        {
            var address = CellAddress.ParseA1(reference);
            return new CellRange(address, address);
        }
        if (separator == 0 ||
            separator == reference.Length - 1 ||
            reference.IndexOf(':', separator + 1) >= 0 ||
            !CellAddress.TryParseA1(
                reference[..separator],
                out var first) ||
            !CellAddress.TryParseA1(
                reference[(separator + 1)..],
                out var second) ||
            first.RowIndex > second.RowIndex ||
            first.ColumnIndex > second.ColumnIndex)
        {
            throw new InvalidDataException(
                $"'{reference}' is not a valid table range.");
        }
        return new CellRange(first, second);
    }

    private static string RequiredAttribute(
        XElement element,
        string name)
    {
        var value = (string?)element.Attribute(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException(
                $"Required table attribute '{name}' is missing.");
        }
        return value;
    }

    private static uint ReadUIntAttribute(
        XElement? element,
        string name,
        uint defaultValue)
    {
        if (element is null)
        {
            return defaultValue;
        }
        var text = (string?)element.Attribute(name);
        if (text is null)
        {
            return defaultValue;
        }
        if (!uint.TryParse(
                text,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var value))
        {
            throw new InvalidDataException(
                $"Table attribute '{name}' is not a valid unsigned integer.");
        }
        return value;
    }

    private static bool ReadBooleanAttribute(
        XElement? element,
        string name,
        bool defaultValue)
    {
        if (element is null)
        {
            return defaultValue;
        }
        var text = (string?)element.Attribute(name);
        if (text is null)
        {
            return defaultValue;
        }
        return text switch
        {
            "1" or "true" => true,
            "0" or "false" => false,
            _ => throw new InvalidDataException(
                $"Table attribute '{name}' is not a valid boolean."),
        };
    }

    private static string? NormalizeImportedFormula(string? formula) =>
        string.IsNullOrWhiteSpace(formula)
            ? null
            : formula.StartsWith('=')
                ? formula
                : $"={formula}";

    private static string StripFormulaPrefix(string formula) =>
        formula.StartsWith('=')
            ? formula[1..]
            : formula;

    private static CellValue ParseFilterValue(string value)
    {
        if (double.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var number) &&
            double.IsFinite(number))
        {
            return CellValue.FromNumber(number);
        }
        if (bool.TryParse(value, out var boolean))
        {
            return CellValue.FromBoolean(boolean);
        }
        return CellValue.FromText(value);
    }

    private static string FormatFilterValue(CellValue value) =>
        value.Kind switch
        {
            CellValueKind.Blank => string.Empty,
            CellValueKind.Number => ((double)value.RawValue!).ToString(
                "R",
                CultureInfo.InvariantCulture),
            CellValueKind.Boolean => (bool)value.RawValue! ? "1" : "0",
            CellValueKind.DateTime => ((DateTime)value.RawValue!).ToOADate()
                .ToString("R", CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

    private static string ToA1Range(CellRange range) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{range.TopLeft.ToA1()}:{range.BottomRight.ToA1()}");

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
                $"The table part '{part.Uri}' does not contain valid XML.",
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
                OmitXmlDeclaration = false,
            });
        document.Save(writer, SaveOptions.DisableFormatting);
    }
}
