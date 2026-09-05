using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetTableControllerTests
{
    [TestMethod]
    public void AddRemoveUndoRedoPreservesStableTableIdentity()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        var table = CreateSalesTable();

        session.Tables.Add(table);

        Assert.AreEqual(1, worksheet.TableCount);
        Assert.AreEqual(table.Id, worksheet.Tables[0].Id);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, worksheet.TableCount);
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(table.Id, worksheet.Tables[0].Id);

        Assert.IsTrue(session.Tables.Remove(table.Id));
        Assert.AreEqual(0, worksheet.TableCount);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(table.Id, worksheet.Tables[0].Id);
    }

    [TestMethod]
    public void RenameTableRewritesWorkbookFormulasAndUndoRedoTogether()
    {
        var workbook = new Workbook();
        var data = workbook.Worksheets[0];
        var summary = workbook.AddWorksheet("Summary");
        var table = CreateSalesTable();
        data.AddTable(table);
        SetAmounts(data);
        var formulaAddress = new CellAddress(0, 0);
        summary.SetFormula(formulaAddress, "=SUM(Sales[Amount])");
        var session = new SpreadsheetSession(workbook, data);
        session.Recalculate();

        session.Tables.RenameTable(table.Id, "Revenue");

        Assert.IsTrue(data.TryGetTable(table.Id, out var renamed));
        Assert.AreEqual("Revenue", renamed!.Name);
        Assert.AreEqual(
            "=SUM(Revenue[Amount])",
            summary.GetFormula(formulaAddress));
        Assert.AreEqual(6d, summary.GetValue(formulaAddress));

        Assert.IsTrue(session.Undo());
        Assert.IsTrue(data.TryGetTable(table.Id, out var restored));
        Assert.AreEqual("Sales", restored!.Name);
        Assert.AreEqual(
            "=SUM(Sales[Amount])",
            summary.GetFormula(formulaAddress));
        Assert.AreEqual(6d, summary.GetValue(formulaAddress));

        Assert.IsTrue(session.Redo());
        Assert.IsTrue(data.TryGetTable(table.Id, out var redone));
        Assert.AreEqual("Revenue", redone!.Name);
        Assert.AreEqual(
            "=SUM(Revenue[Amount])",
            summary.GetFormula(formulaAddress));
    }

    [TestMethod]
    public void RenameColumnRewritesExplicitAndImplicitReferencesButNotStrings()
    {
        var workbook = new Workbook();
        var data = workbook.Worksheets[0];
        var summary = workbook.AddWorksheet("Summary");
        var table = CreateCalculatedSalesTable(out var amountColumnId);
        data.AddTable(table);
        SetAmounts(data);
        var implicitAddress = new CellAddress(1, 2);
        data.SetFormula(implicitAddress, "=[@Amount]*2");
        var explicitAddress = new CellAddress(0, 0);
        var stringAddress = new CellAddress(1, 0);
        summary.SetFormula(explicitAddress, "=SUM(Sales[Amount])");
        summary.SetFormula(stringAddress, "=\"Sales[Amount]\"");
        var session = new SpreadsheetSession(workbook, data);

        session.Tables.RenameColumn(
            table.Id,
            amountColumnId,
            "NetAmount");

        Assert.AreEqual(
            "=[@NetAmount]*2",
            data.GetFormula(implicitAddress));
        Assert.AreEqual(
            "=SUM(Sales[NetAmount])",
            summary.GetFormula(explicitAddress));
        Assert.AreEqual(
            "=\"Sales[Amount]\"",
            summary.GetFormula(stringAddress));
        Assert.IsTrue(data.TryGetTable(table.Id, out var renamed));
        Assert.AreEqual("NetAmount", renamed!.Columns[1].Name);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(
            "=[@Amount]*2",
            data.GetFormula(implicitAddress));
        Assert.AreEqual(
            "=SUM(Sales[Amount])",
            summary.GetFormula(explicitAddress));
        Assert.IsTrue(data.TryGetTable(table.Id, out var restored));
        Assert.AreEqual("Amount", restored!.Columns[1].Name);

        Assert.IsTrue(session.Redo());
        Assert.AreEqual(
            "=[@NetAmount]*2",
            data.GetFormula(implicitAddress));
    }

    [TestMethod]
    public void FailedDuplicateColumnRenameIsAtomicAndNotRecorded()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var table = CreateCalculatedSalesTable(out var amountColumnId);
        worksheet.AddTable(table);
        var formulaAddress = new CellAddress(1, 2);
        worksheet.SetFormula(formulaAddress, "=[@Amount]*2");
        var session = new SpreadsheetSession(workbook);
        var beforeUndoCount = session.History.UndoCount;

        Assert.ThrowsExactly<ArgumentException>(() =>
            session.Tables.RenameColumn(
                table.Id,
                amountColumnId,
                "Category"));

        Assert.AreEqual(beforeUndoCount, session.History.UndoCount);
        Assert.AreEqual(
            "=[@Amount]*2",
            worksheet.GetFormula(formulaAddress));
        Assert.IsTrue(worksheet.TryGetTable(table.Id, out var restored));
        Assert.AreEqual("Amount", restored!.Columns[1].Name);
    }

    [TestMethod]
    public void FilterChangeParticipatesInHistoryAndRowProjection()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusColumnId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Item"),
                new SpreadsheetTableColumn(statusColumnId, "Status"),
            ]);
        worksheet.AddTable(table);
        worksheet.SetValue(new CellAddress(1, 1), "Open");
        worksheet.SetValue(new CellAddress(2, 1), "Closed");
        worksheet.SetValue(new CellAddress(3, 1), "Open");
        var session = new SpreadsheetSession(workbook);
        var filter = new TableAutoFilter([
            new TableFilterColumn(
                statusColumnId,
                [CellValue.FromText("Open")]),
        ]);

        session.Tables.SetAutoFilter(table.Id, filter);

        Assert.IsFalse(
            WorksheetSnapshot.Capture(worksheet)
                .IsRowVisible(2));
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(
            WorksheetSnapshot.Capture(worksheet)
                .IsRowVisible(2));
        Assert.IsTrue(session.Redo());
        Assert.IsFalse(
            WorksheetSnapshot.Capture(worksheet)
                .IsRowVisible(2));
    }

    [TestMethod]
    public void StructuralInsertUndoRedoMovesTableWithStableIdentity()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var table = CreateSalesTable();
        worksheet.AddTable(table);
        var session = new SpreadsheetSession(workbook);

        session.Structure.InsertRows(0, 2);

        Assert.IsTrue(worksheet.TryGetTable(table.Id, out var moved));
        Assert.AreEqual(2, moved!.Range.Top);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(worksheet.TryGetTable(table.Id, out var restored));
        Assert.AreEqual(0, restored!.Range.Top);
        Assert.IsTrue(session.Redo());
        Assert.IsTrue(worksheet.TryGetTable(table.Id, out var redone));
        Assert.AreEqual(2, redone!.Range.Top);
    }

    private static SpreadsheetTable CreateSalesTable() =>
        new(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Category"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Amount"),
            ]);

    private static SpreadsheetTable CreateCalculatedSalesTable(
        out Guid amountColumnId)
    {
        amountColumnId = Guid.NewGuid();
        return new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 2)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Category"),
                new SpreadsheetTableColumn(amountColumnId, "Amount"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Double"),
            ]);
    }

    private static void SetAmounts(Worksheet worksheet)
    {
        worksheet.SetValue(new CellAddress(1, 1), 1d);
        worksheet.SetValue(new CellAddress(2, 1), 2d);
        worksheet.SetValue(new CellAddress(3, 1), 3d);
    }
}
