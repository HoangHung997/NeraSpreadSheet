namespace NeraSpreadSheet.Formulas;

internal readonly record struct BusinessWeekendMask(byte Bits)
{
    public static BusinessWeekendMask SaturdaySunday { get; } =
        new((byte)((1 << 5) | (1 << 6)));

    public int WeekendCount
    {
        get
        {
            var count = 0;
            var bits = Bits;
            for (var index = 0; index < 7; index++)
            {
                count += bits & 1;
                bits >>= 1;
            }

            return count;
        }
    }

    public int WorkdayCount => 7 - WeekendCount;

    public bool IsWeekend(DateTime date)
    {
        var mondayBasedIndex =
            (((int)date.DayOfWeek) + 6) % 7;
        return (Bits & (1 << mondayBasedIndex)) != 0;
    }

    public static bool TryFromCode(
        int code,
        out BusinessWeekendMask mask)
    {
        if (code is >= 1 and <= 7)
        {
            var first = (code + 4) % 7;
            var second = (first + 1) % 7;
            mask = new BusinessWeekendMask(
                (byte)((1 << first) | (1 << second)));
            return true;
        }

        if (code is >= 11 and <= 17)
        {
            var day = (code + 2) % 7;
            mask = new BusinessWeekendMask((byte)(1 << day));
            return true;
        }

        mask = default;
        return false;
    }

    public static bool TryFromString(
        string value,
        out BusinessWeekendMask mask)
    {
        if (value.Length != 7)
        {
            mask = default;
            return false;
        }

        byte bits = 0;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '0':
                    break;
                case '1':
                    bits |= (byte)(1 << index);
                    break;
                default:
                    mask = default;
                    return false;
            }
        }

        mask = new BusinessWeekendMask(bits);
        return true;
    }
}

/// <summary>
/// Platform-neutral business-day arithmetic shared by NETWORKDAYS and WORKDAY
/// families. Counting is week-based and shifting uses a bounded binary search,
/// so large date spans do not require one iteration per calendar day.
/// </summary>
internal static class BusinessDayCalendarMath
{
    public const int MaximumHolidayValues = 2_000_000;

    private const long TicksPerDay = TimeSpan.TicksPerDay;
    private static readonly long MinimumDayNumber =
        GetDayNumber(DateTime.MinValue);
    private static readonly long MaximumDayNumber =
        GetDayNumber(DateTime.MaxValue);

    public static long GetDayNumber(DateTime date) =>
        date.Date.Ticks / TicksPerDay;

