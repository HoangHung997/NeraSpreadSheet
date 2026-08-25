using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Odd-first and odd-last fixed-coupon security functions. Odd-first price and
/// yield share one quasi-coupon state and one bounded inverse equation.
/// </summary>
internal static class OddCouponFormulaFunctions
{
    private const int MaximumYieldIterations = 256;
    private const double MinimumYieldLogBase = -40d;
    private const double MaximumYieldLogBase = 40d;
    private const double MaximumExpArgument = 709d;
    private const double MinimumExpArgument = -745d;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition(
            "ODDFPRICE",
            8,
            9,
            EvaluateOddFirstPrice);
        yield return CreateDefinition(
            "ODDFYIELD",
            8,
            9,
            EvaluateOddFirstYield);
        yield return CreateDefinition(
            "ODDLPRICE",
            7,
            8,
            EvaluateOddLastPrice);
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

    private static FormulaEvaluationResult EvaluateOddFirstPrice(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetOddFirstArguments(
                invocation,
                out var state,
                out var couponRate,
                out var yield,
                out var redemption,
                out var error))
        {
            return error;
        }
        if (couponRate < 0d ||
            yield < 0d ||
            redemption <= 0d ||
            !TryLogOnePlus(
                yield / state.Frequency,
                out var yieldLogBase) ||
            !TryEvaluateOddFirstPrice(
                state,
                couponRate,
                redemption,
                yieldLogBase,
                out var price))
        {
            return NumericError();
        }

