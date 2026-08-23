using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FormulaAggregateSemanticsTests
{
    [TestMethod]
    public void NumericAggregatesPropagateFormulaErrors()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            FormulaErrorCode.NotAvailable,
            engine.Evaluate("=SUM(1,NA())", context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.NotAvailable,
            engine.Evaluate("=AVERAGE(1,NA())", context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.NotAvailable,
            engine.Evaluate("=MIN(1,NA())", context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.NotAvailable,
            engine.Evaluate("=MAX(1,NA())", context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.NotAvailable,
            engine.Evaluate("=PRODUCT(2,NA())", context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.NotAvailable,
            engine.Evaluate("=SUMSQ(2,NA())", context).ErrorCode);
    }

    [TestMethod]
    public void EmptyNumericSetsUseFunctionSpecificResults()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            0d,
            engine.Evaluate("=SUM(\"text\")", context).Value.RawValue);
        Assert.AreEqual(
            0d,
            engine.Evaluate("=MIN(\"text\")", context).Value.RawValue);
        Assert.AreEqual(
            0d,
            engine.Evaluate("=MAX(\"text\")", context).Value.RawValue);
        Assert.AreEqual(
            FormulaErrorCode.DivisionByZero,
            engine.Evaluate("=AVERAGE(\"text\")", context).ErrorCode);
    }

    [TestMethod]
    public void CountingFunctionsKeepTheirNonPropagatingContracts()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            1d,
            engine.Evaluate("=COUNT(1,NA(),\"text\")", context).Value.RawValue);
        Assert.AreEqual(
            3d,
            engine.Evaluate("=COUNTA(1,NA(),\"text\")", context).Value.RawValue);
        Assert.AreEqual(
            0d,
            engine.Evaluate("=COUNTBLANK(1,NA(),\"text\")", context).Value.RawValue);
    }
}
