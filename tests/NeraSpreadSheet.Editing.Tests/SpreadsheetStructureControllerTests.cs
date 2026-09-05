using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetStructureControllerTests
{
    [TestMethod]
    public void InsertRowsMovesWorksheetStateAndRewritesLocalAndCrossSheetFormulas()
    {
        var workbook = CreateStructuralWorkbook(out var sheet, out var summary);
        var session = new SpreadsheetSession(workbook, sheet);
        session.Selection.SetActiveCell(new CellAddress(4, 1));
        session.View.SetFrozenPanes(3, 1);

        session.Structure.InsertRows(1, 2);

        Assert.AreEqual(10d, sheet.GetValue(new CellAddress(3, 0)));
        Assert.AreEqual(20d, sheet.GetValue(new CellAddress(6, 0)));
        Assert.AreEqual("=A4+A7", sheet.GetFormula(new CellAddress(6, 1)));
        Assert.AreEqual("=Sheet1!A4+Sheet1!A7", summary.GetFormula(default));
        Assert.AreEqual(33d, sheet.Dimensions.GetRowHeight(6), 1e-9);
        Assert.IsTrue(sheet.MergedCells.TryGetContaining(new CellAddress(6, 2), out var merged));
        Assert.AreEqual(6, merged.Top);
        Assert.AreEqual(7, merged.Bottom);
        Assert.AreEqual(new CellAddress(6, 1), session.Selection.ActiveCell);
        Assert.AreEqual(5, session.View.FrozenRows);
        Assert.AreEqual(1, session.View.FrozenColumns);
    }

    [TestMethod]
    public void UndoRestoresStructuralStateCrossSheetFormulasSelectionAndFreeze()
    {
        var workbook = CreateStructuralWorkbook(out var sheet, out var summary);
        var session = new SpreadsheetSession(workbook, sheet);
        session.Selection.SetActiveCell(new CellAddress(4, 1));
        session.Selection.AddRange(new CellRange(new CellAddress(8, 0), new CellAddress(9, 1)));
        var selectionBefore = session.Selection.Capture();
        session.View.SetFrozenPanes(3, 1);

        session.Structure.InsertRows(1, 2);
        Assert.IsTrue(session.Undo());

        Assert.AreEqual(10d, sheet.GetValue(new CellAddress(1, 0)));
        Assert.AreEqual(20d, sheet.GetValue(new CellAddress(4, 0)));
        Assert.AreEqual("=A2+A5", sheet.GetFormula(new CellAddress(4, 1)));
        Assert.AreEqual("=Sheet1!A2+Sheet1!A5", summary.GetFormula(default));
        Assert.AreEqual(33d, sheet.Dimensions.GetRowHeight(4), 1e-9);
        Assert.IsTrue(sheet.MergedCells.TryGetContaining(new CellAddress(4, 2), out var merged));
        Assert.AreEqual(4, merged.Top);
        Assert.AreEqual(5, merged.Bottom);
        Assert.AreEqual(selectionBefore.ActiveCell, session.Selection.ActiveCell);
        Assert.AreEqual(selectionBefore.AnchorCell, session.Selection.AnchorCell);
        CollectionAssert.AreEqual(selectionBefore.Ranges.ToArray(), session.Selection.Ranges.ToArray());
        Assert.AreEqual(3, session.View.FrozenRows);
        Assert.AreEqual(1, session.View.FrozenColumns);
    }

    [TestMethod]
    public void RedoReappliesSameStructuralTransformAfterUndo()
    {
        var workbook = CreateStructuralWorkbook(out var sheet, out var summary);
        var session = new SpreadsheetSession(workbook, sheet);
        session.Selection.SetActiveCell(new CellAddress(4, 1));
        session.View.SetFrozenPanes(3, 1);

        session.Structure.InsertRows(1, 2);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(session.Redo());

        Assert.AreEqual(20d, sheet.GetValue(new CellAddress(6, 0)));
        Assert.AreEqual("=A4+A7", sheet.GetFormula(new CellAddress(6, 1)));
        Assert.AreEqual("=Sheet1!A4+Sheet1!A7", summary.GetFormula(default));
        Assert.AreEqual(5, session.View.FrozenRows);
    }

    [TestMethod]
    public void InsertColumnsMapsWholeColumnSelectionWithoutOverflowingLogicalAxisRange()
    {
        var workbook = new Workbook();
        var session = new SpreadsheetSession(workbook);
        session.Selection.SelectColumn(3);

        session.Structure.InsertColumns(1, 2);

        Assert.AreEqual(new CellAddress(0, 5), session.Selection.ActiveCell);
        Assert.AreEqual(0, session.Selection.Ranges[0].Top);
        Assert.AreEqual(SpreadsheetLimits.MaxRows - 1, session.Selection.Ranges[0].Bottom);
        Assert.AreEqual(5, session.Selection.Ranges[0].Left);
        Assert.AreEqual(5, session.Selection.Ranges[0].Right);
    }

    private static Workbook CreateStructuralWorkbook(out Worksheet sheet, out Worksheet summary)
    {
        var workbook = new Workbook();
        sheet = workbook.Worksheets[0];
        sheet.Rename("Sheet1");
        summary = workbook.AddWorksheet("Summary");

        sheet.SetValue(new CellAddress(1, 0), 10d);
        sheet.SetValue(new CellAddress(4, 0), 20d);
        sheet.SetFormula(new CellAddress(4, 1), "=A2+A5");
        sheet.Dimensions.SetRowHeight(4, 33d);
        sheet.MergeCells(new CellRange(new CellAddress(4, 2), new CellAddress(5, 3)));
        summary.SetFormula(default, "=Sheet1!A2+Sheet1!A5");
        return workbook;
    }
}
