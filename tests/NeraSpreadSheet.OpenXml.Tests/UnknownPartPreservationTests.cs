using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class UnknownPartPreservationTests
{
    private const string WorkbookPartRelationshipId =
        "rOpaqueWorkbook";
    private const string WorksheetPartRelationshipId =
        "rOpaqueWorksheet";
    private const string ExternalRelationshipId =
        "rOpaqueExternal";
    private const string WorkbookRelationshipType =
        "urn:neraspreadsheet:test:opaque-workbook";
    private const string WorksheetRelationshipType =
        "urn:neraspreadsheet:test:opaque-worksheet";
    private const string ExternalRelationshipType =
        "urn:neraspreadsheet:test:opaque-external";
    private const string WorkbookContentType =
        "application/vnd.neraspreadsheet.test.workbook-opaque";
    private const string WorksheetContentType =
        "application/vnd.neraspreadsheet.test.worksheet-opaque";
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OpaqueNamespace =
        "urn:neraspreadsheet:test:opaque-markup";
    private static readonly Uri ExternalTarget =
        new(
            "https://example.invalid/nera-opaque",
            UriKind.Absolute);
    private static readonly byte[] WorkbookOpaqueBytes =
        Encoding.UTF8.GetBytes(
            "Nera opaque workbook bytes \u0000 \u0001");
    private static readonly byte[] WorksheetOpaqueBytes =
        Encoding.UTF8.GetBytes(
            "<opaque>worksheet payload</opaque>");

    [TestMethod]
    public async Task OpaquePartsRelationshipsAndMarkupSurviveRepeatedSaves()
    {
        var serializer =
            new NeraOpenXmlWorkbookSerializer();
        await using var sourceStream =
            await CreateOpaquePackageAsync(
                serializer);
        var sourceSnapshot =
            InspectOpaquePackage(sourceStream);
        sourceStream.Position = 0L;
        var workbook = await serializer.LoadAsync(
            sourceStream,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        workbook.RenameWorksheet(
            workbook.Worksheets[0],
            "Renamed");
        workbook.Worksheets[0].SetValue(
            new CellAddress(1, 1),
            "first edit");

        await using var firstOutput =
            new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            firstOutput,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        var firstSnapshot =
            InspectOpaquePackage(firstOutput);
        AssertOpaqueSnapshot(
            sourceSnapshot,
            firstSnapshot);
        Assert.AreEqual(
            "Renamed",
            firstSnapshot.WorksheetName);
        Assert.IsTrue(
            firstSnapshot.HasWorkbookMarker);
        Assert.IsTrue(
            firstSnapshot.HasWorksheetMarker);
        Assert.IsTrue(
            firstSnapshot.HasStylesMarker);

        firstOutput.Position = 0L;
        var firstReload = await serializer.LoadAsync(
            firstOutput,
            new OpenXmlImportOptions());
        Assert.AreEqual(
            "first edit",
            firstReload.Worksheets[0]
                .GetCell(new CellAddress(1, 1))
                .Value.RawValue);

        workbook.Worksheets[0].SetValue(
            new CellAddress(2, 2),
            "second edit");
        await using var secondOutput =
            new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            secondOutput,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        var secondSnapshot =
            InspectOpaquePackage(secondOutput);
        AssertOpaqueSnapshot(
            firstSnapshot,
            secondSnapshot);

        secondOutput.Position = 0L;
        var secondReload = await serializer.LoadAsync(
            secondOutput,
            new OpenXmlImportOptions());
        Assert.AreEqual(
            "second edit",
            secondReload.Worksheets[0]
                .GetCell(new CellAddress(2, 2))
                .Value.RawValue);
    }

    [TestMethod]
    public async Task WorksheetTopologyChangeFailsBeforeDestinationMutation()
    {
        var serializer =
            new NeraOpenXmlWorkbookSerializer();
        await using var sourceStream =
            await CreateOpaquePackageAsync(
                serializer);
        sourceStream.Position = 0L;
        var workbook = await serializer.LoadAsync(
            sourceStream,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        workbook.AddWorksheet("UnsupportedTopology");

        var sentinel =
            Encoding.UTF8.GetBytes("destination-sentinel");
        await using var destination =
            new MemoryStream();
        await destination.WriteAsync(sentinel);
        destination.Position = 0L;

        await Assert.ThrowsExactlyAsync<
            InvalidOperationException>(async () =>
            await serializer.SaveAsync(
                workbook,
                destination,
                new OpenXmlExportOptions
                {
                    PreserveUnknownParts = true,
                }));

        CollectionAssert.AreEqual(
            sentinel,
            destination.ToArray());
    }

    [TestMethod]
    public async Task WorksheetReferenceReplacementFailsBeforeDestinationMutation()
    {
        var serializer =
            new NeraOpenXmlWorkbookSerializer();
        await using var sourceStream =
            await CreateOpaquePackageAsync(
                serializer,
                worksheetCount: 2);
        sourceStream.Position = 0L;
        var workbook = await serializer.LoadAsync(
            sourceStream,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        workbook.RemoveWorksheet(workbook.Worksheets[1]);
        workbook.AddWorksheet("Replacement");

        var sentinel =
            Encoding.UTF8.GetBytes("same-count-destination-sentinel");
        await using var destination =
            new MemoryStream();
        await destination.WriteAsync(sentinel);
        destination.Position = 0L;

        await Assert.ThrowsExactlyAsync<
            InvalidOperationException>(async () =>
            await serializer.SaveAsync(
                workbook,
                destination,
                new OpenXmlExportOptions
                {
                    PreserveUnknownParts = true,
                }));

        CollectionAssert.AreEqual(
            sentinel,
            destination.ToArray());
    }

    private static async Task<MemoryStream> CreateOpaquePackageAsync(
        NeraOpenXmlWorkbookSerializer serializer,
        int worksheetCount = 1)
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(
            default,
            "original");
        for (var index = 2; index <= worksheetCount; index++)
        {
            workbook.AddWorksheet($"Sheet{index}");
        }

        var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document =
               SpreadsheetDocument.Open(
                   stream,
                   true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new AssertFailedException(
                    "The test package is missing its workbook part.");
            var worksheetPart =
                GetFirstWorksheetPart(workbookPart);
            var workbookOpaque =
                workbookPart.AddExtendedPart(
                    WorkbookRelationshipType,
                    WorkbookContentType,
                    ".bin",
                    WorkbookPartRelationshipId);
            WritePartBytes(
                workbookOpaque,
                WorkbookOpaqueBytes);
            var worksheetOpaque =
                worksheetPart.AddExtendedPart(
                    WorksheetRelationshipType,
                    WorksheetContentType,
                    ".xml",
                    WorksheetPartRelationshipId);
            WritePartBytes(
                worksheetOpaque,
                WorksheetOpaqueBytes);
            worksheetPart.AddExternalRelationship(
                ExternalRelationshipType,
                ExternalTarget,
                ExternalRelationshipId);

            AddOpaqueMarker(
                workbookPart,
                "workbook");
            AddOpaqueMarker(
                worksheetPart,
                "worksheet");
            var stylesPart =
                workbookPart.WorkbookStylesPart
                ?? throw new AssertFailedException(
                    "The test package is missing its style part.");
            AddOpaqueMarker(
                stylesPart,
                "styles");

        }

        stream.Position = 0L;
        return stream;
    }

    private static WorksheetPart GetFirstWorksheetPart(
        WorkbookPart workbookPart)
    {
        var workbookXml = workbookPart.Workbook
            ?? throw new AssertFailedException(
                "The test package is missing workbook markup.");
        var firstSheetId = workbookXml
            .GetFirstChild<Sheets>()?
            .Elements<Sheet>()
            .First()
            .Id?
            .Value
            ?? throw new AssertFailedException(
                "The test package is missing its first worksheet relationship.");
        return (WorksheetPart)workbookPart.GetPartById(firstSheetId);
    }

    private static OpaqueSnapshot InspectOpaquePackage(
        MemoryStream stream)
    {
        stream.Position = 0L;
        using var document =
            SpreadsheetDocument.Open(
                stream,
                false);
        var workbookPart = document.WorkbookPart
            ?? throw new AssertFailedException(
                "The preserved package is missing its workbook part.");
        var worksheetPart =
            workbookPart.WorksheetParts.Single();
        var workbookOpaque =
            workbookPart.GetPartById(
                WorkbookPartRelationshipId);
        var worksheetOpaque =
            worksheetPart.GetPartById(
                WorksheetPartRelationshipId);
        var external = worksheetPart
            .ExternalRelationships
            .Single(relationship =>
                relationship.Id ==
                ExternalRelationshipId);
        Assert.AreEqual(
            WorkbookContentType,
            workbookOpaque.ContentType);
        Assert.AreEqual(
            WorkbookRelationshipType,
            workbookOpaque.RelationshipType);
        Assert.AreEqual(
            WorksheetContentType,
            worksheetOpaque.ContentType);
        Assert.AreEqual(
            WorksheetRelationshipType,
            worksheetOpaque.RelationshipType);
        Assert.AreEqual(
            ExternalRelationshipType,
            external.RelationshipType);
        var workbookXml =
            LoadPartXml(workbookPart);
        var worksheetXml =
            LoadPartXml(worksheetPart);
        var stylesPart =
            workbookPart.WorkbookStylesPart
            ?? throw new AssertFailedException(
                "The preserved package is missing its style part.");
        var stylesXml =
            LoadPartXml(stylesPart);
        var worksheetName = (string?)workbookXml
            .Root?
            .Element(
                SpreadsheetNamespace + "sheets")?
            .Elements(
                SpreadsheetNamespace + "sheet")
            .Single()
            .Attribute("name")
            ?? throw new AssertFailedException(
                "The preserved package is missing its worksheet name.");

        return new OpaqueSnapshot(
            workbookOpaque.Uri.OriginalString,
            worksheetOpaque.Uri.OriginalString,
            ReadPartBytes(workbookOpaque),
            ReadPartBytes(worksheetOpaque),
            external.Uri,
            worksheetName,
            HasOpaqueMarker(
                workbookXml,
                "workbook"),
            HasOpaqueMarker(
                worksheetXml,
                "worksheet"),
            HasOpaqueMarker(
                stylesXml,
                "styles"));
    }

    private static void AssertOpaqueSnapshot(
        OpaqueSnapshot expected,
        OpaqueSnapshot actual)
    {
        Assert.AreEqual(
            expected.WorkbookPartUri,
            actual.WorkbookPartUri);
        Assert.AreEqual(
            expected.WorksheetPartUri,
            actual.WorksheetPartUri);
        CollectionAssert.AreEqual(
            expected.WorkbookBytes,
            actual.WorkbookBytes);
        CollectionAssert.AreEqual(
            expected.WorksheetBytes,
            actual.WorksheetBytes);
        Assert.AreEqual(
            expected.ExternalUri,
            actual.ExternalUri);
        Assert.IsTrue(
            actual.HasWorkbookMarker);
        Assert.IsTrue(
            actual.HasWorksheetMarker);
        Assert.IsTrue(
            actual.HasStylesMarker);
    }

    private static void AddOpaqueMarker(
        OpenXmlPart part,
        string marker)
    {
        var document = LoadPartXml(part);
        var root = document.Root
            ?? throw new AssertFailedException(
                "The OpenXml part is missing its root element.");
        var extensionList =
            root.Element(
                SpreadsheetNamespace + "extLst");
        if (extensionList is null)
        {
            extensionList = new XElement(
                SpreadsheetNamespace + "extLst");
            root.Add(extensionList);
        }

        extensionList.Add(
            new XElement(
                SpreadsheetNamespace + "ext",
                new XAttribute(
                    "uri",
                    $"urn:neraspreadsheet:test:{marker}"),
                new XElement(
                    OpaqueNamespace + "payload",
                    new XAttribute(
                        "marker",
                        marker))));
        SavePartXml(
            part,
            document);
    }

    private static bool HasOpaqueMarker(
        XDocument document,
        string marker) =>
        document
            .Descendants(
                OpaqueNamespace + "payload")
            .Any(element =>
                string.Equals(
                    (string?)element.Attribute("marker"),
                    marker,
                    StringComparison.Ordinal));

    private static XDocument LoadPartXml(
        OpenXmlPart part)
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
            });
        return XDocument.Load(
            reader,
            LoadOptions.PreserveWhitespace);
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
                Encoding =
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier:
                            false),
                Indent = false,
                CloseOutput = false,
            });
        document.Save(writer);
    }

    private static void WritePartBytes(
        OpenXmlPart part,
        byte[] bytes)
    {
        using var stream = part.GetStream(
            FileMode.Create,
            FileAccess.Write);
        stream.Write(
            bytes,
            0,
            bytes.Length);
    }

    private static byte[] ReadPartBytes(
        OpenXmlPart part)
    {
        using var stream = part.GetStream(
            FileMode.Open,
            FileAccess.Read);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed record OpaqueSnapshot(
        string WorkbookPartUri,
        string WorksheetPartUri,
        byte[] WorkbookBytes,
        byte[] WorksheetBytes,
        Uri ExternalUri,
        string WorksheetName,
        bool HasWorkbookMarker,
        bool HasWorksheetMarker,
        bool HasStylesMarker);
}
