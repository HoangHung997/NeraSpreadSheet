using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class IncrementalCalculationTests
{
    [TestMethod]
    public void RecalculateAffectedUpdatesOnlyDependencyChain()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(new CellAddress(0, 0), 2d);
        sheet.SetFormula(new CellAddress(0, 1), "=A1*2");
        sheet.SetFormula(new CellAddress(0, 2), "=B1+1");
        sheet.SetFormula(new CellAddress(0, 3), "=10+1");
        var engine = new WorkbookCalculationEngine();
        engine.Recalculate(workbook);

        sheet.SetValue(new CellAddress(0, 0), 4d);
        var result = engine.RecalculateAffected(
            workbook,
            sheet,
            new CellRange(new CellAddress(0, 0), new CellAddress(0, 0)));

        Assert.AreEqual(2, result.FormulaCellCount);
        Assert.AreEqual(8d, sheet.GetCell(new CellAddress(0, 1)).Value.RawValue);
        Assert.AreEqual(9d, sheet.GetCell(new CellAddress(0, 2)).Value.RawValue);
        Assert.AreEqual(11d, sheet.GetCell(new CellAddress(0, 3)).Value.RawValue);
    }
}
