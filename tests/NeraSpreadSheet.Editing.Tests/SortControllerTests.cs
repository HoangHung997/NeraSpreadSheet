using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SortControllerTests
{
    [TestMethod]
    public void SortAscendingMovesEntireRowsAndPreservesStyles()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var boldStyle = workbook.Styles.Intern(CellStyle.Default with
        {
            Font = CellStyle.Default.Font with { Weight = 700 },
        });
        sheet.SetValue(new CellAddress(0, 0), 3d);
        sheet.SetValue(new CellAddress(0, 1), "C");
        sheet.SetValue(new CellAddress(1, 0), 1d);
        sheet.SetValue(new CellAddress(1, 1), "A");
        sheet.SetStyle(new CellAddress(1, 1), boldStyle);
        sheet.SetValue(new CellAddress(2, 0), 2d);
        sheet.SetValue(new CellAddress(2, 1), "B");
        var session = new SpreadsheetSession(workbook);
        var range = new CellRange(default, new CellAddress(2, 1));
        session.Selection.Select(range);

        session.Sort.Sort(range, keyColumnOffset: 0, ascending: true);

        Assert.AreEqual(1d, sheet.GetCell(new CellAddress(0, 0)).Value.RawValue);
        Assert.AreEqual("A", sheet.GetCell(new CellAddress(0, 1)).Value.RawValue);
        Assert.AreEqual(boldStyle, sheet.GetCell(new CellAddress(0, 1)).StyleId);
        Assert.AreEqual(2d, sheet.GetCell(new CellAddress(1, 0)).Value.RawValue);
        Assert.AreEqual(3d, sheet.GetCell(new CellAddress(2, 0)).Value.RawValue);
    }

    [TestMethod]
    public void SortIsUndoable()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, 2d);
        sheet.SetValue(new CellAddress(1, 0), 1d);
        var session = new SpreadsheetSession(workbook);
        var range = new CellRange(default, new CellAddress(1, 0));

        session.Sort.Sort(range, 0, ascending: true);
        Assert.AreEqual(1d, sheet.GetCell(default).Value.RawValue);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(2d, sheet.GetCell(default).Value.RawValue);
        Assert.AreEqual(1d, sheet.GetCell(new CellAddress(1, 0)).Value.RawValue);
    }

    [TestMethod]
    public async Task SortCommandsUseActiveColumnAsKey()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "r1");
        sheet.SetValue(new CellAddress(0, 1), 9d);
        sheet.SetValue(new CellAddress(1, 0), "r2");
        sheet.SetValue(new CellAddress(1, 1), 1d);
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(default, new CellAddress(1, 1)));
        session.Selection.SetActiveCell(new CellAddress(0, 1), preserveAnchor: true);

        Assert.IsTrue(await session.CommandDispatcher.TryExecuteAsync(SpreadsheetSortCommandIds.SortAscending));

        Assert.AreEqual("r2", sheet.GetCell(default).Value.RawValue);
        Assert.AreEqual(1d, sheet.GetCell(new CellAddress(0, 1)).Value.RawValue);
    }

    [TestMethod]
    public void SortRejectsMergedRange()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.MergeCells(new CellRange(default, new CellAddress(0, 1)));
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(default, new CellAddress(1, 1)));

        Assert.IsFalse(session.Sort.CanSortPrimarySelection);
    }

    [TestMethod]
    public void SelectionSortRejectsSpillRootAndChildWithoutChangingHistory()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        var owner = new CellAddress(1, 0);
        session.SetFormula(owner, "=SEQUENCE(2)");
        var historyBefore = session.History.UndoCount;

        var rootRange = new CellRange(new CellAddress(0, 0), owner);
        session.Selection.Select(rootRange);
        Assert.IsFalse(session.Sort.CanSortPrimarySelection);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Sort.Sort(rootRange, 0, ascending: true));

        var childRange = new CellRange(
            new CellAddress(2, 0),
            new CellAddress(3, 0));
        session.Selection.Select(childRange);
        Assert.IsFalse(session.Sort.CanSortPrimarySelection);
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Sort.Sort(childRange, 0, ascending: true));

        Assert.AreEqual(historyBefore, session.History.UndoCount);
        Assert.AreEqual("=SEQUENCE(2)", sheet.GetCell(owner).Formula);
        Assert.AreEqual(2d, sheet.GetValue(new CellAddress(2, 0)));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, sheet.GetFormulaSpillCount());
        Assert.IsNull(sheet.GetValue(owner));
    }

    [TestMethod]
    public void WorksheetAutoFilterSortRejectsSpillAtomically()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "Value");
        var session = new SpreadsheetSession(workbook);
        var owner = new CellAddress(1, 0);
        session.SetFormula(owner, "=SEQUENCE(2)");
        sheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(default, new CellAddress(3, 0))));
        var filterBefore = sheet.AutoFilter;
        var cellsBefore = WorksheetSnapshot.Capture(sheet);
        var historyBefore = session.History.UndoCount;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Sort.SortWorksheet(new SpreadsheetFilterSortState([
                new SpreadsheetFilterSortCondition(0),
            ])));

        Assert.AreEqual(historyBefore, session.History.UndoCount);
        Assert.AreEqual(filterBefore, sheet.AutoFilter);
        Assert.AreEqual(cellsBefore.GetCell(owner), sheet.GetCell(owner));
        Assert.AreEqual(
            cellsBefore.GetCell(new CellAddress(2, 0)),
            sheet.GetCell(new CellAddress(2, 0)));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, sheet.GetFormulaSpillCount());
    }

    [TestMethod]
    public void TableSortUsesOrderedKeysCustomOrderAndOneUndoTransaction()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var statusId = Guid.NewGuid();
        var amountId = Guid.NewGuid();
        sheet.SetValue(new CellAddress(0, 0), "Status");
        sheet.SetValue(new CellAddress(0, 1), "Amount");
        SetRow(sheet, 1, "Low", 30d);
        SetRow(sheet, 2, "High", 10d);
        SetRow(sheet, 3, "Low", 20d);
        SetRow(sheet, 4, "High", 40d);
        sheet.AddTable(new SpreadsheetTable(
            tableId,
            "Tasks",
            new CellRange(new CellAddress(0, 0), new CellAddress(4, 1)),
            [
                new SpreadsheetTableColumn(statusId, "Status"),
                new SpreadsheetTableColumn(amountId, "Amount"),
            ]));
        var session = new SpreadsheetSession(workbook);
        var state = new SpreadsheetFilterSortState([
            new SpreadsheetFilterSortCondition(0, customList: "High,Low"),
            new SpreadsheetFilterSortCondition(1, descending: true),
        ]);

        Assert.IsTrue(session.Sort.SortTable(tableId, state));

        string[] expectedStatuses = ["High", "High", "Low", "Low"];
        double[] expectedAmounts = [40d, 10d, 30d, 20d];
        CollectionAssert.AreEqual(
            expectedStatuses,
            Enumerable.Range(1, 4)
                .Select(row => (string)sheet.GetCell(new CellAddress(row, 0)).Value.RawValue!)
                .ToArray());
        CollectionAssert.AreEqual(
            expectedAmounts,
            Enumerable.Range(1, 4)
                .Select(row => (double)sheet.GetCell(new CellAddress(row, 1)).Value.RawValue!)
                .ToArray());
        Assert.AreEqual(1, session.History.UndoCount);
        Assert.AreEqual(2, sheet.Tables.Single().AutoFilter!.SortState!.Conditions.Count);
        Assert.IsTrue(session.Undo());
        double[] originalAmounts = [30d, 10d, 20d, 40d];
        CollectionAssert.AreEqual(
            originalAmounts,
            Enumerable.Range(1, 4)
                .Select(row => (double)sheet.GetCell(new CellAddress(row, 1)).Value.RawValue!)
                .ToArray());
        Assert.IsNull(sheet.Tables.Single().AutoFilter);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(40d, sheet.GetCell(new CellAddress(1, 1)).Value.RawValue);
    }

    [TestMethod]
    public void WorksheetSortClearAndReapplyUseCurrentMappedState()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), "Name");
        sheet.SetValue(new CellAddress(0, 1), "Rank");
        SetRow(sheet, 1, "C", 3d);
        SetRow(sheet, 2, "A", 1d);
        SetRow(sheet, 3, "B", 2d);
        sheet.SetAutoFilter(new WorksheetAutoFilter(
            new CellRange(new CellAddress(0, 0), new CellAddress(3, 1))));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 1));
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(out var target));

        Assert.IsTrue(session.Sort.SortAutoFilter(
            target,
            new SpreadsheetFilterSortState([
                new SpreadsheetFilterSortCondition(1),
            ])));
        Assert.AreEqual("A", sheet.GetCell(new CellAddress(1, 0)).Value.RawValue);

        sheet.SetValue(new CellAddress(1, 1), 9d);
        sheet.SetValue(new CellAddress(3, 1), 0d);
        Assert.IsTrue(session.Sort.ReapplyAutoFilter(target));
        Assert.AreEqual(0d, sheet.GetCell(new CellAddress(1, 1)).Value.RawValue);
        Assert.IsTrue(session.Sort.ClearAutoFilterSort(target));
        Assert.IsNull(sheet.AutoFilter!.SortState);
        Assert.IsFalse(session.Sort.ClearAutoFilterSort(target));
    }

    [TestMethod]
    public void AutoFilterSortRejectsUnsupportedOrOverBudgetRequestsAtomically()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        sheet.SetValue(default, "Value");
        sheet.SetValue(new CellAddress(1, 0), 2d);
        sheet.SetValue(new CellAddress(2, 0), 1d);
        sheet.AddTable(new SpreadsheetTable(
            tableId,
            "Values",
            new CellRange(default, new CellAddress(2, 0)),
            [new SpreadsheetTableColumn(columnId, "Value")]));
        var session = new SpreadsheetSession(workbook);
        var bounded = new SpreadsheetSortController(session, maximumMaterializedCells: 1);
        var state = new SpreadsheetFilterSortState([
            new SpreadsheetFilterSortCondition(0),
        ]);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            bounded.SortTable(tableId, state));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            session.Sort.SortTable(
                tableId,
                new SpreadsheetFilterSortState([
                    new SpreadsheetFilterSortCondition(
                        0,
                        sortBy: SpreadsheetFilterSortBy.CellColor,
                        color: new SpreadsheetColorFilter(
                            SpreadsheetFilterColorKind.Fill,
                            new NeraSpreadSheet.Foundation.ColorRgba(255, 0, 0))),
                ])));
        Assert.AreEqual(0, session.History.UndoCount);
        Assert.AreEqual(2d, sheet.GetCell(new CellAddress(1, 0)).Value.RawValue);
        Assert.IsNull(sheet.Tables.Single().AutoFilter);
    }

    [TestMethod]
    public void TableReapplyUsesStableIdentityAfterStructuralColumnInsertion()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        var nameId = Guid.NewGuid();
        var rankId = Guid.NewGuid();
        sheet.SetValue(new CellAddress(0, 0), "Name");
        sheet.SetValue(new CellAddress(0, 1), "Rank");
        SetRow(sheet, 1, "B", 2d);
        SetRow(sheet, 2, "A", 1d);
        sheet.AddTable(new SpreadsheetTable(
            tableId,
            "Ranked",
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 1)),
            [
                new SpreadsheetTableColumn(nameId, "Name"),
                new SpreadsheetTableColumn(rankId, "Rank"),
            ]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 1));
        Assert.IsTrue(session.TryResolveActiveAutoFilterTarget(out var originalTarget));
        Assert.IsTrue(session.Sort.SortAutoFilter(
            originalTarget,
            new SpreadsheetFilterSortState([
                new SpreadsheetFilterSortCondition(1),
            ])));

        session.Structure.InsertColumns(1);
        var mapped = sheet.Tables.Single();
        Assert.AreEqual(tableId, mapped.Id);
        Assert.AreEqual(rankId, mapped.Columns[2].Id);
        Assert.AreEqual(2, mapped.AutoFilter!.SortState!.Conditions.Single().ColumnOffset);
        sheet.SetValue(new CellAddress(1, 2), 9d);
        sheet.SetValue(new CellAddress(2, 2), 0d);

        Assert.IsTrue(session.Sort.ReapplyAutoFilter(originalTarget));
        Assert.AreEqual(0d, sheet.GetCell(new CellAddress(1, 2)).Value.RawValue);
        Assert.AreEqual(tableId, sheet.Tables.Single().Id);
        Assert.AreEqual(rankId, sheet.Tables.Single().Columns[2].Id);
    }

    [TestMethod]
    public void TableSortMovesFormulasWithRowsAndTranslatesRelativeReferences()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        sheet.SetValue(new CellAddress(0, 0), "Name");
        sheet.SetValue(new CellAddress(0, 1), "Amount");
        sheet.SetValue(new CellAddress(0, 2), "Double");
        sheet.SetValue(new CellAddress(1, 0), "B");
        sheet.SetValue(new CellAddress(1, 1), 2d);
        sheet.SetFormula(new CellAddress(1, 2), "=B2*2");
        sheet.SetValue(new CellAddress(2, 0), "A");
        sheet.SetValue(new CellAddress(2, 1), 1d);
        sheet.SetFormula(new CellAddress(2, 2), "=B3*2");
        sheet.AddTable(new SpreadsheetTable(
            tableId,
            "FormulaRows",
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 2)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Name"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Amount"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Double"),
            ]));
        var session = new SpreadsheetSession(workbook);

        Assert.IsTrue(session.Sort.SortTable(
            tableId,
            new SpreadsheetFilterSortState([
                new SpreadsheetFilterSortCondition(1),
            ])));

        Assert.AreEqual("A", sheet.GetCell(new CellAddress(1, 0)).Value.RawValue);
        Assert.AreEqual("=B2*2", sheet.GetCell(new CellAddress(1, 2)).Formula);
        Assert.AreEqual("=B3*2", sheet.GetCell(new CellAddress(2, 2)).Formula);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("B", sheet.GetCell(new CellAddress(1, 0)).Value.RawValue);
        Assert.AreEqual("=B2*2", sheet.GetCell(new CellAddress(1, 2)).Formula);
    }

    [TestMethod]
    [Timeout(30_000)]
    public void LargeAutoFilterSortStaysWithinTheMaterializationBudget()
    {
        const int rowCount = 100_000;
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        var tableId = Guid.NewGuid();
        sheet.SetValue(default, "Value");
        for (var row = 1; row <= rowCount; row++)
        {
            sheet.SetValue(new CellAddress(row, 0), (double)(rowCount - row));
        }
        sheet.AddTable(new SpreadsheetTable(
            tableId,
            "LargeValues",
            new CellRange(default, new CellAddress(rowCount, 0)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Value")]));
        var session = new SpreadsheetSession(workbook);

        Assert.IsTrue(session.Sort.SortTable(
            tableId,
            new SpreadsheetFilterSortState([
                new SpreadsheetFilterSortCondition(0),
            ])));

        Assert.AreEqual(0d, sheet.GetCell(new CellAddress(1, 0)).Value.RawValue);
        Assert.AreEqual((double)(rowCount - 1),
            sheet.GetCell(new CellAddress(rowCount, 0)).Value.RawValue);
        Assert.AreEqual(1, session.History.UndoCount);
    }

    private static void SetRow(Worksheet sheet, int row, string text, double number)
    {
        sheet.SetValue(new CellAddress(row, 0), text);
        sheet.SetValue(new CellAddress(row, 1), number);
    }
}
