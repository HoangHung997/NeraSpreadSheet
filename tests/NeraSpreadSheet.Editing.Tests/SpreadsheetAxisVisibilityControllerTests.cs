using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetAxisVisibilityControllerTests
{
    [TestMethod]
    public void HideUnhideUndoAndRedoShouldPreserveCustomDimensions()
    {
        var session = new SpreadsheetSession(new Workbook());
        var dimensions = session.ActiveWorksheet.Dimensions;
        dimensions.SetColumnWidth(2, 135d);

        session.AxisVisibility.HideColumns(1, 3);
        Assert.IsTrue(dimensions.IsColumnHidden(2));
        Assert.AreEqual("Ẩn cột", session.History.NextUndoDescription);

        Assert.IsTrue(session.Undo());
        Assert.IsFalse(dimensions.IsColumnHidden(2));
        Assert.AreEqual(135d, dimensions.GetColumnWidth(2), 1e-9);

        Assert.IsTrue(session.Redo());
        Assert.IsTrue(dimensions.IsColumnHidden(2));

        session.AxisVisibility.UnhideColumns(1, 3);
        Assert.IsFalse(dimensions.IsColumnHidden(2));
        Assert.AreEqual(135d, dimensions.GetColumnWidth(2), 1e-9);
    }

    [TestMethod]
    public async Task VisibilityCommandsShouldUseWholeAxisSelectionAndExposeUnhideState()
    {
        var session = new SpreadsheetSession(new Workbook());
        session.Selection.Select(new CellRange(
            new CellAddress(0, 2),
            new CellAddress(SpreadsheetLimits.MaxRows - 1, 4)));

        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(
            SpreadsheetStructureCommandIds.HideColumns));
        Assert.IsTrue(session.ActiveWorksheet.Dimensions.IsColumnHidden(2));
        Assert.IsTrue(session.ActiveWorksheet.Dimensions.IsColumnHidden(4));
        Assert.IsTrue(session.CommandDispatcher.QueryState(
            SpreadsheetStructureCommandIds.UnhideColumns).IsEnabled);

        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(
            SpreadsheetStructureCommandIds.UnhideColumns));
        Assert.IsFalse(session.ActiveWorksheet.Dimensions.IsColumnHidden(2));
        Assert.IsFalse(session.ActiveWorksheet.Dimensions.IsColumnHidden(4));
    }

    [TestMethod]
    public void VisibleNavigationShouldJumpAcrossSparseHiddenRowsAndColumns()
    {
        var sheet = new Workbook().Worksheets[0];
        sheet.Dimensions.HideRows(7, 100_000);
        sheet.Dimensions.HideColumns(3, 5);

        Assert.AreEqual(
            new CellAddress(100_007, 2),
            SpreadsheetVisibleCellNavigation.GetNextVisibleCell(
                sheet,
                new CellAddress(6, 2),
                1,
                0));
        Assert.AreEqual(
            new CellAddress(6, 8),
            SpreadsheetVisibleCellNavigation.GetNextVisibleCell(
                sheet,
                new CellAddress(6, 2),
                0,
                1));
        Assert.AreEqual(
            new CellAddress(6, 2),
            SpreadsheetVisibleCellNavigation.GetNextVisibleCell(
                sheet,
                new CellAddress(6, 8),
                0,
                -1));
    }

    [TestMethod]
    public void StructuralAndReorderOperationsShouldPreserveHiddenAxisIdentity()
    {
        var session = new SpreadsheetSession(new Workbook());
        var dimensions = session.ActiveWorksheet.Dimensions;
        dimensions.HideRows(4, 3);

        session.Structure.InsertRows(2, 2);
        Assert.AreEqual(
            new WorksheetAxisInterval(6, 8),
            dimensions.GetHiddenRowRanges()[0]);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(
            new WorksheetAxisInterval(4, 6),
            dimensions.GetHiddenRowRanges()[0]);

        Assert.IsTrue(session.Reorder.MoveRows(4, 3, 10));
        Assert.AreEqual(
            new WorksheetAxisInterval(7, 9),
            dimensions.GetHiddenRowRanges()[0]);
    }
}
