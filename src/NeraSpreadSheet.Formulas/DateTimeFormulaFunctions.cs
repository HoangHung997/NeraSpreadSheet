using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class DateTimeFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return FormulaFunctionFactory.Create(
            "DATE",
            3,
            3,
            static (arguments, _) => Date(arguments));
        yield return FormulaFunctionFactory.Create(
            "TIME",
            3,
            3,
            static (arguments, _) => Time(arguments));
        yield return FormulaFunctionFactory.Create(
            "YEAR",
            1,
            1,
            static (arguments, _) => DatePart(
                arguments[0],
                static value => value.Year));
        yield return FormulaFunctionFactory.Create(
            "MONTH",
            1,
            1,
            static (arguments, _) => DatePart(
                arguments[0],
                static value => value.Month));
        yield return FormulaFunctionFactory.Create(
            "DAY",
            1,
            1,
            static (arguments, _) => DatePart(
                arguments[0],
                static value => value.Day));
        yield return FormulaFunctionFactory.Create(
            "HOUR",
            1,
            1,
            static (arguments, _) => TimePart(
                arguments[0],
                static value => value.Hours));
        yield return FormulaFunctionFactory.Create(
            "MINUTE",
            1,
            1,
            static (arguments, _) => TimePart(
                arguments[0],
                static value => value.Minutes));
        yield return FormulaFunctionFactory.Create(
            "SECOND",
            1,
            1,
            static (arguments, _) => TimePart(
                arguments[0],
                static value => value.Seconds));
        yield return FormulaFunctionFactory.Create(
            "DAYS",
            2,
            2,
            static (arguments, _) => Days(arguments));
        yield return FormulaFunctionFactory.Create(
            "EDATE",
            2,
            2,
            static (arguments, _) => ShiftMonth(
                arguments,
                endOfMonth: false));
        yield return FormulaFunctionFactory.Create(
            "EOMONTH",
            2,
            2,
            static (arguments, _) => ShiftMonth(
                arguments,
                endOfMonth: true));
        yield return FormulaFunctionFactory.Create(
            "WEEKDAY",
            1,
            2,
            static (arguments, _) => Weekday(arguments));
        yield return FormulaFunctionFactory.Create(
            "DATEVALUE",
            1,
            1,
            static (arguments, _) => DateValue(arguments[0]));
        yield return FormulaFunctionFactory.Create(
            "TIMEVALUE",
            1,
            1,
            static (arguments, _) => TimeValue(arguments[0]));
        yield return FormulaFunctionFactory.Create(
            "TODAY",
            0,
            0,
            static (_, context) => CellValue.FromDateTime(
                GetCurrentDateTime(context).Date));
        yield return FormulaFunctionFactory.Create(
            "NOW",
            0,
            0,
            static (_, context) => CellValue.FromDateTime(
                GetCurrentDateTime(context)));
    }

    private static CellValue Date(IReadOnlyList<CellValue> arguments)
    {
        if (!TryInteger(arguments[0], out var year) ||
            !TryInteger(arguments[1], out var month) ||
            !TryInteger(arguments[2], out var day))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (year is >= 0 and <= 1899)
        {
            year += 1900;
        }
        if (year is < 1 or > 9999)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        try
        {
            var result = new DateTime(year, 1, 1)
                .AddMonths(month - 1)
                .AddDays(day - 1d);
            return CellValue.FromDateTime(result);
        }
        catch (ArgumentOutOfRangeException)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }
    }

    private static CellValue Time(IReadOnlyList<CellValue> arguments)
    {
        if (!TryInteger(arguments[0], out var hour) ||
            !TryInteger(arguments[1], out var minute) ||
            !TryInteger(arguments[2], out var second))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        if (hour < 0 || minute < 0 || second < 0)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }

        try
        {
            var totalSeconds = checked(
                ((long)hour * 3600L) +
                ((long)minute * 60L) +
                second);
            var secondsInDay = totalSeconds % 86_400L;
            return CellValue.FromNumber(secondsInDay / 86_400d);
        }
        catch (OverflowException)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }
    }

    private static CellValue DatePart(
        CellValue value,
        Func<DateTime, int> selector)
    {
        return FormulaValueCoercion.TryDateTime(
                value,
                out var dateTime)
            ? CellValue.FromNumber(selector(dateTime))
            : FormulaValueCoercion.Error("#VALUE!");
    }

    private static CellValue TimePart(
        CellValue value,
        Func<TimeSpan, int> selector)
    {
        if (value.Kind == CellValueKind.DateTime)
        {
            return CellValue.FromNumber(selector(
                ((DateTime)value.RawValue!).TimeOfDay));
        }
        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        var fraction = number - Math.Floor(number);
        if (fraction < 0d)
        {
            fraction += 1d;
        }
        var ticks = (long)Math.Round(
            fraction * TimeSpan.TicksPerDay,
            MidpointRounding.AwayFromZero);
        if (ticks >= TimeSpan.TicksPerDay)
        {
            ticks = 0L;
        }
        return CellValue.FromNumber(selector(TimeSpan.FromTicks(ticks)));
    }

    private static CellValue Days(IReadOnlyList<CellValue> arguments)
    {
        if (!FormulaValueCoercion.TryDateTime(
                arguments[0],
                out var endDate) ||
            !FormulaValueCoercion.TryDateTime(
                arguments[1],
                out var startDate))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        return CellValue.FromNumber(
            (endDate.Date - startDate.Date).TotalDays);
    }

    private static CellValue ShiftMonth(
        IReadOnlyList<CellValue> arguments,
        bool endOfMonth)
    {
        if (!FormulaValueCoercion.TryDateTime(
                arguments[0],
                out var startDate) ||
            !TryInteger(arguments[1], out var months))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        try
        {
            var shifted = startDate.AddMonths(months);
            if (endOfMonth)
            {
                shifted = new DateTime(
                    shifted.Year,
                    shifted.Month,
                    DateTime.DaysInMonth(
                        shifted.Year,
                        shifted.Month),
                    shifted.Hour,
                    shifted.Minute,
                    shifted.Second,
                    shifted.Kind);
            }
            return CellValue.FromDateTime(shifted);
        }
        catch (ArgumentOutOfRangeException)
        {
            return FormulaValueCoercion.Error("#NUM!");
        }
    }

    private static CellValue Weekday(IReadOnlyList<CellValue> arguments)
    {
        if (!FormulaValueCoercion.TryDateTime(
                arguments[0],
                out var dateTime))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }
        var returnType = 1;
        if (arguments.Count == 2 &&
            !TryInteger(arguments[1], out returnType))
        {
            return FormulaValueCoercion.Error("#VALUE!");
        }

        var sundayBased = (int)dateTime.DayOfWeek;
        var result = returnType switch
        {
            1 => sundayBased + 1,
            2 => ((sundayBased + 6) % 7) + 1,
            3 => (sundayBased + 6) % 7,
            _ => -1,
        };
        return result < 0
            ? FormulaValueCoercion.Error("#NUM!")
            : CellValue.FromNumber(result);
    }

    private static CellValue DateValue(CellValue value)
    {
        if (value.Kind == CellValueKind.DateTime)
        {
            return CellValue.FromDateTime(
                ((DateTime)value.RawValue!).Date);
        }
        var text = FormulaValueCoercion.ToText(value);
        return DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces |
                DateTimeStyles.RoundtripKind,
                out var dateTime)
            ? CellValue.FromDateTime(dateTime.Date)
            : FormulaValueCoercion.Error("#VALUE!");
    }

    private static CellValue TimeValue(CellValue value)
    {
        if (value.Kind == CellValueKind.DateTime)
        {
            return CellValue.FromNumber(
                ((DateTime)value.RawValue!).TimeOfDay.TotalDays);
        }
        var text = FormulaValueCoercion.ToText(value);
        if (TimeSpan.TryParse(
                text,
                CultureInfo.InvariantCulture,
                out var timeSpan))
        {
            if (timeSpan < TimeSpan.Zero)
            {
                return FormulaValueCoercion.Error("#VALUE!");
            }
            return CellValue.FromNumber(
                timeSpan.TotalDays - Math.Floor(timeSpan.TotalDays));
        }
        if (DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces |
                DateTimeStyles.RoundtripKind,
                out var dateTime))
        {
            return CellValue.FromNumber(dateTime.TimeOfDay.TotalDays);
        }
        return FormulaValueCoercion.Error("#VALUE!");
    }

    private static DateTime GetCurrentDateTime(
        IFormulaEvaluationContext context) =>
        context is IFormulaClockEvaluationContext clock
            ? clock.CurrentDateTime
            : DateTime.Now;

    private static bool TryInteger(CellValue value, out int integer) =>
        FormulaValueCoercion.TryInteger(
            value,
            out integer,
            allowText: true);
}
