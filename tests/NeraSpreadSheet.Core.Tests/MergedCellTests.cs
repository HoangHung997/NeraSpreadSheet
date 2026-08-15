using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class MergedCellTests
{
    [TestMethod]
    public void MergeClearsNonAnchorCellsAndResolvesAnchor()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var range = new CellRange(new CellAddress(1, 1), new CellAddress(2, 2));
        sheet.SetValue(new CellAddress(1, 1), "anchor");
        sheet.SetValue(new CellAddress(2, 2), "discarded");

        sheet.MergeCells(range);

        Assert.AreEqual(1, sheet.MergedCells.Count);
        Assert.AreEqual(new CellAddress(1, 1), sheet.ResolveMergedAnchor(new CellAddress(2, 2)));
        Assert.AreEqual("anchor", sheet.GetCell(new CellAddress(1, 1)).Value.RawValue);
        Assert.IsTrue(sheet.GetCell(new CellAddress(2, 2)).IsEmpty);
    }

    [TestMethod]
    public void MergeRejectsOverlappingRange()
    {
        var sheet = new Workbook().Worksheets[0];
        sheet.MergeCells(new CellRange(new CellAddress(0, 0), new CellAddress(1, 1)));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            sheet.MergeCells(new CellRange(new CellAddress(1, 1), new CellAddress(2, 2))));
    }

    [TestMethod]
    public void UnmergeRemovesRange()
    {
        var sheet = new Workbook().Worksheets[0];
        var range = new CellRange(new CellAddress(0, 0), new CellAddress(0, 2));
        sheet.MergeCells(range);

        Assert.IsTrue(sheet.UnmergeCell(new CellAddress(0, 1)));
        Assert.AreEqual(0, sheet.MergedCells.Count);
    }
}
