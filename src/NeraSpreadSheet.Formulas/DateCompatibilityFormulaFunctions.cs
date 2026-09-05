using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Deterministic date-compatibility functions whose calendar semantics are
/// independent from WPF, WinForms and MAUI hosts.
/// </summary>
internal static class DateCompatibilityFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition(
            "DATEDIF",
            3,
            3,
            EvaluateDateDifference);
        yield return CreateDefinition(
            "DAYS360",
            2,
            3,
            EvaluateDays360);
        yield return CreateDefinition(
            "ISOWEEKNUM",
            1,
            1,
            EvaluateIsoWeekNumber);
        yield return CreateDefinition(
            "WEEKNUM",
            1,
            2,
            EvaluateWeekNumber);
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

    private static FormulaEvaluationResult EvaluateDateDifference(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarDate(
                invocation.Arguments[0],
                out var startDate,
                out var error) ||
            !TryGetScalarDate(
                invocation.Arguments[1],
                out var endDate,
                out error) ||
            !TryGetScalarText(
                invocation.Arguments[2],
                out var unit,
                out error))
        {
            return error;
        }
        if (startDate > endDate)
        {
            return NumericError();
        }

        var result = unit.Trim().ToUpperInvariant() switch
        {
            "Y" => GetCompletedYears(startDate, endDate),
            "M" => GetCompletedMonths(startDate, endDate),
            "D" => (endDate - startDate).TotalDays,
            "MD" => GetDayDifferenceIgnoringMonthsAndYears(
                startDate,
                endDate),
            "YM" => GetMonthDifferenceIgnoringYears(
                startDate,
                endDate),
            "YD" => GetDayDifferenceIgnoringYears(
                startDate,
                endDate),
            _ => double.NaN,
        };

        return Number(result);
    }

    private static FormulaEvaluationResult EvaluateDays360(
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

        var europeanMethod = false;
        if (invocation.Arguments.Count == 3 &&
            !TryGetScalarBoolean(
                invocation.Arguments[2],
                out europeanMethod,
                out error))
        {
            return error;
        }

        return Number(GetDays360(
            startDate,
            endDate,
            europeanMethod));
    }

    private static FormulaEvaluationResult EvaluateIsoWeekNumber(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarDate(
                invocation.Arguments[0],
                out var date,
                out var error))
        {
            return error;
        }

        return Number(ISOWeek.GetWeekOfYear(date));
    }

    private static FormulaEvaluationResult EvaluateWeekNumber(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarDate(
                invocation.Arguments[0],
                out var date,
                out var error))
        {
            return error;
        }

        var returnType = 1;
        if (invocation.Arguments.Count == 2 &&
            !TryGetTruncatedInteger(
                invocation.Arguments[1],
                out returnType,
                out error))
        {
            return error;
        }
        if (returnType == 21)
        {
            return Number(ISOWeek.GetWeekOfYear(date));
        }

        var firstDayOfWeek = returnType switch
        {
            1 or 17 => DayOfWeek.Sunday,
            2 or 11 => DayOfWeek.Monday,
            12 => DayOfWeek.Tuesday,
            13 => DayOfWeek.Wednesday,
            14 => DayOfWeek.Thursday,
            15 => DayOfWeek.Friday,
            16 => DayOfWeek.Saturday,
            _ => (DayOfWeek)(-1),
        };
        if ((int)firstDayOfWeek < 0)
        {
            return NumericError();
        }

        return Number(GetSystemOneWeekNumber(
            date,
            firstDayOfWeek));
    }

    private static int GetCompletedYears(
        DateTime startDate,
        DateTime endDate)
    {
        var years = endDate.Year - startDate.Year;
        if (endDate.Month < startDate.Month ||
            (endDate.Month == startDate.Month &&
             endDate.Day < startDate.Day))
        {
            years--;
        }

        return years;
    }

    private static int GetCompletedMonths(
        DateTime startDate,
        DateTime endDate)
    {
        var months =
            ((endDate.Year - startDate.Year) * 12) +
            endDate.Month -
            startDate.Month;
        if (endDate.Day < startDate.Day)
        {
            months--;
        }

        return months;
    }

    private static int GetDayDifferenceIgnoringMonthsAndYears(
        DateTime startDate,
        DateTime endDate)
    {
        if (endDate.Day >= startDate.Day)
        {
            return endDate.Day - startDate.Day;
        }

        var previousMonthYear = endDate.Month == 1
            ? endDate.Year - 1
            : endDate.Year;
        var previousMonth = endDate.Month == 1
            ? 12
            : endDate.Month - 1;
        var previousMonthDays = DateTime.DaysInMonth(
            previousMonthYear,
            previousMonth);
        return endDate.Day +
               previousMonthDays -
               startDate.Day;
    }

    private static int GetMonthDifferenceIgnoringYears(
        DateTime startDate,
        DateTime endDate)
    {
        var months = endDate.Month - startDate.Month;
        if (endDate.Day < startDate.Day)
        {
            months--;
        }
        if (months < 0)
        {
            months += 12;
        }

        return months;
    }

    private static int GetDayDifferenceIgnoringYears(
        DateTime startDate,
        DateTime endDate)
    {
        var anniversary = CreateClampedDate(
            endDate.Year,
            startDate.Month,
            startDate.Day);
        if (anniversary > endDate)
        {
            anniversary = CreateClampedDate(
                endDate.Year - 1,
                startDate.Month,
                startDate.Day);
        }

        return (endDate - anniversary).Days;
    }

    private static DateTime CreateClampedDate(
        int year,
        int month,
        int day) =>
        new(
            year,
            month,
            Math.Min(day, DateTime.DaysInMonth(year, month)));

    private static int GetDays360(
        DateTime startDate,
        DateTime endDate,
        bool europeanMethod)
    {
        var sign = 1;
        if (startDate > endDate)
        {
            (startDate, endDate) = (endDate, startDate);
            sign = -1;
        }

        var days = europeanMethod
            ? GetEuropeanDays360(startDate, endDate)
            : GetUsNasdDays360(startDate, endDate);
        return sign * days;
    }

    private static int GetUsNasdDays360(
        DateTime startDate,
        DateTime endDate)
    {
        var startYear = startDate.Year;
        var startMonth = startDate.Month;
        var startDay = startDate.Day;
        var endYear = endDate.Year;
        var endMonth = endDate.Month;
        var endDay = endDate.Day;

        if (IsLastDayOfMonth(startDate))
        {
            startDay = 30;
        }

        if (IsLastDayOfMonth(endDate))
        {
            if (startDay < 30)
            {
                endDay = 1;
                if (endMonth == 12)
                {
                    endMonth = 1;
                    endYear++;
                }
                else
                {
                    endMonth++;
                }
            }
            else
            {
                endDay = 30;
            }
        }

        return ((endYear - startYear) * 360) +
               ((endMonth - startMonth) * 30) +
               endDay -
               startDay;
    }

    private static int GetEuropeanDays360(
        DateTime startDate,
        DateTime endDate) =>
        ((endDate.Year - startDate.Year) * 360) +
        ((endDate.Month - startDate.Month) * 30) +
        Math.Min(endDate.Day, 30) -
        Math.Min(startDate.Day, 30);

    private static int GetSystemOneWeekNumber(
        DateTime date,
        DayOfWeek firstDayOfWeek)
    {
        var firstDayOfYear = new DateTime(date.Year, 1, 1);
        var leadingDays =
            ((int)firstDayOfYear.DayOfWeek -
             (int)firstDayOfWeek +
             7) %
            7;
        return ((date.DayOfYear - 1 + leadingDays) / 7) + 1;
    }

    private static bool IsLastDayOfMonth(DateTime date) =>
        date.Day == DateTime.DaysInMonth(
            date.Year,
            date.Month);

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

    private static bool TryGetScalarText(
        FormulaFunctionArgument argument,
        out string value,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar)
        {
            value = string.Empty;
            error = InvalidValue();
            return false;
        }

        value = FormulaValueCoercion.ToText(argument.ScalarValue);
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
}
