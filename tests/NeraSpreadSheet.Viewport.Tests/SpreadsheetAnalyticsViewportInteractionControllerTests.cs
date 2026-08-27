using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsViewportInteractionControllerTests
{
    [TestMethod]
    public void PointerDragCommitsExactlyOneUndoablePlacementChange()
    {
        var session = CreateSessionWithChart(out var item);
        var viewport = new SpreadsheetViewportEngine(session);
        var frame = viewport.Compose(0d, 0d, 640d, 420d, 0d);
        var input = new SpreadsheetAnalyticsViewportInteractionController(viewport);
        var target = viewport.GetAnalyticsInteractionTargets(frame.Layout).Single();
        var before = session.AnalyticsPlacements.GetPlacement(item);
        var historyBefore = session.History.UndoCount;
        var start = new PointD(
            target.ViewportBounds.Left + 40d,
            target.ViewportBounds.Top + 40d);

        Assert.IsTrue(input.PointerPressed(start, frame.Layout));
        Assert.IsTrue(input.PointerMoved(
            new PointD(start.X + 9.5d, start.Y + 4.25d)));
        Assert.IsTrue(input.PointerMoved(
            new PointD(start.X + 21.75d, start.Y + 12.5d)));
        Assert.AreEqual(historyBefore, session.History.UndoCount);

        Assert.IsTrue(input.PointerReleased(
            new PointD(start.X + 21.75d, start.Y + 12.5d)));
        Assert.AreEqual(historyBefore + 1, session.History.UndoCount);
        Assert.AreEqual(
            before.DocumentBounds.Translate(21.75d, 12.5d),
            session.AnalyticsPlacements.GetPlacement(item).DocumentBounds);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(
            before.DocumentBounds,
            session.AnalyticsPlacements.GetPlacement(item).DocumentBounds);
    }

    [TestMethod]
    public void BlankPointerPressClearsAnalyticsSelectionAndFallsThrough()
    {
        var session = CreateSessionWithChart(out var item);
        var viewport = new SpreadsheetViewportEngine(session);
        var frame = viewport.Compose(0d, 0d, 640d, 420d, 0d);
        var input = new SpreadsheetAnalyticsViewportInteractionController(viewport);
        Assert.IsTrue(session.AnalyticsInteraction.Select(item));

        Assert.IsFalse(input.PointerPressed(
            new PointD(620d, 400d),
            frame.Layout));

        Assert.IsNull(session.AnalyticsInteraction.SelectedItem);
        Assert.IsFalse(input.IsTransforming);
    }

    [TestMethod]
    public void KeyboardNudgeAndDeleteUseUndoableEditingPath()
    {
        var session = CreateSessionWithChart(out var item);
        var viewport = new SpreadsheetViewportEngine(session);
        var input = new SpreadsheetAnalyticsViewportInteractionController(viewport);
        Assert.IsTrue(session.AnalyticsInteraction.Select(item));
        var before = session.AnalyticsPlacements.GetPlacement(item);

        Assert.IsTrue(input.NudgeSelected(
            SpreadsheetAnalyticsViewportInteractionController.LargeKeyboardNudge,
            -SpreadsheetAnalyticsViewportInteractionController.DefaultKeyboardNudge));
        var moved = session.AnalyticsPlacements.GetPlacement(item);
        Assert.AreEqual(before.DocumentBounds.X + 10d, moved.DocumentBounds.X);
        Assert.AreEqual(before.DocumentBounds.Y - 1d, moved.DocumentBounds.Y);

        Assert.IsTrue(input.DeleteSelected());
        Assert.IsNull(session.AnalyticsInteraction.SelectedItem);
        Assert.AreEqual(0, session.Analytics.Charts.Count);
        Assert.AreEqual(0, session.AnalyticsPlacements.Placements.Count);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(1, session.Analytics.Charts.Count);
        Assert.AreEqual(1, session.AnalyticsPlacements.Placements.Count);
    }

    [TestMethod]
    public void CancelLeavesPersistedPlacementUntouched()
    {
        var session = CreateSessionWithChart(out var item);
        var viewport = new SpreadsheetViewportEngine(session);
        var frame = viewport.Compose(0d, 0d, 640d, 420d, 0d);
        var input = new SpreadsheetAnalyticsViewportInteractionController(viewport);
        var target = viewport.GetAnalyticsInteractionTargets(frame.Layout).Single();
        var before = session.AnalyticsPlacements.GetPlacement(item);
        var start = new PointD(
            target.ViewportBounds.Left + 50d,
            target.ViewportBounds.Top + 50d);

        Assert.IsTrue(input.PointerPressed(start, frame.Layout));
        Assert.IsTrue(input.PointerMoved(
            new PointD(start.X + 30d, start.Y + 20d)));
        Assert.IsTrue(input.Cancel());

        Assert.AreEqual(before, session.AnalyticsPlacements.GetPlacement(item));
        Assert.IsFalse(input.IsTransforming);
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
            "Analytics");
        item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        return session;
    }
}
