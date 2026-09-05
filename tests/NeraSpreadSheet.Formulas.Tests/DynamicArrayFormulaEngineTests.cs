using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class DynamicArrayFormulaEngineTests
{
    [TestMethod]
    public void SequenceCreatesRowMajorArrayWithOptionalArguments()
    {
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=SEQUENCE(2,3,10,2)",
            new FormulaSurfaceTestContext(),
            out var result));

        Assert.IsTrue(result.IsSuccess);
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(2, result.Value.RowCount);
        Assert.AreEqual(3, result.Value.ColumnCount);
        Assert.AreEqual(10d, result.Value[0, 0].RawValue);
        Assert.AreEqual(14d, result.Value[0, 2].RawValue);
        Assert.AreEqual(16d, result.Value[1, 0].RawValue);
        Assert.AreEqual(20d, result.Value[1, 2].RawValue);
        Assert.AreEqual(0, result.Dependencies.Count);
    }

    [TestMethod]
    public void SequenceScalarArgumentsRetainDependencies()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(2d),
        };
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=SEQUENCE(A1,2,A1+8,0.5)",
            new FormulaSurfaceTestContext(values),
            out var result));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(2, result.Value!.RowCount);
        Assert.AreEqual(2, result.Value.ColumnCount);
        Assert.AreEqual(10d, result.Value[0, 0].RawValue);
        Assert.AreEqual(10.5d, result.Value[0, 1].RawValue);
        Assert.AreEqual(11d, result.Value[1, 0].RawValue);
        Assert.AreEqual(11.5d, result.Value[1, 1].RawValue);
        Assert.AreEqual(1, result.Dependencies.Count);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(0, 0)),
            result.Dependencies[0].Range);
    }

    [TestMethod]
    public void TransposeRangePreservesShapeValuesAndDependency()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(0, 1)] = CellValue.FromNumber(2d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(3d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(4d),
        };
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=TRANSPOSE(A1:B2)",
            new FormulaSurfaceTestContext(values),
            out var result));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(2, result.Value!.RowCount);
        Assert.AreEqual(2, result.Value.ColumnCount);
        Assert.AreEqual(1d, result.Value[0, 0].RawValue);
        Assert.AreEqual(3d, result.Value[0, 1].RawValue);
        Assert.AreEqual(2d, result.Value[1, 0].RawValue);
        Assert.AreEqual(4d, result.Value[1, 1].RawValue);
        Assert.AreEqual(1, result.Dependencies.Count);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(1, 1)),
            result.Dependencies[0].Range);
    }

    [TestMethod]
    public void TransposeSupportsNestedSequence()
    {
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=TRANSPOSE(SEQUENCE(2,3))",
            new FormulaSurfaceTestContext(),
            out var result));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(3, result.Value!.RowCount);
        Assert.AreEqual(2, result.Value.ColumnCount);
        Assert.AreEqual(1d, result.Value[0, 0].RawValue);
        Assert.AreEqual(4d, result.Value[0, 1].RawValue);
        Assert.AreEqual(3d, result.Value[2, 0].RawValue);
        Assert.AreEqual(6d, result.Value[2, 1].RawValue);
    }

    [TestMethod]
    public void InvalidAndOversizedSequenceDimensionsReturnErrors()
    {
        var engine = new NeraDynamicArrayFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.IsTrue(engine.TryEvaluate(
            "=SEQUENCE(0)",
            context,
            out var zero));
        Assert.AreEqual(FormulaErrorCode.InvalidValue, zero.ErrorCode);
        Assert.AreEqual("#VALUE!", zero.ErrorValue.RawValue);

        Assert.IsTrue(engine.TryEvaluate(
            "=SEQUENCE(1.5)",
            context,
            out var fractional));
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            fractional.ErrorCode);

        Assert.IsTrue(engine.TryEvaluate(
            "=SEQUENCE(1000000,2)",
            context,
            out var oversized));
        Assert.AreEqual(FormulaErrorCode.InvalidValue, oversized.ErrorCode);
        Assert.AreEqual("#NUM!", oversized.ErrorValue.RawValue);
    }

    [TestMethod]
    public void ScalarCompatibilityReturnsTopLeftAndDependencies()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(3d),
        };
        var engine = new DynamicArrayAwareFormulaEngine();

        var result = engine.Evaluate(
            "=SEQUENCE(A1,2,7,1)",
            new FormulaSurfaceTestContext(values));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(7d, result.Value.RawValue);
        Assert.AreEqual(1, result.Dependencies.Count);
        Assert.AreEqual(
            new CellAddress(0, 0),
            result.Dependencies[0].Range.TopLeft);
    }

    [TestMethod]
    public void NonDynamicRootIsLeftForTheScalarEngine()
    {
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsFalse(engine.TryEvaluate(
            "=SUM(1,2)",
            new FormulaSurfaceTestContext(),
            out _));
    }
}
