using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class DirtyRegionBoundsTests
{
    [TestMethod]
    public void DirtyRangeExpandsToContainingMergedCell()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.MergeCells(new CellRange(new CellAddress(1, 1), new CellAddress(2, 2)));
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(workbook));

        Assert.IsTrue(engine.TryGetRangeBounds(
            new CellRange(new CellAddress(1, 1), new CellAddress(1, 1)),
            0d,
            0d,
            out var bounds));

        Assert.AreEqual(80d, bounds.X, 1e-9);
        Assert.AreEqual(20d, bounds.Y, 1e-9);
        Assert.AreEqual(160d, bounds.Width, 1e-9);
        Assert.AreEqual(40d, bounds.Height, 1e-9);
    }

    [TestMethod]
    public void DirtyRangeUsesFractionalScrollOffsets()
    {
        var engine = new SpreadsheetViewportEngine(new SpreadsheetSession(new Workbook()));
        var range = new CellRange(new CellAddress(2, 3), new CellAddress(4, 5));

        Assert.IsTrue(engine.TryGetRangeBounds(range, 13.5d, 7.25d, out var bounds));

        Assert.AreEqual((3d * 80d) - 13.5d, bounds.X, 1e-9);
        Assert.AreEqual((2d * 20d) - 7.25d, bounds.Y, 1e-9);
        Assert.AreEqual(3d * 80d, bounds.Width, 1e-9);
        Assert.AreEqual(3d * 20d, bounds.Height, 1e-9);
    }

    [TestMethod]
    public void DirtyRangeCrossingFrozenBoundaryRequestsFullInvalidation()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.View.SetFrozenPanes(1, 1);
        var engine = new SpreadsheetViewportEngine(session);
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(1, 1));

        Assert.IsFalse(engine.TryGetRangeBounds(range, 13.5d, 7.25d, out var bounds));
        Assert.AreEqual(NeraSpreadSheet.Foundation.RectD.Empty, bounds);
    }
}