    public static long CountBusinessDaysInclusive(
        DateTime startDate,
        DateTime endDate,
        BusinessWeekendMask weekendMask,
        IReadOnlyList<long> sortedWorkdayHolidays)
    {
        startDate = startDate.Date;
        endDate = endDate.Date;
        if (startDate > endDate)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startDate),
                "The business-day counting interval must be ordered.");
        }

        var startDay = GetDayNumber(startDate);
        var endDay = GetDayNumber(endDate);
        var totalDays = endDay - startDay + 1L;
        var fullWeeks = totalDays / 7L;
        var remainder = checked((int)(totalDays % 7L));
        var workdays =
            fullWeeks * weekendMask.WorkdayCount;

        for (var offset = 0; offset < remainder; offset++)
        {
            var day = startDate.AddDays(offset);
            if (!weekendMask.IsWeekend(day))
            {
                workdays++;
            }
        }

        workdays -= CountSortedValuesInRange(
            sortedWorkdayHolidays,
            startDay,
            endDay);
        return workdays;
    }

    public static bool TryShiftBusinessDays(
        DateTime startDate,
        long dayOffset,
        BusinessWeekendMask weekendMask,
        IReadOnlyList<long> sortedWorkdayHolidays,
        out DateTime result)
    {
        startDate = startDate.Date;
        if (dayOffset == 0L)
        {
            result = startDate;
            return true;
        }
        if (weekendMask.WorkdayCount <= 0)
        {
            result = default;
            return false;
        }

        var direction = dayOffset > 0L ? 1 : -1;
        var target = Math.Abs(dayOffset);
        var startDay = GetDayNumber(startDate);
        var maximumDistance = direction > 0
            ? MaximumDayNumber - startDay
            : startDay - MinimumDayNumber;
        if (maximumDistance <= 0L)
        {
            result = default;
            return false;
        }

        var approximateWeeks =
            (target + weekendMask.WorkdayCount - 1L) /
            weekendMask.WorkdayCount;
        var estimatedDistance = SaturatingAdd(
            SaturatingMultiply(approximateWeeks, 7L),
            sortedWorkdayHolidays.Count + 7L);
        var upper = Math.Min(
            maximumDistance,
            Math.Max(1L, estimatedDistance));

        while (CountBusinessDaysInDirection(
                   startDay,
                   upper,
                   direction,
                   weekendMask,
                   sortedWorkdayHolidays) < target)
        {
            if (upper >= maximumDistance)
            {
                result = default;
                return false;
            }

            upper = Math.Min(
                maximumDistance,
                Math.Max(upper + 1L, SaturatingMultiply(upper, 2L)));
        }

        var lower = 1L;
        while (lower < upper)
        {
            var middle = lower + ((upper - lower) / 2L);
            var count = CountBusinessDaysInDirection(
                startDay,
                middle,
                direction,
                weekendMask,
                sortedWorkdayHolidays);
            if (count >= target)
            {
                upper = middle;
            }
            else
            {
                lower = middle + 1L;
            }
        }

        var resultDay = startDay + (direction * lower);
        if (resultDay < MinimumDayNumber ||
            resultDay > MaximumDayNumber)
        {
            result = default;
            return false;
        }

        result = FromDayNumber(resultDay);
        return true;
    }

    private static long CountBusinessDaysInDirection(
        long startDay,
        long distance,
        int direction,
        BusinessWeekendMask weekendMask,
        IReadOnlyList<long> sortedWorkdayHolidays)
    {
        if (distance <= 0L)
        {
            return 0L;
        }

        var rangeStart = direction > 0
            ? startDay + 1L
            : startDay - distance;
        var rangeEnd = direction > 0
            ? startDay + distance
            : startDay - 1L;
        return CountBusinessDaysInclusive(
            FromDayNumber(rangeStart),
            FromDayNumber(rangeEnd),
            weekendMask,
            sortedWorkdayHolidays);
    }

    private static long CountSortedValuesInRange(
        IReadOnlyList<long> values,
        long minimum,
        long maximum)
    {
        if (values.Count == 0)
        {
            return 0L;
        }

        var first = LowerBound(values, minimum);
        var afterLast = UpperBound(values, maximum);
        return afterLast - first;
    }

    private static int LowerBound(
        IReadOnlyList<long> values,
        long target)
    {
        var lower = 0;
        var upper = values.Count;
        while (lower < upper)
        {
            var middle = lower + ((upper - lower) / 2);
            if (values[middle] < target)
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle;
            }
        }

        return lower;
    }

    private static int UpperBound(
        IReadOnlyList<long> values,
        long target)
    {
        var lower = 0;
        var upper = values.Count;
        while (lower < upper)
        {
            var middle = lower + ((upper - lower) / 2);
            if (values[middle] <= target)
            {
                lower = middle + 1;
            }
            else
            {
                upper = middle;
            }
        }

        return lower;
    }

    private static DateTime FromDayNumber(long dayNumber) =>
        new(
            checked(dayNumber * TicksPerDay),
            DateTimeKind.Unspecified);

    private static long SaturatingAdd(long left, long right)
    {
        if (right > 0L && left > long.MaxValue - right)
        {
            return long.MaxValue;
        }
        if (right < 0L && left < long.MinValue - right)
        {
            return long.MinValue;
        }

        return left + right;
    }

    private static long SaturatingMultiply(long left, long right)
    {
        if (left <= 0L || right <= 0L)
        {
            return left * right;
        }
        if (left > long.MaxValue / right)
        {
            return long.MaxValue;
        }

        return left * right;
    }
}
