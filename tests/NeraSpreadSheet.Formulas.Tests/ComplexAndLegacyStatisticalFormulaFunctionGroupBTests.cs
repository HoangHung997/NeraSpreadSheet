using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class ComplexAndLegacyStatisticalFormulaFunctionGroupBTests
{
    private readonly NeraFormulaEngine _engine = new();

    [TestMethod]
    public void ImSin_AtZero_ReturnsZero()
    {
        AssertText("=IMSIN(0)", "0");
    }

    [TestMethod]
    public void ImSinh_AtZero_ReturnsZero()
    {
        AssertText("=IMSINH(0)", "0");
    }

    [TestMethod]
    public void ImSqrt_OfNegativeOne_ReturnsI()
    {
        AssertText("=IMSQRT(-1)", "i");
    }

    [TestMethod]
    public void ImSub_WithTwoComplexValues_ReturnsDifference()
    {
        AssertText("=IMSUB(\"4+4i\",\"1+2i\")", "3+2i");
        AssertError("=IMSUB(\"1+i\",\"1+j\")", "#VALUE!");
    }

    [TestMethod]
    public void ImSum_WithTwoComplexValues_ReturnsSum()
    {
        AssertText("=IMSUM(\"1+i\",\"2+3i\")", "3+4i");
        AssertError("=IMSUM(\"1+i\",\"1+j\")", "#VALUE!");
    }

    [TestMethod]
    public void ImTan_AtZero_ReturnsZero()
    {
        AssertText("=IMTAN(0)", "0");
    }

    [TestMethod]
    public void BetaDist_LegacyName_ReturnsCumulativeProbability()
    {
        AssertNumber("=BETADIST(0.5,1,1)", 0.5d);
        AssertError("=BETADIST(2,1,1)", "#NUM!");
    }

    [TestMethod]
    public void BetaInv_LegacyName_ReturnsQuantile()
    {
        AssertNumber("=BETAINV(0.5,1,1)", 0.5d, tolerance: 1e-8d);
        AssertError("=BETAINV(0,1,1)", "#NUM!");
    }

    [TestMethod]
    public void BinomDist_LegacyName_ReturnsCumulativeProbability()
    {
        AssertNumber("=BINOMDIST(1,2,0.5,TRUE())", 0.75d);
        AssertError("=BINOMDIST(3,2,0.5,TRUE())", "#NUM!");
    }

    [TestMethod]
    public void ChiDist_LegacyName_ReturnsRightTailProbability()
    {
        AssertNumber("=CHIDIST(0,2)", 1d);
        AssertError("=CHIDIST(-1,2)", "#NUM!");
    }

    [TestMethod]
    public void ChiInv_LegacyName_ReturnsRightTailQuantile()
    {
        AssertNumber(
            "=CHIINV(0.5,2)",
            1.3862943611198906d,
            tolerance: 1e-8d);
        AssertError("=CHIINV(0,2)", "#NUM!");
    }

    [TestMethod]
    public void Covar_LegacyName_ReturnsPopulationCovariance()
    {
        var context = PairContext();
        AssertNumber(
            "=COVAR(A1:A3,B1:B3)",
            2d / 3d,
            context);
    }

    [TestMethod]
    public void ExponDist_LegacyName_ReturnsCumulativeProbability()
    {
        AssertNumber(
            "=EXPONDIST(1,1,TRUE())",
            1d - Math.Exp(-1d));
        AssertError("=EXPONDIST(1,0,TRUE())", "#NUM!");
    }

    [TestMethod]
    public void FDist_LegacyName_ReturnsRightTailProbability()
    {
        AssertNumber("=FDIST(1,1,1)", 0.5d, tolerance: 1e-8d);
        AssertError("=FDIST(-1,1,1)", "#NUM!");
    }

    [TestMethod]
    public void FInv_LegacyName_ReturnsRightTailQuantile()
    {
        AssertNumber("=FINV(0.5,1,1)", 1d, tolerance: 1e-8d);
        AssertError("=FINV(0,1,1)", "#NUM!");
    }

    [TestMethod]
    public void GammaDist_LegacyName_ReturnsCumulativeProbability()
    {
        AssertNumber(
            "=GAMMADIST(1,1,1,TRUE())",
            1d - Math.Exp(-1d));
        AssertError("=GAMMADIST(1,0,1,TRUE())", "#NUM!");
    }

    [TestMethod]
    public void GammaInv_LegacyName_ReturnsQuantile()
    {
        AssertNumber(
            "=GAMMAINV(1-EXP(-1),1,1)",
            1d,
            tolerance: 1e-8d);
        AssertError("=GAMMAINV(1,1,1)", "#NUM!");
    }

    [TestMethod]
    public void LogInv_LegacyName_ReturnsLogNormalQuantile()
    {
        AssertNumber("=LOGINV(0.5,0,1)", 1d, tolerance: 5e-8d);
        AssertError("=LOGINV(0,0,1)", "#NUM!");
    }

    [TestMethod]
    public void LogNormDist_LegacyName_ReturnsCumulativeProbability()
    {
        AssertNumber("=LOGNORMDIST(1,0,1)", 0.5d, tolerance: 5e-8d);
        AssertError("=LOGNORMDIST(0,0,1)", "#NUM!");
    }

    [TestMethod]
    public void Mode_LegacyName_ReturnsSmallestMostFrequentValue()
    {
        AssertNumber("=MODE(1,2,2,3)", 2d);
        AssertError("=MODE(1,2,3)", "#N/A");
    }

    private static TestContext PairContext() =>
        Context(
            (0, 0, 1d),
            (1, 0, 2d),
            (2, 0, 3d),
            (0, 1, 1d),
            (1, 1, 2d),
            (2, 1, 3d));

    private static TestContext Context(
        params (int Row, int Column, double Value)[] cells)
    {
        var values = cells.ToDictionary(
            static cell => new CellAddress(cell.Row, cell.Column),
            static cell => CellValue.FromNumber(cell.Value));
        return new TestContext(values);
    }

    private void AssertNumber(
        string formula,
        double expected,
        TestContext? context = null,
        double tolerance = 1e-10d)
    {
        var result = _engine.Evaluate(
            formula,
            context ?? TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind, formula);
        Assert.AreEqual(
            expected,
            (double)result.Value.RawValue!,
            tolerance,
            formula);
    }

    private void AssertText(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(CellValueKind.Text, result.Value.Kind, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }

    private void AssertError(
        string formula,
        string expected,
        TestContext? context = null)
    {
        var result = _engine.Evaluate(
            formula,
            context ?? TestContext.Empty);
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }

    private sealed class TestContext : IFormulaEvaluationContext
    {
        private readonly IReadOnlyDictionary<CellAddress, CellValue> _values;

        public static TestContext Empty { get; } = new(
            new Dictionary<CellAddress, CellValue>());

        public TestContext(
            IReadOnlyDictionary<CellAddress, CellValue> values)
        {
            _values = values;
        }

        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address) =>
            _values.GetValueOrDefault(address, CellValue.Blank);
    }
}
