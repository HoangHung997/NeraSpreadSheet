using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class WorksheetPrintSettingsRoundTripTests
{
    private const string OpaqueRelationshipId = "rPrintOpaque";
    private const string OpaqueRelationshipType =
        "urn:neraspreadsheet:test:print-opaque";
    private const string OpaqueContentType =
        "application/vnd.neraspreadsheet.test.print-opaque";
    private static readonly XNamespace SpreadsheetNamespace =
        "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly byte[] OpaqueBytes =
        [0x4E, 0x45, 0x52, 0x41, 0x00, 0x50, 0x52, 0x49, 0x4E, 0x54];

    [TestMethod]
    public async Task PageSetupPrintAreaTitlesAndHeaderFooterRoundTrip()
    {
        var workbook = CreateWorkbook();
        var serializer = new NeraOpenXmlDocumentSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());
        AssertSchemaValid(stream);
        AssertMarkup(stream);

        stream.Position = 0L;
        var loaded = await serializer.LoadAsync(
            stream,
            new OpenXmlImportOptions());
        var settings = loaded.Worksheets[0].GetPrintSettings();

        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(99, 5)),
            settings.PrintArea);
        Assert.AreEqual(
            SpreadsheetPageOrientation.Landscape,
            settings.PageSetup.Orientation);
        Assert.AreEqual(
            SpreadsheetPaperSize.A4,
            settings.PageSetup.PaperSize);
        Assert.AreEqual(75d, settings.PageSetup.ScalePercent, 0.000001d);
        Assert.AreEqual(1, settings.PageSetup.FitToPagesWide);
        Assert.AreEqual(2, settings.PageSetup.FitToPagesTall);
        Assert.IsTrue(settings.PageSetup.CenterHorizontally);
        Assert.IsTrue(settings.PageSetup.CenterVertically);
        Assert.IsTrue(settings.PageSetup.PrintGridlines);
        Assert.IsTrue(settings.PageSetup.PrintHeadings);
        Assert.AreEqual(
            "&F — &A",
            settings.PageSetup.OddHeader);
        Assert.AreEqual(
            "Page &P of &N",
            settings.PageSetup.OddFooter);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 5)),
            settings.PageSetup.RepeatTitles.Rows);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(99, 0)),
            settings.PageSetup.RepeatTitles.Columns);
    }

    [TestMethod]
    public async Task RepeatedPreservationKeepsOpaquePartAndRefreshesPrintSettings()
    {
        var serializer = new NeraOpenXmlDocumentSerializer();
        await using var source = new MemoryStream();
        await serializer.SaveAsync(
            CreateWorkbook(),
            source,
            new OpenXmlExportOptions());
        AddOpaqueWorkbookPart(source);
        source.Position = 0L;
        var workbook = await serializer.LoadAsync(
            source,
            new OpenXmlImportOptions
            {
                PreserveUnknownParts = true,
            });
        var worksheet = workbook.Worksheets[0];
        var original = worksheet.GetPrintSettings();
        worksheet.SetPrintSettings(original with
        {
            PageSetup = original.PageSetup with
            {
                Orientation = SpreadsheetPageOrientation.Portrait,
                OddFooter = "Changed &P",
            },
        });

        await using var first = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            first,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        AssertOpaquePart(first);
        AssertSchemaValid(first);

        worksheet.SetPrintSettings(worksheet.GetPrintSettings() with
        {
            PageSetup = worksheet.GetPrintSettings().PageSetup with
            {
                FitToPagesTall = 3,
            },
        });
        await using var second = new MemoryStream();
        await serializer.SaveAsync(
            workbook,
            second,
            new OpenXmlExportOptions
            {
                PreserveUnknownParts = true,
            });
        AssertOpaquePart(second);
        AssertSchemaValid(second);
        second.Position = 0L;
        var reloaded = await serializer.LoadAsync(
            second,
            new OpenXmlImportOptions());
        var settings = reloaded.Worksheets[0].GetPrintSettings();
        Assert.AreEqual(
            SpreadsheetPageOrientation.Portrait,
            settings.PageSetup.Orientation);
        Assert.AreEqual("Changed &P", settings.PageSetup.OddFooter);
        Assert.AreEqual(3, settings.PageSetup.FitToPagesTall);
    }

    [TestMethod]
    public async Task CrossWorksheetPrintAreaIsRejected()
    {
        var serializer = new NeraOpenXmlDocumentSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(
            CreateWorkbook(),
            stream,
            new OpenXmlExportOptions());
        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new AssertFailedException("Workbook part is missing.");
            var xml = LoadPartXml(workbookPart);
            var printArea = xml.Root?
                .Element(SpreadsheetNamespace + "definedNames")?
                .Elements(SpreadsheetNamespace + "definedName")
                .Single(element =>
                    (string?)element.Attribute("name") ==
                    "_xlnm.Print_Area")
                ?? throw new AssertFailedException("Print area is missing.");
            printArea.Value = "'Other Sheet'!$A$1:$B$2";
            SavePartXml(workbookPart, xml);
        }
        stream.Position = 0L;

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () =>
            await serializer.LoadAsync(
                stream,
                new OpenXmlImportOptions()));
    }

    private static NeraWorkbook CreateWorkbook()
    {
        var workbook = new NeraWorkbook();
        var worksheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(worksheet, "Estimate 2026");
        worksheet.SetValue(new CellAddress(0, 0), "Header");
        worksheet.SetValue(new CellAddress(2, 1), 42d);
        worksheet.SetPrintSettings(new WorksheetPrintSettings
        {
            PrintArea = new CellRange(
                new CellAddress(0, 0),
                new CellAddress(99, 5)),
            PageSetup = new SpreadsheetPageSetup
            {
                PaperSize = SpreadsheetPaperSize.A4,
                Orientation = SpreadsheetPageOrientation.Landscape,
                Margins = new SpreadsheetPageMargins(
                    0.4d,
                    0.5d,
                    0.6d,
                    0.7d,
                    0.2d,
                    0.25d),
                ScalePercent = 75d,
                FitToPagesWide = 1,
                FitToPagesTall = 2,
                RepeatTitles = new SpreadsheetRepeatTitles(
                    rows: new CellRange(
                        new CellAddress(0, 0),
                        new CellAddress(1, 5)),
                    columns: new CellRange(
                        new CellAddress(0, 0),
                        new CellAddress(99, 0))),
                CenterHorizontally = true,
                CenterVertically = true,
                PrintGridlines = true,
                PrintHeadings = true,
                OddHeader = "&F — &A",
                OddFooter = "Page &P of &N",
            },
        });
        return workbook;
    }

    private static void AssertMarkup(MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart
            ?? throw new AssertFailedException("Workbook part is missing.");
        var workbookXml = LoadPartXml(workbookPart);
        var names = workbookXml.Root?
            .Element(SpreadsheetNamespace + "definedNames")?
            .Elements(SpreadsheetNamespace + "definedName")
            .ToArray() ?? [];
        Assert.AreEqual(2, names.Length);
        Assert.IsTrue(names.Any(element =>
            element.Value.Contains(
                "'Estimate 2026'!$A$1:$F$100",
                StringComparison.Ordinal)));
        Assert.IsTrue(names.Any(element =>
            element.Value.Contains(
                "'Estimate 2026'!$1:$2",
                StringComparison.Ordinal) &&
            element.Value.Contains(
                "'Estimate 2026'!$A:$A",
                StringComparison.Ordinal)));

        var worksheetPart = workbookPart.WorksheetParts.Single();
        var worksheetXml = LoadPartXml(worksheetPart);
        var root = worksheetXml.Root
            ?? throw new AssertFailedException("Worksheet root is missing.");
        var pageSetup = root.Element(SpreadsheetNamespace + "pageSetup")
            ?? throw new AssertFailedException("pageSetup is missing.");
        Assert.AreEqual(
            "landscape",
            (string?)pageSetup.Attribute("orientation"));
        Assert.AreEqual("9", (string?)pageSetup.Attribute("paperSize"));
        Assert.AreEqual("75", (string?)pageSetup.Attribute("scale"));
        Assert.AreEqual("1", (string?)pageSetup.Attribute("fitToWidth"));
        Assert.AreEqual("2", (string?)pageSetup.Attribute("fitToHeight"));
        stream.Position = 0L;
    }

    private static void AddOpaqueWorkbookPart(MemoryStream stream)
    {
        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            var workbookPart = document.WorkbookPart
                ?? throw new AssertFailedException("Workbook part is missing.");
            var opaque = workbookPart.AddExtendedPart(
                OpaqueRelationshipType,
                OpaqueContentType,
                ".bin",
                OpaqueRelationshipId);
            using var target = opaque.GetStream(
                FileMode.Create,
                FileAccess.Write);
            target.Write(OpaqueBytes);
        }
        stream.Position = 0L;
    }

    private static void AssertOpaquePart(MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart
            ?? throw new AssertFailedException("Workbook part is missing.");
        var part = workbookPart.GetPartById(OpaqueRelationshipId);
        using var source = part.GetStream(FileMode.Open, FileAccess.Read);
        using var buffer = new MemoryStream();
        source.CopyTo(buffer);
        CollectionAssert.AreEqual(OpaqueBytes, buffer.ToArray());
        stream.Position = 0L;
    }

    private static void AssertSchemaValid(MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var errors = new OpenXmlValidator(FileFormatVersions.Office2013)
            .Validate(document)
            .ToArray();
        Assert.AreEqual(
            0,
            errors.Length,
            string.Join(
                Environment.NewLine,
                errors.Select(static error => error.Description)));
        stream.Position = 0L;
    }

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
}
