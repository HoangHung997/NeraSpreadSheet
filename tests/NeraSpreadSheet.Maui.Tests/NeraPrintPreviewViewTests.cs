using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Maui.Tests;

[TestClass]
public sealed class NeraPrintPreviewViewTests
{
    [TestMethod]
    public void ViewHostsSharedSessionAndChangesViewportState()
    {
        var session = CreatePreviewSession();
        using var view = new NeraPrintPreviewView
        {
            Session = session,
        };

        view.SetZoom(0.5d, 100.25d, 75.75d);
        view.SetColumns(2);
        view.ScrollTo(17.25d, 31.75d);

        Assert.AreSame(session, view.Session);
        Assert.AreEqual(0.5d, view.Zoom, 0.000001d);
        Assert.AreEqual(2, session.Columns);
        Assert.AreEqual(17.25d, view.OffsetX, 0.000001d);
        Assert.AreEqual(31.75d, view.OffsetY, 0.000001d);
    }

    [TestMethod]
    public void HitTestingUsesTheSharedPreviewLayout()
    {
        var session = CreatePreviewSession();
        session.SetViewportSize(500d, 400d);
        using var view = new NeraPrintPreviewView
        {
            Session = session,
        };
        var frame = session.Compose();
        var first = frame.Layout.VisiblePages[0];
        var x = first.BoundsDips.X - frame.Layout.OffsetXDips + 5d;
        var y = first.BoundsDips.Y - frame.Layout.OffsetYDips + 7d;

        Assert.IsTrue(view.TryHitTestPage(
            x,
            y,
            out var hit,
            out var pagePoint));
        Assert.AreEqual(first.PageNumber, hit.PageNumber);
        Assert.AreEqual(5d, pagePoint.X, 0.000001d);
        Assert.AreEqual(7d, pagePoint.Y, 0.000001d);
    }

    [TestMethod]
    public void ViewRejectsViewportMutationWithoutSession()
    {
        using var view = new NeraPrintPreviewView();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            view.SetZoom(1d));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            view.ScrollBy(1d, 1d));
    }

    [TestMethod]
    public void DisposeIsIdempotent()
    {
        var view = new NeraPrintPreviewView
        {
            Session = CreatePreviewSession(),
        };

        view.Dispose();
        view.Dispose();
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
