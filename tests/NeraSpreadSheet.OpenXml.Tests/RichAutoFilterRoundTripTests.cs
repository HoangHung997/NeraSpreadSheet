using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class RichAutoFilterRoundTripTests
{
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace ProducerNamespace =
        "urn:neraspreadsheet:test:filter-producer";

    [TestMethod]
    public async Task WorksheetRichCriteriaAndSortStateRoundTripSchemaValid()
    {
        var workbook = CreateRichWorksheetWorkbook();
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());
        AssertSchemaValid(stream);
        stream.Position = 0;
        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());
        var filter = loaded.Worksheets[0].AutoFilter ?? throw new AssertFailedException("AutoFilter missing.");

        Assert.AreEqual(5, filter.Columns.Count);
        Assert.AreEqual(SpreadsheetFilterDateGrouping.Month, filter.Columns[0].DateGroups.Single().Grouping);
        Assert.IsTrue(filter.Columns[1].TopBottom!.Percent);
        Assert.AreEqual(SpreadsheetDynamicFilterType.ThisMonth, filter.Columns[2].DynamicFilter!.Type);
        Assert.AreEqual(new ColorRgba(30, 120, 210), filter.Columns[3].ColorFilter!.Color);
        Assert.AreEqual("3TrafficLights1", filter.Columns[4].IconFilter!.IconSet);
        Assert.IsTrue(filter.SortState!.Conditions.Single().Descending);
    }

    [TestMethod]
    public async Task TableRichCriteriaAndSortStateRoundTripSchemaValid()
    {
        var workbook = CreateRichWorksheetWorkbook();
        var worksheet = workbook.Worksheets[0];
        var existing = worksheet.AutoFilter!;
        worksheet.SetAutoFilter(null);
        var columns = Enumerable.Range(0, existing.Range.ColumnCount)
            .Select(index => new SpreadsheetTableColumn(Guid.NewGuid(), $"Column{index + 1}"))
            .ToArray();
        var tableFilters = existing.Columns.Select(column => new TableFilterColumn(
            columns[column.ColumnOffset].Id,
            column.Values,
            column.IncludeBlank,
            column.FirstCondition,
            column.SecondCondition,
            column.CombineWithAnd,
            column.DateGroups,
            column.TopBottom,
            column.DynamicFilter,
            column.ColorFilter,
            column.IconFilter));
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "RichTable",
            existing.Range,
            columns,
            autoFilter: new TableAutoFilter(tableFilters, existing.SortState)));
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());
        AssertSchemaValid(stream);
        stream.Position = 0;
        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());
        var filter = loaded.Worksheets[0].Tables.Single().AutoFilter!;

        Assert.AreEqual(5, filter.Columns.Count);
        Assert.AreEqual(SpreadsheetDynamicFilterType.ThisMonth, filter.Columns[2].DynamicFilter!.Type);
        Assert.AreEqual(SpreadsheetFilterColorKind.Fill, filter.Columns[3].ColorFilter!.Kind);
        Assert.AreEqual(4, filter.SortState!.Conditions.Single().ColumnOffset);
    }

    [TestMethod]
    public async Task CalendarMonthDynamicFiltersUseSpreadsheetMlMonthTokens()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            [new WorksheetAutoFilterColumn(
                0,
                dynamicFilter: new SpreadsheetDynamicFilter(
                    SpreadsheetDynamicFilterType.September))]));
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());
        AssertSchemaValid(stream);
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var xml = Load(document.WorkbookPart!.WorksheetParts.Single());
            Assert.AreEqual(
                "M9",
                (string?)xml.Descendants(SpreadsheetNamespace + "dynamicFilter")
                    .Single()
                    .Attribute("type"));
        }
        stream.Position = 0;

        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());

        Assert.AreEqual(
            SpreadsheetDynamicFilterType.September,
            loaded.Worksheets[0].AutoFilter!.Columns.Single().DynamicFilter!.Type);
    }

    [TestMethod]
    public async Task CustomFilterLiteralWildcardsRoundTripAsEscapedText()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            [new WorksheetAutoFilterColumn(
                0,
                firstCondition: new TableFilterCondition(
                    TableFilterComparisonOperator.Equal,
                    CellValue.FromText("abc*?~")))]));
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());
        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var xml = Load(document.WorkbookPart!.WorksheetParts.Single());
            Assert.AreEqual(
                "abc~*~?~~",
                (string?)xml.Descendants(SpreadsheetNamespace + "customFilter")
                    .Single()
                    .Attribute("val"));
        }
        stream.Position = 0;

        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());
        var condition = loaded.Worksheets[0].AutoFilter!.Columns.Single()
            .FirstCondition!;

        Assert.AreEqual(TableFilterComparisonOperator.Equal, condition.Operator);
        Assert.AreEqual("abc*?~", condition.Value.RawValue);
    }

    [TestMethod]
    public async Task UnsupportedStandardFilterMarkupShouldRejectStrictAndSurvivePreservedSave()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        foreach (var mutation in new[] { "wildcard", "calendar", "sort", "left-to-right" })
        {
            await using var source = new MemoryStream();
            await serializer.SaveAsync(
                CreateRichWorksheetWorkbook(),
                source,
                new OpenXmlExportOptions());
            source.Position = 0;
            using (var document = SpreadsheetDocument.Open(source, true))
            {
                var part = document.WorkbookPart!.WorksheetParts.Single();
                var xml = Load(part);
                var autoFilter = xml.Root!.Element(SpreadsheetNamespace + "autoFilter")!;
                if (mutation == "wildcard")
                {
                    var column = autoFilter.Elements(SpreadsheetNamespace + "filterColumn").First();
                    column.RemoveNodes();
                    column.Add(new XElement(
                        SpreadsheetNamespace + "customFilters",
                        new XElement(
                            SpreadsheetNamespace + "customFilter",
                            new XAttribute("operator", "equal"),
                            new XAttribute("val", "a?b"))));
                }
                else if (mutation == "calendar")
                {
                    autoFilter.Descendants(SpreadsheetNamespace + "filters").First()
                        .SetAttributeValue("calendarType", "gregorian");
                }
                else if (mutation == "sort")
                {
                    autoFilter.Element(SpreadsheetNamespace + "sortState")!
                        .SetAttributeValue("sortMethod", "stroke");
                }
                else
                {
                    autoFilter.Element(SpreadsheetNamespace + "sortState")!
                        .SetAttributeValue("columnSort", 1);
                }
                Save(part, xml);
            }
            source.Position = 0;
            await Assert.ThrowsExactlyAsync<InvalidDataException>(() =>
                serializer.LoadAsync(source, new OpenXmlImportOptions()));
            source.Position = 0;
            var preserved = await serializer.LoadAsync(
                source,
                new OpenXmlImportOptions { PreserveUnknownParts = true });
            await using var destination = new MemoryStream();

            await serializer.SaveAsync(
                preserved,
                destination,
                new OpenXmlExportOptions { PreserveUnknownParts = true });

            destination.Position = 0;
            using var saved = SpreadsheetDocument.Open(destination, false);
            var savedXml = Load(saved.WorkbookPart!.WorksheetParts.Single());
            var savedFilter = savedXml.Root!.Element(SpreadsheetNamespace + "autoFilter")!;
            if (mutation == "wildcard")
            {
                Assert.AreEqual(
                    "a?b",
                    (string?)savedFilter.Descendants(SpreadsheetNamespace + "customFilter")
                        .First()
                        .Attribute("val"));
            }
            else if (mutation == "calendar")
            {
                Assert.AreEqual(
                    "gregorian",
                    (string?)savedFilter.Descendants(SpreadsheetNamespace + "filters")
                        .First()
                        .Attribute("calendarType"));
            }
            else if (mutation == "sort")
            {
                Assert.AreEqual(
                    "stroke",
                    (string?)savedFilter.Element(SpreadsheetNamespace + "sortState")!
                        .Attribute("sortMethod"));
            }
            else
            {
                Assert.AreEqual(
                    "1",
                    (string?)savedFilter.Element(SpreadsheetNamespace + "sortState")!
                        .Attribute("columnSort"));
            }
        }
    }

    [TestMethod]
    public async Task PreservedUnsupportedConditionalFormattingShouldKeepColorFilterDxfBinding()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var source = new MemoryStream();
        await serializer.SaveAsync(
            CreateRichWorksheetWorkbook(),
            source,
            new OpenXmlExportOptions());
        source.Position = 0;
        using (var document = SpreadsheetDocument.Open(source, true))
        {
            var workbookPart = document.WorkbookPart!;
            var stylesPart = workbookPart.WorkbookStylesPart!;
            var styles = Load(stylesPart);
            var dxfs = styles.Root!.Element(SpreadsheetNamespace + "dxfs")!;
            dxfs.AddFirst(new XElement(
                SpreadsheetNamespace + "dxf",
                new XElement(
                    SpreadsheetNamespace + "fill",
                    new XElement(
                        SpreadsheetNamespace + "patternFill",
                        new XAttribute("patternType", "solid"),
                        new XElement(
                            SpreadsheetNamespace + "fgColor",
                            new XAttribute("rgb", "FFFF0000")),
                        new XElement(
                            SpreadsheetNamespace + "bgColor",
                            new XAttribute("indexed", 64))))));
            dxfs.SetAttributeValue("count", dxfs.Elements(SpreadsheetNamespace + "dxf").Count());
            Save(stylesPart, styles);

            var worksheetPart = workbookPart.WorksheetParts.Single();
            var worksheet = Load(worksheetPart);
            worksheet.Descendants(SpreadsheetNamespace + "colorFilter")
                .Single()
                .SetAttributeValue("dxfId", 1);
            var sheetData = worksheet.Root!.Element(SpreadsheetNamespace + "sheetData")!;
            sheetData.AddAfterSelf(new XElement(
                SpreadsheetNamespace + "conditionalFormatting",
                new XAttribute("sqref", "A1"),
                new XElement(
                    SpreadsheetNamespace + "cfRule",
                    new XAttribute("type", "colorScale"),
                    new XAttribute("priority", 1),
                    new XElement(
                        SpreadsheetNamespace + "colorScale",
                        new XElement(SpreadsheetNamespace + "cfvo", new XAttribute("type", "min")),
                        new XElement(SpreadsheetNamespace + "cfvo", new XAttribute("type", "max")),
                        new XElement(SpreadsheetNamespace + "color", new XAttribute("rgb", "FFFF0000")),
                        new XElement(SpreadsheetNamespace + "color", new XAttribute("rgb", "FF00FF00"))))));
            Save(worksheetPart, worksheet);
        }
        source.Position = 0;
        var loaded = await serializer.LoadAsync(
            source,
            new OpenXmlImportOptions { PreserveUnknownParts = true });
        await using var destination = new MemoryStream();

        await serializer.SaveAsync(
            loaded,
            destination,
            new OpenXmlExportOptions { PreserveUnknownParts = true });

        AssertSchemaValid(destination);
        destination.Position = 0;
        using var saved = SpreadsheetDocument.Open(destination, false);
        var savedWorksheet = Load(saved.WorkbookPart!.WorksheetParts.Single());
        Assert.AreEqual(
            "1",
            (string?)savedWorksheet.Descendants(SpreadsheetNamespace + "colorFilter")
                .Single()
                .Attribute("dxfId"));
        Assert.IsNotNull(savedWorksheet.Descendants(SpreadsheetNamespace + "colorScale")
            .SingleOrDefault());
        var savedStyles = Load(saved.WorkbookPart!.WorkbookStylesPart!);
        var dxf = savedStyles.Root!.Element(SpreadsheetNamespace + "dxfs")!
            .Elements(SpreadsheetNamespace + "dxf")
            .ElementAt(1);
        Assert.AreEqual(
            "FF1E78D2",
            (string?)dxf.Descendants(SpreadsheetNamespace + "fgColor")
                .Single()
                .Attribute("rgb"));
    }

    [TestMethod]
    public async Task ProducerExtensionsOnRichCriteriaAndSortSurvivePreservedSave()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var source = new MemoryStream();
        await serializer.SaveAsync(CreateRichWorksheetWorkbook(), source, new OpenXmlExportOptions());
        source.Position = 0;
        using (var document = SpreadsheetDocument.Open(source, true))
        {
            var part = document.WorkbookPart!.WorksheetParts.Single();
            var xml = Load(part);
            var autoFilter = xml.Root!.Element(SpreadsheetNamespace + "autoFilter")!;
            autoFilter.Elements(SpreadsheetNamespace + "filterColumn").First()
                .SetAttributeValue(ProducerNamespace + "columnMarker", "keep-column");
            autoFilter.Descendants(SpreadsheetNamespace + "filters").First()
                .SetAttributeValue(ProducerNamespace + "definitionMarker", "keep-definition");
            autoFilter.Element(SpreadsheetNamespace + "sortState")!
                .SetAttributeValue(ProducerNamespace + "sortMarker", "keep-sort");
            Save(part, xml);
        }
        source.Position = 0;
        var workbook = await serializer.LoadAsync(source, new OpenXmlImportOptions { PreserveUnknownParts = true });
        await using var destination = new MemoryStream();

        await serializer.SaveAsync(workbook, destination, new OpenXmlExportOptions { PreserveUnknownParts = true });
        destination.Position = 0;
        using var saved = SpreadsheetDocument.Open(destination, false);
        var savedXml = Load(saved.WorkbookPart!.WorksheetParts.Single());
        Assert.AreEqual("keep-column", (string?)savedXml.Descendants(SpreadsheetNamespace + "filterColumn").First().Attribute(ProducerNamespace + "columnMarker"));
        Assert.AreEqual("keep-definition", (string?)savedXml.Descendants(SpreadsheetNamespace + "filters").First().Attribute(ProducerNamespace + "definitionMarker"));
        Assert.AreEqual("keep-sort", (string?)savedXml.Descendants(SpreadsheetNamespace + "sortState").Single().Attribute(ProducerNamespace + "sortMarker"));
    }

    [TestMethod]
    public async Task UnsupportedProducerCriterionSurvivesWorksheetAndTablePreservedSaves()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        foreach (var useTable in new[] { false, true })
        {
            var workbook = CreateRichWorksheetWorkbook();
            if (useTable)
            {
                var worksheet = workbook.Worksheets[0];
                var filter = worksheet.AutoFilter!;
                worksheet.SetAutoFilter(null);
                var columns = Enumerable.Range(0, filter.Range.ColumnCount)
                    .Select(index => new SpreadsheetTableColumn(Guid.NewGuid(), $"Field{index}"))
                    .ToArray();
                worksheet.AddTable(new SpreadsheetTable(Guid.NewGuid(), "OpaqueTable", filter.Range, columns,
                    autoFilter: new TableAutoFilter([
                        new TableFilterColumn(columns[0].Id, topBottom: new SpreadsheetTopBottomFilter(true, false, 1)),
                    ])));
            }
            await using var source = new MemoryStream();
            await serializer.SaveAsync(workbook, source, new OpenXmlExportOptions());
            source.Position = 0;
            using (var document = SpreadsheetDocument.Open(source, true))
            {
                OpenXmlPart part = useTable
                    ? document.WorkbookPart!.WorksheetParts.Single().TableDefinitionParts.Single()
                    : document.WorkbookPart!.WorksheetParts.Single();
                var xml = Load(part);
                var column = xml.Descendants(SpreadsheetNamespace + "filterColumn").First();
                column.RemoveNodes();
                column.Add(new XElement(ProducerNamespace + "futureFilter", new XAttribute("token", "retain")));
                Save(part, xml);
            }
            source.Position = 0;
            var loaded = await serializer.LoadAsync(source, new OpenXmlImportOptions { PreserveUnknownParts = true });
            await using var destination = new MemoryStream();
            await serializer.SaveAsync(loaded, destination, new OpenXmlExportOptions { PreserveUnknownParts = true });
            destination.Position = 0;
            using var saved = SpreadsheetDocument.Open(destination, false);
            OpenXmlPart savedPart = useTable
                ? saved.WorkbookPart!.WorksheetParts.Single().TableDefinitionParts.Single()
                : saved.WorkbookPart!.WorksheetParts.Single();
            Assert.AreEqual("retain", (string?)Load(savedPart).Descendants(ProducerNamespace + "futureFilter").Single().Attribute("token"));
        }
    }

    private static Workbook CreateRichWorksheetWorkbook()
    {
        var workbook = new Workbook { DateSystem = ExcelDateSystem.Date1904 };
        var worksheet = workbook.Worksheets[0];
        var blue = new ColorRgba(30, 120, 210);
        for (var column = 0; column < 5; column++)
        {
            worksheet.SetValue(new CellAddress(0, column), $"Column{column + 1}");
            worksheet.SetValue(new CellAddress(1, column), column == 0 ? 44807d : column + 1d);
            worksheet.SetValue(new CellAddress(2, column), column == 0 ? 44776d : column + 10d);
        }
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 4)),
            [
                new WorksheetAutoFilterColumn(0, dateGroups: [new SpreadsheetFilterDateGroup(2026, SpreadsheetFilterDateGrouping.Month, month: 9)]),
                new WorksheetAutoFilterColumn(1, topBottom: new SpreadsheetTopBottomFilter(true, true, 10)),
                new WorksheetAutoFilterColumn(2, dynamicFilter: new SpreadsheetDynamicFilter(SpreadsheetDynamicFilterType.ThisMonth)),
                new WorksheetAutoFilterColumn(3, colorFilter: new SpreadsheetColorFilter(SpreadsheetFilterColorKind.Fill, blue)),
                new WorksheetAutoFilterColumn(4, iconFilter: new SpreadsheetIconFilter("3TrafficLights1", 2)),
            ],
            sortState: new SpreadsheetFilterSortState([new SpreadsheetFilterSortCondition(4, descending: true)])));
        return workbook;
    }

    private static void AssertSchemaValid(Stream stream)
    {
        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(DocumentFormat.OpenXml.FileFormatVersions.Office2013).Validate(document).ToArray();
        Assert.AreEqual(0, errors.Length, string.Join(Environment.NewLine, errors.Select(static error => error.Description)));
    }

    private static XDocument Load(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static void Save(OpenXmlPart part, XDocument document)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = false });
        document.Save(writer, SaveOptions.DisableFormatting);
    }
}
