using System.Globalization;
using System.Text;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Business-day calendar functions and locale-independent numeric text parsing.
/// Holiday ranges retain their source identity through the versioned SDK.
/// </summary>
internal static class BusinessCalendarFormulaFunctions
{
    public const int MaximumNumberValueTextLength = 1_000_000;

    private static readonly long[] NoHolidays = Array.Empty<long>();

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition(
            "NETWORKDAYS",
            2,
            3,
            invocation => EvaluateNetworkDays(
                invocation,
                international: false),
            allowRanges: true);
        yield return CreateDefinition(
            "NETWORKDAYS.INTL",
            2,
            4,
            invocation => EvaluateNetworkDays(
                invocation,
                international: true),
            allowRanges: true);
        yield return CreateDefinition(
            "WORKDAY",
            2,
            3,
            invocation => EvaluateWorkday(
                invocation,
                international: false),
            allowRanges: true);
        yield return CreateDefinition(
            "WORKDAY.INTL",
            2,
            4,
            invocation => EvaluateWorkday(
                invocation,
                international: true),
            allowRanges: true);
        yield return CreateDefinition(
            "NUMBERVALUE",
            1,
            3,
            EvaluateNumberValue,
            allowRanges: false,
            securityClassification:
                FormulaFunctionSecurityClassification.ContextReadOnly);
    }

    private static FormulaFunctionDefinition CreateDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator,
        bool allowRanges,
        FormulaFunctionSecurityClassification securityClassification =
            FormulaFunctionSecurityClassification.Pure) =>
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
                securityClassification: securityClassification,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaEvaluationResult EvaluateNetworkDays(
        FormulaFunctionInvocation invocation,
        bool international)
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

        var weekendMask = BusinessWeekendMask.SaturdaySunday;
        var holidayIndex = 2;
        if (international)
        {
            holidayIndex = 3;
            if (invocation.Arguments.Count >= 3 &&
                !TryGetWeekendMask(
                    invocation.Arguments[2],
                    allowNoWorkdays: true,
                    out weekendMask,
                    out error))
            {
                return error;
            }
        }

        var holidays = NoHolidays;
        if (invocation.Arguments.Count > holidayIndex &&
            !TryGetHolidays(
                invocation.Arguments[holidayIndex],
                weekendMask,
                out holidays,
                out error))
        {
            return error;
        }

        var sign = 1d;
        if (startDate > endDate)
        {
            (startDate, endDate) = (endDate, startDate);
            sign = -1d;
        }

        var count = BusinessDayCalendarMath.CountBusinessDaysInclusive(
            startDate,
            endDate,
            weekendMask,
            holidays);
        return Number(sign * count);
    }

    private static FormulaEvaluationResult EvaluateWorkday(
        FormulaFunctionInvocation invocation,
        bool international)
    {
        if (!TryGetScalarDate(
                invocation.Arguments[0],
                out var startDate,
                out var error) ||
            !TryGetTruncatedInt64(
                invocation.Arguments[1],
                out var dayOffset,
                out error))
        {
            return error;
        }

        var weekendMask = BusinessWeekendMask.SaturdaySunday;
        var holidayIndex = 2;
        if (international)
        {
            holidayIndex = 3;
            if (invocation.Arguments.Count >= 3 &&
                !TryGetWeekendMask(
                    invocation.Arguments[2],
                    allowNoWorkdays: false,
                    out weekendMask,
                    out error))
            {
                return error;
            }
        }

        var holidays = NoHolidays;
        if (invocation.Arguments.Count > holidayIndex &&
            !TryGetHolidays(
                invocation.Arguments[holidayIndex],
                weekendMask,
                out holidays,
                out error))
        {
            return error;
        }

        if (!BusinessDayCalendarMath.TryShiftBusinessDays(
                startDate,
                dayOffset,
                weekendMask,
                holidays,
                out var result))
        {
            return NumericError();
        }

        return FormulaEvaluationResult.Success(
            CellValue.FromDateTime(result));
    }

    private static FormulaEvaluationResult EvaluateNumberValue(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarText(
                invocation.Arguments[0],
                out var text,
                out var error))
        {
            return error;
        }
        if (text.Length > MaximumNumberValueTextLength)
        {
            return NumericError();
        }

        if (!TryGetNumberSeparators(
                invocation,
                out var decimalSeparator,
                out var groupSeparator,
                out error))
        {
            return error;
        }

        if (!TryParseNumberValue(
                text,
                decimalSeparator,
                groupSeparator,
                out var value))
        {
            return InvalidValue();
        }

        return Number(value);
    }

    private static bool TryGetWeekendMask(
        FormulaFunctionArgument argument,
        bool allowNoWorkdays,
        out BusinessWeekendMask mask,
        out FormulaEvaluationResult error)
    {
        mask = default;
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar)
        {
            error = InvalidValue();
            return false;
        }

        var value = argument.ScalarValue;
        if (value.Kind == CellValueKind.Text)
        {
            var text = (string)value.RawValue!;
            if (BusinessWeekendMask.TryFromString(text, out mask))
            {
                if (!allowNoWorkdays && mask.WorkdayCount == 0)
                {
                    error = InvalidValue();
                    return false;
                }

                error = default!;
                return true;
            }
        }

        if (!FormulaValueCoercion.TryNumber(
                value,
                out var number,
                allowText: true) ||
            !double.IsFinite(number))
        {
            error = InvalidValue();
            return false;
        }

        var truncated = Math.Truncate(number);
        if (truncated < int.MinValue ||
            truncated > int.MaxValue ||
            !BusinessWeekendMask.TryFromCode(
                checked((int)truncated),
                out mask))
        {
            error = NumericError();
            return false;
        }

        error = default!;
        return true;
    }

    private static bool TryGetHolidays(
        FormulaFunctionArgument argument,
        BusinessWeekendMask weekendMask,
        out long[] holidays,
        out FormulaEvaluationResult error)
    {
        if (argument.Values.Count >
            BusinessDayCalendarMath.MaximumHolidayValues)
        {
            holidays = NoHolidays;
            error = NumericError();
            return false;
        }

        var values = new HashSet<long>();
        foreach (var value in argument.Values)
        {
            if (value.Kind == CellValueKind.Blank ||
                value.Kind == CellValueKind.Text &&
                string.IsNullOrWhiteSpace((string)value.RawValue!))
            {
                continue;
            }

            if (!TryGetHolidayDate(
                    value,
                    out var date,
                    out error))
            {
                holidays = NoHolidays;
                return false;
            }

            if (!weekendMask.IsWeekend(date))
            {
                values.Add(
                    BusinessDayCalendarMath.GetDayNumber(date));
            }
        }

        holidays = values
            .OrderBy(static value => value)
            .ToArray();
        error = default!;
        return true;
    }

    private static bool TryGetHolidayDate(
        CellValue value,
        out DateTime date,
        out FormulaEvaluationResult error)
    {
        switch (value.Kind)
        {
            case CellValueKind.DateTime:
                date = ((DateTime)value.RawValue!).Date;
                error = default!;
                return true;
            case CellValueKind.Number:
                try
                {
                    date = DateTime.FromOADate(
                        (double)value.RawValue!).Date;
                    error = default!;
                    return true;
                }
                catch (ArgumentException)
                {
                    date = default;
                    error = NumericError();
                    return false;
                }
            case CellValueKind.Text:
                if (DateTime.TryParse(
                        (string)value.RawValue!,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces |
                        DateTimeStyles.RoundtripKind,
                        out date))
                {
                    date = date.Date;
                    error = default!;
                    return true;
                }
                break;
        }

        date = default;
        error = InvalidValue();
        return false;
    }

    private static bool TryGetScalarDate(
        FormulaFunctionArgument argument,
        out DateTime date,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar)
        {
            date = default;
            error = InvalidValue();
            return false;
        }

        var value = argument.ScalarValue;
        if (value.Kind == CellValueKind.Number)
        {
            try
            {
                date = DateTime.FromOADate(
                    (double)value.RawValue!).Date;
                error = default!;
                return true;
            }
            catch (ArgumentException)
            {
                date = default;
                error = NumericError();
                return false;
            }
        }

        if (!FormulaValueCoercion.TryDateTime(
                value,
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

    private static bool TryGetTruncatedInt64(
        FormulaFunctionArgument argument,
        out long value,
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

    private static bool TryGetScalarText(
        FormulaFunctionArgument argument,
        out string text,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar)
        {
            text = string.Empty;
            error = InvalidValue();
            return false;
        }

        text = FormulaValueCoercion.ToText(argument.ScalarValue);
        error = default!;
        return true;
    }

    private static bool TryGetNumberSeparators(
        FormulaFunctionInvocation invocation,
        out char? decimalSeparator,
        out char? groupSeparator,
        out FormulaEvaluationResult error)
    {
        var defaultDecimal = ".";
        var defaultGroup = ",";
        if (invocation.Context is IFormulaLocaleEvaluationContext locale)
        {
            defaultDecimal = locale.DecimalSeparator;
            defaultGroup = locale.GroupSeparator;
        }

        if (!TryGetSeparator(
                invocation.Arguments.Count >= 2
                    ? invocation.Arguments[1]
                    : null,
                defaultDecimal,
                allowWhitespace: false,
                out decimalSeparator,
                out error) ||
            !TryGetSeparator(
                invocation.Arguments.Count >= 3
                    ? invocation.Arguments[2]
                    : null,
                defaultGroup,
                allowWhitespace: true,
                out groupSeparator,
                out error))
        {
            return false;
        }

        if (decimalSeparator.HasValue &&
            groupSeparator.HasValue &&
            decimalSeparator.Value == groupSeparator.Value)
        {
            error = InvalidValue();
            return false;
        }

        error = default!;
        return true;
    }

    private static bool TryGetSeparator(
        FormulaFunctionArgument? argument,
        string defaultValue,
        bool allowWhitespace,
        out char? separator,
        out FormulaEvaluationResult error)
    {
        string value;
        if (argument is null)
        {
            value = defaultValue;
            if (string.IsNullOrEmpty(value))
            {
                separator = null;
                error = default!;
                return true;
            }
        }
        else
        {
            if (argument.Kind != FormulaFunctionArgumentKind.Scalar)
            {
                separator = null;
                error = InvalidValue();
                return false;
            }

            value = FormulaValueCoercion.ToText(argument.ScalarValue);
            if (value.Length == 0)
            {
                separator = null;
                error = default!;
                return true;
            }
        }

        var candidate = value[0];
        if (!IsValidSeparator(candidate, allowWhitespace))
        {
            separator = null;
            error = InvalidValue();
            return false;
        }

        separator = candidate;
        error = default!;
        return true;
    }

    private static bool IsValidSeparator(
        char value,
        bool allowWhitespace) =>
        !char.IsDigit(value) &&
        (allowWhitespace || !char.IsWhiteSpace(value)) &&
        value is not '+' and not '-' and not '%' and
        not 'e' and not 'E';

    private static bool TryParseNumberValue(
        string text,
        char? decimalSeparator,
        char? groupSeparator,
        out double value)
    {
        if (text.Length == 0)
        {
            value = 0d;
            return true;
        }

        var compact = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character))
            {
                compact.Append(character);
            }
        }

        if (compact.Length == 0)
        {
            value = 0d;
            return true;
        }

        var percentCount = 0;
        while (compact.Length > 0 &&
               compact[^1] == '%')
        {
            percentCount++;
            compact.Length--;
        }
        if (compact.Length == 0 ||
            compact.ToString().Contains('%'))
        {
            value = default;
            return false;
        }

        var normalized = compact.ToString();
        var decimalIndex = -1;
        if (decimalSeparator.HasValue)
        {
            decimalIndex = normalized.IndexOf(decimalSeparator.Value);
            if (decimalIndex >= 0 &&
                normalized.IndexOf(
                    decimalSeparator.Value,
                    decimalIndex + 1) >= 0)
            {
                value = default;
                return false;
            }
        }

        if (groupSeparator.HasValue)
        {
            var groupIndex = normalized.IndexOf(groupSeparator.Value);
            while (groupIndex >= 0)
            {
                if (decimalIndex >= 0 &&
                    groupIndex > decimalIndex)
                {
                    value = default;
                    return false;
                }

                normalized = normalized.Remove(groupIndex, 1);
                if (decimalIndex >= 0 &&
                    groupIndex < decimalIndex)
                {
                    decimalIndex--;
                }
                groupIndex = normalized.IndexOf(groupSeparator.Value);
            }
        }

        if (decimalSeparator.HasValue &&
            decimalIndex >= 0 &&
            decimalSeparator.Value != '.')
        {
            normalized =
                normalized[..decimalIndex] +
                "." +
                normalized[(decimalIndex + 1)..];
        }

        const NumberStyles styles =
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowExponent;
        if (!double.TryParse(
                normalized,
                styles,
                CultureInfo.InvariantCulture,
                out value) ||
            !double.IsFinite(value))
        {
            value = default;
            return false;
        }

        if (percentCount > 0)
        {
            value *= Math.Pow(0.01d, percentCount);
            if (!double.IsFinite(value))
            {
                value = default;
                return false;
            }
        }

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
