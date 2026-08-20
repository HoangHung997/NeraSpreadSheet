using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class StructuredReferenceFormulaEngineTests
{
    [TestMethod]
    public void EvaluateExpandsTableColumnIntoExistingFormulaEngine()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var table = CreateTable();
        var catalog = new WorkbookTableCatalog(workbook);
        catalog.Add(worksheet, table);
        worksheet.SetValue(new CellAddress(1, 1), 1d);
        worksheet.SetValue(new CellAddress(2, 1), 2d);
        worksheet.SetValue(new CellAddress(3, 1), 3d);
        var engine = new StructuredReferenceFormulaEngine();

        var result = engine.Evaluate(
            "=SUM(Sales[Amount])",
            catalog,
            worksheet,
            new CellAddress(0, 3),
            new WorksheetContext(worksheet));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(6d, result.Value.RawValue);
        Assert.AreEqual("=SUM(B2:B4)", engine.Expand(
            "=SUM(Sales[Amount])",
            catalog,
            worksheet,
            new CellAddress(0, 3)));
    }

    [TestMethod]
    public void EvaluateThisRowUsesFormulaRowAndStableColumnIdentity()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var table = CreateTable();
        var catalog = new WorkbookTableCatalog(workbook);
        catalog.Add(worksheet, table);
        worksheet.SetValue(new CellAddress(2, 1), 7d);
        var engine = new StructuredReferenceFormulaEngine();

        var result = engine.Evaluate(
            "=[@Amount]*2",
            catalog,
            worksheet,
            new CellAddress(2, 0),
            new WorksheetContext(worksheet));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(14d, result.Value.RawValue);
    }

    private static SpreadsheetTable CreateTable() =>
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

    private sealed class WorksheetContext(Worksheet worksheet)
        : IFormulaEvaluationContext
    {
        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address)
        {
            if (worksheetName is not null &&
                !string.Equals(
                    worksheetName,
                    worksheet.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                return CellValue.FromError("#REF!");
            }

            return worksheet.GetValue(address);
        }
    }
}
