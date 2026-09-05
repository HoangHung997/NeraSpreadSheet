using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class DynamicArrayClipboardTests
{
    [TestMethod]
    public void PartialSpillCopyAndCutAreRejectedWithoutHistory()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetFormula(new CellAddress(0, 0), "=SEQUENCE(2,2)");
        var session = new SpreadsheetSession(workbook);
        session.Recalculate();
        session.Selection.Select(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(0, 1)));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Clipboard.CopyPrimarySelection());
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Clipboard.CutPrimarySelection());

        Assert.IsNull(session.Clipboard.Clipboard);
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.AreEqual(4d, worksheet.GetValue(new CellAddress(1, 1)));
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void CompleteSpillCopyStoresOnlyOwnerFormulaAndDirectChildStyle()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var owner = new CellAddress(0, 0);
        var styledChild = new CellAddress(1, 1);
        var styleId = workbook.Styles.Intern(new CellStyle
        {
            Alignment = new CellAlignmentStyle
            {
                WrapText = true,
            },
        });
        worksheet.SetStyle(styledChild, styleId);
        worksheet.SetFormula(owner, "=SEQUENCE(2,2)");
        var session = new SpreadsheetSession(workbook);
        session.Recalculate();
        session.Selection.Select(new CellRange(owner, styledChild));

        var package = session.Clipboard.CopyPrimarySelection();

        Assert.AreEqual(2, package.RowCount);
        Assert.AreEqual(2, package.ColumnCount);
        Assert.AreEqual(2, package.UsedCellCount);
        Assert.AreEqual("=SEQUENCE(2,2)", package.GetCell(0, 0).Formula);
        Assert.AreEqual(1d, package.GetCell(0, 0).Value.RawValue);
        Assert.IsTrue(package.GetCell(0, 1).IsEmpty);
        Assert.IsTrue(package.GetCell(1, 0).IsEmpty);
        var copiedStyle = package.GetCell(1, 1);
        Assert.IsTrue(copiedStyle.Value.IsBlank);
        Assert.IsNull(copiedStyle.Formula);
        Assert.AreEqual(styleId, copiedStyle.StyleId);
    }

    [TestMethod]
    public void CompleteSpillCopyPastesAsOneNewDynamicArray()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var sourceOwner = new CellAddress(0, 0);
        worksheet.SetFormula(sourceOwner, "=SEQUENCE(2,2,10,1)");
        var session = new SpreadsheetSession(workbook);
        session.Recalculate();
        session.Selection.Select(new CellRange(
            sourceOwner,
            new CellAddress(1, 1)));
        session.Clipboard.CopyPrimarySelection();
        var destination = new CellAddress(0, 3);

        Assert.IsTrue(session.Clipboard.Paste(destination));

        Assert.AreEqual("=SEQUENCE(2,2,10,1)", worksheet.GetFormula(destination));
        Assert.AreEqual(10d, worksheet.GetValue(destination));
        Assert.AreEqual(11d, worksheet.GetValue(new CellAddress(0, 4)));
        Assert.AreEqual(12d, worksheet.GetValue(new CellAddress(1, 3)));
        Assert.AreEqual(13d, worksheet.GetValue(new CellAddress(1, 4)));
        Assert.AreEqual(2, worksheet.GetFormulaSpillCount());
        Assert.IsTrue(worksheet.TryGetFormulaSpill(destination, out _));
    }

    [TestMethod]
    public void PasteIntersectingExistingSpillIsRejectedBeforeMutationAndHistory()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var owner = new CellAddress(1, 1);
        worksheet.SetFormula(owner, "=SEQUENCE(2,2)");
        var session = new SpreadsheetSession(workbook);
        session.Recalculate();
        session.Clipboard.ImportTabSeparatedText("1\t2\r\n3\t4");
        var ownerBefore = worksheet.GetCell(owner);
        var childBefore = worksheet.GetCell(new CellAddress(2, 2));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Clipboard.Paste(new CellAddress(0, 0)));

        Assert.AreEqual(ownerBefore, worksheet.GetCell(owner));
        Assert.AreEqual(childBefore, worksheet.GetCell(new CellAddress(2, 2)));
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
        Assert.IsFalse(session.Undo());
    }

    [TestMethod]
    public void CompleteSpillCutClearsDerivedRangeAndUndoRestoresIt()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var owner = new CellAddress(0, 0);
        worksheet.SetFormula(owner, "=SEQUENCE(3)");
        var session = new SpreadsheetSession(workbook);
        session.Recalculate();
        session.Selection.Select(new CellRange(
            owner,
            new CellAddress(2, 0)));

        Assert.IsTrue(session.Clipboard.CutPrimarySelection());
        Assert.AreEqual(0, worksheet.GetFormulaSpillCount());
        Assert.IsTrue(worksheet.GetCell(owner).IsEmpty);
        Assert.IsNull(worksheet.GetValue(new CellAddress(1, 0)));
        Assert.IsNull(worksheet.GetValue(new CellAddress(2, 0)));

        Assert.IsTrue(session.Undo());
        Assert.AreEqual("=SEQUENCE(3)", worksheet.GetFormula(owner));
        Assert.AreEqual(1d, worksheet.GetValue(owner));
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(1, 0)));
        Assert.AreEqual(3d, worksheet.GetValue(new CellAddress(2, 0)));
        Assert.AreEqual(1, worksheet.GetFormulaSpillCount());
    }

    [TestMethod]
    public void OrdinaryClipboardBehaviorRemainsAvailableOutsideSpills()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "source");
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(0, 0)));
        session.Clipboard.CopyPrimarySelection();

        Assert.IsTrue(session.Clipboard.Paste(new CellAddress(4, 4)));
        Assert.AreEqual("source", worksheet.GetValue(new CellAddress(4, 4)));
    }
}
