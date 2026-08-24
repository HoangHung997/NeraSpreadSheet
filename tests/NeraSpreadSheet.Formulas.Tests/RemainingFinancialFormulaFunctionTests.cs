using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class RemainingFinancialFormulaFunctionTests
{
    private static readonly string[] RemainingFinancialNames =
    [
        "CUMIPMT",
        "CUMPRINC",
        "DB",
        "DDB",
        "VDB",
    ];

    [TestMethod]
    public void CumulativePaymentsMatchReferencesAndReconcile()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        var cumulativeInterest = EvaluateNumber(
            engine,
            "=CUMIPMT(0.09/12,30*12,125000,13,24,0)",
            context);
        var cumulativePrincipal = EvaluateNumber(
            engine,
            "=CUMPRINC(0.09/12,30*12,125000,13,24,0)",
            context);
        var payment = EvaluateNumber(
            engine,
            "=PMT(0.09/12,30*12,125000)",
            context);

        Assert.AreEqual(
            -11135.232130750845d,
            cumulativeInterest,
            2e-9d);
        Assert.AreEqual(
            -934.1071234208765d,
            cumulativePrincipal,
            2e-9d);
        Assert.AreEqual(
            payment * 12d,
            cumulativeInterest + cumulativePrincipal,
            2e-9d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=CUMIPMT(0.09/12,30*12,125000,1,1,1)",
                context),
            1e-14d);
    }

    [TestMethod]
    public void FixedDecliningBalanceMatchesReferencePeriods()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            186083.33333333334d,
            EvaluateNumber(
                engine,
                "=DB(1000000,100000,6,1,7)",
                context),
            2e-8d);
        Assert.AreEqual(
            15845.098473848071d,
            EvaluateNumber(
                engine,
                "=DB(1000000,100000,6,7,7)",
                context),
            2e-8d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=DB(1000,1000,5,1)",
                context),
            1e-14d);
    }

    [TestMethod]
    public void DoubleDecliningBalanceMatchesReferencePeriods()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            480d,
            EvaluateNumber(
                engine,
                "=DDB(2400,300,10,1)",
                context),
            1e-12d);
        Assert.AreEqual(
            306d,
            EvaluateNumber(
                engine,
                "=DDB(2400,300,10,2,1.5)",
                context),
            1e-12d);
        Assert.AreEqual(
            22.1225472000001d,
            EvaluateNumber(
                engine,
                "=DDB(2400,300,10,10)",
                context),
            2e-12d);
    }

    [TestMethod]
    public void VariableDecliningBalanceSupportsPartialPeriodsAndSwitching()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            1.3150684931506849d,
            EvaluateNumber(
                engine,
                "=VDB(2400,300,10*365,0,1)",
                context),
            2e-12d);
        Assert.AreEqual(
            396.3060532647509d,
            EvaluateNumber(
                engine,
                "=VDB(2400,300,10*12,6,18)",
                context),
            2e-10d);
        Assert.AreEqual(
            311.8089366582341d,
            EvaluateNumber(
                engine,
                "=VDB(2400,300,10*12,6,18,1.5)",
                context),
            2e-10d);
        Assert.AreEqual(
            315d,
            EvaluateNumber(
                engine,
                "=VDB(2400,300,10,0,0.875,1.5)",
                context),
            2e-12d);
        Assert.IsTrue(
            EvaluateNumber(
                engine,
                "=VDB(2400,300,10,0,10,1.5,FALSE())",
                context) >
            EvaluateNumber(
                engine,
                "=VDB(2400,300,10,0,10,1.5,TRUE())",
                context));
    }

    [TestMethod]
    public void RemainingFinancialDomainsAndCapabilitiesAreExplicit()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(2d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        AssertNumericError(
            engine,
            "=CUMIPMT(0,360,125000,1,12,0)",
            context);
        AssertNumericError(
            engine,
            "=CUMPRINC(0.01,360,125000,13,12,0)",
            context);
        AssertNumericError(
            engine,
            "=CUMIPMT(0.01,360,125000,1,12,2)",
            context);
        AssertNumericError(
            engine,
            "=DB(1000,100,5,1,0)",
            context);
        AssertNumericError(
            engine,
            "=DDB(1000,100,5,1,0)",
            context);
        AssertNumericError(
            engine,
            "=DDB(1000,1100,5,1)",
            context);
        AssertNumericError(
            engine,
            "=VDB(1000,100,5,3,2)",
            context);
        AssertNumericError(
            engine,
            "=VDB(1000,100,5,0,6)",
            context);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=DB(A1:A2,100,5,1)",
                context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=VDB(1000,100,5,0,1,2,\"maybe\")",
                context).ErrorCode);
    }

    [TestMethod]
    public void RemainingFinancialDescriptorsAreVersionedPureAndScalarOnly()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        foreach (var name in RemainingFinancialNames)
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

        Assert.AreEqual(191, registry.Count);
        Assert.AreEqual(191, registry.VersionCount);
    }

    [TestMethod]
    public void RemainingFinancialResourceBudgetsFailClosed()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        AssertNumericError(
            engine,
            "=CUMIPMT(0.01,3000000,1000,1,2000001,0)",
            context);
        AssertNumericError(
            engine,
            "=DB(1000,100,3000000,2000001)",
            context);
        AssertNumericError(
            engine,
            "=DDB(1000,100,3000000,2000001)",
            context);
        AssertNumericError(
            engine,
            "=VDB(1000,100,3000000,0,2000001)",
            context);
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
