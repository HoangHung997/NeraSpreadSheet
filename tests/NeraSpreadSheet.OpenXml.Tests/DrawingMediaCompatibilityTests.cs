using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using A = DocumentFormat.OpenXml.Drawing;
using S = DocumentFormat.OpenXml.Spreadsheet;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class DrawingMediaCompatibilityTests
{
    private const string DrawingRelationshipId = "rDrawingMediaCompat";
    private const string OneCellImageRelationshipId = "rOneCellImageCompat";
    private const string BackgroundImageRelationshipId = "rSheetBackgroundCompat";
    private const string LegacyDrawingRelationshipId = "rLegacyDrawingCompat";

    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipNamespace =
        "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace SpreadsheetDrawingNamespace =
        "http://schemas.openxmlformats.org/drawingml/2006/spreadsheetDrawing";
    private static readonly XNamespace DrawingNamespace =
        "http://schemas.openxmlformats.org/drawingml/2006/main";

    private static readonly byte[] OneCellImageBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAIAAAD0In3KAAAADUlEQVR42mNk+M8AAwADpwGaqnWBGwAAAABJRU5ErkJggg==");
    private static readonly byte[] BackgroundImageBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAACCAIAAAD91JpzAAAADUlEQVR42mP8z8BQDwAEhQH+gGLJWQAAAABJRU5ErkJggg==");
    private static readonly byte[] LegacyDrawingBytes = Encoding.UTF8.GetBytes(
        "<xml xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"><v:shape id=\"NeraLegacyNote\" type=\"#_x0000_t202\" style=\"position:absolute;margin-left:0;margin-top:0;width:72pt;height:24pt;z-index:1\" fillcolor=\"#ffffe1\" o:insetmode=\"auto\"><v:textbox><div style=\"text-align:left\">legacy drawing</div></v:textbox><x:ClientData ObjectType=\"Note\" /></v:shape></xml>");

    [TestMethod]
    public async Task WorksheetImageBackgroundAndLegacyDrawingSurviveRepeatedPreservedSessionSaves()
    {
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var source = await CreateDrawingMediaPackageAsync(serializer);
        var sourceSnapshot = InspectDrawingMediaPackage(source);

        source.Position = 0L;
        var session = await serializer.LoadSessionAsync(
            source,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        session.Workbook.Worksheets[0].SetValue(
            new CellAddress(3, 1),
            "first edit");

        await using var firstOutput = new MemoryStream();
        await serializer.SaveSessionAsync(
            session,
            firstOutput,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        var firstSnapshot = InspectDrawingMediaPackage(firstOutput);
        AssertDrawingMediaSnapshot(sourceSnapshot, firstSnapshot);

        firstOutput.Position = 0L;
        var reloaded = await serializer.LoadSessionAsync(
            firstOutput,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        reloaded.Workbook.Worksheets[0].SetValue(
            new CellAddress(4, 1),
            "second edit");

        await using var secondOutput = new MemoryStream();
        await serializer.SaveSessionAsync(
            reloaded,
            secondOutput,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        var secondSnapshot = InspectDrawingMediaPackage(secondOutput);
        AssertDrawingMediaSnapshot(firstSnapshot, secondSnapshot);
    }

    private static async Task<MemoryStream> CreateDrawingMediaPackageAsync(
        NeraOpenXmlSpreadsheetSessionSerializer serializer)
    {
        var workbook = new Workbook();
        workbook.RenameWorksheet(workbook.Worksheets[0], "Media");
        workbook.Worksheets[0].SetValue(default, "media baseline");
        var session = new SpreadsheetSession(workbook);
        var stream = new MemoryStream();
        await serializer.SaveSessionAsync(
            session,
            stream,
            new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var worksheetPart = document.WorkbookPart?.WorksheetParts.Single()
                ?? throw new AssertFailedException(
                    "The drawing media fixture is missing its worksheet part.");

            var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>(
                DrawingRelationshipId);
            WriteDrawingMarkup(drawingsPart);
            WritePartBytes(
                drawingsPart.AddImagePart(
                    "image/png",
                    OneCellImageRelationshipId),
                OneCellImageBytes);
            WritePartBytes(
                worksheetPart.AddImagePart(
                    "image/png",
                    BackgroundImageRelationshipId),
                BackgroundImageBytes);
            WritePartBytes(
                worksheetPart.AddNewPart<VmlDrawingPart>(
                    LegacyDrawingRelationshipId),
                LegacyDrawingBytes);

            var worksheet = worksheetPart.Worksheet
                ?? throw new AssertFailedException(
                    "The drawing media fixture worksheet has no markup.");
            worksheet.Append(
                new S.Drawing { Id = DrawingRelationshipId },
                new S.LegacyDrawing { Id = LegacyDrawingRelationshipId },
                new S.Picture { Id = BackgroundImageRelationshipId });
            worksheet.Save();
            AssertSchemaValid(document);
        }

        stream.Position = 0L;
        return stream;
    }

    private static DrawingMediaSnapshot InspectDrawingMediaPackage(
        MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        AssertSchemaValid(document);
        var worksheetPart = document.WorkbookPart?.WorksheetParts.Single()
            ?? throw new AssertFailedException(
                "The drawing media package is missing its worksheet part.");

        var drawingsPart = (DrawingsPart)worksheetPart.GetPartById(
            DrawingRelationshipId);
        var oneCellImage = (ImagePart)drawingsPart.GetPartById(
            OneCellImageRelationshipId);
        var backgroundImage = (ImagePart)worksheetPart.GetPartById(
            BackgroundImageRelationshipId);
        var legacyDrawing = (VmlDrawingPart)worksheetPart.GetPartById(
            LegacyDrawingRelationshipId);

        var worksheetXml = LoadPartXml(worksheetPart);
        var drawingReferenceId = FindSingleRelationshipReference(
            worksheetXml,
            "drawing");
        var legacyDrawingReferenceId = FindSingleRelationshipReference(
            worksheetXml,
            "legacyDrawing");
        var pictureReferenceId = FindSingleRelationshipReference(
            worksheetXml,
            "picture");

        var drawingXml = LoadPartXml(drawingsPart);
        var oneCellAnchors = drawingXml
            .Root?
            .Elements(SpreadsheetDrawingNamespace + "oneCellAnchor")
            .ToArray()
            ?? [];
        Assert.AreEqual(
            1,
            oneCellAnchors.Length,
            "Expected one preserved oneCellAnchor image.");
        var embeddedImageId = oneCellAnchors[0]
            .Descendants(DrawingNamespace + "blip")
            .Single()
            .Attribute(OfficeRelationshipNamespace + "embed")?
            .Value;

        return new DrawingMediaSnapshot(
            drawingsPart.Uri.OriginalString,
            oneCellImage.Uri.OriginalString,
            backgroundImage.Uri.OriginalString,
            legacyDrawing.Uri.OriginalString,
            drawingReferenceId,
            legacyDrawingReferenceId,
            pictureReferenceId,
            embeddedImageId,
            ReadPartBytes(oneCellImage),
            ReadPartBytes(backgroundImage),
            ReadPartBytes(legacyDrawing));
    }

    private static void AssertDrawingMediaSnapshot(
        DrawingMediaSnapshot expected,
        DrawingMediaSnapshot actual)
    {
        Assert.AreEqual(expected.DrawingsPartUri, actual.DrawingsPartUri);
        Assert.AreEqual(expected.OneCellImagePartUri, actual.OneCellImagePartUri);
        Assert.AreEqual(
            expected.BackgroundImagePartUri,
            actual.BackgroundImagePartUri);
        Assert.AreEqual(expected.LegacyDrawingPartUri, actual.LegacyDrawingPartUri);
        Assert.AreEqual(expected.DrawingReferenceId, actual.DrawingReferenceId);
        Assert.AreEqual(
            expected.LegacyDrawingReferenceId,
            actual.LegacyDrawingReferenceId);
        Assert.AreEqual(expected.PictureReferenceId, actual.PictureReferenceId);
        Assert.AreEqual(expected.EmbeddedImageId, actual.EmbeddedImageId);
        CollectionAssert.AreEqual(expected.OneCellImageBytes, actual.OneCellImageBytes);
        CollectionAssert.AreEqual(
            expected.BackgroundImageBytes,
            actual.BackgroundImageBytes);
        CollectionAssert.AreEqual(
            expected.LegacyDrawingBytes,
            actual.LegacyDrawingBytes);
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
                    SpreadsheetDrawingNamespace + "oneCellAnchor",
                    CreateDrawingMarker("from", 1, 1),
                    new XElement(
                        SpreadsheetDrawingNamespace + "ext",
                        new XAttribute("cx", 19050L),
                        new XAttribute("cy", 9525L)),
                    new XElement(
                        SpreadsheetDrawingNamespace + "pic",
                        new XElement(
                            SpreadsheetDrawingNamespace + "nvPicPr",
                            new XElement(
                                SpreadsheetDrawingNamespace + "cNvPr",
                                new XAttribute("id", 42U),
                                new XAttribute("name", "Preserved media image")),
                            new XElement(
                                SpreadsheetDrawingNamespace + "cNvPicPr")),
                        new XElement(
                            SpreadsheetDrawingNamespace + "blipFill",
                            new XElement(
                                DrawingNamespace + "blip",
                                new XAttribute(
                                    OfficeRelationshipNamespace + "embed",
                                    OneCellImageRelationshipId)),
                            new XElement(
                                DrawingNamespace + "stretch",
                                new XElement(
                                    DrawingNamespace + "fillRect"))),
                        new XElement(
                            SpreadsheetDrawingNamespace + "spPr",
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

    private static string? FindSingleRelationshipReference(
        XDocument document,
        string localName)
    {
        return document
            .Root?
            .Elements(SpreadsheetNamespace + localName)
            .Single()
            .Attribute(OfficeRelationshipNamespace + "id")?
            .Value;
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

    private sealed record DrawingMediaSnapshot(
        string DrawingsPartUri,
        string OneCellImagePartUri,
        string BackgroundImagePartUri,
        string LegacyDrawingPartUri,
        string? DrawingReferenceId,
        string? LegacyDrawingReferenceId,
        string? PictureReferenceId,
        string? EmbeddedImageId,
        byte[] OneCellImageBytes,
        byte[] BackgroundImageBytes,
        byte[] LegacyDrawingBytes);
}
