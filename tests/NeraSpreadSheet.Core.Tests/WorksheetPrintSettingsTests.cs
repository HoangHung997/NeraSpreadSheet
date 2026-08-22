using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetPrintSettingsTests
{
    [TestMethod]
    public void BuiltInPaperSizesUsePositivePhysicalDimensions()
    {
        var sizes = new[]
        {
            SpreadsheetPaperSize.A4,
            SpreadsheetPaperSize.A3,
            SpreadsheetPaperSize.Letter,
            SpreadsheetPaperSize.Legal,
        };

        Assert.IsTrue(sizes.All(static size =>
            size.WidthInches > 0d &&
            size.HeightInches > 0d &&
            !string.IsNullOrWhiteSpace(size.Name)));
    }

    [TestMethod]
    public void CopyDetachesMutablePageBreakCollections()
    {
        var rowBreaks = new List<int> { 10 };
        var columnBreaks = new List<int> { 4 };
        var settings = new WorksheetPrintSettings
        {
            PrintArea = new CellRange(
                new CellAddress(0, 0),
                new CellAddress(100, 10)),
            PageSetup = new SpreadsheetPageSetup
            {
                ManualRowBreaks = rowBreaks,
                ManualColumnBreaks = columnBreaks,
                OddHeader = "Page &P",
            },
        };

        var copy = settings.Copy();
        rowBreaks[0] = 20;
        columnBreaks[0] = 8;

        Assert.AreEqual(10, copy.PageSetup.ManualRowBreaks.Single());
        Assert.AreEqual(4, copy.PageSetup.ManualColumnBreaks.Single());
        Assert.AreEqual("Page &P", copy.PageSetup.OddHeader);
        Assert.AreEqual(settings.PrintArea, copy.PrintArea);
    }

    [TestMethod]
    public void InvalidPaperAndMarginValuesAreRejected()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SpreadsheetPaperSize(0d, 10d));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SpreadsheetPageMargins(-1d, 0d, 0d, 0d));
    }
}
