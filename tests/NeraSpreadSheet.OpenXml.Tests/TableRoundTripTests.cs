using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class TableRoundTripTests
{
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OpaqueNamespace =
        "urn:neraspreadsheet:test:table-extension";

    [TestMethod]
    public async Task FilterButtonVisibilityAndRelationshipShouldRemainStableAcrossRoundTrip()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var columnId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(default, new CellAddress(2, 0)),
            [new SpreadsheetTableColumn(columnId, "Item")],
            autoFilter: new TableAutoFilter([
                new TableFilterColumn(
                    columnId,
                    [CellValue.FromText("A")]),
            ]),
            showFilterButtons: false);
        worksheet.AddTable(table);
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var hidden = new MemoryStream();

        await serializer.SaveAsync(workbook, hidden, new OpenXmlExportOptions());
        AssertSchemaValid(hidden);
        hidden.Position = 0L;
        var loaded = await serializer.LoadAsync(hidden, new OpenXmlImportOptions());
        Assert.IsFalse(loaded.Worksheets[0].Tables.Single().ShowFilterButtons);
        CollectionAssert.AreEqual(
            new[] { CellValue.FromText("A") },
            loaded.Worksheets[0].Tables.Single().AutoFilter!.Columns.Single().Values.ToArray());

        var loadedTable = loaded.Worksheets[0].Tables.Single();
        var visible = new SpreadsheetTable(
            loadedTable.Id,
            loadedTable.Name,
            loadedTable.Range,
            loadedTable.Columns,
            loadedTable.HasHeaders,
            loadedTable.HasTotalsRow,
            loadedTable.StyleName,
            loadedTable.ShowFirstColumn,
            loadedTable.ShowLastColumn,
            loadedTable.ShowRowStripes,
            loadedTable.ShowColumnStripes,
            loadedTable.AutoFilter,
            showFilterButtons: true);
        Assert.IsTrue(loaded.Worksheets[0].RemoveTable(loadedTable.Id));
        loaded.Worksheets[0].AddTable(visible);
        await using var shown = new MemoryStream();
        await serializer.SaveAsync(loaded, shown, new OpenXmlExportOptions());
        AssertSchemaValid(shown);
        shown.Position = 0L;
        using (var document = SpreadsheetDocument.Open(shown, false))
        {
            var worksheetPart = document.WorkbookPart!.WorksheetParts.Single();
            var part = worksheetPart.TableDefinitionParts.Single();
            Assert.AreEqual($"rIdNeraTable{table.Id:N}", worksheetPart.GetIdOfPart(part));
            Assert.IsNotNull(LoadPartXml(part).Root?.Element(
                SpreadsheetNamespace + "autoFilter"));
        }
        shown.Position = 0L;
        var reloaded = await serializer.LoadAsync(shown, new OpenXmlImportOptions());
        Assert.IsTrue(reloaded.Worksheets[0].Tables.Single().ShowFilterButtons);
        CollectionAssert.AreEqual(
            new[] { CellValue.FromText("A") },
            reloaded.Worksheets[0].Tables.Single().AutoFilter!.Columns.Single().Values.ToArray());
    }

    [TestMethod]
    public async Task StandardTableIdentityColumnsStyleAndFilterRoundTrip()
    {
        var workbook = CreateWorkbook(out var table, out var statusColumnId);
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        AssertSchemaValid(stream);

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var worksheetPart = document.WorkbookPart?
                .WorksheetParts.Single()
                ?? throw new AssertFailedException(
                    "Worksheet part is missing.");
            var tablePart = worksheetPart.TableDefinitionParts.Single();
            Assert.AreEqual(
                $"rIdNeraTable{table.Id:N}",
                worksheetPart.GetIdOfPart(tablePart));
            var xml = LoadPartXml(tablePart);
            var root = xml.Root
                ?? throw new AssertFailedException(
                    "Table root is missing.");
            Assert.AreEqual("Sales", (string?)root.Attribute("displayName"));
            Assert.AreEqual("A1:C4", (string?)root.Attribute("ref"));
            Assert.AreEqual(
                "TableStyleMedium4",
                (string?)root
                    .Element(SpreadsheetNamespace + "tableStyleInfo")?
                    .Attribute("name"));
            Assert.AreEqual(
                "1",
                (string?)root
                    .Descendants(SpreadsheetNamespace + "filterColumn")
                    .Single()
                    .Attribute("colId"));
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        var loadedTable = loaded.Worksheets[0].Tables.Single();
        Assert.AreEqual(table.Id, loadedTable.Id);
        Assert.AreEqual(table.Range, loadedTable.Range);
        Assert.AreEqual("TableStyleMedium4", loadedTable.StyleName);
        Assert.AreEqual(3, loadedTable.Columns.Count);
        Assert.AreEqual(statusColumnId, loadedTable.Columns[1].Id);
        Assert.AreEqual(
            "=[@Amount]*2",
            loadedTable.Columns[2].CalculatedColumnFormula);
        Assert.IsNotNull(loadedTable.AutoFilter);
        Assert.AreEqual(1, loadedTable.AutoFilter.Columns.Count);
        Assert.AreEqual(statusColumnId, loadedTable.AutoFilter.Columns[0].ColumnId);
        Assert.AreEqual(
            "Open",
            loadedTable.AutoFilter.Columns[0].Values.Single().RawValue);
    }

    [TestMethod]
    public async Task BlankOnlyAndCustomFiltersRoundTrip()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusColumnId = Guid.NewGuid();
        var amountColumnId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "FilteredSales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(statusColumnId, "Status"),
                new SpreadsheetTableColumn(amountColumnId, "Amount"),
            ],
            autoFilter: new TableAutoFilter([
                new TableFilterColumn(
                    statusColumnId,
                    [CellValue.Blank],
                    includeBlank: true),
                new TableFilterColumn(
                    amountColumnId,
                    firstCondition: new TableFilterCondition(
                        TableFilterComparisonOperator.GreaterThan,
                        CellValue.FromNumber(1d)),
                    secondCondition: new TableFilterCondition(
                        TableFilterComparisonOperator.LessThanOrEqual,
                        CellValue.FromNumber(3d)),
                    combineWithAnd: true),
            ]));
        worksheet.AddTable(table);
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        AssertSchemaValid(stream);

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var tablePart = document.WorkbookPart?
                .WorksheetParts.Single()
                .TableDefinitionParts.Single()
                ?? throw new AssertFailedException(
                    "Table-definition part is missing.");
            var root = LoadPartXml(tablePart).Root
                ?? throw new AssertFailedException(
                    "Table root is missing.");
            var filterColumns = root
                .Descendants(SpreadsheetNamespace + "filterColumn")
                .ToArray();
            var blankFilters = filterColumns
                .Single(element =>
                    (string?)element.Attribute("colId") == "0")
                .Element(SpreadsheetNamespace + "filters")
                ?? throw new AssertFailedException(
                    "Blank filter markup is missing.");
            Assert.AreEqual(
                "1",
                (string?)blankFilters.Attribute("blank"));
            Assert.AreEqual(
                0,
                blankFilters.Elements(
                    SpreadsheetNamespace + "filter").Count());
            var customFilters = filterColumns
                .Single(element =>
                    (string?)element.Attribute("colId") == "1")
                .Element(SpreadsheetNamespace + "customFilters")
                ?? throw new AssertFailedException(
                    "Custom filter markup is missing.");
            Assert.AreEqual("1", (string?)customFilters.Attribute("and"));
            Assert.AreEqual(
                2,
                customFilters.Elements(
                    SpreadsheetNamespace + "customFilter").Count());
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        var loadedFilters = loaded.Worksheets[0]
            .Tables.Single()
            .AutoFilter?
            .Columns
            ?? throw new AssertFailedException(
                "AutoFilter was not restored.");
        var blank = loadedFilters.Single(filter =>
            filter.ColumnId == statusColumnId);
        Assert.IsTrue(blank.IncludeBlank);
        Assert.AreEqual(1, blank.Values.Count);
        Assert.IsTrue(blank.Values[0].IsBlank);
        var custom = loadedFilters.Single(filter =>
            filter.ColumnId == amountColumnId);
        Assert.AreEqual(
            TableFilterComparisonOperator.GreaterThan,
            custom.FirstCondition?.Operator);
        Assert.AreEqual(
            TableFilterComparisonOperator.LessThanOrEqual,
            custom.SecondCondition?.Operator);
        Assert.IsTrue(custom.CombineWithAnd);
    }

    [TestMethod]
    public async Task TotalsRowLabelsAndFormulasRoundTrip()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "SalesTotals",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 1)),
            [
                new SpreadsheetTableColumn(
                    Guid.NewGuid(),
                    "Item",
                    totalsRowLabel: "Total"),
                new SpreadsheetTableColumn(
                    Guid.NewGuid(),
                    "Amount",
                    totalsRowFormula: "=SUM(SalesTotals[Amount])"),
            ],
            hasTotalsRow: true,
            styleName: "TableStyleMedium3");
        worksheet.AddTable(table);
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        AssertSchemaValid(stream);

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        var loadedTable = loaded.Worksheets[0].Tables.Single();
        Assert.IsTrue(loadedTable.HasTotalsRow);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(4, 0),
                new CellAddress(4, 1)),
            loadedTable.TotalsRange);
        Assert.AreEqual("Total", loadedTable.Columns[0].TotalsRowLabel);
        Assert.AreEqual(
            "=SUM(SalesTotals[Amount])",
            loadedTable.Columns[1].TotalsRowFormula);
    }

    [TestMethod]
    public async Task MalformedColumnCountAndFilterIndexAreRejected()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();

        await using var badCount = await CreatePackageAsync(serializer);
        MutateTablePart(badCount, root =>
            root.Element(SpreadsheetNamespace + "tableColumns")!
                .SetAttributeValue("count", 99));
        badCount.Position = 0L;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await serializer.LoadAsync(
                badCount,
                new OpenXmlImportOptions()));

        await using var badFilter = await CreatePackageAsync(serializer);
        MutateTablePart(badFilter, root =>
            root.Descendants(SpreadsheetNamespace + "filterColumn")
                .Single()
                .SetAttributeValue("colId", 99));
        badFilter.Position = 0L;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await serializer.LoadAsync(
                badFilter,
                new OpenXmlImportOptions()));
    }

    [TestMethod]
    public async Task PreservationRepeatedSavesRetainTableExtensionMarkup()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var source = await CreatePackageAsync(serializer);
        MutateTablePart(source, root =>
            root.Add(new XElement(
                SpreadsheetNamespace + "extLst",
                new XElement(
                    SpreadsheetNamespace + "ext",
                    new XAttribute(
                        "uri",
                        "{8E4F3392-9D0E-4C90-89D8-2D35A64A23C6}"),
                    new XElement(
                        OpaqueNamespace + "payload",
                        new XAttribute("marker", "keep-table-extension"))))));
        source.Position = 0L;
        var workbook = await serializer.LoadAsync(
            source,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        var worksheet = workbook.Worksheets[0];
        var table = worksheet.Tables.Single();
        worksheet.RenameTableColumn(
            table.Id,
            table.Columns[2].Id,
            "DoubleAmount");

        await using var first = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            first,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        AssertTableExtension(first);
        AssertSchemaValid(first);

        worksheet.SetValue(new CellAddress(3, 0), "second-save");
        await using var second = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            second,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        AssertTableExtension(second);
        AssertSchemaValid(second);
        second.Position = 0L;
        var reloaded = await serializer.LoadAsync(
            second,
            new OpenXmlImportOptions());
        Assert.AreEqual(
            "DoubleAmount",
            reloaded.Worksheets[0].Tables.Single().Columns[2].Name);
        Assert.AreEqual(
            "second-save",
            reloaded.Worksheets[0].GetValue(new CellAddress(3, 0)));
    }

    private static Workbook CreateWorkbook(
        out SpreadsheetTable table,
        out Guid statusColumnId)
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(worksheet, "Data");
        statusColumnId = Guid.NewGuid();
        table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 2)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Item"),
                new SpreadsheetTableColumn(statusColumnId, "Status"),
                new SpreadsheetTableColumn(
                    Guid.NewGuid(),
                    "Amount",
                    calculatedColumnFormula: "=[@Amount]*2"),
            ],
            styleName: "TableStyleMedium4",
            showFirstColumn: true,
            showRowStripes: true,
            autoFilter: new TableAutoFilter([
                new TableFilterColumn(
                    statusColumnId,
                    [CellValue.FromText("Open")]),
            ]));
        worksheet.AddTable(table);
        worksheet.SetValue(new CellAddress(0, 0), "Item");
        worksheet.SetValue(new CellAddress(0, 1), "Status");
        worksheet.SetValue(new CellAddress(0, 2), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(1, 1), "Open");
        worksheet.SetValue(new CellAddress(1, 2), 1d);
        worksheet.SetValue(new CellAddress(2, 0), "B");
        worksheet.SetValue(new CellAddress(2, 1), "Closed");
        worksheet.SetValue(new CellAddress(2, 2), 2d);
        worksheet.SetValue(new CellAddress(3, 0), "C");
        worksheet.SetValue(new CellAddress(3, 1), "Open");
        worksheet.SetValue(new CellAddress(3, 2), 3d);
        return workbook;
    }

    private static async Task<MemoryStream> CreatePackageAsync(
        NeraOpenXmlWorkbookSerializer serializer)
    {
        var workbook = CreateWorkbook(out _, out _);
        var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        stream.Position = 0L;
        return stream;
    }

    private static void MutateTablePart(
        MemoryStream stream,
        Action<XElement> mutate)
    {
        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var part = document.WorkbookPart?
                .WorksheetParts.Single()
                .TableDefinitionParts.Single()
                ?? throw new AssertFailedException(
                    "Table-definition part is missing.");
            var xml = LoadPartXml(part);
            mutate(xml.Root
                ?? throw new AssertFailedException(
                    "Table root is missing."));
            SavePartXml(part, xml);
        }
        stream.Position = 0L;
    }

    private static void AssertTableExtension(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0L;
        }
        using var document = SpreadsheetDocument.Open(stream, false);
        var tablePart = document.WorkbookPart?
            .WorksheetParts.Single()
            .TableDefinitionParts.Single()
            ?? throw new AssertFailedException(
                "Table-definition part is missing.");
        var xml = LoadPartXml(tablePart);
        Assert.AreEqual(
            "keep-table-extension",
            (string?)xml.Root?
                .Descendants(OpaqueNamespace + "payload")
                .Single()
                .Attribute("marker"));
    }

    private static void AssertSchemaValid(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0L;
        }
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2013)
            .Validate(document)
            .ToArray();
        Assert.AreEqual(
            0,
            errors.Length,
            string.Join(
                Environment.NewLine,
                errors.Select(error =>
                    $"{error.Path?.XPath}: {error.Description}")));
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
            });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
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
                Encoding = new System.Text.UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                Indent = false,
            });
        document.Save(writer, SaveOptions.DisableFormatting);
    }
}
