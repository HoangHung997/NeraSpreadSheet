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
/// helpers and the later bond-price/yield families.
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

    private static bool TryAddCouponMonths(
        DateTime maturity,
        long monthOffset,
        out DateTime result)
    {
        maturity = maturity.Date;
        var maturityMonthIndex =
            (((long)maturity.Year - 1L) * 12L) +
            maturity.Month - 1L;
        var targetMonthIndex = maturityMonthIndex + monthOffset;
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
        var maturityIsEndOfMonth =
            maturity.Day == DateTime.DaysInMonth(
                maturity.Year,
                maturity.Month);
        var day = maturityIsEndOfMonth
            ? targetMonthDays
            : Math.Min(maturity.Day, targetMonthDays);
        result = new DateTime(year, month, day);
        return true;
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

    private static bool IsLastDayOfFebruary(DateTime date) =>
        date.Month == 2 &&
        date.Day == DateTime.DaysInMonth(date.Year, 2);
}
