using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class StructuredReferenceHardeningTests
{
    [TestMethod]
    public void RenameShouldMatchWholeColumnTokenAndIgnoreLiterals()
    {
        const string formula = "=[@Amount]+[@AmountTax]+Sales[[#Data],[Amount]]+\"[@Amount]\"";
        Assert.AreEqual("=[@Net]+[@AmountTax]+Sales[[#Data],[Net]]+\"[@Amount]\"",
            StructuredReferenceFormulaRewriter.RenameColumn(formula, "Sales", "Amount", "Net", true));
    }

    [TestMethod]
    [DataRow("Sales[[Amount],[Tax]]")]
    [DataRow("Sales[[#Headers],[#Totals]]")]
    [DataRow("Sales[[#Data],]")]
    public void UnsupportedSelectorsShouldNotSilentlyBecomeContiguousRange(string reference)
    {
        var workbook = CreateWorkbook();
        Assert.AreEqual("=#REF!", StructuredReferenceFormulaTranslator.Translate(
            "=" + reference, workbook, workbook.Worksheets[0], new CellAddress(1, 0)));
    }

    [TestMethod]
    public void CurrentRowShouldNotBindToSameRowOnAnotherWorksheet()
    {
        var workbook = CreateWorkbook();
        var summary = workbook.AddWorksheet("Summary");
        Assert.AreEqual("=#REF!", StructuredReferenceFormulaTranslator.Translate(
            "=Sales[@Amount]", workbook, summary, new CellAddress(1, 0)));
    }

    [TestMethod]
    [DataRow("Net, cost")]
    [DataRow("Net:cost")]
    public void RenameAndTotalsShouldHandleColumnSeparatorCharacters(string name)
    {
        var workbook = CreateWorkbook();
        var sheet = workbook.Worksheets[0];
        var table = sheet.Tables.Single();
        var formula = StructuredReferenceFormulaRewriter.RenameColumn("=Sales[Amount]", "Sales", "Amount", name, false);
        sheet.RenameTableColumn(table.Id, table.Columns[0].Id, name);
        Assert.AreEqual("=$A$2:$A$4", StructuredReferenceFormulaTranslator.Translate(formula, workbook, sheet, default));
        var totals = SpreadsheetTableFormulaProjection.CreateTotalsFormula(sheet.Tables.Single(), table.Columns[0].Id,
            SpreadsheetTableTotalsFunction.Sum)!;
        Assert.AreEqual("=SUBTOTAL(109,$A$2:$A$4)", StructuredReferenceFormulaTranslator.Translate(totals, workbook, sheet, default));
    }

    [TestMethod]
    public void EscapedColumnShouldRoundTripThroughRenameAndExpansion()
    {
        var workbook = CreateWorkbook();
        var sheet = workbook.Worksheets[0];
        var table = sheet.Tables.Single();
        sheet.RenameTableColumn(table.Id, table.Columns[0].Id, "Cost [net]#'@");
        const string escaped = "Cost '[net']'#'''@";
        var formula = "=Sales[" + escaped + "]";
        Assert.AreEqual("=$A$2:$A$4", StructuredReferenceFormulaTranslator.Translate(
            formula, workbook, sheet, new CellAddress(1, 0)));
        Assert.AreEqual("=Sales[Net]", StructuredReferenceFormulaRewriter.RenameColumn(
            formula, "Sales", "Cost [net]#'@", "Net", false));
    }

    private static Workbook CreateWorkbook()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].AddTable(new SpreadsheetTable(Guid.NewGuid(), "Sales",
            new CellRange(default, new CellAddress(3, 1)),
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Amount"), new SpreadsheetTableColumn(Guid.NewGuid(), "Tax")]));
        return workbook;
    }
}
