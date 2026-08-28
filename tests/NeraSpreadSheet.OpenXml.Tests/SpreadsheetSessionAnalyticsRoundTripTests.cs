using DocumentFormat.OpenXml.Packaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraWorkbook = NeraSpreadSheet.Core.Workbook;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class SpreadsheetSessionAnalyticsRoundTripTests
{
    private const string AnalyticsStateContentType =
        "application/vnd.neraspreadsheet.analytics-state+xml";

    [TestMethod]
    public async Task NativeRoundTripPreservesAnalyticsIdentitySemanticsPlacementAndWorksheetOwnership()
    {
        var workbook = new NeraWorkbook();
        var first = workbook.Worksheets[0];
        first.Name = "Dashboard";
        PopulateAnalyticsSource(first, 4);
        var second = workbook.AddWorksheet("Second");
        PopulateAnalyticsSource(second, 3);
        var session = new SpreadsheetSession(workbook);

        var firstRange = new CellRange(
            new CellAddress(0, 0),
            new CellAddress(3, 1));
        var chart = session.Analytics.InsertChart(
            firstRange,
            SpreadsheetChartType.Line,
            title: "Quarter trend",
            requestedName: "RevenueTrend");
        var pivot = session.Analytics.InsertPivot(
            firstRange,
            rowFieldColumnIndex: 0,
            valueFieldColumnIndex: 1,
            aggregation: SpreadsheetPivotAggregation.Average,
            requestedName: "RevenuePivot");
        var chartKey = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        var pivotKey = SpreadsheetAnalyticsItemKey.ForPivot(pivot.Id);
        var chartBounds = new RectD(101.25d, 202.5d, 444.75d, 255.5d);
        var pivotBounds = new RectD(33.5d, 44.75d, 300d, 180d);
        Assert.IsTrue(session.AnalyticsPlacements.SetBounds(chartKey, chartBounds));
        Assert.IsTrue(session.AnalyticsPlacements.SetBounds(pivotKey, pivotBounds));
        Assert.IsTrue(session.AnalyticsPlacements.BringToFront(chartKey));
        var expectedChartPlacement = session.AnalyticsPlacements.GetPlacement(chartKey);
        var expectedPivotPlacement = session.AnalyticsPlacements.GetPlacement(pivotKey);

        session.ActivateWorksheet(second);
        var secondRange = new CellRange(
            new CellAddress(0, 0),
            new CellAddress(2, 1));
        var secondChart = session.Analytics.InsertChart(
            secondRange,
            SpreadsheetChartType.Bar,
            title: "Second sheet",
            requestedName: "SecondChart");
        var secondChartKey = SpreadsheetAnalyticsItemKey.ForChart(secondChart.Id);
        var secondBounds = new RectD(12.5d, 24.25d, 280.5d, 190.75d);
        Assert.IsTrue(session.AnalyticsPlacements.SetBounds(secondChartKey, secondBounds));
        var expectedSecondPlacement = session.AnalyticsPlacements.GetPlacement(secondChartKey);

        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveSessionAsync(
            session,
            stream,
            new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, false))
        {
            var workbookPart = document.WorkbookPart ??
                throw new AssertFailedException("The XLSX package does not contain a workbook part.");
            var analyticsParts = workbookPart.CustomXmlParts
                .Where(part => string.Equals(
                    part.ContentType,
                    AnalyticsStateContentType,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.AreEqual(1, analyticsParts.Length);
        }

        stream.Position = 0L;
        var loaded = await serializer.LoadSessionAsync(
            stream,
            new OpenXmlImportOptions());
        var loadedFirst = loaded.Workbook.Worksheets.Single(sheet => sheet.Name == "Dashboard");
        var loadedSecond = loaded.Workbook.Worksheets.Single(sheet => sheet.Name == "Second");

        var loadedChart = loaded.Analytics.GetCharts(loadedFirst).Single();
        Assert.AreEqual(chart.Id, loadedChart.Id);
        Assert.AreEqual(chart.Name, loadedChart.Name);
        Assert.AreEqual(chart.ChartType, loadedChart.ChartType);
        Assert.AreEqual(chart.SourceRange, loadedChart.SourceRange);
        Assert.AreEqual(chart.Title, loadedChart.Title);
        Assert.AreEqual(
            chart.FirstRowContainsSeriesNames,
            loadedChart.FirstRowContainsSeriesNames);
        Assert.AreEqual(
            chart.FirstColumnContainsCategories,
            loadedChart.FirstColumnContainsCategories);

        var loadedPivot = loaded.Analytics.GetPivots(loadedFirst).Single();
        Assert.AreEqual(pivot.Id, loadedPivot.Id);
        Assert.AreEqual(pivot.Name, loadedPivot.Name);
        Assert.AreEqual(pivot.SourceRange, loadedPivot.SourceRange);
        Assert.AreEqual(pivot.RowFieldColumnIndex, loadedPivot.RowFieldColumnIndex);
        Assert.AreEqual(pivot.ValueFieldColumnIndex, loadedPivot.ValueFieldColumnIndex);
        Assert.AreEqual(pivot.Aggregation, loadedPivot.Aggregation);
        Assert.AreEqual(pivot.FirstRowContainsHeaders, loadedPivot.FirstRowContainsHeaders);

        AssertPlacementEqual(
            expectedChartPlacement,
            loaded.AnalyticsPlacements
                .GetPlacements(loadedFirst)
                .Single(placement => placement.Item == chartKey));
        AssertPlacementEqual(
            expectedPivotPlacement,
            loaded.AnalyticsPlacements
                .GetPlacements(loadedFirst)
                .Single(placement => placement.Item == pivotKey));

        var loadedSecondChart = loaded.Analytics.GetCharts(loadedSecond).Single();
        Assert.AreEqual(secondChart.Id, loadedSecondChart.Id);
        Assert.AreEqual(secondChart.Name, loadedSecondChart.Name);
        Assert.AreEqual(secondChart.ChartType, loadedSecondChart.ChartType);
        Assert.AreEqual(secondChart.SourceRange, loadedSecondChart.SourceRange);
        Assert.AreEqual(secondChart.Title, loadedSecondChart.Title);
        AssertPlacementEqual(
            expectedSecondPlacement,
            loaded.AnalyticsPlacements
                .GetPlacements(loadedSecond)
                .Single(placement => placement.Item == secondChartKey));

        Assert.AreEqual(0, loaded.History.UndoCount);
        Assert.AreEqual(0, loaded.History.RedoCount);
    }

    [TestMethod]
    public async Task SessionWithoutAnalyticsDoesNotEmitNativeAnalyticsMetadata()
    {
        var session = new SpreadsheetSession(new NeraWorkbook());
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveSessionAsync(
            session,
            stream,
            new OpenXmlExportOptions());

        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart ??
            throw new AssertFailedException("The XLSX package does not contain a workbook part.");
        Assert.IsFalse(workbookPart.CustomXmlParts.Any(part => string.Equals(
            part.ContentType,
            AnalyticsStateContentType,
            StringComparison.OrdinalIgnoreCase)));
    }

    private static void PopulateAnalyticsSource(Worksheet worksheet, int rowCount)
    {
        worksheet.SetValue(new CellAddress(0, 0), "Category");
        worksheet.SetValue(new CellAddress(0, 1), "Value");
        for (var row = 1; row < rowCount; row++)
        {
            worksheet.SetValue(new CellAddress(row, 0), $"Item {row}");
            worksheet.SetValue(new CellAddress(row, 1), row * 10d);
        }
    }

    private static void AssertPlacementEqual(
        SpreadsheetAnalyticsPlacement expected,
        SpreadsheetAnalyticsPlacement actual)
    {
        Assert.AreEqual(expected.Item, actual.Item);
        Assert.AreEqual(expected.DocumentBounds, actual.DocumentBounds);
        Assert.AreEqual(expected.ZIndex, actual.ZIndex);
    }
}
