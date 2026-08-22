using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetPrintPreviewSessionOffsetRegressionTests
{
    [TestMethod]
    public void FractionalOffsetInsideContentExtentIsPreserved()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.Dimensions.SetColumnWidth(0, 120d);
        for (var row = 0; row < 200; row++)
        {
            worksheet.Dimensions.SetRowHeight(row, 30d);
            worksheet.SetValue(new CellAddress(row, 0), $"Row {row}");
        }
        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            snapshot,
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(199, 0)),
            new SpreadsheetPageSetup
            {
                PaperSize = new SpreadsheetPaperSize(4d, 4d),
                Margins = new SpreadsheetPageMargins(
                    0.25d,
                    0.25d,
                    0.25d,
                    0.25d),
            });
        var session = new SpreadsheetPrintPreviewSession(
            snapshot,
            plan,
            workbook.Styles,
            previewOptions: new SpreadsheetPrintPreviewOptions
            {
                Zoom = 0.25d,
                OverscanDips = 20d,
                PageGapDips = 12d,
            });
        session.SetViewportSize(400d, 300d);

        session.ScrollTo(0d, 500.25d);
        var frame = session.Compose();

        Assert.AreEqual(500.25d, frame.Layout.OffsetYDips, 0.000001d);
        Assert.IsTrue(frame.Pages.Count > 0);
        Assert.IsTrue(frame.Pages.Count < plan.Pages.Count);
    }
}
