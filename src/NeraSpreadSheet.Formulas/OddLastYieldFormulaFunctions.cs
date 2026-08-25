using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Odd-last yield is the algebraic inverse of the validated ODDLPRICE state.
/// The implementation reuses FinancialDateMath for all quasi-coupon ratios.
/// </summary>
internal static class OddLastYieldFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "NERA.BUILTIN",
                    "ODDLYIELD"),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                7,
                8,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            EvaluateOddLastYield);
    }

    private static FormulaEvaluationResult EvaluateOddLastYield(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetOddLastState(
                invocation,
                out var state,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[3],
                out var couponRate,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[4],
                out var price,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[5],
                out var redemption,
                out error))
        {
            return error;
        }
        if (couponRate < 0d ||
            price <= 0d ||
            redemption <= 0d)
        {
            return NumericError();
        }

        var coupon = 100d * couponRate / state.Frequency;
        var dirtyPrice =
            price +
            (coupon * state.LastCouponToSettlementPeriods);
        var maturityValue =
            redemption +
            (coupon * state.LastCouponToMaturityPeriods);
        if (!double.IsFinite(coupon) ||
            !double.IsFinite(dirtyPrice) ||
            dirtyPrice <= 0d ||
            !double.IsFinite(maturityValue) ||
            !double.IsFinite(state.SettlementToMaturityPeriods) ||
            state.SettlementToMaturityPeriods <= 0d)
        {
            return NumericError();
        }

        var yield =
            state.Frequency *
            ((maturityValue / dirtyPrice) - 1d) /
            state.SettlementToMaturityPeriods;
        return Number(yield);
    }

    private static bool TryGetOddLastState(
        FormulaFunctionInvocation invocation,
        out OddLastCouponState state,
        out FormulaEvaluationResult error)
    {
        state = default;
        if (!TryGetScalarDate(
                invocation.Arguments[0],
                out var settlement,
                out error) ||
            !TryGetScalarDate(
                invocation.Arguments[1],
                out var maturity,
                out error) ||
            !TryGetScalarDate(
                invocation.Arguments[2],
                out var lastCoupon,
                out error) ||
            !TryGetTruncatedInteger(
                invocation.Arguments[6],
                out var frequency,
                out error))
        {
            return false;
        }

        var basis = 0;
        if (invocation.Arguments.Count == 8 &&
            !TryGetTruncatedInteger(
                invocation.Arguments[7],
                out basis,
                out error))
        {
            return false;
        }
        if (!(lastCoupon < settlement &&
              settlement < maturity) ||
            !FinancialDateMath.IsSupportedFrequency(frequency) ||
            !FinancialDateMath.IsSupportedBasis(basis) ||
            !TryGetCouponBoundaryOnOrAfter(
                lastCoupon,
                maturity,
                frequency,
                out var anchorDate))
        {
            error = NumericError();
            return false;
        }

        var dayCountBasis = (FinancialDayCountBasis)basis;
        if (!FinancialDateMath.TryGetCouponPeriodRatio(
                lastCoupon,
                settlement,
                anchorDate,
                frequency,
                dayCountBasis,
                out var lastCouponToSettlement) ||
            !FinancialDateMath.TryGetCouponPeriodRatio(
                lastCoupon,
                maturity,
                anchorDate,
                frequency,
                dayCountBasis,
                out var lastCouponToMaturity) ||
            !FinancialDateMath.TryGetCouponPeriodRatio(
                settlement,
                maturity,
                anchorDate,
                frequency,
                dayCountBasis,
                out var settlementToMaturity) ||
            !double.IsFinite(lastCouponToSettlement) ||
            !double.IsFinite(lastCouponToMaturity) ||
            !double.IsFinite(settlementToMaturity) ||
            lastCouponToSettlement <= 0d ||
            lastCouponToMaturity <= 0d ||
            settlementToMaturity <= 0d)
        {
            error = NumericError();
            return false;
        }

        state = new OddLastCouponState(
            frequency,
            lastCouponToSettlement,
            lastCouponToMaturity,
            settlementToMaturity);
        error = default!;
        return true;
    }

    private static bool TryGetCouponBoundaryOnOrAfter(
        DateTime lastCoupon,
        DateTime maturity,
        int frequency,
        out DateTime boundary)
    {
        var monthsPerCoupon = 12 / frequency;
        for (var couponIndex = 1;
             couponIndex <= FinancialDateMath.MaximumCouponPeriods;
             couponIndex++)
        {
            if (!FinancialDateMath.TryAddCouponMonths(
                    lastCoupon,
                    (long)couponIndex * monthsPerCoupon,
                    out var couponDate))
            {
                boundary = default;
                return false;
            }

            if (couponDate >= maturity)
            {
                boundary = couponDate;
                return true;
            }
        }

        boundary = default;
        return false;
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

    private static bool TryGetTruncatedInteger(
        FormulaFunctionArgument argument,
        out int value,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarNumber(
                argument,
                out var number,
                out error))
        {
            value = default;
            return false;
        }

        var truncated = Math.Truncate(number);
        if (truncated < int.MinValue ||
            truncated > int.MaxValue)
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

    private readonly record struct OddLastCouponState(
        int Frequency,
        double LastCouponToSettlementPeriods,
        double LastCouponToMaturityPeriods,
        double SettlementToMaturityPeriods);
}
