using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class StatisticalFormulaFunctionTests
{
    private static readonly string[] StatisticalFunctionNames =
    [
        "MEDIAN",
        "MODE.SNGL",
        "PERCENTILE.INC",
        "QUARTILE.INC",
        "VAR.P",
        "VAR.S",
        "STDEV.P",
        "STDEV.S",
        "RANK.EQ",
        "LARGE",
        "SMALL",
    ];

    [TestMethod]
    public void MedianSupportsOddEvenAndLogicalArgumentBoundaries()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(4d),
            [new CellAddress(2, 0)] = CellValue.FromNumber(2d),
            [new CellAddress(3, 0)] = CellValue.FromNumber(3d),
            [new CellAddress(0, 1)] = CellValue.FromBoolean(false),
            [new CellAddress(1, 1)] = CellValue.FromText("99"),
            [new CellAddress(2, 1)] = CellValue.FromNumber(3d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            2d,
            EvaluateNumber(engine, "=MEDIAN(A1:A3)", context));
        Assert.AreEqual(
            2.5d,
            EvaluateNumber(engine, "=MEDIAN(A1:A4)", context));
        Assert.AreEqual(
            2d,
            EvaluateNumber(
                engine,
                "=MEDIAN(TRUE(),B1:B3)",
                context));
        Assert.AreEqual(
            3d,
            EvaluateNumber(engine, "=MEDIAN(\"2\",4)", context));
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=MEDIAN(\"not-a-number\",1)",
                context).ErrorCode);
    }

    [TestMethod]
    public void ModeSingleReturnsLowestModeAndNotAvailableWithoutDuplicates()
    {
        var values = CreateColumn(2d, 3d, 2d, 3d, 4d, 5d);
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            2d,
            EvaluateNumber(engine, "=MODE.SNGL(A1:A6)", context));

        var noMode = engine.Evaluate(
            "=MODE.SNGL(A4:A6)",
            context);
        Assert.AreEqual(FormulaErrorCode.NotAvailable, noMode.ErrorCode);
        Assert.AreEqual("#N/A", noMode.Value.RawValue);
    }

    [TestMethod]
    public void InclusivePercentileAndQuartileUseLinearInterpolation()
    {
        var values = CreateColumn(0d, 10d, 20d, 30d);
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=PERCENTILE.INC(A1:A4,0)",
                context));
        Assert.AreEqual(
            7.5d,
            EvaluateNumber(
                engine,
                "=PERCENTILE.INC(A1:A4,0.25)",
                context),
            0.0000001d);
        Assert.AreEqual(
            30d,
            EvaluateNumber(
                engine,
                "=PERCENTILE.INC(A1:A4,1)",
                context));
        Assert.AreEqual(
            7.5d,
            EvaluateNumber(
                engine,
                "=QUARTILE.INC(A1:A4,1)",
                context),
            0.0000001d);
        Assert.AreEqual(
            15d,
            EvaluateNumber(
                engine,
                "=QUARTILE.INC(A1:A4,2)",
                context),
            0.0000001d);
    }

    [TestMethod]
    public void PopulationAndSampleVarianceUseStableTwoPassResults()
    {
        var values = CreateColumn(2d, 4d, 4d, 4d, 5d, 5d, 7d, 9d);
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            4d,
            EvaluateNumber(engine, "=VAR.P(A1:A8)", context),
            0.0000001d);
        Assert.AreEqual(
            32d / 7d,
            EvaluateNumber(engine, "=VAR.S(A1:A8)", context),
            0.0000001d);
        Assert.AreEqual(
            2d,
            EvaluateNumber(engine, "=STDEV.P(A1:A8)", context),
            0.0000001d);
        Assert.AreEqual(
            Math.Sqrt(32d / 7d),
            EvaluateNumber(engine, "=STDEV.S(A1:A8)", context),
            0.0000001d);
    }

    [TestMethod]
    public void RankLargeAndSmallRespectOrderAndDuplicateRanks()
    {
        var values = CreateColumn(10d, 20d, 20d, 30d);
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            2d,
            EvaluateNumber(engine, "=RANK.EQ(20,A1:A4)", context));
        Assert.AreEqual(
            2d,
            EvaluateNumber(engine, "=RANK.EQ(20,A1:A4,1)", context));
        Assert.AreEqual(
            2d,
            EvaluateNumber(engine, "=RANK.EQ(25,A1:A4,0)", context));
        Assert.AreEqual(
            20d,
            EvaluateNumber(engine, "=LARGE(A1:A4,2)", context));
        Assert.AreEqual(
            20d,
            EvaluateNumber(engine, "=SMALL(A1:A4,3)", context));
    }

    [TestMethod]
    public void StatisticalErrorsAndInsufficientSamplesAreExplicit()
    {
        var values = CreateColumn(1d, 2d, 3d);
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        var propagated = engine.Evaluate(
            "=MEDIAN(NA(),1)",
            context);
        Assert.AreEqual(FormulaErrorCode.NotAvailable, propagated.ErrorCode);

        Assert.AreEqual(
            "#NUM!",
            engine.Evaluate(
                "=PERCENTILE.INC(A1:A3,1.1)",
                context).Value.RawValue);
        Assert.AreEqual(
            "#NUM!",
            engine.Evaluate(
                "=QUARTILE.INC(A1:A3,5)",
                context).Value.RawValue);
        Assert.AreEqual(
            FormulaErrorCode.DivisionByZero,
            engine.Evaluate("=VAR.S(A1:A1)", context).ErrorCode);
        Assert.AreEqual(
            "#NUM!",
            engine.Evaluate("=LARGE(A1:A3,0)", context).Value.RawValue);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=PERCENTILE.INC(A1:A3,A1:A1)",
                context).ErrorCode);
    }

    [TestMethod]
    public void StatisticalRangesAreCapturedForAffectedRecalculation()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), 1d);
        worksheet.SetValue(new CellAddress(1, 0), 3d);
        worksheet.SetValue(new CellAddress(2, 0), 5d);
        var formulaAddress = new CellAddress(0, 2);
        worksheet.SetFormula(formulaAddress, "=MEDIAN(A1:A3)");
        var calculation = new WorkbookCalculationEngine();

        calculation.Recalculate(workbook);
        Assert.AreEqual(3d, worksheet.GetValue(formulaAddress));
        var dependencies = calculation.DependencyGraph.GetDependencies(
            new FormulaCellKey(worksheet.Name, formulaAddress));
        Assert.AreEqual(1, dependencies.Count);
        Assert.AreEqual(
            new CellRange(
                new CellAddress(0, 0),
                new CellAddress(2, 0)),
            dependencies[0].Range);

        worksheet.SetValue(new CellAddress(2, 0), 9d);
        calculation.RecalculateAffected(
            workbook,
            worksheet,
            new CellRange(
                new CellAddress(2, 0),
                new CellAddress(2, 0)));
        Assert.AreEqual(3d, worksheet.GetValue(formulaAddress));

        worksheet.SetValue(new CellAddress(1, 0), 7d);
        calculation.RecalculateAffected(
            workbook,
            worksheet,
            new CellRange(
                new CellAddress(1, 0),
                new CellAddress(1, 0)));
        Assert.AreEqual(7d, worksheet.GetValue(formulaAddress));
    }

    [TestMethod]
    public void StatisticalDescriptorsUseVersionedLogicalArgumentPolicy()
    {
        var registry = new BuiltInFormulaFunctionRegistry();
        var descriptors = registry.SnapshotDescriptors();

        foreach (var name in StatisticalFunctionNames)
        {
            var descriptor = descriptors.Single(candidate =>
                string.Equals(
                    candidate.Identity.Name,
                    name,
                    StringComparison.Ordinal));
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
            Assert.IsTrue(descriptor.Capabilities.HasFlag(
                FormulaFunctionCapabilities.ScalarArguments));
            Assert.IsTrue(descriptor.Capabilities.HasFlag(
                FormulaFunctionCapabilities.RangeArguments));
            Assert.IsTrue(descriptor.Capabilities.HasFlag(
                FormulaFunctionCapabilities.ReturnsScalar));
            Assert.AreEqual(
                FormulaFunctionVolatility.Deterministic,
                descriptor.Volatility);
            Assert.AreEqual(
                FormulaFunctionSecurityClassification.Pure,
                descriptor.SecurityClassification);
        }
    }

    private static Dictionary<CellAddress, CellValue> CreateColumn(
        params double[] values) =>
        values
            .Select((value, index) => new KeyValuePair<CellAddress, CellValue>(
                new CellAddress(index, 0),
                CellValue.FromNumber(value)))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value);

    private static double EvaluateNumber(
        IFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(
            result.IsSuccess,
            $"Expected success for {formula}, but received {result.Value}.");
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind);
        return (double)result.Value.RawValue!;
    }
}
