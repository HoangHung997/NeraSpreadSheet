using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsInteractionEditingTests
{
    [TestMethod]
    public void TransformCommitCreatesExactlyOneUndoablePlacementEdit()
    {
        var session = CreateSessionWithSource();
        var chart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Column);
        var item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        var before = session.AnalyticsPlacements.GetPlacement(item);
        var undoBefore = session.History.UndoCount;
        var afterBounds = before.DocumentBounds.Translate(25d, 15d);
        var commit = new SpreadsheetAnalyticsTransformCommit(
            item,
            before.DocumentBounds,
            afterBounds);

        Assert.IsTrue(SpreadsheetAnalyticsInteractionEditing.ApplyTransformCommit(
            session.AnalyticsPlacements,
            commit));
        Assert.AreEqual(undoBefore + 1, session.History.UndoCount);
        Assert.AreEqual(
            afterBounds,
            session.AnalyticsPlacements.GetPlacement(item).DocumentBounds);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(
            before.DocumentBounds,
            session.AnalyticsPlacements.GetPlacement(item).DocumentBounds);
    }

    [TestMethod]
    public void StaleTransformCommitIsRejectedWithoutMutatingPlacement()
    {
        var session = CreateSessionWithSource();
        var chart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Line);
        var item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        var original = session.AnalyticsPlacements.GetPlacement(item);
        Assert.IsTrue(session.AnalyticsPlacements.MoveBy(item, 10d, 5d));
        var current = session.AnalyticsPlacements.GetPlacement(item);
        var stale = new SpreadsheetAnalyticsTransformCommit(
            item,
            original.DocumentBounds,
            original.DocumentBounds.Translate(30d, 20d));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            SpreadsheetAnalyticsInteractionEditing.ApplyTransformCommit(
                session.AnalyticsPlacements,
                stale));
        Assert.AreEqual(current, session.AnalyticsPlacements.GetPlacement(item));
    }

    [TestMethod]
    public void RemoveItemDispatchesByAnalyticsKindAndRemainsUndoable()
    {
        var session = CreateSessionWithSource();
        var pivot = session.Analytics.InsertPivotFromSelection();
        var item = SpreadsheetAnalyticsItemKey.ForPivot(pivot.Id);

        Assert.IsTrue(SpreadsheetAnalyticsInteractionEditing.RemoveItem(
            session.Analytics,
            item));
        Assert.AreEqual(
            0,
            session.Analytics.GetPivots(session.ActiveWorksheet).Count);
        Assert.AreEqual(0, session.AnalyticsPlacements.Placements.Count);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(
            1,
            session.Analytics.GetPivots(session.ActiveWorksheet).Count);
        Assert.AreEqual(1, session.AnalyticsPlacements.Placements.Count);
    }

    private static SpreadsheetSession CreateSessionWithSource()
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
        return session;
    }
}
