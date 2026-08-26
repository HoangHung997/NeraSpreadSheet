using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class ComplexEngineeringFormulaFunctionGroupATests
{
    private readonly NeraFormulaEngine _engine = new();

    [TestMethod]
    public void Complex_WithRealImaginaryAndSuffix_ReturnsCanonicalText()
    {
        AssertText("=COMPLEX(3,4)", "3+4i");
        AssertText("=COMPLEX(0,-1,\"j\")", "-j");
        AssertError("=COMPLEX(1,2,\"x\")", "#VALUE!");
    }

    [TestMethod]
    public void ImAbs_WithThreeFourComplex_ReturnsFive()
    {
        AssertNumber("=IMABS(\"3+4i\")", 5d);
        AssertError("=IMABS(\"bad\")", "#VALUE!");
    }

    [TestMethod]
    public void Imaginary_WithComplexInput_ReturnsImaginaryCoefficient()
    {
        AssertNumber("=IMAGINARY(\"3-4j\")", -4d);
        AssertNumber("=IMAGINARY(7)", 0d);
    }

    [TestMethod]
    public void ImArgument_WithFirstQuadrantComplex_ReturnsQuarterPi()
    {
        AssertNumber("=IMARGUMENT(\"1+i\")", Math.PI / 4d);
        AssertError("=IMARGUMENT(0)", "#DIV/0!");
    }

    [TestMethod]
    public void ImConjugate_WithComplexInput_ReturnsConjugate()
    {
        AssertText("=IMCONJUGATE(\"3+4i\")", "3-4i");
    }

    [TestMethod]
    public void ImCos_AtZero_ReturnsOne()
    {
        AssertText("=IMCOS(0)", "1");
    }

    [TestMethod]
    public void ImCosh_AtZero_ReturnsOne()
    {
        AssertText("=IMCOSH(0)", "1");
    }

    [TestMethod]
    public void ImCot_AtQuarterPi_ReturnsOne()
    {
        AssertText("=IMCOT(PI()/4)", "1");
        AssertError("=IMCOT(0)", "#NUM!");
    }

    [TestMethod]
    public void ImCsc_AtHalfPi_ReturnsOne()
    {
        AssertText("=IMCSC(PI()/2)", "1");
        AssertError("=IMCSC(0)", "#NUM!");
    }

    [TestMethod]
    public void ImCsch_AtAsinhOne_ReturnsOne()
    {
        AssertText("=IMCSCH(ASINH(1))", "1");
        AssertError("=IMCSCH(0)", "#NUM!");
    }

    [TestMethod]
    public void ImDiv_WithTwoComplexValues_ReturnsQuotient()
    {
        AssertText("=IMDIV(\"3+4i\",\"1-2i\")", "-1+2i");
        AssertError("=IMDIV(\"1+i\",0)", "#NUM!");
    }

    [TestMethod]
    public void ImExp_AtZero_ReturnsOne()
    {
        AssertText("=IMEXP(0)", "1");
    }

    [TestMethod]
    public void ImLn_AtEulerNumber_ReturnsOne()
    {
        AssertText("=IMLN(EXP(1))", "1");
        AssertError("=IMLN(0)", "#NUM!");
    }

    [TestMethod]
    public void ImLog10_AtOneHundred_ReturnsTwo()
    {
        AssertText("=IMLOG10(100)", "2");
        AssertError("=IMLOG10(0)", "#NUM!");
    }

    [TestMethod]
    public void ImLog2_AtEight_ReturnsThree()
    {
        AssertText("=IMLOG2(8)", "3");
        AssertError("=IMLOG2(0)", "#NUM!");
    }

    [TestMethod]
    public void ImPower_WithOnePlusISquared_ReturnsTwoI()
    {
        AssertText("=IMPOWER(\"1+i\",2)", "2i");
        AssertError("=IMPOWER(0,0)", "#NUM!");
    }

    [TestMethod]
    public void ImProduct_WithConjugatePair_ReturnsTwo()
    {
        AssertText("=IMPRODUCT(\"1+i\",\"1-i\")", "2");
        AssertError("=IMPRODUCT(\"1+i\",\"1+j\")", "#VALUE!");
    }

    [TestMethod]
    public void ImReal_WithComplexInput_ReturnsRealCoefficient()
    {
        AssertNumber("=IMREAL(\"3-4i\")", 3d);
    }

    [TestMethod]
    public void ImSec_AtZero_ReturnsOne()
    {
        AssertText("=IMSEC(0)", "1");
    }

    [TestMethod]
    public void ImSech_AtZero_ReturnsOne()
    {
        AssertText("=IMSECH(0)", "1");
    }

    private void AssertNumber(string formula, double expected)
    {
        var result = _engine.Evaluate(formula, TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind, formula);
        Assert.AreEqual(
            expected,
            (double)result.Value.RawValue!,
            1e-10d,
            formula);
    }

    private void AssertText(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(CellValueKind.Text, result.Value.Kind, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }

    private void AssertError(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, TestContext.Empty);
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }

    private sealed class TestContext : IFormulaEvaluationContext
    {
        public static TestContext Empty { get; } = new();

        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address) =>
            CellValue.Blank;
    }
}
