using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class DynamicArrayStructuralEditingTests
{
    [TestMethod]
    public void InsertRowMovesOwnerAndRematerializesSpillWithUndoRedo()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        var originalOwner = new CellAddress(1, 1);
        session.SetFormula(originalOwner, "=SEQUENCE(2,2)");

        session.Structure.InsertRows(0);

        var movedOwner = new CellAddress(2, 1);
        Assert.AreEqual("=SEQUENCE(2,2)", worksheet.GetFormula(movedOwner));
        Assert.AreEqual(1d, worksheet.GetValue(movedOwner));
        Assert.AreEqual(4d, worksheet.GetValue(new CellAddress(3, 2)));
        Assert.IsNull(worksheet.GetValue(originalOwner));
        Assert.IsTrue(worksheet.TryGetFormulaSpill(
            movedOwner,
            out var movedSpill));
        Assert.AreEqual(movedOwner, movedSpill!.Owner);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual("=SEQUENCE(2,2)", worksheet.GetFormula(originalOwner));
        Assert.AreEqual(4d, worksheet.GetValue(new CellAddress(2, 2)));
        Assert.IsNull(worksheet.GetFormula(movedOwner));
        Assert.IsTrue(worksheet.TryGetFormulaSpill(
            originalOwner,
            out _));

        Assert.IsTrue(session.Redo());
        Assert.AreEqual("=SEQUENCE(2,2)", worksheet.GetFormula(movedOwner));
        Assert.AreEqual(4d, worksheet.GetValue(new CellAddress(3, 2)));
        Assert.IsTrue(worksheet.TryGetFormulaSpill(
            movedOwner,
            out _));
    }

    [TestMethod]
    public void InsertColumnMovesOwnerWithoutMovingDerivedChildrenAsData()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        var owner = new CellAddress(0, 1);
        session.SetFormula(owner, "=SEQUENCE(2,2,10,1)");

        session.Structure.InsertColumns(0);

        var movedOwner = new CellAddress(0, 2);
        Assert.AreEqual("=SEQUENCE(2,2,10,1)", worksheet.GetFormula(movedOwner));
        Assert.AreEqual(10d, worksheet.GetValue(movedOwner));
        Assert.AreEqual(13d, worksheet.GetValue(new CellAddress(1, 3)));
        Assert.IsNull(worksheet.GetValue(owner));
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
    }

    [TestMethod]
    public void DeletingRowInsideDerivedRangeRegeneratesArrayFromOwner()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        var owner = new CellAddress(1, 0);
        session.SetFormula(owner, "=SEQUENCE(3)");

        session.Structure.DeleteRows(2);

        Assert.AreEqual("=SEQUENCE(3)", worksheet.GetFormula(owner));
        Assert.AreEqual(1d, worksheet.GetValue(owner));
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(2, 0)));
        Assert.AreEqual(3d, worksheet.GetValue(new CellAddress(3, 0)));
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
    }

    [TestMethod]
    public void FailedStructuralEditRestoresDerivedSpillImmediately()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1))));
        var session = new SpreadsheetSession(workbook);
        var owner = new CellAddress(5, 0);
        session.SetFormula(owner, "=SEQUENCE(2)");
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(6, 0)));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Structure.DeleteRows(0));

        Assert.AreEqual("=SEQUENCE(2)", worksheet.GetFormula(owner));
        Assert.AreEqual(1d, worksheet.GetValue(owner));
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(6, 0)));
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.IsNotNull(worksheet.AutoFilter);
    }
}
