using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class StyleControllerTests
{
    [TestMethod]
    public void ToggleBoldAppliesInternedStyleAndIsUndoable()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var styles = new SpreadsheetStyleController(session);
        styles.ToggleBold();
        var cell = workbook.Worksheets[0].GetCell(default);
        Assert.AreNotEqual(0, cell.StyleId);
        Assert.AreEqual(700, workbook.Styles.Get(cell.StyleId).Font.Weight);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, workbook.Worksheets[0].GetCell(default).StyleId);
    }

    [TestMethod]
    public void SetFillFormatsBlankCellsWithoutLosingSparseSemanticsAfterUndo()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(default, new CellAddress(1, 1)));
        var styles = new SpreadsheetStyleController(session);
        styles.SetFill(new ColorRgba(200, 220, 240));
        Assert.AreEqual(4, workbook.Worksheets[0].UsedCellCount);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, workbook.Worksheets[0].UsedCellCount);
    }

    [TestMethod]
    public async Task FormattingCommandStateReflectsActiveCellStyle()
    {
        var session = new SpreadsheetSession(new Workbook());
        var styles = new SpreadsheetStyleController(session);
        var registry = new CommandRegistry();
        SpreadsheetFormattingCommandCatalog.Register(registry, styles);
        var dispatcher = new CommandDispatcher(registry);
        Assert.AreEqual(false, dispatcher.QueryState(SpreadsheetFormattingCommandIds.Bold).IsChecked);
        Assert.IsTrue(await dispatcher.TryExecuteAsync(SpreadsheetFormattingCommandIds.Bold));
        Assert.AreEqual(true, dispatcher.QueryState(SpreadsheetFormattingCommandIds.Bold).IsChecked);
    }
}
