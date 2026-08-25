using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Regular fixed-coupon price, yield, duration and modified internal-rate
/// functions. Bond functions share one maturity-anchored coupon state and one
/// clean-price equation.
/// </summary>
internal static class CouponBondFormulaFunctions
{
    public const int MaximumMirrValues = 2_000_000;

    private const int MaximumYieldIterations = 256;
    private const double MinimumYieldLogBase = -40d;
    private const double MaximumYieldLogBase = 40d;
    private const double MaximumExpArgument = 709d;
    private const double MinimumExpArgument = -745d;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition(
            "PRICE",
            6,
            7,
            EvaluatePrice);
        yield return CreateDefinition(
            "YIELD",
            6,
            7,
            EvaluateYield);
        yield return CreateDefinition(
            "DURATION",
            5,
            6,
            invocation => EvaluateDuration(
                invocation,
                modified: false));
        yield return CreateDefinition(
            "MDURATION",
            5,
            6,
            invocation => EvaluateDuration(
                invocation,
                modified: true));
        yield return CreateDefinition(
            "MIRR",
            3,
            3,
            EvaluateModifiedInternalRate,
            allowRanges: true);
    }

    private static FormulaFunctionDefinition CreateDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator,
        bool allowRanges = false) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                FormulaFunctionCapabilities.ScalarArguments |
                (allowRanges
                    ? FormulaFunctionCapabilities.RangeArguments
                    : FormulaFunctionCapabilities.None) |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaEvaluationResult EvaluatePrice(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetCouponSecurityArguments(
                invocation,
                out var state,
                out var couponRate,
                out var yield,
                out var redemption,
                out var error))
        {
            return error;
        }
        if (couponRate < 0d || yield < 0d || redemption <= 0d)
        {
            return NumericError();
        }

        var periodicYield = yield / state.Frequency;
        if (!TryLogOnePlus(periodicYield, out var logBase) ||
            !TryEvaluatePriceFromLogBase(
                state,
                couponRate,
                redemption,
                logBase,
                out var cleanPrice,
                out _))
        {
            return NumericError();
        }
        return Number(cleanPrice);
    }

    private static FormulaEvaluationResult EvaluateYield(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetCouponSecurityArguments(
                invocation,
                out var state,
                out var couponRate,
                out var targetPrice,
                out var redemption,
                out var error))
        {
            return error;
        }
        if (couponRate < 0d || targetPrice <= 0d || redemption <= 0d ||
            !TrySolveYield(
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

    private static FormulaEvaluationResult EvaluateDuration(
        FormulaFunctionInvocation invocation,
        bool modified)
    {
        if (!TryGetCouponState(
                invocation,
                frequencyIndex: 4,
                basisIndex: 5,
                out var state,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var couponRate,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[3],
                out var yield,
                out error))
        {
            return error;
        }
        if (couponRate < 0d || yield < 0d)
        {
            return NumericError();
        }

        var periodicYield = yield / state.Frequency;
        if (!TryLogOnePlus(periodicYield, out var logBase))
        {
            return NumericError();
        }

        var coupon = 100d * couponRate / state.Frequency;
        var firstPeriodFraction = state.DaysToNextCoupon /
                                  state.DaysInCouponPeriod;
        var presentValue = 0d;
        var presentCompensation = 0d;
        var weightedValue = 0d;
        var weightedCompensation = 0d;
        for (var couponIndex = 0;
             couponIndex < state.RemainingCouponCount;
             couponIndex++)
        {
            var periodExponent = couponIndex + firstPeriodFraction;
            var cashFlow = coupon;
            if (couponIndex == state.RemainingCouponCount - 1)
            {
                cashFlow += 100d;
            }
            if (cashFlow == 0d)
            {
                continue;
            }

            var discountExponent = -periodExponent * logBase;
            var discountFactor = discountExponent < MinimumExpArgument
                ? 0d
                : Math.Exp(discountExponent);
            var discountedCashFlow = cashFlow * discountFactor;
            var timeInYears = periodExponent / state.Frequency;
            if (!double.IsFinite(discountedCashFlow) ||
                !double.IsFinite(timeInYears))
            {
                return NumericError();
            }

            AddCompensated(
                ref presentValue,
                ref presentCompensation,
                discountedCashFlow);
            AddCompensated(
                ref weightedValue,
                ref weightedCompensation,
                timeInYears * discountedCashFlow);
        }

        if (!double.IsFinite(presentValue) || presentValue <= 0d ||
            !double.IsFinite(weightedValue))
        {
            return NumericError();
        }
        var duration = weightedValue / presentValue;
        if (modified)
        {
            duration /= 1d + periodicYield;
        }
        return Number(duration);
    }

    private static FormulaEvaluationResult EvaluateModifiedInternalRate(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[1],
                out var financeRate,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var reinvestRate,
                out error))
        {
            return error;
        }
        if (financeRate <= -1d || reinvestRate <= -1d)
        {
            return NumericError();
        }

        var values = invocation.Arguments[0];
        if (values.Values.Count < 2 ||
            values.Values.Count > MaximumMirrValues)
        {
            return values.Values.Count < 2
                ? DivisionByZero()
                : NumericError();
        }

        var cashFlows = new List<IndexedCashFlow>();
        var isScalar = values.Kind == FormulaFunctionArgumentKind.Scalar;
        var hasPositive = false;
        var hasNegative = false;
        for (var index = 0; index < values.Values.Count; index++)
        {
            if (!TryGetMirrValue(
                    values.Values[index],
                    isScalar,
                    out var value,
                    out var participates))
            {
                return InvalidValue();
            }
            if (!participates || value == 0d)
            {
                continue;
            }

            hasPositive |= value > 0d;
            hasNegative |= value < 0d;
            cashFlows.Add(new IndexedCashFlow(index, value));
        }
        if (!hasPositive || !hasNegative)
        {
            return DivisionByZero();
        }

        if (!TryLogOnePlus(financeRate, out var financeLog) ||
            !TryLogOnePlus(reinvestRate, out var reinvestLog) ||
            !TryGetLogCashFlowAggregate(
                cashFlows,
                selectPositive: false,
                values.Values.Count,
                financeLog,
                out var negativePresentValueLog) ||
            !TryGetLogCashFlowAggregate(
                cashFlows,
                selectPositive: true,
                values.Values.Count,
                reinvestLog,
                out var positiveFutureValueLog))
        {
            return NumericError();
        }

        var periods = values.Values.Count - 1d;
        var rateLog =
            (positiveFutureValueLog - negativePresentValueLog) /
            periods;
        if (!double.IsFinite(rateLog) ||
            rateLog > MaximumExpArgument)
        {
            return NumericError();
        }
        return Number(ExpMinusOne(rateLog));
    }

    private static bool TryGetCouponSecurityArguments(
        FormulaFunctionInvocation invocation,
        out CouponSecurityState state,
        out double couponRate,
        out double fourthValue,
        out double redemption,
        out FormulaEvaluationResult error)
    {
        state = default;
        couponRate = default;
        fourthValue = default;
        redemption = default;

        if (!TryGetCouponState(
                invocation,
                frequencyIndex: 5,
                basisIndex: 6,
                out state,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out couponRate,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[3],
                out fourthValue,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[4],
                out redemption,
                out error))
        {
            return false;
        }

        error = default!;
        return true;
    }

    private static bool TryGetCouponState(
        FormulaFunctionInvocation invocation,
        int frequencyIndex,
        int basisIndex,
        out CouponSecurityState state,
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
            !TryGetTruncatedInteger(
                invocation.Arguments[frequencyIndex],
                out var frequency,
                out error))
        {
            return false;
        }

        var basis = 0;
        if (invocation.Arguments.Count > basisIndex &&
            !TryGetTruncatedInteger(
                invocation.Arguments[basisIndex],
                out basis,
                out error))
        {
            return false;
        }
        if (!FinancialDateMath.IsSupportedFrequency(frequency) ||
            !FinancialDateMath.IsSupportedBasis(basis) ||
            !FinancialDateMath.TryGetCouponPeriod(
                settlement,
                maturity,
                frequency,
                out var period))
        {
            error = NumericError();
            return false;
        }

        var dayCountBasis = (FinancialDayCountBasis)basis;
        var daysInCouponPeriod = FinancialDateMath.GetCouponDays(
            period,
            frequency,
            dayCountBasis);
        var daysBeforeSettlement =
            FinancialDateMath.GetCouponDaysBeforeSettlement(
                period,
                settlement,
                dayCountBasis);
        var daysToNextCoupon =
            FinancialDateMath.GetCouponDaysAfterSettlement(
                period,
                settlement,
                frequency,
                dayCountBasis);
        if (!double.IsFinite(daysInCouponPeriod) ||
            !double.IsFinite(daysBeforeSettlement) ||
            !double.IsFinite(daysToNextCoupon) ||
            daysInCouponPeriod <= 0d ||
            daysBeforeSettlement < 0d ||
            daysToNextCoupon <= 0d ||
            period.RemainingCouponCount <= 0)
        {
            error = NumericError();
            return false;
        }

        state = new CouponSecurityState(
            frequency,
            period.RemainingCouponCount,
            daysBeforeSettlement,
            daysToNextCoupon,
            daysInCouponPeriod);
        error = default!;
        return true;
    }

    private static bool TrySolveYield(
        CouponSecurityState state,
        double couponRate,
        double targetPrice,
        double redemption,
        out double yield)
    {
        var lower = MinimumYieldLogBase;
        var upper = MaximumYieldLogBase;
        if (!TryEvaluatePriceFromLogBase(
                state,
                couponRate,
                redemption,
                lower,
                out var lowerPrice,
                out _) ||
            !TryEvaluatePriceFromLogBase(
                state,
                couponRate,
                redemption,
                upper,
                out var upperPrice,
                out _) ||
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
            if (!TryEvaluatePriceFromLogBase(
                    state,
                    couponRate,
                    redemption,
                    middle,
                    out var middlePrice,
                    out _))
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

    private static bool TryEvaluatePriceFromLogBase(
        CouponSecurityState state,
        double couponRate,
        double redemption,
        double logBase,
        out double cleanPrice,
        out double dirtyPrice)
    {
        cleanPrice = default;
        dirtyPrice = default;
        if (!double.IsFinite(logBase) ||
            couponRate < 0d || redemption <= 0d)
        {
            return false;
        }

        var coupon = 100d * couponRate / state.Frequency;
        var firstPeriodFraction = state.DaysToNextCoupon /
                                  state.DaysInCouponPeriod;
        var sum = 0d;
        var compensation = 0d;
        for (var couponIndex = 0;
             couponIndex < state.RemainingCouponCount;
             couponIndex++)
        {
            var cashFlow = coupon;
            if (couponIndex == state.RemainingCouponCount - 1)
            {
                cashFlow += redemption;
            }
            if (cashFlow == 0d)
            {
                continue;
            }

            var periodExponent = couponIndex + firstPeriodFraction;
            var discountExponent = -periodExponent * logBase;
            if (discountExponent > MaximumExpArgument)
            {
                cleanPrice = double.PositiveInfinity;
                dirtyPrice = double.PositiveInfinity;
                return true;
            }
            var discountFactor = discountExponent < MinimumExpArgument
                ? 0d
                : Math.Exp(discountExponent);
            var term = cashFlow * discountFactor;
            if (!double.IsFinite(term))
            {
                return false;
            }
            AddCompensated(ref sum, ref compensation, term);
        }

        var accruedInterest = coupon *
                              state.DaysBeforeSettlement /
                              state.DaysInCouponPeriod;
        dirtyPrice = sum;
        cleanPrice = sum - accruedInterest;
        return double.IsFinite(cleanPrice) &&
               double.IsFinite(dirtyPrice);
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
        return double.IsFinite(yield) && yield > -frequency;
    }

    private static bool TryGetMirrValue(
        CellValue value,
        bool isScalar,
        out double number,
        out bool participates)
    {
        if (!isScalar)
        {
            if (value.Kind == CellValueKind.Number)
            {
                number = (double)value.RawValue!;
                participates = double.IsFinite(number);
                return participates;
            }
            if (value.Kind == CellValueKind.DateTime)
            {
                number = ((DateTime)value.RawValue!).ToOADate();
                participates = double.IsFinite(number);
                return participates;
            }

            number = 0d;
            participates = false;
            return true;
        }

        if (!FormulaValueCoercion.TryNumber(
                value,
                out number,
                allowText: true) ||
            !double.IsFinite(number))
        {
            participates = false;
            return false;
        }
        participates = true;
        return true;
    }

    private static bool TryGetLogCashFlowAggregate(
        IReadOnlyList<IndexedCashFlow> cashFlows,
        bool selectPositive,
        int totalPeriods,
        double baseLog,
        out double aggregateLog)
    {
        var maximumTermLog = double.NegativeInfinity;
        foreach (var cashFlow in cashFlows)
        {
            if ((cashFlow.Value > 0d) != selectPositive)
            {
                continue;
            }

            var exponent = selectPositive
                ? totalPeriods - 1d - cashFlow.Index
                : -cashFlow.Index;
            var termLog = Math.Log(Math.Abs(cashFlow.Value)) +
                          (exponent * baseLog);
            if (!double.IsFinite(termLog))
            {
                aggregateLog = default;
                return false;
            }
            maximumTermLog = Math.Max(maximumTermLog, termLog);
        }
        if (!double.IsFinite(maximumTermLog))
        {
            aggregateLog = default;
            return false;
        }

        var scaledSum = 0d;
        var compensation = 0d;
        foreach (var cashFlow in cashFlows)
        {
            if ((cashFlow.Value > 0d) != selectPositive)
            {
                continue;
            }

            var exponent = selectPositive
                ? totalPeriods - 1d - cashFlow.Index
                : -cashFlow.Index;
            var termLog = Math.Log(Math.Abs(cashFlow.Value)) +
                          (exponent * baseLog);
            AddCompensated(
                ref scaledSum,
                ref compensation,
                Math.Exp(termLog - maximumTermLog));
        }
        if (!double.IsFinite(scaledSum) || scaledSum <= 0d)
        {
            aggregateLog = default;
            return false;
        }

        aggregateLog = maximumTermLog + Math.Log(scaledSum);
        return double.IsFinite(aggregateLog);
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
        out double number,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryNumber(
                argument.ScalarValue,
                out number,
                allowText: true) ||
            !double.IsFinite(number))
        {
            number = default;
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

    private static bool TryLogOnePlus(double value, out double result)
    {
        if (!double.IsFinite(value) || value <= -1d)
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

    private readonly record struct CouponSecurityState(
        int Frequency,
        int RemainingCouponCount,
        double DaysBeforeSettlement,
        double DaysToNextCoupon,
        double DaysInCouponPeriod);

    private readonly record struct IndexedCashFlow(
        int Index,
        double Value);
}
