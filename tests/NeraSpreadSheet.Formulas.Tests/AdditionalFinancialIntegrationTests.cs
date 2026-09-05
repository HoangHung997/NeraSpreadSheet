using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class AdditionalFinancialIntegrationTests
{
    private const int MaximumXirrValuesForTest = 100_000;

    [TestMethod]
    public void ScheduledFinancialRangesEnterDependenciesAndRecalculate()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var row = 0;
             row < AdditionalFinancialTestData.ReferenceValues.Length;
             row++)
        {
            worksheet.SetValue(
                new CellAddress(row, 0),
                AdditionalFinancialTestData.ReferenceValues[row]);
            worksheet.SetValue(
                new CellAddress(row, 1),
                AdditionalFinancialTestData.ReferenceDates[row]);
        }

        var xnpvAddress = new CellAddress(0, 3);
        var xirrAddress = new CellAddress(1, 3);
        worksheet.SetFormula(
            xnpvAddress,
            "=XNPV(0.09,A1:A5,B1:B5)");
        worksheet.SetFormula(
            xirrAddress,
            "=XIRR(A1:A5,B1:B5)");
        var calculation = new WorkbookCalculationEngine();

        calculation.Recalculate(workbook);
        var expectedValueRange = new CellRange(
            new CellAddress(0, 0),
            new CellAddress(4, 0));
        var expectedDateRange = new CellRange(
            new CellAddress(0, 1),
            new CellAddress(4, 1));
        var xnpvDependencies = calculation.DependencyGraph
            .GetDependencies(
                new FormulaCellKey(
                    worksheet.Name,
                    xnpvAddress))
            .Select(static dependency => dependency.Range)
            .ToArray();
        var xirrDependencies = calculation.DependencyGraph
            .GetDependencies(
                new FormulaCellKey(
                    worksheet.Name,
                    xirrAddress))
            .Select(static dependency => dependency.Range)
            .ToArray();
        Assert.IsTrue(xnpvDependencies.Contains(
            expectedValueRange));
        Assert.IsTrue(xnpvDependencies.Contains(
            expectedDateRange));
        Assert.IsTrue(xirrDependencies.Contains(
            expectedValueRange));
        Assert.IsTrue(xirrDependencies.Contains(
            expectedDateRange));

        var previousXnpv =
            (double)worksheet.GetValue(xnpvAddress)!;
        var previousXirr =
            (double)worksheet.GetValue(xirrAddress)!;
        worksheet.SetValue(
            new CellAddress(2, 1),
            AdditionalFinancialTestData.ReferenceDates[2].AddDays(30d));
        calculation.RecalculateAffected(
            workbook,
            worksheet,
            new CellRange(
                new CellAddress(2, 1),
                new CellAddress(2, 1)));

        Assert.AreNotEqual(
            previousXnpv,
            (double)worksheet.GetValue(xnpvAddress)!);
        Assert.AreNotEqual(
            previousXirr,
            (double)worksheet.GetValue(xirrAddress)!);
    }

    [TestMethod]
    public void AdditionalFinancialDescriptorsAreVersionedAndBounded()
    {
        var registry = new BuiltInFormulaFunctionRegistry();

        foreach (var name in new[] { "RATE", "XNPV", "XIRR" })
        {
            var descriptor = registry.Descriptors.Single(candidate =>
                candidate.Identity.Name == name);
            Assert.AreEqual(
                "NERA.BUILTIN",
                descriptor.Identity.Namespace);
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
                FormulaFunctionCapabilities.ReturnsScalar));
            Assert.AreEqual(
                name is "XNPV" or "XIRR",
                descriptor.Capabilities.HasFlag(
                    FormulaFunctionCapabilities.RangeArguments));
            Assert.AreEqual(
                FormulaFunctionVolatility.Deterministic,
                descriptor.Volatility);
            Assert.AreEqual(
                FormulaFunctionSecurityClassification.Pure,
                descriptor.SecurityClassification);
        }
    }

    [TestMethod]
    public void XirrRejectsSchedulesBeyondItsValueBudget()
    {
        var registry = new BuiltInFormulaFunctionRegistry();
        Assert.IsTrue(registry.TryResolve(
            "XIRR",
            out var resolved));
        var function = (IVersionedFormulaFunction)resolved;
        var count = MaximumXirrValuesForTest + 1;
        var values = Enumerable.Range(0, count)
            .Select(index => CellValue.FromNumber(
                index == 0 ? -1d : 1d))
            .ToArray();
        var dates = Enumerable.Range(0, count)
            .Select(index => CellValue.FromNumber(
                40000d + index))
            .ToArray();
        var valueArgument = FormulaFunctionArgument.Range(
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(count - 1, 0))),
            values);
        var dateArgument = FormulaFunctionArgument.Range(
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 1),
                    new CellAddress(count - 1, 1))),
            dates);

        var result = function.Invoke(
            new FormulaFunctionInvocation(
                [valueArgument, dateArgument],
                new FormulaSurfaceTestContext()));

        Assert.AreEqual("#NUM!", result.Value.RawValue);
    }
}
