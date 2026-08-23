using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class EngineeringFormulaFunctionsTests
{
    private static readonly string[] FunctionNames =
    [
        "DELTA",
        "GESTEP",
        "BITAND",
        "BITOR",
        "BITXOR",
        "BITLSHIFT",
        "BITRSHIFT",
        "DEC2BIN",
        "DEC2OCT",
        "DEC2HEX",
        "BIN2DEC",
        "OCT2DEC",
        "HEX2DEC",
        "BIN2OCT",
        "BIN2HEX",
        "OCT2BIN",
        "OCT2HEX",
        "HEX2BIN",
        "HEX2OCT",
    ];

    [TestMethod]
    public void EngineeringDescriptorsAreDeterministicScalarSdkV1Functions()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        foreach (var name in FunctionNames)
        {
            Assert.IsTrue(
                registry.TryGetDescriptor(name, out var descriptor),
                $"Missing descriptor for {name}.");
            Assert.AreEqual("NERA.BUILTIN", descriptor.Identity.Namespace);
            Assert.AreEqual(name, descriptor.Identity.Name);
            Assert.AreEqual(new FormulaFunctionVersion(1, 0, 0), descriptor.Version);
            Assert.AreEqual(FormulaFunctionApiVersion.Current, descriptor.MinimumHostApiVersion);
            Assert.AreEqual(
                FormulaFunctionVolatility.Deterministic,
                descriptor.Volatility);
            Assert.AreEqual(
                FormulaFunctionSecurityClassification.Pure,
                descriptor.SecurityClassification);
            Assert.AreEqual(
                FormulaFunctionArgumentCountPolicy.LogicalArguments,
                descriptor.ArgumentCountPolicy);
            Assert.IsTrue((descriptor.Capabilities &
                FormulaFunctionCapabilities.ScalarArguments) != 0);
            Assert.IsTrue((descriptor.Capabilities &
                FormulaFunctionCapabilities.ReturnsScalar) != 0);
            Assert.IsFalse((descriptor.Capabilities &
                FormulaFunctionCapabilities.RangeArguments) != 0);
        }
    }

    [TestMethod]
    public void DeltaAndGreaterOrEqualStepUseDeterministicNumericCoercion()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        AssertNumber(engine, context, "=DELTA(5,5)", 1d);
        AssertNumber(engine, context, "=DELTA(5,4)", 0d);
        AssertNumber(engine, context, "=DELTA(0)", 1d);
        AssertNumber(engine, context, "=GESTEP(5,4)", 1d);
        AssertNumber(engine, context, "=GESTEP(4,5)", 0d);
        AssertNumber(engine, context, "=GESTEP(-1)", 0d);
        AssertNumber(engine, context, "=GESTEP(0)", 1d);
    }

    [TestMethod]
    public void BitwiseFunctionsTruncateAndHonorSignedShiftDirection()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        AssertNumber(engine, context, "=BITAND(13.9,11.1)", 9d);
        AssertNumber(engine, context, "=BITOR(13,11)", 15d);
        AssertNumber(engine, context, "=BITXOR(13,11)", 6d);
        AssertNumber(engine, context, "=BITLSHIFT(4,2)", 16d);
        AssertNumber(engine, context, "=BITLSHIFT(16,-2)", 4d);
        AssertNumber(engine, context, "=BITRSHIFT(16,2)", 4d);
        AssertNumber(engine, context, "=BITRSHIFT(4,-2)", 16d);
    }

    [TestMethod]
    public void BitwiseFunctionsRejectNegativeExcessiveAndOverflowingInputs()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        AssertError(engine, context, "=BITAND(-1,1)", "#NUM!");
        AssertError(
            engine,
            context,
            "=BITOR(281474976710656,1)",
            "#NUM!");
        AssertError(engine, context, "=BITLSHIFT(1,54)", "#NUM!");
        AssertError(
            engine,
            context,
            "=BITLSHIFT(281474976710655,1)",
            "#NUM!");
    }

    [TestMethod]
    public void DecimalConversionsUseFixedWidthTwosComplementForNegatives()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        AssertText(engine, context, "=DEC2BIN(10)", "1010");
        AssertText(engine, context, "=DEC2BIN(10,8)", "00001010");
        AssertText(engine, context, "=DEC2BIN(-1)", "1111111111");
        AssertText(engine, context, "=DEC2BIN(-512)", "1000000000");
        AssertText(engine, context, "=DEC2OCT(-1)", "7777777777");
        AssertText(engine, context, "=DEC2HEX(-1)", "FFFFFFFFFF");
        AssertText(engine, context, "=DEC2HEX(255,4)", "00FF");
    }

    [TestMethod]
    public void BaseToDecimalRecognizesMaximumWidthNegativeValues()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        AssertNumber(engine, context, "=BIN2DEC(\"1111111111\")", -1d);
        AssertNumber(engine, context, "=BIN2DEC(\"1000000000\")", -512d);
        AssertNumber(engine, context, "=OCT2DEC(\"7777777777\")", -1d);
        AssertNumber(engine, context, "=HEX2DEC(\"FFFFFFFFFF\")", -1d);
        AssertNumber(engine, context, "=HEX2DEC(\"7FFFFFFFFF\")", 549755813887d);
    }

    [TestMethod]
    public void CrossBaseConversionsApplyTargetRangeAndPlacesRules()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        AssertText(engine, context, "=BIN2HEX(\"1111111111\")", "FFFFFFFFFF");
        AssertText(engine, context, "=BIN2OCT(\"1010\",5)", "00012");
        AssertText(engine, context, "=HEX2BIN(\"1FF\",10)", "0111111111");
        AssertText(engine, context, "=OCT2HEX(\"7777777777\")", "FFFFFFFFFF");
        AssertText(engine, context, "=HEX2OCT(\"FFFFFFFFFF\")", "7777777777");

        AssertError(engine, context, "=HEX2BIN(\"200\")", "#NUM!");
        AssertError(engine, context, "=DEC2BIN(512)", "#NUM!");
        AssertError(engine, context, "=DEC2BIN(10,2)", "#NUM!");
        AssertError(engine, context, "=BIN2DEC(\"102\")", "#NUM!");
        AssertError(engine, context, "=HEX2DEC(\"GG\")", "#NUM!");
    }

    [TestMethod]
    public void EngineeringFunctionsRejectRangeArguments()
    {
        var engine = new NeraFormulaEngine();
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(2d),
        };

        var result = engine.Evaluate(
            "=BITAND(A1:A2,1)",
            new FormulaSurfaceTestContext(values));

        Assert.AreEqual(FormulaErrorCode.InvalidValue, result.ErrorCode);
        Assert.AreEqual("#VALUE!", result.Value.RawValue);
    }

    private static void AssertNumber(
        IFormulaEngine engine,
        IFormulaEvaluationContext context,
        string formula,
        double expected)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(expected, (double)result.Value.RawValue!, 1e-12d, formula);
    }

    private static void AssertText(
        IFormulaEngine engine,
        IFormulaEvaluationContext context,
        string formula,
        string expected)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }

    private static void AssertError(
        IFormulaEngine engine,
        IFormulaEvaluationContext context,
        string formula,
        string expected)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }
}
