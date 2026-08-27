using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Viewport;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class NeraSpreadsheetAnalyticsTouchRouterTests
{
    [TestMethod]
    public void OwnedTouchMovesAnalyticsOnceAndBlocksSecondaryPointer()
    {
        var harness = new AnalyticsTouchHarness();
        var before = harness.Session.AnalyticsPlacements.GetPlacement(harness.Item);
        var historyBefore = harness.Session.History.UndoCount;
        var start = harness.GetTargetCenter();

        Assert.IsTrue(harness.Process(
            7L,
            SKTouchAction.Pressed,
            start,
            isBodyRegion: true));
        Assert.AreEqual(1, harness.SpreadsheetGestureCancelCount);
        Assert.AreEqual(7L, harness.Router.ActiveTouchId);
        Assert.AreEqual(harness.Item, harness.Session.AnalyticsInteraction.SelectedItem);
        Assert.IsTrue(harness.Session.AnalyticsInteraction.IsTransforming);

        Assert.IsTrue(harness.Process(
            8L,
            SKTouchAction.Pressed,
            new PointD(start.X + 80d, start.Y + 60d),
            isBodyRegion: true));
        Assert.AreEqual(7L, harness.Router.ActiveTouchId);

        var end = new PointD(start.X + 18.5d, start.Y + 9.25d);
        Assert.IsTrue(harness.Process(
            7L,
            SKTouchAction.Moved,
            end,
            isBodyRegion: true));
        Assert.IsTrue(harness.Process(
            7L,
            SKTouchAction.Released,
            end,
            isBodyRegion: true));

        var after = harness.Session.AnalyticsPlacements.GetPlacement(harness.Item);
        Assert.AreEqual(
            before.DocumentBounds.Translate(18.5d, 9.25d),
            after.DocumentBounds);
        Assert.AreEqual(historyBefore + 1, harness.Session.History.UndoCount);
        Assert.IsFalse(harness.Router.HasActiveTouch);
        Assert.IsFalse(harness.Session.AnalyticsInteraction.IsTransforming);
        Assert.IsTrue(harness.InvalidateCount >= 3);
    }

    [TestMethod]
    public void BlankBodyPressFallsThroughAfterClearingAnalyticsSelection()
    {
        var harness = new AnalyticsTouchHarness();
        Assert.IsTrue(harness.Session.AnalyticsInteraction.Select(harness.Item));
        var blank = new PointD(
            harness.Frame.Layout.ViewportSize.Width - 2d,
            harness.Frame.Layout.ViewportSize.Height - 2d);

        Assert.IsFalse(harness.Process(
            11L,
            SKTouchAction.Pressed,
            blank,
            isBodyRegion: true));

        Assert.IsNull(harness.Session.AnalyticsInteraction.SelectedItem);
        Assert.IsFalse(harness.Router.HasActiveTouch);
        Assert.AreEqual(0, harness.SpreadsheetGestureCancelCount);
    }

    [TestMethod]
    public void CancelAllAbortsPreviewWithoutCreatingHistoryEntry()
    {
        var harness = new AnalyticsTouchHarness();
        var before = harness.Session.AnalyticsPlacements.GetPlacement(harness.Item);
        var historyBefore = harness.Session.History.UndoCount;
        var start = harness.GetTargetCenter();

        Assert.IsTrue(harness.Process(
            15L,
            SKTouchAction.Pressed,
            start,
            isBodyRegion: true));
        Assert.IsTrue(harness.Process(
            15L,
            SKTouchAction.Moved,
            new PointD(start.X + 50d, start.Y + 35d),
            isBodyRegion: true));
        Assert.IsTrue(harness.Router.CancelAll());

        Assert.AreEqual(
            before,
            harness.Session.AnalyticsPlacements.GetPlacement(harness.Item));
        Assert.AreEqual(historyBefore, harness.Session.History.UndoCount);
        Assert.IsFalse(harness.Router.HasActiveTouch);
        Assert.IsFalse(harness.Session.AnalyticsInteraction.IsTransforming);
    }

    [TestMethod]
    public void NonOwnedWheelFallsThroughToSpreadsheetInput()
    {
        var harness = new AnalyticsTouchHarness();

        Assert.IsFalse(harness.Process(
            0L,
            SKTouchAction.WheelChanged,
            PointD.Zero,
            isBodyRegion: false,
            wheelDelta: -120));
        Assert.IsFalse(harness.Router.HasActiveTouch);
    }

    private sealed class AnalyticsTouchHarness
    {
        private readonly SpreadsheetAnalyticsViewportInteractionController _controller;

        public AnalyticsTouchHarness()
        {
            var workbook = new Workbook();
            Session = new SpreadsheetSession(workbook);
            var worksheet = Session.ActiveWorksheet;
            worksheet.SetValue(new CellAddress(0, 0), "Category");
            worksheet.SetValue(new CellAddress(0, 1), "Value");
            worksheet.SetValue(new CellAddress(1, 0), "A");
            worksheet.SetValue(new CellAddress(1, 1), 10d);
            worksheet.SetValue(new CellAddress(2, 0), "B");
            worksheet.SetValue(new CellAddress(2, 1), 20d);
            Session.Selection.Select(new CellRange(
                new CellAddress(0, 0),
                new CellAddress(2, 1)));
            var chart = Session.Analytics.InsertChartFromSelection(
                SpreadsheetChartType.Column,
                "Touch chart");
            Item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
            Viewport = new SpreadsheetViewportEngine(Session);
            Frame = Viewport.Compose(0d, 0d, 640d, 420d, 0d);
            _controller = new SpreadsheetAnalyticsViewportInteractionController(Viewport);
            Router = new NeraSpreadsheetAnalyticsTouchRouter(
                () => _controller,
                () => Frame.Layout,
                () => SpreadsheetGestureCancelCount++,
                () => InvalidateCount++);
        }

        public SpreadsheetSession Session { get; }

        public SpreadsheetViewportEngine Viewport { get; }

        public SpreadsheetViewportFrame Frame { get; }

        public SpreadsheetAnalyticsItemKey Item { get; }

        public NeraSpreadsheetAnalyticsTouchRouter Router { get; }

        public int SpreadsheetGestureCancelCount { get; private set; }

        public int InvalidateCount { get; private set; }

        public PointD GetTargetCenter()
        {
            var target = Viewport
                .GetAnalyticsInteractionTargets(Frame.Layout)
                .Single(value => value.Item == Item);
            var visible = target.ViewportBounds.Intersect(target.ClipBounds);
            Assert.IsFalse(visible.IsEmpty);
            return new PointD(
                visible.Left + (visible.Width / 2d),
                visible.Top + (visible.Height / 2d));
        }

        public bool Process(
            long id,
            SKTouchAction action,
            PointD bodyPoint,
            bool isBodyRegion,
            int wheelDelta = 0) =>
            Router.Process(
                new SKTouchEventArgs(
                    id,
                    action,
                    SKMouseButton.Left,
                    SKTouchDeviceType.Touch,
                    new SKPoint((float)bodyPoint.X, (float)bodyPoint.Y),
                    action is SKTouchAction.Pressed or SKTouchAction.Moved,
                    wheelDelta),
                bodyPoint,
                isBodyRegion);
    }
}
