using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FlattenWrapTrimXMatchAndErrorFormulaFunctionTests
{
    [TestMethod]
    public void ToColSupportsIgnoreModesAndColumnScanning()
    {
        var context = new TestContext(new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(0, 2)] = CellValue.FromNumber(3d),
            [new CellAddress(1, 0)] = CellValue.FromError("#N/A"),
            [new CellAddress(1, 1)] = CellValue.FromNumber(5d),
            [new CellAddress(1, 2)] = CellValue.FromNumber(6d),
        });
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=TOCOL(A1:C2)",
            context,
            out var allValues));
        Assert.IsTrue(allValues.IsSuccess);
        Assert.AreEqual(6, allValues.Value!.RowCount);
        Assert.AreEqual(CellValueKind.Blank, allValues.Value[1, 0].Kind);
        Assert.AreEqual("#N/A", allValues.Value[3, 0].RawValue);

        Assert.IsTrue(engine.TryEvaluate(
            "=TOCOL(A1:C2,3,TRUE)",
            context,
            out var filtered));
        Assert.IsTrue(filtered.IsSuccess);
        AssertArray(
            filtered.Value!,
            4,
            1,
            CellValue.FromNumber(1d),
            CellValue.FromNumber(5d),
            CellValue.FromNumber(3d),
            CellValue.FromNumber(6d));
    }

    [TestMethod]
    public void ToRowPreservesRequestedScanOrder()
    {
        var context = new TestContext(new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(0, 2)] = CellValue.FromNumber(3d),
            [new CellAddress(1, 0)] = CellValue.FromError("#N/A"),
            [new CellAddress(1, 1)] = CellValue.FromNumber(5d),
            [new CellAddress(1, 2)] = CellValue.FromNumber(6d),
        });
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=TOROW(A1:C2,1,TRUE)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArray(
            result.Value!,
            1,
            5,
            CellValue.FromNumber(1d),
            CellValue.FromError("#N/A"),
            CellValue.FromNumber(5d),
            CellValue.FromNumber(3d),
            CellValue.FromNumber(6d));
    }

    [TestMethod]
    public void TrimRangeRemovesOnlySelectedOuterBlankEdges()
    {
        var context = new TestContext(new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(1, 1)] = CellValue.FromText("A"),
            [new CellAddress(1, 2)] = CellValue.FromText("B"),
            [new CellAddress(1, 3)] = CellValue.FromText("C"),
            [new CellAddress(2, 1)] = CellValue.FromNumber(1d),
            [new CellAddress(2, 2)] = CellValue.FromNumber(2d),
            [new CellAddress(2, 3)] = CellValue.FromNumber(3d),
        });
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=TRIMRANGE(A1:E4)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArray(
            result.Value!,
            2,
            3,
            CellValue.FromText("A"),
            CellValue.FromText("B"),
            CellValue.FromText("C"),
            CellValue.FromNumber(1d),
            CellValue.FromNumber(2d),
            CellValue.FromNumber(3d));

        Assert.IsTrue(engine.TryEvaluate(
            "=TRIMRANGE(A1:E4,0,1)",
            context,
            out var leadingColumnsOnly));
        Assert.IsTrue(leadingColumnsOnly.IsSuccess);
        Assert.AreEqual(4, leadingColumnsOnly.Value!.RowCount);
        Assert.AreEqual(4, leadingColumnsOnly.Value.ColumnCount);
    }

    [TestMethod]
    public void VStackPadsNarrowInputsWithNotAvailable()
    {
        var context = new TestContext(new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(0, 1)] = CellValue.FromNumber(2d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(3d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(4d),
            [new CellAddress(0, 3)] = CellValue.FromNumber(9d),
        });
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=VSTACK(A1:B2,D1)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArray(
            result.Value!,
            3,
            2,
            CellValue.FromNumber(1d),
            CellValue.FromNumber(2d),
            CellValue.FromNumber(3d),
            CellValue.FromNumber(4d),
            CellValue.FromNumber(9d),
            CellValue.FromError("#N/A"));
    }

    [TestMethod]
    public void WrapColsFillsColumnsAndPadsTheTail()
    {
        var context = new TestContext(CreateColumn(1d, 2d, 3d, 4d, 5d));
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=WRAPCOLS(A1:A5,2)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArray(
            result.Value!,
            2,
            3,
            CellValue.FromNumber(1d),
            CellValue.FromNumber(3d),
            CellValue.FromNumber(5d),
            CellValue.FromNumber(2d),
            CellValue.FromNumber(4d),
            CellValue.FromError("#N/A"));
    }

    [TestMethod]
    public void WrapRowsFillsRowsAndRejectsTwoDimensionalInput()
    {
        var values = CreateColumn(1d, 2d, 3d, 4d, 5d);
        values[new CellAddress(0, 1)] = CellValue.FromNumber(9d);
        var context = new TestContext(values);
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=WRAPROWS(A1:A5,2)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArray(
            result.Value!,
            3,
            2,
            CellValue.FromNumber(1d),
            CellValue.FromNumber(2d),
            CellValue.FromNumber(3d),
            CellValue.FromNumber(4d),
            CellValue.FromNumber(5d),
            CellValue.FromError("#N/A"));

        AssertDynamicError(
            engine,
            "=WRAPROWS(A1:B2,2)",
            context,
            "#VALUE!");
    }

    [TestMethod]
    public void XMatchSupportsExactApproximateReverseAndWildcardModes()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(3d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(5d),
            [new CellAddress(3, 0)] = CellValue.FromNumber(7d),
            [new CellAddress(4, 0)] = CellValue.FromNumber(9d),
            [new CellAddress(0, 1)] = CellValue.FromText("A"),
            [new CellAddress(1, 1)] = CellValue.FromText("B"),
            [new CellAddress(2, 1)] = CellValue.FromText("A"),
            [new CellAddress(3, 1)] = CellValue.FromText("C"),
            [new CellAddress(0, 2)] = CellValue.FromText("Apple"),
            [new CellAddress(1, 2)] = CellValue.FromText("Grape"),
            [new CellAddress(2, 2)] = CellValue.FromText("Graph"),
        };
        var context = new TestContext(values);
        var engine = new NeraFormulaEngine();

        Assert.AreEqual(
            3d,
            EvaluateNumber(engine, "=XMATCH(5,A1:A5)", context),
            1e-12d);
        Assert.AreEqual(
            3d,
            EvaluateNumber(engine, "=XMATCH(6,A1:A5,-1)", context),
            1e-12d);
        Assert.AreEqual(
            4d,
            EvaluateNumber(engine, "=XMATCH(6,A1:A5,1)", context),
            1e-12d);
        Assert.AreEqual(
            3d,
            EvaluateNumber(engine, "=XMATCH(\"A\",B1:B4,0,-1)", context),
            1e-12d);
        Assert.AreEqual(
            2d,
            EvaluateNumber(engine, "=XMATCH(\"Gra*\",C1:C3,2)", context),
            1e-12d);
    }

    [TestMethod]
    public void IfErrorIsLazyAndReplacesArrayErrorsElementWise()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromError("#VALUE!"),
            [new CellAddress(2, 0)] = CellValue.FromNumber(3d),
        };
        var context = new TestContext(values);
        var scalar = new NeraFormulaEngine();

        Assert.AreEqual(
            5d,
            EvaluateNumber(scalar, "=IFERROR(5,1/0)", context),
            1e-12d);
        Assert.AreEqual(
            42d,
            EvaluateNumber(scalar, "=IFERROR(1/0,42)", context),
            1e-12d);

        var dynamic = new NeraDynamicArrayFormulaEngine(scalar);
        Assert.IsTrue(dynamic.TryEvaluate(
            "=IFERROR(A1:A3,0)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArray(
            result.Value!,
            3,
            1,
            CellValue.FromNumber(1d),
            CellValue.FromNumber(0d),
            CellValue.FromNumber(3d));
    }

    [TestMethod]
    public void IfNaReplacesOnlyNotAvailableErrors()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromError("#N/A"),
            [new CellAddress(1, 0)] = CellValue.FromError("#VALUE!"),
            [new CellAddress(2, 0)] = CellValue.FromNumber(3d),
        };
        var context = new TestContext(values);
        var scalar = new NeraFormulaEngine();
        AssertScalarError(
            scalar,
            "=IFNA(1/0,42)",
            context,
            "#DIV/0!");

        var dynamic = new NeraDynamicArrayFormulaEngine(scalar);
        Assert.IsTrue(dynamic.TryEvaluate(
            "=IFNA(A1:A3,0)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArray(
            result.Value!,
            3,
            1,
            CellValue.FromNumber(0d),
            CellValue.FromError("#VALUE!"),
            CellValue.FromNumber(3d));
    }

    [TestMethod]
    public void SwitchSelectsFirstMatchLazilyAndVectorizesExpression()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(2d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(9d),
        };
        var context = new TestContext(values);
        var scalar = new NeraFormulaEngine();

        Assert.AreEqual(
            20d,
            EvaluateNumber(
                scalar,
                "=SWITCH(2,1,1/0,2,20,1/0)",
                context),
            1e-12d);
        Assert.AreEqual(
            99d,
            EvaluateNumber(
                scalar,
                "=SWITCH(9,1,10,99)",
                context),
            1e-12d);
        AssertScalarError(
            scalar,
            "=SWITCH(9,1,10)",
            context,
            "#N/A");

        var dynamic = new NeraDynamicArrayFormulaEngine(scalar);
        Assert.IsTrue(dynamic.TryEvaluate(
            "=SWITCH(A1:A3,1,\"A\",2,\"B\",\"X\")",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArray(
            result.Value!,
            3,
            1,
            CellValue.FromText("A"),
            CellValue.FromText("B"),
            CellValue.FromText("X"));
    }

    private static Dictionary<CellAddress, CellValue> CreateColumn(
        params double[] values)
    {
        var result = new Dictionary<CellAddress, CellValue>();
        for (var index = 0; index < values.Length; index++)
        {
            result[new CellAddress(index, 0)] =
                CellValue.FromNumber(values[index]);
        }
        return result;
    }

    private static double EvaluateNumber(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind, formula);
        return (double)result.Value.RawValue!;
    }

    private static void AssertScalarError(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context,
        string expected)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }

    private static void AssertDynamicError(
        NeraDynamicArrayFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context,
        string expected)
    {
        Assert.IsTrue(engine.TryEvaluate(formula, context, out var result));
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(expected, result.ErrorValue.RawValue, formula);
    }

    private static void AssertArray(
        FormulaArrayValue actual,
        int rows,
        int columns,
        params CellValue[] expected)
    {
        Assert.AreEqual(rows, actual.RowCount);
        Assert.AreEqual(columns, actual.ColumnCount);
        CollectionAssert.AreEqual(expected, actual.ToArray());
    }

    private sealed class TestContext : IFormulaEvaluationContext
    {
        private readonly IReadOnlyDictionary<CellAddress, CellValue> _values;

        public TestContext(
            IReadOnlyDictionary<CellAddress, CellValue>? values = null)
        {
            _values = values ??
                new Dictionary<CellAddress, CellValue>();
        }

        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address) =>
            _values.GetValueOrDefault(address, CellValue.Blank);
    }
}
