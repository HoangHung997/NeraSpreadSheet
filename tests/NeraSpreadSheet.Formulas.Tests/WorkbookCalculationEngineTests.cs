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
}
