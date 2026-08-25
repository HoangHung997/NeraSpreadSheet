using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class AdvancedMaturitySecurityFormulaFunctionTests
{
    private static readonly string[] FunctionNames =
    [
        "YIELDDISC",
        "PRICEMAT",
        "YIELDMAT",
        "ACCRINT",
        "FVSCHEDULE",
    ];

    [TestMethod]
    public void YielddiscMatchesReferenceAndInvertsPricedisc()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string settlement = "DATE(2008,2,16)";
        const string maturity = "DATE(2008,3,1)";

        Assert.AreEqual(
            0.05282257198685834d,
            EvaluateNumber(
                engine,
                $"=YIELDDISC({settlement},{maturity},99.795,100,2)",
                context),
            2e-14d);

        var discountedPrice = EvaluateNumber(
            engine,
            $"=PRICEDISC({settlement},{maturity},0.0525,100,2)",
            context);
        Assert.AreEqual(
            (100d - discountedPrice) /
            (discountedPrice * (14d / 360d)),
            EvaluateNumber(
                engine,
                $"=YIELDDISC({settlement},{maturity}," +
                $"PRICEDISC({settlement},{maturity},0.0525,100,2),100,2)",
                context),
            2e-14d);
    }

    [TestMethod]
    public void PricematAndYieldmatMatchPublishedReferencesAndRoundTrip()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            99.98449887555694d,
            EvaluateNumber(
                engine,
                "=PRICEMAT(DATE(2008,2,15),DATE(2008,4,13)," +
                "DATE(2007,11,11),0.061,0.061,0)",
                context),
            2e-13d);
        Assert.AreEqual(
            0.060954333691538576d,
            EvaluateNumber(
                engine,
                "=YIELDMAT(DATE(2008,3,15),DATE(2008,11,3)," +
                "DATE(2007,11,8),0.0625,100.0123,0)",
                context),
            2e-14d);
        Assert.AreEqual(
            0.061d,
            EvaluateNumber(
                engine,
                "=YIELDMAT(DATE(2008,2,15),DATE(2008,4,13)," +
                "DATE(2007,11,11),0.061," +
                "PRICEMAT(DATE(2008,2,15),DATE(2008,4,13)," +
                "DATE(2007,11,11),0.061,0.061,0),0)",
                context),
            2e-14d);
    }

    [TestMethod]
    public void AccrintMatchesReferenceExamplesAndCalculationMethod()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            16.666666666666668d,
            EvaluateNumber(
                engine,
                "=ACCRINT(DATE(2008,3,1),DATE(2008,8,31)," +
                "DATE(2008,5,1),0.1,1000,2,0)",
                context),
            2e-13d);
        Assert.AreEqual(
            15.555555555555555d,
            EvaluateNumber(
                engine,
                "=ACCRINT(DATE(2008,3,5),DATE(2008,8,31)," +
                "DATE(2008,5,1),0.1,1000,2,0,FALSE())",
                context),
            2e-13d);
        Assert.AreEqual(
            7.222222222222222d,
            EvaluateNumber(
                engine,
                "=ACCRINT(DATE(2008,4,5),DATE(2008,8,31)," +
                "DATE(2008,5,1),0.1,1000,2,0,TRUE())",
                context),
            2e-13d);
        Assert.AreEqual(
            25d,
            EvaluateNumber(
                engine,
                "=ACCRINT(DATE(2024,1,1),DATE(2024,7,1)," +
                "DATE(2024,10,1),0.1,1000,2,0,FALSE())",
                context),
            2e-13d);
    }

    [TestMethod]
    public void FvscheduleSupportsRangesBlanksAndDependencies()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(0.09d),
            [new CellAddress(1, 0)] = CellValue.Blank,
            [new CellAddress(2, 0)] = CellValue.FromNumber(0.11d),
            [new CellAddress(3, 0)] = CellValue.FromNumber(0.10d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        var result = engine.Evaluate("=FVSCHEDULE(1,A1:A4)", context);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(1.33089d, (double)result.Value.RawValue!, 2e-14d);
        Assert.AreEqual(1, result.Dependencies.Count);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 0)),
            result.Dependencies[0].Range);
    }

    [TestMethod]
    public void AdvancedMaturitySecurityDomainsAndCapabilitiesAreExplicit()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(0.05d),
            [new CellAddress(1, 0)] = CellValue.FromText("bad"),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        AssertNumericError(
            engine,
            "=YIELDDISC(DATE(2025,1,1),DATE(2024,1,1),95,100,0)",
            context);
        AssertNumericError(
            engine,
            "=YIELDDISC(DATE(2024,1,1),DATE(2025,1,1),0,100,0)",
            context);
        AssertNumericError(
            engine,
            "=PRICEMAT(DATE(2024,1,1),DATE(2025,1,1)," +
            "DATE(2023,1,1),-0.1,0.05,0)",
            context);
        AssertNumericError(
            engine,
            "=YIELDMAT(DATE(2024,1,1),DATE(2025,1,1)," +
            "DATE(2023,1,1),0.05,0,0)",
            context);
        AssertNumericError(
            engine,
            "=ACCRINT(DATE(2024,7,1),DATE(2024,7,1)," +
            "DATE(2024,10,1),0.1,1000,2,0)",
            context);
        AssertNumericError(
            engine,
            "=ACCRINT(DATE(2024,1,1),DATE(2024,7,1)," +
            "DATE(2024,10,1),0.1,1000,3,0)",
            context);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate("=FVSCHEDULE(1,A1:A2)", context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=YIELDDISC(A1:A2,DATE(2025,1,1),95,100,0)",
                context).ErrorCode);
    }

    [TestMethod]
    public void AdvancedMaturitySecurityDescriptorsExposeExpectedCapabilities()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        foreach (var name in FunctionNames)
        {
            var descriptor = registry.Descriptors.Single(candidate =>
                candidate.Identity.Name == name);
            Assert.AreEqual("NERA.BUILTIN", descriptor.Identity.Namespace);
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
            Assert.AreEqual(
                name == "FVSCHEDULE",
                descriptor.Capabilities.HasFlag(
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

        Assert.AreEqual(213, registry.Count);
        Assert.AreEqual(213, registry.VersionCount);
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
