using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Rendering.Spreadsheet.Tests;

[TestClass]
public sealed class FrozenPaneRenderingTests
{
    [TestMethod]
    public void ComposeUsesPaneClipsAndFreezeSeparators()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(1, 1), "scrolling");
        var rows = new SparseAxisMetricIndex(100, 20d);
        var columns = new SparseAxisMetricIndex(20, 80d);
        var layout = new ViewportLayoutEngine(rows, columns).Compute(new ViewportRequest(
            13.25d, 7.75d, new SizeD(320d, 200d), 0d, FrozenRows: 1, FrozenColumns: 1));
        var selection = new SelectionModel();
        selection.SetActiveCell(new CellAddress(1, 1));
        var theme = new SpreadsheetRenderTheme();

        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(sheet), layout, selection.Capture(), theme, workbook.Styles);

        var clips = displayList.Commands.OfType<PushClipCommand>().ToArray();
        Assert.IsTrue(clips.Any(command => command.Bounds == new RectD(0d, 0d, 80d, 20d)));
        Assert.IsTrue(clips.Any(command => command.Bounds == new RectD(80d, 20d, 240d, 180d)));
        Assert.IsTrue(displayList.Commands.OfType<DrawLineCommand>().Any(command =>
            command.Color == theme.FreezePaneLine && Math.Abs(command.Start.X - 80d) <= 1e-9 && Math.Abs(command.End.X - 80d) <= 1e-9));
        Assert.IsTrue(displayList.Commands.OfType<DrawLineCommand>().Any(command =>
            command.Color == theme.FreezePaneLine && Math.Abs(command.Start.Y - 20d) <= 1e-9 && Math.Abs(command.End.Y - 20d) <= 1e-9));
    }

    [TestMethod]
    public void ScrollingTextCanMoveUnderFrozenPaneAndIsClippedByPane()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(1, 1), "under pane");
        var rows = new SparseAxisMetricIndex(100, 20d);
        var columns = new SparseAxisMetricIndex(20, 80d);
        var layout = new ViewportLayoutEngine(rows, columns).Compute(new ViewportRequest(
            13.25d, 7.75d, new SizeD(320d, 200d), 0d, FrozenRows: 1, FrozenColumns: 1));

        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(sheet), layout, theme: new SpreadsheetRenderTheme(), styles: workbook.Styles);
        var text = displayList.Commands.OfType<DrawTextCommand>().Single(command => command.Text == "under pane");

        Assert.IsTrue(text.Bounds.X < layout.FrozenWidth);
        Assert.IsTrue(text.Bounds.Y < layout.FrozenHeight);
        Assert.IsTrue(displayList.Commands.OfType<PushClipCommand>().Any(command =>
            command.Bounds.Left == layout.FrozenWidth && command.Bounds.Top == layout.FrozenHeight));
    }
}
