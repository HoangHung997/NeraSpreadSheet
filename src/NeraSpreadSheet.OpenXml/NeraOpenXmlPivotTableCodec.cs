using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace NeraSpreadSheet.OpenXml;

internal static class NeraOpenXmlPivotTableCodec
{
    private const string ManagedDataCaption = "NeraSpreadSheet Values";
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    internal static void Import(
        SpreadsheetDocument document,
        SpreadsheetSession session,
        bool preserveUnknownParts)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(session);
        if (preserveUnknownParts)
        {
            return;
        }

        foreach (var mapping in EnumerateWorksheetMappings(document, session.Workbook))
        {
            foreach (var pivotPart in mapping.WorksheetPart.PivotTableParts)
            {
                if (TryImportPivot(pivotPart, session, mapping.Worksheet))
                {
                    continue;
                }
            }
        }
    }

    internal static void Export(
        SpreadsheetDocument document,
        SpreadsheetSession session,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(session);
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException(
                "The XLSX package does not contain a workbook part.");

        RemoveManagedPivots(workbookPart, cancellationToken);

        var usedNames = GetPivotNames(workbookPart);
        foreach (var mapping in EnumerateWorksheetMappings(document, session.Workbook))
        {
            foreach (var pivot in session.Analytics.GetPivots(mapping.Worksheet))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ExportPivot(
                    workbookPart,
                    mapping.WorksheetPart,
                    mapping.Worksheet,
                    pivot,
                    usedNames);
            }
        }
    }

    private static bool TryImportPivot(
        PivotTablePart pivotPart,
        SpreadsheetSession session,
        Worksheet fallbackWorksheet)
    {
        var pivotXml = LoadXmlPart(pivotPart);
        var pivotRoot = pivotXml.Root;
        if (pivotRoot?.Name != SpreadsheetNamespace + "pivotTableDefinition")
        {
            return false;
        }

        var cachePart = pivotPart.PivotTableCacheDefinitionPart;
        if (cachePart is null)
        {
            return false;
        }

        var cacheXml = LoadXmlPart(cachePart);
        var cacheRoot = cacheXml.Root;
        if (cacheRoot?.Name != SpreadsheetNamespace + "pivotCacheDefinition")
        {
            return false;
        }

        var source = cacheRoot
            .Element(SpreadsheetNamespace + "cacheSource")
            ?.Element(SpreadsheetNamespace + "worksheetSource");
        if (source is null ||
            !TryParseCellRange((string?)source.Attribute("ref"), out var sourceRange))
        {
            return false;
        }

        var sourceSheetName = (string?)source.Attribute("sheet");
        var worksheet = string.IsNullOrWhiteSpace(sourceSheetName)
            ? fallbackWorksheet
            : session.Workbook.Worksheets.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.Name,
                    sourceSheetName,
                    StringComparison.OrdinalIgnoreCase));
        if (worksheet is null)
        {
            return false;
        }

        var rowFieldIndex = TryReadFieldIndex(
            pivotRoot,
            "rowFields",
            "field",
            "x");
        var valueFieldIndex = TryReadFieldIndex(
            pivotRoot,
            "dataFields",
            "dataField",
            "fld");
        if (rowFieldIndex is null || valueFieldIndex is null)
        {
            return false;
        }

        var aggregation = ParseAggregation(
            (string?)pivotRoot
                .Element(SpreadsheetNamespace + "dataFields")
                ?.Element(SpreadsheetNamespace + "dataField")
                ?.Attribute("subtotal"));
        var name = ((string?)pivotRoot.Attribute("name"))?.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var definition = new SpreadsheetPivotDefinition(
            Guid.NewGuid(),
            name,
            sourceRange,
            checked(sourceRange.Left + rowFieldIndex.Value),
            checked(sourceRange.Left + valueFieldIndex.Value),
            aggregation,
            firstRowContainsHeaders: true);
        var existing = session.Analytics.GetPivots(worksheet);
        if (existing.Any(candidate => IsEquivalent(candidate, definition)))
        {
            return true;
        }

        try
        {
            session.Analytics.RestorePivot(worksheet, definition);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static void ExportPivot(
        WorkbookPart workbookPart,
        WorksheetPart worksheetPart,
        Worksheet worksheet,
        SpreadsheetPivotDefinition pivot,
        HashSet<string> usedNames)
    {
        var projection = SpreadsheetPivotProjector.Project(worksheet, pivot);
        var cacheId = GetNextCacheId(workbookPart);
        var standardName = GetUniquePivotName(pivot.Name, usedNames);
        var cachePart = workbookPart.AddNewPart<PivotTableCacheDefinitionPart>();
        var recordsPart = cachePart.AddNewPart<PivotTableCacheRecordsPart>();
        WriteXmlPart(
            cachePart,
            CreateCacheDefinitionXml(worksheet, pivot));
        WriteXmlPart(
            recordsPart,
            CreateCacheRecordsXml(worksheet, pivot));

        var pivotPart = worksheetPart.AddNewPart<PivotTablePart>();
        pivotPart.AddPart(cachePart);
        WriteXmlPart(
            pivotPart,
            CreatePivotTableXml(
                standardName,
                cacheId,
                pivot,
                projection));

        AddWorkbookPivotCache(
            workbookPart,
            cacheId,
            workbookPart.GetIdOfPart(cachePart));
    }

    private static XDocument CreateCacheDefinitionXml(
        Worksheet worksheet,
        SpreadsheetPivotDefinition pivot)
    {
        var fields = EnumerateFields(worksheet, pivot)
            .Select(field => new XElement(
                SpreadsheetNamespace + "cacheField",
                new XAttribute("name", field.Name),
                new XAttribute("numFmtId", "0"),
                CreateSharedItemsElement(field.Values)))
            .ToArray();
        return new XDocument(
            new XElement(
                SpreadsheetNamespace + "pivotCacheDefinition",
                new XAttribute("saveData", "1"),
                new XAttribute("refreshOnLoad", "0"),
                new XAttribute("createdVersion", "3"),
                new XAttribute("refreshedVersion", "3"),
                new XAttribute("minRefreshableVersion", "3"),
                new XAttribute("recordCount", GetDataRowCount(pivot)),
                new XElement(
                    SpreadsheetNamespace + "cacheSource",
                    new XAttribute("type", "worksheet"),
                    new XElement(
                        SpreadsheetNamespace + "worksheetSource",
                        new XAttribute("ref", ToA1Range(pivot.SourceRange)),
                        new XAttribute("sheet", worksheet.Name))),
                new XElement(
                    SpreadsheetNamespace + "cacheFields",
                    new XAttribute("count", fields.Length),
                    fields)));
    }

    private static XElement CreateSharedItemsElement(IReadOnlyList<CellValue> values)
    {
        var unique = new List<CellValue>();
        foreach (var value in values)
        {
            if (unique.Contains(value))
            {
                continue;
            }
            unique.Add(value);
        }

        var result = new XElement(
            SpreadsheetNamespace + "sharedItems",
            new XAttribute("count", unique.Count));
        if (unique.Any(static value => value.Kind == CellValueKind.Text))
        {
            result.Add(new XAttribute("containsString", "1"));
        }
        if (unique.Any(static value => value.Kind == CellValueKind.Number))
        {
            result.Add(new XAttribute("containsNumber", "1"));
        }
        if (unique.Any(static value => value.Kind == CellValueKind.Boolean))
        {
            result.Add(new XAttribute("containsBool", "1"));
        }
        if (unique.Any(static value => value.IsBlank))
        {
            result.Add(new XAttribute("containsBlank", "1"));
        }

        foreach (var value in unique)
        {
            result.Add(CreateSharedItemElement(value));
        }
        return result;
    }

    private static XDocument CreateCacheRecordsXml(
        Worksheet worksheet,
        SpreadsheetPivotDefinition pivot)
    {
        var fields = EnumerateFields(worksheet, pivot).ToArray();
        var sharedIndexes = fields
            .Select(static field => field.Values
                .Distinct()
                .Select((value, index) => (value, index))
                .ToDictionary(pair => pair.value, pair => pair.index))
            .ToArray();
        var records = new List<XElement>();
        for (var row = GetFirstDataRow(pivot); row <= pivot.SourceRange.Bottom; row++)
        {
            var values = new List<XElement>();
            for (var fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
            {
                var value = worksheet.GetCell(
                    new CellAddress(
                        row,
                        fields[fieldIndex].ColumnIndex)).Value;
                values.Add(CreateRecordItemElement(value, sharedIndexes[fieldIndex][value]));
            }
            records.Add(new XElement(SpreadsheetNamespace + "r", values));
        }

        return new XDocument(
            new XElement(
                SpreadsheetNamespace + "pivotCacheRecords",
                new XAttribute("count", records.Count),
                records));
    }

    private static XDocument CreatePivotTableXml(
        string name,
        uint cacheId,
        SpreadsheetPivotDefinition pivot,
        SpreadsheetPivotProjection projection)
    {
        var rowFieldIndex = pivot.RowFieldColumnIndex - pivot.SourceRange.Left;
        var valueFieldIndex = pivot.ValueFieldColumnIndex - pivot.SourceRange.Left;
        var fields = Enumerable.Range(0, pivot.SourceRange.ColumnCount)
            .Select(index =>
            {
                var attributes = new List<XAttribute>
                {
                    new("showAll", "0"),
                };
                if (index == rowFieldIndex)
                {
                    attributes.Add(new XAttribute("axis", "axisRow"));
                }
                if (index == valueFieldIndex)
                {
                    attributes.Add(new XAttribute("dataField", "1"));
                }

                var field = new XElement(
                    SpreadsheetNamespace + "pivotField",
                    attributes);
                if (index == rowFieldIndex)
                {
                    field.Add(new XElement(
                        SpreadsheetNamespace + "items",
                        new XAttribute("count", "1"),
                        new XElement(
                            SpreadsheetNamespace + "item",
                            new XAttribute("t", "default"))));
                }
                return field;
            })
            .ToArray();

        return new XDocument(
            new XElement(
                SpreadsheetNamespace + "pivotTableDefinition",
                new XAttribute("name", name),
                new XAttribute("cacheId", cacheId),
                new XAttribute("dataCaption", ManagedDataCaption),
                new XAttribute("createdVersion", "3"),
                new XAttribute("updatedVersion", "3"),
                new XAttribute("minRefreshableVersion", "3"),
                new XAttribute("useAutoFormatting", "1"),
                new XElement(
                    SpreadsheetNamespace + "location",
                    new XAttribute(
                        "ref",
                        GetDefaultPivotLocation(pivot, projection)),
                    new XAttribute("firstHeaderRow", "1"),
                    new XAttribute("firstDataRow", "1"),
                    new XAttribute("firstDataCol", "1")),
                new XElement(
                    SpreadsheetNamespace + "pivotFields",
                    new XAttribute("count", fields.Length),
                    fields),
                new XElement(
                    SpreadsheetNamespace + "rowFields",
                    new XAttribute("count", "1"),
                    new XElement(
                        SpreadsheetNamespace + "field",
                        new XAttribute("x", rowFieldIndex))),
                new XElement(
                    SpreadsheetNamespace + "dataFields",
                    new XAttribute("count", "1"),
                    new XElement(
                        SpreadsheetNamespace + "dataField",
                        new XAttribute(
                            "name",
                            $"{GetAggregationCaption(pivot.Aggregation)} of {projection.ValueFieldName}"),
                        new XAttribute("fld", valueFieldIndex),
                        new XAttribute("subtotal", ToOpenXmlSubtotal(pivot.Aggregation)))),
                new XElement(
                    SpreadsheetNamespace + "pivotTableStyleInfo",
                    new XAttribute("name", "PivotStyleLight16"),
                    new XAttribute("showRowHeaders", "1"),
                    new XAttribute("showColHeaders", "1"),
                    new XAttribute("showRowStripes", "0"),
                    new XAttribute("showColStripes", "0"),
                    new XAttribute("showLastColumn", "0"))));
    }

    private static IEnumerable<PivotCacheField> EnumerateFields(
        Worksheet worksheet,
        SpreadsheetPivotDefinition pivot)
    {
        for (var column = pivot.SourceRange.Left; column <= pivot.SourceRange.Right; column++)
        {
            var name = pivot.FirstRowContainsHeaders
                ? worksheet.GetCell(new CellAddress(pivot.SourceRange.Top, column))
                    .Value
                    .ToString()
                : $"Field {column - pivot.SourceRange.Left + 1}";
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"Field {column - pivot.SourceRange.Left + 1}";
            }

            var values = new List<CellValue>();
            for (var row = GetFirstDataRow(pivot); row <= pivot.SourceRange.Bottom; row++)
            {
                values.Add(worksheet.GetCell(new CellAddress(row, column)).Value);
            }

            yield return new PivotCacheField(column, name, values);
        }
    }

    private static XElement CreateSharedItemElement(CellValue value) =>
        value.Kind switch
        {
            CellValueKind.Blank => new XElement(SpreadsheetNamespace + "m"),
            CellValueKind.Number => new XElement(
                SpreadsheetNamespace + "n",
                new XAttribute("v", FormatNumber((double)value.RawValue!))),
            CellValueKind.Boolean => new XElement(
                SpreadsheetNamespace + "b",
                new XAttribute("v", (bool)value.RawValue! ? "1" : "0")),
            _ => new XElement(
                SpreadsheetNamespace + "s",
                new XAttribute("v", value.ToString())),
        };

    private static XElement CreateRecordItemElement(CellValue value, int sharedIndex) =>
        value.Kind switch
        {
            CellValueKind.Number => new XElement(
                SpreadsheetNamespace + "n",
                new XAttribute("v", FormatNumber((double)value.RawValue!))),
            CellValueKind.Boolean => new XElement(
                SpreadsheetNamespace + "b",
                new XAttribute("v", (bool)value.RawValue! ? "1" : "0")),
            CellValueKind.Blank => new XElement(SpreadsheetNamespace + "m"),
            _ => new XElement(
                SpreadsheetNamespace + "x",
                new XAttribute("v", sharedIndex)),
        };

    private static void AddWorkbookPivotCache(
        WorkbookPart workbookPart,
        uint cacheId,
        string relationshipId)
    {
        var workbookXml = LoadXmlPart(workbookPart);
        var pivotCaches = workbookXml.Root?.Element(SpreadsheetNamespace + "pivotCaches");
        if (pivotCaches is null)
        {
            pivotCaches = new XElement(SpreadsheetNamespace + "pivotCaches");
            workbookXml.Root?.Add(pivotCaches);
        }

        pivotCaches.Add(new XElement(
            SpreadsheetNamespace + "pivotCache",
            new XAttribute("cacheId", cacheId),
            new XAttribute(
                OfficeRelationshipNamespace + "id",
                relationshipId)));
        WriteXmlPart(workbookPart, workbookXml);
    }

    private static void RemoveManagedPivots(
        WorkbookPart workbookPart,
        CancellationToken cancellationToken)
    {
        var cachePartsToDelete = new List<PivotTableCacheDefinitionPart>();
        foreach (var worksheetPart in workbookPart.WorksheetParts)
        {
            foreach (var pivotPart in worksheetPart.PivotTableParts.ToArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pivotXml = LoadXmlPart(pivotPart);
                if (!string.Equals(
                        (string?)pivotXml.Root?.Attribute("dataCaption"),
                        ManagedDataCaption,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                if (pivotPart.PivotTableCacheDefinitionPart is { } cachePart)
                {
                    cachePartsToDelete.Add(cachePart);
                }
                worksheetPart.DeletePart(pivotPart);
            }
        }

        foreach (var cachePart in cachePartsToDelete.Distinct().ToArray())
        {
            if (workbookPart.PivotTableCacheDefinitionParts.Contains(cachePart))
            {
                RemoveWorkbookPivotCache(workbookPart, workbookPart.GetIdOfPart(cachePart));
                workbookPart.DeletePart(cachePart);
            }
        }
    }

    private static void RemoveWorkbookPivotCache(
        WorkbookPart workbookPart,
        string relationshipId)
    {
        var workbookXml = LoadXmlPart(workbookPart);
        var pivotCaches = workbookXml.Root?.Element(SpreadsheetNamespace + "pivotCaches");
        if (pivotCaches is null)
        {
            return;
        }

        foreach (var pivotCache in pivotCaches
                     .Elements(SpreadsheetNamespace + "pivotCache")
                     .Where(element => string.Equals(
                         (string?)element.Attribute(OfficeRelationshipNamespace + "id"),
                         relationshipId,
                         StringComparison.Ordinal))
                     .ToArray())
        {
            pivotCache.Remove();
        }
        if (!pivotCaches.HasElements)
        {
            pivotCaches.Remove();
        }
        WriteXmlPart(workbookPart, workbookXml);
    }

    private static HashSet<string> GetPivotNames(WorkbookPart workbookPart)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var worksheetPart in workbookPart.WorksheetParts)
        {
            foreach (var pivotPart in worksheetPart.PivotTableParts)
            {
                var name = (string?)LoadXmlPart(pivotPart).Root?.Attribute("name");
                if (!string.IsNullOrWhiteSpace(name))
                {
                    result.Add(name);
                }
            }
        }
        return result;
    }

    private static uint GetNextCacheId(WorkbookPart workbookPart)
    {
        var workbookXml = LoadXmlPart(workbookPart);
        var maximum = workbookXml.Root?
            .Element(SpreadsheetNamespace + "pivotCaches")
            ?.Elements(SpreadsheetNamespace + "pivotCache")
            .Select(static element =>
                uint.TryParse(
                    (string?)element.Attribute("cacheId"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var cacheId)
                    ? cacheId
                    : 0U)
            .DefaultIfEmpty(0U)
            .Max() ?? 0U;
        return checked(maximum + 1U);
    }

    private static IEnumerable<WorksheetMapping> EnumerateWorksheetMappings(
        SpreadsheetDocument document,
        Workbook workbook)
    {
        var workbookPart = document.WorkbookPart
            ?? throw new InvalidDataException(
                "The XLSX package does not contain a workbook part.");
        var sheets = workbookPart.Workbook?.GetFirstChild<S.Sheets>()?
            .Elements<S.Sheet>()
            .ToArray()
            ?? throw new InvalidDataException(
                "The XLSX workbook does not contain a sheets collection.");
        var count = Math.Min(sheets.Length, workbook.Worksheets.Count);
        for (var index = 0; index < count; index++)
        {
            var relationshipId = sheets[index].Id?.Value;
            if (string.IsNullOrWhiteSpace(relationshipId) ||
                workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
            {
                continue;
            }
            yield return new WorksheetMapping(workbook.Worksheets[index], worksheetPart);
        }
    }

    private static int? TryReadFieldIndex(
        XElement pivotRoot,
        string containerName,
        string elementName,
        string attributeName)
    {
        var text = (string?)pivotRoot
            .Element(SpreadsheetNamespace + containerName)
            ?.Elements(SpreadsheetNamespace + elementName)
            .FirstOrDefault()
            ?.Attribute(attributeName);
        return int.TryParse(
            text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value) &&
            value >= 0
            ? value
            : null;
    }

    private static SpreadsheetPivotAggregation ParseAggregation(string? subtotal) =>
        subtotal switch
        {
            "count" => SpreadsheetPivotAggregation.Count,
            "average" => SpreadsheetPivotAggregation.Average,
            "min" => SpreadsheetPivotAggregation.Minimum,
            "max" => SpreadsheetPivotAggregation.Maximum,
            _ => SpreadsheetPivotAggregation.Sum,
        };

    private static bool IsEquivalent(
        SpreadsheetPivotDefinition left,
        SpreadsheetPivotDefinition right) =>
        string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase) &&
        left.SourceRange.Equals(right.SourceRange) &&
        left.RowFieldColumnIndex == right.RowFieldColumnIndex &&
        left.ValueFieldColumnIndex == right.ValueFieldColumnIndex &&
        left.Aggregation == right.Aggregation &&
        left.FirstRowContainsHeaders == right.FirstRowContainsHeaders;

    private static string GetDefaultPivotLocation(
        SpreadsheetPivotDefinition pivot,
        SpreadsheetPivotProjection projection)
    {
        var top = pivot.SourceRange.Top;
        var left = pivot.SourceRange.Right + 2;
        var bottom = top + projection.Rows.Count + 1;
        var right = left + 1;
        return ToA1Range(new CellRange(
            new CellAddress(top, left),
            new CellAddress(bottom, right)));
    }

    private static int GetFirstDataRow(SpreadsheetPivotDefinition pivot) =>
        pivot.SourceRange.Top + (pivot.FirstRowContainsHeaders ? 1 : 0);

    private static int GetDataRowCount(SpreadsheetPivotDefinition pivot) =>
        pivot.SourceRange.Bottom - GetFirstDataRow(pivot) + 1;

    private static string GetUniquePivotName(
        string requestedName,
        HashSet<string> usedNames)
    {
        var baseName = SanitizePivotName(requestedName);
        var name = baseName;
        var suffix = 2;
        while (!usedNames.Add(name))
        {
            name = $"{baseName}_{suffix++}";
        }
        return name;
    }

    private static string SanitizePivotName(string name)
    {
        var sanitized = name.Trim();
        foreach (var invalid in new[] { '[', ']', ':', '*', '?', '/', '\\' })
        {
            sanitized = sanitized.Replace(invalid, '_');
        }
        return string.IsNullOrWhiteSpace(sanitized)
            ? "Pivot"
            : sanitized.Length <= 255
                ? sanitized
                : sanitized[..255];
    }

    private static string GetAggregationCaption(SpreadsheetPivotAggregation aggregation) =>
        aggregation switch
        {
            SpreadsheetPivotAggregation.Count => "Count",
            SpreadsheetPivotAggregation.Average => "Average",
            SpreadsheetPivotAggregation.Minimum => "Min",
            SpreadsheetPivotAggregation.Maximum => "Max",
            _ => "Sum",
        };

    private static string ToOpenXmlSubtotal(SpreadsheetPivotAggregation aggregation) =>
        aggregation switch
        {
            SpreadsheetPivotAggregation.Count => "count",
            SpreadsheetPivotAggregation.Average => "average",
            SpreadsheetPivotAggregation.Minimum => "min",
            SpreadsheetPivotAggregation.Maximum => "max",
            _ => "sum",
        };

    private static bool TryParseCellRange(string? reference, out CellRange range)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            range = default;
            return false;
        }

        var separatorIndex = reference.IndexOf(':');
        if (separatorIndex <= 0 ||
            separatorIndex >= reference.Length - 1 ||
            !CellAddress.TryParseA1(reference[..separatorIndex], out var first) ||
            !CellAddress.TryParseA1(reference[(separatorIndex + 1)..], out var second))
        {
            range = default;
            return false;
        }

        range = new CellRange(first, second);
        return range.RowCount > 1 && range.ColumnCount > 1;
    }

    private static string ToA1Range(CellRange range) =>
        $"{range.TopLeft.ToA1()}:{range.BottomRight.ToA1()}";

    private static string FormatNumber(double value) =>
        value.ToString("R", CultureInfo.InvariantCulture);

    private static XDocument LoadXmlPart(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        return XDocument.Load(stream, LoadOptions.PreserveWhitespace);
    }

    private static void WriteXmlPart(OpenXmlPart part, XDocument document)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(
            stream,
            new XmlWriterSettings
            {
                Encoding = new System.Text.UTF8Encoding(false),
                Indent = false,
                CloseOutput = false,
            });
        document.Save(writer);
    }

    private sealed record WorksheetMapping(
        Worksheet Worksheet,
        WorksheetPart WorksheetPart);

    private sealed record PivotCacheField(
        int ColumnIndex,
        string Name,
        IReadOnlyList<CellValue> Values);
}
