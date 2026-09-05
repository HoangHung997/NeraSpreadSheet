using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class WorkbookCalculationEngineTests
{
    [TestMethod]
    public void RecalculateEvaluatesFormulaDependencyChain()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), 4d);
        sheet.SetFormula(new CellAddress(0, 1), "=A1*2");
        sheet.SetFormula(new CellAddress(0, 2), "=B1+1");
        var result = new WorkbookCalculationEngine().Recalculate(workbook);
        Assert.AreEqual(2, result.FormulaCellCount);
        Assert.AreEqual(8d, sheet.GetCell(new CellAddress(0, 1)).Value.RawValue);
        Assert.AreEqual(9d, sheet.GetCell(new CellAddress(0, 2)).Value.RawValue);
    }

    [TestMethod]
    public void RecalculateDetectsCircularReference()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetFormula(new CellAddress(0, 0), "=B1");
        sheet.SetFormula(new CellAddress(0, 1), "=A1");
        var result = new WorkbookCalculationEngine().Recalculate(workbook);
        Assert.IsTrue(result.ErrorCellCount > 0);
        Assert.AreEqual(CellValueKind.Error, sheet.GetCell(new CellAddress(0, 0)).Value.Kind);
    }

    [TestMethod]
    public void RecalculateStructuredReferenceTracksExpandedRangeDependency()
    {
        var workbook = CreateStructuredReferenceWorkbook(out var sheet);
        var formulaAddress = new CellAddress(0, 3);
        sheet.SetFormula(formulaAddress, "=SUM(Sales[Amount])");
        var engine = new WorkbookCalculationEngine();

        var result = engine.Recalculate(workbook);

        Assert.AreEqual(1, result.FormulaCellCount);
        Assert.AreEqual(6d, sheet.GetCell(formulaAddress).Value.RawValue);
        var dependencies = engine.DependencyGraph.GetDependencies(
            new FormulaCellKey(sheet.Name, formulaAddress));
        Assert.AreEqual(1, dependencies.Count);
        Assert.IsNull(dependencies[0].WorksheetName);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(1, 1),
                new CellAddress(3, 1)),
            dependencies[0].Range);
    }

    [TestMethod]
    public void RecalculateAffectedUpdatesStructuredFormulaAfterTableCellEdit()
    {
        var workbook = CreateStructuredReferenceWorkbook(out var sheet);
        var formulaAddress = new CellAddress(0, 3);
        var changedAddress = new CellAddress(2, 1);
        sheet.SetFormula(formulaAddress, "=SUM(Sales[Amount])");
        var engine = new WorkbookCalculationEngine();
        engine.Recalculate(workbook);

        sheet.SetValue(changedAddress, 10d);
        var result = engine.RecalculateAffected(
            workbook,
            sheet,
            new CellRange(changedAddress, changedAddress));

        Assert.AreEqual(1, result.FormulaCellCount);
        Assert.AreEqual(14d, sheet.GetCell(formulaAddress).Value.RawValue);
    }

    [TestMethod]
    public void RecalculateVlookupIgnoresErrorsOutsideMatchedLookupPath()
    {
        var workbook = new Workbook();
        var data = workbook.Worksheets[0];
        workbook.RenameWorksheet(data, "TTC");
        var summary = workbook.AddWorksheet("HSHC");
        data.SetValue(new CellAddress(2, 1), "Fields");
        data.SetValue(new CellAddress(2, 3), "Expected");
        data.SetValue(new CellAddress(3, 1), "Other");
        data.SetValue(new CellAddress(3, 3), CellValue.FromError("#VALUE!"));
        summary.SetValue(new CellAddress(5, 0), "Fields");
        summary.SetFormula(
            new CellAddress(5, 2),
            "=VLOOKUP($A6,TTC!$B$3:$D$4,COLUMN(C$1),FALSE)");

        var result = new WorkbookCalculationEngine().Recalculate(workbook);

        Assert.AreEqual(1, result.FormulaCellCount);
        Assert.AreEqual(0, result.ErrorCellCount);
        Assert.AreEqual(
            "Expected",
            summary.GetCell(new CellAddress(5, 2)).Value.RawValue);
    }

    [TestMethod]
    public void RecalculateVlookupSupportsBoundedWholeColumnReference()
    {
        var workbook = new Workbook();
        var data = workbook.Worksheets[0];
        workbook.RenameWorksheet(data, "DL");
        var summary = workbook.AddWorksheet("TTC");
        var sparseRow = SpreadsheetLimits.MaxRows - 1;
        data.SetValue(new CellAddress(sparseRow, 1), "Vendor");
        data.SetValue(new CellAddress(sparseRow, 2), "Expected");
        summary.SetValue(new CellAddress(0, 2), "Vendor");
        summary.SetFormula(
            new CellAddress(1, 2),
            "=VLOOKUP($C$1,DL!$B:$R,2,0)");
        var engine = new WorkbookCalculationEngine();

        var result = engine.Recalculate(workbook);

        Assert.AreEqual(1, result.FormulaCellCount);
        Assert.AreEqual(0, result.ErrorCellCount);
        Assert.AreEqual(
            "Expected",
            summary.GetCell(new CellAddress(1, 2)).Value.RawValue);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 1),
                new CellAddress(SpreadsheetLimits.MaxRows - 1, 17)),
            engine.DependencyGraph.GetDependencies(
                    new FormulaCellKey("TTC", new CellAddress(1, 2)))
                .Single(dependency => dependency.WorksheetName == "DL")
                .Range);
    }

    private static Workbook CreateStructuredReferenceWorkbook(
        out Worksheet worksheet)
    {
        var workbook = new Workbook();
        worksheet = workbook.Worksheets[0];
        worksheet.AddTable(new SpreadsheetTable(
            Guid.NewGuid(),
            "Sales",
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1)),
            [
                new SpreadsheetTableColumn(Guid.NewGuid(), "Category"),
                new SpreadsheetTableColumn(Guid.NewGuid(), "Amount"),
            ]));
        worksheet.SetValue(new CellAddress(1, 1), 1d);
        worksheet.SetValue(new CellAddress(2, 1), 2d);
        worksheet.SetValue(new CellAddress(3, 1), 3d);
        return workbook;
    }
}
