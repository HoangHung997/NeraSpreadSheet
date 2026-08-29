using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class OpenXmlSpreadsheetSessionChartPersistenceExtensionsTests
{
    private const string ManagedDescriptionPrefix = "NeraSpreadSheet:Chart:";

    [TestMethod]
    public async Task SaveSessionWithStandardChartsAsyncEmitsEverySupportedChartKind()
    {
        var workbook = CreateWorkbook("O'Brien Data");
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        var sourceRange = new CellRange(
            new CellAddress(0, 0),
            new CellAddress(3, 1));
        var chartTypes = new[]
        {
            SpreadsheetChartType.Column,
            SpreadsheetChartType.Bar,
            SpreadsheetChartType.Line,
            SpreadsheetChartType.Pie,
        };

        for (var index = 0; index < chartTypes.Length; index++)
        {
            var chart = session.Analytics.InsertChart(
                sourceRange,
                chartTypes[index],
                title: $"Chart {index + 1}",
                requestedName: $"Chart{index + 1}");
            Assert.IsTrue(session.AnalyticsPlacements.SetBounds(
                SpreadsheetAnalyticsItemKey.ForChart(chart.Id),
                new RectD(20d + (index * 40d), 30d + (index * 25d), 320d, 180d)));
        }

        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var stream = new MemoryStream();
        await serializer.SaveSessionWithStandardChartsAsync(
            session,
            stream,
            new OpenXmlExportOptions());

        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var worksheetPart = document.WorkbookPart?.WorksheetParts.Single()
            ?? throw new AssertFailedException("The package does not contain one worksheet part.");
        var drawingsPart = worksheetPart.DrawingsPart
            ?? throw new AssertFailedException("The session save did not emit a standard drawings part.");
        var anchors = drawingsPart.WorksheetDrawing?
            .Elements<Xdr.AbsoluteAnchor>()
            .ToArray()
            ?? [];
        Assert.AreEqual(4, anchors.Length);
        Assert.AreEqual(
            4,
            anchors.Count(anchor =>
                anchor.Descendants<Xdr.NonVisualDrawingProperties>()
                    .Any(properties => properties.Description?.Value?.StartsWith(
                        ManagedDescriptionPrefix,
                        StringComparison.Ordinal) == true)));

        var chartParts = drawingsPart.Parts
            .Select(static pair => pair.OpenXmlPart)
            .OfType<ChartPart>()
            .ToArray();
        Assert.AreEqual(4, chartParts.Length);
        Assert.AreEqual(1, chartParts.Count(part =>
            part.ChartSpace?.Descendants<C.BarChart>()
                .Any(chart => chart.BarDirection?.Val?.Value == C.BarDirectionValues.Column) == true));
        Assert.AreEqual(1, chartParts.Count(part =>
            part.ChartSpace?.Descendants<C.BarChart>()
                .Any(chart => chart.BarDirection?.Val?.Value == C.BarDirectionValues.Bar) == true));
        Assert.AreEqual(1, chartParts.Count(part =>
            part.ChartSpace?.Descendants<C.LineChart>().Any() == true));
        Assert.AreEqual(1, chartParts.Count(part =>
            part.ChartSpace?.Descendants<C.PieChart>().Any() == true));

        var formulas = chartParts
            .SelectMany(static part => part.ChartSpace?.Descendants<C.Formula>() ?? [])
            .Select(static formula => formula.Text)
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
        CollectionAssert.Contains(formulas, "'O''Brien Data'!$B$1");
        CollectionAssert.Contains(formulas, "'O''Brien Data'!$A$2:$A$4");
        CollectionAssert.Contains(formulas, "'O''Brien Data'!$B$2:$B$4");

        var validationErrors = new OpenXmlValidator()
            .Validate(document)
            .Select(error => $"{error.Path?.XPath}: {error.Description}")
            .ToArray();
        Assert.AreEqual(0, validationErrors.Length, string.Join(Environment.NewLine, validationErrors));
    }

    [TestMethod]
    public async Task ReloadAndResaveRefreshesManagedChartWithoutDuplicatingParts()
    {
        var workbook = CreateWorkbook("Dashboard");
        var session = new SpreadsheetSession(workbook);
        var chart = session.Analytics.InsertChart(
            new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
            SpreadsheetChartType.Line,
            title: "Revenue trend",
            requestedName: "RevenueTrend");
        Assert.IsTrue(session.AnalyticsPlacements.SetBounds(
            SpreadsheetAnalyticsItemKey.ForChart(chart.Id),
            new RectD(40d, 50d, 360d, 220d)));

        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var first = new MemoryStream();
        await serializer.SaveSessionWithStandardChartsAsync(
            session,
            first,
            new OpenXmlExportOptions());

        first.Position = 0L;
        var loaded = await serializer.LoadSessionAsync(first, new OpenXmlImportOptions());
        await using var second = new MemoryStream();
        await serializer.SaveSessionWithStandardChartsAsync(
            loaded,
            second,
            new OpenXmlExportOptions());

        second.Position = 0L;
        using var document = SpreadsheetDocument.Open(second, false);
        var drawingsPart = document.WorkbookPart?.WorksheetParts.Single().DrawingsPart
            ?? throw new AssertFailedException("The resaved package lost its drawings part.");
        var managedAnchors = drawingsPart.WorksheetDrawing?
            .Elements<Xdr.AbsoluteAnchor>()
            .Where(anchor => anchor.Descendants<Xdr.NonVisualDrawingProperties>()
                .Any(properties => properties.Description?.Value?.StartsWith(
                    ManagedDescriptionPrefix,
                    StringComparison.Ordinal) == true))
            .ToArray()
            ?? [];
        Assert.AreEqual(1, managedAnchors.Length);
        Assert.AreEqual(
            1,
            drawingsPart.Parts
                .Select(static pair => pair.OpenXmlPart)
                .OfType<ChartPart>()
                .Count());

        var validationErrors = new OpenXmlValidator()
            .Validate(document)
            .Select(error => $"{error.Path?.XPath}: {error.Description}")
            .ToArray();
        Assert.AreEqual(0, validationErrors.Length, string.Join(Environment.NewLine, validationErrors));
    }

    [TestMethod]
    public async Task SessionWithoutChartsDoesNotCreateDrawingPart()
    {
        var session = new SpreadsheetSession(CreateWorkbook("NoCharts"));
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveSessionWithStandardChartsAsync(
            session,
            stream,
            new OpenXmlExportOptions());

        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        Assert.IsNull(document.WorkbookPart?.WorksheetParts.Single().DrawingsPart);
    }

    private static Workbook CreateWorkbook(string worksheetName)
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(worksheet, worksheetName);
        worksheet.SetValue(new CellAddress(0, 0), "Category");
        worksheet.SetValue(new CellAddress(0, 1), "Revenue");
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "B");
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 0), "C");
        worksheet.SetValue(new CellAddress(3, 1), 30d);
        return workbook;
    }
}
