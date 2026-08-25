using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class TreasuryBillAndDollarFormulaFunctionTests
{
    private static readonly string[] FunctionNames =
    [
        "TBILLEQ",
        "TBILLPRICE",
        "TBILLYIELD",
        "DOLLARDE",
        "DOLLARFR",
    ];

    [TestMethod]
    public void TreasuryBillFunctionsMatchPublishedReferencesAndReconcile()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();
        const string dates =
            "DATE(2008,3,31),DATE(2008,6,1)";

        Assert.AreEqual(
            98.45d,
            EvaluateNumber(
                engine,
                $"=TBILLPRICE({dates},0.09)",
                context),
            1e-12d);
        Assert.AreEqual(
            0.09141696292534264d,
            EvaluateNumber(
                engine,
                $"=TBILLYIELD({dates},98.45)",
                context),
            2e-15d);
        Assert.AreEqual(
            0.09415149356594302d,
            EvaluateNumber(
                engine,
                $"=TBILLEQ({dates},0.0914)",
                context),
            2e-15d);

        var price = EvaluateNumber(
            engine,
            $"=TBILLPRICE({dates},0.09)",
            context);
        var invariantPrice = price.ToString(
            "R",
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.AreEqual(
            (100d - price) * 360d / (price * 62d),
            EvaluateNumber(
                engine,
                $"=TBILLYIELD({dates},{invariantPrice})",
                context),
            2e-15d);
    }

    [TestMethod]
    public void TreasuryBillCalendarYearBoundaryUsesActualDays()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            94.91666666666667d,
            EvaluateNumber(
                engine,
                "=TBILLPRICE(DATE(2023,3,1),DATE(2024,3,1),0.05)",
                context),
            2e-14d);
        AssertNumericError(
            engine,
            "=TBILLPRICE(DATE(2023,3,1),DATE(2024,3,2),0.05)",
            context);
        AssertNumericError(
            engine,
            "=TBILLYIELD(DATE(2025,1,1),DATE(2025,1,1),99)",
            context);
    }

    [TestMethod]
    public void DollarConversionsMatchPublishedReferencesAndRoundTrip()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            1.125d,
            EvaluateNumber(engine, "=DOLLARDE(1.02,16)", context),
            1e-15d);
        Assert.AreEqual(
            1.3125d,
            EvaluateNumber(engine, "=DOLLARDE(1.1,32)", context),
            1e-15d);
        Assert.AreEqual(
            1.02d,
            EvaluateNumber(engine, "=DOLLARFR(1.125,16)", context),
            1e-15d);
        Assert.AreEqual(
            1.04d,
            EvaluateNumber(engine, "=DOLLARFR(1.125,32)", context),
            1e-15d);
        Assert.AreEqual(
            -123.3125d,
            EvaluateNumber(
                engine,
                "=DOLLARDE(DOLLARFR(-123.3125,32),32)",
                context),
            2e-13d);
        Assert.AreEqual(
            1.125d,
            EvaluateNumber(engine, "=DOLLARDE(1.02,16.9)", context),
            1e-15d);
    }

    [TestMethod]
    public void TreasuryBillAndDollarDomainsFailClosed()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] =
                CellValue.FromDateTime(new DateTime(2025, 1, 1)),
            [new CellAddress(1, 0)] =
                CellValue.FromDateTime(new DateTime(2025, 2, 1)),
        };
        var context = new FormulaSurfaceTestContext(values);
        var engine = new NeraFormulaEngine();

        foreach (var formula in new[]
        {
            "=TBILLEQ(DATE(2025,1,1),DATE(2025,2,1),0)",
            "=TBILLPRICE(DATE(2025,1,1),DATE(2025,2,1),-0.01)",
            "=TBILLPRICE(DATE(2025,1,1),DATE(2026,2,1),0.05)",
            "=TBILLPRICE(DATE(2025,1,1),DATE(2025,2,1),12)",
            "=TBILLEQ(DATE(2025,1,1),DATE(2025,2,1),12)",
            "=TBILLYIELD(DATE(2025,1,1),DATE(2025,2,1),0)",
            "=DOLLARDE(1.02,-16)",
            "=DOLLARFR(1.125,-16)",
        })
        {
            AssertNumericError(engine, formula, context);
        }

        foreach (var formula in new[]
        {
            "=DOLLARDE(1.02,0)",
            "=DOLLARDE(1.02,0.5)",
            "=DOLLARFR(1.125,0)",
        })
        {
            var result = engine.Evaluate(formula, context);
            Assert.AreEqual("#DIV/0!", result.Value.RawValue, formula);
            Assert.AreEqual(
                FormulaErrorCode.DivisionByZero,
                result.ErrorCode,
                formula);
        }

        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=TBILLPRICE(A1:A2,DATE(2025,3,1),0.05)",
                context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=DOLLARDE(\"bad\",16)",
                context).ErrorCode);
    }

    [TestMethod]
    public void TreasuryBillAndDollarDescriptorsAreVersionedAndScalarOnly()
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
