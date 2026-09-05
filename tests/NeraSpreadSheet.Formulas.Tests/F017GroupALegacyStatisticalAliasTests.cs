using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class F017GroupALegacyStatisticalAliasTests
{
    private readonly NeraFormulaEngine _engine = new();

    [TestMethod]
    public void NormDist_LegacyName_MatchesModernNormalDistribution()
    {
        AssertMatches("=NORMDIST(42,40,1.5,TRUE())", "=NORM.DIST(42,40,1.5,TRUE())");
        AssertError("=NORMDIST(1,0,0,TRUE())", "#NUM!");
    }

    [TestMethod]
    public void NormInv_LegacyName_MatchesModernNormalInverse()
    {
        AssertMatches("=NORMINV(0.9,10,2)", "=NORM.INV(0.9,10,2)");
        AssertError("=NORMINV(0,10,2)", "#NUM!");
    }

    [TestMethod]
    public void NormSDist_LegacyName_UsesCumulativeStandardNormal()
    {
        AssertMatches("=NORMSDIST(1.25)", "=NORM.S.DIST(1.25,TRUE())");
        AssertError("=NORMSDIST(A1:A2)", "#VALUE!", ColumnContext(1d, 2d));
    }

    [TestMethod]
    public void NormSInv_LegacyName_MatchesModernStandardNormalInverse()
    {
        AssertMatches("=NORMSINV(0.25)", "=NORM.S.INV(0.25)");
        AssertError("=NORMSINV(1)", "#NUM!");
    }

    [TestMethod]
    public void Poisson_LegacyName_MatchesModernPoissonDistribution()
    {
        AssertMatches("=POISSON(3,2.5,TRUE())", "=POISSON.DIST(3,2.5,TRUE())");
        AssertError("=POISSON(-1,2,TRUE())", "#NUM!");
    }

    [TestMethod]
    public void Weibull_LegacyName_MatchesModernWeibullDistribution()
    {
        AssertMatches("=WEIBULL(2,3,4,FALSE())", "=WEIBULL.DIST(2,3,4,FALSE())");
        AssertError("=WEIBULL(2,0,4,TRUE())", "#NUM!");
    }

    [TestMethod]
    public void Rank_LegacyName_MatchesRankEqWithTies()
    {
        var context = ColumnContext(10d, 7d, 7d, 3d);
        AssertMatches("=RANK(7,A1:A4)", "=RANK.EQ(7,A1:A4)", context);
        AssertMatches("=RANK(7,A1:A4,1)", "=RANK.EQ(7,A1:A4,1)", context);
    }

    [TestMethod]
    public void Percentile_LegacyName_MatchesInclusivePercentile()
    {
        var context = ColumnContext(1d, 2d, 4d, 8d);
        AssertMatches(
            "=PERCENTILE(A1:A4,0.25)",
            "=PERCENTILE.INC(A1:A4,0.25)",
            context);
        AssertError("=PERCENTILE(A1:A4,2)", "#NUM!", context);
    }

    [TestMethod]
    public void Quartile_LegacyName_MatchesInclusiveQuartile()
    {
        var context = ColumnContext(1d, 2d, 3d, 4d, 5d);
        AssertMatches(
            "=QUARTILE(A1:A5,3)",
            "=QUARTILE.INC(A1:A5,3)",
            context);
        AssertError("=QUARTILE(A1:A5,5)", "#NUM!", context);
    }

    [TestMethod]
    public void Forecast_LegacyName_MatchesForecastLinear()
    {
        var context = PairContext(
            (1d, 2d),
            (2d, 4d),
            (3d, 6d),
            (4d, 8d));
        AssertMatches(
            "=FORECAST(5,B1:B4,A1:A4)",
            "=FORECAST.LINEAR(5,B1:B4,A1:A4)",
            context);
    }

    private void AssertMatches(
        string legacyFormula,
        string modernFormula,
        TestContext? context = null,
        double tolerance = 1e-10d)
    {
        var effectiveContext = context ?? TestContext.Empty;
        var legacy = _engine.Evaluate(legacyFormula, effectiveContext);
        var modern = _engine.Evaluate(modernFormula, effectiveContext);
        Assert.IsTrue(legacy.IsSuccess, legacyFormula);
        Assert.IsTrue(modern.IsSuccess, modernFormula);
        Assert.AreEqual(CellValueKind.Number, legacy.Value.Kind, legacyFormula);
        Assert.AreEqual(CellValueKind.Number, modern.Value.Kind, modernFormula);
        Assert.AreEqual(
            (double)modern.Value.RawValue!,
            (double)legacy.Value.RawValue!,
            tolerance,
            legacyFormula);
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

    private static TestContext ColumnContext(params double[] values)
    {
        var cells = new Dictionary<CellAddress, CellValue>();
        for (var index = 0; index < values.Length; index++)
        {
            cells[new CellAddress(index, 0)] = CellValue.FromNumber(values[index]);
        }
        return new TestContext(cells);
    }

    private static TestContext PairContext(
        params (double X, double Y)[] values)
    {
        var cells = new Dictionary<CellAddress, CellValue>();
        for (var index = 0; index < values.Length; index++)
        {
            cells[new CellAddress(index, 0)] = CellValue.FromNumber(values[index].X);
            cells[new CellAddress(index, 1)] = CellValue.FromNumber(values[index].Y);
        }
        return new TestContext(cells);
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
