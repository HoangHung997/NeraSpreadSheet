using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class WorksheetAutoFilterTests
{
    [TestMethod]
    public void DirectFilterUsesCompressedRowProjection()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Status");
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(3, 0), "Pending");
        worksheet.SetValue(new CellAddress(4, 0), "Open");
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 0)),
            [
                new WorksheetAutoFilterColumn(
                    0,
                    [CellValue.FromText("Open")]),
            ]));

        var snapshot = WorksheetSnapshot.Capture(worksheet);
        var spans = snapshot.GetFilteredOutRowSpans();

        Assert.IsTrue(snapshot.IsRowVisible(0));
        Assert.IsTrue(snapshot.IsRowVisible(1));
        Assert.IsFalse(snapshot.IsRowVisible(2));
        Assert.IsFalse(snapshot.IsRowVisible(3));
        Assert.IsTrue(snapshot.IsRowVisible(4));
        Assert.AreEqual(1, spans.Count);
        Assert.AreEqual(new FilteredRowSpan(2, 3), spans[0]);
    }

    [TestMethod]
    public void DirectFilterCannotOverlapTableOrMergedCells()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Status"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Amount"),
            ]));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            worksheet.SetAutoFilter(new WorksheetAutoFilter(
                new CellRange(
                    new CellAddress(0, 1),
                    new CellAddress(5, 2)))));

        var second = workbook.AddWorksheet("Second");
        second.MergeCells(new CellRange(
            new CellAddress(0, 0),
            new CellAddress(0, 1)));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            second.SetAutoFilter(new WorksheetAutoFilter(
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(5, 1)))));
    }

    [TestMethod]
    public void StructuralInsertMapsRangeAndFilterColumn()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 1),
                new CellAddress(5, 2)),
            [
                new WorksheetAutoFilterColumn(
                    1,
                    firstCondition: new TableFilterCondition(
                        TableFilterComparisonOperator.GreaterThan,
                        CellValue.FromNumber(10d))),
            ]));

        worksheet.ApplyStructuralChange(new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Insert,
            index: 2,
            count: 1));

        var filter = worksheet.AutoFilter!;
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 1),
                new CellAddress(5, 3)),
            filter.Range);
        Assert.AreEqual(2, filter.Columns.Single().ColumnOffset);
    }

    [TestMethod]
    public void StructuralUndoStateRestoresExactDirectFilter()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var filter = new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(5, 1)),
            [
                new WorksheetAutoFilterColumn(
                    0,
                    firstCondition: new TableFilterCondition(
                        TableFilterComparisonOperator.Contains,
                        CellValue.FromText("open"))),
            ]);
        worksheet.SetAutoFilter(filter);
        var state = worksheet.CaptureStructuralState();
        var change = new WorksheetStructuralChange(
            WorksheetAxis.Row,
            WorksheetStructuralChangeKind.Insert,
            index: 2,
            count: 2);

        worksheet.ApplyStructuralChange(change);
        worksheet.RestoreStructuralState(state, change);

        Assert.AreEqual(filter, worksheet.AutoFilter);
    }

    [TestMethod]
    public void PartialHeaderDeletionIsRejectedBeforeMutation()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var filter = new WorksheetAutoFilter(
            new CellRange(
                new CellAddress(2, 0),
                new CellAddress(5, 1)));
        worksheet.SetAutoFilter(filter);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            worksheet.ApplyStructuralChange(
                new WorksheetStructuralChange(
                    WorksheetAxis.Row,
                    WorksheetStructuralChangeKind.Delete,
                    index: 2,
                    count: 1)));

        Assert.AreEqual(filter, worksheet.AutoFilter);
    }
}
