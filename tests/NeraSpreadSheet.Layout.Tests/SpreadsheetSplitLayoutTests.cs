using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Layout.Tests;

[TestClass]
public sealed class SpreadsheetSplitLayoutTests
{
    [TestMethod]
    public void NoSplitProducesOneFullViewportPane()
    {
        var layout = SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(
            new SizeD(800d, 600d)));

        Assert.IsFalse(layout.HasSplitPanes);
        Assert.AreEqual(1, layout.Panes.Count);
        Assert.AreEqual(SpreadsheetPaneId.TopLeft, layout.Panes[0].PaneId);
        Assert.AreEqual(new RectD(0d, 0d, 800d, 600d), layout.Panes[0].Bounds);
    }

    [TestMethod]
    public void DualSplitProducesFourNonOverlappingPanesAndSeparators()
    {
        var layout = SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(
            new SizeD(800d, 600d),
            SplitX: 300d,
            SplitY: 200d,
            SeparatorThickness: 6d,
            MinimumPaneExtent: 50d));

        Assert.IsTrue(layout.HasVerticalSplit);
        Assert.IsTrue(layout.HasHorizontalSplit);
        Assert.AreEqual(new RectD(300d, 0d, 6d, 600d), layout.VerticalSeparator);
        Assert.AreEqual(new RectD(0d, 200d, 800d, 6d), layout.HorizontalSeparator);
        Assert.AreEqual(4, layout.Panes.Count);
        AssertPane(layout, SpreadsheetPaneId.TopLeft, new RectD(0d, 0d, 300d, 200d));
        AssertPane(layout, SpreadsheetPaneId.TopRight, new RectD(306d, 0d, 494d, 200d));
        AssertPane(layout, SpreadsheetPaneId.BottomLeft, new RectD(0d, 206d, 300d, 394d));
        AssertPane(layout, SpreadsheetPaneId.BottomRight, new RectD(306d, 206d, 494d, 394d));
    }

    [TestMethod]
    public void SplitPositionsClampToMinimumPaneExtent()
    {
        var layout = SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(
            new SizeD(500d, 400d),
            SplitX: 5d,
            SplitY: 399d,
            SeparatorThickness: 4d,
            MinimumPaneExtent: 80d));

        Assert.AreEqual(80d, layout.SplitX);
        Assert.AreEqual(316d, layout.SplitY);
        AssertPane(layout, SpreadsheetPaneId.TopLeft, new RectD(0d, 0d, 80d, 316d));
        AssertPane(layout, SpreadsheetPaneId.BottomRight, new RectD(84d, 320d, 416d, 80d));
    }

    [TestMethod]
    public void SplitAxisDisablesWhenViewportCannotFitTwoMinimumPanes()
    {
        var layout = SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(
            new SizeD(100d, 300d),
            SplitX: 50d,
            SplitY: 120d,
            SeparatorThickness: 4d,
            MinimumPaneExtent: 50d));

        Assert.IsFalse(layout.HasVerticalSplit);
        Assert.IsTrue(layout.HasHorizontalSplit);
        Assert.AreEqual(2, layout.Panes.Count);
        AssertPane(layout, SpreadsheetPaneId.TopLeft, new RectD(0d, 0d, 100d, 120d));
        AssertPane(layout, SpreadsheetPaneId.BottomLeft, new RectD(0d, 124d, 100d, 176d));
    }

    [TestMethod]
    public void HitTestDistinguishesPanesSeparatorsAndLocalCoordinates()
    {
        var layout = SpreadsheetSplitLayoutEngine.Compute(new SpreadsheetSplitRequest(
            new SizeD(800d, 600d),
            SplitX: 300d,
            SplitY: 200d,
            SeparatorThickness: 6d,
            MinimumPaneExtent: 50d));

        var paneHit = layout.HitTest(new PointD(400d, 100d));
        Assert.AreEqual(SpreadsheetSplitHitRegionKind.Pane, paneHit.RegionKind);
        Assert.AreEqual(SpreadsheetPaneId.TopRight, paneHit.PaneId);
        Assert.AreEqual(new PointD(94d, 100d), paneHit.LocalPoint);

        var vertical = layout.HitTest(new PointD(302d, 100d));
        Assert.AreEqual(SpreadsheetSplitHitRegionKind.VerticalSeparator, vertical.RegionKind);

        var horizontal = layout.HitTest(new PointD(100d, 202d));
        Assert.AreEqual(SpreadsheetSplitHitRegionKind.HorizontalSeparator, horizontal.RegionKind);

        var intersection = layout.HitTest(new PointD(302d, 202d));
        Assert.AreEqual(SpreadsheetSplitHitRegionKind.SeparatorIntersection, intersection.RegionKind);

        Assert.AreEqual(
            SpreadsheetSplitHitTest.None,
            layout.HitTest(new PointD(800d, 600d)));
    }

    private static void AssertPane(
        SpreadsheetSplitLayout layout,
        SpreadsheetPaneId paneId,
        RectD expectedBounds)
    {
        Assert.IsTrue(layout.TryGetPane(paneId, out var pane));
        Assert.AreEqual(expectedBounds, pane.Bounds);
    }
}
