using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetAxisReorderControllerTests
{
    [TestMethod]
    public void MoveRowsPreservesIdentityAcrossDataFormulaSelectionSplitAndHistory()
    {
        var workbook = new Workbook();
        var data = workbook.Worksheets[0];
        data.Rename("Data");
        var calc = workbook.AddWorksheet("Calc");
        data.SetValue(new CellAddress(1, 0), "A");
        data.SetValue(new CellAddress(2, 0), "B");
        data.SetValue(new CellAddress(3, 0), "C");
        data.SetFormula(new CellAddress(0, 1), "=A2");
        calc.SetFormula(default, "=Data!A2");
        data.Dimensions.SetRowHeight(1, 31d);
        data.Dimensions.SetRowHeight(2, 42d);
        var session = new SpreadsheetSession(workbook, data);
        session.Selection.SelectRow(1);
        var splitBefore = new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Both,
            300d,
            180d,
            topLeftScroll: new SpreadsheetPaneScrollOffset(0d, 25d),
            topRightScroll: new SpreadsheetPaneScrollOffset(15d, 10d),
            bottomLeftScroll: new SpreadsheetPaneScrollOffset(0d, 25d),
            bottomRightScroll: new SpreadsheetPaneScrollOffset(15d, 10d));
        session.View.SetSplitState(splitBefore);

        Assert.IsTrue(session.Reorder.MoveRows(1, 1, 4));

        Assert.AreEqual("B", data.GetValue(new CellAddress(1, 0)));
        Assert.AreEqual("C", data.GetValue(new CellAddress(2, 0)));
        Assert.AreEqual("A", data.GetValue(new CellAddress(3, 0)));
        Assert.AreEqual("=A4", data.GetFormula(new CellAddress(0, 1)));
        Assert.AreEqual("=Data!A4", calc.GetFormula(default));
        Assert.AreEqual(42d, data.Dimensions.GetRowHeight(1));
        Assert.AreEqual(31d, data.Dimensions.GetRowHeight(3));
        Assert.AreEqual(
            new CellRange(
                new CellAddress(3, 0),
                new CellAddress(3, SpreadsheetLimits.MaxColumns - 1)),
            session.Selection.Ranges.Single());
        Assert.AreEqual(
            87d,
            session.View.SplitState.TopLeftScroll.OffsetY,
            0.001d);
        Assert.AreEqual(
            10d,
            session.View.SplitState.TopRightScroll.OffsetY,
            0.001d);
        Assert.AreEqual("Reorder rows", session.History.NextUndoDescription);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual("A", data.GetValue(new CellAddress(1, 0)));
        Assert.AreEqual("=A2", data.GetFormula(new CellAddress(0, 1)));
        Assert.AreEqual("=Data!A2", calc.GetFormula(default));
        Assert.AreEqual(31d, data.Dimensions.GetRowHeight(1));
        Assert.AreEqual(splitBefore, session.View.SplitState);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(1, 0),
                new CellAddress(1, SpreadsheetLimits.MaxColumns - 1)),
            session.Selection.Ranges.Single());

        Assert.IsTrue(session.Redo());
        Assert.AreEqual("A", data.GetValue(new CellAddress(3, 0)));
        Assert.AreEqual("=Data!A4", calc.GetFormula(default));
        Assert.AreEqual(
            87d,
            session.View.SplitState.TopLeftScroll.OffsetY,
            0.001d);
    }

    [TestMethod]
    public void MoveColumnsMapsDimensionsReferencesAndHorizontalScroll()
    {
        var workbook = new Workbook();
        var data = workbook.Worksheets[0];
        data.Rename("Data");
        var calc = workbook.AddWorksheet("Calc");
        data.SetValue(new CellAddress(0, 1), "A");
        data.SetValue(new CellAddress(0, 2), "B");
        data.SetValue(new CellAddress(0, 3), "C");
        calc.SetFormula(default, "=Data!B1");
        data.Dimensions.SetColumnWidth(1, 111d);
        var session = new SpreadsheetSession(workbook, data);
        session.Selection.SelectColumn(1);
        session.View.SetSplitState(new SpreadsheetSplitViewState(
            SpreadsheetSplitViewMode.Vertical,
            260d,
            null,
            SpreadsheetSplitViewPane.TopRight,
            topRightScroll: new SpreadsheetPaneScrollOffset(90d, 0d)));

        Assert.IsTrue(session.Reorder.MoveColumns(1, 1, 4));

        Assert.AreEqual("B", data.GetValue(new CellAddress(0, 1)));
        Assert.AreEqual("C", data.GetValue(new CellAddress(0, 2)));
        Assert.AreEqual("A", data.GetValue(new CellAddress(0, 3)));
        Assert.AreEqual("=Data!D1", calc.GetFormula(default));
        Assert.AreEqual(111d, data.Dimensions.GetColumnWidth(3));
        Assert.AreEqual(
            250d,
            session.View.SplitState.TopRightScroll.OffsetX,
            0.001d);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 3),
                new CellAddress(SpreadsheetLimits.MaxRows - 1, 3)),
            session.Selection.Ranges.Single());
    }

    [TestMethod]
    public void DiscontiguousFormulaRangeRejectsMoveAtomically()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetFormula(default, "=SUM(A3:A4)");
        worksheet.SetValue(new CellAddress(3, 0), "moved");
        var session = new SpreadsheetSession(workbook);
        var version = worksheet.Version;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Reorder.MoveRows(3, 2, 1));

        Assert.AreEqual(version, worksheet.Version);
        Assert.AreEqual("=SUM(A3:A4)", worksheet.GetFormula(default));
        Assert.AreEqual("moved", worksheet.GetValue(new CellAddress(3, 0)));
        Assert.IsFalse(session.History.CanUndo);
    }

    [TestMethod]
    public void MergedRangeCannotBeMovedAcrossFreezeBoundary()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(2, 0), "merged");
        worksheet.MergeCells(new CellRange(
            new CellAddress(2, 0),
            new CellAddress(3, 1)));
        var session = new SpreadsheetSession(workbook);
        session.View.SetFrozenPanes(2, 0);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Reorder.MoveRows(2, 2, 1));

        Assert.AreEqual("merged", worksheet.GetValue(new CellAddress(2, 0)));
        Assert.AreEqual(
            new CellRange(
                new CellAddress(2, 0),
                new CellAddress(3, 1)),
            worksheet.MergedCells.Ranges.Single());
        Assert.IsFalse(session.History.CanUndo);
    }

    [TestMethod]
    public void NoOpMoveDoesNotEnterHistory()
    {
        var session = new SpreadsheetSession(new Workbook());

        Assert.IsFalse(session.Reorder.MoveRows(4, 2, 6));
        Assert.IsFalse(session.History.CanUndo);
    }
}
