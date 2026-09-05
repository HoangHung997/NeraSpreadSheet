using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetAxisMoveMutationTests
{
    [TestMethod]
    public void RowMoveReordersCellsAndDimensionOverrides()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(2, 0), "B");
        worksheet.SetValue(new CellAddress(3, 0), "C");
        worksheet.Dimensions.SetRowHeight(1, 31d);
        worksheet.Dimensions.SetRowHeight(2, 42d);

        worksheet.ApplyAxisMove(new WorksheetAxisMove(
            WorksheetAxis.Row,
            sourceIndex: 1,
            count: 1,
            destinationBoundary: 4));

        Assert.AreEqual("B", worksheet.GetValue(new CellAddress(1, 0)));
        Assert.AreEqual("C", worksheet.GetValue(new CellAddress(2, 0)));
        Assert.AreEqual("A", worksheet.GetValue(new CellAddress(3, 0)));
        Assert.AreEqual(42d, worksheet.Dimensions.GetRowHeight(1));
        Assert.AreEqual(31d, worksheet.Dimensions.GetRowHeight(3));
    }

    [TestMethod]
    public void MovingCompleteMergedBlockPreservesAnchorAndRange()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(1, 0), "merged");
        worksheet.MergeCells(new CellRange(
            new CellAddress(1, 0),
            new CellAddress(2, 1)));

        worksheet.ApplyAxisMove(new WorksheetAxisMove(
            WorksheetAxis.Row,
            sourceIndex: 1,
            count: 2,
            destinationBoundary: 4));

        Assert.AreEqual("merged", worksheet.GetValue(new CellAddress(2, 0)));
        Assert.AreEqual(
            new CellRange(
                new CellAddress(2, 0),
                new CellAddress(3, 1)),
            worksheet.MergedCells.Ranges.Single());
    }

    [TestMethod]
    public void MoveThatWouldSplitMergedRangeFailsBeforeMutation()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(1, 0), "anchor");
        worksheet.MergeCells(new CellRange(
            new CellAddress(1, 0),
            new CellAddress(2, 1)));
        var version = worksheet.Version;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            worksheet.ApplyAxisMove(new WorksheetAxisMove(
                WorksheetAxis.Row,
                sourceIndex: 1,
                count: 1,
                destinationBoundary: 4)));

        Assert.AreEqual(version, worksheet.Version);
        Assert.AreEqual("anchor", worksheet.GetValue(new CellAddress(1, 0)));
        Assert.AreEqual(
            new CellRange(
                new CellAddress(1, 0),
                new CellAddress(2, 1)),
            worksheet.MergedCells.Ranges.Single());
    }

    [TestMethod]
    public void NoOpMoveDoesNotPublishMutation()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(2, 2), "value");
        var version = worksheet.Version;

        worksheet.ApplyAxisMove(new WorksheetAxisMove(
            WorksheetAxis.Column,
            sourceIndex: 2,
            count: 1,
            destinationBoundary: 3));

        Assert.AreEqual(version, worksheet.Version);
        Assert.AreEqual("value", worksheet.GetValue(new CellAddress(2, 2)));
    }
}
