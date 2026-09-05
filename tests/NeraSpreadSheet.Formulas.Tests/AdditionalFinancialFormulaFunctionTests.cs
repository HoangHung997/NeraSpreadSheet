using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class AdditionalFinancialFormulaFunctionTests
{
    [TestMethod]
    public void RateMatchesReferenceValuesAndRoundTripsPayment()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            0.007701472488202044d,
            EvaluateNumber(
                engine,
                "=RATE(48,-200,8000)",
                context),
            2e-12d);
        Assert.AreEqual(
            -0.7562659368780726d,
            EvaluateNumber(
                engine,
                "=RATE(3,-10,900,0,0,-0.7)",
                context),
            2e-11d);
        Assert.AreEqual(
            0.05d / 12d,
            EvaluateNumber(
                engine,
                "=RATE(60,PMT(0.05/12,60,10000,0,1),10000,0,1)",
                context),
            2e-11d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=RATE(10,-100,1000)",
                context),
            2e-10d);
    }

    [TestMethod]
    public void RateDomainTimingAndScalarCapabilityFailuresAreExplicit()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromNumber(10d),
            [new CellAddress(1, 0)] = CellValue.FromNumber(20d),
        };
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext(values);

        AssertNumericError(
            engine,
            "=RATE(0,-100,1000)",
            context);
        AssertNumericError(
            engine,
            "=RATE(10,100,1000)",
            context);
        AssertNumericError(
            engine,
            "=RATE(10,-100,1000,0,0,-1)",
            context);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=RATE(10,-100,1000,0,2)",
                context).ErrorCode);
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=RATE(A1:A2,-100,1000)",
                context).ErrorCode);
    }

    [TestMethod]
    public void XnpvAndXirrMatchIrregularScheduleReferences()
    {
        var engine = new NeraFormulaEngine();
        var context = AdditionalFinancialTestData.CreateReferenceContext();

        Assert.AreEqual(
            2086.6476020315363d,
            EvaluateNumber(
                engine,
                "=XNPV(0.09,A1:A5,B1:B5)",
                context),
            2e-9d);
        Assert.AreEqual(
            0.3733625335188315d,
            EvaluateNumber(
                engine,
                "=XIRR(A1:A5,B1:B5)",
                context),
            2e-9d);
        Assert.AreEqual(
            0d,
            EvaluateNumber(
                engine,
                "=XNPV(XIRR(A1:A5,B1:B5),A1:A5,B1:B5)",
                context),
            2e-6d);
    }

    [TestMethod]
    public void ScheduledDatesMayBeUnsortedAfterTheFirst()
    {
        var reordered = new Dictionary<CellAddress, CellValue>();
        var order = new[] { 0, 3, 1, 4, 2 };
        for (var row = 0; row < order.Length; row++)
        {
            var source = order[row];
            reordered[new CellAddress(row, 0)] =
                CellValue.FromNumber(
                    AdditionalFinancialTestData.ReferenceValues[source]);
            reordered[new CellAddress(row, 1)] =
                CellValue.FromDateTime(
                    AdditionalFinancialTestData.ReferenceDates[source]);
        }

        var engine = new NeraFormulaEngine();
        var reference =
            AdditionalFinancialTestData.CreateReferenceContext();
        var reorderedContext =
            new FormulaSurfaceTestContext(reordered);

        Assert.AreEqual(
            EvaluateNumber(
                engine,
                "=XNPV(0.09,A1:A5,B1:B5)",
                reference),
            EvaluateNumber(
                engine,
                "=XNPV(0.09,A1:A5,B1:B5)",
                reorderedContext),
            2e-10d);
        Assert.AreEqual(
            EvaluateNumber(
                engine,
                "=XIRR(A1:A5,B1:B5)",
                reference),
            EvaluateNumber(
                engine,
                "=XIRR(A1:A5,B1:B5)",
                reorderedContext),
            2e-10d);
    }

    [TestMethod]
    public void NumericScheduledDatesAreTruncatedToWholeDays()
    {
        var wholeDates = new Dictionary<CellAddress, CellValue>();
        var fractionalDates = new Dictionary<CellAddress, CellValue>();
        for (var row = 0;
             row < AdditionalFinancialTestData.ReferenceValues.Length;
             row++)
        {
            var value =
                AdditionalFinancialTestData.ReferenceValues[row];
            wholeDates[new CellAddress(row, 0)] =
                CellValue.FromNumber(value);
            fractionalDates[new CellAddress(row, 0)] =
                CellValue.FromNumber(value);
            var serial =
                AdditionalFinancialTestData.ReferenceDates[row].ToOADate();
            wholeDates[new CellAddress(row, 1)] =
                CellValue.FromNumber(serial);
            fractionalDates[new CellAddress(row, 1)] =
                CellValue.FromNumber(serial + 0.9d);
        }

        var engine = new NeraFormulaEngine();
        var wholeContext =
            new FormulaSurfaceTestContext(wholeDates);
        var fractionalContext =
            new FormulaSurfaceTestContext(fractionalDates);

        Assert.AreEqual(
            EvaluateNumber(
                engine,
                "=XNPV(0.09,A1:A5,B1:B5)",
                wholeContext),
            EvaluateNumber(
                engine,
                "=XNPV(0.09,A1:A5,B1:B5)",
                fractionalContext),
            1e-12d);
        Assert.AreEqual(
            EvaluateNumber(
                engine,
                "=XIRR(A1:A5,B1:B5)",
                wholeContext),
            EvaluateNumber(
                engine,
                "=XIRR(A1:A5,B1:B5)",
                fractionalContext),
            1e-12d);
    }

    [TestMethod]
    public void ScheduleValidationUsesExplicitValueAndNumericErrors()
    {
        var engine = new NeraFormulaEngine();
        var context =
            AdditionalFinancialTestData.CreateReferenceContext();

        AssertNumericError(
            engine,
            "=XNPV(0.09,A1:A5,B1:B4)",
            context);
        AssertNumericError(
            engine,
            "=XIRR(A1:A5,B1:B4)",
            context);
        AssertNumericError(
            engine,
            "=XNPV(-1,A1:A5,B1:B5)",
            context);

        var earlierDate =
            AdditionalFinancialTestData.CreateReferenceValues();
        earlierDate[new CellAddress(2, 1)] =
            CellValue.FromDateTime(new DateTime(2007, 12, 31));
        var earlierDateContext =
            new FormulaSurfaceTestContext(earlierDate);
        AssertNumericError(
            engine,
            "=XNPV(0.09,A1:A5,B1:B5)",
            earlierDateContext);
        AssertNumericError(
            engine,
            "=XIRR(A1:A5,B1:B5)",
            earlierDateContext);

        var textValue =
            AdditionalFinancialTestData.CreateReferenceValues();
        textValue[new CellAddress(2, 0)] =
            CellValue.FromText("4250");
        Assert.AreEqual(
            FormulaErrorCode.InvalidValue,
            engine.Evaluate(
                "=XNPV(0.09,A1:A5,B1:B5)",
                new FormulaSurfaceTestContext(textValue)).ErrorCode);

        var positiveOnly =
            AdditionalFinancialTestData.CreateReferenceValues();
        positiveOnly[new CellAddress(0, 0)] =
            CellValue.FromNumber(10000d);
        var positiveOnlyContext =
            new FormulaSurfaceTestContext(positiveOnly);
        AssertNumericError(
            engine,
            "=XNPV(0.09,A1:A5,B1:B5)",
            positiveOnlyContext);
        AssertNumericError(
            engine,
            "=XIRR(A1:A5,B1:B5)",
            positiveOnlyContext);
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
}

internal static class AdditionalFinancialTestData
{
    public static readonly DateTime[] ReferenceDates =
    [
        new(2008, 1, 1),
        new(2008, 3, 1),
        new(2008, 10, 30),
        new(2009, 2, 15),
        new(2009, 4, 1),
    ];

    public static readonly double[] ReferenceValues =
    [
        -10000d,
        2750d,
        4250d,
        3250d,
        2750d,
    ];

    public static FormulaSurfaceTestContext CreateReferenceContext() =>
        new(CreateReferenceValues());

    public static Dictionary<CellAddress, CellValue>
        CreateReferenceValues()
    {
        var result = new Dictionary<CellAddress, CellValue>();
        for (var row = 0; row < ReferenceValues.Length; row++)
        {
            result[new CellAddress(row, 0)] =
                CellValue.FromNumber(ReferenceValues[row]);
            result[new CellAddress(row, 1)] =
                CellValue.FromDateTime(ReferenceDates[row]);
        }
        return result;
    }
}
