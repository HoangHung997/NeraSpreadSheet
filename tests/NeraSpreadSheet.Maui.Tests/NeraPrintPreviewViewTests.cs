using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Spreadsheet;
using SkiaSharp.Views.Maui.Controls;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class NeraPrintPreviewViewTests
{
    [TestMethod]
    public void ViewTypeExposesTheSharedPreviewContract()
    {
        var type = typeof(NeraPrintPreviewView);

        Assert.IsTrue(typeof(SKCanvasView).IsAssignableFrom(type));
        Assert.IsNotNull(type.GetProperty(nameof(NeraPrintPreviewView.Session)));
        Assert.IsNotNull(type.GetProperty(nameof(NeraPrintPreviewView.Zoom)));
        Assert.IsNotNull(type.GetProperty(nameof(NeraPrintPreviewView.OffsetX)));
        Assert.IsNotNull(type.GetProperty(nameof(NeraPrintPreviewView.OffsetY)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraPrintPreviewView.SetZoom)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraPrintPreviewView.SetColumns)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraPrintPreviewView.ScrollTo)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraPrintPreviewView.ScrollBy)));
        Assert.IsNotNull(type.GetMethod(nameof(NeraPrintPreviewView.TryHitTestPage)));
    }

    [TestMethod]
    public void SharedSessionChangesViewportStateWithoutANativeApp()
    {
        var session = CreatePreviewSession();

        session.SetZoom(0.5d, 100.25d, 75.75d);
        session.SetColumns(2);
        session.SetViewportSize(300d, 200d);
        session.ScrollTo(17.25d, 31.75d);

        Assert.AreEqual(0.5d, session.Zoom, 0.000001d);
        Assert.AreEqual(2, session.Columns);
        Assert.AreEqual(17.25d, session.OffsetX, 0.000001d);
        Assert.AreEqual(31.75d, session.OffsetY, 0.000001d);
    }

    [TestMethod]
    public void SharedSessionHitTestingUsesThePreviewLayout()
    {
        var session = CreatePreviewSession();
        var frame = session.Compose();
        var first = frame.Layout.VisiblePages[0];
        var x = first.BoundsDips.X - frame.Layout.OffsetXDips + 5d;
        var y = first.BoundsDips.Y - frame.Layout.OffsetYDips + 7d;

        Assert.IsTrue(session.TryHitTest(
            x,
            y,
            out var hit,
            out var pagePoint));
        Assert.AreEqual(first.PageNumber, hit.PageNumber);
        Assert.AreEqual(5d, pagePoint.X, 0.000001d);
        Assert.AreEqual(7d, pagePoint.Y, 0.000001d);
    }

    private static SpreadsheetPrintPreviewSession CreatePreviewSession()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var row = 0; row < 30; row++)
        {
            worksheet.Dimensions.SetRowHeight(row, 20d);
            for (var column = 0; column < 6; column++)
            {
                if (row == 0)
                {
                    worksheet.Dimensions.SetColumnWidth(column, 80d);
                }
                worksheet.SetValue(
                    new CellAddress(row, column),
                    $"R{row}C{column}");
            }
        }
        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var plan = SpreadsheetPageLayoutPlanner.CreatePlan(
            snapshot,
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(29, 5)),
            new SpreadsheetPageSetup
            {
                PaperSize = new SpreadsheetPaperSize(4d, 5d),
                Margins = SpreadsheetPageMargins.Narrow,
            });
        var session = new SpreadsheetPrintPreviewSession(
            snapshot,
            plan,
            workbook.Styles,
            previewOptions: new SpreadsheetPrintPreviewOptions
            {
                Zoom = 0.35d,
                Columns = 1,
            });
        session.SetViewportSize(500d, 400d);
        return session;
    }
}
