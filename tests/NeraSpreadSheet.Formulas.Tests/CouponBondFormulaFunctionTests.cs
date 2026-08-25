using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class CouponBondFormulaFunctionTests
{
    private static readonly string[] CouponBondNames =
    [
        "PRICE",
        "YIELD",
        "DURATION",
        "MDURATION",
        "MIRR",
    ];

    [TestMethod]
    public void PriceAndYieldMatchPublishedReferenceAndRoundTrip()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string common =
            "DATE(2008,2,15),DATE(2016,11,15),0.0575";

        var price = EvaluateNumber(
            engine,
            $"=PRICE({common},0.065,100,2,0)",
            context);
        Assert.AreEqual(
            95.04287439939205d,
            price,
            2e-11d);
        Assert.AreEqual(
            0.065d,
            EvaluateNumber(
                engine,
                $"=YIELD({common},{price.ToString("R", System.Globalization.CultureInfo.InvariantCulture)},100,2,0)",
                context),
            2e-10d);
        Assert.AreEqual(
            price,
            EvaluateNumber(
                engine,
                $"=PRICE({common},YIELD({common},{price.ToString("R", System.Globalization.CultureInfo.InvariantCulture)},100,2,0),100,2,0)",
                context),
            2e-9d);
    }

    [TestMethod]
    public void DurationAndModifiedDurationMatchPublishedReference()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string arguments =
            "DATE(2008,1,1),DATE(2016,1,1),0.08,0.09,2,1";

        var duration = EvaluateNumber(
            engine,
            $"=DURATION({arguments})",
            context);
        var modifiedDuration = EvaluateNumber(
            engine,
            $"=MDURATION({arguments})",
            context);

        Assert.AreEqual(
            5.993774955545185d,
            duration,
            2e-12d);
        Assert.AreEqual(
            5.735669813918838d,
            modifiedDuration,
            2e-12d);
        Assert.AreEqual(
            duration / (1d + (0.09d / 2d)),
            modifiedDuration,
            2e-14d);
    }

    [TestMethod]
    public void MirrMatchesPublishedReferenceAndPreservesRangePositions()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(-120000d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(39000d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(30000d),
            [new CellAddress(3, 0)] = CellValue.FromNumber(21000d),
            [new CellAddress(4, 0)] = CellValue.FromNumber(37000d),
            [new CellAddress(5, 0)] = CellValue.FromNumber(46000d),
            [new CellAddress(0, 1)] = CellValue.FromNumber(-100d),
            [new CellAddress(1, 1)] = CellValue.Blank,
            [new CellAddress(2, 1)] = CellValue.FromNumber(121d),
        };
        var context = new FormulaSurfaceTestContext(values);
        var engine = new NeraFormulaEngine();

        var reference = engine.Evaluate(
            "=MIRR(A1:A6,0.1,0.12)",
            context);
        Assert.IsTrue(reference.IsSuccess);
        Assert.AreEqual(
            0.1260941303659051d,
            (double)reference.Value.RawValue!,
            2e-14d);
        Assert.AreEqual(1, reference.Dependencies.Count);

        Assert.AreEqual(
            0.1d,
            EvaluateNumber(
                engine,
                "=MIRR(B1:B3,0,0)",
                context),
            2e-14d);
    }

    [TestMethod]
    public void CouponBondDomainsAndCapabilitiesFailClosed()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(0.05d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(0.06d),
            [new CellAddress(0, 1)] = CellValue.FromNumber(100d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(110d),
        };
        var context = new FormulaSurfaceTestContext(values);
        var engine = new NeraFormulaEngine();

        AssertNumericError(
            engine,
            "=PRICE(DATE(2025,1,1),DATE(2025,1,1),0.05,0.06,100,2,0)",
            context);
        AssertNumericError(
            engine,
            "=PRICE(DATE(2024,1,1),DATE(2025,1,1),-0.05,0.06,100,2,0)",
            context);
        AssertNumericError(
            engine,
            "=PRICE(DATE(2024,1,1),DATE(2025,1,1),0.05,-0.06,100,2,0)",
            context);
        AssertNumericError(
            engine,
            "=YIELD(DATE(2024,1,1),DATE(2025,1,1),0.05,0,100,2,0)",
            context);
        AssertNumericError(
            engine,
            "=DURATION(DATE(2024,1,1),DATE(2025,1,1),-0.05,0.06,2,0)",
            context);
        AssertNumericError(
            engine,
            "=MDURATION(DATE(2024,1,1),DATE(2025,1,1),0.05,-0.06,2,0)",
            context);
        AssertNumericError(
            engine,
            "=PRICE(DATE(2024,1,1),DATE(2025,1,1),0.05,0.06,100,3,0)",
            context);
        AssertNumericError(
            engine,
            "=PRICE(DATE(2024,1,1),DATE(2025,1,1),0.05,0.06,100,2,5)",
            context);
        AssertNumericError(
            engine,
            "=MIRR(B1:B2,-1,0.1)",
            context);

        var oneSign = engine.Evaluate(
            "=MIRR(B1:B2,0.1,0.1)",
            context);
        Assert.AreEqual("#DIV/0!", oneSign.Value.RawValue);
        Assert.AreEqual(
            FormulaErrorCode.DivisionByZero,
            oneSign.ErrorCode);

        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=PRICE(A1:A2,DATE(2025,1,1),0.05,0.06,100,2,0)",
                context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=MIRR(\"bad\",0.1,0.1)",
                context).ErrorCode);
    }

    [TestMethod]
    public void CouponBondDescriptorsAreVersionedPureAndCapabilityBounded()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        foreach (var name in CouponBondNames)
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
            Assert.AreEqual(
                name == "MIRR",
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
