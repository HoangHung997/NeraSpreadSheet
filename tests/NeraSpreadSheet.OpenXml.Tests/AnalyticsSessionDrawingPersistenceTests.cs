using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class AnalyticsSessionDrawingPersistenceTests
{
    private const string AnalyticsStateContentType =
        "application/vnd.neraspreadsheet.analytics-state+xml";
    private const string ManagedDescriptionPrefix = "NeraSpreadSheet:Chart:";

    [TestMethod]
    public async Task SaveSessionMaterializesStandardDrawingWithoutManualCodecCall()
    {
        var (session, chart) = CreateChartSession();
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var stream = new MemoryStream();

        await serializer.SaveSessionAsync(
            session,
            stream,
            new OpenXmlExportOptions());

        AssertManagedChartPackage(stream, chart.Id, expectedAnalyticsPartCount: 1);
    }

    [TestMethod]
    public async Task SaveLoadSaveKeepsSingleManagedChartAndSingleAnalyticsMetadataPart()
    {
        var (session, chart) = CreateChartSession();
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var first = new MemoryStream();

        await serializer.SaveSessionAsync(
            session,
            first,
            new OpenXmlExportOptions());
        AssertManagedChartPackage(first, chart.Id, expectedAnalyticsPartCount: 1);

        first.Position = 0L;
        var loaded = await serializer.LoadSessionAsync(
            first,
            new OpenXmlImportOptions());
        var loadedChart = loaded.Analytics.GetCharts(loaded.ActiveWorksheet).Single();
        Assert.AreEqual(chart.Id, loadedChart.Id);

        await using var second = new MemoryStream();
        await serializer.SaveSessionAsync(
            loaded,
            second,
            new OpenXmlExportOptions());

        AssertManagedChartPackage(second, chart.Id, expectedAnalyticsPartCount: 1);
    }

    [TestMethod]
    public async Task RemovingLastChartAfterLoadRemovesManagedDrawingAndAnalyticsMetadata()
    {
        var (session, chart) = CreateChartSession();
        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var first = new MemoryStream();

        await serializer.SaveSessionAsync(
            session,
            first,
            new OpenXmlExportOptions());

        first.Position = 0L;
        var loaded = await serializer.LoadSessionAsync(
            first,
            new OpenXmlImportOptions());
        Assert.IsTrue(loaded.Analytics.RemoveChart(chart.Id));
        Assert.AreEqual(0, loaded.Analytics.GetCharts(loaded.ActiveWorksheet).Count);
        Assert.AreEqual(0, loaded.AnalyticsPlacements.GetPlacements(loaded.ActiveWorksheet).Count);

        await using var second = new MemoryStream();
        await serializer.SaveSessionAsync(
            loaded,
            second,
            new OpenXmlExportOptions());

        second.Position = 0L;
        using var document = SpreadsheetDocument.Open(second, false);
        var workbookPart = document.WorkbookPart
            ?? throw new AssertFailedException("The package has no workbook part.");
        Assert.AreEqual(
            0,
            workbookPart.CustomXmlParts.Count(part => string.Equals(
                part.ContentType,
                AnalyticsStateContentType,
                StringComparison.OrdinalIgnoreCase)));

        var worksheetPart = workbookPart.WorksheetParts.Single();
        Assert.IsNull(
            worksheetPart.DrawingsPart,
            "Removing the final managed chart should not leave an empty Nera drawings part.");
        Assert.AreEqual(
            0,
            worksheetPart.Worksheet?
                .Elements<DocumentFormat.OpenXml.Spreadsheet.Drawing>()
                .Count() ?? 0,
            "Removing the final managed chart should remove its worksheet drawing relationship markup.");
        AssertSchemaValid(document);
    }

    private static (SpreadsheetSession Session, SpreadsheetChartDefinition Chart)
        CreateChartSession()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(worksheet, "Dashboard");
        worksheet.SetValue(new CellAddress(0, 0), "Category");
        worksheet.SetValue(new CellAddress(0, 1), "Revenue");
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "B");
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 0), "C");
        worksheet.SetValue(new CellAddress(3, 1), 30d);

        var session = new SpreadsheetSession(workbook);
        var chart = session.Analytics.InsertChart(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            SpreadsheetChartType.Line,
            title: "Revenue trend",
            requestedName: "RevenueTrend");
        var chartKey = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        Assert.IsTrue(session.AnalyticsPlacements.SetBounds(
            chartKey,
            new RectD(20d, 30d, 420d, 240d)));
        return (session, chart);
    }

    private static void AssertManagedChartPackage(
        MemoryStream stream,
        Guid chartId,
        int expectedAnalyticsPartCount)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart
            ?? throw new AssertFailedException("The package has no workbook part.");
        Assert.AreEqual(
            expectedAnalyticsPartCount,
            workbookPart.CustomXmlParts.Count(part => string.Equals(
                part.ContentType,
                AnalyticsStateContentType,
                StringComparison.OrdinalIgnoreCase)));

        var worksheetPart = workbookPart.WorksheetParts.Single();
        var drawingsPart = worksheetPart.DrawingsPart
            ?? throw new AssertFailedException(
                "SaveSessionAsync did not materialize the analytics chart as a standard drawings part.");
        var managedAnchors = drawingsPart.WorksheetDrawing?
            .ChildElements
            .OfType<DocumentFormat.OpenXml.OpenXmlCompositeElement>()
            .Where(anchor => anchor
                .Descendants<Xdr.NonVisualDrawingProperties>()
                .Any(properties => string.Equals(
                    properties.Description?.Value,
                    ManagedDescriptionPrefix + chartId.ToString("N"),
                    StringComparison.Ordinal)))
            .ToArray()
            ?? [];
        Assert.AreEqual(1, managedAnchors.Length);
        Assert.AreEqual(
            1,
            drawingsPart.Parts.Count(pair => pair.OpenXmlPart is ChartPart),
            "Repeated session saves must not accumulate orphan or duplicate managed ChartPart relationships.");
        AssertSchemaValid(document);
    }

    private static void AssertSchemaValid(SpreadsheetDocument document)
    {
        var validationErrors = new OpenXmlValidator()
            .Validate(document)
            .Select(error => $"{error.Path?.XPath}: {error.Description}")
            .ToArray();
        Assert.AreEqual(
            0,
            validationErrors.Length,
            string.Join(Environment.NewLine, validationErrors));
    }
}
