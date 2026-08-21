using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetTablePresenterTests
{
    [TestMethod]
    public void ManagerSnapshotExposesColumnsFormulasTotalsAndFilterState()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var categoryId = Guid.NewGuid();
        var amountId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(new CellAddress(0, 1), new CellAddress(4, 2)),
            [
                new SpreadsheetTableColumn(categoryId, "Category"),
                new SpreadsheetTableColumn(
                    amountId,
                    "Amount",
                    calculatedColumnFormula: "=[@Category]*2",
                    totalsRowFormula: "=SUM(Sales[Amount])"),
            ],
            hasTotalsRow: true,
            autoFilter: new TableAutoFilter([
                new TableFilterColumn(
                    categoryId,
                    [CellValue.FromText("A")]),
            ]));
        worksheet.AddTable(table);
        var presenter = new SpreadsheetTablePresenterController(
            new SpreadsheetSession(workbook));

        var snapshot = presenter.GetManagerSnapshot();

        Assert.AreEqual(worksheet.Name, snapshot.WorksheetName);
        Assert.AreEqual(1, snapshot.Tables.Count);
        Assert.IsTrue(snapshot.Tables[0].HasActiveFilter);
        Assert.AreEqual(2, snapshot.Tables[0].Columns.Count);
        Assert.IsTrue(snapshot.Tables[0].Columns[0].IsFiltered);
        Assert.IsFalse(snapshot.Tables[0].Columns[1].IsFiltered);
        Assert.IsTrue(snapshot.Tables[0].Columns[1].HasCalculatedFormula);
        Assert.IsTrue(snapshot.Tables[0].Columns[1].HasTotalsFormula);
        Assert.AreEqual(2, snapshot.Tables[0].Columns[1].WorksheetColumnIndex);
    }

    [TestMethod]
    public void FilterMenuCountsValuesSearchesAndChangesVisibleSelection()
    {
        var context = CreateFilteredTable();
        var presenter = new SpreadsheetTablePresenterController(context.Session);

        var menu = presenter.OpenFilterMenu(
            context.Table.Id,
            context.StatusColumnId);
        var initial = menu.Capture();

        Assert.AreEqual(5, initial.SourceRowCount);
        Assert.AreEqual(5, initial.ScannedRowCount);
        Assert.AreEqual(3, initial.DistinctValueCount);
        Assert.AreEqual(2, initial.Values.Single(item =>
            item.DisplayText == "Open").Count);
        Assert.IsTrue(initial.Values.Single(item =>
            item.DisplayText == "Open").IsSelected);
        Assert.IsFalse(initial.Values.Single(item =>
            item.DisplayText == "Closed").IsSelected);

        menu.SetSearchText("pen");
        var searched = menu.Capture();
        Assert.AreEqual(2, searched.Values.Count);
        Assert.IsTrue(searched.Values.Any(item =>
            item.DisplayText == "Open"));
        Assert.IsTrue(searched.Values.Any(item =>
            item.DisplayText == "Pending"));
        menu.ClearVisibleSelection();
        Assert.IsFalse(menu.Capture().CanApplyValueSelection);
        menu.SelectAllVisible();
        Assert.IsTrue(menu.Capture().CanApplyValueSelection);
    }

    [TestMethod]
    public void BoundedEnumerationReportsRowAndDistinctTruncation()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var columnId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Items",
            new CellRange(new CellAddress(0, 0), new CellAddress(10, 0)),
            [new SpreadsheetTableColumn(columnId, "Code")]);
        worksheet.AddTable(table);
        for (var row = 1; row <= 10; row++)
        {
            worksheet.SetValue(new CellAddress(row, 0), $"V{row}");
        }
        var presenter = new SpreadsheetTablePresenterController(
            new SpreadsheetSession(workbook));

        var snapshot = presenter.OpenFilterMenu(
            table.Id,
            columnId,
            maximumRows: 5,
            maximumDistinctValues: 3).Capture();

        Assert.IsTrue(snapshot.IsRowScanTruncated);
        Assert.IsTrue(snapshot.IsDistinctValueTruncated);
        Assert.AreEqual(10, snapshot.SourceRowCount);
        Assert.AreEqual(5, snapshot.ScannedRowCount);
        Assert.AreEqual(3, snapshot.DistinctValueCount);
    }

    [TestMethod]
    public void ApplyValueSelectionUsesProductionHistoryAndUndoRedo()
    {
        var context = CreateFilteredTable(withInitialFilter: false);
        var presenter = new SpreadsheetTablePresenterController(context.Session);
        var menu = presenter.OpenFilterMenu(
            context.Table.Id,
            context.StatusColumnId);
        var open = menu.Capture().Values.Single(item =>
            item.DisplayText == "Open").Value;
        foreach (var item in menu.Capture().Values)
        {
            menu.SetSelected(item.Value, item.Value == open);
        }

        menu.ApplyValueSelection();

        Assert.IsFalse(WorksheetSnapshot.Capture(context.Worksheet)
            .IsRowVisible(2));
        Assert.IsTrue(context.Session.Undo());
        Assert.IsTrue(WorksheetSnapshot.Capture(context.Worksheet)
            .IsRowVisible(2));
        Assert.IsTrue(context.Session.Redo());
        Assert.IsFalse(WorksheetSnapshot.Capture(context.Worksheet)
            .IsRowVisible(2));
    }

    [TestMethod]
    public void SelectingEveryEnumeratedValueClearsColumnFilter()
    {
        var context = CreateFilteredTable();
        var presenter = new SpreadsheetTablePresenterController(context.Session);
        var menu = presenter.OpenFilterMenu(
            context.Table.Id,
            context.StatusColumnId);

        menu.SelectAllVisible();
        menu.ApplyValueSelection();

        Assert.IsTrue(context.Worksheet.TryGetTable(
            context.Table.Id,
            out var updated));
        Assert.IsNull(updated!.AutoFilter);
        Assert.IsTrue(WorksheetSnapshot.Capture(context.Worksheet)
            .IsRowVisible(2));
    }

    [TestMethod]
    public void EmptySelectionIsRejectedWithoutHistoryMutation()
    {
        var context = CreateFilteredTable(withInitialFilter: false);
        var presenter = new SpreadsheetTablePresenterController(context.Session);
        var menu = presenter.OpenFilterMenu(
            context.Table.Id,
            context.StatusColumnId);
        menu.ClearVisibleSelection();
        var undoCount = context.Session.History.UndoCount;

        Assert.ThrowsExactly<InvalidOperationException>(
            menu.ApplyValueSelection);

        Assert.AreEqual(undoCount, context.Session.History.UndoCount);
        Assert.IsTrue(context.Worksheet.TryGetTable(
            context.Table.Id,
            out var unchanged));
        Assert.IsNull(unchanged!.AutoFilter);
    }

    [TestMethod]
    public void CustomFilterAndClearColumnPreserveOtherColumnFilters()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusId = Guid.NewGuid();
        var amountId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(new CellAddress(0, 0), new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(statusId, "Status"),
                new SpreadsheetTableColumn(amountId, "Amount"),
            ],
            autoFilter: new TableAutoFilter([
                new TableFilterColumn(
                    statusId,
                    [CellValue.FromText("Open")]),
            ]));
        worksheet.AddTable(table);
        var session = new SpreadsheetSession(workbook);
        var presenter = new SpreadsheetTablePresenterController(session);

        presenter.ApplyCustomFilter(
            table.Id,
            amountId,
            new TableFilterCondition(
                TableFilterComparisonOperator.GreaterThan,
                CellValue.FromNumber(10d)));

        Assert.IsTrue(worksheet.TryGetTable(table.Id, out var filtered));
        Assert.AreEqual(2, filtered!.AutoFilter!.Columns.Count);
        presenter.ClearColumnFilter(table.Id, amountId);
        Assert.IsTrue(worksheet.TryGetTable(table.Id, out var cleared));
        Assert.AreEqual(1, cleared!.AutoFilter!.Columns.Count);
        Assert.AreEqual(statusId, cleared.AutoFilter.Columns[0].ColumnId);
    }

    private static TestContext CreateFilteredTable(
        bool withInitialFilter = true)
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(new CellAddress(0, 0), new CellAddress(5, 0)),
            [new SpreadsheetTableColumn(statusId, "Status")],
            autoFilter: withInitialFilter
                ? new TableAutoFilter([
                    new TableFilterColumn(
                        statusId,
                        [CellValue.FromText("Open")]),
                ])
                : null);
        worksheet.AddTable(table);
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(3, 0), "Open");
        worksheet.SetValue(new CellAddress(4, 0), "Pending");
        worksheet.SetValue(new CellAddress(5, 0), "Closed");
        return new TestContext(
            worksheet,
            table,
            statusId,
            new SpreadsheetSession(workbook));
    }

    private sealed record TestContext(
        Worksheet Worksheet,
        SpreadsheetTable Table,
        Guid StatusColumnId,
        SpreadsheetSession Session);
}
