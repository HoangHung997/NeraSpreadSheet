using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class F017GroupBStatisticalCompatibilityTests
{
    private readonly NeraFormulaEngine _engine = new();

    [TestMethod]
    public void Stdev_LegacyName_MatchesSampleStandardDeviation()
    {
        AssertEquivalent("=STDEV(A1:A4)", "=STDEV.S(A1:A4)", NumberContext());
        AssertError("=STDEV(1)", "#DIV/0!");
    }

    [TestMethod]
    public void StdevP_LegacyName_MatchesPopulationStandardDeviation()
    {
        AssertEquivalent("=STDEVP(A1:A4)", "=STDEV.P(A1:A4)", NumberContext());
    }

    [TestMethod]
    public void Var_LegacyName_MatchesSampleVariance()
    {
        AssertEquivalent("=VAR(A1:A4)", "=VAR.S(A1:A4)", NumberContext());
        AssertError("=VAR(1)", "#DIV/0!");
    }

    [TestMethod]
    public void VarP_LegacyName_MatchesPopulationVariance()
    {
        AssertEquivalent("=VARP(A1:A4)", "=VAR.P(A1:A4)", NumberContext());
    }

    [TestMethod]
    public void TInv_LegacyName_MatchesTwoTailedInverse()
    {
        AssertEquivalent("=TINV(0.05,10)", "=T.INV.2T(0.05,10)");
        AssertError("=TINV(0,10)", "#NUM!");
    }

    [TestMethod]
    public void TDist_LegacyName_SelectsOneOrTwoTailDistribution()
    {
        AssertEquivalent("=TDIST(1.5,10,1)", "=T.DIST.RT(1.5,10)");
        AssertEquivalent("=TDIST(1.5,10,2)", "=T.DIST.2T(1.5,10)");
        AssertError("=TDIST(1.5,10,3)", "#NUM!");
        AssertError("=TDIST(-1,10,1)", "#NUM!");
    }

    [TestMethod]
    public void Confidence_LegacyName_MatchesConfidenceNorm()
    {
        AssertEquivalent("=CONFIDENCE(0.05,2,100)", "=CONFIDENCE.NORM(0.05,2,100)");
        AssertError("=CONFIDENCE(0,2,100)", "#NUM!");
    }

    [TestMethod]
    public void ConfidenceNorm_ReturnsNormalMarginOfError()
    {
        var expected = EvaluateNumber("=NORM.S.INV(0.975)*2/SQRT(100)");
        AssertNumber("=CONFIDENCE.NORM(0.05,2,100)", expected, tolerance: 1e-10d);
        AssertError("=CONFIDENCE.NORM(0.05,0,100)", "#NUM!");
    }

    [TestMethod]
    public void ConfidenceT_ReturnsStudentTMarginOfError()
    {
        var expected = EvaluateNumber("=T.INV.2T(0.05,9)*2/SQRT(10)");
        AssertNumber("=CONFIDENCE.T(0.05,2,10)", expected, tolerance: 1e-10d);
        AssertError("=CONFIDENCE.T(0.05,2,1)", "#NUM!");
    }

    [TestMethod]
    public void Prob_SumsProbabilityMassWithinClosedInterval()
    {
        var context = ProbabilityContext();
        AssertNumber("=PROB(A1:A4,B1:B4,2,3)", 0.5d, context);
        AssertNumber("=PROB(A1:A4,B1:B4,4)", 0.4d, context);
        AssertError("=PROB(A1:A4,B1:B3,2,3)", "#N/A", context);

        var invalid = Context(
            (0, 0, 1d), (1, 0, 2d),
            (0, 1, 0.4d), (1, 1, 0.4d));
        AssertError("=PROB(A1:A2,B1:B2,1,2)", "#NUM!", invalid);
    }

    private static TestContext NumberContext() =>
        Context((0, 0, 1d), (1, 0, 2d), (2, 0, 3d), (3, 0, 4d));

    private static TestContext ProbabilityContext() =>
        Context(
            (0, 0, 1d), (1, 0, 2d), (2, 0, 3d), (3, 0, 4d),
            (0, 1, 0.1d), (1, 1, 0.2d), (2, 1, 0.3d), (3, 1, 0.4d));

    private static TestContext Context(
        params (int Row, int Column, double Value)[] cells)
    {
        var values = cells.ToDictionary(
            static cell => new CellAddress(cell.Row, cell.Column),
            static cell => CellValue.FromNumber(cell.Value));
        return new TestContext(values);
    }

    private void AssertEquivalent(
        string actualFormula,
        string expectedFormula,
        TestContext? context = null,
        double tolerance = 1e-10d)
    {
        var effectiveContext = context ?? TestContext.Empty;
        var actual = _engine.Evaluate(actualFormula, effectiveContext);
        var expected = _engine.Evaluate(expectedFormula, effectiveContext);
        Assert.IsTrue(actual.IsSuccess, actualFormula);
        Assert.IsTrue(expected.IsSuccess, expectedFormula);
        Assert.AreEqual(expected.Value.Kind, actual.Value.Kind, actualFormula);
        if (actual.Value.Kind == CellValueKind.Number)
        {
            Assert.AreEqual(
                (double)expected.Value.RawValue!,
                (double)actual.Value.RawValue!,
                tolerance,
                actualFormula);
        }
        else
        {
            Assert.AreEqual(expected.Value.RawValue, actual.Value.RawValue, actualFormula);
        }
    }

    private double EvaluateNumber(string formula)
    {
        var result = _engine.Evaluate(formula, TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind, formula);
        return (double)result.Value.RawValue!;
    }

    private void AssertNumber(
        string formula,
        double expected,
        TestContext? context = null,
        double tolerance = 1e-10d)
    {
        var result = _engine.Evaluate(formula, context ?? TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind, formula);
        Assert.AreEqual(expected, (double)result.Value.RawValue!, tolerance, formula);
    }

    private void AssertError(
        string formula,
        string expected,
        TestContext? context = null)
    {
        var result = _engine.Evaluate(formula, context ?? TestContext.Empty);
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }

    private sealed class TestContext : IFormulaEvaluationContext
    {
        private readonly IReadOnlyDictionary<CellAddress, CellValue> _values;

        public static TestContext Empty { get; } = new(
            new Dictionary<CellAddress, CellValue>());

        public TestContext(IReadOnlyDictionary<CellAddress, CellValue> values)
        {
            _values = values;
        }

        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address) =>
            _values.GetValueOrDefault(address, CellValue.Blank);
    }
}
