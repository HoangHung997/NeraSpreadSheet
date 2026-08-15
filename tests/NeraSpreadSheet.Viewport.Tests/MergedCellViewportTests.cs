using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class MergedCellViewportTests
{
    [TestMethod]
    public void HitTestInsideMergedRangeReturnsTopLeftCell()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.MergeCells(new CellRange(default, new CellAddress(1, 1)));
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));

        Assert.IsTrue(engine.TryHitTest(100d, 30d, 0d, 0d, out var address));
        Assert.AreEqual(default(CellAddress), address);
    }

    [TestMethod]
    public void CellBoundsCoverEntireMergedRange()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.MergeCells(new CellRange(default, new CellAddress(1, 1)));
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));

        Assert.IsTrue(engine.TryGetCellBounds(new CellAddress(1, 1), 0d, 0d, out var bounds));
        Assert.AreEqual(sheet.Dimensions.DefaultColumnWidth * 2d, bounds.Width, 1e-9);
        Assert.AreEqual(sheet.Dimensions.DefaultRowHeight * 2d, bounds.Height, 1e-9);
    }
}
