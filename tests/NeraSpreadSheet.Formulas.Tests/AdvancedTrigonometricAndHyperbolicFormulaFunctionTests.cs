using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class AdvancedTrigonometricAndHyperbolicFormulaFunctionTests
{
    private readonly NeraFormulaEngine _engine = new();
    private readonly TestContext _context = new();

    [TestMethod]
    public void AcotUsesTheSpreadsheetPrincipalRange()
    {
        AssertNumber("=ACOT(2)", Math.Atan2(1d, 2d));
        AssertNumber("=ACOT(0)", Math.PI / 2d);
        AssertNumber("=ACOT(-2)", Math.Atan2(1d, -2d));
        AssertError("=ACOT(\"text\")", "#VALUE!");
    }

    [TestMethod]
    public void AcothRequiresMagnitudeGreaterThanOne()
    {
        AssertNumber(
            "=ACOTH(6)",
            0.5d * Math.Log(7d / 5d));
        AssertNumber(
            "=ACOTH(-6)",
            0.5d * Math.Log(5d / 7d));
        AssertError("=ACOTH(1)", "#NUM!");
        AssertError("=ACOTH(-1)", "#NUM!");
        AssertError("=ACOTH(0.5)", "#NUM!");
    }

    [TestMethod]
    public void CotUsesRadiansAndGuardsItsSingularityAndMagnitude()
    {
        AssertNumber("=COT(30)", 1d / Math.Tan(30d));
        AssertError("=COT(0)", "#DIV/0!");
        AssertError("=COT(134217728)", "#NUM!");
    }

    [TestMethod]
    public void CothUsesRadiansAndGuardsItsSingularityAndMagnitude()
    {
        AssertNumber("=COTH(2)", 1d / Math.Tanh(2d));
        AssertError("=COTH(0)", "#DIV/0!");
        AssertError("=COTH(-134217728)", "#NUM!");
    }

    [TestMethod]
    public void CscUsesRadiansAndGuardsItsSingularityAndMagnitude()
    {
        AssertNumber("=CSC(15)", 1d / Math.Sin(15d));
        AssertError("=CSC(0)", "#DIV/0!");
        AssertError("=CSC(134217728)", "#NUM!");
    }

    [TestMethod]
    public void CschUsesRadiansAndGuardsItsSingularityAndMagnitude()
    {
        AssertNumber("=CSCH(1.5)", 1d / Math.Sinh(1.5d));
        AssertError("=CSCH(0)", "#DIV/0!");
        AssertError("=CSCH(134217728)", "#NUM!");
    }

    [TestMethod]
    public void SecUsesRadiansAndEnforcesTheMagnitudeContract()
    {
        AssertNumber("=SEC(45)", 1d / Math.Cos(45d));
        AssertNumber("=SEC(\"1.5\")", 1d / Math.Cos(1.5d));
        AssertError("=SEC(134217728)", "#NUM!");
    }

    [TestMethod]
    public void SechUsesRadiansAndEnforcesTheMagnitudeContract()
    {
        AssertNumber("=SECH(1.5)", 1d / Math.Cosh(1.5d));
        AssertNumber("=SECH(0)", 1d);
        AssertError("=SECH(134217728)", "#NUM!");
    }

    [TestMethod]
    public void AsinhAcceptsEveryFiniteRealNumber()
    {
        AssertNumber("=ASINH(1.5)", Math.Asinh(1.5d));
        AssertNumber("=ASINH(-1.5)", Math.Asinh(-1.5d));
        AssertNumber("=ASINH(0)", 0d);
    }

    [TestMethod]
    public void AcoshRequiresAnInputOfAtLeastOne()
    {
        AssertNumber("=ACOSH(1)", 0d);
        AssertNumber("=ACOSH(10)", Math.Acosh(10d));
        AssertError("=ACOSH(0.999)", "#NUM!");
    }

    private void AssertNumber(
        string formula,
        double expected)
    {
        var result = _engine.Evaluate(formula, _context);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind, formula);
        Assert.AreEqual(
            expected,
            (double)result.Value.RawValue!,
            1e-12d,
            formula);
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
