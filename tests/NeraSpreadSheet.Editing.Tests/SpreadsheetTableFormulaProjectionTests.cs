using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetTableFormulaProjectionTests
{
    [TestMethod]
    public void AddProjectsCalculatedColumnAndTotalsWithUndoRedo()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        SetSourceValues(worksheet);
        var table = CreateCalculatedTable(
            out _,
            out var extendedColumnId);
        var session = new SpreadsheetSession(workbook);

        session.Tables.Add(table);

        AssertProjectedState(
            worksheet,
            table.Id,
            extendedColumnId);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(0, worksheet.TableCount);
        for (var row = 1; row <= 3; row++)
        {
            Assert.IsNull(worksheet.GetFormula(
                new CellAddress(row, 3)));
        }
        Assert.IsNull(worksheet.GetFormula(
            new CellAddress(4, 3)));
        Assert.IsNull(worksheet.GetValue(
            new CellAddress(4, 0)));

        Assert.IsTrue(session.Redo());
        AssertProjectedState(
            worksheet,
            table.Id,
            extendedColumnId);
    }

    [TestMethod]
    public void StructuralInsertProjectsFormulaIntoNewDataRow()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        SetSourceValues(worksheet);
        var table = CreateCalculatedTable(
            out _,
            out _);
        var session = new SpreadsheetSession(workbook);
        session.Tables.Add(table);

        session.Structure.InsertRows(2);

        Assert.IsTrue(worksheet.TryGetTable(
            table.Id,
            out var expanded));
        Assert.AreEqual(5, expanded!.Range.Bottom);
        Assert.AreEqual(
            "=[@Quantity]*[@Price]",
            worksheet.GetFormula(new CellAddress(2, 3)));
        Assert.AreEqual(
            "=SUBTOTAL(109,Sales[Extended])",
            worksheet.GetFormula(new CellAddress(5, 3)));

        Assert.IsTrue(session.Undo());
        Assert.IsTrue(worksheet.TryGetTable(
            table.Id,
            out var restored));
        Assert.AreEqual(4, restored!.Range.Bottom);
        Assert.AreEqual(
            "=SUBTOTAL(109,Sales[Extended])",
            worksheet.GetFormula(new CellAddress(4, 3)));
    }

    [TestMethod]
    public void MetadataCommandsPropagateAndTotalsRespectFilter()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusColumnId = Guid.NewGuid();
        var amountColumnId = Guid.NewGuid();
        var doubleColumnId = Guid.NewGuid();
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(3, 0), "Open");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 1), 30d);
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 2)),
            [
                new SpreadsheetTableColumn(
                    statusColumnId,
                    "Status",
                    totalsRowLabel: "Total"),
                new SpreadsheetTableColumn(
                    amountColumnId,
                    "Amount"),
                new SpreadsheetTableColumn(
                    doubleColumnId,
                    "Double"),
            ],
            hasTotalsRow: true);
        var session = new SpreadsheetSession(workbook);
        session.Tables.Add(table);

        session.Tables.SetCalculatedColumnFormula(
            table.Id,
            doubleColumnId,
            "=[@Amount]*2");
        session.Tables.SetTotalsRowFunction(
            table.Id,
            doubleColumnId,
            SpreadsheetTableTotalsFunction.Sum);
        session.Tables.SetAutoFilter(
            table.Id,
            new TableAutoFilter([
                new TableFilterColumn(
                    statusColumnId,
                    [CellValue.FromText("Open")]),
            ]));

        Assert.AreEqual(20d, worksheet.GetValue(
            new CellAddress(1, 2)));
        Assert.AreEqual(40d, worksheet.GetValue(
            new CellAddress(2, 2)));
        Assert.AreEqual(60d, worksheet.GetValue(
            new CellAddress(3, 2)));
        Assert.AreEqual(
            "=SUBTOTAL(109,Sales[Double])",
            worksheet.GetFormula(new CellAddress(4, 2)));
        Assert.AreEqual(80d, worksheet.GetValue(
            new CellAddress(4, 2)));

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(120d, worksheet.GetValue(
            new CellAddress(4, 2)));
        Assert.IsTrue(session.Undo());
        Assert.IsNull(worksheet.GetFormula(
            new CellAddress(4, 2)));
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(
            "=SUBTOTAL(109,Sales[Double])",
            worksheet.GetFormula(new CellAddress(4, 2)));
    }

    [TestMethod]
    public void OversizedProjectionRollsBackWithoutMaterializingRows()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var session = new SpreadsheetSession(workbook);
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Huge",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(
                    SpreadsheetLimits.MaxRows - 1,
                    0)),
            [
                new SpreadsheetTableColumn(
                    Guid.NewGuid(),
                    "Calculated",
                    "=ROW()"),
            ]);
        var undoCount = session.History.UndoCount;

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            session.Tables.Add(table));

        Assert.AreEqual(0, worksheet.TableCount);
        Assert.AreEqual(0, worksheet.UsedCellCount);
        Assert.AreEqual(undoCount, session.History.UndoCount);
    }

    private static SpreadsheetTable CreateCalculatedTable(
        out Guid quantityColumnId,
        out Guid extendedColumnId)
    {
        quantityColumnId = Guid.NewGuid();
        extendedColumnId = Guid.NewGuid();
        return new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 3)),
            [
                new SpreadsheetTableColumn(
                    Guid.NewGuid(),
                    "Item",
                    totalsRowLabel: "Total"),
                new SpreadsheetTableColumn(
                    quantityColumnId,
                    "Quantity"),
                new SpreadsheetTableColumn(
                    Guid.NewGuid(),
                    "Price"),
                new SpreadsheetTableColumn(
                    extendedColumnId,
                    "Extended",
                    calculatedColumnFormula:
                        "=[@Quantity]*[@Price]",
                    totalsRowFormula:
                        "=SUBTOTAL(109,Sales[Extended])"),
            ],
            hasTotalsRow: true);
    }

    private static void SetSourceValues(Worksheet worksheet)
    {
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(2, 0), "B");
        worksheet.SetValue(new CellAddress(3, 0), "C");
        worksheet.SetValue(new CellAddress(1, 1), 2d);
        worksheet.SetValue(new CellAddress(2, 1), 3d);
        worksheet.SetValue(new CellAddress(3, 1), 4d);
        worksheet.SetValue(new CellAddress(1, 2), 10d);
        worksheet.SetValue(new CellAddress(2, 2), 20d);
        worksheet.SetValue(new CellAddress(3, 2), 15d);
    }

    private static void AssertProjectedState(
        Worksheet worksheet,
        Guid tableId,
        Guid extendedColumnId)
    {
        Assert.IsTrue(worksheet.TryGetTable(
            tableId,
            out var projected));
        Assert.AreEqual(
            "=[@Quantity]*[@Price]",
            projected!.Columns.Single(column =>
                column.Id == extendedColumnId)
                .CalculatedColumnFormula);
        Assert.AreEqual(20d, worksheet.GetValue(
            new CellAddress(1, 3)));
        Assert.AreEqual(60d, worksheet.GetValue(
            new CellAddress(2, 3)));
        Assert.AreEqual(60d, worksheet.GetValue(
            new CellAddress(3, 3)));
        Assert.AreEqual("Total", worksheet.GetValue(
            new CellAddress(4, 0)));
        Assert.AreEqual(
            "=SUBTOTAL(109,Sales[Extended])",
            worksheet.GetFormula(new CellAddress(4, 3)));
        Assert.AreEqual(140d, worksheet.GetValue(
            new CellAddress(4, 3)));
    }
}
