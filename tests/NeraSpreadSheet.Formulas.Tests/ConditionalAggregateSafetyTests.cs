using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class ConditionalAggregateSafetyTests
{
    [TestMethod]
    public void MultipleCriteriaRangesShareOneBoundedScanBudget()
    {
        var result = new NeraFormulaEngine().Evaluate(
            "=COUNTIFS(A1:A1048576,\">0\",B1:B1048576,\">0\",C1:C1048576,\">0\")",
            new FormulaSurfaceTestContext());

        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            result.ErrorCode);
        Assert.AreEqual("#NUM!", result.Value.RawValue);
    }

    [TestMethod]
    public void SumIfsBudgetIncludesAggregateAndCriteriaPasses()
    {
        var result = new NeraFormulaEngine().Evaluate(
            "=SUMIFS(A1:A1048576,B1:B1048576,\">0\")",
            new FormulaSurfaceTestContext());

        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            result.ErrorCode);
        Assert.AreEqual("#NUM!", result.Value.RawValue);
    }
}
