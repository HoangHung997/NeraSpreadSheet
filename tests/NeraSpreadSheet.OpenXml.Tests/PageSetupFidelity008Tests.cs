using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class PageSetupFidelity008Tests
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    [TestMethod]
    public async Task ExtendedExcelPageSetupFieldsRoundTripWithoutLosingPrintAreaOrTitles()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Header");
        sheet.SetPrintSettings(new WorksheetPrintSettings
        {
            PrintArea = new CellRange(new CellAddress(0, 0), new CellAddress(49, 7)),
            PageSetup = new SpreadsheetPageSetup
            {
                Orientation = SpreadsheetPageOrientation.Landscape,
                PaperSize = SpreadsheetPaperSize.A4,
                ScalePercent = 80,
                RepeatTitles = new SpreadsheetRepeatTitles(
                    rows: new CellRange(new CellAddress(0, 0), new CellAddress(1, SpreadsheetLimits.MaxColumns - 1)),
                    columns: new CellRange(new CellAddress(0, 0), new CellAddress(SpreadsheetLimits.MaxRows - 1, 1))),
                PageOrder = SpreadsheetPageOrder.OverThenDown,
                PrintQualityDpi = 600,
                FirstPageNumber = 3,
                BlackAndWhite = true,
                DraftQuality = true,
                PrintComments = SpreadsheetPrintComments.AtEnd,
                PrintErrors = SpreadsheetPrintErrors.Dash,
                OddHeader = "&C&A",
                OddFooter = "&R&P/&N",
                EvenHeader = "&LEven",
                EvenFooter = "&REven",
                FirstHeader = "&CFirst",
                FirstFooter = "&CFirst footer",
                DifferentOddEvenPages = true,
                DifferentFirstPage = true,
                ScaleHeaderFooterWithDocument = false,
                AlignHeaderFooterWithMargins = false,
            },
        });

        var serializer = new NeraOpenXmlDocumentSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveAsync(workbook, stream, new OpenXmlExportOptions());

        stream.Position = 0;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var worksheetPart = document.WorkbookPart!.WorksheetParts.Single();
            var xml = Load(worksheetPart);
            var root = xml.Root!;
            var page = root.Element(Ns + "pageSetup")!;
            Assert.AreEqual("overThenDown", (string?)page.Attribute("pageOrder"));
            Assert.AreEqual("600", (string?)page.Attribute("horizontalDpi"));
            Assert.AreEqual("600", (string?)page.Attribute("verticalDpi"));
            Assert.AreEqual("1", (string?)page.Attribute("useFirstPageNumber"));
            Assert.AreEqual("3", (string?)page.Attribute("firstPageNumber"));
            Assert.AreEqual("1", (string?)page.Attribute("blackAndWhite"));
            Assert.AreEqual("1", (string?)page.Attribute("draft"));
            Assert.AreEqual("atEnd", (string?)page.Attribute("cellComments"));
            Assert.AreEqual("dash", (string?)page.Attribute("errors"));
            var hf = root.Element(Ns + "headerFooter")!;
            Assert.AreEqual("1", (string?)hf.Attribute("differentOddEven"));
            Assert.AreEqual("1", (string?)hf.Attribute("differentFirst"));
            Assert.AreEqual("0", (string?)hf.Attribute("scaleWithDoc"));
            Assert.AreEqual("0", (string?)hf.Attribute("alignWithMargins"));
            Assert.AreEqual("&LEven", hf.Element(Ns + "evenHeader")?.Value);
            Assert.AreEqual("&CFirst footer", hf.Element(Ns + "firstFooter")?.Value);
        }

        stream.Position = 0;
        var loaded = await serializer.LoadAsync(stream, new OpenXmlImportOptions());
        var settings = loaded.Worksheets[0].GetPrintSettings();
        Assert.AreEqual(new CellRange(new CellAddress(0, 0), new CellAddress(49, 7)), settings.PrintArea);
        Assert.AreEqual(SpreadsheetPageOrder.OverThenDown, settings.PageSetup.PageOrder);
        Assert.AreEqual(600, settings.PageSetup.PrintQualityDpi);
        Assert.AreEqual(3, settings.PageSetup.FirstPageNumber);
        Assert.IsTrue(settings.PageSetup.BlackAndWhite);
        Assert.IsTrue(settings.PageSetup.DraftQuality);
        Assert.AreEqual(SpreadsheetPrintComments.AtEnd, settings.PageSetup.PrintComments);
        Assert.AreEqual(SpreadsheetPrintErrors.Dash, settings.PageSetup.PrintErrors);
        Assert.AreEqual("&LEven", settings.PageSetup.EvenHeader);
        Assert.AreEqual("&REven", settings.PageSetup.EvenFooter);
        Assert.AreEqual("&CFirst", settings.PageSetup.FirstHeader);
        Assert.AreEqual("&CFirst footer", settings.PageSetup.FirstFooter);
        Assert.IsTrue(settings.PageSetup.DifferentOddEvenPages);
        Assert.IsTrue(settings.PageSetup.DifferentFirstPage);
        Assert.IsFalse(settings.PageSetup.ScaleHeaderFooterWithDocument);
        Assert.IsFalse(settings.PageSetup.AlignHeaderFooterWithMargins);
        Assert.AreEqual(0, settings.PageSetup.RepeatTitles.Rows?.Top);
        Assert.AreEqual(1, settings.PageSetup.RepeatTitles.Rows?.Bottom);
        Assert.AreEqual(0, settings.PageSetup.RepeatTitles.Columns?.Left);
        Assert.AreEqual(1, settings.PageSetup.RepeatTitles.Columns?.Right);
    }

    private static XDocument Load(OpenXmlPart part)
    {
        using var stream = part.GetStream(FileMode.Open, FileAccess.Read);
        return XDocument.Load(stream);
    }
}
