using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FinancialFormulaFunctionTests
{
    private static readonly string[] FinancialFunctionNames =
    [
        "PV",
        "FV",
        "PMT",
        "NPER",
        "NPV",
        "IRR",
        "IPMT",
        "PPMT",
        "SLN",
        "SYD",
    ];

    [TestMethod]
    public void AnnuityFunctionsUseConsistentCashFlowSigns()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        var payment = EvaluateNumber(
            engine,
            "=PMT(0.05/12,60,10000)",
            context);
        Assert.AreEqual(-188.7123364401099d, payment, 1e-9d);
        Assert.AreEqual(
            10000d,
            EvaluateNumber(
                engine,
                "=PV(0.05/12,60,-188.7123364401099)",
                context),
            1e-8d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=FV(0.05/12,60,-188.7123364401099,10000)",
                context),
            1e-8d);
        Assert.AreEqual(
            60d,
            EvaluateNumber(
                engine,
                "=NPER(0.05/12,-188.7123364401099,10000)",
                context),
            1e-8d);

        Assert.AreEqual(
            -187.9292976996945d,
            EvaluateNumber(
                engine,
                "=PMT(0.05/12,60,10000,0,1)",
                context),
            1e-9d);
    }

    [TestMethod]
    public void ZeroRateAnnuitiesUseLinearFormulas()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(1000d, EvaluateNumber(engine, "=PV(0,10,-100)", context));
        Assert.AreEqual(0d, EvaluateNumber(engine, "=FV(0,10,-100,1000)", context));
        Assert.AreEqual(-100d, EvaluateNumber(engine, "=PMT(0,10,1000)", context));
        Assert.AreEqual(10d, EvaluateNumber(engine, "=NPER(0,-100,1000)", context));
        Assert.AreEqual(
            1000d,
            EvaluateNumber(engine, "=PV(0,\"10\",-100)", context));
    }

    [TestMethod]
    public void NpvAndIrrEvaluateOrderedCashFlows()
    {
        var values = CreateCashFlows(-10000d, 3000d, 4200d, 6800d);
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            11307.287753568744d,
            EvaluateNumber(engine, "=NPV(0.1,A2:A4)", context),
            1e-9d);
        Assert.AreEqual(
            11307.287753568744d,
            EvaluateNumber(engine, "=NPV(0.1,A2:A3,6800)", context),
            1e-9d);
        Assert.AreEqual(
            0.1634056006889894d,
            EvaluateNumber(engine, "=IRR(A1:A4)", context),
            1e-9d);
        Assert.AreEqual(
            0.1634056006889894d,
            EvaluateNumber(engine, "=IRR(A1:A4,0.5)", context),
            1e-9d);
    }

    [TestMethod]
    public void InterestAndPrincipalPaymentsReconcileToPmt()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string rate = "0.1/12";

        var payment = EvaluateNumber(
            engine,
            $"=PMT({rate},36,8000)",
            context);
        var firstInterest = EvaluateNumber(
            engine,
            $"=IPMT({rate},1,36,8000)",
            context);
        var firstPrincipal = EvaluateNumber(
            engine,
            $"=PPMT({rate},1,36,8000)",
            context);
        Assert.AreEqual(-258.13749755070063d, payment, 1e-9d);
        Assert.AreEqual(-66.66666666666667d, firstInterest, 1e-9d);
        Assert.AreEqual(-191.47083088403394d, firstPrincipal, 1e-9d);
        Assert.AreEqual(payment, firstInterest + firstPrincipal, 1e-10d);

        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                $"=IPMT({rate},1,36,8000,0,1)",
                context));
        Assert.AreEqual(
            -64.53329891831378d,
            EvaluateNumber(
                engine,
                $"=IPMT({rate},2,36,8000,0,1)",
                context),
            1e-9d);
    }

    [TestMethod]
    public void DepreciationFunctionsUseDocumentedPeriodRules()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            1800d,
            EvaluateNumber(engine, "=SLN(10000,1000,5)", context));
        Assert.AreEqual(
            3000d,
            EvaluateNumber(engine, "=SYD(10000,1000,5,1)", context));
        Assert.AreEqual(
            2400d,
            EvaluateNumber(engine, "=SYD(10000,1000,5,2)", context));
        Assert.AreEqual(
            600d,
            EvaluateNumber(engine, "=SYD(10000,1000,5,5)", context));
    }

    [TestMethod]
    public void FinancialDomainAndArgumentFailuresAreExplicit()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(CreateCashFlows(1d, 2d, 3d));

        Assert.AreEqual("#NUM!", engine.Evaluate("=PV(-1,10,-100)", context).Value.RawValue);
        Assert.AreEqual("#NUM!", engine.Evaluate("=PMT(0.1,0,1000)", context).Value.RawValue);
        Assert.AreEqual(
            FormulaErrorCode.DivisionByZero,
            engine.Evaluate("=NPER(0,0,1000)", context).ErrorCode);
        Assert.AreEqual("#NUM!", engine.Evaluate("=NPV(-1,A1:A3)", context).Value.RawValue);
        Assert.AreEqual("#NUM!", engine.Evaluate("=IRR(A1:A3)", context).Value.RawValue);
        Assert.AreEqual("#NUM!", engine.Evaluate("=IRR(A1:A3,-1)", context).Value.RawValue);
        Assert.AreEqual("#NUM!", engine.Evaluate("=IPMT(0.1,0,10,1000)", context).Value.RawValue);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate("=PMT(0.1,10,1000,0,2)", context).ErrorCode);
        Assert.AreEqual("#NUM!", engine.Evaluate("=SYD(1000,100,5,6)", context).Value.RawValue);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate("=NPV(A1:A2,A3)", context).ErrorCode);
    }

    [TestMethod]
    public void CashFlowRangesIgnoreNonNumericCellsButScalarTextMayCoerce()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(-100d),
            [new CellAddress(1, 0)] = CellValue.FromText("ignored"),
            [new CellAddress(2, 0)] = CellValue.FromBoolean(true),
            [new CellAddress(3, 0)] = CellValue.FromNumber(121d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            100d,
            EvaluateNumber(engine, "=NPV(0.1,A4)", context),
            1e-10d);
        Assert.AreEqual(
            0.1d,
            EvaluateNumber(engine, "=IRR(A1:A4)", context),
            1e-9d);
        Assert.AreEqual(
            100d,
            EvaluateNumber(engine, "=NPV(0.1,\"110\")", context),
            1e-10d);
    }

    [TestMethod]
    public void FinancialRangesEnterDependencyGraphAndRecalculateAffected()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), -1000d);
        worksheet.SetValue(new CellAddress(1, 0), 600d);
        worksheet.SetValue(new CellAddress(2, 0), 600d);
        var npvAddress = new CellAddress(0, 2);
        var irrAddress = new CellAddress(1, 2);
        worksheet.SetFormula(npvAddress, "=NPV(0.1,A2:A3)");
        worksheet.SetFormula(irrAddress, "=IRR(A1:A3)");
        var calculation = new WorkbookCalculationEngine();

        calculation.Recalculate(workbook);
        Assert.AreEqual(
            new CellRange(new CellAddress(1, 0), new CellAddress(2, 0)),
            calculation.DependencyGraph.GetDependencies(
                new FormulaCellKey(worksheet.Name, npvAddress)).Single().Range);
        Assert.AreEqual(
            new CellRange(new CellAddress(0, 0), new CellAddress(2, 0)),
            calculation.DependencyGraph.GetDependencies(
                new FormulaCellKey(worksheet.Name, irrAddress)).Single().Range);
        var previousNpv = (double)worksheet.GetValue(npvAddress)!;
        var previousIrr = (double)worksheet.GetValue(irrAddress)!;

        worksheet.SetValue(new CellAddress(2, 0), 900d);
        calculation.RecalculateAffected(
            workbook,
            worksheet,
            new CellRange(new CellAddress(2, 0), new CellAddress(2, 0)));

        Assert.AreNotEqual(previousNpv, (double)worksheet.GetValue(npvAddress)!);
        Assert.AreNotEqual(previousIrr, (double)worksheet.GetValue(irrAddress)!);
    }

    [TestMethod]
    public void FinancialDescriptorsUseVersionedLogicalArgumentPolicy()
    {
        var registry = new BuiltInFormulaFunctionRegistry();
        var descriptors = registry.Descriptors;

        foreach (var name in FinancialFunctionNames)
        {
            var descriptor = descriptors.Single(candidate =>
                string.Equals(candidate.Identity.Name, name, StringComparison.Ordinal));
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
            Assert.IsTrue(descriptor.Capabilities.HasFlag(
                FormulaFunctionCapabilities.ReturnsScalar));
            Assert.AreEqual(
                name is "NPV" or "IRR",
                descriptor.Capabilities.HasFlag(
                    FormulaFunctionCapabilities.RangeArguments));
            Assert.AreEqual(
                FormulaFunctionVolatility.Deterministic,
                descriptor.Volatility);
            Assert.AreEqual(
                FormulaFunctionSecurityClassification.Pure,
                descriptor.SecurityClassification);
        }
    }

    [TestMethod]
    public void IrrRejectsCashFlowVectorsBeyondItsIterationBudget()
    {
        var registry = new BuiltInFormulaFunctionRegistry();
        Assert.IsTrue(registry.TryResolve("IRR", out var resolved));
        var function = (IVersionedFormulaFunction)resolved;
        var values = Enumerable.Range(0, FinancialFormulaFunctions.MaximumIrrValues + 1)
            .Select(index => CellValue.FromNumber(index == 0 ? -1d : 1d))
            .ToArray();
        var argument = FormulaFunctionArgument.Range(
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(values.Length - 1, 0))),
            values);

        var result = function.Invoke(new FormulaFunctionInvocation(
            [argument],
            new FormulaSurfaceTestContext()));

        Assert.AreEqual("#NUM!", result.Value.RawValue);
    }

    private static Dictionary<CellAddress, CellValue> CreateCashFlows(
        params double[] values) =>
        values
            .Select((value, index) => new KeyValuePair<CellAddress, CellValue>(
                new CellAddress(index, 0),
                CellValue.FromNumber(value)))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value);

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