        return Number(price);
    }

    private static FormulaEvaluationResult EvaluateOddFirstYield(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetOddFirstArguments(
                invocation,
                out var state,
                out var couponRate,
                out var targetPrice,
                out var redemption,
                out var error))
        {
            return error;
        }
        if (couponRate < 0d ||
            targetPrice <= 0d ||
            redemption <= 0d ||
            !TrySolveOddFirstYield(
                state,
                couponRate,
                targetPrice,
                redemption,
                out var yield))
        {
            return NumericError();
        }

        return Number(yield);
    }

    private static FormulaEvaluationResult EvaluateOddLastPrice(
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
                out var yield,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[5],
                out var redemption,
                out error))
        {
            return error;
        }
        if (couponRate < 0d ||
            yield < 0d ||
            redemption <= 0d)
        {
            return NumericError();
        }

        var coupon = 100d * couponRate / state.Frequency;
        var denominator =
            1d +
            ((yield / state.Frequency) *
             state.SettlementToMaturityPeriods);
        if (!double.IsFinite(coupon) ||
            !double.IsFinite(denominator) ||
            denominator == 0d)
        {
            return NumericError();
        }

        var price =
            (redemption +
             (coupon * state.LastCouponToMaturityPeriods)) /
            denominator -
            (coupon * state.LastCouponToSettlementPeriods);
        return Number(price);
    }

    private static bool TryGetOddFirstArguments(
        FormulaFunctionInvocation invocation,
        out OddFirstCouponState state,
        out double couponRate,
        out double fourthValue,
        out double redemption,
        out FormulaEvaluationResult error)
    {
        state = default;
        couponRate = default;
        fourthValue = default;
        redemption = default;
        if (!TryGetOddFirstState(
                invocation,
                out state,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[4],
                out couponRate,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[5],
                out fourthValue,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[6],
                out redemption,
                out error))
        {
            return false;
        }

        error = default!;
        return true;
    }

    private static bool TryGetOddFirstState(
        FormulaFunctionInvocation invocation,
        out OddFirstCouponState state,
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
                out var issue,
                out error) ||
            !TryGetScalarDate(
                invocation.Arguments[3],
                out var firstCoupon,
                out error) ||
            !TryGetTruncatedInteger(
                invocation.Arguments[7],
                out var frequency,
                out error))
        {
            return false;
        }

        var basis = 0;
        if (invocation.Arguments.Count == 9 &&
            !TryGetTruncatedInteger(
                invocation.Arguments[8],
                out basis,
                out error))
        {
            return false;
        }
        if (!(issue < settlement &&
              settlement < firstCoupon &&
              firstCoupon < maturity) ||
            !FinancialDateMath.IsSupportedFrequency(frequency) ||
            !FinancialDateMath.IsSupportedBasis(basis) ||
            !TryGetRegularCouponCount(
                firstCoupon,
                maturity,
                frequency,
                out var regularCouponCount))
        {
            error = NumericError();
            return false;
        }

        var dayCountBasis = (FinancialDayCountBasis)basis;
        if (!FinancialDateMath.TryGetCouponPeriodRatio(
                issue,
                firstCoupon,
                firstCoupon,
                frequency,
                dayCountBasis,
                out var firstCouponPeriods) ||
            !FinancialDateMath.TryGetCouponPeriodRatio(
                issue,
                settlement,
                firstCoupon,
                frequency,
                dayCountBasis,
                out var accruedPeriods) ||
            !FinancialDateMath.TryGetCouponPeriodRatio(
                settlement,
                firstCoupon,
                firstCoupon,
                frequency,
                dayCountBasis,
                out var firstDiscountPeriods) ||
            !double.IsFinite(firstCouponPeriods) ||
            !double.IsFinite(accruedPeriods) ||
            !double.IsFinite(firstDiscountPeriods) ||
            firstCouponPeriods <= 0d ||
            accruedPeriods < 0d ||
            firstDiscountPeriods <= 0d)
        {
            error = NumericError();
            return false;
        }

        state = new OddFirstCouponState(
            frequency,
            regularCouponCount,
            firstCouponPeriods,
            accruedPeriods,
            firstDiscountPeriods);
        error = default!;
        return true;
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

    private static bool TryGetRegularCouponCount(
        DateTime firstCoupon,
        DateTime maturity,
        int frequency,
        out int couponCount)
    {
        var monthsPerCoupon = 12 / frequency;
        for (var couponIndex = 1;
             couponIndex <= FinancialDateMath.MaximumCouponPeriods;
             couponIndex++)
        {
            if (!FinancialDateMath.TryAddCouponMonths(
                    firstCoupon,
                    (long)couponIndex * monthsPerCoupon,
                    out var couponDate))
            {
                couponCount = default;
                return false;
            }

            if (couponDate == maturity)
            {
                couponCount = couponIndex;
                return true;
            }
            if (couponDate > maturity)
            {
                couponCount = default;
                return false;
            }
        }

        couponCount = default;
        return false;
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

    private static bool TrySolveOddFirstYield(
        OddFirstCouponState state,
        double couponRate,
        double targetPrice,
        double redemption,
        out double yield)
    {
        var lower = MinimumYieldLogBase;
        var upper = MaximumYieldLogBase;
        if (!TryEvaluateOddFirstPrice(
                state,
                couponRate,
                redemption,
                lower,
                out var lowerPrice) ||
            !TryEvaluateOddFirstPrice(
                state,
                couponRate,
                redemption,
                upper,
                out var upperPrice) ||
            lowerPrice < targetPrice ||
            upperPrice > targetPrice)
        {
            yield = default;
            return false;
        }

        for (var iteration = 0;
             iteration < MaximumYieldIterations;
             iteration++)
        {
            var middle = lower + ((upper - lower) / 2d);
            if (!TryEvaluateOddFirstPrice(
                    state,
                    couponRate,
                    redemption,
                    middle,
                    out var middlePrice))
            {
                yield = default;
                return false;
            }

            if (double.IsFinite(middlePrice) &&
                Math.Abs(middlePrice - targetPrice) <=
                1e-12d * Math.Max(1d, targetPrice))
            {
                return TryConvertYieldLogBase(
                    state.Frequency,
                    middle,
                    out yield);
            }

            if (middlePrice > targetPrice)
            {
                lower = middle;
            }
            else
            {
                upper = middle;
            }

            if (upper - lower <= 2e-14d)
            {
                return TryConvertYieldLogBase(
                    state.Frequency,
                    lower + ((upper - lower) / 2d),
                    out yield);
            }
        }

        yield = default;
        return false;
    }

    private static bool TryEvaluateOddFirstPrice(
        OddFirstCouponState state,
        double couponRate,
        double redemption,
        double yieldLogBase,
        out double price)
    {
        price = default;
        if (!double.IsFinite(yieldLogBase) ||
            couponRate < 0d ||
            redemption <= 0d)
        {
            return false;
        }

        var coupon = 100d * couponRate / state.Frequency;
        var sum = 0d;
        var compensation = 0d;
        if (!TryAddDiscountedCashFlow(
                coupon * state.FirstCouponPeriods,
                state.FirstDiscountPeriods,
                yieldLogBase,
                ref sum,
                ref compensation,
                out var overflowed))
        {
            return false;
        }
        if (overflowed)
        {
            price = double.PositiveInfinity;
            return true;
        }

        for (var couponIndex = 1;
             couponIndex <= state.RegularCouponCount;
             couponIndex++)
        {
            var cashFlow = coupon;
            if (couponIndex == state.RegularCouponCount)
            {
                cashFlow += redemption;
            }

            if (!TryAddDiscountedCashFlow(
                    cashFlow,
                    state.FirstDiscountPeriods + couponIndex,
                    yieldLogBase,
                    ref sum,
                    ref compensation,
                    out overflowed))
            {
                return false;
            }
            if (overflowed)
            {
                price = double.PositiveInfinity;
                return true;
            }
        }

        var accruedInterest =
            coupon * state.AccruedPeriods;
        price = sum - accruedInterest;
        return double.IsFinite(price);
    }

    private static bool TryAddDiscountedCashFlow(
        double cashFlow,
        double exponent,
        double yieldLogBase,
        ref double sum,
        ref double compensation,
        out bool overflowed)
    {
        overflowed = false;
        if (!double.IsFinite(cashFlow) ||
            !double.IsFinite(exponent) ||
            exponent <= 0d)
        {
            return false;
        }
        if (cashFlow == 0d)
        {
            return true;
        }

        var discountArgument = -exponent * yieldLogBase;
        if (!double.IsFinite(discountArgument))
        {
            return false;
        }
        if (discountArgument > MaximumExpArgument)
        {
            overflowed = true;
            return true;
        }

        var discountFactor = discountArgument < MinimumExpArgument
            ? 0d
            : Math.Exp(discountArgument);
        var discountedCashFlow =
            cashFlow * discountFactor;
        if (!double.IsFinite(discountedCashFlow))
        {
            return false;
        }

        AddCompensated(
            ref sum,
            ref compensation,
            discountedCashFlow);
        return true;
    }

    private static bool TryConvertYieldLogBase(
        int frequency,
        double logBase,
        out double yield)
    {
        if (!double.IsFinite(logBase) ||
            logBase > MaximumExpArgument)
        {
            yield = default;
            return false;
        }

        yield = frequency * ExpMinusOne(logBase);
        return double.IsFinite(yield) &&
               yield > -frequency;
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
        if (!TryGetScalarNumber(argument, out var number, out error))
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

    private static bool TryLogOnePlus(
        double value,
        out double result)
    {
        if (!double.IsFinite(value) ||
            value <= -1d)
        {
            result = default;
            return false;
        }
        if (value == 0d)
        {
            result = 0d;
            return true;
        }
        if (Math.Abs(value) > 0.5d)
        {
            result = Math.Log(1d + value);
            return double.IsFinite(result);
        }

        var term = value;
        var sum = 0d;
        for (var index = 1; index <= 64; index++)
        {
            sum += (index & 1) == 1
                ? term / index
                : -(term / index);
            term *= value;
        }

        result = sum;
        return double.IsFinite(result);
    }

    private static double ExpMinusOne(double value)
    {
        if (Math.Abs(value) > 1e-5d)
        {
            return Math.Exp(value) - 1d;
        }

        var term = value;
        var sum = value;
        for (var index = 2; index <= 32; index++)
        {
            term *= value / index;
            sum += term;
        }

        return sum;
    }

    private static void AddCompensated(
        ref double sum,
        ref double compensation,
        double value)
    {
        var adjusted = value - compensation;
        var next = sum + adjusted;
        compensation = (next - sum) - adjusted;
        sum = next;
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

    private readonly record struct OddFirstCouponState(
        int Frequency,
        int RegularCouponCount,
        double FirstCouponPeriods,
        double AccruedPeriods,
        double FirstDiscountPeriods);

    private readonly record struct OddLastCouponState(
        int Frequency,
        double LastCouponToSettlementPeriods,
        double LastCouponToMaturityPeriods,
        double SettlementToMaturityPeriods);
}
