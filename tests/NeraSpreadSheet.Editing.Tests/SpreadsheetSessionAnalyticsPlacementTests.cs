using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetSessionAnalyticsPlacementTests
{
    [TestMethod]
    public void SessionOwnedPlacementTracksAnalyticsLifetime()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Category");
        sheet.SetValue(new CellAddress(0, 1), "Value");
        sheet.SetValue(new CellAddress(1, 0), "A");
        sheet.SetValue(new CellAddress(1, 1), 10d);
        session.Selection.Select(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(1, 1)));

        var chart = session.Analytics.InsertChartFromSelection(
            SpreadsheetChartType.Column);
        var key = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);

        Assert.AreEqual(1, session.AnalyticsPlacements.Placements.Count);
        Assert.AreEqual(
            key,
            session.AnalyticsPlacements.GetPlacement(key).Item);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, session.AnalyticsPlacements.Placements.Count);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(1, session.AnalyticsPlacements.Placements.Count);
    }
}
