using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetTableDesignControllerTests
{
    [TestMethod]
    public void CreateAndDesignOptionsShouldUseOneHistoryEntryPerMutation()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Item");
        worksheet.SetValue(new CellAddress(0, 1), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(1, 1), 2d);
        var session = new SpreadsheetSession(workbook);
        session.Selection.Select(new CellRange(default, new CellAddress(1, 1)));

        var table = session.TableDesign.CreateTable("Sales");

        Assert.AreEqual(1, session.History.UndoCount);
        Assert.AreEqual(table.Id, session.TableDesign.Snapshot.TableId);
        Assert.AreEqual(60, session.TableDesign.Snapshot.Styles.Count);
        Assert.IsTrue(session.TableDesign.Snapshot.Styles.All(static item =>
            item.Preview.Count <= TableStylePreview.MaximumRows *
            TableStylePreview.MaximumColumns));

        session.Tables.SetTotalsRow(table.Id, true);
        Assert.AreEqual(2, session.History.UndoCount);
        session.Tables.SetFirstColumn(table.Id, true);
        session.Tables.SetLastColumn(table.Id, true);
        session.Tables.SetBandedRows(table.Id, false);
        session.Tables.SetBandedColumns(table.Id, true);
        session.Tables.SetFilterButtons(table.Id, false);
        session.Tables.SetStyle(table.Id, "TableStyleDark1");

        Assert.IsTrue(worksheet.TryGetTable(table.Id, out var designed));
        Assert.IsTrue(designed!.HasTotalsRow);
        Assert.IsTrue(designed.ShowFirstColumn);
        Assert.IsTrue(designed.ShowLastColumn);
        Assert.IsFalse(designed.ShowRowStripes);
        Assert.IsTrue(designed.ShowColumnStripes);
        Assert.IsFalse(designed.ShowFilterButtons);
        Assert.AreEqual("TableStyleDark1", designed.StyleName);
        Assert.AreEqual(8, session.History.UndoCount);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual("TableStyleMedium2", worksheet.Tables.Single().StyleName);
    }

    [TestMethod]
    public void CalculatedColumnAndTotalsFunctionShouldProjectAndUndoTogether()
    {
        var session = CreateSession(out var worksheet, out var table);
        var amount = table.Columns[1];
        session.Selection.SetActiveCell(new CellAddress(1, 0));

        session.TableDesign.SetCalculatedColumnFormula("=[@Amount]*2");
        session.Tables.SetTotalsRow(table.Id, true);
        session.Selection.SetActiveCell(new CellAddress(1, 1));
        session.TableDesign.SetTotalsFunction(SpreadsheetTableTotalsFunction.Sum);

        Assert.AreEqual("=[@Amount]*2", worksheet.GetFormula(new CellAddress(1, 0)));
        var current = worksheet.Tables.Single();
        Assert.AreEqual(amount.Id, current.Columns[1].Id);
        Assert.AreEqual(
            "=SUBTOTAL(109,Sales[Amount])",
            current.Columns[1].TotalsRowFormula);
        Assert.AreEqual(
            "=SUBTOTAL(109,Sales[Amount])",
            worksheet.GetFormula(new CellAddress(current.Range.Bottom, 1)));

        Assert.IsTrue(session.Undo());
        Assert.IsNull(worksheet.Tables.Single().Columns[1].TotalsRowFormula);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(
            "=SUBTOTAL(109,Sales[Amount])",
            worksheet.Tables.Single().Columns[1].TotalsRowFormula);
    }

    [TestMethod]
    public void HeaderRowShouldGrowAndShrinkWithoutLosingData()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(default, "A");
        worksheet.SetValue(new CellAddress(0, 1), 2d);
        worksheet.SetValue(new CellAddress(1, 0), "B");
        worksheet.SetValue(new CellAddress(1, 1), 3d);
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(default, new CellAddress(1, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Item"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Amount"),
            ],
            hasHeaders: false);
        worksheet.AddTable(table);
        var session = new SpreadsheetSession(workbook);

        session.Tables.SetHeaderRow(table.Id, true);

        Assert.AreEqual(2, worksheet.Tables.Single().Range.Bottom);
        Assert.AreEqual("Item", worksheet.GetValue(default));
        Assert.AreEqual("A", worksheet.GetValue(new CellAddress(1, 0)));
        Assert.AreEqual("B", worksheet.GetValue(new CellAddress(2, 0)));

        session.Tables.SetHeaderRow(table.Id, false);

        Assert.AreEqual(1, worksheet.Tables.Single().Range.Bottom);
        Assert.AreEqual("A", worksheet.GetValue(default));
        Assert.AreEqual("B", worksheet.GetValue(new CellAddress(1, 0)));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual("Item", worksheet.GetValue(default));
        Assert.AreEqual("A", worksheet.GetValue(new CellAddress(1, 0)));
    }

    [TestMethod]
    public void InsertRowShouldCreateFirstDataRowInHeaderOnlyTable()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(default, new CellAddress(0, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Item"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Amount"),
            ]);
        worksheet.AddTable(table);
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(default);

        session.TableDesign.InsertRow();

        Assert.AreEqual(1, worksheet.Tables.Single().Range.Bottom);
        Assert.AreEqual(new CellAddress(1, 0), session.Selection.ActiveCell);
    }

    [TestMethod]
    public void InsertDeleteRowsAndColumnsShouldPreserveRetainedIdentityAndSelectionHistory()
    {
        var session = CreateSession(out var worksheet, out var table);
        var originalIds = table.Columns.Select(static column => column.Id).ToArray();
        session.Selection.SetActiveCell(new CellAddress(2, 1));

        session.Tables.InsertRow(table.Id, 2);

        var expanded = worksheet.Tables.Single();
        Assert.AreEqual(4, expanded.Range.Bottom);
        Assert.AreEqual("B", worksheet.GetValue(new CellAddress(3, 0)));
        Assert.AreEqual(new CellAddress(2, 1), session.Selection.ActiveCell);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(3, worksheet.Tables.Single().Range.Bottom);
        Assert.AreEqual("B", worksheet.GetValue(new CellAddress(2, 0)));
        Assert.AreEqual(new CellAddress(2, 1), session.Selection.ActiveCell);
        Assert.IsTrue(session.Redo());

        var inserted = session.Tables.InsertColumn(table.Id, 1, "Status");
        expanded = worksheet.Tables.Single();
        Assert.AreEqual(3, expanded.Columns.Count);
        Assert.AreEqual(originalIds[0], expanded.Columns[0].Id);
        Assert.AreEqual(inserted.Id, expanded.Columns[1].Id);
        Assert.AreEqual(originalIds[1], expanded.Columns[2].Id);
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(1, 2)));

        session.Tables.DeleteColumn(table.Id, inserted.Id);
        var restoredWidth = worksheet.Tables.Single();
        CollectionAssert.AreEqual(
            originalIds,
            restoredWidth.Columns.Select(static column => column.Id).ToArray());
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(1, 1)));
    }

    [TestMethod]
    public void RemoveDuplicatesShouldCompactSparseRowsAndUndoAsOneTransaction()
    {
        var session = CreateSession(out var worksheet, out var table);
        worksheet.SetValue(new CellAddress(2, 0), "A");
        worksheet.SetValue(new CellAddress(2, 1), 2d);
        worksheet.SetValue(new CellAddress(3, 0), "B");
        worksheet.SetValue(new CellAddress(3, 1), 3d);
        var before = session.History.UndoCount;

        var removed = session.Tables.RemoveDuplicates(
            table.Id,
            [table.Columns[0].Id]);

        Assert.AreEqual(1, removed);
        Assert.AreEqual(before + 1, session.History.UndoCount);
        Assert.AreEqual(2, worksheet.Tables.Single().Range.Bottom);
        Assert.AreEqual("B", worksheet.GetValue(new CellAddress(2, 0)));
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(3, worksheet.Tables.Single().Range.Bottom);
        Assert.AreEqual("A", worksheet.GetValue(new CellAddress(2, 0)));
        Assert.AreEqual("B", worksheet.GetValue(new CellAddress(3, 0)));
    }

    [TestMethod]
    public void DeleteReferencedColumnShouldRejectAtomicallyWithoutHistory()
    {
        var session = CreateSession(out var worksheet, out var table);
        var summary = session.Workbook.AddWorksheet("Summary");
        summary.SetFormula(default, "=SUM(Sales[Amount])");
        var before = session.History.UndoCount;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Tables.DeleteColumn(table.Id, table.Columns[1].Id));

        Assert.AreEqual(before, session.History.UndoCount);
        Assert.AreEqual(2, worksheet.Tables.Single().Columns.Count);
        Assert.AreEqual("=SUM(Sales[Amount])", summary.GetFormula(default));
    }

    [TestMethod]
    public void InsertRowShouldRejectExternalA1ReferenceAtomically()
    {
        var session = CreateSession(out var worksheet, out var table);
        var summary = session.Workbook.AddWorksheet("Summary");
        summary.SetFormula(default, "=Sheet1!A2");
        var before = session.History.UndoCount;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Tables.InsertRow(table.Id, 1));

        Assert.AreEqual(before, session.History.UndoCount);
        Assert.AreEqual(3, worksheet.Tables.Single().Range.Bottom);
        Assert.AreEqual("A", worksheet.GetValue(new CellAddress(1, 0)));
        Assert.AreEqual("=Sheet1!A2", summary.GetFormula(default));
    }

    [TestMethod]
    public void InsertColumnShouldRejectMovingA1FormulaAtomically()
    {
        var session = CreateSession(out var worksheet, out var table);
        worksheet.SetFormula(new CellAddress(1, 1), "=Z1");
        var before = session.History.UndoCount;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Tables.InsertColumn(table.Id, 1, "Status"));

        Assert.AreEqual(before, session.History.UndoCount);
        Assert.AreEqual(2, worksheet.Tables.Single().Columns.Count);
        Assert.AreEqual("=Z1", worksheet.GetFormula(new CellAddress(1, 1)));
    }

    [TestMethod]
    public void ResizeShouldPreserveRetainedIdentityAndRejectOccupiedGrowthAtomically()
    {
        var session = CreateSession(out var worksheet, out var table);
        var firstId = table.Columns[0].Id;

        session.Tables.Resize(table.Id, new CellRange(default, new CellAddress(3, 0)));

        Assert.AreEqual(firstId, worksheet.Tables.Single().Columns[0].Id);
        Assert.AreEqual(1, worksheet.Tables.Single(item => item.Id == table.Id).Columns.Count);
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Other",
            new CellRange(new CellAddress(0, 2), new CellAddress(3, 2)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Other")]));
        var before = session.History.UndoCount;
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Tables.Resize(table.Id, new CellRange(default, new CellAddress(3, 2))));
        Assert.AreEqual(before, session.History.UndoCount);
        Assert.AreEqual(1, worksheet.Tables.Single(item => item.Id == table.Id).Columns.Count);
    }

    [TestMethod]
    public void TotalsRowShouldRejectOccupiedGrowthWithoutHistory()
    {
        var session = CreateSession(out var worksheet, out var table);
        worksheet.SetValue(new CellAddress(4, 0), "Outside");
        var before = session.History.UndoCount;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Tables.SetTotalsRow(table.Id, true));

        Assert.AreEqual(before, session.History.UndoCount);
        Assert.IsFalse(worksheet.Tables.Single().HasTotalsRow);
        Assert.AreEqual("Outside", worksheet.GetValue(new CellAddress(4, 0)));
    }

    private static SpreadsheetSession CreateSession(
        out Worksheet worksheet,
        out SpreadsheetTable table)
    {
        var workbook = new Workbook();
        worksheet = workbook.Worksheets[0];
        worksheet.SetValue(default, "Item");
        worksheet.SetValue(new CellAddress(0, 1), "Amount");
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(1, 1), 2d);
        worksheet.SetValue(new CellAddress(2, 0), "B");
        worksheet.SetValue(new CellAddress(2, 1), 3d);
        worksheet.SetValue(new CellAddress(3, 0), "C");
        worksheet.SetValue(new CellAddress(3, 1), 4d);
        table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(default, new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Item"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Amount"),
            ]);
        worksheet.AddTable(table);
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        return session;
    }
}
