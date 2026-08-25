using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FrenchDepreciationAndOddCouponFormulaFunctionTests
{
    private static readonly string[] FunctionNames =
    [
        "AMORLINC",
        "AMORDEGRC",
        "ODDFPRICE",
        "ODDFYIELD",
        "ODDLPRICE",
    ];

    [TestMethod]
    public void FrenchDepreciationFunctionsMatchPublishedReferences()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string common =
            "2400,DATE(2008,8,19),DATE(2008,12,31),300";

        Assert.AreEqual(
            360d,
            EvaluateNumber(
                engine,
                $"=AMORLINC({common},1,0.15,1)",
                context),
            1e-12d);
        Assert.AreEqual(
            776d,
            EvaluateNumber(
                engine,
                $"=AMORDEGRC({common},1,0.15,1)",
                context),
            1e-12d);
        Assert.AreEqual(
            330d,
            EvaluateNumber(
                engine,
                $"=AMORDEGRC({common},0,0.15,1)",
                context),
            1e-12d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                $"=AMORLINC({common},100,0.15,1)",
                context),
            1e-12d);
    }

    [TestMethod]
    public void OddFirstPriceAndYieldMatchPublishedReferencesAndRoundTrip()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string dates =
            "DATE(2008,11,11),DATE(2021,3,1)," +
            "DATE(2008,10,15),DATE(2009,3,1)";

        var price = EvaluateNumber(
            engine,
            $"=ODDFPRICE({dates},0.0785,0.0625,100,2,1)",
            context);
        Assert.AreEqual(
            113.59771747407883d,
            price,
            2e-11d);

        var yield = EvaluateNumber(
            engine,
            $"=ODDFYIELD({dates},0.0575,84.5,100,2,0)",
            context);
        Assert.AreEqual(
            0.07724554159782439d,
            yield,
            2e-12d);

        var invariantYield = yield.ToString(
            "R",
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(
            84.5d,
            EvaluateNumber(
                engine,
                $"=ODDFPRICE({dates},0.0575,{invariantYield},100,2,0)",
                context),
            2e-9d);
    }

    [TestMethod]
    public void LongOddFirstAndOddLastPricesUseBoundedQuasiCouponPeriods()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string longDates =
            "DATE(2020,10,15),DATE(2025,12,31)," +
            "DATE(2019,12,31),DATE(2020,12,31)";

        var longPrice = EvaluateNumber(
            engine,
            $"=ODDFPRICE({longDates},0.06,0.05,100,2,1)",
            context);
        Assert.AreEqual(
            104.49678831090984d,
            longPrice,
            2e-11d);

        var invariantPrice = longPrice.ToString(
            "R",
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(
            0.05d,
            EvaluateNumber(
                engine,
                $"=ODDFYIELD({longDates},0.06,{invariantPrice},100,2,1)",
                context),
            2e-11d);

        Assert.AreEqual(
            99.87828601472134d,
            EvaluateNumber(
                engine,
                "=ODDLPRICE(" +
                "DATE(2008,2,7),DATE(2008,6,15)," +
                "DATE(2007,10,15),0.0375,0.0405,100,2,0)",
                context),
            2e-11d);
    }

    [TestMethod]
    public void F005DomainsAndArgumentKindsFailClosed()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] =
                CellValue.FromDateTime(new DateTime(2008, 11, 11)),
            [new CellAddress(1, 0)] =
                CellValue.FromDateTime(new DateTime(2021, 3, 1)),
        };
        var context = new FormulaSurfaceTestContext(values);
        var engine = new NeraFormulaEngine();

        foreach (var formula in new[]
        {
            "=AMORLINC(2400,DATE(2008,8,19),DATE(2008,12,31),300,1,0.15,2)",
            "=AMORLINC(2400,DATE(2009,1,1),DATE(2008,12,31),300,1,0.15,1)",
            "=AMORDEGRC(2400,DATE(2008,8,19),DATE(2008,12,31),300,1,0.4,1)",
            "=AMORDEGRC(0,DATE(2008,8,19),DATE(2008,12,31),0,1,0.15,1)",
            "=ODDFPRICE(DATE(2008,11,11),DATE(2021,3,1),DATE(2008,11,11),DATE(2009,3,1),0.05,0.06,100,2,0)",
            "=ODDFPRICE(DATE(2008,11,11),DATE(2021,3,1),DATE(2008,10,15),DATE(2009,3,1),-0.05,0.06,100,2,0)",
            "=ODDFPRICE(DATE(2008,11,11),DATE(2021,3,1),DATE(2008,10,15),DATE(2009,3,1),0.05,-0.06,100,2,0)",
            "=ODDFPRICE(DATE(2008,11,11),DATE(2021,3,1),DATE(2008,10,15),DATE(2009,3,1),0.05,0.06,100,3,0)",
            "=ODDFYIELD(DATE(2008,11,11),DATE(2021,3,1),DATE(2008,10,15),DATE(2009,3,1),0.05,0,100,2,0)",
            "=ODDLPRICE(DATE(2008,2,7),DATE(2008,6,15),DATE(2008,2,7),0.05,0.06,100,2,0)",
            "=ODDLPRICE(DATE(2008,2,7),DATE(2008,6,15),DATE(2007,10,15),0.05,-0.06,100,2,0)",
        })
        {
            AssertNumericError(engine, formula, context);
        }

        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=AMORLINC(A1:A2,DATE(2008,8,19),DATE(2008,12,31),300,1,0.15,1)",
                context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=ODDFPRICE(A1:A2,DATE(2021,3,1),DATE(2008,10,15),DATE(2009,3,1),0.05,0.06,100,2,0)",
                context).ErrorCode);
    }

    [TestMethod]
    public void F005DescriptorsAreVersionedPureAndScalarOnly()
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
