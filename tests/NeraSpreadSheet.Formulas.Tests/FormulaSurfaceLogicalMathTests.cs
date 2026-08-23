using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FormulaSurfaceLogicalMathTests
{
    [TestMethod]
    public void LogicalAndInformationFunctionsUseSharedCoercion()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(true, engine.Evaluate(
            "=AND(TRUE,1)", context).Value.RawValue);
        Assert.AreEqual(true, engine.Evaluate(
            "=OR(FALSE,0,TRUE)", context).Value.RawValue);
        Assert.AreEqual(false, engine.Evaluate(
            "=XOR(TRUE,TRUE)", context).Value.RawValue);
        Assert.AreEqual(true, engine.Evaluate(
            "=NOT(FALSE)", context).Value.RawValue);
        Assert.AreEqual(true, engine.Evaluate(
            "=ISERROR(1/0)", context).Value.RawValue);
        Assert.AreEqual(true, engine.Evaluate(
            "=ISNA(NA())", context).Value.RawValue);
        Assert.AreEqual(false, engine.Evaluate(
            "=ISERR(NA())", context).Value.RawValue);
    }

    [TestMethod]
    public void MathAndRoundingFunctionsCoverPositiveAndNegativeValues()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(3.14d, Number(engine, context, "=ROUND(3.14159,2)"));
        Assert.AreEqual(-3.14d, Number(engine, context, "=ROUNDDOWN(-3.149,2)"));
        Assert.AreEqual(-3.15d, Number(engine, context, "=ROUNDUP(-3.141,2)"));
        Assert.AreEqual(-3d, Number(engine, context, "=TRUNC(-3.9)"));
        Assert.AreEqual(-4d, Number(engine, context, "=INT(-3.1)"));
        Assert.AreEqual(1d, Number(engine, context, "=MOD(-3,2)"));
        Assert.AreEqual(3d, Number(engine, context, "=QUOTIENT(7,2)"));
        Assert.AreEqual(-4d, Number(engine, context, "=EVEN(-3.1)"));
        Assert.AreEqual(5d, Number(engine, context, "=ODD(4.1)"));
        Assert.AreEqual(-3d, Number(engine, context, "=CEILING.MATH(-3.2)"));
        Assert.AreEqual(-4d, Number(engine, context, "=FLOOR.MATH(-3.2)"));
    }

    [TestMethod]
    public void AggregateAndTranscendentalFunctionsReturnFiniteResults()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(24d, Number(engine, context, "=PRODUCT(2,3,4)"));
        Assert.AreEqual(14d, Number(engine, context, "=SUMSQ(1,2,3)"));
        Assert.AreEqual(8d, Number(engine, context, "=POWER(2,3)"));
        Assert.AreEqual(3d, Number(engine, context, "=SQRT(9)"));
        Assert.AreEqual(2d, Number(engine, context, "=LOG(100,10)"));
        Assert.AreEqual(
            180d,
            Number(engine, context, "=DEGREES(PI())"),
            1e-10d);
        Assert.AreEqual(
            Math.PI,
            Number(engine, context, "=RADIANS(180)"),
            1e-10d);
        Assert.AreEqual(
            "#NUM!",
            engine.Evaluate("=SQRT(-1)", context).Value.RawValue);
    }

    [TestMethod]
    public void RegistryContainsTheExpandedScalarSurface()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        Assert.IsTrue(registry.Count >= 92);
        Assert.IsTrue(registry.TryResolve("ROUND", out _));
        Assert.IsTrue(registry.TryResolve("TEXTJOIN", out _));
        Assert.IsTrue(registry.TryResolve("EOMONTH", out _));
        Assert.IsTrue(registry.TryResolve("CEILING.MATH", out _));
        Assert.IsTrue(registry.TryResolve("ISERROR", out _));
    }

    private static double Number(
        NeraFormulaEngine engine,
        IFormulaEvaluationContext context,
        string formula)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(
            result.IsSuccess,
            $"{formula} returned {result.ErrorCode}.");
        return (double)result.Value.RawValue!;
    }
}
