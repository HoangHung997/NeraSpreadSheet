using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using A = DocumentFormat.OpenXml.Drawing;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;

namespace NeraSpreadSheet.OpenXml.Tests;

[TestClass]
public sealed class AnalyticsSessionForeignDrawingPreservationTests
{
    private const string ForeignDescription = "ThirdParty:Keep";
    private const string ManagedDescriptionPrefix = "NeraSpreadSheet:Chart:";

    [TestMethod]
    public async Task SaveLoadSavePreservesForeignDrawingWhileReplacingManagedChart()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        workbook.RenameWorksheet(worksheet, "Dashboard");
        worksheet.SetValue(new CellAddress(0, 0), "Category");
        worksheet.SetValue(new CellAddress(0, 1), "Value");
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "B");
        worksheet.SetValue(new CellAddress(2, 1), 20d);

        var session = new SpreadsheetSession(workbook);
        var chart = session.Analytics.InsertChart(
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 1)),
            SpreadsheetChartType.Column,
            title: "Managed",
            requestedName: "ManagedChart");
        Assert.IsTrue(session.AnalyticsPlacements.SetBounds(
            SpreadsheetAnalyticsItemKey.ForChart(chart.Id),
            new RectD(20d, 30d, 320d, 200d)));

        var serializer = new NeraOpenXmlSpreadsheetSessionSerializer();
        await using var first = new MemoryStream();
        await serializer.SaveSessionAsync(
            session,
            first,
            new OpenXmlExportOptions());

        AddForeignShape(first);
        AssertDrawingState(
            first,
            chart.Id,
            expectedManagedAnchors: 1,
            expectedForeignAnchors: 1,
            expectedChartParts: 1);

        first.Position = 0L;
        var loaded = await serializer.LoadSessionAsync(
            first,
            new OpenXmlImportOptions());
        Assert.AreEqual(chart.Id, loaded.Analytics.GetCharts(loaded.ActiveWorksheet).Single().Id);

        await using var second = new MemoryStream();
        await serializer.SaveSessionAsync(
            loaded,
            second,
            new OpenXmlExportOptions());

        AssertDrawingState(
            second,
            chart.Id,
            expectedManagedAnchors: 1,
            expectedForeignAnchors: 1,
            expectedChartParts: 1);
    }

    private static void AddForeignShape(MemoryStream stream)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, true);
        var worksheetPart = document.WorkbookPart?.WorksheetParts.Single()
            ?? throw new AssertFailedException("The package has no worksheet part.");
        var drawingsPart = worksheetPart.DrawingsPart
            ?? throw new AssertFailedException("The managed chart did not create a drawings part.");
        var worksheetDrawing = drawingsPart.WorksheetDrawing
            ?? throw new AssertFailedException("The drawings part has no worksheet drawing markup.");

        var shape = new Xdr.Shape(
            new Xdr.NonVisualShapeProperties(
                new Xdr.NonVisualDrawingProperties
                {
                    Id = 500U,
                    Name = "ThirdPartyShape",
                    Description = ForeignDescription,
                },
                new Xdr.NonVisualShapeDrawingProperties()),
            new Xdr.ShapeProperties(
                new A.PresetGeometry(new A.AdjustValueList())
                {
                    Preset = A.ShapeTypeValues.Rectangle,
                }),
            new Xdr.TextBody(
                new A.BodyProperties(),
                new A.ListStyle(),
                new A.Paragraph(
                    new A.Run(
                        new A.RunProperties { Language = "en-US" },
                        new A.Text("Keep me")),
                    new A.EndParagraphRunProperties { Language = "en-US" })));

        worksheetDrawing.Append(
            new Xdr.AbsoluteAnchor(
                new Xdr.Position { X = 500_000L, Y = 500_000L },
                new Xdr.Extent { Cx = 1_000_000L, Cy = 500_000L },
                shape,
                new Xdr.ClientData()));
        worksheetDrawing.Save();
        AssertSchemaValid(document);
    }

    private static void AssertDrawingState(
        MemoryStream stream,
        Guid chartId,
        int expectedManagedAnchors,
        int expectedForeignAnchors,
        int expectedChartParts)
    {
        stream.Position = 0L;
        using var document = SpreadsheetDocument.Open(stream, false);
        var drawingsPart = document.WorkbookPart?.WorksheetParts.Single().DrawingsPart
            ?? throw new AssertFailedException("The drawings part was not preserved.");
        var anchors = drawingsPart.WorksheetDrawing?
            .ChildElements
            .OfType<DocumentFormat.OpenXml.OpenXmlCompositeElement>()
            .ToArray()
            ?? [];

        Assert.AreEqual(
            expectedManagedAnchors,
            anchors.Count(anchor => anchor
                .Descendants<Xdr.NonVisualDrawingProperties>()
                .Any(properties => string.Equals(
                    properties.Description?.Value,
                    ManagedDescriptionPrefix + chartId.ToString("N"),
                    StringComparison.Ordinal))));
        Assert.AreEqual(
            expectedForeignAnchors,
            anchors.Count(anchor => anchor
                .Descendants<Xdr.NonVisualDrawingProperties>()
                .Any(properties => string.Equals(
                    properties.Description?.Value,
                    ForeignDescription,
                    StringComparison.Ordinal))));
        Assert.AreEqual(
            expectedChartParts,
            drawingsPart.Parts.Count(pair => pair.OpenXmlPart is ChartPart));
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
