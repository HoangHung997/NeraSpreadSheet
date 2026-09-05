using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class ReferenceSelectionAndProjectionFormulaFunctionTests
{
    [TestMethod]
    public void AddressSupportsA1R1C1MissingArgumentsAndSheetNames()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual("$C$2", EvaluateText(engine, "=ADDRESS(2,3)", context));
        Assert.AreEqual("C$2", EvaluateText(engine, "=ADDRESS(2,3,2)", context));
        Assert.AreEqual("$C2", EvaluateText(engine, "=ADDRESS(2,3,3)", context));
        Assert.AreEqual("C2", EvaluateText(engine, "=ADDRESS(2,3,4)", context));
        Assert.AreEqual(
            "R[2]C[3]",
            EvaluateText(engine, "=ADDRESS(2,3,4,FALSE)", context));
        Assert.AreEqual(
            "'Sheet 2'!R2C3",
            EvaluateText(
                engine,
                "=ADDRESS(2,3,,FALSE,\"Sheet 2\")",
                context));

        AssertInvalidValue(engine, "=ADDRESS(0,1)", context);
        AssertInvalidValue(engine, "=ADDRESS(1,16385)", context);
        AssertInvalidValue(engine, "=ADDRESS(A1:A2,1)", context);
    }

    [TestMethod]
    public void AreasCountsReferenceUnionsAndCapturesOnlyChooseSelector()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(2d),
        };
        var context = new FormulaSurfaceTestContext(values);
        var engine = new NeraFormulaEngine();

        var single = engine.Evaluate("=AREAS(A1:B2)", context);
        Assert.AreEqual(1d, GetNumber(single), 1e-12d);
        Assert.AreEqual(0, single.Dependencies.Count);

        Assert.AreEqual(
            3d,
            EvaluateNumber(
                engine,
                "=AREAS((A1:A2,B1:B2,C1))",
                context),
            1e-12d);

        var selected = engine.Evaluate(
            "=AREAS(CHOOSE(A1,B2:B3,(C1:C2,D1:D2)))",
            context);
        Assert.AreEqual(2d, GetNumber(selected), 1e-12d);
        Assert.AreEqual(1, selected.Dependencies.Count);
        Assert.AreEqual(
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(0, 0))),
            selected.Dependencies[0]);

        AssertInvalidValue(engine, "=AREAS(1)", context);
    }

    [TestMethod]
    public void ChooseIsLazyAndPreservesSelectedRangeIdentity()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(2d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(10d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(11d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(7d),
            [new CellAddress(2, 1)] = CellValue.FromNumber(8d),
        };
        var context = new FormulaSurfaceTestContext(values);
        var engine = new NeraFormulaEngine();

        Assert.AreEqual(
            2d,
            EvaluateNumber(engine, "=CHOOSE(2.9,1,2,3)", context),
            1e-12d);
        Assert.AreEqual(
            20d,
            EvaluateNumber(engine, "=CHOOSE(2,1/0,20)", context),
            1e-12d);

        var scalar = engine.Evaluate(
            "=CHOOSE(A1,A2,B2,1/0)",
            context);
        Assert.AreEqual(7d, GetNumber(scalar), 1e-12d);
        CollectionAssert.AreEqual(
            new[]
            {
                new FormulaDependency(
                    null,
                    new CellRange(
                        new CellAddress(0, 0),
                        new CellAddress(0, 0))),
                new FormulaDependency(
                    null,
                    new CellRange(
                        new CellAddress(1, 1),
                        new CellAddress(1, 1))),
            },
            scalar.Dependencies.ToArray());

        var sum = engine.Evaluate(
            "=SUM(CHOOSE(A1,A2:A3,B2:B3))",
            context);
        Assert.AreEqual(15d, GetNumber(sum), 1e-12d);
        Assert.IsTrue(sum.Dependencies.Contains(
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(1, 1),
                    new CellAddress(2, 1)))));

        var arrayEngine = new NeraDynamicArrayFormulaEngine(engine);
        Assert.IsTrue(arrayEngine.TryEvaluate(
            "=CHOOSE(A1,A2:A3,B2:B3)",
            context,
            out var arrayResult));
        Assert.IsTrue(arrayResult.IsSuccess);
        AssertArrayNumbers(arrayResult.Value!, 2, 1, 7d, 8d);
        AssertInvalidValue(engine, "=CHOOSE(0,1,2)", context);
    }

    [TestMethod]
    public void ChooseColsProjectsOrderedDuplicateAndNegativeIndexes()
    {
        var context = new FormulaSurfaceTestContext(CreateGridValues(2, 4));
        var arrayEngine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(arrayEngine.TryEvaluate(
            "=CHOOSECOLS(A1:D2,1,-1,2,2)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArrayNumbers(
            result.Value!,
            2,
            4,
            1d,
            4d,
            2d,
            2d,
            5d,
            8d,
            6d,
            6d);
        Assert.AreEqual(1, result.Dependencies.Count);
        Assert.AreEqual(
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(1, 3))),
            result.Dependencies[0]);

        Assert.IsTrue(arrayEngine.TryEvaluate(
            "=CHOOSECOLS(A1:D2,SEQUENCE(1,2,2,1))",
            context,
            out var dynamicIndexes));
        Assert.IsTrue(dynamicIndexes.IsSuccess);
        AssertArrayNumbers(
            dynamicIndexes.Value!,
            2,
            2,
            2d,
            3d,
            6d,
            7d);

        AssertDynamicError(arrayEngine, "=CHOOSECOLS(A1:D2,0)", context);
        AssertDynamicError(arrayEngine, "=CHOOSECOLS(A1:D2,5)", context);
    }

    [TestMethod]
    public void ChooseRowsProjectsIndexRangesAndLocksRegistryMetadata()
    {
        var values = CreateGridValues(4, 2);
        values[new CellAddress(0, 2)] = CellValue.FromNumber(-2d);
        values[new CellAddress(1, 2)] = CellValue.FromNumber(1d);
        var context = new FormulaSurfaceTestContext(values);
        var arrayEngine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(arrayEngine.TryEvaluate(
            "=CHOOSEROWS(A1:B4,-1,1,2,2)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArrayNumbers(
            result.Value!,
            4,
            2,
            7d,
            8d,
            1d,
            2d,
            3d,
            4d,
            3d,
            4d);

        Assert.IsTrue(arrayEngine.TryEvaluate(
            "=CHOOSEROWS(A1:B4,C1:C2)",
            context,
            out var rangeIndexes));
        Assert.IsTrue(rangeIndexes.IsSuccess);
        AssertArrayNumbers(
            rangeIndexes.Value!,
            2,
            2,
            5d,
            6d,
            1d,
            2d);
        Assert.IsTrue(rangeIndexes.Dependencies.Contains(
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 2),
                    new CellAddress(1, 2)))));
        AssertDynamicError(arrayEngine, "=CHOOSEROWS(A1:B4,0)", context);

        var registry = new BuiltInFormulaFunctionRegistry();
        var descriptor = registry.Descriptors.Single(candidate =>
            candidate.Identity.Name == "ADDRESS");
        Assert.AreEqual("NERA.BUILTIN", descriptor.Identity.Namespace);
        Assert.AreEqual(
            new FormulaFunctionVersion(1, 0, 0),
            descriptor.Version);
        Assert.AreEqual(
            FormulaFunctionApiVersion.Current,
            descriptor.MinimumHostApiVersion);
        Assert.AreEqual(
            FormulaFunctionArgumentCountPolicy.LogicalArguments,
            descriptor.ArgumentCountPolicy);
        Assert.AreEqual(
            FormulaFunctionCapabilities.ScalarArguments |
            FormulaFunctionCapabilities.ReturnsScalar,
            descriptor.Capabilities);
        Assert.AreEqual(
            FormulaFunctionVolatility.Deterministic,
            descriptor.Volatility);
        Assert.AreEqual(
            FormulaFunctionSecurityClassification.Pure,
            descriptor.SecurityClassification);
        Assert.AreEqual(BuiltInFormulaTestCounts.EagerVersioned, registry.Count);
        Assert.AreEqual(
            BuiltInFormulaTestCounts.EagerVersioned,
            registry.VersionCount);
    }

    private static Dictionary<CellAddress, CellValue> CreateGridValues(
        int rowCount,
        int columnCount)
    {
        var values = new Dictionary<CellAddress, CellValue>();
        var number = 1d;
        for (var row = 0; row < rowCount; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                values[new CellAddress(row, column)] =
                    CellValue.FromNumber(number++);
            }
        }
        return values;
    }

    private static string EvaluateText(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(result.IsSuccess, $"Expected success for {formula}.");
        Assert.AreEqual(CellValueKind.Text, result.Value.Kind);
        return (string)result.Value.RawValue!;
    }

    private static double EvaluateNumber(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context) =>
        GetNumber(engine.Evaluate(formula, context));

    private static double GetNumber(FormulaEvaluationResult result)
    {
        Assert.IsTrue(result.IsSuccess, $"Unexpected result: {result.Value}.");
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind);
        return (double)result.Value.RawValue!;
    }

    private static void AssertInvalidValue(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        var result = engine.Evaluate(formula, context);
        Assert.AreEqual(FormulaErrorCode.InvalidValue, result.ErrorCode, formula);
        Assert.AreEqual("#VALUE!", result.Value.RawValue, formula);
    }

    private static void AssertDynamicError(
        NeraDynamicArrayFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        Assert.IsTrue(engine.TryEvaluate(formula, context, out var result));
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(FormulaErrorCode.InvalidValue, result.ErrorCode, formula);
        Assert.AreEqual("#VALUE!", result.ErrorValue.RawValue, formula);
    }

    private static void AssertArrayNumbers(
        FormulaArrayValue value,
        int expectedRows,
        int expectedColumns,
        params double[] expected)
    {
        Assert.AreEqual(expectedRows, value.RowCount);
        Assert.AreEqual(expectedColumns, value.ColumnCount);
        Assert.AreEqual(expected.Length, value.Count);
        var actual = value.ToArray();
        for (var index = 0; index < actual.Length; index++)
        {
            Assert.AreEqual(CellValueKind.Number, actual[index].Kind);
            Assert.AreEqual(
                expected[index],
                (double)actual[index].RawValue!,
                1e-12d,
                $"Unexpected array value at flat index {index}.");
        }
    }
}
