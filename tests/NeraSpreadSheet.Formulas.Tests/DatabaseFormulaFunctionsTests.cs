using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class DatabaseFormulaFunctionsTests
{
    private static readonly string[] FunctionNames =
    [
        "DSUM",
        "DCOUNT",
        "DCOUNTA",
        "DAVERAGE",
        "DMAX",
        "DMIN",
        "DPRODUCT",
        "DGET",
        "DSTDEV",
        "DSTDEVP",
        "DVAR",
        "DVARP",
    ];

    [TestMethod]
    public void DatabaseDescriptorsUseLogicalRangeAwareSdkContracts()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        foreach (var name in FunctionNames)
        {
            Assert.IsTrue(
                registry.TryGetDescriptor(name, out var descriptor),
                $"Missing descriptor for {name}.");
            Assert.AreEqual("NERA.BUILTIN", descriptor.Identity.Namespace);
            Assert.AreEqual(new FormulaFunctionVersion(1, 0, 0), descriptor.Version);
            Assert.AreEqual(3, descriptor.MinimumArguments);
            Assert.AreEqual(3, descriptor.MaximumArguments);
            Assert.AreEqual(
                FormulaFunctionArgumentCountPolicy.LogicalArguments,
                descriptor.ArgumentCountPolicy);
            Assert.IsFalse(descriptor.PropagateArgumentErrors);
            Assert.IsTrue((descriptor.Capabilities &
                FormulaFunctionCapabilities.ScalarArguments) != 0);
            Assert.IsTrue((descriptor.Capabilities &
                FormulaFunctionCapabilities.RangeArguments) != 0);
            Assert.IsTrue((descriptor.Capabilities &
                FormulaFunctionCapabilities.ReturnsScalar) != 0);
        }
    }

    [TestMethod]
    public void DatabaseAggregatesApplyAndWithinRowsOrAcrossRows()
    {
        var engine = new NeraFormulaEngine();
        var context = CreateDatabaseContext();

        AssertNumber(engine, context, "=DSUM(A1:E6,\"Profit\",G1:H3)", 256d);
        AssertNumber(engine, context, "=DSUM(A1:E6,5,G1:H3)", 256d);
        AssertNumber(engine, context, "=DCOUNT(A1:E6,\"Yield\",G1:H3)", 3d);
        AssertNumber(engine, context, "=DCOUNTA(A1:E6,\"Tree\",G1:H3)", 3d);
        AssertNumber(engine, context, "=DAVERAGE(A1:E6,\"Height\",G1:H3)", 41d / 3d);
        AssertNumber(engine, context, "=DMAX(A1:E6,\"Age\",G1:H3)", 20d);
        AssertNumber(engine, context, "=DMIN(A1:E6,\"Age\",G1:H3)", 8d);
        AssertNumber(engine, context, "=DPRODUCT(A1:E6,\"Yield\",G1:H3)", 1120d);
    }

    [TestMethod]
    public void DatabaseVarianceFunctionsUseStableSampleAndPopulationContracts()
    {
        var engine = new NeraFormulaEngine();
        var context = CreateDatabaseContext();
        var values = new[] { 105d, 75d, 76d };
        var populationVariance = Variance(values, sample: false);
        var sampleVariance = Variance(values, sample: true);

        AssertNumber(engine, context, "=DVARP(A1:E6,\"Profit\",G1:H3)", populationVariance);
        AssertNumber(engine, context, "=DVAR(A1:E6,\"Profit\",G1:H3)", sampleVariance);
        AssertNumber(engine, context, "=DSTDEVP(A1:E6,\"Profit\",G1:H3)", Math.Sqrt(populationVariance));
        AssertNumber(engine, context, "=DSTDEV(A1:E6,\"Profit\",G1:H3)", Math.Sqrt(sampleVariance));
    }

    [TestMethod]
    public void DGetRequiresExactlyOneMatchingRecord()
    {
        var engine = new NeraFormulaEngine();
        var values = CreateDatabaseValues();
        values[new CellAddress(0, 9)] = CellValue.FromText("Tree");
        values[new CellAddress(1, 9)] = CellValue.FromText("Cherry");
        values[new CellAddress(0, 10)] = CellValue.FromText("Tree");
        values[new CellAddress(1, 10)] = CellValue.FromText("Orange");
        values[new CellAddress(0, 11)] = CellValue.FromText("Tree");
        values[new CellAddress(1, 11)] = CellValue.FromText("Apple");
        var context = new FormulaSurfaceTestContext(values);

        AssertNumber(engine, context, "=DGET(A1:E6,\"Profit\",J1:J2)", 105d);
        AssertError(engine, context, "=DGET(A1:E6,\"Profit\",K1:K2)", "#VALUE!");
        AssertError(engine, context, "=DGET(A1:E6,\"Profit\",L1:L2)", "#NUM!");
    }

    [TestMethod]
    public void CriteriaSupportWildcardsEscapesBlankRowsAndDuplicateHeaders()
    {
        var engine = new NeraFormulaEngine();
        var values = CreateDatabaseValues();
        values[new CellAddress(0, 9)] = CellValue.FromText("Tree");
        values[new CellAddress(1, 9)] = CellValue.FromText("A*");
        values[new CellAddress(0, 10)] = CellValue.FromText("Tree");
        values[new CellAddress(1, 10)] = CellValue.Blank;
        values[new CellAddress(0, 11)] = CellValue.FromText("Tree");
        values[new CellAddress(0, 12)] = CellValue.FromText("Tree");
        values[new CellAddress(1, 11)] = CellValue.FromText("A*");
        values[new CellAddress(1, 12)] = CellValue.FromText("<>Apple");

        values[new CellAddress(7, 0)] = CellValue.FromText("Name");
        values[new CellAddress(7, 1)] = CellValue.FromText("Value");
        values[new CellAddress(8, 0)] = CellValue.FromText("*");
        values[new CellAddress(8, 1)] = CellValue.FromNumber(10d);
        values[new CellAddress(9, 0)] = CellValue.FromText("Ax");
        values[new CellAddress(9, 1)] = CellValue.FromNumber(20d);
        values[new CellAddress(7, 3)] = CellValue.FromText("Name");
        values[new CellAddress(8, 3)] = CellValue.FromText("~*");
        var context = new FormulaSurfaceTestContext(values);

        AssertNumber(engine, context, "=DSUM(A1:E6,\"Profit\",J1:J2)", 180d);
        AssertNumber(engine, context, "=DSUM(A1:E6,\"Profit\",K1:K2)", 457d);
        AssertNumber(engine, context, "=DSUM(A1:E6,\"Profit\",L1:M2)", 0d);
        AssertNumber(engine, context, "=DSUM(A8:B10,\"Value\",D8:D9)", 10d);
    }

    [TestMethod]
    public void DatabaseFunctionsCaptureDatabaseFieldAndCriteriaDependencies()
    {
        var values = CreateDatabaseValues();
        values[new CellAddress(0, 9)] = CellValue.FromText("Profit");
        var engine = new NeraFormulaEngine();

        var result = engine.Evaluate(
            "=DSUM(A1:E6,J1,G1:H3)",
            new FormulaSurfaceTestContext(values));

        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(256d, result.Value.RawValue);
        Assert.IsTrue(result.Dependencies.Any(dependency =>
            dependency.Range == new CellRange(
                new CellAddress(0, 0),
                new CellAddress(5, 4))));
        Assert.IsTrue(result.Dependencies.Any(dependency =>
            dependency.Range == new CellRange(
                new CellAddress(0, 9),
                new CellAddress(0, 9))));
        Assert.IsTrue(result.Dependencies.Any(dependency =>
            dependency.Range == new CellRange(
                new CellAddress(0, 6),
                new CellAddress(2, 7))));
    }

    [TestMethod]
    public void AffectedRecalculationRespondsToDatabaseAndCriteriaChanges()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        foreach (var pair in CreateDatabaseValues())
        {
            worksheet.SetCell(pair.Key, new CellData(pair.Value));
        }
        var formulaAddress = new CellAddress(0, 11);
        worksheet.SetFormula(
            formulaAddress,
            "=DSUM(A1:E6,\"Profit\",G1:H3)");
        var calculation = new WorkbookCalculationEngine();
        calculation.Recalculate(workbook);
        Assert.AreEqual(256d, worksheet.GetValue(formulaAddress));

        var changedProfit = new CellAddress(1, 4);
        worksheet.SetValue(changedProfit, 205d);
        calculation.RecalculateAffected(
            workbook,
            worksheet,
            new CellRange(changedProfit, changedProfit));
        Assert.AreEqual(356d, worksheet.GetValue(formulaAddress));

        var changedCriterion = new CellAddress(1, 7);
        worksheet.SetValue(changedCriterion, ">15");
        calculation.RecalculateAffected(
            workbook,
            worksheet,
            new CellRange(changedCriterion, changedCriterion));
        Assert.AreEqual(281d, worksheet.GetValue(formulaAddress));
    }

    [TestMethod]
    public void InvalidShapesHeadersFieldsAndBudgetsFailClosed()
    {
        var engine = new NeraFormulaEngine();
        var values = CreateDatabaseValues();
        values[new CellAddress(0, 9)] = CellValue.FromText("Unknown");
        var context = new FormulaSurfaceTestContext(values);

        AssertError(engine, context, "=DSUM(A1:E6,\"Missing\",G1:H3)", "#VALUE!");
        AssertError(engine, context, "=DSUM(A1:E6,\"Profit\",J1:J1)", "#VALUE!");
        AssertError(engine, context, "=DSUM(A1:E6,\"Profit\",J1:J2)", "#VALUE!");

        var registry = new BuiltInFormulaFunctionRegistry();
        Assert.IsTrue(registry.TryResolve("DSUM", out var function));
        var versioned = (IVersionedFormulaFunction)function;
        var excessiveDatabase = FormulaFunctionArgument.Range(
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(
                        SpreadsheetLimits.MaxRows - 1,
                        1))),
            [CellValue.Blank]);
        var criteria = FormulaFunctionArgument.Range(
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(1, 0))),
            [CellValue.FromText("A"), CellValue.Blank]);
        var budgetResult = versioned.Invoke(new FormulaFunctionInvocation(
            [
                excessiveDatabase,
                FormulaFunctionArgument.Scalar(CellValue.FromText("A")),
                criteria,
            ],
            context));
        Assert.AreEqual("#NUM!", budgetResult.Value.RawValue);
    }

    private static FormulaSurfaceTestContext CreateDatabaseContext() =>
        new(CreateDatabaseValues());

    private static Dictionary<CellAddress, CellValue> CreateDatabaseValues()
    {
        var values = new Dictionary<CellAddress, CellValue>();
        SetRow(values, 0, 0, "Tree", "Height", "Age", "Yield", "Profit");
        SetRow(values, 1, 0, "Apple", 18d, 20d, 14d, 105d);
        SetRow(values, 2, 0, "Pear", 12d, 12d, 10d, 96d);
        SetRow(values, 3, 0, "Cherry", 13d, 14d, 9d, 105d);
        SetRow(values, 4, 0, "Apple", 14d, 15d, 10d, 75d);
        SetRow(values, 5, 0, "Pear", 9d, 8d, 8d, 76d);

        SetRow(values, 0, 6, "Tree", "Height");
        SetRow(values, 1, 6, "Apple", ">10");
        SetRow(values, 2, 6, "Pear", "<10");
        return values;
    }

    private static void SetRow(
        IDictionary<CellAddress, CellValue> values,
        int row,
        int startColumn,
        params object[] rowValues)
    {
        for (var index = 0; index < rowValues.Length; index++)
        {
            values[new CellAddress(row, startColumn + index)] =
                CellValue.FromObject(rowValues[index]);
        }
    }

    private static double Variance(IReadOnlyList<double> values, bool sample)
    {
        var mean = values.Average();
        var sum = values.Sum(value => (value - mean) * (value - mean));
        return sum / (sample ? values.Count - 1d : values.Count);
    }

    private static void AssertNumber(
        IFormulaEngine engine,
        IFormulaEvaluationContext context,
        string formula,
        double expected)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(expected, (double)result.Value.RawValue!, 1e-10d, formula);
    }

    private static void AssertError(
        IFormulaEngine engine,
        IFormulaEvaluationContext context,
        string formula,
        string expected)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }
}
