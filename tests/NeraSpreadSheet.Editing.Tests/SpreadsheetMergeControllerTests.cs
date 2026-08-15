using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetMergeControllerTests
{
    [TestMethod]
    public void MergeAndUndoRestoreInteriorCellData()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        var sheet = session.ActiveWorksheet;
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(1, 1));
        sheet.SetValue(new CellAddress(0, 0), "anchor");
        sheet.SetValue(new CellAddress(1, 1), "interior");
        session.Selection.Select(range);

        Assert.IsTrue(session.Merge.MergeSelection());
        Assert.AreEqual(1, sheet.MergedCells.Count);
        Assert.IsTrue(sheet.GetCell(new CellAddress(1, 1)).IsEmpty);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, sheet.MergedCells.Count);
        Assert.AreEqual("interior", sheet.GetCell(new CellAddress(1, 1)).Value.RawValue);

        Assert.IsTrue(session.Redo());
        Assert.AreEqual(1, sheet.MergedCells.Count);
    }

    [TestMethod]
    public void SessionRegistersMergeAndSortCommands()
    {
        var session = new SpreadsheetSession(new Workbook());

        Assert.IsTrue(session.Commands.TryResolve(SpreadsheetMergeCommandIds.MergeCells, out _, out _));
        Assert.IsTrue(session.Commands.TryResolve(SpreadsheetSortCommandIds.SortAscending, out _, out _));
    }
}
