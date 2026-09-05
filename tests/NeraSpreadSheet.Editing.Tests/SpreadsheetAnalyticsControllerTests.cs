using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsControllerTests
{
    [TestMethod]
    public void InsertChartParticipatesInUndoRedoAndPublishesChanges()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        PopulateSource(workbook.Worksheets[0]);
        session.Selection.Select(SourceRange());
        var changes = new List<SpreadsheetAnalyticsChangeKind>();
        session.Analytics.Changed += (_, args) => changes.Add(args.ChangeKind);

        var chart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Column,
            "Sales");

        Assert.AreEqual(1, session.Analytics.Charts.Count);
        Assert.AreEqual(chart.Id, session.Analytics.Charts[0].Id);
        Assert.AreEqual("Chart1", chart.Name);
        Assert.AreEqual(1, session.History.UndoCount);
        Assert.AreEqual(SpreadsheetAnalyticsChangeKind.ChartAdded, changes[0]);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, session.Analytics.Charts.Count);
        Assert.AreEqual(SpreadsheetAnalyticsChangeKind.ChartRemoved, changes[1]);

        Assert.IsTrue(session.Redo());
        Assert.AreEqual(1, session.Analytics.Charts.Count);
        Assert.AreEqual(chart.Id, session.Analytics.Charts[0].Id);
        Assert.AreEqual(SpreadsheetAnalyticsChangeKind.ChartAdded, changes[2]);
    }

    [TestMethod]
    public void RemoveChartCanBeUndoneAtOriginalCollectionPosition()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        PopulateSource(workbook.Worksheets[0]);
        session.Selection.Select(SourceRange());
        var first = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Line);
        var second = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Bar);

        Assert.IsTrue(session.Analytics.RemoveChart(first.Id));
        Assert.AreEqual(1, session.Analytics.Charts.Count);
        Assert.AreEqual(second.Id, session.Analytics.Charts[0].Id);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(2, session.Analytics.Charts.Count);
        Assert.AreEqual(first.Id, session.Analytics.Charts[0].Id);
        Assert.AreEqual(second.Id, session.Analytics.Charts[1].Id);
    }

    [TestMethod]
    public void PivotProjectionIsLiveAndUndoable()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        PopulateSource(sheet);
        session.Selection.Select(SourceRange());

        var pivot = session.Analytics.InsertPivotFromSelection();
        var firstProjection = session.Analytics.ProjectPivot(pivot.Id);
        Assert.AreEqual(2, firstProjection.Rows.Count);
        Assert.AreEqual("North", firstProjection.Rows[0].Label);
        Assert.AreEqual(17d, firstProjection.Rows[0].Value);

        sheet.SetValue(new CellAddress(3, 1), 20d);
        var updatedProjection = session.Analytics.ProjectPivot(pivot.Id);
        Assert.AreEqual(30d, updatedProjection.Rows[0].Value);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, session.Analytics.Pivots.Count);
        Assert.ThrowsExactly<KeyNotFoundException>(() =>
            session.Analytics.ProjectPivot(pivot.Id));
    }

    [TestMethod]
    public async Task AnalyticsCommandsFollowSelectionAndCreateRequestedKinds()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        PopulateSource(workbook.Worksheets[0]);

        Assert.IsFalse(session.CommandDispatcher.QueryState(
            SpreadsheetAnalyticsCommandIds.InsertPieChart).IsEnabled);
        Assert.IsFalse(await session.CommandDispatcher.TryExecuteAsync(
            SpreadsheetAnalyticsCommandIds.InsertPieChart));

        session.Selection.Select(SourceRange());
        Assert.IsTrue(session.CommandDispatcher.QueryState(
            SpreadsheetAnalyticsCommandIds.InsertPieChart).IsEnabled);
        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(
            SpreadsheetAnalyticsCommandIds.InsertPieChart));
        Assert.AreEqual(SpreadsheetChartType.Pie, session.Analytics.Charts.Single().ChartType);

        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(
            SpreadsheetAnalyticsCommandIds.InsertSumPivot));
        Assert.AreEqual(
            SpreadsheetPivotAggregation.Sum,
            session.Analytics.Pivots.Single().Aggregation);
    }

    [TestMethod]
    public void AnalyticsCollectionsAreIsolatedPerWorksheet()
    {
        var workbook = new Workbook();
        var firstSheet = workbook.Worksheets[0];
        var secondSheet = workbook.AddWorksheet("Second");
        PopulateSource(firstSheet);
        PopulateSource(secondSheet);
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(SourceRange());
        var firstChart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Column);

        session.ActivateWorksheet(secondSheet);
        session.Selection.Select(SourceRange());
        Assert.AreEqual(0, session.Analytics.Charts.Count);
        var secondChart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Line);

        Assert.AreEqual(1, session.Analytics.GetCharts(firstSheet).Count);
        Assert.AreEqual(firstChart.Id, session.Analytics.GetCharts(firstSheet)[0].Id);
        Assert.AreEqual(1, session.Analytics.GetCharts(secondSheet).Count);
        Assert.AreEqual(secondChart.Id, session.Analytics.GetCharts(secondSheet)[0].Id);
    }

    [TestMethod]
    public void DivergentAnalyticsEditInvalidatesRedoHistory()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        PopulateSource(workbook.Worksheets[0]);
        session.Selection.Select(SourceRange());
        session.Analytics.InsertChartFromSelection(SpreadsheetChartType.Column);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(session.History.CanRedo);

        session.Analytics.InsertPivotFromSelection();

        Assert.IsFalse(session.History.CanRedo);
        Assert.IsFalse(session.Redo());
        Assert.AreEqual(0, session.Analytics.Charts.Count);
        Assert.AreEqual(1, session.Analytics.Pivots.Count);
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
