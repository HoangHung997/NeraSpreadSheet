using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetAnalyticsDisplayListComposerTests
{
    [TestMethod]
    public void ColumnChartProducesClippedBarsAndLabels()
    {
        var projection = CreateChartProjection(SpreadsheetChartType.Column);
        var bounds = new RectD(10d, 20d, 420d, 260d);

        var displayList = SpreadsheetAnalyticsDisplayListComposer.ComposeChart(
            projection,
            bounds);

        Assert.IsInstanceOfType<PushClipCommand>(displayList.Commands[0]);
        Assert.IsInstanceOfType<PopClipCommand>(displayList.Commands[^1]);
        Assert.IsTrue(displayList.Commands
            .OfType<FillRectangleCommand>()
            .Count() >= 5);
        Assert.IsTrue(displayList.Commands
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "Quarterly"));
        Assert.IsTrue(displayList.Commands
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "Q1"));
        AssertCommandsStayWithinClip(displayList, bounds);
    }

    [TestMethod]
    public void BarChartProducesHorizontalBars()
    {
        var projection = CreateChartProjection(SpreadsheetChartType.Bar);
        var bounds = new RectD(0d, 0d, 360d, 240d);

        var displayList = SpreadsheetAnalyticsDisplayListComposer.ComposeChart(
            projection,
            bounds);
        var bars = displayList.Commands
            .OfType<FillRectangleCommand>()
            .Where(command => command.Color != ColorRgba.White)
            .Where(command => command.Color != new ColorRgba(252, 252, 252))
            .ToArray();

        Assert.IsTrue(bars.Length >= 4);
        Assert.IsTrue(bars.Any(bar => bar.Bounds.Width > bar.Bounds.Height));
    }

    [TestMethod]
    public void LineChartProducesSeriesSegmentsAndPointMarkers()
    {
        var projection = CreateChartProjection(SpreadsheetChartType.Line);

        var displayList = SpreadsheetAnalyticsDisplayListComposer.ComposeChart(
            projection,
            new RectD(0d, 0d, 400d, 240d));

        Assert.IsTrue(displayList.Commands
            .OfType<DrawLineCommand>()
            .Any(command => command.StrokeWidth == 2d));
        Assert.IsTrue(displayList.Commands
            .OfType<FillRectangleCommand>()
            .Any(command =>
                command.Bounds.Width <= 4d &&
                command.Bounds.Height <= 4d));
    }

    [TestMethod]
    public void EmptyChartProducesDeterministicEmptyState()
    {
        var projection = new SpreadsheetChartProjection(
            Guid.NewGuid(),
            SpreadsheetChartType.Column,
            "Empty",
            []);

        var displayList = SpreadsheetAnalyticsDisplayListComposer.ComposeChart(
            projection,
            new RectD(0d, 0d, 300d, 180d));

        Assert.IsTrue(displayList.Commands
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "No chart data"));
    }

    [TestMethod]
    public void PieChartProducesVectorSectorsAndLegend()
    {
        var projection = CreateChartProjection(SpreadsheetChartType.Pie);
        var bounds = new RectD(0d, 0d, 420d, 260d);

        var displayList = SpreadsheetAnalyticsDisplayListComposer.ComposeChart(
            projection,
            bounds);
        var sectors = displayList.Commands
            .OfType<FillPolygonCommand>()
            .ToArray();

        Assert.AreEqual(2, sectors.Length);
        Assert.IsTrue(sectors.All(static sector => sector.Points.Count >= 4));
        Assert.IsTrue(displayList.Commands
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "Q1 40%"));
        Assert.IsTrue(displayList.Commands
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "Q2 60%"));
        foreach (var sector in sectors)
        {
            foreach (var point in sector.Points)
            {
                Assert.IsTrue(point.X >= bounds.Left && point.X <= bounds.Right);
                Assert.IsTrue(point.Y >= bounds.Top && point.Y <= bounds.Bottom);
            }
        }
    }

    [TestMethod]
    public void PieChartWithNoPositiveValuesProducesExplicitEmptyState()
    {
        var projection = new SpreadsheetChartProjection(
            Guid.NewGuid(),
            SpreadsheetChartType.Pie,
            "Invalid pie",
            [
                new SpreadsheetChartProjectedSeries(
                    "Values",
                    [
                        new SpreadsheetChartPoint("Zero", 0d),
                        new SpreadsheetChartPoint("Negative", -3d),
                        new SpreadsheetChartPoint("Missing", null),
                    ]),
            ]);

        var displayList = SpreadsheetAnalyticsDisplayListComposer.ComposeChart(
            projection,
            new RectD(0d, 0d, 320d, 200d));

        Assert.AreEqual(
            0,
            displayList.Commands.OfType<FillPolygonCommand>().Count());
        Assert.IsTrue(displayList.Commands
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "Pie requires positive values"));
    }

    [TestMethod]
    public void FilledPolygonBuilderRejectsInvalidGeometry()
    {
        var builder = new DisplayListBuilder();

        Assert.ThrowsExactly<ArgumentException>(() =>
            builder.FillPolygon(
                [new PointD(0d, 0d), new PointD(1d, 1d)],
                ColorRgba.Black));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            builder.FillPolygon(
                [
                    new PointD(0d, 0d),
                    new PointD(double.NaN, 1d),
                    new PointD(1d, 0d),
                ],
                ColorRgba.Black));
    }

    [TestMethod]
    public void PivotProjectionProducesHeaderRowsAndGridLines()
    {
        var projection = new SpreadsheetPivotProjection(
            Guid.NewGuid(),
            "Region",
            "Amount",
            SpreadsheetPivotAggregation.Sum,
            [
                new SpreadsheetPivotRow(
                    CellValue.FromText("North"),
                    "North",
                    17d,
                    2,
                    2),
                new SpreadsheetPivotRow(
                    CellValue.FromText("South"),
                    "South",
                    5d,
                    1,
                    1),
            ]);
        var bounds = new RectD(12d, 8d, 320d, 160d);

        var displayList = SpreadsheetAnalyticsDisplayListComposer.ComposePivot(
            projection,
            bounds);

        Assert.IsTrue(displayList.Commands
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "Region"));
        Assert.IsTrue(displayList.Commands
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "Sum of Amount"));
        Assert.IsTrue(displayList.Commands
            .OfType<DrawTextCommand>()
            .Any(command => command.Text == "17"));
        Assert.IsTrue(displayList.Commands.OfType<DrawLineCommand>().Count() >= 4);
        AssertCommandsStayWithinClip(displayList, bounds);
    }

    private static SpreadsheetChartProjection CreateChartProjection(
        SpreadsheetChartType chartType) =>
        new(
            Guid.NewGuid(),
            chartType,
            "Quarterly",
            [
                new SpreadsheetChartProjectedSeries(
                    "Sales",
                    [
                        new SpreadsheetChartPoint("Q1", 12d),
                        new SpreadsheetChartPoint("Q2", 18d),
                    ]),
                new SpreadsheetChartProjectedSeries(
                    "Cost",
                    [
                        new SpreadsheetChartPoint("Q1", 7d),
                        new SpreadsheetChartPoint("Q2", -4d),
                    ]),
            ]);

    private static void AssertCommandsStayWithinClip(
        DisplayList displayList,
        RectD clip)
    {
        foreach (var fill in displayList.Commands.OfType<FillRectangleCommand>())
        {
            Assert.IsTrue(
                clip.Contains(fill.Bounds),
                $"Fill {fill.Bounds} escaped clip {clip}.");
        }
        foreach (var text in displayList.Commands.OfType<DrawTextCommand>())
        {
            Assert.IsTrue(
                clip.Contains(text.Bounds),
                $"Text {text.Bounds} escaped clip {clip}.");
        }
    }
}
