using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Treasury-bill pricing/yield functions and fractional-dollar conversions.
/// Treasury bills share one strict date/domain reader; dollar conversions share
/// one truncated denominator and decimal-place scale.
/// </summary>
internal static class TreasuryBillAndDollarFormulaFunctions
{
    private const double TreasuryBillYearDays = 360d;
    private const double BondEquivalentYearDays = 365d;
    private const double FaceValue = 100d;
    private const double MaximumPowerOfTenExponent = 308d;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition(
            "TBILLEQ",
            3,
            3,
            EvaluateTreasuryBillEquivalentYield);
        yield return CreateDefinition(
            "TBILLPRICE",
            3,
            3,
            EvaluateTreasuryBillPrice);
        yield return CreateDefinition(
            "TBILLYIELD",
            3,
            3,
            EvaluateTreasuryBillYield);
        yield return CreateDefinition(
            "DOLLARDE",
            2,
            2,
            EvaluateFractionalDollarToDecimal);
        yield return CreateDefinition(
            "DOLLARFR",
            2,
            2,
            EvaluateDecimalDollarToFractional);
    }

    private static FormulaFunctionDefinition CreateDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaEvaluationResult EvaluateTreasuryBillEquivalentYield(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetTreasuryBillArguments(
                invocation,
                out _,
                out _,
                out var discount,
                out var daysToMaturity,
                out var error))
        {
            return error;
        }
        if (discount <= 0d)
        {
            return NumericError();
        }

        var denominator = TreasuryBillYearDays -
                          (discount * daysToMaturity);
        if (!double.IsFinite(denominator) || denominator == 0d)
        {
            return NumericError();
        }

        return Number(
            BondEquivalentYearDays * discount / denominator);
    }

    private static FormulaEvaluationResult EvaluateTreasuryBillPrice(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetTreasuryBillArguments(
                invocation,
                out _,
                out _,
                out var discount,
                out var daysToMaturity,
                out var error))
        {
            return error;
        }
        if (discount <= 0d)
        {
            return NumericError();
        }

        var price = FaceValue *
                    (1d -
                     (discount * daysToMaturity /
                      TreasuryBillYearDays));
        return Number(price);
    }

    private static FormulaEvaluationResult EvaluateTreasuryBillYield(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetTreasuryBillArguments(
                invocation,
                out _,
                out _,
                out var price,
                out var daysToMaturity,
                out var error))
        {
            return error;
        }
        if (price <= 0d)
        {
            return NumericError();
        }

        var denominator = price * daysToMaturity;
        if (!double.IsFinite(denominator) || denominator <= 0d)
        {
            return NumericError();
        }

        return Number(
            (FaceValue - price) *
            TreasuryBillYearDays /
            denominator);
    }

    private static FormulaEvaluationResult EvaluateFractionalDollarToDecimal(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetDollarArguments(
                invocation,
                out var fractionalDollar,
                out var denominator,
                out var scale,
                out var error))
        {
            return error;
        }

        var whole = Math.Truncate(fractionalDollar);
        var fractional = fractionalDollar - whole;
        return Number(
            whole + ((fractional * scale) / denominator));
    }

    private static FormulaEvaluationResult EvaluateDecimalDollarToFractional(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetDollarArguments(
                invocation,
                out var decimalDollar,
                out var denominator,
                out var scale,
                out var error))
        {
            return error;
        }

        var whole = Math.Truncate(decimalDollar);
        var fractional = decimalDollar - whole;
        return Number(
            whole + ((fractional * denominator) / scale));
    }

    private static bool TryGetTreasuryBillArguments(
        FormulaFunctionInvocation invocation,
        out DateTime settlement,
        out DateTime maturity,
        out double value,
        out double daysToMaturity,
        out FormulaEvaluationResult error)
    {
        settlement = default;
        maturity = default;
        value = default;
        daysToMaturity = default;

        if (!TryGetScalarDate(
                invocation.Arguments[0],
                out settlement,
                out error) ||
            !TryGetScalarDate(
                invocation.Arguments[1],
                out maturity,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out value,
                out error))
        {
            return false;
        }

        if (settlement >= maturity ||
            !IsWithinOneCalendarYear(settlement, maturity))
        {
            error = NumericError();
            return false;
        }

        daysToMaturity = (maturity - settlement).TotalDays;
        if (!double.IsFinite(daysToMaturity) ||
            daysToMaturity <= 0d)
        {
            error = NumericError();
            return false;
        }

        error = default!;
        return true;
    }

    private static bool IsWithinOneCalendarYear(
        DateTime settlement,
        DateTime maturity)
    {
        if (settlement.Year == DateTime.MaxValue.Year)
        {
            return true;
        }

        return maturity <= settlement.AddYears(1);
    }

    private static bool TryGetDollarArguments(
        FormulaFunctionInvocation invocation,
        out double dollar,
        out double denominator,
        out double scale,
        out FormulaEvaluationResult error)
    {
        dollar = default;
        denominator = default;
        scale = default;

        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out dollar,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var fraction,
                out error))
        {
            return false;
        }

        if (fraction < 0d)
        {
            error = NumericError();
            return false;
        }

        denominator = Math.Truncate(fraction);
        if (denominator < 1d)
        {
            error = DivisionByZero();
            return false;
        }

        var exponent = Math.Ceiling(Math.Log10(denominator));
        if (!double.IsFinite(exponent) ||
            exponent < 0d ||
            exponent > MaximumPowerOfTenExponent)
        {
            error = NumericError();
            return false;
        }

        scale = Math.Pow(10d, exponent);
        if (!double.IsFinite(scale) || scale <= 0d)
        {
            error = NumericError();
            return false;
        }

        error = default!;
        return true;
    }

    private static bool TryGetScalarDate(
        FormulaFunctionArgument argument,
        out DateTime date,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryDateTime(
                argument.ScalarValue,
                out date,
                allowText: true))
        {
            date = default;
            error = InvalidValue();
            return false;
        }

        date = date.Date;
        error = default!;
        return true;
    }

    private static bool TryGetScalarNumber(
        FormulaFunctionArgument argument,
        out double value,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryNumber(
                argument.ScalarValue,
                out value,
                allowText: true) ||
            !double.IsFinite(value))
        {
            value = default;
            error = InvalidValue();
            return false;
        }

        error = default!;
        return true;
    }

    private static FormulaEvaluationResult Number(double value) =>
        double.IsFinite(value)
            ? FormulaEvaluationResult.Success(CellValue.FromNumber(value))
            : NumericError();

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult DivisionByZero() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.DivisionByZero);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());
}
