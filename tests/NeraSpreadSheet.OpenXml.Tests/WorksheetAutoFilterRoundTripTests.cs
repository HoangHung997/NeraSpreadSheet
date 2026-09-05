using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class WorksheetAutoFilterRoundTripTests
{
    private const string OpaqueRelationshipId =
        "rWorksheetAutoFilterOpaque";
    private const string OpaqueRelationshipType =
        "urn:neraspreadsheet:test:worksheet-autofilter-opaque";
    private const string OpaqueContentType =
        "application/vnd.neraspreadsheet.test.worksheet-autofilter-opaque";

    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OpaqueNamespace =
        "urn:neraspreadsheet:test:worksheet-autofilter-extension";
    private static readonly byte[] OpaqueBytes =
        Encoding.UTF8.GetBytes("worksheet-autofilter-opaque-bytes");

    [TestMethod]
    public async Task ValueAndCustomFiltersRoundTripAndRemainSchemaValid()
    {
        var workbook = CreateWorkbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 1)),
            [
                new WorksheetAutoFilterColumn(
                    0,
                    [CellValue.FromText("Open")],
                    includeBlank: true),
                new WorksheetAutoFilterColumn(
                    1,
                    firstCondition: new TableFilterCondition(
                        TableFilterComparisonOperator.GreaterThan,
                        CellValue.FromNumber(10d)),
                    secondCondition: new TableFilterCondition(
                        TableFilterComparisonOperator.LessThanOrEqual,
                        CellValue.FromNumber(30d)),
                    combineWithAnd: true),
            ]));
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
        var loadedWorksheet = loaded.Worksheets[0];
        var filter = loadedWorksheet.AutoFilter
            ?? throw new AssertFailedException(
                "Worksheet AutoFilter was not restored.");
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 1)),
            filter.Range);
        Assert.AreEqual(2, filter.Columns.Count);
        Assert.IsTrue(filter.Columns[0].IncludeBlank);
        Assert.AreEqual(
            TableFilterComparisonOperator.GreaterThan,
            filter.Columns[1].FirstCondition?.Operator);
        Assert.AreEqual(
            TableFilterComparisonOperator.LessThanOrEqual,
            filter.Columns[1].SecondCondition?.Operator);
        Assert.IsTrue(filter.Columns[1].CombineWithAnd);

        var snapshot = WorksheetSnapshot.Capture(loadedWorksheet);
        Assert.IsTrue(snapshot.IsRowVisible(0));
        Assert.IsFalse(snapshot.IsRowVisible(1));
        Assert.IsFalse(snapshot.IsRowVisible(2));
        Assert.IsTrue(snapshot.IsRowVisible(3));
        Assert.IsFalse(snapshot.IsRowVisible(4));
    }

    [TestMethod]
    public async Task ContainsFilterUsesSpreadsheetWildcardMarkup()
    {
        var workbook = CreateWorkbook();
        workbook.Worksheets[0].SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 1)),
            [
                new WorksheetAutoFilterColumn(
                    0,
                    firstCondition: new TableFilterCondition(
                        TableFilterComparisonOperator.Contains,
                        CellValue.FromText("port"))),
            ]));
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var worksheetPart = document.WorkbookPart?
                .WorksheetParts.Single()
                ?? throw new AssertFailedException(
                    "Worksheet part is missing.");
            var xml = LoadPartXml(worksheetPart);
            var custom = xml.Descendants(
                    SpreadsheetNamespace + "customFilter")
                .Single();
            Assert.AreEqual(
                "equal",
                (string?)custom.Attribute("operator"));
            Assert.AreEqual(
                "*port*",
                (string?)custom.Attribute("val"));
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        Assert.AreEqual(
            TableFilterComparisonOperator.Contains,
            loaded.Worksheets[0]
                .AutoFilter?
                .Columns.Single()
                .FirstCondition?
                .Operator);
    }

    [TestMethod]
    public async Task DuplicateAndUnsupportedFiltersAreRejected()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();

        await using var duplicate = await CreateFilteredPackageAsync(serializer);
        MutateWorksheet(duplicate, root =>
        {
            var filter = root.Element(
                SpreadsheetNamespace + "autoFilter")
                ?? throw new AssertFailedException(
                    "AutoFilter markup is missing.");
            filter.AddAfterSelf(new XElement(filter));
        });
        duplicate.Position = 0L;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await serializer.LoadAsync(
                duplicate,
                new OpenXmlImportOptions()));

        await using var unsupported = await CreateFilteredPackageAsync(serializer);
        MutateWorksheet(unsupported, root =>
        {
            var filterColumn = root
                .Descendants(SpreadsheetNamespace + "filterColumn")
                .Single();
            filterColumn.RemoveNodes();
            filterColumn.Add(new XElement(
                SpreadsheetNamespace + "unsupportedFilter"));
        });
        unsupported.Position = 0L;
        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await serializer.LoadAsync(
                unsupported,
                new OpenXmlImportOptions()));
    }

    [TestMethod]
    public async Task OpaqueFilterExtensionAndPartSurviveRepeatedPreservedSaves()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var source = await CreateOpaqueFilteredPackageAsync(serializer);
        source.Position = 0L;
        var workbook = await serializer.LoadAsync(
            source,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        SetStatusFilter(workbook.Worksheets[0], "Closed");

        await using var first = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            first,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        var firstSnapshot = InspectOpaqueFilter(first);
        Assert.AreEqual("Closed", firstSnapshot.FilterValue);
        CollectionAssert.AreEqual(OpaqueBytes, firstSnapshot.PartBytes);
        Assert.IsTrue(firstSnapshot.HasExtensionMarker);
        AssertSchemaValid(first);

        SetStatusFilter(workbook.Worksheets[0], "Open");
        await using var second = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            second,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        var secondSnapshot = InspectOpaqueFilter(second);
        Assert.AreEqual("Open", secondSnapshot.FilterValue);
        CollectionAssert.AreEqual(OpaqueBytes, secondSnapshot.PartBytes);
        Assert.IsTrue(secondSnapshot.HasExtensionMarker);
        AssertSchemaValid(second);
    }

    private static Workbook CreateWorkbook()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Status");
        worksheet.SetValue(new CellAddress(0, 1), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 1), 25d);
        worksheet.SetValue(new CellAddress(4, 0), "Open");
        worksheet.SetValue(new CellAddress(4, 1), 40d);
        return workbook;
    }

    private static void SetStatusFilter(
        Worksheet worksheet,
        string value) =>
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 1)),
            [
                new WorksheetAutoFilterColumn(
                    0,
                    [CellValue.FromText(value)]),
            ]));

    private static async Task<MemoryStream> CreateFilteredPackageAsync(
        NeraOpenXmlWorkbookSerializer serializer)
    {
        var workbook = CreateWorkbook();
        SetStatusFilter(workbook.Worksheets[0], "Open");
        var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        stream.Position = 0L;
        return stream;
    }

    private static async Task<MemoryStream> CreateOpaqueFilteredPackageAsync(
        NeraOpenXmlWorkbookSerializer serializer)
    {
        var stream = await CreateFilteredPackageAsync(serializer);
        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var worksheetPart = document.WorkbookPart?
                .WorksheetParts.Single()
                ?? throw new AssertFailedException(
                    "Worksheet part is missing.");
            var opaquePart = worksheetPart.AddExtendedPart(
                OpaqueRelationshipType,
                OpaqueContentType,
                ".bin",
                OpaqueRelationshipId);
            using (var opaqueStream = opaquePart.GetStream(
                       FileMode.Create,
                       FileAccess.Write))
            {
                opaqueStream.Write(OpaqueBytes);
            }

            var xml = LoadPartXml(worksheetPart);
            var autoFilter = xml.Root?
                .Element(SpreadsheetNamespace + "autoFilter")
                ?? throw new AssertFailedException(
                    "AutoFilter markup is missing.");
            autoFilter.Add(new XElement(
                SpreadsheetNamespace + "extLst",
                new XElement(
                    SpreadsheetNamespace + "ext",
                    new XAttribute(
                        "uri",
                        "urn:neraspreadsheet:test:worksheet-autofilter-extension"),
                    new XElement(
                        OpaqueNamespace + "payload",
                        new XAttribute("marker", "preserve")))));
            SavePartXml(worksheetPart, xml);
        }
        stream.Position = 0L;
        return stream;
    }

    private static OpaqueFilterSnapshot InspectOpaqueFilter(
        MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var worksheetPart = document.WorkbookPart?
            .WorksheetParts.Single()
            ?? throw new AssertFailedException(
                "Worksheet part is missing.");
        var opaquePart = worksheetPart.GetPartById(OpaqueRelationshipId);
        byte[] bytes;
        using (var opaqueStream = opaquePart.GetStream(
                   FileMode.Open,
                   FileAccess.Read))
        using (var buffer = new MemoryStream())
        {
            opaqueStream.CopyTo(buffer);
            bytes = buffer.ToArray();
        }
        var xml = LoadPartXml(worksheetPart);
        var filterValue = (string?)xml
            .Descendants(SpreadsheetNamespace + "filter")
            .Single()
            .Attribute("val")
            ?? throw new AssertFailedException(
                "Filter value is missing.");
        var hasMarker = xml
            .Descendants(OpaqueNamespace + "payload")
            .Any(element =>
                string.Equals(
                    (string?)element.Attribute("marker"),
                    "preserve",
                    StringComparison.Ordinal));
        return new OpaqueFilterSnapshot(
            filterValue,
            bytes,
            hasMarker);
    }

    private static void MutateWorksheet(
        MemoryStream stream,
        Action<XElement> mutate)
    {
        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var worksheetPart = document.WorkbookPart?
                .WorksheetParts.Single()
                ?? throw new AssertFailedException(
                    "Worksheet part is missing.");
            var xml = LoadPartXml(worksheetPart);
            mutate(xml.Root
                ?? throw new AssertFailedException(
                    "Worksheet root is missing."));
            SavePartXml(worksheetPart, xml);
        }
        stream.Position = 0L;
    }

    private static void AssertSchemaValid(MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(
                DocumentFormat.OpenXml.FileFormatVersions.Office2013)
            .Validate(document)
            .ToArray();
        Assert.AreEqual(
            0,
            errors.Length,
            string.Join(
                Environment.NewLine,
                errors.Select(static error => error.Description)));
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
                Encoding = new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                Indent = false,
            });
        document.Save(writer, SaveOptions.DisableFormatting);
    }

    private sealed record OpaqueFilterSnapshot(
        string FilterValue,
        byte[] PartBytes,
        bool HasExtensionMarker);
}
