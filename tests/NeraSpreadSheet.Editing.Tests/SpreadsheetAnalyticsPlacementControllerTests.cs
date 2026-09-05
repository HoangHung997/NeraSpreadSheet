using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsPlacementControllerTests
{
    [TestMethod]
    public void ChartLifetimeCreatesRemovesAndRestoresPlacement()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var placements = new SpreadsheetAnalyticsPlacementController(
            session,
            session.Analytics);
        PopulateSource(workbook.Worksheets[0]);
        session.Selection.Select(SourceRange());

        var chart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Column);
        var key = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        var inserted = placements.GetPlacement(key);

        Assert.AreEqual(1, placements.Placements.Count);
        Assert.AreEqual(
            new RectD(
                SpreadsheetAnalyticsPlacementController.DefaultInset,
                SpreadsheetAnalyticsPlacementController.DefaultInset,
                SpreadsheetAnalyticsPlacementController.DefaultWidth,
                SpreadsheetAnalyticsPlacementController.DefaultHeight),
            inserted.DocumentBounds);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, placements.Placements.Count);

        Assert.IsTrue(session.Redo());
        Assert.AreEqual(inserted, placements.GetPlacement(key));
    }

    [TestMethod]
    public void MoveParticipatesInSharedUndoRedoHistory()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var placements = new SpreadsheetAnalyticsPlacementController(
            session,
            session.Analytics);
        PopulateSource(workbook.Worksheets[0]);
        session.Selection.Select(SourceRange());
        var chart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Line);
        var key = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        var before = placements.GetPlacement(key);
        var historyBeforeMove = session.History.UndoCount;

        Assert.IsTrue(placements.MoveBy(key, 37.5d, 18.25d));
        var moved = placements.GetPlacement(key);
        Assert.AreEqual(before.DocumentBounds.X + 37.5d, moved.DocumentBounds.X);
        Assert.AreEqual(before.DocumentBounds.Y + 18.25d, moved.DocumentBounds.Y);
        Assert.AreEqual(historyBeforeMove + 1, session.History.UndoCount);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(before, placements.GetPlacement(key));
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(moved, placements.GetPlacement(key));
    }

    [TestMethod]
    public void RemovalUndoRestoresLatestTransformedPlacement()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var placements = new SpreadsheetAnalyticsPlacementController(
            session,
            session.Analytics);
        PopulateSource(workbook.Worksheets[0]);
        session.Selection.Select(SourceRange());
        var pivot = session.Analytics.InsertPivotFromSelection();
        var key = SpreadsheetAnalyticsItemKey.ForPivot(pivot.Id);
        var customBounds = new RectD(125d, 95d, 480d, 260d);
        Assert.IsTrue(placements.SetBounds(key, customBounds));

        Assert.IsTrue(session.Analytics.RemovePivot(pivot.Id));
        Assert.IsFalse(placements.TryGetPlacement(key, out _));

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(customBounds, placements.GetPlacement(key).DocumentBounds);
    }

    [TestMethod]
    public void PlacementsAreIsolatedPerWorksheetAndCascadeDeterministically()
    {
        var workbook = new Workbook();
        var first = workbook.Worksheets[0];
        var second = workbook.AddWorksheet("Second");
        PopulateSource(first);
        PopulateSource(second);
        var session = new SpreadsheetSession(workbook);
        var placements = new SpreadsheetAnalyticsPlacementController(
            session,
            session.Analytics);
        session.Selection.Select(SourceRange());

        var firstChart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Column);
        var secondChart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Bar);
        var firstPlacements = placements.GetPlacements(first);
        Assert.AreEqual(2, firstPlacements.Count);
        Assert.AreEqual(
            SpreadsheetAnalyticsPlacementController.CascadeStep,
            firstPlacements[1].DocumentBounds.X - firstPlacements[0].DocumentBounds.X);
        Assert.IsTrue(firstPlacements[1].ZIndex > firstPlacements[0].ZIndex);

        session.ActivateWorksheet(second);
        session.Selection.Select(SourceRange());
        var secondSheetChart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Line);
        var secondPlacements = placements.GetPlacements(second);
        Assert.AreEqual(1, secondPlacements.Count);
        Assert.AreEqual(
            SpreadsheetAnalyticsPlacementController.DefaultInset,
            secondPlacements[0].DocumentBounds.X);

        Assert.AreEqual(
            firstChart.Id,
            firstPlacements[0].Item.Id);
        Assert.AreEqual(
            secondChart.Id,
            firstPlacements[1].Item.Id);
        Assert.AreEqual(
            secondSheetChart.Id,
            secondPlacements[0].Item.Id);
    }

    [TestMethod]
    public void BringToFrontIsUndoableAndDoesNotCreateNoOpHistory()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var placements = new SpreadsheetAnalyticsPlacementController(
            session,
            session.Analytics);
        PopulateSource(workbook.Worksheets[0]);
        session.Selection.Select(SourceRange());
        var first = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Column);
        session.Analytics.InsertChartFromSelection(SpreadsheetChartType.Line);
        var firstKey = SpreadsheetAnalyticsItemKey.ForChart(first.Id);
        var before = placements.GetPlacement(firstKey);

        Assert.IsTrue(placements.BringToFront(firstKey));
        var after = placements.GetPlacement(firstKey);
        Assert.IsTrue(after.ZIndex > before.ZIndex);
        Assert.IsFalse(placements.BringToFront(firstKey));

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(before, placements.GetPlacement(firstKey));
    }

    private static void PopulateSource(Worksheet sheet)
    {
        sheet.SetValue(new CellAddress(0, 0), "Region");
        sheet.SetValue(new CellAddress(0, 1), "Amount");
        sheet.SetValue(new CellAddress(1, 0), "North");
        sheet.SetValue(new CellAddress(1, 1), 10d);
        sheet.SetValue(new CellAddress(2, 0), "South");
        sheet.SetValue(new CellAddress(2, 1), 5d);
        sheet.SetValue(new CellAddress(3, 0), "North");
        sheet.SetValue(new CellAddress(3, 1), 7d);
    }

    private static CellRange SourceRange() =>
        new(new CellAddress(0, 0), new CellAddress(3, 1));
}
