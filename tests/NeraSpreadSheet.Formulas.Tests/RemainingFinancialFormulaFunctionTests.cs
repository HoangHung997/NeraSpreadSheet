using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class RemainingFinancialFormulaFunctionTests
{
    [TestMethod]
    public void RateMatchesExactReferenceAndZeroRateLimit()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            0.1d,
            EvaluateNumber(engine, "=RATE(1,-110,100)", context),
            2e-10d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(engine, "=RATE(10,-100,1000)", context),
            2e-11d);
    }

    [TestMethod]
    public void XnpvUsesActualDayFractionsAndCapturesBothRanges()
    {
        var values = CreateIrregularExactSchedule(0.1d, 180d);
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        var result = engine.Evaluate(
            "=XNPV(0.1,A1:A2,B1:B2)",
            context);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(0d, (double)result.Value.RawValue!, 2e-10d);
        Assert.AreEqual(2, result.Dependencies.Count);
    }

    [TestMethod]
    public void XirrRoundTripsIrregularScheduleThroughXnpv()
    {
        var values = CreateIrregularExactSchedule(0.125d, 217d);
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        var rate = EvaluateNumber(
            engine,
            "=XIRR(A1:A2,B1:B2)",
            context);

        Assert.AreEqual(0.125d, rate, 3e-9d);
        values[new CellAddress(0, 2)] = CellValue.FromNumber(rate);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=XNPV(C1,A1:A2,B1:B2)",
                new FormulaSurfaceTestContext(values)),
            2e-8d);
    }

    [TestMethod]
    public void XirrChoosesRootNearestGuessWhenMultipleRootsExist()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(-100d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(230d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(-132d),
            [new CellAddress(0, 1)] = CellValue.FromNumber(45000d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(45365d),
            [new CellAddress(2, 1)] = CellValue.FromNumber(45730d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            0.1d,
            EvaluateNumber(engine, "=XIRR(A1:A3,B1:B3,0.09)", context),
            3e-9d);
        Assert.AreEqual(
            0.2d,
            EvaluateNumber(engine, "=XIRR(A1:A3,B1:B3,0.21)", context),
            3e-9d);
    }

    [TestMethod]
    public void RemainingFinancialDomainsFailClosed()
    {
        var values = CreateIrregularExactSchedule(0.1d, 180d);
        var context = new FormulaSurfaceTestContext(values);
        var engine = new NeraFormulaEngine();

        AssertNumericError(engine, "=RATE(0,-100,1000)", context);
        AssertNumericError(engine, "=RATE(10,-100,1000,0,2)", context);
        AssertNumericError(engine, "=RATE(10,-100,1000,0,0,-1)", context);
        AssertNumericError(engine, "=XNPV(-1,A1:A2,B1:B2)", context);
        AssertNumericError(engine, "=XNPV(0.1,A1:A1,B1:B2)", context);
        AssertNumericError(engine, "=XIRR(A1:A2,B1:B1)", context);

        values[new CellAddress(1, 1)] = CellValue.FromNumber(44000d);
        AssertNumericError(
            engine,
            "=XIRR(A1:A2,B1:B2)",
            new FormulaSurfaceTestContext(values));
    }

    [TestMethod]
    public void RemainingFinancialDescriptorsAreVersionedPureAndBounded()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        foreach (var name in new[] { "RATE", "XNPV", "XIRR" })
        {
            var descriptor = registry.Descriptors.Single(candidate =>
                candidate.Identity.Name == name);
            Assert.AreEqual("NERA.BUILTIN", descriptor.Identity.Namespace);
            Assert.AreEqual(
                new FormulaFunctionVersion(1, 0, 0),
                descriptor.Version);
            Assert.AreEqual(
                FormulaFunctionArgumentCountPolicy.LogicalArguments,
                descriptor.ArgumentCountPolicy);
            Assert.AreEqual(
                FormulaFunctionVolatility.Deterministic,
                descriptor.Volatility);
            Assert.AreEqual(
                FormulaFunctionSecurityClassification.Pure,
                descriptor.SecurityClassification);
            Assert.IsTrue(descriptor.Capabilities.HasFlag(
                FormulaFunctionCapabilities.ReturnsScalar));
        }

        Assert.AreEqual(186, registry.Count);
        Assert.AreEqual(186, registry.VersionCount);
    }

    private static Dictionary<CellAddress, CellValue>
        CreateIrregularExactSchedule(double rate, double days)
    {
        const double presentValue = 1000d;
        var futureValue = presentValue * Math.Pow(
            1d + rate,
            days / 365d);
        return new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(-presentValue),
            [new CellAddress(1, 0)] = CellValue.FromNumber(futureValue),
            [new CellAddress(0, 1)] = CellValue.FromNumber(45000d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(45000d + days),
        };
    }

    private static void AssertNumericError(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        var result = engine.Evaluate(formula, context);
        Assert.AreEqual("#NUM!", result.Value.RawValue, formula);
    }

    private static double EvaluateNumber(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(
            result.IsSuccess,
            $"Expected success for {formula}, but received {result.Value}.");
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind);
        return (double)result.Value.RawValue!;
    }
}
