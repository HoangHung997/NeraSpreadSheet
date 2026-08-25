using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class MaturitySecurityFormulaFunctionTests
{
    private static readonly string[] FunctionNames =
    [
        "ACCRINTM",
        "DISC",
        "INTRATE",
        "RECEIVED",
        "PRICEDISC",
    ];

    [TestMethod]
    public void AccrintmMatchesReferenceAndSupportsDefaultPar()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            20.54794520547945d,
            EvaluateNumber(
                engine,
                "=ACCRINTM(DATE(2008,4,1),DATE(2008,6,15),0.1,1000,3)",
                context),
            2e-13d);
        Assert.AreEqual(
            50d,
            EvaluateNumber(
                engine,
                "=ACCRINTM(DATE(2024,1,1),DATE(2024,7,1),0.1)",
                context),
            2e-13d);
    }

    [TestMethod]
    public void IntrateAndReceivedMatchPublishedReferences()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string dates =
            "DATE(2008,2,15),DATE(2008,5,15)";

        Assert.AreEqual(
            0.05768d,
            EvaluateNumber(
                engine,
                $"=INTRATE({dates},1000000,1014420,2)",
                context),
            2e-14d);
        Assert.AreEqual(
            1014584.6544071021d,
            EvaluateNumber(
                engine,
                $"=RECEIVED({dates},1000000,0.0575,2)",
                context),
            2e-8d);
    }

    [TestMethod]
    public void PricediscMatchesReferenceAndDiscRoundTrips()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string settlement = "DATE(2008,2,16)";
        const string maturity = "DATE(2008,3,1)";

        Assert.AreEqual(
            99.79583333333333d,
            EvaluateNumber(
                engine,
                $"=PRICEDISC({settlement},{maturity},0.0525,100,2)",
                context),
            2e-13d);
        Assert.AreEqual(
            0.0525d,
            EvaluateNumber(
                engine,
                $"=DISC({settlement},{maturity}," +
                $"PRICEDISC({settlement},{maturity},0.0525,100,2),100,2)",
                context),
            2e-14d);
    }

    [TestMethod]
    public void MaturitySecurityFunctionsRespectBasisTruncation()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            EvaluateNumber(
                engine,
                "=DISC(DATE(2024,1,1),DATE(2024,7,1),97,100,2)",
                context),
            EvaluateNumber(
                engine,
                "=DISC(DATE(2024,1,1),DATE(2024,7,1),97,100,2.9)",
                context),
            1e-15d);
    }

    [TestMethod]
    public void MaturitySecurityDomainsAndScalarCapabilitiesAreExplicit()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] =
                CellValue.FromDateTime(new DateTime(2024, 1, 1)),
            [new CellAddress(1, 0)] =
                CellValue.FromDateTime(new DateTime(2024, 7, 1)),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        AssertNumericError(
            engine,
            "=ACCRINTM(DATE(2024,7,1),DATE(2024,1,1),0.05,1000,0)",
            context);
        AssertNumericError(
            engine,
            "=ACCRINTM(DATE(2024,1,1),DATE(2024,7,1),0,1000,0)",
            context);
        AssertNumericError(
            engine,
            "=DISC(DATE(2024,1,1),DATE(2024,7,1),0,100,0)",
            context);
        AssertNumericError(
            engine,
            "=INTRATE(DATE(2024,1,1),DATE(2024,7,1),1000,0,0)",
            context);
        AssertNumericError(
            engine,
            "=RECEIVED(DATE(2024,1,1),DATE(2025,1,1),1000,1,0)",
            context);
        AssertNumericError(
            engine,
            "=PRICEDISC(DATE(2024,1,1),DATE(2025,1,1),1,100,0)",
            context);
        AssertNumericError(
            engine,
            "=PRICEDISC(DATE(2024,1,1),DATE(2024,7,1),0.05,100,5)",
            context);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=DISC(A1:A2,DATE(2025,1,1),95,100,0)",
                context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=INTRATE(\"bad\",DATE(2025,1,1),1000,1100,0)",
                context).ErrorCode);
    }

    [TestMethod]
    public void MaturitySecurityDescriptorsAreVersionedPureAndScalarOnly()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        foreach (var name in FunctionNames)
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

        Assert.AreEqual(
            BuiltInFormulaTestCounts.EagerVersioned,
            registry.Count);
        Assert.AreEqual(
            BuiltInFormulaTestCounts.EagerVersioned,
            registry.VersionCount);
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
