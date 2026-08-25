using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class AdvancedReferenceAndAggregationFormulaFunctionTests
{
    [TestMethod]
    public void GetPivotDataUsesProviderAndProviderDependencies()
    {
        var context = new F010TestContext();
        var engine = new NeraFormulaEngine();

        var result = engine.Evaluate(
            "=GETPIVOTDATA(\"Sales\",A1,\"Region\",\"East\")",
            context);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(42d, GetNumber(result.Value), 1e-12d);
        Assert.AreEqual("Sales", context.LastPivotDataField);
        Assert.AreEqual(1, context.LastPivotItems.Length);
        Assert.AreEqual("Region", context.LastPivotItems[0].FieldName);
        Assert.AreEqual("East", context.LastPivotItems[0].Item.RawValue);
        CollectionAssert.Contains(
            result.Dependencies.ToArray(),
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 3),
                    new CellAddress(3, 3))));
    }

    [TestMethod]
    public void GroupByAggregatesFiltersSortsAndAddsGrandTotal()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("Category"),
            [new CellAddress(0, 1)] = CellValue.FromText("Sales"),
            [new CellAddress(0, 2)] = CellValue.FromBoolean(true),
            [new CellAddress(1, 0)] = CellValue.FromText("B"),
            [new CellAddress(1, 1)] = CellValue.FromNumber(2d),
            [new CellAddress(1, 2)] = CellValue.FromBoolean(true),
            [new CellAddress(2, 0)] = CellValue.FromText("A"),
            [new CellAddress(2, 1)] = CellValue.FromNumber(3d),
            [new CellAddress(2, 2)] = CellValue.FromBoolean(true),
            [new CellAddress(3, 0)] = CellValue.FromText("B"),
            [new CellAddress(3, 1)] = CellValue.FromNumber(5d),
            [new CellAddress(3, 2)] = CellValue.FromBoolean(false),
        };
        var context = new F010TestContext(values);
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=GROUPBY(A1:A4,B1:B4,SUM,3,1,2,C1:C4,1)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(4, result.Value!.RowCount);
        Assert.AreEqual(2, result.Value.ColumnCount);
        Assert.AreEqual("Category", result.Value[0, 0].RawValue);
        Assert.AreEqual("Sales", result.Value[0, 1].RawValue);
        Assert.AreEqual("B", result.Value[1, 0].RawValue);
        Assert.AreEqual(2d, GetNumber(result.Value[1, 1]), 1e-12d);
        Assert.AreEqual("A", result.Value[2, 0].RawValue);
        Assert.AreEqual(3d, GetNumber(result.Value[2, 1]), 1e-12d);
        Assert.AreEqual("Grand Total", result.Value[3, 0].RawValue);
        Assert.AreEqual(5d, GetNumber(result.Value[3, 1]), 1e-12d);
    }

    [TestMethod]
    public void HStackPadsShortArraysWithNotAvailable()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(2d),
            [new CellAddress(0, 1)] = CellValue.FromNumber(10d),
            [new CellAddress(0, 2)] = CellValue.FromNumber(11d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(12d),
            [new CellAddress(1, 2)] = CellValue.FromNumber(13d),
            [new CellAddress(2, 1)] = CellValue.FromNumber(14d),
            [new CellAddress(2, 2)] = CellValue.FromNumber(15d),
        };
        var context = new F010TestContext(values);
        var engine = new NeraDynamicArrayFormulaEngine();

        Assert.IsTrue(engine.TryEvaluate(
            "=HSTACK(A1:A2,B1:C3)",
            context,
            out var result));
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(3, result.Value!.RowCount);
        Assert.AreEqual(3, result.Value.ColumnCount);
        Assert.AreEqual("#N/A", result.Value[2, 0].RawValue);
        Assert.AreEqual(14d, GetNumber(result.Value[2, 1]), 1e-12d);
        Assert.AreEqual(15d, GetNumber(result.Value[2, 2]), 1e-12d);
    }

    [TestMethod]
    public void HyperlinkReturnsFriendlyValueAndPublishesMetadata()
    {
        var context = new F010TestContext();
        var engine = new NeraFormulaEngine();

        var result = engine.Evaluate(
            "=HYPERLINK(\"https://example.com\",\"Open\")",
            context);

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(CellValueKind.Text, result.Value.Kind);
        Assert.AreEqual("Open", result.Value.RawValue);
        Assert.IsNotNull(context.LastHyperlink);
        Assert.AreEqual(
            "https://example.com",
            context.LastHyperlink.Value.LinkLocation);
        Assert.AreEqual(
            "Open",
            context.LastHyperlink.Value.DisplayValue.RawValue);

        var registry = new BuiltInFormulaFunctionRegistry();
        Assert.AreEqual(BuiltInFormulaTestCounts.EagerVersioned, registry.Count);
        Assert.IsTrue(registry.TryGetDescriptor(
            "HYPERLINK",
            out var descriptor));
        Assert.AreEqual(
            FormulaFunctionSecurityClassification.ContextReadOnly,
            descriptor.SecurityClassification);
    }

    [TestMethod]
    public void IndirectSupportsRangeIdentityDynamicSpillAndRelativeR1C1()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromText("B1:B2"),
            [new CellAddress(0, 1)] = CellValue.FromNumber(4d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(6d),
            [new CellAddress(2, 1)] = CellValue.FromNumber(9d),
        };
        var context = new F010TestContext(
            values,
            new CellAddress(3, 3));
        var scalarEngine = new NeraFormulaEngine();

        var sum = scalarEngine.Evaluate("=SUM(INDIRECT(A1))", context);
        Assert.IsTrue(sum.IsSuccess);
        Assert.AreEqual(10d, GetNumber(sum.Value), 1e-12d);
        CollectionAssert.Contains(
            sum.Dependencies.ToArray(),
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 1),
                    new CellAddress(1, 1))));

        var relative = scalarEngine.Evaluate(
            "=INDIRECT(\"R[-1]C[-2]\",FALSE)",
            context);
        Assert.IsTrue(relative.IsSuccess);
        Assert.AreEqual(9d, GetNumber(relative.Value), 1e-12d);

        var arrayEngine = new NeraDynamicArrayFormulaEngine(scalarEngine);
        Assert.IsTrue(arrayEngine.TryEvaluate(
            "=INDIRECT(\"B1:B2\")",
            context,
            out var array));
        Assert.IsTrue(array.IsSuccess);
        Assert.AreEqual(2, array.Value!.RowCount);
        Assert.AreEqual(1, array.Value.ColumnCount);
        Assert.AreEqual(4d, GetNumber(array.Value[0, 0]), 1e-12d);
        Assert.AreEqual(6d, GetNumber(array.Value[1, 0]), 1e-12d);
    }

    private static double GetNumber(CellValue value)
    {
        Assert.AreEqual(CellValueKind.Number, value.Kind);
        return (double)value.RawValue!;
    }

    private sealed class F010TestContext :
        IFormulaReferenceIntrospectionContext,
        IFormulaPivotDataEvaluationContext,
        IFormulaHyperlinkEvaluationContext
    {
        private readonly IReadOnlyDictionary<CellAddress, CellValue> _values;

        public F010TestContext(
            IReadOnlyDictionary<CellAddress, CellValue>? values = null,
            CellAddress? currentAddress = null)
        {
            _values = values ??
                new Dictionary<CellAddress, CellValue>();
            CurrentCellAddress = currentAddress ?? new CellAddress(0, 0);
        }

        public string CurrentWorksheetName => "Sheet1";

        public CellAddress CurrentCellAddress { get; }

        public FormulaHyperlink? LastHyperlink { get; private set; }

        public string? LastPivotDataField { get; private set; }

        public FormulaPivotFieldItem[] LastPivotItems
        {
            get;
            private set;
        } = Array.Empty<FormulaPivotFieldItem>();

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
            return false;
        }

        public bool TryGetPivotData(
            string? worksheetName,
            CellRange pivotTableReference,
            string dataField,
            IReadOnlyList<FormulaPivotFieldItem> fieldItems,
            out CellValue value,
            out IReadOnlyList<FormulaDependency> dependencies)
        {
            LastPivotDataField = dataField;
            LastPivotItems = fieldItems.ToArray();
            value = CellValue.FromNumber(42d);
            dependencies =
            [
                new FormulaDependency(
                    worksheetName,
                    new CellRange(
                        new CellAddress(0, 3),
                        new CellAddress(3, 3))),
            ];
            return pivotTableReference.Contains(new CellAddress(0, 0));
        }

        public void SetCurrentFormulaHyperlink(FormulaHyperlink hyperlink)
        {
            LastHyperlink = hyperlink;
        }
    }
}
