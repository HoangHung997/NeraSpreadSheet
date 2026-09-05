using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class DynamicArraySpreadsheetSessionTests
{
    [TestMethod]
    public void SetFormulaUndoAndRedoRestoreCompleteSpill()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        var owner = new CellAddress(0, 0);

        session.SetFormula(owner, "=SEQUENCE(2,2)");

        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual(4d, worksheet.GetValue(new CellAddress(1, 1)));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, worksheet.GetFormulaSpillCount());
        Assert.IsTrue(worksheet.GetCell(owner).IsEmpty);
        Assert.IsNull(worksheet.GetValue(new CellAddress(0, 1)));
        Assert.IsNull(worksheet.GetValue(new CellAddress(1, 0)));
        Assert.IsNull(worksheet.GetValue(new CellAddress(1, 1)));

        Assert.IsTrue(session.Redo());
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual("=SEQUENCE(2,2)", worksheet.GetFormula(owner));
        Assert.AreEqual(1d, worksheet.GetValue(owner));
        Assert.AreEqual(4d, worksheet.GetValue(new CellAddress(1, 1)));
    }

    [TestMethod]
    public void DirectSpillChildEditIsRejectedBeforeMutation()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        session.SetFormula(new CellAddress(0, 0), "=SEQUENCE(1,2)");
        var child = new CellAddress(0, 1);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.SetValue(child, 99d));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.SetFormula(child, "=99"));

        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual(2d, worksheet.GetValue(child));
        Assert.AreEqual(1d, worksheet.GetValue(new CellAddress(0, 0)));
    }

    [TestMethod]
    public void ClearingOnlyAChildIsRejectedAndLeavesSpillIntact()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        session.SetFormula(new CellAddress(0, 0), "=SEQUENCE(2,2)");
        var child = new CellAddress(1, 1);
        session.Selection.Select(new CellRange(child, child));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.ClearSelection());

        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual(4d, worksheet.GetValue(child));
    }

    [TestMethod]
    public void ClearingOwnerClearsWholeSpillAndUndoRestoresIt()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        var owner = new CellAddress(0, 0);
        session.SetFormula(owner, "=SEQUENCE(3)");
        session.Selection.Select(new CellRange(owner, owner));

        Assert.IsTrue(session.ClearSelection());
        Assert.AreEqual(0, worksheet.GetFormulaSpillCount());
        Assert.IsNull(worksheet.GetValue(owner));
        Assert.IsNull(worksheet.GetValue(new CellAddress(1, 0)));
        Assert.IsNull(worksheet.GetValue(new CellAddress(2, 0)));

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual("=SEQUENCE(3)", worksheet.GetFormula(owner));
        Assert.AreEqual(3d, worksheet.GetValue(new CellAddress(2, 0)));
    }

    [TestMethod]
    public void SourceEditResizesSpillThroughAffectedRecalculation()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        var source = new CellAddress(0, 0);
        worksheet.SetValue(source, 2d);
        session.SetFormula(new CellAddress(0, 1), "=SEQUENCE(A1)");
        Assert.IsNull(worksheet.GetValue(new CellAddress(2, 1)));

        session.SetValue(source, 3d);

        Assert.AreEqual(1d, worksheet.GetValue(new CellAddress(0, 1)));
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(1, 1)));
        Assert.AreEqual(3d, worksheet.GetValue(new CellAddress(2, 1)));
    }
}
