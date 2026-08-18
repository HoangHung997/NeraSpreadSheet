using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetAxisStyleRenderingTests
{
    [TestMethod]
    public void ComposeRendersFillForBlankCellInSparseFormattedRow()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var fill = new ColorRgba(25, 145, 205);
        session.Selection.SelectRow(1);
        session.Styles.SetFill(fill);
        var worksheet = session.ActiveWorksheet;
        var rows = new SparseAxisMetricIndex(100, 20d);
        var columns = new SparseAxisMetricIndex(20, 80d);
        var layout = new ViewportLayoutEngine(rows, columns).Compute(
            new ViewportRequest(
                0d,
                0d,
                new SizeD(320d, 100d),
                0d));

        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(worksheet),
            layout,
            styles: workbook.Styles);

        Assert.AreEqual(0, worksheet.UsedCellCount);
        Assert.IsTrue(displayList.Commands
            .OfType<FillRectangleCommand>()
            .Any(command =>
                command.Color == fill &&
                Math.Abs(command.Bounds.X - 160d) < 1e-9 &&
                Math.Abs(command.Bounds.Y - 20d) < 1e-9 &&
                Math.Abs(command.Bounds.Width - 80d) < 1e-9 &&
                Math.Abs(command.Bounds.Height - 20d) < 1e-9));
    }

    [TestMethod]
    public void ComposeUsesLaterColumnStyleAtRowColumnIntersection()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var rowFill = new ColorRgba(220, 70, 35);
        var columnFill = new ColorRgba(50, 95, 220);
        session.Selection.SelectRow(1);
        session.Styles.SetFill(rowFill);
        session.Selection.SelectColumn(2);
        session.Styles.SetFill(columnFill);
        var rows = new SparseAxisMetricIndex(100, 20d);
        var columns = new SparseAxisMetricIndex(20, 80d);
        var layout = new ViewportLayoutEngine(rows, columns).Compute(
            new ViewportRequest(
                0d,
                0d,
                new SizeD(320d, 100d),
                0d));

        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(session.ActiveWorksheet),
            layout,
            styles: workbook.Styles);

        Assert.IsTrue(displayList.Commands
            .OfType<FillRectangleCommand>()
            .Any(command =>
                command.Color == columnFill &&
                Math.Abs(command.Bounds.X - 160d) < 1e-9 &&
                Math.Abs(command.Bounds.Y - 20d) < 1e-9));
    }
}
