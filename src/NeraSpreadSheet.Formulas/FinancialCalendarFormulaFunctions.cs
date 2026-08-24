using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Financial calendar and day-count functions shared by future bond, treasury,
/// price, yield and duration implementations.
/// </summary>
internal static class FinancialCalendarFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition(
            "YEARFRAC",
            2,
            3,
            EvaluateYearFraction);
        yield return CreateDefinition(
            "COUPDAYBS",
            3,
            4,
            invocation => EvaluateCouponNumber(
                invocation,
                CouponNumberOperation.DaysBeforeSettlement));
        yield return CreateDefinition(
            "COUPDAYS",
            3,
            4,
            invocation => EvaluateCouponNumber(
                invocation,
                CouponNumberOperation.DaysInPeriod));
        yield return CreateDefinition(
            "COUPDAYSNC",
            3,
            4,
            invocation => EvaluateCouponNumber(
                invocation,
                CouponNumberOperation.DaysAfterSettlement));
        yield return CreateDefinition(
            "COUPNCD",
            3,
            4,
            invocation => EvaluateCouponDate(
                invocation,
                returnNextCoupon: true));
        yield return CreateDefinition(
            "COUPPCD",
            3,
            4,
            invocation => EvaluateCouponDate(
                invocation,
                returnNextCoupon: false));
        yield return CreateDefinition(
            "COUPNUM",
            3,
            4,
            invocation => EvaluateCouponNumber(
                invocation,
                CouponNumberOperation.RemainingCouponCount));
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

    private static FormulaEvaluationResult EvaluateYearFraction(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarDate(
                invocation.Arguments[0],
                out var startDate,
                out var error) ||
            !TryGetScalarDate(
                invocation.Arguments[1],
                out var endDate,
                out error))
        {
            return error;
        }

        var basis = 0;
        if (invocation.Arguments.Count == 3 &&
            !TryGetTruncatedInteger(
                invocation.Arguments[2],
                out basis,
                out error))
        {
            return error;
        }
        if (!FinancialDateMath.IsSupportedBasis(basis))
        {
            return NumericError();
        }

        var result = FinancialDateMath.GetYearFraction(
            startDate,
            endDate,
            (FinancialDayCountBasis)basis);
        return Number(result);
    }

    private static FormulaEvaluationResult EvaluateCouponDate(
        FormulaFunctionInvocation invocation,
        bool returnNextCoupon)
    {
        if (!TryGetCouponArguments(
                invocation,
                out _,
                out _,
                out _,
                out _,
                out var period,
                out var error))
        {
            return error;
        }

        return FormulaEvaluationResult.Success(
            CellValue.FromDateTime(
                returnNextCoupon
                    ? period.NextCoupon
                    : period.PreviousCoupon));
    }

    private static FormulaEvaluationResult EvaluateCouponNumber(
        FormulaFunctionInvocation invocation,
        CouponNumberOperation operation)
    {
        if (!TryGetCouponArguments(
                invocation,
                out var settlement,
                out _,
                out var frequency,
                out var basis,
                out var period,
                out var error))
        {
            return error;
        }

        var dayCountBasis = (FinancialDayCountBasis)basis;
        var result = operation switch
        {
            CouponNumberOperation.DaysBeforeSettlement =>
                FinancialDateMath.GetCouponDaysBeforeSettlement(
                    period,
                    settlement,
                    dayCountBasis),
            CouponNumberOperation.DaysInPeriod =>
                FinancialDateMath.GetCouponDays(
                    period,
                    frequency,
                    dayCountBasis),
            CouponNumberOperation.DaysAfterSettlement =>
                FinancialDateMath.GetCouponDaysAfterSettlement(
                    period,
                    settlement,
                    frequency,
                    dayCountBasis),
            CouponNumberOperation.RemainingCouponCount =>
                period.RemainingCouponCount,
            _ => double.NaN,
        };
        return Number(result);
    }

    private static bool TryGetCouponArguments(
        FormulaFunctionInvocation invocation,
        out DateTime settlement,
        out DateTime maturity,
        out int frequency,
        out int basis,
        out FinancialCouponPeriod period,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarDate(
                invocation.Arguments[0],
                out settlement,
                out error) ||
            !TryGetScalarDate(
                invocation.Arguments[1],
                out maturity,
                out error) ||
            !TryGetTruncatedInteger(
                invocation.Arguments[2],
                out frequency,
                out error))
        {
            basis = default;
            period = default;
            return false;
        }

        basis = 0;
        if (invocation.Arguments.Count == 4 &&
            !TryGetTruncatedInteger(
                invocation.Arguments[3],
                out basis,
                out error))
        {
            period = default;
            return false;
        }

        if (!FinancialDateMath.IsSupportedBasis(basis) ||
            !FinancialDateMath.IsSupportedFrequency(frequency) ||
            !FinancialDateMath.TryGetCouponPeriod(
                settlement,
                maturity,
                frequency,
                out period))
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

    private static bool TryGetTruncatedInteger(
        FormulaFunctionArgument argument,
        out int value,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryNumber(
                argument.ScalarValue,
                out var number,
                allowText: true) ||
            !double.IsFinite(number))
        {
            value = default;
            error = InvalidValue();
            return false;
        }

        var truncated = Math.Truncate(number);
        if (truncated < int.MinValue || truncated > int.MaxValue)
        {
            value = default;
            error = NumericError();
            return false;
        }

        value = checked((int)truncated);
        error = default!;
        return true;
    }

    private static FormulaEvaluationResult Number(double value) =>
        double.IsFinite(value)
            ? FormulaEvaluationResult.Success(
                CellValue.FromNumber(value))
            : NumericError();

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(
            FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());

    private enum CouponNumberOperation
    {
        DaysBeforeSettlement,
        DaysInPeriod,
        DaysAfterSettlement,
        RemainingCouponCount,
    }
}
