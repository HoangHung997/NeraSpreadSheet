using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class ReferenceIntrospectionAndArrayShapeFormulaFunctionTests
{
    [TestMethod]
    public void ColumnUsesCurrentCellReferenceGeometryAndHorizontalSpill()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(2d),
        };
        var context = new IntrospectionContext(
            values,
            currentAddress: new CellAddress(6, 4));
        var engine = new NeraFormulaEngine();

        Assert.AreEqual(
            5d,
            EvaluateNumber(engine, "=COLUMN()", context),
            1e-12d);

        var staticRange = engine.Evaluate("=COLUMN(C3:E8)", context);
        Assert.AreEqual(3d, GetNumber(staticRange), 1e-12d);
        Assert.AreEqual(0, staticRange.Dependencies.Count);

        var selected = engine.Evaluate(
            "=COLUMN(CHOOSE(A1,B1:B2,D1:F2))",
            context);
        Assert.AreEqual(4d, GetNumber(selected), 1e-12d);
        Assert.AreEqual(1, selected.Dependencies.Count);
        Assert.AreEqual(
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(0, 0))),
            selected.Dependencies[0]);

        var arrayEngine = new NeraDynamicArrayFormulaEngine(engine);
        Assert.IsTrue(arrayEngine.TryEvaluate(
            "=COLUMN(C3:E8)",
            context,
            out var arrayResult));
        Assert.IsTrue(arrayResult.IsSuccess);
        AssertArrayNumbers(arrayResult.Value!, 1, 3, 3d, 4d, 5d);
        Assert.AreEqual(0, arrayResult.Dependencies.Count);

        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetFormula(
            new CellAddress(1, 0),
            "=COLUMN(C1:E1)");
        new DynamicArrayWorkbookCalculationEngine().Recalculate(workbook);
        Assert.AreEqual(
            3d,
            GetCellNumber(worksheet, new CellAddress(1, 0)),
            1e-12d);
        Assert.AreEqual(
            4d,
            GetCellNumber(worksheet, new CellAddress(1, 1)),
            1e-12d);
        Assert.AreEqual(
            5d,
            GetCellNumber(worksheet, new CellAddress(1, 2)),
            1e-12d);
    }

    [TestMethod]
    public void ColumnsReadsReferenceScalarAndDynamicArrayShapes()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
        };
        var context = new IntrospectionContext(values);
        var engine = new NeraFormulaEngine();

        var staticRange = engine.Evaluate("=COLUMNS(C3:E8)", context);
        Assert.AreEqual(3d, GetNumber(staticRange), 1e-12d);
        Assert.AreEqual(0, staticRange.Dependencies.Count);
        Assert.AreEqual(
            1d,
            EvaluateNumber(engine, "=COLUMNS(42)", context),
            1e-12d);

        var arrayEngine = new NeraDynamicArrayFormulaEngine(engine);
        Assert.IsTrue(arrayEngine.TryEvaluate(
            "=COLUMNS(SEQUENCE(2,4))",
            context,
            out var dynamicShape));
        Assert.IsTrue(dynamicShape.IsSuccess);
        AssertArrayNumbers(dynamicShape.Value!, 1, 1, 4d);

        Assert.IsTrue(arrayEngine.TryEvaluate(
            "=COLUMNS(CHOOSE(A1,SEQUENCE(2,3),A2:B2))",
            context,
            out var selectedShape));
        Assert.IsTrue(selectedShape.IsSuccess);
        AssertArrayNumbers(selectedShape.Value!, 1, 1, 3d);
        Assert.AreEqual(1, selectedShape.Dependencies.Count);
        Assert.AreEqual(
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(0, 0))),
            selectedShape.Dependencies[0]);

        AssertDynamicError(
            arrayEngine,
            "=COLUMNS((A1:B2,D1:E2))",
            context,
            "#VALUE!");
    }

    [TestMethod]
    public void DropRemovesLeadingTrailingAndOptionalDimensions()
    {
        var context = new IntrospectionContext(CreateGridValues(4, 4));
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=DROP(A1:D4,1,-1)",
            context,
            out var leadingRowsTrailingColumn));
        Assert.IsTrue(leadingRowsTrailingColumn.IsSuccess);
        AssertArrayNumbers(
            leadingRowsTrailingColumn.Value!,
            3,
            3,
            5d,
            6d,
            7d,
            9d,
            10d,
            11d,
            13d,
            14d,
            15d);

        Assert.IsTrue(engine.TryEvaluate(
            "=DROP(A1:D4,-1,1)",
            context,
            out var trailingRowLeadingColumn));
        Assert.IsTrue(trailingRowLeadingColumn.IsSuccess);
        AssertArrayNumbers(
            trailingRowLeadingColumn.Value!,
            3,
            3,
            2d,
            3d,
            4d,
            6d,
            7d,
            8d,
            10d,
            11d,
            12d);

        Assert.IsTrue(engine.TryEvaluate(
            "=DROP(A1:D4,,2)",
            context,
            out var columnsOnly));
        Assert.IsTrue(columnsOnly.IsSuccess);
        AssertArrayNumbers(
            columnsOnly.Value!,
            4,
            2,
            3d,
            4d,
            7d,
            8d,
            11d,
            12d,
            15d,
            16d);

        AssertDynamicError(engine, "=DROP(A1:D4,0)", context, "#CALC!");
        AssertDynamicError(engine, "=DROP(A1:D4,4)", context, "#CALC!");
    }

    [TestMethod]
    public void ExpandPadsDefaultsDimensionsAndEnforcesLimits()
    {
        var context = new IntrospectionContext(CreateGridValues(2, 2));
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=EXPAND(A1:B2,3,4,0)",
            context,
            out var padded));
        Assert.IsTrue(padded.IsSuccess);
        AssertArrayNumbers(
            padded.Value!,
            3,
            4,
            1d,
            2d,
            0d,
            0d,
            3d,
            4d,
            0d,
            0d,
            0d,
            0d,
            0d,
            0d);

        Assert.IsTrue(engine.TryEvaluate(
            "=EXPAND(A1:B2,3,3)",
            context,
            out var defaultPadding));
        Assert.IsTrue(defaultPadding.IsSuccess);
        Assert.AreEqual("#N/A", defaultPadding.Value![0, 2].RawValue);
        Assert.AreEqual("#N/A", defaultPadding.Value[2, 0].RawValue);

        Assert.IsTrue(engine.TryEvaluate(
            "=EXPAND(A1:B2,,3,\"-\")",
            context,
            out var defaultRows));
        Assert.IsTrue(defaultRows.IsSuccess);
        Assert.AreEqual(2, defaultRows.Value!.RowCount);
        Assert.AreEqual(3, defaultRows.Value.ColumnCount);
        Assert.AreEqual("-", defaultRows.Value[0, 2].RawValue);
        Assert.AreEqual("-", defaultRows.Value[1, 2].RawValue);

        AssertDynamicError(
            engine,
            "=EXPAND(A1:B2,1,2)",
            context,
            "#VALUE!");
        AssertDynamicError(
            engine,
            "=EXPAND(A1:B2,1001,1000)",
            context,
            "#NUM!");
    }

    [TestMethod]
    public void FormulaTextReadsMetadataLazilyAndWorkbookContext()
    {
        var formulaAddress = new CellAddress(0, 0);
        var selectorAddress = new CellAddress(0, 2);
        var selfAddress = new CellAddress(3, 3);
        var values = new Dictionary<CellAddress, CellValue>
        {
            [selectorAddress] = CellValue.FromNumber(1d),
        };
        var formulas = new Dictionary<CellAddress, string>
        {
            [formulaAddress] = "=SUM(B1:B2)",
            [selfAddress] = "=FORMULATEXT(D4)",
        };
        var context = new IntrospectionContext(
            values,
            formulas,
            currentAddress: selfAddress);
        var engine = new NeraFormulaEngine();

        var range = engine.Evaluate("=FORMULATEXT(A1:B2)", context);
        Assert.AreEqual("=SUM(B1:B2)", GetText(range));
        Assert.AreEqual(
            new FormulaDependency(
                null,
                new CellRange(formulaAddress, formulaAddress)),
            range.Dependencies.Single());

        var selected = engine.Evaluate(
            "=FORMULATEXT(CHOOSE(C1,A1,B1))",
            context);
        Assert.AreEqual("=SUM(B1:B2)", GetText(selected));
        CollectionAssert.AreEqual(
            new[]
            {
                new FormulaDependency(
                    null,
                    new CellRange(selectorAddress, selectorAddress)),
                new FormulaDependency(
                    null,
                    new CellRange(formulaAddress, formulaAddress)),
            },
            selected.Dependencies.ToArray());

        Assert.AreEqual(
            "=FORMULATEXT(D4)",
            EvaluateText(engine, "=FORMULATEXT(D4)", context));
        AssertScalarError(engine, "=FORMULATEXT(B1)", context, "#N/A");

        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetFormula(new CellAddress(0, 0), "=1+2");
        worksheet.SetFormula(new CellAddress(0, 1), "=FORMULATEXT(A1)");
        worksheet.SetFormula(new CellAddress(2, 4), "=COLUMN()");
        new WorkbookCalculationEngine().Recalculate(workbook);

        Assert.AreEqual(
            "=1+2",
            worksheet.GetCell(new CellAddress(0, 1)).Value.RawValue);
        Assert.AreEqual(
            5d,
            GetCellNumber(worksheet, new CellAddress(2, 4)),
            1e-12d);
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

    private static double EvaluateNumber(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context) =>
        GetNumber(engine.Evaluate(formula, context));

    private static string EvaluateText(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context) =>
        GetText(engine.Evaluate(formula, context));

    private static double GetNumber(FormulaEvaluationResult result)
    {
        Assert.IsTrue(result.IsSuccess, $"Unexpected result: {result.Value}.");
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind);
        return (double)result.Value.RawValue!;
    }

    private static string GetText(FormulaEvaluationResult result)
    {
        Assert.IsTrue(result.IsSuccess, $"Unexpected result: {result.Value}.");
        Assert.AreEqual(CellValueKind.Text, result.Value.Kind);
        return (string)result.Value.RawValue!;
    }

    private static double GetCellNumber(
        Worksheet worksheet,
        CellAddress address)
    {
        var value = worksheet.GetCell(address).Value;
        Assert.AreEqual(CellValueKind.Number, value.Kind);
        return (double)value.RawValue!;
    }

    private static void AssertScalarError(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context,
        string expectedError)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(expectedError, result.Value.RawValue, formula);
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
                $"Unexpected array value at flat index {index}.");
        }
    }

    private sealed class IntrospectionContext :
        IFormulaReferenceIntrospectionContext
    {
        private readonly IReadOnlyDictionary<CellAddress, CellValue> _values;
        private readonly IReadOnlyDictionary<CellAddress, string> _formulas;

        public IntrospectionContext(
            IReadOnlyDictionary<CellAddress, CellValue>? values = null,
            IReadOnlyDictionary<CellAddress, string>? formulas = null,
            string currentWorksheetName = "Sheet1",
            CellAddress? currentAddress = null)
        {
            _values = values ??
                new Dictionary<CellAddress, CellValue>();
            _formulas = formulas ??
                new Dictionary<CellAddress, string>();
            CurrentWorksheetName = currentWorksheetName;
            CurrentCellAddress = currentAddress ?? new CellAddress(0, 0);
        }

        public string CurrentWorksheetName { get; }

        public CellAddress CurrentCellAddress { get; }

        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address) =>
            _values.GetValueOrDefault(address, CellValue.Blank);

        public bool TryGetCellFormula(
            string? worksheetName,
            CellAddress address,
            out string? formula)
        {
            if (_formulas.TryGetValue(address, out var stored))
            {
                formula = stored;
                return true;
            }

            formula = null;
            return true;
        }
    }
}
