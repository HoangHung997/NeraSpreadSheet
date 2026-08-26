using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class HyperbolicCombinatoricsAndIntegerFormulaFunctionTests
{
    private readonly NeraFormulaEngine _engine = new();
    private readonly TestContext _context = new();

    [TestMethod]
    public void AtanhUsesTheOpenUnitInterval()
    {
        AssertNumber("=ATANH(0.5)", Math.Atanh(0.5d));
        AssertNumber("=ATANH(\"-0.25\")", Math.Atanh(-0.25d));
        AssertError("=ATANH(1)", "#NUM!");
        AssertError("=ATANH(-1)", "#NUM!");
        AssertError("=ATANH(\"text\")", "#VALUE!");
    }

    [TestMethod]
    public void SinhReturnsTheHyperbolicSineAndRejectsOverflow()
    {
        AssertNumber("=SINH(1.5)", Math.Sinh(1.5d));
        AssertNumber("=SINH(-1.5)", Math.Sinh(-1.5d));
        AssertError("=SINH(1000)", "#NUM!");
    }

    [TestMethod]
    public void CoshReturnsTheHyperbolicCosineAndRejectsOverflow()
    {
        AssertNumber("=COSH(0)", 1d);
        AssertNumber("=COSH(1.5)", Math.Cosh(1.5d));
        AssertError("=COSH(1000)", "#NUM!");
    }

    [TestMethod]
    public void TanhAcceptsEveryFiniteInput()
    {
        AssertNumber("=TANH(0)", 0d);
        AssertNumber("=TANH(1.5)", Math.Tanh(1.5d));
        AssertNumber("=TANH(-1000)", -1d);
    }

    [TestMethod]
    public void CombinTruncatesInputsAndEnforcesItsDomain()
    {
        AssertNumber("=COMBIN(8,2)", 28d);
        AssertNumber("=COMBIN(8.9,2.9)", 28d);
        AssertNumber("=COMBIN(100000000000000000000,1)", 1e20d);
        AssertError("=COMBIN(2,3)", "#NUM!");
        AssertError("=COMBIN(-0.5,0)", "#NUM!");
    }

    [TestMethod]
    public void CombinaCountsCombinationsWithRepetition()
    {
        AssertNumber("=COMBINA(4,3)", 20d);
        AssertNumber("=COMBINA(10,3)", 220d);
        AssertNumber("=COMBINA(4.9,3.9)", 20d);
        AssertNumber("=COMBINA(0,0)", 1d);
        AssertError("=COMBINA(2,3)", "#NUM!");
    }

    [TestMethod]
    public void FactTruncatesAndUsesTheFiniteDoubleBoundary()
    {
        AssertNumber("=FACT(5)", 120d);
        AssertNumber("=FACT(1.9)", 1d);
        AssertNumber("=FACT(0)", 1d);
        AssertError("=FACT(-1)", "#NUM!");
        AssertError("=FACT(171)", "#NUM!");
    }

    [TestMethod]
    public void FactDoubleHandlesEvenOddAndOverflowCases()
    {
        AssertNumber("=FACTDOUBLE(6)", 48d);
        AssertNumber("=FACTDOUBLE(7.9)", 105d);
        AssertNumber("=FACTDOUBLE(0)", 1d);
        AssertError("=FACTDOUBLE(-1)", "#NUM!");
        AssertError("=FACTDOUBLE(301)", "#NUM!");
    }

    [TestMethod]
    public void GcdTruncatesValuesAndHonorsTheExactIntegerBoundary()
    {
        AssertNumber("=GCD(24,36)", 12d);
        AssertNumber("=GCD(24.9,18.2)", 6d);
        AssertNumber("=GCD(5,0)", 5d);
        AssertError("=GCD(-1,2)", "#NUM!");
        AssertError("=GCD(9007199254740992)", "#NUM!");
    }

    [TestMethod]
    public void LcmTruncatesValuesAndRejectsInexactResults()
    {
        AssertNumber("=LCM(24,36)", 72d);
        AssertNumber("=LCM(5,2.9)", 10d);
        AssertNumber("=LCM(5,0)", 0d);
        AssertError("=LCM(-1,2)", "#NUM!");
        AssertError("=LCM(4503599627370496,3)", "#NUM!");
    }

    private void AssertNumber(
        string formula,
        double expected)
    {
        var result = _engine.Evaluate(formula, _context);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind, formula);
        var actual = (double)result.Value.RawValue!;
        var tolerance = Math.Max(
            1e-12d,
            Math.Abs(expected) * 1e-12d);
        Assert.AreEqual(expected, actual, tolerance, formula);
    }

    private void AssertError(
        string formula,
        string expected)
    {
        var result = _engine.Evaluate(formula, _context);
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }

    private sealed class TestContext : IFormulaEvaluationContext
    {
        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address) =>
            CellValue.Blank;
    }
}
