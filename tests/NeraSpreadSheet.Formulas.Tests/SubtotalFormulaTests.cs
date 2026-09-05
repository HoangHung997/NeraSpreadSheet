using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class SubtotalFormulaTests
{
    [TestMethod]
    public void SubtotalExcludesFilteredRowsAndTracksFilterColumns()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusColumnId = Guid.NewGuid();
        var amountColumnId = Guid.NewGuid();
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 1)),
            [
                new SpreadsheetTableColumn(statusColumnId, "Status"),
                new SpreadsheetTableColumn(
                    amountColumnId,
                    "Amount",
                    totalsRowFormula: "=SUBTOTAL(109,Sales[Amount])"),
            ],
            hasTotalsRow: true,
            autoFilter: new TableAutoFilter([
                new TableFilterColumn(
                    statusColumnId,
                    [CellValue.FromText("Open")]),
            ]));
        worksheet.AddTable(table);
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(3, 0), "Open");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 1), 30d);
        var engine = new WorkbookCalculationEngine();

        engine.Recalculate(workbook);

        var totalsAddress = new CellAddress(4, 1);
        Assert.AreEqual(40d, worksheet.GetValue(totalsAddress));
        var dependencies = engine.DependencyGraph.GetDependencies(
            new FormulaCellKey(worksheet.Name, totalsAddress));
        Assert.IsTrue(dependencies.Any(dependency =>
            dependency.Range == new CellRange(
                new CellAddress(1, 0),
                new CellAddress(3, 0))));

        worksheet.SetValue(new CellAddress(2, 0), "Open");
        var result = engine.RecalculateAffected(
            workbook,
            worksheet,
            new CellRange(
                new CellAddress(2, 0),
                new CellAddress(2, 0)));

        Assert.AreEqual(1, result.FormulaCellCount);
        Assert.AreEqual(60d, worksheet.GetValue(totalsAddress));
    }

    [TestMethod]
    public void SubtotalSupportsCurrentAggregateFunctionCodes()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var statusColumnId = Guid.NewGuid();
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(statusColumnId, "Status"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Amount"),
            ],
            autoFilter: new TableAutoFilter([
                new TableFilterColumn(
                    statusColumnId,
                    [CellValue.FromText("Open")]),
            ])));
        worksheet.SetValue(new CellAddress(1, 0), "Open");
        worksheet.SetValue(new CellAddress(2, 0), "Closed");
        worksheet.SetValue(new CellAddress(3, 0), "Open");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 1), 30d);
        worksheet.SetFormula(new CellAddress(0, 3), "=SUBTOTAL(101,Sales[Amount])");
        worksheet.SetFormula(new CellAddress(1, 3), "=SUBTOTAL(102,Sales[Amount])");
        worksheet.SetFormula(new CellAddress(2, 3), "=SUBTOTAL(103,Sales[Amount])");
        worksheet.SetFormula(new CellAddress(3, 3), "=SUBTOTAL(104,Sales[Amount])");
        worksheet.SetFormula(new CellAddress(4, 3), "=SUBTOTAL(105,Sales[Amount])");

        new WorkbookCalculationEngine().Recalculate(workbook);

        Assert.AreEqual(20d, worksheet.GetValue(new CellAddress(0, 3)));
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(1, 3)));
        Assert.AreEqual(2d, worksheet.GetValue(new CellAddress(2, 3)));
        Assert.AreEqual(30d, worksheet.GetValue(new CellAddress(3, 3)));
        Assert.AreEqual(10d, worksheet.GetValue(new CellAddress(4, 3)));
    }
}
