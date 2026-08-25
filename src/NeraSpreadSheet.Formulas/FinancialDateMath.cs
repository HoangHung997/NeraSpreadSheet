namespace NeraSpreadSheet.Formulas;

internal enum FinancialDayCountBasis
{
    UsNasd30Over360 = 0,
    ActualOverActual = 1,
    ActualOver360 = 2,
    ActualOver365 = 3,
    European30Over360 = 4,
}

internal readonly record struct FinancialCouponPeriod(
    DateTime PreviousCoupon,
    DateTime NextCoupon,
    int RemainingCouponCount);

/// <summary>
/// Platform-neutral financial date arithmetic shared by YEARFRAC, coupon
/// helpers and security price/yield families.
/// </summary>
internal static class FinancialDateMath
{
    public const int MaximumCouponPeriods = 100_000;

    public static bool IsSupportedBasis(int basis) =>
        basis is >= 0 and <= 4;

    public static bool IsSupportedFrequency(int frequency) =>
        frequency is 1 or 2 or 4;

    public static bool TryGetCouponPeriod(
        DateTime settlement,
        DateTime maturity,
        int frequency,
        out FinancialCouponPeriod period)
    {
        settlement = settlement.Date;
        maturity = maturity.Date;
        if (settlement >= maturity ||
            !IsSupportedFrequency(frequency))
        {
            period = default;
            return false;
        }

        var monthsPerCoupon = 12 / frequency;
        var nextCoupon = maturity;
        for (var couponIndex = 0;
             couponIndex <= MaximumCouponPeriods;
             couponIndex++)
        {
            var monthOffset =
                -((long)couponIndex * monthsPerCoupon);
            if (!TryAddCouponMonths(
                    maturity,
                    monthOffset,
                    out var candidate))
            {
                period = default;
                return false;
            }

            if (candidate <= settlement)
            {
                if (couponIndex == 0)
                {
                    period = default;
                    return false;
                }

                period = new FinancialCouponPeriod(
                    candidate,
                    nextCoupon,
                    couponIndex);
                return true;
            }

            nextCoupon = candidate;
        }

        period = default;
        return false;
    }

    public static bool TryGetCouponPeriodRatio(
        DateTime startDate,
        DateTime endDate,
        DateTime anchorDate,
        int frequency,
        FinancialDayCountBasis basis,
        out double ratio)
    {
        startDate = startDate.Date;
        endDate = endDate.Date;
        anchorDate = anchorDate.Date;
        if (!IsSupportedFrequency(frequency) ||
            !IsSupportedBasis((int)basis))
        {
            ratio = default;
            return false;
        }
        if (startDate == endDate)
        {
            ratio = 0d;
            return true;
        }

        var sign = 1d;
        if (startDate > endDate)
        {
            (startDate, endDate) = (endDate, startDate);
            sign = -1d;
        }

        if (!TryFindCouponPeriod(
                startDate,
                anchorDate,
                frequency,
                out var previousCoupon,
                out var nextCoupon,
                out var nextCouponIndex))
        {
            ratio = default;
            return false;
        }

        var monthsPerCoupon = 12 / frequency;
        var cursor = startDate;
        var sum = 0d;
        var compensation = 0d;
        for (var segmentIndex = 0;
             segmentIndex <= MaximumCouponPeriods;
             segmentIndex++)
        {
            var segmentEnd = endDate < nextCoupon
                ? endDate
                : nextCoupon;
            var couponDays = GetCouponDays(
                new FinancialCouponPeriod(
                    previousCoupon,
                    nextCoupon,
                    0),
                frequency,
                basis);
            var segmentDays = GetDayCount(
                cursor,
                segmentEnd,
                basis);
            if (!double.IsFinite(couponDays) ||
                couponDays <= 0d ||
                !double.IsFinite(segmentDays) ||
                segmentDays < 0d)
            {
                ratio = default;
                return false;
            }

            AddCompensated(
                ref sum,
                ref compensation,
                segmentDays / couponDays);
            if (segmentEnd == endDate)
            {
                ratio = sign * sum;
                return double.IsFinite(ratio);
            }

            cursor = nextCoupon;
            previousCoupon = nextCoupon;
            nextCouponIndex++;
            if (!TryAddCouponMonths(
                    anchorDate,
                    nextCouponIndex * monthsPerCoupon,
                    out nextCoupon) ||
                nextCoupon <= previousCoupon)
            {
                ratio = default;
                return false;
            }
        }

        ratio = default;
        return false;
    }

