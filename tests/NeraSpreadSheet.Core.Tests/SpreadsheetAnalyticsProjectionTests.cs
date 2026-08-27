using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsProjectionTests
{
    [TestMethod]
    public void ChartProjectionUsesHeadersCategoriesAndNumericValues()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Month");
        sheet.SetValue(new CellAddress(0, 1), "Sales");
        sheet.SetValue(new CellAddress(0, 2), "Cost");
        sheet.SetValue(new CellAddress(1, 0), "Jan");
        sheet.SetValue(new CellAddress(1, 1), 12d);
        sheet.SetValue(new CellAddress(1, 2), 7d);
        sheet.SetValue(new CellAddress(2, 0), "Feb");
        sheet.SetValue(new CellAddress(2, 1), 18d);
        sheet.SetValue(new CellAddress(2, 2), "n/a");

        var definition = new SpreadsheetChartDefinition(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Chart1",
            SpreadsheetChartType.Column,
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 2)),
            "Monthly");

        var projection = SpreadsheetChartProjector.Project(sheet, definition);

        Assert.AreEqual(SpreadsheetChartType.Column, projection.ChartType);
        Assert.AreEqual("Monthly", projection.Title);
        Assert.AreEqual(2, projection.Series.Count);
        Assert.AreEqual("Sales", projection.Series[0].Name);
        Assert.AreEqual("Cost", projection.Series[1].Name);
        Assert.AreEqual("Jan", projection.Series[0].Points[0].Category);
        Assert.AreEqual(12d, projection.Series[0].Points[0].Value);
        Assert.AreEqual("Feb", projection.Series[0].Points[1].Category);
        Assert.AreEqual(18d, projection.Series[0].Points[1].Value);
        Assert.IsNull(projection.Series[1].Points[1].Value);
    }

    [TestMethod]
    public void ChartProjectionWithoutHeadersCreatesDeterministicLabels()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), 2d);
        sheet.SetValue(new CellAddress(1, 0), 3d);

        var definition = new SpreadsheetChartDefinition(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Chart2",
            SpreadsheetChartType.Line,
            new CellRange(new CellAddress(0, 0), new CellAddress(1, 0)),
            firstRowContainsSeriesNames: false,
            firstColumnContainsCategories: false);

        var projection = SpreadsheetChartProjector.Project(sheet, definition);

        Assert.AreEqual(1, projection.Series.Count);
        Assert.AreEqual("Series 1", projection.Series[0].Name);
        Assert.AreEqual("1", projection.Series[0].Points[0].Category);
        Assert.AreEqual("2", projection.Series[0].Points[1].Category);
        Assert.AreEqual(2d, projection.Series[0].Points[0].Value);
        Assert.AreEqual(3d, projection.Series[0].Points[1].Value);
    }

    [TestMethod]
    public void PivotProjectionGroupsInFirstSeenOrderAndSumsNumbers()
    {
        var sheet = BuildPivotSheet();
        var definition = CreatePivotDefinition(SpreadsheetPivotAggregation.Sum);

        var projection = SpreadsheetPivotProjector.Project(sheet, definition);

        Assert.AreEqual("Region", projection.RowFieldName);
        Assert.AreEqual("Amount", projection.ValueFieldName);
        Assert.AreEqual(3, projection.Rows.Count);
        Assert.AreEqual("North", projection.Rows[0].Label);
        Assert.AreEqual(17d, projection.Rows[0].Value);
        Assert.AreEqual(2, projection.Rows[0].SourceRowCount);
        Assert.AreEqual("South", projection.Rows[1].Label);
        Assert.AreEqual(5d, projection.Rows[1].Value);
        Assert.AreEqual("(blank)", projection.Rows[2].Label);
        Assert.AreEqual(3d, projection.Rows[2].Value);
    }

    [TestMethod]
    public void PivotProjectionSupportsCountAverageMinimumAndMaximum()
    {
        var sheet = BuildPivotSheet();

        var count = SpreadsheetPivotProjector.Project(
            sheet,
            CreatePivotDefinition(SpreadsheetPivotAggregation.Count));
        var average = SpreadsheetPivotProjector.Project(
            sheet,
            CreatePivotDefinition(SpreadsheetPivotAggregation.Average));
        var minimum = SpreadsheetPivotProjector.Project(
            sheet,
            CreatePivotDefinition(SpreadsheetPivotAggregation.Minimum));
        var maximum = SpreadsheetPivotProjector.Project(
            sheet,
            CreatePivotDefinition(SpreadsheetPivotAggregation.Maximum));

        Assert.AreEqual(2d, count.Rows[0].Value);
        Assert.AreEqual(8.5d, average.Rows[0].Value);
        Assert.AreEqual(7d, minimum.Rows[0].Value);
        Assert.AreEqual(10d, maximum.Rows[0].Value);
    }

    [TestMethod]
    public void PivotDefinitionRejectsFieldsOutsideSourceRange()
    {
        var range = new CellRange(
            new CellAddress(0, 2),
            new CellAddress(4, 4));

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SpreadsheetPivotDefinition(
                Guid.NewGuid(),
                "Pivot",
                range,
                1,
                3));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new SpreadsheetPivotDefinition(
                Guid.NewGuid(),
                "Pivot",
                range,
                2,
                5));
    }

    private static Worksheet BuildPivotSheet()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Region");
        sheet.SetValue(new CellAddress(0, 1), "Amount");
        sheet.SetValue(new CellAddress(1, 0), "North");
        sheet.SetValue(new CellAddress(1, 1), 10d);
        sheet.SetValue(new CellAddress(2, 0), "South");
        sheet.SetValue(new CellAddress(2, 1), 5d);
        sheet.SetValue(new CellAddress(3, 0), "North");
        sheet.SetValue(new CellAddress(3, 1), 7d);
        sheet.SetValue(new CellAddress(4, 0), null);
        sheet.SetValue(new CellAddress(4, 1), 3d);
        return sheet;
    }

    private static SpreadsheetPivotDefinition CreatePivotDefinition(
        SpreadsheetPivotAggregation aggregation) =>
        new(
            Guid.Parse("33333333-3333-3333-3333-333333333333"),
            "Pivot1",
            new CellRange(new CellAddress(0, 0), new CellAddress(4, 1)),
            0,
            1,
            aggregation);
}
