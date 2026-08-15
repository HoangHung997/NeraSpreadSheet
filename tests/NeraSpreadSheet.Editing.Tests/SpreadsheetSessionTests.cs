using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetSessionTests
{
    [TestMethod]
    public void SetFormulaCalculatesAndUndoRestoresPreviousCell()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), 5d);
        var session = new SpreadsheetSession(workbook);

        session.SetFormula(new CellAddress(0, 1), "=A1*3");

        Assert.AreEqual(15d, sheet.GetCell(new CellAddress(0, 1)).Value.RawValue);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(sheet.GetCell(new CellAddress(0, 1)).IsEmpty);
    }

    [TestMethod]
    public void ClearSelectionCanBeUndone()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "A");
        sheet.SetValue(new CellAddress(0, 1), "B");
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(new CellAddress(0, 0), new CellAddress(0, 1)));

        Assert.IsTrue(session.ClearSelection());
        Assert.AreEqual(0, sheet.UsedCellCount);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(2, sheet.UsedCellCount);
    }

    [TestMethod]
    public async Task NativeCommandCatalogClearsAndUndoesCells()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "Nera");
        var session = new SpreadsheetSession(workbook);
        var registry = new CommandRegistry();
        SpreadsheetCommandCatalog.Register(registry, session);
        var dispatcher = new CommandDispatcher(registry);

        Assert.IsTrue(await dispatcher.TryExecuteAsync(SpreadsheetCommandIds.ClearContents));
        Assert.IsTrue(sheet.GetCell(default).IsEmpty);
        Assert.IsTrue(await dispatcher.TryExecuteAsync(SpreadsheetCommandIds.Undo));
        Assert.AreEqual("Nera", sheet.GetCell(default).Value.RawValue);
    }
}
