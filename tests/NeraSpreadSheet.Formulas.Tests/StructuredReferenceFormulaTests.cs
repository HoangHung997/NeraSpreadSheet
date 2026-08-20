using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class StructuredReferenceFormulaTests
{
    [TestMethod]
    public void CalculationExpandsTableColumnIntoDependencyRange()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var table = CreateTable(
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            "Item",
            "Amount");
        worksheet.AddTable(table);
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 1), 30d);
        var formulaAddress = new CellAddress(0, 3);
        worksheet.SetFormula(
            formulaAddress,
            "=SUM(Sales[Amount])");

        var engine = new WorkbookCalculationEngine();
        var result = engine.Recalculate(workbook);

        Assert.AreEqual(1, result.FormulaCellCount);
        Assert.AreEqual(60d, worksheet.GetValue(formulaAddress));
        CollectionAssert.Contains(
            engine.DependencyGraph.GetDependencies(new FormulaCellKey(
                    worksheet.Name,
                    formulaAddress))
                .ToArray(),
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(1, 1),
                    new CellAddress(3, 1))));
    }

    [TestMethod]
    public void CurrentRowReferencesUseFormulaCellRow()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.AddTable(CreateTable(
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(2, 2)),
            "Quantity",
            "Price",
            "Total"));
        worksheet.SetValue(new CellAddress(1, 0), 2d);
        worksheet.SetValue(new CellAddress(1, 1), 5d);
        worksheet.SetValue(new CellAddress(2, 0), 3d);
        worksheet.SetValue(new CellAddress(2, 1), 7d);
        worksheet.SetFormula(
            new CellAddress(1, 2),
            "=[@Quantity]*[@Price]");
        worksheet.SetFormula(
            new CellAddress(2, 2),
            "=[@Quantity]*[@Price]");

        new WorkbookCalculationEngine().Recalculate(workbook);

        Assert.AreEqual(10d, worksheet.GetValue(new CellAddress(1, 2)));
        Assert.AreEqual(21d, worksheet.GetValue(new CellAddress(2, 2)));
    }

    [TestMethod]
    public void CrossSheetTableReferenceSupportsQuotedWorksheetName()
    {
        var workbook = new Workbook();
        var data = workbook.Worksheets[0];
        workbook.RenameWorksheet(data, "Data Sheet");
        var summary = workbook.AddWorksheet("Summary");
        data.AddTable(CreateTable(
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(2, 1)),
            "Item",
            "Amount"));
        data.SetValue(new CellAddress(1, 1), 4d);
        data.SetValue(new CellAddress(2, 1), 6d);
        summary.SetFormula(default, "=SUM(Sales[Amount])");

        new WorkbookCalculationEngine().Recalculate(workbook);

        Assert.AreEqual(10d, summary.GetValue(default));
    }

    [TestMethod]
    public void TranslatorSupportsAreasAndColumnRanges()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.AddTable(CreateTable(
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(4, 2)),
            "Item",
            "Amount",
            "Tax"));

        Assert.AreEqual(
            "=SUM($B$2:$C$5)",
            StructuredReferenceFormulaTranslator.Translate(
                "=SUM(Sales[[#Data],[Amount]:[Tax]])",
                workbook,
                worksheet,
                default));
        Assert.AreEqual(
            "=SUM($A$1:$C$5)",
            StructuredReferenceFormulaTranslator.Translate(
                "=SUM(Sales[#All])",
                workbook,
                worksheet,
                default));
    }

    [TestMethod]
    public void RewriterRenamesOnlyStructuredTokensOutsideStrings()
    {
        var formula =
            "=SUM(Sales[Amount])+\"Sales[Amount]\"+[@Amount]";

        var tableRenamed = StructuredReferenceFormulaRewriter.RenameTable(
            formula,
            "Sales",
            "Orders");
        var columnRenamed = StructuredReferenceFormulaRewriter.RenameColumn(
            tableRenamed,
            "Orders",
            "Amount",
            "Net Amount",
            rewriteImplicitReferences: true);

        Assert.AreEqual(
            "=SUM(Orders[Net Amount])+\"Sales[Amount]\"+[@Net Amount]",
            columnRenamed);
    }

    private static SpreadsheetTable CreateTable(
        string name,
        CellRange range,
        params string[] columnNames)
    {
        return new SpreadsheetTable(
            Guid.NewGuid(),
            name,
            range,
            columnNames.Select(columnName =>
                new SpreadsheetTableColumn(
                    Guid.NewGuid(),
                    columnName)));
    }
}
