using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class AdvancedStatisticalRegressionTests
{
    private static readonly string[] RegressionFunctionNames =
    [
        "COVARIANCE.P",
        "COVARIANCE.S",
        "CORREL",
        "PEARSON",
        "SLOPE",
        "INTERCEPT",
        "RSQ",
        "STEYX",
        "FORECAST.LINEAR",
    ];

    [TestMethod]
    public void CovarianceCorrelationAndRegressionUseStablePairedMoments()
    {
        var values = CreatePairedColumns(
            [1d, 2d, 3d, 4d, 5d],
            [2d, 4d, 5d, 4d, 5d]);
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            1.2d,
            EvaluateNumber(
                engine,
                "=COVARIANCE.P(A1:A5,B1:B5)",
                context),
            1e-12d);
        Assert.AreEqual(
            1.5d,
            EvaluateNumber(
                engine,
                "=COVARIANCE.S(A1:A5,B1:B5)",
                context),
            1e-12d);
        Assert.AreEqual(
            Math.Sqrt(0.6d),
            EvaluateNumber(engine, "=CORREL(A1:A5,B1:B5)", context),
            1e-12d);
        Assert.AreEqual(
            Math.Sqrt(0.6d),
            EvaluateNumber(engine, "=PEARSON(A1:A5,B1:B5)", context),
            1e-12d);
        Assert.AreEqual(
            0.6d,
            EvaluateNumber(engine, "=SLOPE(B1:B5,A1:A5)", context),
            1e-12d);
        Assert.AreEqual(
            2.2d,
            EvaluateNumber(
                engine,
                "=INTERCEPT(B1:B5,A1:A5)",
                context),
            1e-12d);
        Assert.AreEqual(
            0.6d,
            EvaluateNumber(engine, "=RSQ(B1:B5,A1:A5)", context),
            1e-12d);
        Assert.AreEqual(
            Math.Sqrt(0.8d),
            EvaluateNumber(engine, "=STEYX(B1:B5,A1:A5)", context),
            1e-12d);
        Assert.AreEqual(
            5.8d,
            EvaluateNumber(
                engine,
                "=FORECAST.LINEAR(6,B1:B5,A1:A5)",
                context),
            1e-12d);
    }

    [TestMethod]
    public void PairwiseRangesIgnoreNonNumericPairsButRequireEqualShape()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(1d),
            [new CellAddress(1, 0)] = CellValue.FromText("ignored"),
            [new CellAddress(2, 0)] = CellValue.FromNumber(3d),
            [new CellAddress(3, 0)] = CellValue.FromBoolean(true),
            [new CellAddress(0, 1)] = CellValue.FromNumber(2d),
            [new CellAddress(1, 1)] = CellValue.FromNumber(100d),
            [new CellAddress(2, 1)] = CellValue.FromNumber(6d),
            [new CellAddress(3, 1)] = CellValue.FromNumber(200d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            2d,
            EvaluateNumber(
                engine,
                "=COVARIANCE.P(A1:A4,B1:B4)",
                context),
            1e-12d);
        Assert.AreEqual(
            1d,
            EvaluateNumber(engine, "=CORREL(A1:A4,B1:B4)", context),
            1e-12d);

        var mismatch = engine.Evaluate(
            "=CORREL(A1:A3,B1:B4)",
            context);
        Assert.AreEqual(FormulaErrorCode.NotAvailable, mismatch.ErrorCode);
        Assert.AreEqual("#N/A", mismatch.Value.RawValue);
    }

    [TestMethod]
    public void DegenerateRegressionAndInsufficientSamplesAreExplicit()
    {
        var values = CreatePairedColumns(
            [2d, 2d, 2d],
            [1d, 2d, 3d]);
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        Assert.AreEqual(
            FormulaErrorCode.DivisionByZero,
            engine.Evaluate("=CORREL(A1:A3,B1:B3)", context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.DivisionByZero,
            engine.Evaluate("=SLOPE(B1:B3,A1:A3)", context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.DivisionByZero,
            engine.Evaluate("=INTERCEPT(B1:B3,A1:A3)", context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.DivisionByZero,
            engine.Evaluate("=STEYX(B1:B2,A1:A2)", context).ErrorCode);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=COVARIANCE.P(A1:A1,B1:B1)",
                context));
        Assert.AreEqual(
            FormulaErrorCode.DivisionByZero,
            engine.Evaluate(
                "=COVARIANCE.S(A1:A1,B1:B1)",
                context).ErrorCode);
    }

    [TestMethod]
    public void StatisticalTransformsValidateTheirDomains()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            2d,
            EvaluateNumber(engine, "=STANDARDIZE(14,10,2)", context));
        Assert.AreEqual(
            0.5493061443340548d,
            EvaluateNumber(engine, "=FISHER(0.5)", context),
            1e-14d);
        Assert.AreEqual(
            0.5d,
            EvaluateNumber(
                engine,
                "=FISHERINV(FISHER(0.5))",
                context),
            1e-14d);
        Assert.AreEqual(
            "#NUM!",
            engine.Evaluate("=STANDARDIZE(1,0,0)", context).Value.RawValue);
        Assert.AreEqual(
            "#NUM!",
            engine.Evaluate("=FISHER(1)", context).Value.RawValue);
    }

    [TestMethod]
    public void RegressionDependenciesDriveAffectedRecalculation()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var index = 0; index < 4; index++)
        {
            worksheet.SetValue(
                new CellAddress(index, 0),
                index + 1d);
            worksheet.SetValue(
                new CellAddress(index, 1),
                (2d * (index + 1d)) + 1d);
        }
        var formulaAddress = new CellAddress(0, 3);
        worksheet.SetFormula(
            formulaAddress,
            "=FORECAST.LINEAR(5,B1:B4,A1:A4)");
        var calculation = new WorkbookCalculationEngine();

        calculation.Recalculate(workbook);
        Assert.AreEqual(11d, worksheet.GetValue(formulaAddress));
        var dependencies = calculation.DependencyGraph.GetDependencies(
            new FormulaCellKey(worksheet.Name, formulaAddress));
        Assert.AreEqual(2, dependencies.Count);
        CollectionAssert.AreEquivalent(
            new[]
            {
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(3, 0)),
                new CellRange(
                    new CellAddress(0, 1),
                    new CellAddress(3, 1)),
            },
            dependencies.Select(static dependency => dependency.Range)
                .ToArray());

        worksheet.SetValue(new CellAddress(3, 1), 20d);
        var affected = calculation.RecalculateAffected(
            workbook,
            worksheet,
            new CellRange(
                new CellAddress(3, 1),
                new CellAddress(3, 1)));
        Assert.AreEqual(1, affected.FormulaCellCount);
        Assert.AreNotEqual(11d, worksheet.GetValue(formulaAddress));
    }

    [TestMethod]
    public void RegressionDescriptorsUseVersionedRangeContracts()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        foreach (var name in RegressionFunctionNames)
        {
            var descriptor = registry.Descriptors.Single(candidate =>
                candidate.Identity.Name == name);
            Assert.AreEqual("NERA.BUILTIN", descriptor.Identity.Namespace);
            Assert.AreEqual(
                new FormulaFunctionVersion(1, 0, 0),
                descriptor.Version);
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

    private static Dictionary<CellAddress, CellValue> CreatePairedColumns(
        IReadOnlyList<double> xValues,
        IReadOnlyList<double> yValues)
    {
        Assert.AreEqual(xValues.Count, yValues.Count);
        var result = new Dictionary<CellAddress, CellValue>();
        for (var index = 0; index < xValues.Count; index++)
        {
            result[new CellAddress(index, 0)] =
                CellValue.FromNumber(xValues[index]);
            result[new CellAddress(index, 1)] =
                CellValue.FromNumber(yValues[index]);
        }
        return result;
    }

    private static double EvaluateNumber(
        NeraFormulaEngine engine,
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
