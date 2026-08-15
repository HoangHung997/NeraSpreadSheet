using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class SpreadsheetDisplayListComposerTests
{
    [TestMethod]
    public void ComposeIncludesTextAndSelectionCommandsForVisibleCell()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Nera");
        var rows = new SparseAxisMetricIndex(100, 20d);
        var columns = new SparseAxisMetricIndex(20, 80d);
        var layout = new ViewportLayoutEngine(rows, columns).Compute(new ViewportRequest(0d, 0d, new SizeD(320d, 200d), 0d));
        var selection = new SelectionModel();
        selection.SetActiveCell(new CellAddress(0, 0));
        var displayList = new SpreadsheetDisplayListComposer().Compose(WorksheetSnapshot.Capture(sheet), layout, selection.Capture());
        Assert.IsTrue(displayList.Commands.OfType<DrawTextCommand>().Any(command => command.Text == "Nera"));
        Assert.IsTrue(displayList.Commands.OfType<DrawLineCommand>().Count() >= 4);
    }
}
