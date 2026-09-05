using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class LookupReferenceAndOrderingFormulaFunctionTests
{
    [TestMethod]
    public void LookupSupportsVectorAndArrayApproximateMatches()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(3d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(5d),
            [new CellAddress(0, 1)] = CellValue.FromText("One"),
            [new CellAddress(1, 1)] = CellValue.FromText("Three"),
            [new CellAddress(2, 1)] = CellValue.FromText("Five"),
        };
        var context = new F011TestContext(values);
        var engine = new NeraFormulaEngine();

        var vector = engine.Evaluate("=LOOKUP(4,A1:A3,B1:B3)", context);
        Assert.AreEqual("Three", GetText(vector));
        Assert.AreEqual(2, vector.Dependencies.Count);

        var array = engine.Evaluate("=LOOKUP(4,A1:B3)", context);
        Assert.AreEqual("Three", GetText(array));
        Assert.AreEqual("#N/A", engine.Evaluate(
            "=LOOKUP(0,A1:A3,B1:B3)", context).Value.RawValue);
    }

    [TestMethod]
    public void OffsetPreservesRangeIdentityAndSpills()
    {
        var values = CreateGridValues(4, 4);
        var context = new F011TestContext(values);
        var scalar = new NeraFormulaEngine();

        var sum = scalar.Evaluate("=SUM(OFFSET(A1,1,1,2,2))", context);
        Assert.AreEqual(34d, GetNumber(sum), 1e-12d);
        CollectionAssert.Contains(
            sum.Dependencies.ToArray(),
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(1, 1),
                    new CellAddress(2, 2))));

        var dynamic = new NeraDynamicArrayFormulaEngine(scalar);
        Assert.IsTrue(dynamic.TryEvaluate(
            "=OFFSET(A1,1,1,2,2)",
            context,
            out var spill));
        Assert.IsTrue(spill.IsSuccess);
        AssertArrayNumbers(spill.Value!, 2, 2, 6d, 7d, 10d, 11d);
        Assert.AreEqual("#REF!", scalar.Evaluate(
            "=OFFSET(A1,-1,0)", context).Value.RawValue);
    }

    [TestMethod]
    public void PercentOfSumsNumericValuesAndRejectsZeroTotal()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(20d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(5d),
            [new CellAddress(3, 0)] = CellValue.FromNumber(15d),
        };
        var context = new F011TestContext(values);
        var engine = new NeraFormulaEngine();

        Assert.AreEqual(
            0.6d,
            GetNumber(engine.Evaluate(
                "=PERCENTOF(A1:A2,A1:A4)", context)),
            1e-12d);
        Assert.AreEqual(
            "#DIV/0!",
            engine.Evaluate("=PERCENTOF(A1:A2,0)", context).Value.RawValue);

        var registry = new BuiltInFormulaFunctionRegistry();
        Assert.AreEqual(BuiltInFormulaTestCounts.EagerVersioned, registry.Count);
        Assert.IsTrue(registry.TryGetDescriptor(
            "PERCENTOF",
            out var descriptor));
        Assert.AreEqual(
            FormulaFunctionArgumentCountPolicy.LogicalArguments,
            descriptor.ArgumentCountPolicy);
    }

    [TestMethod]
    public void PivotByAggregatesTwoAxesAndSupportsPercentOf()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("Product"),
            [new CellAddress(0, 1)] = CellValue.FromText("Year"),
            [new CellAddress(0, 2)] = CellValue.FromText("Sales"),
            [new CellAddress(1, 0)] = CellValue.FromText("P1"),
            [new CellAddress(1, 1)] = CellValue.FromNumber(2025d),
            [new CellAddress(1, 2)] = CellValue.FromNumber(10d),
            [new CellAddress(2, 0)] = CellValue.FromText("P1"),
            [new CellAddress(2, 1)] = CellValue.FromNumber(2026d),
            [new CellAddress(2, 2)] = CellValue.FromNumber(20d),
            [new CellAddress(3, 0)] = CellValue.FromText("P2"),
            [new CellAddress(3, 1)] = CellValue.FromNumber(2025d),
            [new CellAddress(3, 2)] = CellValue.FromNumber(5d),
            [new CellAddress(4, 0)] = CellValue.FromText("P2"),
            [new CellAddress(4, 1)] = CellValue.FromNumber(2026d),
            [new CellAddress(4, 2)] = CellValue.FromNumber(15d),
        };
        var context = new F011TestContext(values);
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=PIVOTBY(A1:A5,B1:B5,C1:C5,SUM,3,1,1,1,1)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(4, result.Value!.RowCount);
        Assert.AreEqual(4, result.Value.ColumnCount);
        Assert.AreEqual("Product", result.Value[0, 0].RawValue);
        Assert.AreEqual(2025d, GetNumber(result.Value[0, 1]), 1e-12d);
        Assert.AreEqual(2026d, GetNumber(result.Value[0, 2]), 1e-12d);
        Assert.AreEqual("P1", result.Value[1, 0].RawValue);
        Assert.AreEqual(10d, GetNumber(result.Value[1, 1]), 1e-12d);
        Assert.AreEqual(20d, GetNumber(result.Value[1, 2]), 1e-12d);
        Assert.AreEqual(30d, GetNumber(result.Value[1, 3]), 1e-12d);
        Assert.AreEqual("Grand Total", result.Value[3, 0].RawValue);
        Assert.AreEqual(50d, GetNumber(result.Value[3, 3]), 1e-12d);

        Assert.IsTrue(engine.TryEvaluate(
            "=PIVOTBY(A1:A5,B1:B5,C1:C5,PERCENTOF,1,0,1,0,1,,2)",
            context,
            out var percent));
        Assert.IsTrue(percent.IsSuccess);
        Assert.AreEqual(0.2d, GetNumber(percent.Value![0, 1]), 1e-12d);
        Assert.AreEqual(0.4d, GetNumber(percent.Value[0, 2]), 1e-12d);
    }

    [TestMethod]
    public void RowUsesCurrentCellReferenceGeometryAndSpill()
    {
        var context = new F011TestContext(
            currentAddress: new CellAddress(6, 4));
        var scalar = new NeraFormulaEngine();

        Assert.AreEqual(
            7d,
            GetNumber(scalar.Evaluate("=ROW()", context)),
            1e-12d);
        Assert.AreEqual(
            3d,
            GetNumber(scalar.Evaluate("=ROW(C3:E5)", context)),
            1e-12d);

        var dynamic = new NeraDynamicArrayFormulaEngine(scalar);
        Assert.IsTrue(dynamic.TryEvaluate(
            "=ROW(C3:E5)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArrayNumbers(result.Value!, 3, 1, 3d, 4d, 5d);
        Assert.AreEqual(0, result.Dependencies.Count);
    }

    [TestMethod]
    public void RowsCountsReferenceScalarAndDynamicArrayShapes()
    {
        var context = new F011TestContext();
        var scalar = new NeraFormulaEngine();

        Assert.AreEqual(
            4d,
            GetNumber(scalar.Evaluate("=ROWS(B2:C5)", context)),
            1e-12d);
        Assert.AreEqual(
            1d,
            GetNumber(scalar.Evaluate("=ROWS(42)", context)),
            1e-12d);

        var dynamic = new NeraDynamicArrayFormulaEngine(scalar);
        Assert.IsTrue(dynamic.TryEvaluate(
            "=ROWS(SEQUENCE(4,3))",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArrayNumbers(result.Value!, 1, 1, 4d);
    }

    [TestMethod]
    public void SheetReturnsCurrentNamedAndReferencedSheetIndexes()
    {
        var context = new F011TestContext(
            currentWorksheetName: "Sheet2");
        var engine = new NeraFormulaEngine();

        Assert.AreEqual(
            2d,
            GetNumber(engine.Evaluate("=SHEET()", context)),
            1e-12d);
        Assert.AreEqual(
            3d,
            GetNumber(engine.Evaluate("=SHEET(\"Data\")", context)),
            1e-12d);
        Assert.AreEqual(
            1d,
            GetNumber(engine.Evaluate("=SHEET(Sheet1!A1)", context)),
            1e-12d);
        Assert.AreEqual(
            "#N/A",
            engine.Evaluate("=SHEET(\"Missing\")", context).Value.RawValue);
    }

    [TestMethod]
    public void SheetsCountsWorkbookAndDistinctReferenceSheets()
    {
        var context = new F011TestContext(
            currentWorksheetName: "Sheet2");
        var engine = new NeraFormulaEngine();

        Assert.AreEqual(
            3d,
            GetNumber(engine.Evaluate("=SHEETS()", context)),
            1e-12d);
        Assert.AreEqual(
            2d,
            GetNumber(engine.Evaluate(
                "=SHEETS((Sheet1!A1,Data!B2,Sheet1!C3))",
                context)),
            1e-12d);
    }

    [TestMethod]
    public void SortBySupportsStableMultipleKeys()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("B"),
            [new CellAddress(0, 1)] = CellValue.FromNumber(2d),
            [new CellAddress(1, 0)] = CellValue.FromText("A"),
            [new CellAddress(1, 1)] = CellValue.FromNumber(3d),
            [new CellAddress(2, 0)] = CellValue.FromText("B"),
            [new CellAddress(2, 1)] = CellValue.FromNumber(1d),
            [new CellAddress(3, 0)] = CellValue.FromText("A"),
            [new CellAddress(3, 1)] = CellValue.FromNumber(3d),
        };
        var context = new F011TestContext(values);
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=SORTBY(A1:B4,A1:A4,1,B1:B4,-1)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual("A", result.Value![0, 0].RawValue);
        Assert.AreEqual(3d, GetNumber(result.Value[0, 1]), 1e-12d);
        Assert.AreEqual("A", result.Value[1, 0].RawValue);
        Assert.AreEqual("B", result.Value[2, 0].RawValue);
        Assert.AreEqual(2d, GetNumber(result.Value[2, 1]), 1e-12d);
        Assert.AreEqual(3, result.Dependencies.Count);
    }

    [TestMethod]
    public void TakeSelectsStartEndAndOptionalColumns()
    {
        var context = new F011TestContext(CreateGridValues(4, 4));
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=TAKE(A1:D4,2,-2)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        AssertArrayNumbers(result.Value!, 2, 2, 3d, 4d, 7d, 8d);

        Assert.IsTrue(engine.TryEvaluate(
            "=TAKE(A1:D4,-2)",
            context,
            out var trailing));
        Assert.IsTrue(trailing.IsSuccess);
        AssertArrayNumbers(
            trailing.Value!,
            2,
            4,
            9d,
            10d,
            11d,
            12d,
            13d,
            14d,
            15d,
            16d);

        Assert.IsTrue(engine.TryEvaluate(
            "=TAKE(A1:D4,,2)",
            context,
            out var columnsOnly));
        Assert.IsTrue(columnsOnly.IsSuccess);
        Assert.AreEqual(4, columnsOnly.Value!.RowCount);
        Assert.AreEqual(2, columnsOnly.Value.ColumnCount);
        AssertDynamicError(engine, "=TAKE(A1:D4,0)", context, "#CALC!");
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

    private static double GetNumber(FormulaEvaluationResult result)
    {
        Assert.IsTrue(result.IsSuccess, $"Unexpected result: {result.Value}.");
        return GetNumber(result.Value);
    }

    private static double GetNumber(CellValue value)
    {
        Assert.AreEqual(CellValueKind.Number, value.Kind);
        return (double)value.RawValue!;
    }

    private static string GetText(FormulaEvaluationResult result)
    {
        Assert.IsTrue(result.IsSuccess, $"Unexpected result: {result.Value}.");
        Assert.AreEqual(CellValueKind.Text, result.Value.Kind);
        return (string)result.Value.RawValue!;
    }

    private static void AssertDynamicError(
        NeraDynamicArrayFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context,
        string expectedError)
    {
        Assert.IsTrue(engine.TryEvaluate(formula, context, out var result));
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(expectedError, result.ErrorValue.RawValue, formula);
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
                $"Unexpected value at flat index {index}.");
        }
    }

    private sealed class F011TestContext :
        IFormulaReferenceIntrospectionContext,
        IFormulaWorkbookMetadataEvaluationContext
    {
        private static readonly string[] WorksheetNames =
            ["Sheet1", "Sheet2", "Data"];
        private readonly IReadOnlyDictionary<CellAddress, CellValue> _values;

        public F011TestContext(
            IReadOnlyDictionary<CellAddress, CellValue>? values = null,
            CellAddress? currentAddress = null,
            string currentWorksheetName = "Sheet1")
        {
            _values = values ??
                new Dictionary<CellAddress, CellValue>();
            CurrentCellAddress = currentAddress ?? new CellAddress(0, 0);
            CurrentWorksheetName = currentWorksheetName;
        }

        public string CurrentWorksheetName { get; }

        public CellAddress CurrentCellAddress { get; }

        public int WorksheetCount => WorksheetNames.Length;

        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address) =>
            _values.GetValueOrDefault(address, CellValue.Blank);

        public bool TryGetCellFormula(
            string? worksheetName,
            CellAddress address,
            out string? formula)
        {
            formula = null;
            return true;
        }

        public bool TryGetWorksheetIndex(
            string? worksheetName,
            out int oneBasedIndex)
        {
            var requested = worksheetName ?? CurrentWorksheetName;
            for (var index = 0; index < WorksheetNames.Length; index++)
            {
                if (!string.Equals(
                        WorksheetNames[index],
                        requested,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                oneBasedIndex = index + 1;
                return true;
            }

            oneBasedIndex = default;
            return false;
        }
    }
}
