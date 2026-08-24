using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FinancialCalendarFormulaFunctionTests
{
    private static readonly string[] FinancialCalendarNames =
    [
        "YEARFRAC",
        "COUPDAYBS",
        "COUPDAYS",
        "COUPDAYSNC",
        "COUPNCD",
        "COUPPCD",
        "COUPNUM",
    ];

    [TestMethod]
    public void YearFracSupportsEveryBasisAndSignedIntervals()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            0.5805555555555556d,
            EvaluateNumber(
                engine,
                "=YEARFRAC(DATE(2012,1,1),DATE(2012,7,30),0)",
                context),
            2e-15d);
        Assert.AreEqual(
            0.5765027322404371d,
            EvaluateNumber(
                engine,
                "=YEARFRAC(DATE(2012,1,1),DATE(2012,7,30),1)",
                context),
            2e-15d);
        Assert.AreEqual(
            0.5861111111111111d,
            EvaluateNumber(
                engine,
                "=YEARFRAC(DATE(2012,1,1),DATE(2012,7,30),2)",
                context),
            2e-15d);
        Assert.AreEqual(
            0.5780821917808219d,
            EvaluateNumber(
                engine,
                "=YEARFRAC(DATE(2012,1,1),DATE(2012,7,30),3)",
                context),
            2e-15d);
        Assert.AreEqual(
            0.5805555555555556d,
            EvaluateNumber(
                engine,
                "=YEARFRAC(DATE(2012,1,1),DATE(2012,7,30),4)",
                context),
            2e-15d);
        Assert.AreEqual(
            -0.5765027322404371d,
            EvaluateNumber(
                engine,
                "=YEARFRAC(DATE(2012,7,30),DATE(2012,1,1),1)",
                context),
            2e-15d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=YEARFRAC(DATE(2024,2,29),DATE(2024,2,29))",
                context),
            1e-15d);
    }

    [TestMethod]
    public void YearFracLocksLeapYearAndThirtyDayMonthRules()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            1d,
            EvaluateNumber(
                engine,
                "=YEARFRAC(DATE(2019,7,1),DATE(2020,7,1),1)",
                context),
            2e-15d);
        Assert.AreEqual(
            31d / 360d,
            EvaluateNumber(
                engine,
                "=YEARFRAC(DATE(2021,2,28),DATE(2021,3,31),0)",
                context),
            2e-15d);
        Assert.AreEqual(
            32d / 360d,
            EvaluateNumber(
                engine,
                "=YEARFRAC(DATE(2021,2,28),DATE(2021,3,31),4)",
                context),
            2e-15d);
    }

    [TestMethod]
    public void CouponFunctionsMatchTheReferenceSemiannualSchedule()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string arguments =
            "DATE(2011,1,25),DATE(2011,11,15),2,1";

        Assert.AreEqual(
            new DateTime(2010, 11, 15),
            EvaluateDate(
                engine,
                $"=COUPPCD({arguments})",
                context));
        Assert.AreEqual(
            new DateTime(2011, 5, 15),
            EvaluateDate(
                engine,
                $"=COUPNCD({arguments})",
                context));
        Assert.AreEqual(
            71d,
            EvaluateNumber(
                engine,
                $"=COUPDAYBS({arguments})",
                context));
        Assert.AreEqual(
            181d,
            EvaluateNumber(
                engine,
                $"=COUPDAYS({arguments})",
                context));
        Assert.AreEqual(
            110d,
            EvaluateNumber(
                engine,
                $"=COUPDAYSNC({arguments})",
                context));
        Assert.AreEqual(
            2d,
            EvaluateNumber(
                engine,
                $"=COUPNUM({arguments})",
                context));
    }

    [TestMethod]
    public void CouponDayCountsRespectEveryBasis()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string dates =
            "DATE(2011,1,25),DATE(2011,11,15),2";

        Assert.AreEqual(
            70d,
            EvaluateNumber(
                engine,
                $"=COUPDAYBS({dates},0)",
                context));
        Assert.AreEqual(
            180d,
            EvaluateNumber(
                engine,
                $"=COUPDAYS({dates},0)",
                context));
        Assert.AreEqual(
            110d,
            EvaluateNumber(
                engine,
                $"=COUPDAYSNC({dates},0)",
                context));
        Assert.AreEqual(
            180d,
            EvaluateNumber(
                engine,
                $"=COUPDAYS({dates},2)",
                context));
        Assert.AreEqual(
            182.5d,
            EvaluateNumber(
                engine,
                $"=COUPDAYS({dates},3)",
                context));
        Assert.AreEqual(
            110d,
            EvaluateNumber(
                engine,
                $"=COUPDAYSNC({dates},4)",
                context));
    }

    [TestMethod]
    public void CouponSchedulePreservesEndOfMonthAnchorsAndExactCouponDates()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            new DateTime(2024, 2, 29),
            EvaluateDate(
                engine,
                "=COUPPCD(DATE(2024,2,29),DATE(2025,8,31),2,1)",
                context));
        Assert.AreEqual(
            new DateTime(2024, 8, 31),
            EvaluateDate(
                engine,
                "=COUPNCD(DATE(2024,2,29),DATE(2025,8,31),2,1)",
                context));
        Assert.AreEqual(
            3d,
            EvaluateNumber(
                engine,
                "=COUPNUM(DATE(2024,2,29),DATE(2025,8,31),2,1)",
                context));
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=COUPDAYBS(DATE(2024,2,29),DATE(2025,8,31),2,1)",
                context));
        Assert.AreEqual(
            5d,
            EvaluateNumber(
                engine,
                "=COUPNUM(DATE(2025,1,15),DATE(2026,1,31),4,1)",
                context));
        Assert.AreEqual(
            EvaluateNumber(
                engine,
                "=COUPNUM(DATE(2011,1,25),DATE(2011,11,15),2,1)",
                context),
            EvaluateNumber(
                engine,
                "=COUPNUM(DATE(2011,1,25),DATE(2011,11,15),2.9,1.9)",
                context),
            1e-15d);
    }

    [TestMethod]
    public void FinancialCalendarDomainsAndScalarCapabilitiesAreExplicit()
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
            "=YEARFRAC(DATE(2024,1,1),DATE(2024,7,1),5)",
            context);
        AssertNumericError(
            engine,
            "=COUPNUM(DATE(2025,1,1),DATE(2025,1,1),2,0)",
            context);
        AssertNumericError(
            engine,
            "=COUPNUM(DATE(2024,1,1),DATE(2025,1,1),3,0)",
            context);
        AssertNumericError(
            engine,
            "=COUPNUM(DATE(2024,1,1),DATE(2025,1,1),2,-1)",
            context);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=YEARFRAC(A1:A2,DATE(2025,1,1),1)",
                context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=COUPNCD(\"bad\",DATE(2025,1,1),2,1)",
                context).ErrorCode);
    }

    [TestMethod]
    public void FinancialCalendarDescriptorsAreVersionedPureAndScalarOnly()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        foreach (var name in FinancialCalendarNames)
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

        Assert.AreEqual(208, registry.Count);
        Assert.AreEqual(208, registry.VersionCount);
    }

    private static void AssertNumericError(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        var result = engine.Evaluate(formula, context);
        Assert.AreEqual("#NUM!", result.Value.RawValue, formula);
    }

    private static DateTime EvaluateDate(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(
            result.IsSuccess,
            $"Expected success for {formula}, but received {result.Value}.");
        Assert.AreEqual(CellValueKind.DateTime, result.Value.Kind);
        return (DateTime)result.Value.RawValue!;
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
