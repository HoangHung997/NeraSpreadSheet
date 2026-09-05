using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class OddLastYieldAndDateCompatibilityFormulaFunctionTests
{
    private static readonly string[] FunctionNames =
    [
        "ODDLYIELD",
        "DATEDIF",
        "DAYS360",
        "ISOWEEKNUM",
        "WEEKNUM",
    ];

    [TestMethod]
    public void OddLastYieldMatchesPublishedReferenceAndPriceRoundTrip()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            0.04519223562916898d,
            EvaluateNumber(
                engine,
                "=ODDLYIELD(" +
                "DATE(2008,4,20),DATE(2008,6,15)," +
                "DATE(2007,12,24),0.0375,99.875,100,2,0)",
                context),
            2e-14d);

        var price = EvaluateNumber(
            engine,
            "=ODDLPRICE(" +
            "DATE(2008,2,7),DATE(2008,6,15)," +
            "DATE(2007,10,15),0.0375,0.0405,100,2,0)",
            context);
        var invariantPrice = price.ToString(
            "R",
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(
            0.0405d,
            EvaluateNumber(
                engine,
                "=ODDLYIELD(" +
                "DATE(2008,2,7),DATE(2008,6,15)," +
                $"DATE(2007,10,15),0.0375,{invariantPrice},100,2,0)",
                context),
            2e-14d);
    }

    [TestMethod]
    public void DateDifSupportsAllPublishedUnitsAndLegacyMonthEndBehavior()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string dates =
            "DATE(2001,6,1),DATE(2002,8,15)";

        Assert.AreEqual(
            1d,
            EvaluateNumber(engine, $"=DATEDIF({dates},\"Y\")", context),
            1e-12d);
        Assert.AreEqual(
            14d,
            EvaluateNumber(engine, $"=DATEDIF({dates},\"M\")", context),
            1e-12d);
        Assert.AreEqual(
            440d,
            EvaluateNumber(engine, $"=DATEDIF({dates},\"D\")", context),
            1e-12d);
        Assert.AreEqual(
            14d,
            EvaluateNumber(engine, $"=DATEDIF({dates},\"MD\")", context),
            1e-12d);
        Assert.AreEqual(
            2d,
            EvaluateNumber(engine, $"=DATEDIF({dates},\"ym\")", context),
            1e-12d);
        Assert.AreEqual(
            75d,
            EvaluateNumber(engine, $"=DATEDIF({dates},\"YD\")", context),
            1e-12d);
        Assert.AreEqual(
            -2d,
            EvaluateNumber(
                engine,
                "=DATEDIF(DATE(2023,1,31),DATE(2023,3,1),\"MD\")",
                context),
            1e-12d);
    }

    [TestMethod]
    public void Days360MatchesPublishedUsAndEuropeanRulesAndIsSigned()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            1d,
            EvaluateNumber(
                engine,
                "=DAYS360(DATE(2011,1,30),DATE(2011,2,1))",
                context),
            1e-12d);
        Assert.AreEqual(
            360d,
            EvaluateNumber(
                engine,
                "=DAYS360(DATE(2011,1,1),DATE(2011,12,31))",
                context),
            1e-12d);
        Assert.AreEqual(
            30d,
            EvaluateNumber(
                engine,
                "=DAYS360(DATE(2011,1,1),DATE(2011,2,1))",
                context),
            1e-12d);
        Assert.AreEqual(
            30d,
            EvaluateNumber(
                engine,
                "=DAYS360(DATE(2021,2,28),DATE(2021,3,31),0)",
                context),
            1e-12d);
        Assert.AreEqual(
            32d,
            EvaluateNumber(
                engine,
                "=DAYS360(DATE(2021,2,28),DATE(2021,3,31),1)",
                context),
            1e-12d);
        Assert.AreEqual(
            -1d,
            EvaluateNumber(
                engine,
                "=DAYS360(DATE(2011,2,1),DATE(2011,1,30))",
                context),
            1e-12d);
    }

    [TestMethod]
    public void WeekNumberFunctionsMatchSystemOneAndIsoReferences()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            10d,
            EvaluateNumber(
                engine,
                "=WEEKNUM(DATE(2012,3,9))",
                context),
            1e-12d);
        Assert.AreEqual(
            11d,
            EvaluateNumber(
                engine,
                "=WEEKNUM(DATE(2012,3,9),2)",
                context),
            1e-12d);
        Assert.AreEqual(
            11d,
            EvaluateNumber(
                engine,
                "=WEEKNUM(DATE(2012,3,9),12)",
                context),
            1e-12d);
        Assert.AreEqual(
            10d,
            EvaluateNumber(
                engine,
                "=ISOWEEKNUM(DATE(2012,3,9))",
                context),
            1e-12d);
        Assert.AreEqual(
            53d,
            EvaluateNumber(
                engine,
                "=ISOWEEKNUM(DATE(2021,1,1))",
                context),
            1e-12d);
        Assert.AreEqual(
            53d,
            EvaluateNumber(
                engine,
                "=WEEKNUM(DATE(2021,1,1),21)",
                context),
            1e-12d);
    }

    [TestMethod]
    public void F006DomainsDescriptorsAndRegistryFailClosed()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] =
                CellValue.FromDateTime(new DateTime(2023, 1, 1)),
            [new CellAddress(1, 0)] =
                CellValue.FromDateTime(new DateTime(2023, 2, 1)),
        };
        var context = new FormulaSurfaceTestContext(values);
        var engine = new NeraFormulaEngine();

        foreach (var formula in new[]
        {
            "=ODDLYIELD(DATE(2008,6,15),DATE(2008,6,15),DATE(2007,12,24),0.0375,99.875,100,2,0)",
            "=ODDLYIELD(DATE(2008,4,20),DATE(2008,6,15),DATE(2007,12,24),-0.0375,99.875,100,2,0)",
            "=ODDLYIELD(DATE(2008,4,20),DATE(2008,6,15),DATE(2007,12,24),0.0375,0,100,2,0)",
            "=DATEDIF(DATE(2023,2,1),DATE(2023,1,1),\"D\")",
            "=DATEDIF(DATE(2023,1,1),DATE(2023,2,1),\"BAD\")",
            "=WEEKNUM(DATE(2023,1,1),3)",
        })
        {
            AssertNumericError(engine, formula, context);
        }

        foreach (var formula in new[]
        {
            "=ODDLYIELD(A1:A2,DATE(2008,6,15),DATE(2007,12,24),0.0375,99.875,100,2,0)",
            "=DATEDIF(A1:A2,DATE(2023,2,1),\"D\")",
            "=DAYS360(A1:A2,DATE(2023,2,1))",
            "=ISOWEEKNUM(A1:A2)",
            "=WEEKNUM(A1:A2)",
            "=DAYS360(DATE(2023,1,1),DATE(2023,2,1),\"bad\")",
        })
        {
            Assert.AreEqual(
                FormulaErrorCode.InvalidValue,
                engine.Evaluate(formula, context).ErrorCode,
                formula);
        }

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
