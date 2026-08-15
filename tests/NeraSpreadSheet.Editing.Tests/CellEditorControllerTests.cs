using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class CellEditorControllerTests
{
    [TestMethod]
    public void BeginEditUsesFormulaTextWhenCellContainsFormula()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.ActiveWorksheet.SetFormula(default, "=1+2");
        var editor = new SpreadsheetCellEditorController(session);
        Assert.AreEqual("=1+2", editor.BeginEdit().InitialText);
    }

    [TestMethod]
    public void CommitFormulaUsesUndoableSessionAndCalculatesValue()
    {
        var session = new SpreadsheetSession(new Workbook());
        var editor = new SpreadsheetCellEditorController(session);
        editor.BeginEdit();
        Assert.IsTrue(editor.Commit("=1+2*3"));
        var cell = session.ActiveWorksheet.GetCell(default);
        Assert.AreEqual("=1+2*3", cell.Formula);
        Assert.AreEqual(7d, cell.Value.RawValue);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(session.ActiveWorksheet.GetCell(default).IsEmpty);
    }

    [TestMethod]
    public void CommitLiteralInfersNumberAndBooleanButKeepsText()
    {
        var session = new SpreadsheetSession(new Workbook());
        var editor = new SpreadsheetCellEditorController(session);
        editor.BeginEdit(new CellAddress(0, 0)); editor.Commit("12.5");
        editor.BeginEdit(new CellAddress(0, 1)); editor.Commit("true");
        editor.BeginEdit(new CellAddress(0, 2)); editor.Commit("Nera");
        Assert.AreEqual(CellValueKind.Number, session.ActiveWorksheet.GetCell(new CellAddress(0, 0)).Value.Kind);
        Assert.AreEqual(CellValueKind.Boolean, session.ActiveWorksheet.GetCell(new CellAddress(0, 1)).Value.Kind);
        Assert.AreEqual("Nera", session.ActiveWorksheet.GetCell(new CellAddress(0, 2)).Value.RawValue);
    }

    [TestMethod]
    public void CancelDoesNotMutateCell()
    {
        var session = new SpreadsheetSession(new Workbook());
        var editor = new SpreadsheetCellEditorController(session);
        editor.BeginEdit();
        Assert.IsTrue(editor.Cancel());
        Assert.IsTrue(session.ActiveWorksheet.GetCell(default).IsEmpty);
    }
}
