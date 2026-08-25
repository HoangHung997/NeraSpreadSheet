using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class BusinessCalendarAndNumberValueFormulaFunctionTests
{
    private static readonly string[] FunctionNames =
    [
        "NETWORKDAYS",
        "NETWORKDAYS.INTL",
        "WORKDAY",
        "WORKDAY.INTL",
        "NUMBERVALUE",
    ];

    [TestMethod]
    public void NetworkDaysMatchesInclusiveSignedAndWeekendReferences()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] =
                CellValue.FromDateTime(new DateTime(2006, 1, 2)),
            [new CellAddress(1, 0)] =
                CellValue.FromDateTime(new DateTime(2006, 1, 7)),
        };
        var context = new FormulaSurfaceTestContext(values);
        var engine = new NeraFormulaEngine();

        Assert.AreEqual(
            22d,
            EvaluateNumber(
                engine,
                "=NETWORKDAYS(DATE(2006,1,1),DATE(2006,1,31))",
                context),
            1e-12d);
        Assert.AreEqual(
            -21d,
            EvaluateNumber(
                engine,
                "=NETWORKDAYS(DATE(2006,2,28),DATE(2006,1,31))",
                context),
            1e-12d);
        Assert.AreEqual(
            22d,
            EvaluateNumber(
                engine,
                "=NETWORKDAYS.INTL(" +
                "DATE(2006,1,1),DATE(2006,1,31),7,A1:A2)",
                context),
            1e-12d);
        Assert.AreEqual(
            20d,
            EvaluateNumber(
                engine,
                "=NETWORKDAYS.INTL(" +
                "DATE(2006,1,1),DATE(2006,1,31),\"0010001\",A1:A2)",
                context),
            1e-12d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=NETWORKDAYS.INTL(" +
                "DATE(2006,1,1),DATE(2006,1,31),\"1111111\")",
                context),
            1e-12d);
    }

    [TestMethod]
    public void WorkdayMatchesPublishedPositiveNegativeAndInternationalCases()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] =
                CellValue.FromDateTime(new DateTime(2008, 11, 26)),
            [new CellAddress(1, 0)] =
                CellValue.FromDateTime(new DateTime(2008, 12, 4)),
            [new CellAddress(2, 0)] =
                CellValue.FromDateTime(new DateTime(2009, 1, 21)),
        };
        var context = new FormulaSurfaceTestContext(values);
        var engine = new NeraFormulaEngine();

        Assert.AreEqual(
            new DateTime(2009, 4, 30),
            EvaluateDate(
                engine,
                "=WORKDAY(DATE(2008,10,1),151)",
                context));
        Assert.AreEqual(
            new DateTime(2009, 5, 5),
            EvaluateDate(
                engine,
                "=WORKDAY(DATE(2008,10,1),151,A1:A3)",
                context));
        Assert.AreEqual(
            new DateTime(2012, 4, 14),
            EvaluateDate(
                engine,
                "=WORKDAY.INTL(DATE(2012,1,1),90,11)",
                context));
        Assert.AreEqual(
            new DateTime(2012, 2, 5),
            EvaluateDate(
                engine,
                "=WORKDAY.INTL(DATE(2012,1,1),30,17)",
                context));
        Assert.AreEqual(
            new DateTime(2024, 1, 1),
            EvaluateDate(
                engine,
                "=WORKDAY(DATE(2024,1,8),-5)",
                context));
        Assert.AreEqual(
            new DateTime(2024, 1, 6),
            EvaluateDate(
                engine,
                "=WORKDAY(DATE(2024,1,6),0)",
                context));
    }

    [TestMethod]
    public void HolidayRangesDeduplicateIgnoreWeekendsAndRemainDependencies()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] =
                CellValue.FromDateTime(new DateTime(2024, 1, 1)),
            [new CellAddress(1, 0)] =
                CellValue.FromDateTime(new DateTime(2024, 1, 1)),
            [new CellAddress(2, 0)] =
                CellValue.FromDateTime(new DateTime(2024, 1, 6)),
            [new CellAddress(3, 0)] = CellValue.Blank,
        };
        var context = new FormulaSurfaceTestContext(values);
        var engine = new NeraFormulaEngine();

        var result = engine.Evaluate(
            "=NETWORKDAYS(DATE(2024,1,1),DATE(2024,1,7),A1:A4)",
            context);
        Assert.IsTrue(result.IsSuccess);
        Assert.AreEqual(4d, result.Value.RawValue);
        Assert.IsTrue(result.Dependencies.Contains(
            new FormulaDependency(
                null,
                new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(3, 0)))));

        Assert.AreEqual(
            new DateTime(2024, 1, 2),
            EvaluateDate(
                engine,
                "=WORKDAY(DATE(2023,12,29),1,A1:A4)",
                context));
        Assert.AreEqual(
            new DateTime(2024, 1, 2),
            EvaluateDate(
                engine,
                "=WORKDAY(" +
                "DATE(2023,12,29),1,DATE(2024,1,1))",
                context));
    }

    [TestMethod]
    public void NumberValueUsesExplicitAndContextSeparatorsAndPercentSuffixes()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            2500.27d,
            EvaluateNumber(
                engine,
                "=NUMBERVALUE(\"2.500,27\",\",\",\".\")",
                context),
            1e-12d);
        Assert.AreEqual(
            3000d,
            EvaluateNumber(
                engine,
                "=NUMBERVALUE(\" 3 000 \",\",\",\".\")",
                context),
            1e-12d);
        Assert.AreEqual(
            0.035d,
            EvaluateNumber(
                engine,
                "=NUMBERVALUE(\"3,5%\",\",\",\".\")",
                context),
            1e-12d);
        Assert.AreEqual(
            0.0009d,
            EvaluateNumber(
                engine,
                "=NUMBERVALUE(\"9%%\",\",\",\".\")",
                context),
            1e-12d);
        Assert.AreEqual(
            2500.25d,
            EvaluateNumber(
                engine,
                "=NUMBERVALUE(\"2x500y25\",\"yy\",\"xx\")",
                context),
            1e-12d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=NUMBERVALUE(\"\")",
                context),
            1e-12d);

        var localeContext = new LocaleFormulaTestContext(",", ".");
        Assert.AreEqual(
            2500.27d,
            EvaluateNumber(
                engine,
                "=NUMBERVALUE(\"2.500,27\")",
                localeContext),
            1e-12d);
    }

    [TestMethod]
    public void F007DomainsDescriptorsAndRegistryFailClosed()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] =
                CellValue.FromDateTime(new DateTime(2024, 1, 1)),
            [new CellAddress(1, 0)] =
                CellValue.FromDateTime(new DateTime(2024, 1, 2)),
        };
        var context = new FormulaSurfaceTestContext(values);
        var engine = new NeraFormulaEngine();

        foreach (var formula in new[]
        {
            "=NETWORKDAYS.INTL(DATE(2024,1,1),DATE(2024,1,7),0)",
            "=WORKDAY(DATE(2024,1,1),2147483648)",
            "=WORKDAY(1E20,1)",
        })
        {
            AssertNumericError(engine, formula, context);
        }

        foreach (var formula in new[]
        {
            "=NETWORKDAYS.INTL(DATE(2024,1,1),DATE(2024,1,7),\"bad\")",
            "=WORKDAY.INTL(DATE(2024,1,1),1,\"1111111\")",
            "=NETWORKDAYS(DATE(2024,1,1),DATE(2024,1,7),\"bad holiday\")",
            "=NUMBERVALUE(\"1,2,3\",\",\",\".\")",
            "=NUMBERVALUE(\"1.2,3\",\".\",\",\")",
            "=NUMBERVALUE(\"1.23\",\".\",\".\")",
            "=NETWORKDAYS(A1:A2,DATE(2024,1,7))",
            "=WORKDAY(DATE(2024,1,1),A1:A2)",
            "=NUMBERVALUE(A1:A2)",
        })
        {
            Assert.AreEqual(
                FormulaErrorCode.InvalidValue,
                engine.Evaluate(formula, context).ErrorCode,
                formula);
        }

        var registry = new BuiltInFormulaFunctionRegistry();
        foreach (var name in FunctionNames)
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
                FormulaFunctionVolatility.Deterministic,
                descriptor.Volatility);

            var isNumberValue = name == "NUMBERVALUE";
            Assert.AreEqual(
                !isNumberValue,
                descriptor.Capabilities.HasFlag(
                    FormulaFunctionCapabilities.RangeArguments));
            Assert.AreEqual(
                isNumberValue
                    ? FormulaFunctionSecurityClassification.ContextReadOnly
                    : FormulaFunctionSecurityClassification.Pure,
                descriptor.SecurityClassification);
        }

        Assert.AreEqual(
            BuiltInFormulaTestCounts.EagerVersioned,
            registry.Count);
        Assert.AreEqual(
            BuiltInFormulaTestCounts.EagerVersioned,
            registry.VersionCount);
    }

    private static void AssertNumericError(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        var result = engine.Evaluate(formula, context);
        Assert.AreEqual("#NUM!", result.Value.RawValue, formula);
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

    private static DateTime EvaluateDate(
        NeraFormulaEngine engine,
        string formula,
        IFormulaEvaluationContext context)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(
            result.IsSuccess,
            $"Expected success for {formula}, but received {result.Value}.");
        Assert.AreEqual(CellValueKind.DateTime, result.Value.Kind);
        return ((DateTime)result.Value.RawValue!).Date;
    }

    private sealed class LocaleFormulaTestContext :
        IFormulaLocaleEvaluationContext
    {
        public LocaleFormulaTestContext(
            string decimalSeparator,
            string groupSeparator)
        {
            DecimalSeparator = decimalSeparator;
            GroupSeparator = groupSeparator;
        }

        public string DecimalSeparator { get; }

        public string GroupSeparator { get; }

        public CellValue GetCellValue(
            string? worksheetName,
            CellAddress address) => CellValue.Blank;
    }
}
