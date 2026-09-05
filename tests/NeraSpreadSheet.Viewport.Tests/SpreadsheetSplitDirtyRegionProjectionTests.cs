using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class SpreadsheetSplitDirtyRegionProjectionTests
{
    [TestMethod]
    public void CellRangeProjectsIntoEveryVisiblePaneUsingIndependentScroll()
    {
        var engine = CreateEngine();
        engine.SetPaneScroll(SpreadsheetPaneId.TopRight, 160d, 0d);
        engine.SetPaneScroll(SpreadsheetPaneId.BottomLeft, 0d, 100d);
        engine.SetPaneScroll(SpreadsheetPaneId.BottomRight, 160d, 100d);
        engine.Compose(new SpreadsheetSplitRequest(
            new SizeD(800d, 600d),
            SplitX: 300d,
            SplitY: 200d,
            SeparatorThickness: 6d,
            MinimumPaneExtent: 50d));

        var projection = engine.ProjectDirtyRange(new CellRange(
            new CellAddress(6, 3),
            new CellAddress(6, 3)));

        Assert.IsFalse(projection.RequiresFullInvalidation);
        Assert.AreEqual(4, projection.Regions.Length);
        AssertRegion(
            projection,
            SpreadsheetPaneId.TopLeft,
            new RectD(240d, 120d, 60d, 20d));
        AssertRegion(
            projection,
            SpreadsheetPaneId.TopRight,
            new RectD(386d, 120d, 80d, 20d));
        AssertRegion(
            projection,
            SpreadsheetPaneId.BottomLeft,
            new RectD(240d, 226d, 60d, 20d));
        AssertRegion(
            projection,
            SpreadsheetPaneId.BottomRight,
            new RectD(386d, 226d, 80d, 20d));
    }

    [TestMethod]
    public void FreezeCrossingRangeSplitsIntoVisibleFreezeSubregions()
    {
        var engine = CreateEngine();
        engine.Session.View.SetFrozenPanes(1, 1);
        engine.SetPaneScroll(SpreadsheetPaneId.TopLeft, 80d, 20d);
        engine.Compose(new SpreadsheetSplitRequest(new SizeD(400d, 200d)));

        var projection = engine.ProjectDirtyRange(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(2, 2)));

        Assert.IsFalse(projection.RequiresFullInvalidation);
        Assert.AreEqual(4, projection.Regions.Length);
        CollectionAssert.AreEquivalent(
            new[]
            {
                new RectD(0d, 0d, 80d, 20d),
                new RectD(80d, 0d, 80d, 20d),
                new RectD(0d, 20d, 80d, 20d),
                new RectD(80d, 20d, 80d, 20d),
            },
            projection.Regions.Select(region => region.Bounds).ToArray());
    }

    [TestMethod]
    public void DirtyCellExpandsToItsCompleteMergedRange()
    {
        var engine = CreateEngine();
        engine.Session.ActiveWorksheet.MergeCells(new CellRange(
            new CellAddress(1, 1),
            new CellAddress(2, 2)));
        engine.Compose(new SpreadsheetSplitRequest(new SizeD(400d, 200d)));

        var projection = engine.ProjectDirtyRange(new CellRange(
            new CellAddress(1, 1),
            new CellAddress(1, 1)));

        Assert.IsFalse(projection.RequiresFullInvalidation);
        Assert.AreEqual(1, projection.Regions.Length);
        Assert.AreEqual(
            new RectD(80d, 20d, 160d, 40d),
            projection.Regions[0].Bounds);
    }

    [TestMethod]
    public void CompletelyOffscreenRangeProducesNoInvalidationRectangles()
    {
        var engine = CreateEngine();
        engine.Compose(new SpreadsheetSplitRequest(new SizeD(200d, 100d)));

        var projection = engine.ProjectDirtyRange(new CellRange(
            new CellAddress(100, 100),
            new CellAddress(100, 100)));

        Assert.IsFalse(projection.RequiresFullInvalidation);
        Assert.AreEqual(0, projection.Regions.Length);
    }

    [TestMethod]
    public void ProjectionWithoutComposedFrameRequiresFullInvalidation()
    {
        var engine = CreateEngine();

        var projection = engine.ProjectDirtyRange(new CellRange(
            default,
            default));

        Assert.IsTrue(projection.RequiresFullInvalidation);
        Assert.AreEqual(0, projection.Regions.Length);
    }

    private static SpreadsheetSplitViewportEngine CreateEngine()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(
            new CellAddress(200, 120),
            "extent");
        return new SpreadsheetSplitViewportEngine(
            new SpreadsheetSession(workbook));
    }

    private static void AssertRegion(
        SpreadsheetSplitDirtyRegionProjection projection,
        SpreadsheetPaneId paneId,
        RectD expected)
    {
        var actual = projection.Regions.Single(region => region.PaneId == paneId);
        Assert.AreEqual(expected, actual.Bounds);
    }
}
