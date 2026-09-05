using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class F017GroupCDiscreteAndHypothesisTests
{
    private readonly NeraFormulaEngine _engine = new();

    [TestMethod]
    public void BinomInv_ReturnsSmallestSuccessCountMeetingProbability()
    {
        AssertNumber("=BINOM.INV(10,0.5,0.75)", 6d);
        AssertNumber("=BINOM.INV(10,0.5,0)", 0d);
        AssertError("=BINOM.INV(10,1.1,0.5)", "#NUM!");
    }

    [TestMethod]
    public void NegBinomDist_ReturnsMassAndCumulativeProbability()
    {
        AssertNumber("=NEGBINOM.DIST(2,3,0.5,FALSE())", 0.1875d);
        AssertNumber("=NEGBINOM.DIST(2,3,0.5,TRUE())", 0.5d);
        AssertError("=NEGBINOM.DIST(2,0,0.5,FALSE())", "#NUM!");
    }

    [TestMethod]
    public void HypGeomDist_ReturnsMassAndCumulativeProbability()
    {
        var mass = EvaluateNumber("=COMBIN(7,2)*COMBIN(13,3)/COMBIN(20,5)");
        var cumulative =
            EvaluateNumber("=(COMBIN(7,0)*COMBIN(13,5)+COMBIN(7,1)*COMBIN(13,4)+COMBIN(7,2)*COMBIN(13,3))/COMBIN(20,5)");
        AssertNumber("=HYPGEOM.DIST(2,5,7,20,FALSE())", mass, tolerance: 1e-10d);
        AssertNumber("=HYPGEOM.DIST(2,5,7,20,TRUE())", cumulative, tolerance: 1e-10d);
        AssertError("=HYPGEOM.DIST(6,5,7,20,FALSE())", "#NUM!");
    }

    [TestMethod]
    public void FTest_ReturnsTwoTailedVarianceProbability()
    {
        var context = FTestContext();
        var expected = EvaluateNumber(
            "=2*MIN(F.DIST(0.25,3,3,TRUE()),1-F.DIST(0.25,3,3,TRUE()))",
            context);
        AssertNumber("=F.TEST(A1:A4,B1:B4)", expected, context, tolerance: 1e-10d);
        AssertNumber("=F.TEST(B1:B4,A1:A4)", expected, context, tolerance: 1e-10d);
    }

    [TestMethod]
    public void ZTest_ReturnsOneTailedProbabilityWithKnownOrEstimatedSigma()
    {
        var context = ZTestContext();
        var expected = EvaluateNumber("=1-NORM.S.DIST(SQRT(3)/2,TRUE())", context);
        AssertNumber("=Z.TEST(A1:A3,1.5,1)", expected, context, tolerance: 1e-10d);
        AssertNumber("=Z.TEST(A1:A3,1.5)", expected, context, tolerance: 1e-10d);
        AssertError("=Z.TEST(A1:A3,1.5,0)", "#NUM!", context);
    }

    [TestMethod]
    public void CritBinom_LegacyName_MatchesBinomInv()
    {
        AssertEquivalent("=CRITBINOM(10,0.5,0.75)", "=BINOM.INV(10,0.5,0.75)");
    }

    [TestMethod]
    public void NegBinomDist_LegacyName_MatchesModernMass()
    {
        AssertEquivalent(
            "=NEGBINOMDIST(2,3,0.5)",
            "=NEGBINOM.DIST(2,3,0.5,FALSE())");
    }

    [TestMethod]
    public void HypGeomDist_LegacyName_MatchesModernMass()
    {
        AssertEquivalent(
            "=HYPGEOMDIST(2,5,7,20)",
            "=HYPGEOM.DIST(2,5,7,20,FALSE())");
    }

    [TestMethod]
    public void FTest_LegacyName_MatchesModernFTest()
    {
        var context = FTestContext();
        AssertEquivalent("=FTEST(A1:A4,B1:B4)", "=F.TEST(A1:A4,B1:B4)", context);
    }

    [TestMethod]
    public void ZTest_LegacyName_MatchesModernZTest()
    {
        var context = ZTestContext();
        AssertEquivalent("=ZTEST(A1:A3,1.5)", "=Z.TEST(A1:A3,1.5)", context);
    }

    private static TestContext FTestContext() =>
        Context(
            (0, 0, 1d), (1, 0, 2d), (2, 0, 3d), (3, 0, 4d),
            (0, 1, 2d), (1, 1, 4d), (2, 1, 6d), (3, 1, 8d));

    private static TestContext ZTestContext() =>
        Context((0, 0, 1d), (1, 0, 2d), (2, 0, 3d));

    private static TestContext Context(
        params (int Row, int Column, double Value)[] cells)
    {
        var values = cells.ToDictionary(
            static cell => new CellAddress(cell.Row, cell.Column),
            static cell => CellValue.FromNumber(cell.Value));
        return new TestContext(values);
    }

    private double EvaluateNumber(string formula, TestContext? context = null)
    {
        var result = _engine.Evaluate(formula, context ?? TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind, formula);
        return (double)result.Value.RawValue!;
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
