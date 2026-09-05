using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class MergedCellRenderingTests
{
    [TestMethod]
    public void ComposeDrawsMergedCellTextOnlyOnce()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "Merged title");
        sheet.MergeCells(new CellRange(default, new CellAddress(1, 2)));
        var rows = new SparseAxisMetricIndex(20, 20d);
        var columns = new SparseAxisMetricIndex(20, 80d);
        var layout = new ViewportLayoutEngine(rows, columns).Compute(
            new ViewportRequest(0d, 0d, new SizeD(400d, 200d), 0d));

        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(sheet),
            layout,
            styles: workbook.Styles);

        Assert.AreEqual(
            1,
            displayList.Commands.OfType<DrawTextCommand>().Count(command => command.Text == "Merged title"));
    }
}
