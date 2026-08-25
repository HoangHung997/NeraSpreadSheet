using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Second five-function financial milestone: remaining simple maturity
/// securities, periodic accrued interest and variable-rate future value.
/// </summary>
internal static class AdvancedMaturitySecurityFormulaFunctions
{
    public const int MaximumScheduleValues = 2_000_000;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition(
            "YIELDDISC",
            4,
            5,
            EvaluateDiscountedSecurityYield);
        yield return CreateDefinition(
            "PRICEMAT",
            5,
            6,
            EvaluateMaturityInterestPrice);
        yield return CreateDefinition(
            "YIELDMAT",
            5,
            6,
            EvaluateMaturityInterestYield);
        yield return CreateDefinition(
            "ACCRINT",
            6,
            8,
            EvaluatePeriodicAccruedInterest);
        yield return CreateDefinition(
            "FVSCHEDULE",
            2,
            2,
            EvaluateFutureValueSchedule,
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

    private static FormulaEvaluationResult EvaluateDiscountedSecurityYield(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetMaturityArguments(
                invocation,
                out var settlement,
                out var maturity,
                out var price,
                out var redemption,
                out var basis,
                out var error))
        {
            return error;
        }
        if (price <= 0d || redemption <= 0d)
        {
            return NumericError();
        }

        var fraction = FinancialDateMath.GetYearFraction(
            settlement,
            maturity,
            (FinancialDayCountBasis)basis);
        return Divide(
            redemption - price,
            price * fraction);
    }

    private static FormulaEvaluationResult EvaluateMaturityInterestPrice(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetMaturityInterestArguments(
                invocation,
                out var settlement,
                out var maturity,
                out var issue,
                out var rate,
                out var yield,
                out var basis,
                out var error))
        {
            return error;
        }
        if (rate < 0d || yield < 0d)
        {
            return NumericError();
        }

        if (!TryGetMaturityInterestComponents(
                issue,
                settlement,
                maturity,
                rate,
                basis,
                out var maturityValue,
                out var accruedInterest,
                out var settlementToMaturity))
        {
            return NumericError();
        }

