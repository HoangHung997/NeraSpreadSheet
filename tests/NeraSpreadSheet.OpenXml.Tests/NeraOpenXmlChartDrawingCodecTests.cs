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
public sealed class NeraOpenXmlChartDrawingCodecTests
{
    private const double EmusPerPixel = 9_525d;

    [TestMethod]
    public async Task ExportCreatesSchemaValidStandardDrawingAndChartParts()
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
            new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
            SpreadsheetChartType.Line,
            title: "Revenue trend",
            requestedName: "RevenueTrend");
        var chartKey = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
        var bounds = new RectD(20d, 30d, 420d, 240d);
        Assert.IsTrue(session.AnalyticsPlacements.SetBounds(chartKey, bounds));

        var workbookSerializer = new NeraOpenXmlWorkbookSerializer();
        await using var stream = new MemoryStream();
        await workbookSerializer.SaveAsync(
            workbook,
            stream,
            new OpenXmlExportOptions());

        stream.Position = 0L;
        using (var document = SpreadsheetDocument.Open(stream, true))
        {
            NeraOpenXmlChartDrawingCodec.Export(
                document,
                session,
                CancellationToken.None);
        }

        stream.Position = 0L;
        using var readDocument = SpreadsheetDocument.Open(stream, false);
        var workbookPart = readDocument.WorkbookPart ??
            throw new AssertFailedException("The package has no workbook part.");
        var worksheetPart = workbookPart.WorksheetParts.Single();
        var drawingsPart = worksheetPart.DrawingsPart ??
            throw new AssertFailedException("The chart export did not create a drawings part.");
        var drawingRelationshipId = worksheetPart.GetIdOfPart(drawingsPart);
        Assert.AreEqual(
            drawingRelationshipId,
            worksheetPart.Worksheet.Elements<DocumentFormat.OpenXml.Spreadsheet.Drawing>()
                .Single()
                .Id?.Value);

        var anchor = drawingsPart.WorksheetDrawing?
            .Elements<Xdr.AbsoluteAnchor>()
            .SingleOrDefault()
            ?? throw new AssertFailedException(
                "The drawings part did not contain exactly one absolute anchor.");
        Assert.AreEqual(ToEmu(bounds.X), anchor.Position?.X?.Value);
        Assert.AreEqual(ToEmu(bounds.Y), anchor.Position?.Y?.Value);
        Assert.AreEqual(ToEmu(bounds.Width), anchor.Extent?.Cx?.Value);
        Assert.AreEqual(ToEmu(bounds.Height), anchor.Extent?.Cy?.Value);
        var nonVisual = anchor.Descendants<Xdr.NonVisualDrawingProperties>().Single();
        Assert.AreEqual(chart.Name, nonVisual.Name?.Value);
        Assert.AreEqual(
            $"NeraSpreadSheet:Chart:{chart.Id:N}",
            nonVisual.Description?.Value);

        var chartPart = drawingsPart.Parts
            .Select(static pair => pair.OpenXmlPart)
            .OfType<ChartPart>()
            .Single();
        Assert.IsNotNull(chartPart.ChartSpace?.Descendants<C.LineChart>().SingleOrDefault());
        var formulas = chartPart.ChartSpace?
            .Descendants<C.Formula>()
            .Select(static formula => formula.Text)
            .Where(static text => !string.IsNullOrWhiteSpace(text))
            .ToArray()
            ?? [];
        CollectionAssert.Contains(formulas, "'Dashboard'!$B$1");
        CollectionAssert.Contains(formulas, "'Dashboard'!$A$2:$A$4");
        CollectionAssert.Contains(formulas, "'Dashboard'!$B$2:$B$4");

        var validationErrors = new OpenXmlValidator()
            .Validate(readDocument)
            .Select(error => $"{error.Path?.XPath}: {error.Description}")
            .ToArray();
        Assert.AreEqual(
            0,
            validationErrors.Length,
            string.Join(Environment.NewLine, validationErrors));
    }

    private static long ToEmu(double pixels) =>
        checked((long)Math.Round(
            pixels * EmusPerPixel,
            MidpointRounding.AwayFromZero));
}
