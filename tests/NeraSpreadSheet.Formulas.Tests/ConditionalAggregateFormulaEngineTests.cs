using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class ConditionalAggregateFormulaEngineTests
{
    [TestMethod]
    public void CountIfSupportsNumericTextWildcardAndBlankCriteria()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(20d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(30d),
            [new CellAddress(3, 0)] = CellValue.FromText("North"),
            [new CellAddress(4, 0)] = CellValue.FromText("South"),
        };
        var engine = new ConditionalAggregateFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(2d, engine.Evaluate(
            "=COUNTIF(A1:A5,\">=20\")",
            context).Value.RawValue);
        Assert.AreEqual(1d, engine.Evaluate(
            "=COUNTIF(A1:A5,\"N*\")",
            context).Value.RawValue);
        Assert.AreEqual(1d, engine.Evaluate(
            "=COUNTIF(A1:A6,\"=\")",
            context).Value.RawValue);
        Assert.AreEqual(5d, engine.Evaluate(
            "=COUNTIF(A1:A6,\"<>\")",
            context).Value.RawValue);
    }

    [TestMethod]
    public void SumIfAndAverageIfUseAlignedAggregateRanges()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("A"),
            [new CellAddress(1, 0)] = CellValue.FromText("B"),
            [new CellAddress(2, 0)] = CellValue.FromText("A"),
            [new CellAddress(3, 0)] = CellValue.FromText("A"),
            [new CellAddress(0, 1)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(20d),
            [new CellAddress(2, 1)] = CellValue.FromNumber(30d),
            [new CellAddress(3, 1)] = CellValue.FromText("ignored"),
        };
        var engine = new ConditionalAggregateFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(40d, engine.Evaluate(
            "=SUMIF(A1:A4,\"A\",B1:B4)",
            context).Value.RawValue);
        Assert.AreEqual(20d, engine.Evaluate(
            "=AVERAGEIF(A1:A4,\"A\",B1:B4)",
            context).Value.RawValue);
        Assert.AreEqual(0d, engine.Evaluate(
            "=SUMIF(A1:A4,\"missing\",B1:B4)",
            context).Value.RawValue);
        Assert.AreEqual(
            FormulaErrorCode.DivisionByZero,
            engine.Evaluate(
                "=AVERAGEIF(A1:A4,\"missing\",B1:B4)",
                context).ErrorCode);
    }

    [TestMethod]
    public void IFSFunctionsCombineCriteriaByAndWithMatchingShapes()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("North"),
            [new CellAddress(1, 0)] = CellValue.FromText("North"),
            [new CellAddress(2, 0)] = CellValue.FromText("South"),
            [new CellAddress(3, 0)] = CellValue.FromText("North"),
            [new CellAddress(0, 1)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(25d),
            [new CellAddress(2, 1)] = CellValue.FromNumber(30d),
            [new CellAddress(3, 1)] = CellValue.FromNumber(40d),
            [new CellAddress(0, 2)] = CellValue.FromNumber(100d),
            [new CellAddress(1, 2)] = CellValue.FromNumber(200d),
            [new CellAddress(2, 2)] = CellValue.FromNumber(300d),
            [new CellAddress(3, 2)] = CellValue.FromNumber(400d),
        };
        var engine = new ConditionalAggregateFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(2d, engine.Evaluate(
            "=COUNTIFS(A1:A4,\"North\",B1:B4,\">=20\")",
            context).Value.RawValue);
        Assert.AreEqual(600d, engine.Evaluate(
            "=SUMIFS(C1:C4,A1:A4,\"North\",B1:B4,\">=20\")",
            context).Value.RawValue);
        Assert.AreEqual(300d, engine.Evaluate(
            "=AVERAGEIFS(C1:C4,A1:A4,\"North\",B1:B4,\">=20\")",
            context).Value.RawValue);
    }

    [TestMethod]
    public void ShapeMismatchAndMatchedAggregateErrorReturnErrors()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("A"),
            [new CellAddress(1, 0)] = CellValue.FromText("A"),
            [new CellAddress(0, 1)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 1)] = CellValue.FromError("#N/A"),
        };
        var engine = new ConditionalAggregateFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=SUMIF(A1:A2,\"A\",B1:B3)",
                context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.NotAvailable,
            engine.Evaluate(
                "=SUMIF(A1:A2,\"A\",B1:B2)",
                context).ErrorCode);
    }

    [TestMethod]
    public void CriteriaCellAndAllInspectedRangesBecomeDependencies()
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
        var engine = new ConditionalAggregateFormulaEngine();

        var result = engine.Evaluate(
            "=SUMIF(A1:A3,D1,B1:B3)",
            new FormulaSurfaceTestContext(values));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(50d, result.Value.RawValue);
        Assert.AreEqual(3, result.Dependencies.Count);
        CollectionAssert.AreEquivalent(
            new[]
            {
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(2, 0)),
                new CellRange(
                    new CellAddress(0, 1),
                    new CellAddress(2, 1)),
                new CellRange(
                    new CellAddress(0, 3),
                    new CellAddress(0, 3)),
            },
            result.Dependencies
                .Select(static dependency => dependency.Range)
                .ToArray());
    }

    [TestMethod]
    public void ConditionalAggregatesCanBeNestedInOrdinaryExpressions()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(2d),
            [new CellAddress(0, 1)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(20d),
        };
        var engine = new ConditionalAggregateFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        var result = engine.Evaluate(
            "=SUMIF(A1:A2,\">1\",B1:B2)+5",
            context);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(25d, result.Value.RawValue);
    }

    [TestMethod]
    public void LazyControlDoesNotEvaluateUnselectedConditionalAggregateBranch()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("A"),
            [new CellAddress(0, 1)] = CellValue.FromError("#N/A"),
        };
        var engine = new ConditionalAggregateFormulaEngine();

        var result = engine.Evaluate(
            "=IF(FALSE(),SUMIF(A1:A1,\"A\",B1:B1),99)",
            new FormulaSurfaceTestContext(values));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(99d, result.Value.RawValue);
        Assert.AreEqual(0, result.Dependencies.Count);
    }
}
