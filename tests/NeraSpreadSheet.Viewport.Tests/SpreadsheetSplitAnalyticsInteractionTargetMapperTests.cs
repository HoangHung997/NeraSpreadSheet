using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class SpreadsheetSplitAnalyticsInteractionTargetMapperTests
{
    [TestMethod]
    public void SplitFrameProjectsSameDocumentPlacementIntoEveryVisiblePane()
    {
        var session = CreateSessionWithChart(out var item);
        var engine = new SpreadsheetSplitViewportEngine(session);
        var frame = engine.Compose(new SpreadsheetSplitRequest(
            new SizeD(640d, 420d),
            SplitX: 320d,
            SplitY: 210d));

        var targets = SpreadsheetSplitAnalyticsInteractionTargetMapper.Map(
            session,
            frame);

        Assert.AreEqual(4, targets.Count);
        Assert.IsTrue(targets.All(target => target.Target.Item == item));
        Assert.AreEqual(
            4,
            targets.Select(static target => target.PaneId).Distinct().Count());
        Assert.IsTrue(targets.All(target =>
            target.Target.DocumentBounds ==
            session.AnalyticsPlacements.GetPlacement(item).DocumentBounds));
    }

    [TestMethod]
    public void HitTestResolvesCorrectPaneAndRejectsSeparators()
    {
        var session = CreateSessionWithChart(out var item);
        var engine = new SpreadsheetSplitViewportEngine(session);
        var frame = engine.Compose(new SpreadsheetSplitRequest(
            new SizeD(640d, 420d),
            SplitX: 320d,
            SplitY: 210d));
        Assert.IsTrue(frame.Layout.TryGetPane(
            SpreadsheetPaneId.TopRight,
            out var topRight));

        var hit = SpreadsheetSplitAnalyticsInteractionTargetMapper.HitTest(
            session,
            frame,
            new PointD(
                topRight.Bounds.Left + 40d,
                topRight.Bounds.Top + 40d));

        Assert.IsTrue(hit.HasValue);
        Assert.AreEqual(SpreadsheetPaneId.TopRight, hit.Value.PaneId);
        Assert.AreEqual(item, hit.Value.Hit.Item);

        var separatorHit = SpreadsheetSplitAnalyticsInteractionTargetMapper.HitTest(
            session,
            frame,
            new PointD(
                frame.Layout.VerticalSeparator.Left + 1d,
                40d));
        Assert.IsNull(separatorHit);
    }

    [TestMethod]
    public void PaneScrollMovesOnlyThatPaneTargetProjection()
    {
        var session = CreateSessionWithChart(out _);
        var engine = new SpreadsheetSplitViewportEngine(session);
        var request = new SpreadsheetSplitRequest(
            new SizeD(640d, 420d),
            SplitX: 320d,
            SplitY: 210d);
        var first = engine.Compose(request);
        var before = SpreadsheetSplitAnalyticsInteractionTargetMapper.Map(
            session,
            first);
        var topRightBefore = before.Single(target =>
            target.PaneId == SpreadsheetPaneId.TopRight);
        var topLeftBefore = before.Single(target =>
            target.PaneId == SpreadsheetPaneId.TopLeft);

        engine.SetPaneScroll(SpreadsheetPaneId.TopRight, 25.5d, 0d);
        var second = engine.Compose(request);
        var after = SpreadsheetSplitAnalyticsInteractionTargetMapper.Map(
            session,
            second);
        var topRightAfter = after.Single(target =>
            target.PaneId == SpreadsheetPaneId.TopRight);
        var topLeftAfter = after.Single(target =>
            target.PaneId == SpreadsheetPaneId.TopLeft);

        Assert.AreEqual(
            topRightBefore.Target.ViewportBounds.X - 25.5d,
            topRightAfter.Target.ViewportBounds.X,
            1e-9);
        Assert.AreEqual(
            topLeftBefore.Target.ViewportBounds.X,
            topLeftAfter.Target.ViewportBounds.X,
            1e-9);
    }

    private static SpreadsheetSession CreateSessionWithChart(
        out SpreadsheetAnalyticsItemKey item)
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Category");
        sheet.SetValue(new CellAddress(0, 1), "Value");
        sheet.SetValue(new CellAddress(1, 0), "A");
        sheet.SetValue(new CellAddress(1, 1), 10d);
        sheet.SetValue(new CellAddress(2, 0), "B");
        sheet.SetValue(new CellAddress(2, 1), 20d);
        session.Selection.Select(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(2, 1)));
        var chart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Column,
            "Split Analytics");
        item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        return session;
    }
}
