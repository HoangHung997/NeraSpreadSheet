using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsKeyboardEditingTests
{
    [TestMethod]
    public void MoveAndAcceleratedMoveAreUndoablePlacementEdits()
    {
        var session = CreateSessionWithChart(out var item);
        var interaction = new SpreadsheetAnalyticsInteractionController();
        Assert.IsTrue(interaction.Select(item));
        var before = session.AnalyticsPlacements.GetPlacement(item).DocumentBounds;
        var undoBefore = session.History.UndoCount;

        Assert.IsTrue(SpreadsheetAnalyticsKeyboardEditing.Execute(
            SpreadsheetAnalyticsKeyboardMapper.Map(
                SpreadsheetAnalyticsKeyboardKey.Right),
            interaction,
            session.AnalyticsPlacements,
            session.Analytics));
        Assert.IsTrue(SpreadsheetAnalyticsKeyboardEditing.Execute(
            SpreadsheetAnalyticsKeyboardMapper.Map(
                SpreadsheetAnalyticsKeyboardKey.Down,
                SpreadsheetAnalyticsKeyboardModifiers.Control),
            interaction,
            session.AnalyticsPlacements,
            session.Analytics));

        var after = session.AnalyticsPlacements.GetPlacement(item).DocumentBounds;
        Assert.AreEqual(before.X + 1d, after.X);
        Assert.AreEqual(before.Y + 10d, after.Y);
        Assert.AreEqual(undoBefore + 2, session.History.UndoCount);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(before, session.AnalyticsPlacements.GetPlacement(item).DocumentBounds);
    }

    [TestMethod]
    public void ShiftArrowResizesWithSharedMinimumBoundsAndUndo()
    {
        var session = CreateSessionWithChart(out var item);
        var interaction = new SpreadsheetAnalyticsInteractionController();
        interaction.Select(item);
        var before = session.AnalyticsPlacements.GetPlacement(item).DocumentBounds;

        Assert.IsTrue(SpreadsheetAnalyticsKeyboardEditing.Execute(
            SpreadsheetAnalyticsKeyboardMapper.Map(
                SpreadsheetAnalyticsKeyboardKey.Right,
                SpreadsheetAnalyticsKeyboardModifiers.Shift |
                SpreadsheetAnalyticsKeyboardModifiers.Control),
            interaction,
            session.AnalyticsPlacements,
            session.Analytics));
        var after = session.AnalyticsPlacements.GetPlacement(item).DocumentBounds;
        Assert.AreEqual(before.Width + 10d, after.Width);
        Assert.AreEqual(before.Height, after.Height);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(before, session.AnalyticsPlacements.GetPlacement(item).DocumentBounds);
    }

    [TestMethod]
    public void DeleteRemovesSelectedAnalyticsAndEscapeClearsSelection()
    {
        var session = CreateSessionWithChart(out var item);
        var interaction = new SpreadsheetAnalyticsInteractionController();
        interaction.Select(item);

        Assert.IsTrue(SpreadsheetAnalyticsKeyboardEditing.Execute(
            SpreadsheetAnalyticsKeyboardMapper.Map(
                SpreadsheetAnalyticsKeyboardKey.Delete),
            interaction,
            session.AnalyticsPlacements,
            session.Analytics));
        Assert.IsNull(interaction.SelectedItem);
        Assert.AreEqual(0, session.Analytics.GetCharts(session.ActiveWorksheet).Count);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(1, session.Analytics.GetCharts(session.ActiveWorksheet).Count);

        interaction.Select(item);
        Assert.IsTrue(SpreadsheetAnalyticsKeyboardEditing.Execute(
            SpreadsheetAnalyticsKeyboardMapper.Map(
                SpreadsheetAnalyticsKeyboardKey.Escape),
            interaction,
            session.AnalyticsPlacements,
            session.Analytics));
        Assert.IsNull(interaction.SelectedItem);
    }

    [TestMethod]
    public void KeyboardIntentWithoutSelectionDoesNotMutateHistory()
    {
        var session = CreateSessionWithChart(out _);
        var interaction = new SpreadsheetAnalyticsInteractionController();
        var undoBefore = session.History.UndoCount;

        Assert.IsFalse(SpreadsheetAnalyticsKeyboardEditing.Execute(
            SpreadsheetAnalyticsKeyboardMapper.Map(
                SpreadsheetAnalyticsKeyboardKey.Left),
            interaction,
            session.AnalyticsPlacements,
            session.Analytics));
        Assert.AreEqual(undoBefore, session.History.UndoCount);
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
            SpreadsheetChartType.Column);
        item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        return session;
    }
}
