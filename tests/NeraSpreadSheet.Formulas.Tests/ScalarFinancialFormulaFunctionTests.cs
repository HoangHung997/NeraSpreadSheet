using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class ScalarFinancialFormulaFunctionTests
{
    private static readonly string[] ScalarFinancialNames =
    [
        "ISPMT",
        "EFFECT",
        "NOMINAL",
        "RRI",
        "PDURATION",
    ];

    [TestMethod]
    public void IspmtUsesZeroBasedEqualPrincipalSchedule()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            -64814.81481481482d,
            EvaluateNumber(
                engine,
                "=ISPMT(0.1/12,1,3*12,8000000)",
                context),
            2e-10d);
        Assert.AreEqual(
            -66666.66666666667d,
            EvaluateNumber(
                engine,
                "=ISPMT(0.1/12,0,3*12,8000000)",
                context),
            2e-10d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=ISPMT(0.1/12,36,36,8000000)",
                context),
            1e-14d);
        Assert.AreEqual(
            64814.81481481482d,
            EvaluateNumber(
                engine,
                "=ISPMT(0.1/12,1,3*12,-8000000)",
                context),
            2e-10d);
    }

    [TestMethod]
    public void EffectAndNominalMatchReferencesAndRoundTrip()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            0.05354266737075805d,
            EvaluateNumber(
                engine,
                "=EFFECT(0.0525,4)",
                context),
            2e-15d);
        Assert.AreEqual(
            0.05250031986835587d,
            EvaluateNumber(
                engine,
                "=NOMINAL(0.053543,4)",
                context),
            2e-15d);
        Assert.AreEqual(
            0.0525d,
            EvaluateNumber(
                engine,
                "=NOMINAL(EFFECT(0.0525,4),4)",
                context),
            2e-15d);
        Assert.AreEqual(
            0.053543d,
            EvaluateNumber(
                engine,
                "=EFFECT(NOMINAL(0.053543,4),4)",
                context),
            2e-15d);
        Assert.AreEqual(
            EvaluateNumber(
                engine,
                "=EFFECT(0.0525,4)",
                context),
            EvaluateNumber(
                engine,
                "=EFFECT(0.0525,4.9)",
                context),
            1e-15d);
    }

    [TestMethod]
    public void RriAndPdurationMatchReferencesAndRoundTrip()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            0.0009933073762913847d,
            EvaluateNumber(
                engine,
                "=RRI(96,10000,11000)",
                context),
            2e-15d);
        Assert.AreEqual(
            3.8598661626226414d,
            EvaluateNumber(
                engine,
                "=PDURATION(0.025,2000,2200)",
                context),
            2e-14d);
        Assert.AreEqual(
            87.60547641937576d,
            EvaluateNumber(
                engine,
                "=PDURATION(0.025/12,1000,1200)",
                context),
            2e-12d);
        Assert.AreEqual(
            96d,
            EvaluateNumber(
                engine,
                "=PDURATION(RRI(96,10000,11000),10000,11000)",
                context),
            2e-10d);
        Assert.AreEqual(
            0.025d,
            EvaluateNumber(
                engine,
                "=RRI(PDURATION(0.025,2000,2200),2000,2200)",
                context),
            2e-14d);
    }

    [TestMethod]
    public void ClosedFormRatesRemainStableNearZero()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            0.000000000001d,
            EvaluateNumber(
                engine,
                "=NOMINAL(EFFECT(0.000000000001,1000000),1000000)",
                context),
            2e-23d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=RRI(100,5000,5000)",
                context),
            1e-15d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=PDURATION(0.05,5000,5000)",
                context),
            1e-15d);
    }

    [TestMethod]
    public void ScalarFinancialHelperDomainsAndCapabilitiesAreExplicit()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(0.05d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(0.06d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        AssertNumericError(
            engine,
            "=ISPMT(0.01,-1,12,1000)",
            context);
        AssertNumericError(
            engine,
            "=ISPMT(0.01,13,12,1000)",
            context);
        AssertNumericError(
            engine,
            "=ISPMT(0.01,1,0,1000)",
            context);
        AssertNumericError(
            engine,
            "=EFFECT(0,4)",
            context);
        AssertNumericError(
            engine,
            "=EFFECT(0.05,0.9)",
            context);
        AssertNumericError(
            engine,
            "=NOMINAL(0,4)",
            context);
        AssertNumericError(
            engine,
            "=RRI(0,1000,1200)",
            context);
        AssertNumericError(
            engine,
            "=RRI(12,0,1200)",
            context);
        AssertNumericError(
            engine,
            "=PDURATION(0,1000,1200)",
            context);
        AssertNumericError(
            engine,
            "=PDURATION(0.05,0,1200)",
            context);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=EFFECT(A1:A2,4)",
                context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=PDURATION(\"bad\",1000,1200)",
                context).ErrorCode);
    }

    [TestMethod]
    public void ScalarFinancialDescriptorsAreVersionedPureAndScalarOnly()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        foreach (var name in ScalarFinancialNames)
        {
            var descriptor = registry.Descriptors.Single(candidate =>
                candidate.Identity.Name == name);
            Assert.AreEqual(
                "NERA.BUILTIN",
                descriptor.Identity.Namespace);
            Assert.AreEqual(
                new FormulaFunctionVersion(1, 0, 0),
                descriptor.Version);
            Assert.AreEqual(
                FormulaFunctionApiVersion.Current,
                descriptor.MinimumHostApiVersion);
            Assert.AreEqual(
                FormulaFunctionArgumentCountPolicy.LogicalArguments,
                descriptor.ArgumentCountPolicy);
            Assert.IsTrue(descriptor.Capabilities.HasFlag(
                FormulaFunctionCapabilities.ScalarArguments));
            Assert.IsFalse(descriptor.Capabilities.HasFlag(
                FormulaFunctionCapabilities.RangeArguments));
            Assert.IsTrue(descriptor.Capabilities.HasFlag(
                FormulaFunctionCapabilities.ReturnsScalar));
            Assert.AreEqual(
                FormulaFunctionVolatility.Deterministic,
                descriptor.Volatility);
            Assert.AreEqual(
                FormulaFunctionSecurityClassification.Pure,
                descriptor.SecurityClassification);
        }

        Assert.AreEqual(196, registry.Count);
        Assert.AreEqual(196, registry.VersionCount);
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