    public static double GetYearFraction(
        DateTime startDate,
        DateTime endDate,
        FinancialDayCountBasis basis)
    {
        startDate = startDate.Date;
        endDate = endDate.Date;
        if (startDate == endDate)
        {
            return 0d;
        }

        var sign = 1d;
        if (startDate > endDate)
        {
            (startDate, endDate) = (endDate, startDate);
            sign = -1d;
        }

        var dayCount = GetDayCountOrdered(
            startDate,
            endDate,
            basis);
        var daysInYear = basis switch
        {
            FinancialDayCountBasis.UsNasd30Over360 => 360d,
            FinancialDayCountBasis.ActualOverActual =>
                GetActualActualDenominator(startDate, endDate),
            FinancialDayCountBasis.ActualOver360 => 360d,
            FinancialDayCountBasis.ActualOver365 => 365d,
            FinancialDayCountBasis.European30Over360 => 360d,
            _ => double.NaN,
        };

        return sign * dayCount / daysInYear;
    }

    public static double GetDayCount(
        DateTime startDate,
        DateTime endDate,
        FinancialDayCountBasis basis)
    {
        startDate = startDate.Date;
        endDate = endDate.Date;
        if (startDate == endDate)
        {
            return 0d;
        }

        var sign = 1d;
        if (startDate > endDate)
        {
            (startDate, endDate) = (endDate, startDate);
            sign = -1d;
        }

        return sign * GetDayCountOrdered(
            startDate,
            endDate,
            basis);
    }

    public static double GetCouponDays(
        FinancialCouponPeriod period,
        int frequency,
        FinancialDayCountBasis basis) =>
        basis switch
        {
            FinancialDayCountBasis.ActualOverActual =>
                (period.NextCoupon - period.PreviousCoupon).TotalDays,
            FinancialDayCountBasis.ActualOver365 =>
                365d / frequency,
            _ => 360d / frequency,
        };

    public static double GetCouponDaysBeforeSettlement(
        FinancialCouponPeriod period,
        DateTime settlement,
        FinancialDayCountBasis basis) =>
        GetDayCount(
            period.PreviousCoupon,
            settlement.Date,
            basis);

    public static double GetCouponDaysAfterSettlement(
        FinancialCouponPeriod period,
        DateTime settlement,
        int frequency,
        FinancialDayCountBasis basis)
    {
        settlement = settlement.Date;
        if (basis is FinancialDayCountBasis.UsNasd30Over360 or
            FinancialDayCountBasis.European30Over360)
        {
            return GetCouponDays(period, frequency, basis) -
                   GetCouponDaysBeforeSettlement(
                       period,
                       settlement,
                       basis);
        }

        return GetDayCount(
            settlement,
            period.NextCoupon,
            basis);
    }

    public static bool TryAddCouponMonths(
        DateTime anchorDate,
        long monthOffset,
        out DateTime result)
    {
        anchorDate = anchorDate.Date;
        var anchorMonthIndex =
            (((long)anchorDate.Year - 1L) * 12L) +
            anchorDate.Month - 1L;
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
            anchorDate.Day == DateTime.DaysInMonth(
                anchorDate.Year,
                anchorDate.Month);
        var day = anchorIsEndOfMonth
            ? targetMonthDays
            : Math.Min(anchorDate.Day, targetMonthDays);
        result = new DateTime(year, month, day);
        return true;
    }