        var denominator = 1d + (yield * settlementToMaturity);
        if (!double.IsFinite(denominator) || denominator <= 0d)
        {
            return NumericError();
        }
        return Number(
            (maturityValue / denominator) - accruedInterest);
    }

    private static FormulaEvaluationResult EvaluateMaturityInterestYield(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetMaturityInterestArguments(
                invocation,
                out var settlement,
                out var maturity,
                out var issue,
                out var rate,
                out var price,
                out var basis,
                out var error))
        {
            return error;
        }
        if (rate < 0d || price <= 0d)
        {
            return NumericError();
        }

        if (!TryGetMaturityInterestComponents(
                issue,
                settlement,
                maturity,
                rate,
                basis,
                out var maturityValue,
                out var accruedInterest,
                out var settlementToMaturity))
        {
            return NumericError();
        }

        var adjustedPrice = price + accruedInterest;
        if (!double.IsFinite(adjustedPrice) || adjustedPrice <= 0d ||
            settlementToMaturity <= 0d)
        {
            return NumericError();
        }
        return Number(
            ((maturityValue / adjustedPrice) - 1d) /
            settlementToMaturity);
    }

    private static FormulaEvaluationResult EvaluatePeriodicAccruedInterest(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarDate(
                invocation.Arguments[0],
                out var issue,
                out var error) ||
            !TryGetScalarDate(
                invocation.Arguments[1],
                out var firstInterest,
                out error) ||
            !TryGetScalarDate(
                invocation.Arguments[2],
                out var settlement,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[3],
                out var rate,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[4],
                out var par,
                out error) ||
            !TryGetFrequency(
                invocation.Arguments[5],
                out var frequency,
                out error))
        {
            return error;
        }

        var basis = 0;
        if (invocation.Arguments.Count >= 7 &&
            !TryGetBasis(
                invocation.Arguments[6],
                out basis,
                out error))
        {
            return error;
        }

        var calculateFromIssue = true;
        if (invocation.Arguments.Count == 8 &&
            !TryGetScalarBoolean(
                invocation.Arguments[7],
                out calculateFromIssue,
                out error))
        {
            return error;
        }

        if (issue >= settlement ||
            issue >= firstInterest ||
            rate <= 0d ||
            par <= 0d)
        {
            return NumericError();
        }

        var accrualStart =
            !calculateFromIssue && settlement > firstInterest
                ? firstInterest
                : issue;
        if (!TryCalculateAccruedCouponFraction(
                accrualStart,
                settlement,
                firstInterest,
                frequency,
                (FinancialDayCountBasis)basis,
                out var fraction))
        {
            return NumericError();
        }

        return Number(par * rate / frequency * fraction);
    }

    private static FormulaEvaluationResult EvaluateFutureValueSchedule(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var value,
                out var error))
        {
            return error;
        }

        var schedule = invocation.Arguments[1];
        if (schedule.Values.Count > MaximumScheduleValues)
        {
            return NumericError();
        }

        foreach (var rateValue in schedule.Values)
        {
            double rate;
            switch (rateValue.Kind)
            {
                case CellValueKind.Blank:
                    rate = 0d;
                    break;
                case CellValueKind.Number:
                    rate = (double)rateValue.RawValue!;
                    break;
                default:
                    return InvalidValue();
            }

            value *= 1d + rate;
            if (!double.IsFinite(value))
            {
                return NumericError();
            }
        }

        return Number(value);
    }

    private static bool TryGetMaturityArguments(
        FormulaFunctionInvocation invocation,
        out DateTime settlement,
        out DateTime maturity,
        out double firstValue,
        out double secondValue,
        out int basis,
        out FormulaEvaluationResult error)
    {
        settlement = default;
        maturity = default;
        firstValue = default;
        secondValue = default;
        basis = 0;

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
                out firstValue,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[3],
                out secondValue,
                out error))
        {
            return false;
        }
        if (invocation.Arguments.Count == 5 &&
            !TryGetBasis(
                invocation.Arguments[4],
                out basis,
                out error))
        {
            return false;
        }
        if (settlement >= maturity)
        {
            error = NumericError();
            return false;
        }

        error = default!;
        return true;
    }

    private static bool TryGetMaturityInterestArguments(
        FormulaFunctionInvocation invocation,
        out DateTime settlement,
        out DateTime maturity,
        out DateTime issue,
        out double rate,
        out double finalValue,
        out int basis,
        out FormulaEvaluationResult error)
    {
        settlement = default;
        maturity = default;
        issue = default;
        rate = default;
        finalValue = default;
        basis = 0;

        if (!TryGetScalarDate(
                invocation.Arguments[0],
                out settlement,
                out error) ||
            !TryGetScalarDate(
                invocation.Arguments[1],
                out maturity,
                out error) ||
            !TryGetScalarDate(
                invocation.Arguments[2],
                out issue,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[3],
                out rate,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[4],
                out finalValue,
                out error))
        {
            return false;
        }
        if (invocation.Arguments.Count == 6 &&
            !TryGetBasis(
                invocation.Arguments[5],
                out basis,
                out error))
        {
            return false;
        }
        if (settlement >= maturity || issue >= maturity)
        {
            error = NumericError();
            return false;
        }

        error = default!;
        return true;
    }

    private static bool TryGetMaturityInterestComponents(
        DateTime issue,
        DateTime settlement,
        DateTime maturity,
        double rate,
        int basis,
        out double maturityValue,
        out double accruedInterest,
        out double settlementToMaturity)
    {
        var dayCountBasis = (FinancialDayCountBasis)basis;
        var issueToMaturity = FinancialDateMath.GetYearFraction(
            issue,
            maturity,
            dayCountBasis);
        var issueToSettlement = FinancialDateMath.GetYearFraction(
            issue,
            settlement,
            dayCountBasis);
        settlementToMaturity = FinancialDateMath.GetYearFraction(
            settlement,
            maturity,
            dayCountBasis);
        maturityValue = 100d * (1d + (rate * issueToMaturity));
        accruedInterest = 100d * rate * issueToSettlement;
        return double.IsFinite(maturityValue) &&
               double.IsFinite(accruedInterest) &&
               double.IsFinite(settlementToMaturity) &&
               settlementToMaturity > 0d;
    }

    private static bool TryCalculateAccruedCouponFraction(
        DateTime start,
        DateTime settlement,
        DateTime firstInterest,
        int frequency,
        FinancialDayCountBasis basis,
        out double result)
    {
        start = start.Date;
        settlement = settlement.Date;
        firstInterest = firstInterest.Date;
        if (start >= settlement ||
            !TryGetAnchoredCouponPeriod(
                start,
                firstInterest,
                frequency,
                out var previousCoupon,
                out var nextCoupon,
                out var nextCouponIndex))
        {
            result = start == settlement ? 0d : default;
            return start == settlement;
        }

        var sum = 0d;
        var compensation = 0d;
        var segmentStart = start;
        var monthsPerCoupon = 12 / frequency;
        for (var periodIndex = 0;
             periodIndex < FinancialDateMath.MaximumCouponPeriods;
             periodIndex++)
        {
            var segmentEnd = settlement < nextCoupon
                ? settlement
                : nextCoupon;
            var period = new FinancialCouponPeriod(
                previousCoupon,
                nextCoupon,
                0);
            var normalDays = FinancialDateMath.GetCouponDays(
                period,
                frequency,
                basis);
            var accruedDays = FinancialDateMath.GetDayCount(
                segmentStart,
                segmentEnd,
                basis);
            if (!double.IsFinite(normalDays) || normalDays <= 0d ||
                !double.IsFinite(accruedDays) || accruedDays < 0d)
            {
                result = default;
                return false;
            }

            AddCompensated(
                ref sum,
                ref compensation,
                accruedDays / normalDays);
            if (segmentEnd >= settlement)
            {
                result = sum;
                return double.IsFinite(result);
            }

            previousCoupon = nextCoupon;
            nextCouponIndex++;
            if (!TryAddAnchoredCouponMonths(
                    firstInterest,
                    (long)nextCouponIndex * monthsPerCoupon,
                    out nextCoupon))
            {
                result = default;
                return false;
            }
            segmentStart = previousCoupon;
        }

        result = default;
        return false;
    }

    private static bool TryGetAnchoredCouponPeriod(
        DateTime date,
        DateTime anchor,
        int frequency,
        out DateTime previousCoupon,
        out DateTime nextCoupon,
        out int nextCouponIndex)
    {
        var monthsPerCoupon = 12 / frequency;
        if (date < anchor)
        {
            nextCoupon = anchor;
            for (var step = 1;
                 step <= FinancialDateMath.MaximumCouponPeriods;
                 step++)
            {
                if (!TryAddAnchoredCouponMonths(
                        anchor,
                        -(long)step * monthsPerCoupon,
                        out previousCoupon))
                {
                    nextCouponIndex = default;
                    return false;
                }
                if (previousCoupon <= date)
                {
                    nextCouponIndex = -(step - 1);
                    return true;
                }
                nextCoupon = previousCoupon;
            }
        }
        else
        {
            previousCoupon = anchor;
            for (var step = 1;
                 step <= FinancialDateMath.MaximumCouponPeriods;
                 step++)
            {
                if (!TryAddAnchoredCouponMonths(
                        anchor,
                        (long)step * monthsPerCoupon,
                        out nextCoupon))
                {
                    nextCouponIndex = default;
                    return false;
                }
                if (nextCoupon > date)
                {
                    nextCouponIndex = step;
                    return true;
                }
                previousCoupon = nextCoupon;
            }
        }

        previousCoupon = default;
        nextCoupon = default;
        nextCouponIndex = default;
        return false;
    }

    private static bool TryAddAnchoredCouponMonths(
        DateTime anchor,
        long monthOffset,
        out DateTime result)
    {
        anchor = anchor.Date;
        var anchorMonthIndex =
            (((long)anchor.Year - 1L) * 12L) +
            anchor.Month - 1L;
        var targetMonthIndex = anchorMonthIndex + monthOffset;
        const long maximumMonthIndex = (9999L * 12L) - 1L;
        if (targetMonthIndex < 0L ||
            targetMonthIndex > maximumMonthIndex)
        {
            result = default;
            return false;
        }

        var year = checked((int)(targetMonthIndex / 12L) + 1);
        var month = checked((int)(targetMonthIndex % 12L) + 1);
        var targetMonthDays = DateTime.DaysInMonth(year, month);
        var anchorIsEndOfMonth =
            anchor.Day == DateTime.DaysInMonth(
                anchor.Year,
                anchor.Month);
        var day = anchorIsEndOfMonth
            ? targetMonthDays
            : Math.Min(anchor.Day, targetMonthDays);
        result = new DateTime(year, month, day);
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

    private static bool TryGetScalarBoolean(
        FormulaFunctionArgument argument,
        out bool value,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryBoolean(
                argument.ScalarValue,
                out value,
                allowText: true))
        {
            value = default;
            error = InvalidValue();
            return false;
        }
        error = default!;
        return true;
    }

    private static bool TryGetFrequency(
        FormulaFunctionArgument argument,
        out int frequency,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarNumber(argument, out var number, out error))
        {
            frequency = default;
            return false;
        }
        var truncated = Math.Truncate(number);
        if (truncated < int.MinValue || truncated > int.MaxValue)
        {
            frequency = default;
            error = NumericError();
            return false;
        }
        frequency = checked((int)truncated);
        if (!FinancialDateMath.IsSupportedFrequency(frequency))
        {
            error = NumericError();
            return false;
        }
        error = default!;
        return true;
    }

    private static bool TryGetBasis(
        FormulaFunctionArgument argument,
        out int basis,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarNumber(argument, out var number, out error))
        {
            basis = default;
            return false;
        }
        var truncated = Math.Truncate(number);
        if (truncated < int.MinValue || truncated > int.MaxValue)
        {
            basis = default;
            error = NumericError();
            return false;
        }
        basis = checked((int)truncated);
        if (!FinancialDateMath.IsSupportedBasis(basis))
        {
            error = NumericError();
            return false;
        }
        error = default!;
        return true;
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

    private static FormulaEvaluationResult Divide(
        double numerator,
        double denominator)
    {
        if (!double.IsFinite(denominator) || denominator == 0d)
        {
            return NumericError();
        }
        return Number(numerator / denominator);
    }

    private static FormulaEvaluationResult Number(double value) =>
        double.IsFinite(value)
            ? FormulaEvaluationResult.Success(CellValue.FromNumber(value))
            : NumericError();

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());
}
