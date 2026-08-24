using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class StatisticalDistributionFunctionTests
{
    private static readonly string[] DistributionFunctionNames =
    [
        "STANDARDIZE",
        "FISHER",
        "FISHERINV",
        "NORM.DIST",
        "NORM.S.DIST",
        "NORM.INV",
        "NORM.S.INV",
        "LOGNORM.DIST",
        "LOGNORM.INV",
        "EXPON.DIST",
        "BINOM.DIST",
        "POISSON.DIST",
        "WEIBULL.DIST",
        "BETA.DIST",
        "BETA.INV",
        "GAMMA.DIST",
        "GAMMA.INV",
        "CHISQ.DIST",
        "CHISQ.DIST.RT",
        "CHISQ.INV",
        "CHISQ.INV.RT",
        "T.DIST",
        "T.DIST.RT",
        "T.DIST.2T",
        "T.INV",
        "T.INV.2T",
        "F.DIST",
        "F.DIST.RT",
        "F.INV",
        "F.INV.RT",
    ];

    [TestMethod]
    public void NormalDensityCumulativeAndInverseAreConsistent()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            0.5d,
            EvaluateNumber(engine, "=NORM.S.DIST(0,TRUE())", context),
            2e-8d);
        Assert.AreEqual(
            0.3989422804014327d,
            EvaluateNumber(engine, "=NORM.S.DIST(0,FALSE())", context),
            1e-15d);
        Assert.AreEqual(
            0.8413447460685429d,
            EvaluateNumber(engine, "=NORM.DIST(12,10,2,TRUE())", context),
            2e-7d);
        Assert.AreEqual(
            0.12098536225957168d,
            EvaluateNumber(engine, "=NORM.DIST(12,10,2,FALSE())", context),
            1e-15d);
        Assert.AreEqual(
            1.959963984540054d,
            EvaluateNumber(engine, "=NORM.S.INV(0.975)", context),
            2e-6d);
        Assert.AreEqual(
            42d,
            EvaluateNumber(
                engine,
                "=NORM.INV(NORM.DIST(42,40,1.5,TRUE()),40,1.5)",
                context),
            3e-6d);
    }

    [TestMethod]
    public void LogNormalExponentialAndWeibullReturnDensityAndCumulativeValues()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        var e = Math.E.ToString("R", System.Globalization.CultureInfo.InvariantCulture);

        Assert.AreEqual(
            0.5d,
            EvaluateNumber(
                engine,
                $"=LOGNORM.DIST({e},1,0.5,TRUE())",
                context),
            2e-8d);
        Assert.AreEqual(
            Math.E,
            EvaluateNumber(
                engine,
                "=LOGNORM.INV(0.5,1,0.5)",
                context),
            2e-7d);
        Assert.AreEqual(
            1d - Math.Exp(-1d),
            EvaluateNumber(engine, "=EXPON.DIST(2,0.5,TRUE())", context),
            1e-15d);
        Assert.AreEqual(
            0.5d * Math.Exp(-1d),
            EvaluateNumber(engine, "=EXPON.DIST(2,0.5,FALSE())", context),
            1e-15d);
        Assert.AreEqual(
            1d - Math.Exp(-0.125d),
            EvaluateNumber(engine, "=WEIBULL.DIST(2,3,4,TRUE())", context),
            1e-15d);
        Assert.AreEqual(
            0.1875d * Math.Exp(-0.125d),
            EvaluateNumber(engine, "=WEIBULL.DIST(2,3,4,FALSE())", context),
            1e-15d);
    }

    [TestMethod]
    public void BinomialAndPoissonSupportMassAndCumulativeModes()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            0.375d,
            EvaluateNumber(engine, "=BINOM.DIST(2,4,0.5,FALSE())", context),
            5e-15d);
        Assert.AreEqual(
            0.6875d,
            EvaluateNumber(engine, "=BINOM.DIST(2,4,0.5,TRUE())", context),
            5e-15d);
        Assert.AreEqual(
            0.22404180765538775d,
            EvaluateNumber(engine, "=POISSON.DIST(2,3,FALSE())", context),
            1e-14d);
        Assert.AreEqual(
            0.42319008112684353d,
            EvaluateNumber(engine, "=POISSON.DIST(2,3,TRUE())", context),
            2e-13d);
    }

    [TestMethod]
    public void DistributionDomainsAndScalarCapabilitiesAreEnforced()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(0.5d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(0.75d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        AssertNumericError(engine, "=NORM.INV(0,0,1)", context);
        AssertNumericError(engine, "=NORM.INV(1,0,1)", context);
        AssertNumericError(engine, "=NORM.DIST(0,0,0,TRUE())", context);
        AssertNumericError(engine, "=LOGNORM.DIST(0,0,1,TRUE())", context);
        AssertNumericError(engine, "=EXPON.DIST(-1,1,TRUE())", context);
        AssertNumericError(engine, "=BINOM.DIST(5,4,0.5,TRUE())", context);
        AssertNumericError(engine, "=POISSON.DIST(-1,3,TRUE())", context);
        AssertNumericError(engine, "=WEIBULL.DIST(1,0,2,TRUE())", context);
        AssertNumericError(
            engine,
            "=BINOM.DIST(1500000,3000000,0.5,TRUE())",
            context);

        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate("=NORM.S.INV(A1:A2)", context).ErrorCode);
    }

    [TestMethod]
    public void DistributionDescriptorsArePureDeterministicAndScalarOnly()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        foreach (var name in DistributionFunctionNames)
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
    }

    [TestMethod]
    public void RegistryCountIncludesAllAdvancedStatisticalNames()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        Assert.AreEqual(183, registry.Count);
        Assert.AreEqual(183, registry.VersionCount);
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