    private static bool TryFindCouponPeriod(
        DateTime date,
        DateTime anchorDate,
        int frequency,
        out DateTime previousCoupon,
        out DateTime nextCoupon,
        out long nextCouponIndex)
    {
        var monthsPerCoupon = 12 / frequency;
        if (date < anchorDate)
        {
            nextCoupon = anchorDate;
            nextCouponIndex = 0L;
            for (long couponIndex = 1;
                 couponIndex <= MaximumCouponPeriods;
                 couponIndex++)
            {
                var previousIndex = -couponIndex;
                if (!TryAddCouponMonths(
                        anchorDate,
                        previousIndex * monthsPerCoupon,
                        out var candidate))
                {
                    previousCoupon = default;
                    nextCoupon = default;
                    nextCouponIndex = default;
                    return false;
                }

                if (candidate <= date)
                {
                    previousCoupon = candidate;
                    return true;
                }

                nextCoupon = candidate;
                nextCouponIndex = previousIndex;
            }
        }
        else
        {
            previousCoupon = anchorDate;
            for (long couponIndex = 1;
                 couponIndex <= MaximumCouponPeriods;
                 couponIndex++)
            {
                if (!TryAddCouponMonths(
                        anchorDate,
                        couponIndex * monthsPerCoupon,
                        out var candidate))
                {
                    previousCoupon = default;
                    nextCoupon = default;
                    nextCouponIndex = default;
                    return false;
                }

                if (candidate > date)
                {
                    nextCoupon = candidate;
                    nextCouponIndex = couponIndex;
                    return true;
                }

                previousCoupon = candidate;
            }
        }

        previousCoupon = default;
        nextCoupon = default;
        nextCouponIndex = default;
        return false;
    }

    private static double GetDayCountOrdered(
        DateTime startDate,
        DateTime endDate,
        FinancialDayCountBasis basis) =>
        basis switch
        {
            FinancialDayCountBasis.UsNasd30Over360 =>
                GetUsNasd30Over360Days(startDate, endDate),
            FinancialDayCountBasis.ActualOverActual or
            FinancialDayCountBasis.ActualOver360 or
            FinancialDayCountBasis.ActualOver365 =>
                (endDate - startDate).TotalDays,
            FinancialDayCountBasis.European30Over360 =>
                GetEuropean30Over360Days(startDate, endDate),
            _ => double.NaN,
        };

    private static double GetUsNasd30Over360Days(
        DateTime startDate,
        DateTime endDate)
    {
        var startDay = startDate.Day;
        var endDay = endDate.Day;
        if (startDay == 31)
        {
            startDay = 30;
        }

        if (startDay == 30 && endDay == 31)
        {
            endDay = 30;
        }
        else if (IsLastDayOfFebruary(startDate))
        {
            startDay = 30;
            if (IsLastDayOfFebruary(endDate))
            {
                endDay = 30;
            }
        }

        return ((endDate.Year - startDate.Year) * 360d) +
               ((endDate.Month - startDate.Month) * 30d) +
               endDay - startDay;
    }

    private static double GetEuropean30Over360Days(
        DateTime startDate,
        DateTime endDate)
    {
        var startDay = Math.Min(startDate.Day, 30);
        var endDay = Math.Min(endDate.Day, 30);
        return ((endDate.Year - startDate.Year) * 360d) +
               ((endDate.Month - startDate.Month) * 30d) +
               endDay - startDay;
    }

    private static double GetActualActualDenominator(
        DateTime startDate,
        DateTime endDate)
    {
        var yearsDiffer = startDate.Year != endDate.Year;
        var spansMoreThanOneYear =
            yearsDiffer &&
            (endDate.Year != startDate.Year + 1 ||
             startDate.Month < endDate.Month ||
             (startDate.Month == endDate.Month &&
              startDate.Day < endDate.Day));
        if (spansMoreThanOneYear)
        {
            var totalDays = 0d;
            for (var year = startDate.Year;
                 year <= endDate.Year;
                 year++)
            {
                totalDays += DateTime.IsLeapYear(year)
                    ? 366d
                    : 365d;
            }

            return totalDays /
                   (endDate.Year - startDate.Year + 1d);
        }

        if (!yearsDiffer &&
            DateTime.IsLeapYear(startDate.Year))
        {
            return 366d;
        }

        var includesLeapDay =
            DateTime.IsLeapYear(startDate.Year) &&
            (startDate.Month < 2 ||
             (startDate.Month == 2 && startDate.Day <= 29));
        if (yearsDiffer && DateTime.IsLeapYear(endDate.Year))
        {
            includesLeapDay |=
                endDate.Month > 2 ||
                (endDate.Month == 2 && endDate.Day == 29);
        }

        return includesLeapDay ? 366d : 365d;
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

    private static bool IsLastDayOfFebruary(DateTime date) =>
        date.Month == 2 &&
        date.Day == DateTime.DaysInMonth(date.Year, 2);
}
