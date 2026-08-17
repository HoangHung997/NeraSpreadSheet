using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetStructureCommandTests
{
    [TestMethod]
    public async Task InsertRowsCommandUsesWholeRowSelectionExtent()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(3, 0), "moved");
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(
            new CellAddress(1, 0),
            new CellAddress(2, SpreadsheetLimits.MaxColumns - 1)));

        var executed = await session.CommandDispatcher.TryExecuteAsync(
            SpreadsheetStructureCommandIds.InsertRows);

        Assert.IsTrue(executed);
        Assert.IsNull(sheet.GetValue(new CellAddress(3, 0)));
        Assert.AreEqual("moved", sheet.GetValue(new CellAddress(5, 0)));
        Assert.AreEqual("Insert rows", session.History.NextUndoDescription);
    }

    [TestMethod]
    public async Task DeleteColumnsCommandUsesWholeColumnSelectionExtent()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 4), "kept");
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(
            new CellAddress(0, 1),
            new CellAddress(SpreadsheetLimits.MaxRows - 1, 2)));

        var executed = await session.CommandDispatcher.TryExecuteAsync(
            SpreadsheetStructureCommandIds.DeleteColumns);

        Assert.IsTrue(executed);
        Assert.AreEqual("kept", sheet.GetValue(new CellAddress(0, 2)));
        Assert.IsNull(sheet.GetValue(new CellAddress(0, 4)));
        Assert.AreEqual("Delete columns", session.History.NextUndoDescription);
    }

    [TestMethod]
    public void SessionRegistersAllStructuralCommandsAsEnabledAtAValidActiveCell()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.SetActiveCell(new CellAddress(2, 3));

        Assert.IsTrue(session.CommandDispatcher.QueryState(SpreadsheetStructureCommandIds.InsertRows).IsEnabled);
        Assert.IsTrue(session.CommandDispatcher.QueryState(SpreadsheetStructureCommandIds.DeleteRows).IsEnabled);
        Assert.IsTrue(session.CommandDispatcher.QueryState(SpreadsheetStructureCommandIds.InsertColumns).IsEnabled);
        Assert.IsTrue(session.CommandDispatcher.QueryState(SpreadsheetStructureCommandIds.DeleteColumns).IsEnabled);
    }
}
