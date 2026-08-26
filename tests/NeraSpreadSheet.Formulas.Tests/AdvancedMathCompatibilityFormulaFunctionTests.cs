using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class AdvancedMathCompatibilityFormulaFunctionTests
{
    private readonly NeraFormulaEngine _engine = new();

    [TestMethod]
    public void MRoundRoundsToTheNearestMultiple()
    {
        AssertNumber("=MROUND(10,3)", 9d);
        AssertNumber("=MROUND(10,4)", 12d);
        AssertNumber("=MROUND(-10,-4)", -12d);
        AssertError("=MROUND(-10,4)", "#NUM!");
    }

    [TestMethod]
    public void CeilingUsesLegacySameSignSemantics()
    {
        AssertNumber("=CEILING(4.3,2)", 6d);
        AssertNumber("=CEILING(-4.3,-2)", -6d);
        AssertError("=CEILING(-4.3,2)", "#NUM!");
    }

    [TestMethod]
    public void FloorUsesLegacySameSignSemantics()
    {
        AssertNumber("=FLOOR(4.3,2)", 4d);
        AssertNumber("=FLOOR(-4.3,-2)", -4d);
        AssertError("=FLOOR(4.3,-2)", "#NUM!");
    }

    [TestMethod]
    public void CeilingPreciseIgnoresTheSignOfSignificance()
    {
        AssertNumber("=CEILING.PRECISE(4.3,-2)", 6d);
        AssertNumber("=CEILING.PRECISE(-4.3,2)", -4d);
        AssertNumber("=CEILING.PRECISE(4.3,0)", 0d);
    }

    [TestMethod]
    public void FloorPreciseIgnoresTheSignOfSignificance()
    {
        AssertNumber("=FLOOR.PRECISE(4.3,-2)", 4d);
        AssertNumber("=FLOOR.PRECISE(-4.3,2)", -6d);
        AssertNumber("=FLOOR.PRECISE(4.3,0)", 0d);
    }

    [TestMethod]
    public void IsoCeilingMatchesCeilingPrecise()
    {
        AssertNumber("=ISO.CEILING(4.3,2)", 6d);
        AssertNumber("=ISO.CEILING(-4.3,2)", -4d);
    }

    [TestMethod]
    public void MultinomialAcceptsARange()
    {
        var context = Context(
            (0, 0, 2d),
            (1, 0, 3d),
            (2, 0, 4d));

        AssertNumber(
            "=MULTINOMIAL(A1:A3)",
            1260d,
            context);
        AssertError(
            "=MULTINOMIAL(-1,2)",
            "#NUM!");
    }

    [TestMethod]
    public void SeriesSumUsesRangeCoefficients()
    {
        var context = Context(
            (0, 0, 1d),
            (1, 0, 2d),
            (2, 0, 3d));

        AssertNumber(
            "=SERIESSUM(2,1,2,A1:A3)",
            114d,
            context);
    }

    [TestMethod]
    public void SumProductPreservesLogicalArrayShapes()
    {
        var context = Context(
            (0, 0, 1d),
            (1, 0, 2d),
            (2, 0, 3d),
            (0, 1, 4d),
            (1, 1, 5d),
            (2, 1, 6d));

        AssertNumber(
            "=SUMPRODUCT(A1:A3,B1:B3)",
            32d,
            context);
        AssertError(
            "=SUMPRODUCT(A1:A2,B1:B3)",
            "#VALUE!",
            context);
    }

    [TestMethod]
    public void SqrtPiRejectsNegativeInputs()
    {
        AssertNumber(
            "=SQRTPI(2)",
            Math.Sqrt(2d * Math.PI));
        AssertError("=SQRTPI(-1)", "#NUM!");
    }

    [TestMethod]
    public void SumX2MinusY2UsesEqualSizedArrays()
    {
        var context = PairContext();

        AssertNumber(
            "=SUMX2MY2(A1:A3,B1:B3)",
            -63d,
            context);
        AssertError(
            "=SUMX2MY2(A1:A2,B1:B3)",
            "#N/A",
            context);
    }

    [TestMethod]
    public void SumX2PlusY2UsesEqualSizedArrays()
    {
        AssertNumber(
            "=SUMX2PY2(A1:A3,B1:B3)",
            91d,
            PairContext());
    }

    [TestMethod]
    public void SumXMinusY2UsesEqualSizedArrays()
    {
        AssertNumber(
            "=SUMXMY2(A1:A3,B1:B3)",
            27d,
            PairContext());
    }

    [TestMethod]
    public void BaseSupportsPaddingAndRadicesThroughThirtySix()
    {
        AssertText("=BASE(31,16,4)", "001F");
        AssertText("=BASE(35,36)", "Z");
        AssertError("=BASE(10,1)", "#NUM!");
    }

    [TestMethod]
    public void DecimalConvertsTextFromTheRequestedRadix()
    {
        AssertNumber("=DECIMAL(\"FF\",16)", 255d);
        AssertNumber("=DECIMAL(\"-101\",2)", -5d);
        AssertError("=DECIMAL(\"2\",2)", "#NUM!");
    }

    [TestMethod]
    public void ArabicAcceptsClassicalAndSimplifiedRomanForms()
    {
        AssertNumber("=ARABIC(\"CDXCIX\")", 499d);
        AssertNumber("=ARABIC(\"ID\")", 499d);
        AssertError("=ARABIC(\"IIII\")", "#VALUE!");
    }

    [TestMethod]
    public void RomanSupportsAllConcisenessModes()
    {
        AssertText("=ROMAN(499,0)", "CDXCIX");
        AssertText("=ROMAN(499,1)", "LDVLIV");
        AssertText("=ROMAN(499,2)", "XDIX");
        AssertText("=ROMAN(499,3)", "VDIV");
        AssertText("=ROMAN(499,4)", "ID");
    }

    [TestMethod]
    public void IsEvenTruncatesTheFractionalPart()
    {
        AssertBoolean("=ISEVEN(4.9)", true);
        AssertBoolean("=ISEVEN(5)", false);
        AssertError("=ISEVEN(\"text\")", "#VALUE!");
    }

    [TestMethod]
    public void IsOddTruncatesTheFractionalPart()
    {
        AssertBoolean("=ISODD(5.9)", true);
        AssertBoolean("=ISODD(4)", false);
        AssertError("=ISODD(\"text\")", "#VALUE!");
    }

    [TestMethod]
    public void IsNonTextReturnsBooleanEvenForErrors()
    {
        AssertBoolean("=ISNONTEXT(5)", true);
        AssertBoolean("=ISNONTEXT(\"5\")", false);
        AssertBoolean("=ISNONTEXT(1/0)", true);
    }

    private static TestContext PairContext() =>
        Context(
            (0, 0, 1d),
            (1, 0, 2d),
            (2, 0, 3d),
            (0, 1, 4d),
            (1, 1, 5d),
            (2, 1, 6d));

    private static TestContext Context(
        params (int Row, int Column, double Value)[] cells)
    {
        var values = cells.ToDictionary(
            static cell => new CellAddress(
                cell.Row,
                cell.Column),
            static cell => CellValue.FromNumber(cell.Value));
        return new TestContext(values);
    }

    private void AssertNumber(
        string formula,
        double expected,
        IFormulaEvaluationContext? context = null)
    {
        var result = _engine.Evaluate(
            formula,
            context ?? TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(
            CellValueKind.Number,
            result.Value.Kind,
            formula);
        Assert.AreEqual(
            expected,
            (double)result.Value.RawValue!,
            1e-10d,
            formula);
    }

    private void AssertText(
        string formula,
        string expected)
    {
        var result = _engine.Evaluate(
            formula,
            TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(
            CellValueKind.Text,
            result.Value.Kind,
            formula);
        Assert.AreEqual(
            expected,
            result.Value.RawValue,
            formula);
    }

    private void AssertBoolean(
        string formula,
        bool expected)
    {
        var result = _engine.Evaluate(
            formula,
            TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(
            CellValueKind.Boolean,
            result.Value.Kind,
            formula);
        Assert.AreEqual(
            expected,
            result.Value.RawValue,
            formula);
    }

    private void AssertError(
        string formula,
        string expected,
        IFormulaEvaluationContext? context = null)
    {
        var result = _engine.Evaluate(
            formula,
            context ?? TestContext.Empty);
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(
            expected,
            result.Value.RawValue,
            formula);
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
            _values.GetValueOrDefault(
                address,
                CellValue.Blank);
    }
}
