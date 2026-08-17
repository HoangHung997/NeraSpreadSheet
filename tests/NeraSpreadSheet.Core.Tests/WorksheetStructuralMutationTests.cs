using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetStructuralMutationTests
{
    [TestMethod]
    public void InsertRowsMovesCellsDimensionsAndMergedRangesTogether()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.SetValue(new CellAddress(4, 1), "A");
        sheet.SetValue(new CellAddress(7, 2), "B");
        sheet.Dimensions.SetRowHeight(4, 31d);
        sheet.MergeCells(new CellRange(new CellAddress(6, 3), new CellAddress(7, 4)));
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 5,
            count: 2);

        sheet.ApplyStructuralChange(change);

        Assert.AreEqual("A", sheet.GetValue(new CellAddress(4, 1)));
        Assert.AreEqual("B", sheet.GetValue(new CellAddress(9, 2)));
        Assert.AreEqual(31d, sheet.Dimensions.GetRowHeight(4), 1e-9);
        Assert.IsTrue(sheet.MergedCells.TryGetContaining(new CellAddress(8, 3), out var merged));
        Assert.AreEqual(8, merged.Top);
        Assert.AreEqual(9, merged.Bottom);
    }

    [TestMethod]
    public void DeleteColumnsDropsDeletedCellsShiftsOverridesAndShrinksMerge()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.SetValue(new CellAddress(2, 2), "deleted");
        sheet.SetValue(new CellAddress(2, 6), "kept");
        sheet.Dimensions.SetColumnWidth(6, 123d);
        sheet.MergeCells(new CellRange(new CellAddress(4, 1), new CellAddress(5, 5)));
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Delete,
            index: 2,
            count: 2);

        sheet.ApplyStructuralChange(change);

        Assert.IsNull(sheet.GetValue(new CellAddress(2, 2)));
        Assert.AreEqual("kept", sheet.GetValue(new CellAddress(2, 4)));
        Assert.AreEqual(123d, sheet.Dimensions.GetColumnWidth(4), 1e-9);
        Assert.IsTrue(sheet.MergedCells.TryGetContaining(new CellAddress(4, 1), out var merged));
        Assert.AreEqual(1, merged.Left);
        Assert.AreEqual(3, merged.Right);
    }

    [TestMethod]
    public void DeleteThatCollapsesMergeToSingleCellRemovesMerge()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.MergeCells(new CellRange(new CellAddress(1, 1), new CellAddress(1, 2)));
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Delete,
            index: 2,
            count: 1);

        sheet.ApplyStructuralChange(change);

        Assert.AreEqual(0, sheet.MergedCells.Count);
    }

    [TestMethod]
    public void StructuralSnapshotRestoresCellsDimensionsAndMerges()
    {
        var sheet = new Worksheet("Sheet1");
        sheet.SetValue(new CellAddress(3, 2), "Nera");
        sheet.Dimensions.SetRowHeight(3, 44d);
        sheet.MergeCells(new CellRange(new CellAddress(5, 1), new CellAddress(6, 2)));
        var before = sheet.CaptureStructuralState();
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 2,
            count: 3);

        sheet.ApplyStructuralChange(change);
        sheet.RestoreStructuralState(before, change);

        Assert.AreEqual("Nera", sheet.GetValue(new CellAddress(3, 2)));
        Assert.AreEqual(44d, sheet.Dimensions.GetRowHeight(3), 1e-9);
        Assert.IsTrue(sheet.MergedCells.TryGetContaining(new CellAddress(5, 1), out var merged));
        Assert.AreEqual(new CellRange(new CellAddress(5, 1), new CellAddress(6, 2)), merged);
    }

    [TestMethod]
    public void InsertOverflowIsAtomicWhenUsedCellWouldFallOffSheet()
    {
        var sheet = new Worksheet("Sheet1");
        var address = new CellAddress(SpreadsheetLimits.MaxRows - 1, 1);
        sheet.SetValue(address, "edge");
        sheet.Dimensions.SetRowHeight(2, 42d);
        sheet.MergeCells(new CellRange(new CellAddress(4, 1), new CellAddress(5, 1)));
        var beforeVersion = sheet.Version;
        var beforeDimensionVersion = sheet.Dimensions.Version;
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: SpreadsheetLimits.MaxRows - 2,
            count: 1);

        Assert.ThrowsExactly<InvalidOperationException>(() => sheet.ApplyStructuralChange(change));

        Assert.AreEqual("edge", sheet.GetValue(address));
        Assert.AreEqual(42d, sheet.Dimensions.GetRowHeight(2), 1e-9);
        Assert.AreEqual(1, sheet.MergedCells.Count);
        Assert.AreEqual(beforeVersion, sheet.Version);
        Assert.AreEqual(beforeDimensionVersion, sheet.Dimensions.Version);
    }
}
