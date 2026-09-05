using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class UnknownPackageGraphPreservationTests
{
    private const string RootPartRelationshipId = "rOpaquePackageRoot";
    private const string RootNestedRelationshipId = "rOpaqueRootNested";
    private const string RootExternalRelationshipId = "rOpaqueRootExternal";
    private const string NestedExternalRelationshipId = "rOpaqueNestedExternal";
    private const string DrawingRelationshipId = "rDrawingOpaque";
    private const string ImageRelationshipId = "rImageOpaque";
    private const string DrawingNestedRelationshipId = "rDrawingNestedOpaque";
    private const string CustomXmlRelationshipId = "rCustomXmlOpaque";
    private const string CustomXmlPropertiesRelationshipId = "rCustomXmlPropertiesOpaque";
    private const string CustomXmlNestedRelationshipId = "rCustomXmlNestedOpaque";

    private const string RootPartRelationshipType =
        "urn:neraspreadsheet:test:package-root";
    private const string RootNestedRelationshipType =
        "urn:neraspreadsheet:test:package-root-nested";
    private const string RootExternalRelationshipType =
        "urn:neraspreadsheet:test:package-root-external";
    private const string NestedExternalRelationshipType =
        "urn:neraspreadsheet:test:nested-external";
    private const string DrawingNestedRelationshipType =
        "urn:neraspreadsheet:test:drawing-nested";
    private const string CustomXmlNestedRelationshipType =
        "urn:neraspreadsheet:test:custom-xml-nested";

    private const string RootPartContentType =
        "application/vnd.neraspreadsheet.test.package-root";
    private const string RootNestedContentType =
        "application/vnd.neraspreadsheet.test.package-root-nested";
    private const string DrawingNestedContentType =
        "application/vnd.neraspreadsheet.test.drawing-nested";
    private const string CustomXmlNestedContentType =
        "application/vnd.neraspreadsheet.test.custom-xml-nested";

    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace SpreadsheetDrawingNamespace =
        "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
    private static readonly XNamespace DrawingNamespace =
        "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace CustomXmlPropertiesNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/customXml";

    private static readonly Uri RootExternalTarget = new(
        "https://example.invalid/nera-package-root",
        UriKind.Absolute);
    private static readonly Uri NestedExternalTarget = new(
        "https://example.invalid/nera-nested-part",
        UriKind.Absolute);

    private static readonly byte[] RootOpaqueBytes =
    [
        0x4E,
        0x45,
        0x52,
        0x41,
        0x00,
        0xFF,
        0x17,
    ];

    private static readonly byte[] RootNestedBytes =
        Encoding.UTF8.GetBytes("nested package-root payload");
    private static readonly byte[] DrawingNestedBytes =
        Encoding.UTF8.GetBytes("nested drawing payload");
    private static readonly byte[] CustomXmlNestedBytes =
        Encoding.UTF8.GetBytes("nested custom-xml payload");
    private static readonly byte[] CustomXmlBytes =
        Encoding.UTF8.GetBytes(
            "<nera:opaque xmlns:nera=\"urn:neraspreadsheet:test:custom-xml\"><nera:value>preserve me</nera:value></nera:opaque>");
    private static readonly byte[] ImageBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9ZlS8AAAAASUVORK5CYII=");

    [TestMethod]
    public async Task NestedDrawingCustomXmlAndPackageRootGraphSurviveRepeatedSaves()
    {
        var serializer = new NeraOpenXmlWorkbookSerializer();
        await using var source = await CreateGraphPackageAsync(serializer);
        var sourceSnapshot = InspectGraphPackage(source);

        source.Position = 0L;
        var workbook = await serializer.LoadAsync(
            source,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        workbook.RenameWorksheet(workbook.Worksheets[0], "GraphPreserved");
        workbook.Worksheets[0].SetValue(
            new CellAddress(4, 3),
            "first graph edit");

        await using var firstOutput = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            firstOutput,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        var firstSnapshot = InspectGraphPackage(firstOutput);
        AssertGraphSnapshot(sourceSnapshot, firstSnapshot);
        Assert.AreEqual("GraphPreserved", firstSnapshot.WorksheetName);

        firstOutput.Position = 0L;
        var firstReload = await serializer.LoadAsync(
            firstOutput,
            new OpenXmlImportOptions());
        Assert.AreEqual(
            "first graph edit",
            firstReload.Worksheets[0]
                .GetCell(new CellAddress(4, 3))
                .Value.RawValue);

        workbook.Worksheets[0].SetValue(
            new CellAddress(8, 6),
            "second graph edit");
        await using var secondOutput = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            secondOutput,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        var secondSnapshot = InspectGraphPackage(secondOutput);
        AssertGraphSnapshot(firstSnapshot, secondSnapshot);

        secondOutput.Position = 0L;
        var secondReload = await serializer.LoadAsync(
            secondOutput,
            new OpenXmlImportOptions());
        Assert.AreEqual(
            "second graph edit",
            secondReload.Worksheets[0]
                .GetCell(new CellAddress(8, 6))
                .Value.RawValue);
    }

    private static async Task<MemoryStream> CreateGraphPackageAsync(
        NeraOpenXmlWorkbookSerializer serializer)
    {
        var workbook = new NeraWorkbook();
        workbook.Worksheets[0].SetValue(default, "graph baseline");
        var stream = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new AssertFailedException(
                    "The graph fixture is missing its workbook part.");
            var worksheetPart = workbookPart.WorksheetParts.Single();

            var rootPart = document.AddExtendedPart(
                RootPartRelationshipType,
                RootPartContentType,
                ".bin",
                RootPartRelationshipId);
            WritePartBytes(rootPart, RootOpaqueBytes);
            var rootNested = rootPart.AddExtendedPart(
                RootNestedRelationshipType,
                RootNestedContentType,
                ".dat",
                RootNestedRelationshipId);
            WritePartBytes(rootNested, RootNestedBytes);
            document.AddExternalRelationship(
                RootExternalRelationshipType,
                RootExternalTarget,
                RootExternalRelationshipId);
            rootNested.AddExternalRelationship(
                NestedExternalRelationshipType,
                NestedExternalTarget,
                NestedExternalRelationshipId);

            var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>(
                DrawingRelationshipId);
            WriteDrawingMarkup(drawingsPart);
            var imagePart = drawingsPart.AddImagePart(
                "image/png",
                ImageRelationshipId);
            WritePartBytes(imagePart, ImageBytes);
            var drawingNested = drawingsPart.AddExtendedPart(
                DrawingNestedRelationshipType,
                DrawingNestedContentType,
                ".bin",
                DrawingNestedRelationshipId);
            WritePartBytes(drawingNested, DrawingNestedBytes);
            AppendWorksheetDrawingReference(
                worksheetPart,
                DrawingRelationshipId);

            var customXmlPart = workbookPart.AddCustomXmlPart(
                "application/xml",
                CustomXmlRelationshipId);
            WritePartBytes(customXmlPart, CustomXmlBytes);
            var propertiesPart =
                customXmlPart.AddNewPart<CustomXmlPropertiesPart>(
                    CustomXmlPropertiesRelationshipId);
            WriteCustomXmlProperties(propertiesPart);
            var customXmlNested = customXmlPart.AddExtendedPart(
                CustomXmlNestedRelationshipType,
                CustomXmlNestedContentType,
                ".bin",
                CustomXmlNestedRelationshipId);
            WritePartBytes(customXmlNested, CustomXmlNestedBytes);
        }

        stream.Position = 0L;
        return stream;
    }

    private static GraphSnapshot InspectGraphPackage(MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        AssertSchemaValid(document);

        var workbookPart = document.WorkbookPart
            ?? throw new AssertFailedException(
                "The preserved graph is missing its workbook part.");
        var worksheetPart = workbookPart.WorksheetParts.Single();

        var rootPart = document.GetPartById(RootPartRelationshipId);
        var rootNested = rootPart.GetPartById(RootNestedRelationshipId);
        var rootExternal = document.ExternalRelationships.Single(
            relationship => relationship.Id == RootExternalRelationshipId);
        var nestedExternal = rootNested.ExternalRelationships.Single(
            relationship => relationship.Id == NestedExternalRelationshipId);

        var drawingsPart = (DrawingsPart)worksheetPart.GetPartById(
            DrawingRelationshipId);
        var imagePart = (ImagePart)drawingsPart.GetPartById(
            ImageRelationshipId);
        var drawingNested = drawingsPart.GetPartById(
            DrawingNestedRelationshipId);

        var customXmlPart = (CustomXmlPart)workbookPart.GetPartById(
            CustomXmlRelationshipId);
        var customXmlProperties =
            (CustomXmlPropertiesPart)customXmlPart.GetPartById(
                CustomXmlPropertiesRelationshipId);
        var customXmlNested = customXmlPart.GetPartById(
            CustomXmlNestedRelationshipId);

        Assert.AreEqual(
            RootPartRelationshipType,
            rootPart.RelationshipType);
        Assert.AreEqual(RootPartContentType, rootPart.ContentType);
        Assert.AreEqual(
            RootNestedRelationshipType,
            rootNested.RelationshipType);
        Assert.AreEqual(RootNestedContentType, rootNested.ContentType);
        Assert.AreEqual(
            RootExternalRelationshipType,
            rootExternal.RelationshipType);
        Assert.AreEqual(
            NestedExternalRelationshipType,
            nestedExternal.RelationshipType);
        Assert.AreEqual(
            DrawingNestedRelationshipType,
            drawingNested.RelationshipType);
        Assert.AreEqual(
            DrawingNestedContentType,
            drawingNested.ContentType);
        Assert.AreEqual(
            CustomXmlNestedRelationshipType,
            customXmlNested.RelationshipType);
        Assert.AreEqual(
            CustomXmlNestedContentType,
            customXmlNested.ContentType);
        Assert.AreEqual(
            DrawingRelationshipId,
            worksheetPart.GetIdOfPart(drawingsPart));
        Assert.AreEqual(
            ImageRelationshipId,
            drawingsPart.GetIdOfPart(imagePart));
        Assert.AreEqual(
            CustomXmlRelationshipId,
            workbookPart.GetIdOfPart(customXmlPart));
        Assert.AreEqual(
            CustomXmlPropertiesRelationshipId,
            customXmlPart.GetIdOfPart(customXmlProperties));
        Assert.IsTrue(HasWorksheetDrawingReference(worksheetPart));

        var workbookXml = LoadPartXml(workbookPart);
        var worksheetName = (string?)workbookXml
            .Root?
            .Element(SpreadsheetNamespace + "sheets")?
            .Elements(SpreadsheetNamespace + "sheet")
            .Single()
            .Attribute("name")
            ?? throw new AssertFailedException(
                "The preserved graph is missing its worksheet name.");

        return new GraphSnapshot(
            rootPart.Uri.OriginalString,
            rootNested.Uri.OriginalString,
            drawingsPart.Uri.OriginalString,
            imagePart.Uri.OriginalString,
            drawingNested.Uri.OriginalString,
            customXmlPart.Uri.OriginalString,
            customXmlProperties.Uri.OriginalString,
            customXmlNested.Uri.OriginalString,
            ReadPartBytes(rootPart),
            ReadPartBytes(rootNested),
            ReadPartBytes(imagePart),
            ReadPartBytes(drawingNested),
            ReadPartBytes(customXmlPart),
            ReadPartBytes(customXmlProperties),
            ReadPartBytes(customXmlNested),
            rootExternal.Uri,
            nestedExternal.Uri,
            worksheetName);
    }

    private static void AssertGraphSnapshot(
        GraphSnapshot expected,
        GraphSnapshot actual)
    {
        Assert.AreEqual(expected.RootPartUri, actual.RootPartUri);
        Assert.AreEqual(expected.RootNestedUri, actual.RootNestedUri);
        Assert.AreEqual(expected.DrawingsPartUri, actual.DrawingsPartUri);
        Assert.AreEqual(expected.ImagePartUri, actual.ImagePartUri);
        Assert.AreEqual(expected.DrawingNestedUri, actual.DrawingNestedUri);
        Assert.AreEqual(expected.CustomXmlPartUri, actual.CustomXmlPartUri);
        Assert.AreEqual(
            expected.CustomXmlPropertiesUri,
            actual.CustomXmlPropertiesUri);
        Assert.AreEqual(
            expected.CustomXmlNestedUri,
            actual.CustomXmlNestedUri);
        CollectionAssert.AreEqual(expected.RootBytes, actual.RootBytes);
        CollectionAssert.AreEqual(
            expected.RootNestedBytes,
            actual.RootNestedBytes);
        CollectionAssert.AreEqual(expected.ImageBytes, actual.ImageBytes);
        CollectionAssert.AreEqual(
            expected.DrawingNestedBytes,
            actual.DrawingNestedBytes);
        CollectionAssert.AreEqual(
            expected.CustomXmlBytes,
            actual.CustomXmlBytes);
        CollectionAssert.AreEqual(
            expected.CustomXmlPropertiesBytes,
            actual.CustomXmlPropertiesBytes);
        CollectionAssert.AreEqual(
            expected.CustomXmlNestedBytes,
            actual.CustomXmlNestedBytes);
        Assert.AreEqual(expected.RootExternalUri, actual.RootExternalUri);
        Assert.AreEqual(
            expected.NestedExternalUri,
            actual.NestedExternalUri);
    }

    private static void AppendWorksheetDrawingReference(
        WorksheetPart worksheetPart,
        string relationshipId)
    {
        var document = LoadPartXml(worksheetPart);
        var root = document.Root
            ?? throw new AssertFailedException(
                "The graph fixture worksheet is missing its root element.");
        root.Add(
            new XElement(
                SpreadsheetNamespace + "drawing",
                new XAttribute(
                    OfficeRelationshipNamespace + "id",
                    relationshipId)));
        SavePartXml(worksheetPart, document);
    }

    private static bool HasWorksheetDrawingReference(
        WorksheetPart worksheetPart)
    {
        var document = LoadPartXml(worksheetPart);
        return document
            .Root?
            .Elements(SpreadsheetNamespace + "drawing")
            .Any(element => string.Equals(
                (string?)element.Attribute(
                    OfficeRelationshipNamespace + "id"),
                DrawingRelationshipId,
                StringComparison.Ordinal)) == true;
    }

    private static void WriteDrawingMarkup(DrawingsPart drawingsPart)
    {
        var document = new XDocument(
            new XElement(
                SpreadsheetDrawingNamespace + "wsDr",
                new XAttribute(
                    XNamespace.Xmlns + "xdr",
                    SpreadsheetDrawingNamespace),
                new XAttribute(
                    XNamespace.Xmlns + "a",
                    DrawingNamespace),
                new XAttribute(
                    XNamespace.Xmlns + "r",
                    OfficeRelationshipNamespace),
                new XElement(
                    SpreadsheetDrawingNamespace + "twoCellAnchor",
                    CreateDrawingMarker("from", 0, 0),
                    CreateDrawingMarker("to", 1, 1),
                    new XElement(
                        SpreadsheetDrawingNamespace + "pic",
                        new XElement(
                            SpreadsheetDrawingNamespace + "nvPicPr",
                            new XElement(
                                SpreadsheetDrawingNamespace + "cNvPr",
                                new XAttribute("id", 1U),
                                new XAttribute("name", "Nera opaque image")),
                            new XElement(
                                SpreadsheetDrawingNamespace + "cNvPicPr")),
                        new XElement(
                            SpreadsheetDrawingNamespace + "blipFill",
                            new XElement(
                                DrawingNamespace + "blip",
                                new XAttribute(
                                    OfficeRelationshipNamespace + "embed",
                                    ImageRelationshipId)),
                            new XElement(
                                DrawingNamespace + "stretch",
                                new XElement(
                                    DrawingNamespace + "fillRect"))),
                        new XElement(
                            SpreadsheetDrawingNamespace + "spPr",
                            new XElement(
                                DrawingNamespace + "xfrm",
                                new XElement(
                                    DrawingNamespace + "off",
                                    new XAttribute("x", 0L),
                                    new XAttribute("y", 0L)),
                                new XElement(
                                    DrawingNamespace + "ext",
                                    new XAttribute("cx", 9525L),
                                    new XAttribute("cy", 9525L))),
                            new XElement(
                                DrawingNamespace + "prstGeom",
                                new XAttribute("prst", "rect"),
                                new XElement(
                                    DrawingNamespace + "avLst")))),
                    new XElement(
                        SpreadsheetDrawingNamespace + "clientData"))));
        SavePartXml(drawingsPart, document);
    }

    private static XElement CreateDrawingMarker(
        string localName,
        int column,
        int row) =>
        new(
            SpreadsheetDrawingNamespace + localName,
            new XElement(
                SpreadsheetDrawingNamespace + "col",
                column),
            new XElement(
                SpreadsheetDrawingNamespace + "colOff",
                0),
            new XElement(
                SpreadsheetDrawingNamespace + "row",
                row),
            new XElement(
                SpreadsheetDrawingNamespace + "rowOff",
                0));

    private static void WriteCustomXmlProperties(
        CustomXmlPropertiesPart propertiesPart)
    {
        var document = new XDocument(
            new XElement(
                CustomXmlPropertiesNamespace + "datastoreItem",
                new XAttribute(
                    XNamespace.Xmlns + "ds",
                    CustomXmlPropertiesNamespace),
                new XAttribute(
                    CustomXmlPropertiesNamespace + "itemID",
                    "{E7B9FE09-BE6A-4F68-A88B-8A5819FA4D9C}"),
                new XElement(
                    CustomXmlPropertiesNamespace + "schemaRefs",
                    new XElement(
                        CustomXmlPropertiesNamespace + "schemaRef",
                        new XAttribute(
                            CustomXmlPropertiesNamespace + "uri",
                            "urn:neraspreadsheet:test:custom-xml")))));
        SavePartXml(propertiesPart, document);
    }

    private static void AssertSchemaValid(SpreadsheetDocument document)
    {
        var errors = new OpenXmlValidator(FileFormatVersions.Office2013)
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
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        using var reader = XmlReader.Create(
            stream,
            new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                CloseInput = false,
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
                Encoding = new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                Indent = false,
                CloseOutput = false,
            });
        document.Save(writer);
    }

    private static void WritePartBytes(OpenXmlPart part, byte[] bytes)
    {
        using var stream = part.GetStream(FileMode.Create, FileAccess.Write);
        stream.Write(bytes, 0, bytes.Length);
    }

    private static byte[] ReadPartBytes(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private sealed record GraphSnapshot(
        string RootPartUri,
        string RootNestedUri,
        string DrawingsPartUri,
        string ImagePartUri,
        string DrawingNestedUri,
        string CustomXmlPartUri,
        string CustomXmlPropertiesUri,
        string CustomXmlNestedUri,
        byte[] RootBytes,
        byte[] RootNestedBytes,
        byte[] ImageBytes,
        byte[] DrawingNestedBytes,
        byte[] CustomXmlBytes,
        byte[] CustomXmlPropertiesBytes,
        byte[] CustomXmlNestedBytes,
        Uri RootExternalUri,
        Uri NestedExternalUri,
        string WorksheetName);
}
