using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class SpreadsheetSplitViewStateAdapterTests
{
    [TestMethod]
    public void CapturePreservesAllPaneOffsetsAndActivePane()
    {
        var engine = CreateEngine();
        engine.ScrollPaneTo(SpreadsheetPaneId.TopLeft, 10.5d, 20.25d);
        engine.ScrollPaneTo(SpreadsheetPaneId.TopRight, 110.75d, 30.5d);
        engine.ScrollPaneTo(SpreadsheetPaneId.BottomLeft, 40d, 220.25d);
        engine.ScrollPaneTo(SpreadsheetPaneId.BottomRight, 140.5d, 260.75d);
        engine.SetActivePane(SpreadsheetPaneId.BottomRight);

        var state = SpreadsheetSplitViewStateAdapter.Capture(
            engine,
            320.5d,
            180.25d);

        Assert.AreEqual(SpreadsheetSplitViewMode.Both, state.Mode);
        Assert.AreEqual(320.5d, state.SplitX);
        Assert.AreEqual(180.25d, state.SplitY);
        Assert.AreEqual(
            SpreadsheetSplitViewPane.BottomRight,
            state.ActivePane);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(10.5d, 20.25d),
            state.TopLeftScroll);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(110.75d, 30.5d),
            state.TopRightScroll);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(40d, 220.25d),
            state.BottomLeftScroll);
        Assert.AreEqual(
            new SpreadsheetPaneScrollOffset(140.5d, 260.75d),
            state.BottomRightScroll);
    }

    [TestMethod]
    public void ApplyRestoresHiddenPaneOffsetsAndActivePane()
    {
        var engine = CreateEngine();
        var state = new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Both,
            300d,
            200d,
            SpreadsheetSplitViewPane.BottomRight,
            topLeftScroll: new SpreadsheetPaneScrollOffset(11d, 21d),
            topRightScroll: new SpreadsheetPaneScrollOffset(111d, 31d),
            bottomLeftScroll: new SpreadsheetPaneScrollOffset(41d, 221d),
            bottomRightScroll: new SpreadsheetPaneScrollOffset(141d, 261d));

        SpreadsheetSplitViewStateAdapter.Apply(engine, state);

        Assert.AreEqual(SpreadsheetPaneId.BottomRight, engine.ActivePane);
        Assert.AreEqual(
            new PointD(11d, 21d),
            engine.GetPaneScroll(SpreadsheetPaneId.TopLeft));
        Assert.AreEqual(
            new PointD(111d, 31d),
            engine.GetPaneScroll(SpreadsheetPaneId.TopRight));
        Assert.AreEqual(
            new PointD(41d, 221d),
            engine.GetPaneScroll(SpreadsheetPaneId.BottomLeft));
        Assert.AreEqual(
            new PointD(141d, 261d),
            engine.GetPaneScroll(SpreadsheetPaneId.BottomRight));
    }

    [TestMethod]
    public void ModeAndPaneMappingsAreStable()
    {
        Assert.AreEqual(
            SpreadsheetSplitViewMode.None,
            SpreadsheetSplitViewStateAdapter.ResolveMode(null, null));
        Assert.AreEqual(
            SpreadsheetSplitViewMode.Vertical,
            SpreadsheetSplitViewStateAdapter.ResolveMode(100d, null));
        Assert.AreEqual(
            SpreadsheetSplitViewMode.Horizontal,
            SpreadsheetSplitViewStateAdapter.ResolveMode(null, 100d));
        Assert.AreEqual(
            SpreadsheetSplitViewMode.Both,
            SpreadsheetSplitViewStateAdapter.ResolveMode(100d, 200d));

        foreach (var paneId in Enum.GetValues<SpreadsheetPaneId>())
        {
            Assert.AreEqual(
                paneId,
                SpreadsheetSplitViewStateAdapter.ToPaneId(
                    SpreadsheetSplitViewStateAdapter.ToViewPane(paneId)));
        }
    }

    [TestMethod]
    public void CaptureRejectsNonFiniteSplitCoordinates()
    {
        var engine = CreateEngine();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetSplitViewStateAdapter.Capture(engine, double.NaN, null));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            SpreadsheetSplitViewStateAdapter.ResolveMode(null, double.PositiveInfinity));
    }

    private static SpreadsheetSplitViewportEngine CreateEngine()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetValue(
            new CellAddress(100, 100),
            "extent");
        return new SpreadsheetSplitViewportEngine(
            new SpreadsheetSession(workbook));
    }
}
