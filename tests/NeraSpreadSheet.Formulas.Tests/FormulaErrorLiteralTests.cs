using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FormulaErrorLiteralTests
{
    [TestMethod]
    public void ReferenceErrorLiteralCanBeParsedAndCalculated()
    {
        var workbook = new Workbook();
        workbook.Worksheets[0].SetFormula(default, "=#REF!");
        var engine = new WorkbookCalculationEngine();

        engine.Recalculate(workbook);

        var value = workbook.Worksheets[0].GetCell(default).Value;
        Assert.AreEqual(CellValueKind.Error, value.Kind);
        Assert.AreEqual("#REF!", value.ToString());
    }
}
