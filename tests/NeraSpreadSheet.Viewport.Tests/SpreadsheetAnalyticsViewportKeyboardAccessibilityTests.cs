using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Viewport.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsViewportKeyboardAccessibilityTests
{
    [TestMethod]
    public void NormalizedKeyboardPathMovesResizesDeletesAndUsesSharedHistory()
    {
        var session = CreateSessionWithChart(out var item);
        var viewport = new SpreadsheetViewportEngine(session);
        var input = new SpreadsheetAnalyticsViewportInteractionController(viewport);
        session.AnalyticsInteraction.Select(item);
        var before = session.AnalyticsPlacements.GetPlacement(item).DocumentBounds;

        Assert.IsTrue(input.Keyboard(SpreadsheetAnalyticsKeyboardKey.Right));
        Assert.IsTrue(input.Keyboard(
            SpreadsheetAnalyticsKeyboardKey.Down,
            SpreadsheetAnalyticsKeyboardModifiers.Control));
        Assert.IsTrue(input.Keyboard(
            SpreadsheetAnalyticsKeyboardKey.Right,
            SpreadsheetAnalyticsKeyboardModifiers.Shift));

        var transformed = session.AnalyticsPlacements.GetPlacement(item).DocumentBounds;
        Assert.AreEqual(before.X + 1d, transformed.X);
        Assert.AreEqual(before.Y + 10d, transformed.Y);
        Assert.AreEqual(before.Width + 1d, transformed.Width);

        Assert.IsTrue(input.Keyboard(SpreadsheetAnalyticsKeyboardKey.Delete));
        Assert.AreEqual(0, session.Analytics.Charts.Count);
        Assert.IsNull(input.Snapshot.SelectedItem);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(1, session.Analytics.Charts.Count);
    }

    [TestMethod]
    public void AccessibilityNodesUseViewportTargetsAndCallerResolvedNames()
    {
        var session = CreateSessionWithChart(out var item);
        var viewport = new SpreadsheetViewportEngine(session);
        var frame = viewport.Compose(0d, 0d, 640d, 420d, 0d);
        var input = new SpreadsheetAnalyticsViewportInteractionController(viewport);
        session.AnalyticsInteraction.Select(item);

        var nodes = input.GetAccessibilityNodes(
            frame.Layout,
            key => key == item ? "Quarterly revenue" : null);

        Assert.AreEqual(1, nodes.Count);
        Assert.AreEqual(item, nodes[0].Item);
        Assert.AreEqual("Quarterly revenue", nodes[0].Name);
        Assert.AreEqual(SpreadsheetAnalyticsAccessibleRole.Chart, nodes[0].Role);
        Assert.IsTrue(nodes[0].IsSelected);
        Assert.IsTrue(nodes[0].ViewportBounds.Width > 0d);
        Assert.IsTrue(nodes[0].ViewportBounds.Height > 0d);
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
