using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class SpreadsheetSplitViewportEngineTests
{
    [TestMethod]
    public void ComposeKeepsFractionalScrollIndependentPerPane()
    {
        var engine = CreateEngine();
        engine.SetPaneScroll(SpreadsheetPaneId.TopLeft, 12.5d, 35.25d);
        engine.SetPaneScroll(SpreadsheetPaneId.TopRight, 173.75d, 91.5d);

        var frame = engine.Compose(CreateVerticalSplitRequest(), overscan: 0d);

        var left = GetPane(frame, SpreadsheetPaneId.TopLeft);
        var right = GetPane(frame, SpreadsheetPaneId.TopRight);
        Assert.AreEqual(12.5d, left.ScrollX, 0.0001d);
        Assert.AreEqual(35.25d, left.ScrollY, 0.0001d);
        Assert.AreEqual(173.75d, right.ScrollX, 0.0001d);
        Assert.AreEqual(91.5d, right.ScrollY, 0.0001d);
    }

    [TestMethod]
    public void PrecisionDeltaAdvancesOnlyTheTargetPane()
    {
        var engine = CreateEngine();
        engine.Compose(CreateVerticalSplitRequest(), overscan: 0d);
        engine.QueuePaneScroll(
            SpreadsheetPaneId.TopRight,
            new ScrollDelta(82.5d, 41.25d, ScrollInputKind.Precision));

        Assert.IsTrue(engine.AdvanceScrollFrame(TimeSpan.Zero));
        var frame = engine.Compose(CreateVerticalSplitRequest(), overscan: 0d);

        var left = GetPane(frame, SpreadsheetPaneId.TopLeft);
        var right = GetPane(frame, SpreadsheetPaneId.TopRight);
        Assert.AreEqual(0d, left.ScrollX, 0.0001d);
        Assert.AreEqual(0d, left.ScrollY, 0.0001d);
        Assert.AreEqual(82.5d, right.ScrollX, 0.0001d);
        Assert.AreEqual(41.25d, right.ScrollY, 0.0001d);
    }

    [TestMethod]
    public void HitTestUsesTheScrollStateOfTheHitPane()
    {
        var engine = CreateEngine();
        engine.SetPaneScroll(SpreadsheetPaneId.TopRight, 160d, 0d);
        engine.Compose(CreateVerticalSplitRequest(), overscan: 0d);

        Assert.IsTrue(engine.TryHitTest(
            10d,
            10d,
            out var leftPane,
            out var leftAddress));
        Assert.IsTrue(engine.TryHitTest(
            214d,
            10d,
            out var rightPane,
            out var rightAddress));

        Assert.AreEqual(SpreadsheetPaneId.TopLeft, leftPane);
        Assert.AreEqual(SpreadsheetPaneId.TopRight, rightPane);
        Assert.AreEqual(0, leftAddress.ColumnIndex);
        Assert.AreEqual(2, rightAddress.ColumnIndex);
        Assert.AreEqual(leftAddress.RowIndex, rightAddress.RowIndex);
    }

    [TestMethod]
    public void CellBoundsAreTranslatedIntoTheRequestedPane()
    {
        var engine = CreateEngine();
        engine.Compose(CreateVerticalSplitRequest(), overscan: 0d);

        Assert.IsTrue(engine.TryGetCellBounds(
            SpreadsheetPaneId.TopRight,
            default,
            out var bounds));

        Assert.AreEqual(new RectD(204d, 0d, 80d, 20d), bounds);
    }

    [TestMethod]
    public void ActivePaneFallsBackButHiddenPaneScrollIsPreserved()
    {
        var engine = CreateEngine();
        var dualSplit = new SpreadsheetSplitRequest(
            new SizeD(500d, 320d),
            SplitX: 200d,
            SplitY: 140d,
            SeparatorThickness: 4d,
            MinimumPaneExtent: 60d);
        engine.SetPaneScroll(SpreadsheetPaneId.BottomRight, 310.5d, 220.25d);
        engine.SetActivePane(SpreadsheetPaneId.BottomRight);

        var splitFrame = engine.Compose(dualSplit, overscan: 0d);
        Assert.AreEqual(SpreadsheetPaneId.BottomRight, splitFrame.ActivePane);

        var singleFrame = engine.Compose(
            new SpreadsheetSplitRequest(new SizeD(500d, 320d)),
            overscan: 0d);
        Assert.AreEqual(SpreadsheetPaneId.TopLeft, singleFrame.ActivePane);

        var restoredFrame = engine.Compose(dualSplit, overscan: 0d);
        var restored = GetPane(restoredFrame, SpreadsheetPaneId.BottomRight);
        Assert.AreEqual(310.5d, restored.ScrollX, 0.0001d);
        Assert.AreEqual(220.25d, restored.ScrollY, 0.0001d);
    }

    private static SpreadsheetSplitViewportEngine CreateEngine() =>
        new(new SpreadsheetSession(new Workbook()));

    private static SpreadsheetSplitRequest CreateVerticalSplitRequest() => new(
        new SizeD(500d, 300d),
        SplitX: 200d,
        SeparatorThickness: 4d,
        MinimumPaneExtent: 60d);

    private static SpreadsheetSplitPaneFrame GetPane(
        SpreadsheetSplitViewportFrame frame,
        SpreadsheetPaneId paneId)
    {
        Assert.IsTrue(frame.TryGetPane(paneId, out var pane));
        return pane;
    }
}
