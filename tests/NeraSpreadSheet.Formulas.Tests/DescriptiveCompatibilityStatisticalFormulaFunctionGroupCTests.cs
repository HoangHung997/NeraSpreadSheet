using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class DescriptiveCompatibilityStatisticalFormulaFunctionGroupCTests
{
    private readonly NeraFormulaEngine _engine = new();

    [TestMethod]
    public void AveDev_WithSymmetricValues_ReturnsMeanAbsoluteDeviation()
    {
        AssertNumber("=AVEDEV(2,4,6)", 4d / 3d);
    }

    [TestMethod]
    public void AverageA_WithBooleanAndText_CountsCompatibilityValues()
    {
        AssertNumber("=AVERAGEA(TRUE(),4,\"x\")", 5d / 3d);
    }

    [TestMethod]
    public void DevSq_WithSymmetricValues_ReturnsSquaredDeviationSum()
    {
        AssertNumber("=DEVSQ(2,4,6)", 8d);
    }

    [TestMethod]
    public void GeoMean_WithPositiveValues_ReturnsGeometricMean()
    {
        AssertNumber("=GEOMEAN(1,4,16)", 4d);
        AssertError("=GEOMEAN(0,1)", "#NUM!");
    }

    [TestMethod]
    public void HarMean_WithPositiveValues_ReturnsHarmonicMean()
    {
        AssertNumber("=HARMEAN(1,2,4)", 12d / 7d);
        AssertError("=HARMEAN(-1,2)", "#NUM!");
    }

    [TestMethod]
    public void Kurt_WithOneThroughFive_ReturnsSampleExcessKurtosis()
    {
        AssertNumber("=KURT(1,2,3,4,5)", -1.2d);
        AssertError("=KURT(1,2,3)", "#DIV/0!");
    }

    [TestMethod]
    public void MaxA_WithBooleanNegativeAndText_ReturnsOne()
    {
        AssertNumber("=MAXA(TRUE(),-2,\"x\")", 1d);
    }

    [TestMethod]
    public void MinA_WithBooleanNegativeAndText_ReturnsNegativeTwo()
    {
        AssertNumber("=MINA(TRUE(),-2,\"x\")", -2d);
    }

    [TestMethod]
    public void Skew_WithSymmetricValues_ReturnsZero()
    {
        AssertNumber("=SKEW(1,2,3,4,5)", 0d);
        AssertError("=SKEW(1,1,1)", "#DIV/0!");
    }

    [TestMethod]
    public void SkewP_WithSymmetricPopulation_ReturnsZero()
    {
        AssertNumber("=SKEW.P(1,2,3,4,5)", 0d);
        AssertError("=SKEW.P(1,1,1)", "#DIV/0!");
    }

    [TestMethod]
    public void StdevA_WithBooleanAndNumbers_ReturnsSampleDeviation()
    {
        AssertNumber("=STDEVA(TRUE(),2,3)", 1d);
    }

    [TestMethod]
    public void StdevPa_WithBooleanAndNumbers_ReturnsPopulationDeviation()
    {
        AssertNumber("=STDEVPA(TRUE(),2,3)", Math.Sqrt(2d / 3d));
    }

    [TestMethod]
    public void VarA_WithBooleanAndNumbers_ReturnsSampleVariance()
    {
        AssertNumber("=VARA(TRUE(),2,3)", 1d);
    }

    [TestMethod]
    public void VarPa_WithBooleanAndNumbers_ReturnsPopulationVariance()
    {
        AssertNumber("=VARPA(TRUE(),2,3)", 2d / 3d);
    }

    [TestMethod]
    public void TrimMean_WithTwentyPercent_RemovesOneValueFromEachTail()
    {
        var context = ColumnContext(1d, 2d, 3d, 4d, 5d, 6d, 7d, 8d, 9d, 10d);
        AssertNumber("=TRIMMEAN(A1:A10,0.2)", 5.5d, context);
        AssertError("=TRIMMEAN(A1:A10,1)", "#NUM!", context);
    }

    [TestMethod]
    public void PercentileExc_WithValidExclusiveRank_ReturnsInterpolatedValue()
    {
        var context = ColumnContext(1d, 2d, 3d, 4d);
        AssertNumber("=PERCENTILE.EXC(A1:A4,0.4)", 2d, context);
        AssertError("=PERCENTILE.EXC(A1:A4,0)", "#NUM!", context);
    }

    [TestMethod]
    public void QuartileExc_WithSecondQuartile_ReturnsMedian()
    {
        var context = ColumnContext(1d, 2d, 3d, 4d, 5d, 6d, 7d);
        AssertNumber("=QUARTILE.EXC(A1:A7,2)", 4d, context);
        AssertError("=QUARTILE.EXC(A1:A7,0)", "#NUM!", context);
    }

    [TestMethod]
    public void RankAvg_WithTie_ReturnsAverageRank()
    {
        var context = ColumnContext(10d, 5d, 5d, 2d, 1d);
        AssertNumber("=RANK.AVG(5,A1:A5)", 2.5d, context);
    }

    [TestMethod]
    public void PercentRankInc_WithMiddleValue_ReturnsOneHalf()
    {
        var context = ColumnContext(1d, 2d, 3d, 4d, 5d);
        AssertNumber("=PERCENTRANK.INC(A1:A5,3)", 0.5d, context);
        AssertError("=PERCENTRANK.INC(A1:A5,6)", "#N/A", context);
    }

    [TestMethod]
    public void PercentRankExc_WithMiddleValue_ReturnsOneHalf()
    {
        var context = ColumnContext(1d, 2d, 3d, 4d, 5d);
        AssertNumber("=PERCENTRANK.EXC(A1:A5,3)", 0.5d, context);
        AssertError("=PERCENTRANK.EXC(A1:A5,0)", "#N/A", context);
    }

    private static TestContext ColumnContext(params double[] values)
    {
        var cells = new Dictionary<CellAddress, CellValue>();
        for (var index = 0; index < values.Length; index++)
        {
            cells.Add(
                new CellAddress(index, 0),
                CellValue.FromNumber(values[index]));
        }
        return new TestContext(cells);
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
