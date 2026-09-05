using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class ConditionalAggregateFormulaFunctionTests
{
    [TestMethod]
    public void CountIfSupportsNumericTextWildcardBlankAndErrorCriteria()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(20d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(30d),
            [new CellAddress(3, 0)] = CellValue.FromText("Open"),
            [new CellAddress(4, 0)] = CellValue.FromText("Order*01"),
            [new CellAddress(5, 0)] = CellValue.FromError("#N/A"),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            2d,
            engine.Evaluate(
                "=COUNTIF(A1:A6,\">=20\")",
                context).Value.RawValue);
        Assert.AreEqual(
            1d,
            engine.Evaluate(
                "=COUNTIF(A1:A6,\"op*\")",
                context).Value.RawValue);
        Assert.AreEqual(
            1d,
            engine.Evaluate(
                "=COUNTIF(A1:A6,\"order~*01\")",
                context).Value.RawValue);
        Assert.AreEqual(
            1d,
            engine.Evaluate(
                "=COUNTIF(A1:A6,\"#N/A\")",
                context).Value.RawValue);
        Assert.AreEqual(
            1d,
            engine.Evaluate(
                "=COUNTIF(A1:A7,\"=\")",
                context).Value.RawValue);
        Assert.AreEqual(
            6d,
            engine.Evaluate(
                "=COUNTIF(A1:A7,\"<>\")",
                context).Value.RawValue);
    }

    [TestMethod]
    public void CountIfsCombinesCriteriaByPositionAndRequiresEqualShapes()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("North"),
            [new CellAddress(1, 0)] = CellValue.FromText("South"),
            [new CellAddress(2, 0)] = CellValue.FromText("North"),
            [new CellAddress(3, 0)] = CellValue.FromText("North"),
            [new CellAddress(0, 1)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(30d),
            [new CellAddress(2, 1)] = CellValue.FromNumber(20d),
            [new CellAddress(3, 1)] = CellValue.FromNumber(40d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        var result = engine.Evaluate(
            "=COUNTIFS(A1:A4,\"North\",B1:B4,\">=20\")",
            context);
        Assert.AreEqual(2d, result.Value.RawValue);
        Assert.AreEqual(2, result.Dependencies.Count);

        var mismatch = engine.Evaluate(
            "=COUNTIFS(A1:A4,\"North\",B1:B3,\">=20\")",
            context);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            mismatch.ErrorCode);
    }

    [TestMethod]
    public void SumIfUsesCriteriaRangeOrExplicitSameShapeSumRange()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(2d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(3d),
            [new CellAddress(0, 1)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(20d),
            [new CellAddress(2, 1)] = CellValue.FromNumber(30d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            5d,
            engine.Evaluate(
                "=SUMIF(A1:A3,\">1\")",
                context).Value.RawValue);
        var explicitSum = engine.Evaluate(
            "=SUMIF(A1:A3,\">1\",B1:B3)",
            context);
        Assert.AreEqual(50d, explicitSum.Value.RawValue);
        Assert.AreEqual(2, explicitSum.Dependencies.Count);

        var mismatch = engine.Evaluate(
            "=SUMIF(A1:A3,\">1\",B1:B2)",
            context);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            mismatch.ErrorCode);
    }

    [TestMethod]
    public void SumIfsAndAverageIfsSupportMultipleCriteriaRanges()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(20d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(30d),
            [new CellAddress(3, 0)] = CellValue.FromNumber(40d),
            [new CellAddress(0, 1)] = CellValue.FromText("North"),
            [new CellAddress(1, 1)] = CellValue.FromText("North"),
            [new CellAddress(2, 1)] = CellValue.FromText("South"),
            [new CellAddress(3, 1)] = CellValue.FromText("North"),
            [new CellAddress(0, 2)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 2)] = CellValue.FromNumber(2d),
            [new CellAddress(2, 2)] = CellValue.FromNumber(3d),
            [new CellAddress(3, 2)] = CellValue.FromNumber(4d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        var sum = engine.Evaluate(
            "=SUMIFS(A1:A4,B1:B4,\"North\",C1:C4,\">=2\")",
            context);
        Assert.AreEqual(60d, sum.Value.RawValue);
        Assert.AreEqual(3, sum.Dependencies.Count);

        var average = engine.Evaluate(
            "=AVERAGEIFS(A1:A4,B1:B4,\"North\",C1:C4,\">=2\")",
            context);
        Assert.AreEqual(30d, average.Value.RawValue);
        Assert.AreEqual(3, average.Dependencies.Count);
    }

    [TestMethod]
    public void AverageIfReturnsDivisionByZeroWhenNoNumericMatchExists()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("A"),
            [new CellAddress(1, 0)] = CellValue.FromText("B"),
            [new CellAddress(0, 1)] = CellValue.FromText("text"),
            [new CellAddress(1, 1)] = CellValue.FromText("also text"),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        var result = engine.Evaluate(
            "=AVERAGEIF(A1:A2,\"*\",B1:B2)",
            context);

        Assert.AreEqual(
            FormulaErrorCode.DivisionByZero,
            result.ErrorCode);
        Assert.AreEqual("#DIV/0!", result.Value.RawValue);
    }

    [TestMethod]
    public void MatchedAggregateErrorsPropagateButUnmatchedErrorsDoNot()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(2d),
            [new CellAddress(0, 1)] = CellValue.FromError("#VALUE!"),
            [new CellAddress(1, 1)] = CellValue.FromNumber(5d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        var unmatched = engine.Evaluate(
            "=SUMIF(A1:A2,\">1\",B1:B2)",
            context);
        Assert.AreEqual(5d, unmatched.Value.RawValue);

        var matched = engine.Evaluate(
            "=SUMIF(A1:A2,\">0\",B1:B2)",
            context);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            matched.ErrorCode);
        Assert.AreEqual("#VALUE!", matched.Value.RawValue);
    }

    [TestMethod]
    public void CriteriaCellAndEveryRangeAreCapturedAsDependencies()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(2d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(3d),
            [new CellAddress(0, 1)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(20d),
            [new CellAddress(2, 1)] = CellValue.FromNumber(30d),
            [new CellAddress(0, 3)] = CellValue.FromText(">1"),
        };
        var engine = new NeraFormulaEngine();

        var result = engine.Evaluate(
            "=SUMIF(A1:A3,D1,B1:B3)",
            new FormulaSurfaceTestContext(values));

        Assert.AreEqual(50d, result.Value.RawValue);
        Assert.AreEqual(3, result.Dependencies.Count);
        Assert.IsTrue(result.Dependencies.Any(dependency =>
            dependency.Range == new CellRange(
                new CellAddress(0, 3),
                new CellAddress(0, 3))));
    }

    [TestMethod]
    public void InvalidArgumentsAndExcessiveRangesFailBeforeEnumeration()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate("=COUNTIF(1,\">0\")", context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate("=SUMIFS(A1:A2,A1:A2)", context).ErrorCode);
        var excessive = engine.Evaluate(
            "=COUNTIF(A1:C1048576,\">0\")",
            context);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            excessive.ErrorCode);
        Assert.AreEqual("#NUM!", excessive.Value.RawValue);
    }

    [TestMethod]
    public void AffectedRecalculationTracksCriteriaAndAggregateRanges()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), 1d);
        worksheet.SetValue(new CellAddress(1, 0), 2d);
        worksheet.SetValue(new CellAddress(2, 0), 3d);
        worksheet.SetValue(new CellAddress(0, 1), "North");
        worksheet.SetValue(new CellAddress(1, 1), "South");
        worksheet.SetValue(new CellAddress(2, 1), "North");
        var resultAddress = new CellAddress(0, 3);
        worksheet.SetFormula(
            resultAddress,
            "=SUMIFS(A1:A3,B1:B3,\"North\")");
        var calculation = new WorkbookCalculationEngine();
        calculation.Recalculate(workbook);
        Assert.AreEqual(4d, worksheet.GetValue(resultAddress));

        worksheet.SetValue(new CellAddress(1, 1), "North");
        var affected = calculation.RecalculateAffected(
            workbook,
            worksheet,
            new CellRange(
                new CellAddress(1, 1),
                new CellAddress(1, 1)));

        Assert.AreEqual(6d, worksheet.GetValue(resultAddress));
        Assert.AreEqual(1, affected.FormulaCellCount);
        Assert.AreEqual(1, affected.UpdatedCellCount);
    }
}
