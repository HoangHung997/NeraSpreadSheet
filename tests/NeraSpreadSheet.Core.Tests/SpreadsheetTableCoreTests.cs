using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class SpreadsheetTableCoreTests
{
    [TestMethod]
    public void FilterButtonVisibilityShouldSurviveImmutableTableCopies()
    {
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(default, new CellAddress(2, 0)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Item")],
            showFilterButtons: false);

        var copy = table.Copy();
        var renamed = table.Rename("Revenue");
        var filtered = table.WithAutoFilter(new TableAutoFilter([]));

        Assert.IsFalse(copy.ShowFilterButtons);
        Assert.IsFalse(renamed.ShowFilterButtons);
        Assert.IsFalse(filtered.ShowFilterButtons);
    }

    [TestMethod]
    public void WorkbookRejectsDuplicateAndOverlappingTables()
    {
        var workbook = new Workbook();
        var first = workbook.Worksheets[0];
        var second = workbook.AddWorksheet("Second");
        first.AddTable(CreateTable(
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 2))));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            second.AddTable(CreateTable(
                "sales",
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(2, 2)))));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            first.AddTable(CreateTable(
                "Other",
                new CellRange(
                    new CellAddress(2, 1),
                    new CellAddress(5, 3)))));
        Assert.AreEqual(1, first.TableCount);
        Assert.AreEqual(0, second.TableCount);
    }

    [TestMethod]
    public void SnapshotFilterProjectionUsesStableColumnIdentity()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var table = CreateTable(
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 2)));
        var statusColumn = table.Columns[1];
        worksheet.SetValue(new CellAddress(1, 1), "Open");
        worksheet.SetValue(new CellAddress(2, 1), "Closed");
        worksheet.SetValue(new CellAddress(3, 1), "Open");
        worksheet.AddTable(table.WithAutoFilter(new TableAutoFilter([
            new TableFilterColumn(
                statusColumn.Id,
                [CellValue.FromText("Open")]),
        ])));

        var snapshot = WorksheetSnapshot.Capture(worksheet);

        Assert.IsTrue(snapshot.IsRowVisible(0));
        Assert.IsTrue(snapshot.IsRowVisible(1));
        Assert.IsFalse(snapshot.IsRowVisible(2));
        Assert.IsTrue(snapshot.IsRowVisible(3));
        Assert.IsTrue(snapshot.TryGetTable("sales", out var captured));
        Assert.AreEqual(statusColumn.Id, captured!.Columns[1].Id);
    }

    [TestMethod]
    public void StructuralColumnInsertAddsColumnAndUndoStateIsExact()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var table = CreateTable(
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 2)));
        worksheet.AddTable(table);
        var before = worksheet.CaptureStructuralState();

        worksheet.ApplyStructuralChange(new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Insert,
            index: 1,
            count: 1));

        var expanded = worksheet.Tables.Single();
        Assert.AreEqual(4, expanded.Columns.Count);
        Assert.AreEqual(4, expanded.Range.ColumnCount);
        Assert.AreEqual(table.Columns[0].Id, expanded.Columns[0].Id);
        Assert.AreEqual(table.Columns[1].Id, expanded.Columns[2].Id);
        Assert.AreNotEqual(Guid.Empty, expanded.Columns[1].Id);

        worksheet.RestoreStructuralState(
            before,
            new WorksheetStructuralChange(
                WorksheetAxis.Column,
                WorksheetStructuralChangeKind.Insert,
                index: 1,
                count: 1));
        var restored = worksheet.Tables.Single();
        CollectionAssert.AreEqual(
            table.Columns.Select(static column => column.Id).ToArray(),
            restored.Columns.Select(static column => column.Id).ToArray());
        Assert.AreEqual(table.Range, restored.Range);
    }

    [TestMethod]
    public void InternalTableReorderIsRejectedBeforeMutation()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var table = CreateTable(
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(5, 2)));
        worksheet.AddTable(table);
        var cellsBefore = worksheet.EnumerateUsedCells().ToArray();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            worksheet.ApplyAxisMove(new WorksheetAxisMove(
                WorksheetAxis.Row,
                sourceIndex: 2,
                count: 1,
                destinationBoundary: 5)));

        Assert.AreEqual(table.Range, worksheet.Tables.Single().Range);
        CollectionAssert.AreEqual(
            cellsBefore,
            worksheet.EnumerateUsedCells().ToArray());
    }

    [TestMethod]
    public void TableAndMergedRangeCannotOverlap()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.MergeCells(new CellRange(
            new CellAddress(1, 1),
            new CellAddress(1, 2)));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            worksheet.AddTable(CreateTable(
                "Sales",
                new CellRange(
                    new CellAddress(0, 0),
                new CellAddress(3, 2)))));
    }

    [TestMethod]
    public void StructuralColumnChangesRemapTableSortStateWithoutChangingColumnIdentity()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var table = CreateTable(
            "Sales",
            new CellRange(new CellAddress(0, 0), new CellAddress(3, 2)));
        var sortedColumnId = table.Columns[2].Id;
        worksheet.AddTable(table.WithAutoFilter(new TableAutoFilter(
            [],
            new SpreadsheetFilterSortState([new SpreadsheetFilterSortCondition(2)]))));

        worksheet.ApplyStructuralChange(new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Insert,
            index: 1,
            count: 1));

        var inserted = worksheet.Tables.Single();
        Assert.AreEqual(3, inserted.AutoFilter!.SortState!.Conditions.Single().ColumnOffset);
        Assert.AreEqual(sortedColumnId, inserted.Columns[3].Id);

        worksheet.ApplyStructuralChange(new WorksheetStructuralChange(
            WorksheetAxis.Column,
            WorksheetStructuralChangeKind.Delete,
            index: 1,
            count: 1));
        var restored = worksheet.Tables.Single();
        Assert.AreEqual(2, restored.AutoFilter!.SortState!.Conditions.Single().ColumnOffset);
        Assert.AreEqual(sortedColumnId, restored.Columns[2].Id);
    }

    private static SpreadsheetTable CreateTable(
        string name,
        CellRange range)
    {
        var columns = Enumerable.Range(0, range.ColumnCount)
            .Select(index => new SpreadsheetTableColumn(
                Guid.NewGuid(),
                index switch
                {
                    0 => "Item",
                    1 => "Status",
                    _ => $"Value{index}",
                }))
            .ToArray();
        return new SpreadsheetTable(
            Guid.NewGuid(),
            name,
            range,
            columns);
    }
}
