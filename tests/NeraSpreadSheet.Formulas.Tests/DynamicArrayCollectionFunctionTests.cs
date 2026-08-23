using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class DynamicArrayCollectionFunctionTests
{
    [TestMethod]
    public void FilterSelectsRowsAndRetainsBothDependencies()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("A"),
            [new CellAddress(0, 1)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 0)] = CellValue.FromText("B"),
            [new CellAddress(1, 1)] = CellValue.FromNumber(20d),
            [new CellAddress(2, 0)] = CellValue.FromText("C"),
            [new CellAddress(2, 1)] = CellValue.FromNumber(30d),
            [new CellAddress(3, 0)] = CellValue.FromText("D"),
            [new CellAddress(3, 1)] = CellValue.FromNumber(40d),
            [new CellAddress(0, 2)] = CellValue.FromBoolean(true),
            [new CellAddress(1, 2)] = CellValue.FromBoolean(false),
            [new CellAddress(2, 2)] = CellValue.FromBoolean(true),
            [new CellAddress(3, 2)] = CellValue.FromBoolean(false),
        };
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=FILTER(A1:B4,C1:C4)",
            new FormulaSurfaceTestContext(values),
            out var result));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(2, result.Value!.RowCount);
        Assert.AreEqual(2, result.Value.ColumnCount);
        Assert.AreEqual("A", result.Value[0, 0].RawValue);
        Assert.AreEqual(10d, result.Value[0, 1].RawValue);
        Assert.AreEqual("C", result.Value[1, 0].RawValue);
        Assert.AreEqual(30d, result.Value[1, 1].RawValue);
        Assert.AreEqual(2, result.Dependencies.Count);
    }

    [TestMethod]
    public void FilterSelectsColumnsAndUsesFallbackWhenEmpty()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("A"),
            [new CellAddress(0, 1)] = CellValue.FromText("B"),
            [new CellAddress(0, 2)] = CellValue.FromText("C"),
            [new CellAddress(1, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(2d),
            [new CellAddress(1, 2)] = CellValue.FromNumber(3d),
            [new CellAddress(3, 0)] = CellValue.FromBoolean(true),
            [new CellAddress(3, 1)] = CellValue.FromBoolean(false),
            [new CellAddress(3, 2)] = CellValue.FromBoolean(true),
            [new CellAddress(4, 0)] = CellValue.FromBoolean(false),
            [new CellAddress(4, 1)] = CellValue.FromBoolean(false),
        };
        var engine = new NeraDynamicArrayFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.IsTrue(engine.TryEvaluate(
            "=FILTER(A1:C2,A4:C4)",
            context,
            out var columns));
        Assert.IsTrue(columns.IsSuccess);
        Assert.AreEqual(2, columns.Value!.RowCount);
        Assert.AreEqual(2, columns.Value.ColumnCount);
        Assert.AreEqual("A", columns.Value[0, 0].RawValue);
        Assert.AreEqual("C", columns.Value[0, 1].RawValue);
        Assert.AreEqual(1d, columns.Value[1, 0].RawValue);
        Assert.AreEqual(3d, columns.Value[1, 1].RawValue);

        Assert.IsTrue(engine.TryEvaluate(
            "=FILTER(A1:B2,A5:B5,\"none\")",
            context,
            out var fallback));
        Assert.IsTrue(fallback.IsSuccess);
        Assert.AreEqual(1, fallback.Value!.RowCount);
        Assert.AreEqual(1, fallback.Value.ColumnCount);
        Assert.AreEqual("none", fallback.Value[0, 0].RawValue);
    }

    [TestMethod]
    public void FilterRejectsIncompatibleIncludeShape()
    {
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=FILTER(SEQUENCE(2,2),SEQUENCE(3,1))",
            new FormulaSurfaceTestContext(),
            out var result));

        Assert.AreEqual(FormulaErrorCode.InvalidValue, result.ErrorCode);
        Assert.AreEqual("#VALUE!", result.ErrorValue.RawValue);
    }

    [TestMethod]
    public void SortOrdersRowsStablyBySelectedColumn()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("B"),
            [new CellAddress(0, 1)] = CellValue.FromNumber(2d),
            [new CellAddress(1, 0)] = CellValue.FromText("A"),
            [new CellAddress(1, 1)] = CellValue.FromNumber(1d),
            [new CellAddress(2, 0)] = CellValue.FromText("C"),
            [new CellAddress(2, 1)] = CellValue.FromNumber(2d),
        };
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=SORT(A1:B3,2,1)",
            new FormulaSurfaceTestContext(values),
            out var ascending));
        Assert.AreEqual("A", ascending.Value![0, 0].RawValue);
        Assert.AreEqual("B", ascending.Value[1, 0].RawValue);
        Assert.AreEqual("C", ascending.Value[2, 0].RawValue);

        Assert.IsTrue(engine.TryEvaluate(
            "=SORT(A1:B3,2,-1)",
            new FormulaSurfaceTestContext(values),
            out var descending));
        Assert.AreEqual("B", descending.Value![0, 0].RawValue);
        Assert.AreEqual("C", descending.Value[1, 0].RawValue);
        Assert.AreEqual("A", descending.Value[2, 0].RawValue);
    }

    [TestMethod]
    public void SortCanOrderColumnsBySelectedRow()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("C"),
            [new CellAddress(0, 1)] = CellValue.FromText("A"),
            [new CellAddress(0, 2)] = CellValue.FromText("B"),
            [new CellAddress(1, 0)] = CellValue.FromNumber(3d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 2)] = CellValue.FromNumber(2d),
        };
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=SORT(A1:C2,2,1,TRUE())",
            new FormulaSurfaceTestContext(values),
            out var result));

        Assert.AreEqual("A", result.Value![0, 0].RawValue);
        Assert.AreEqual("B", result.Value[0, 1].RawValue);
        Assert.AreEqual("C", result.Value[0, 2].RawValue);
        Assert.AreEqual(1d, result.Value[1, 0].RawValue);
        Assert.AreEqual(2d, result.Value[1, 1].RawValue);
        Assert.AreEqual(3d, result.Value[1, 2].RawValue);
    }

    [TestMethod]
    public void UniquePreservesFirstRowsAndSupportsExactlyOnce()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("A"),
            [new CellAddress(0, 1)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromText("B"),
            [new CellAddress(1, 1)] = CellValue.FromNumber(2d),
            [new CellAddress(2, 0)] = CellValue.FromText("A"),
            [new CellAddress(2, 1)] = CellValue.FromNumber(1d),
            [new CellAddress(3, 0)] = CellValue.FromText("C"),
            [new CellAddress(3, 1)] = CellValue.FromNumber(3d),
        };
        var engine = new NeraDynamicArrayFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.IsTrue(engine.TryEvaluate(
            "=UNIQUE(A1:B4)",
            context,
            out var unique));
        Assert.AreEqual(3, unique.Value!.RowCount);
        Assert.AreEqual("A", unique.Value[0, 0].RawValue);
        Assert.AreEqual("B", unique.Value[1, 0].RawValue);
        Assert.AreEqual("C", unique.Value[2, 0].RawValue);

        Assert.IsTrue(engine.TryEvaluate(
            "=UNIQUE(A1:B4,FALSE(),TRUE())",
            context,
            out var exactlyOnce));
        Assert.AreEqual(2, exactlyOnce.Value!.RowCount);
        Assert.AreEqual("B", exactlyOnce.Value[0, 0].RawValue);
        Assert.AreEqual("C", exactlyOnce.Value[1, 0].RawValue);
    }

    [TestMethod]
    public void UniqueCanCompareColumnsAndNestInsideSort()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("B"),
            [new CellAddress(0, 1)] = CellValue.FromText("A"),
            [new CellAddress(0, 2)] = CellValue.FromText("B"),
            [new CellAddress(1, 0)] = CellValue.FromNumber(2d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 2)] = CellValue.FromNumber(2d),
        };
        var engine = new NeraDynamicArrayFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.IsTrue(engine.TryEvaluate(
            "=UNIQUE(A1:C2,TRUE())",
            context,
            out var columns));
        Assert.AreEqual(2, columns.Value!.ColumnCount);
        Assert.AreEqual("B", columns.Value[0, 0].RawValue);
        Assert.AreEqual("A", columns.Value[0, 1].RawValue);

        Assert.IsTrue(engine.TryEvaluate(
            "=SORT(UNIQUE(A1:C1),1,1,TRUE())",
            context,
            out var nested));
        Assert.AreEqual(1, nested.Value!.RowCount);
        Assert.AreEqual(2, nested.Value.ColumnCount);
        Assert.AreEqual("A", nested.Value[0, 0].RawValue);
        Assert.AreEqual("B", nested.Value[0, 1].RawValue);
    }
}
