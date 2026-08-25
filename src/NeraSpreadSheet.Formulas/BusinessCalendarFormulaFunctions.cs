using System.Globalization;
using System.Text;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Business-day calendar functions and deterministic locale-sensitive number
/// parsing. Holiday ranges preserve source identity through engine capture.
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
            invocation => EvaluateNetworkDays(invocation, false),
            allowRanges: true);
        yield return CreateDefinition(
            "NETWORKDAYS.INTL",
            2,
            4,
            invocation => EvaluateNetworkDays(invocation, true),
            allowRanges: true);
        yield return CreateDefinition(
            "WORKDAY",
            2,
            3,
            invocation => EvaluateWorkday(invocation, false),
            allowRanges: true);
        yield return CreateDefinition(
            "WORKDAY.INTL",
            2,
            4,
            invocation => EvaluateWorkday(invocation, true),
            allowRanges: true);
        yield return CreateDefinition(
            "NUMBERVALUE",
            1,
            3,
            EvaluateNumberValue,
            allowRanges: false,
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

        var weekend = BusinessWeekendMask.SaturdaySunday;
        var holidayIndex = 2;
        if (international)
        {
            holidayIndex = 3;
            if (invocation.Arguments.Count >= 3 &&
                !TryGetWeekendMask(
                    invocation.Arguments[2],
                    allowNoWorkdays: true,
                    out weekend,
                    out error))
            {
                return error;
            }
        }

        var holidays = NoHolidays;
        if (invocation.Arguments.Count > holidayIndex &&
            !TryGetHolidays(
                invocation.Arguments[holidayIndex],
                weekend,
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
            weekend,
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
            !TryGetTruncatedInt32(
                invocation.Arguments[1],
                out var dayOffset,
                out error))
        {
            return error;
        }

        var weekend = BusinessWeekendMask.SaturdaySunday;
        var holidayIndex = 2;
        if (international)
        {
            holidayIndex = 3;
            if (invocation.Arguments.Count >= 3 &&
                !TryGetWeekendMask(
                    invocation.Arguments[2],
                    allowNoWorkdays: false,
                    out weekend,
                    out error))
            {
                return error;
            }
        }

        var holidays = NoHolidays;
        if (invocation.Arguments.Count > holidayIndex &&
            !TryGetHolidays(
                invocation.Arguments[holidayIndex],
                weekend,
                out holidays,
                out error))
        {
            return error;
        }

        if (!BusinessDayCalendarMath.TryShiftBusinessDays(
                startDate,
                dayOffset,
                weekend,
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
                out var number))
        {
            return InvalidValue();
        }

        return Number(number);
    }

    private static bool TryGetWeekendMask(
        FormulaFunctionArgument argument,
        bool allowNoWorkdays,
        out BusinessWeekendMask weekend,
        out FormulaEvaluationResult error)
    {
        weekend = default;
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar)
        {
            error = InvalidValue();
            return false;
        }

        var value = argument.ScalarValue;
        if (value.Kind == CellValueKind.Text)
        {
            var text = (string)value.RawValue!;
            if (BusinessWeekendMask.TryFromString(text, out weekend))
            {
                if (!allowNoWorkdays && weekend.WorkdayCount == 0)
                {
                    error = InvalidValue();
                    return false;
                }

                error = default!;
                return true;
            }

            if (text.Length > 2)
            {
                error = InvalidValue();
                return false;
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
                out weekend))
        {
            error = NumericError();
            return false;
        }

        error = default!;
        return true;
    }

    private static bool TryGetHolidays(
        FormulaFunctionArgument argument,
        BusinessWeekendMask weekend,
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

        var uniqueDays = new HashSet<long>();
        foreach (var value in argument.Values)
        {
            if (value.Kind == CellValueKind.Blank ||
                value.Kind == CellValueKind.Text &&
                string.IsNullOrWhiteSpace((string)value.RawValue!))
            {
                continue;
            }
            if (!TryGetHolidayDate(value, out var date, out error))
            {
                holidays = NoHolidays;
                return false;
            }
            if (!weekend.IsWeekend(date))
            {
                uniqueDays.Add(
                    BusinessDayCalendarMath.GetDayNumber(date));
            }
        }

        holidays = uniqueDays
            .OrderBy(static day => day)
            .ToArray();
        error = default!;
        return true;
    }

    private static bool TryGetHolidayDate(
        CellValue value,
        out DateTime date,
        out FormulaEvaluationResult error)
    {
        if (value.Kind == CellValueKind.DateTime)
        {
            date = ((DateTime)value.RawValue!).Date;
            error = default!;
            return true;
        }
        if (value.Kind == CellValueKind.Number)
        {
            return TryGetOaDate(
                (double)value.RawValue!,
                out date,
                out error);
        }
        if (value.Kind == CellValueKind.Text &&
            DateTime.TryParse(
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
            return TryGetOaDate(
                (double)value.RawValue!,
                out date,
                out error);
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

    private static bool TryGetOaDate(
        double serial,
        out DateTime date,
        out FormulaEvaluationResult error)
    {
        try
        {
            date = DateTime.FromOADate(serial).Date;
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

    private static bool TryGetTruncatedInt32(
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
        decimalSeparator = null;
        groupSeparator = null;
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
                out error))
        {
            return false;
        }
        if (!TryGetSeparator(
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
        string fallback,
        bool allowWhitespace,
        out char? separator,
        out FormulaEvaluationResult error)
    {
        var text = fallback;
        if (argument is not null)
        {
            if (argument.Kind != FormulaFunctionArgumentKind.Scalar)
            {
                separator = null;
                error = InvalidValue();
                return false;
            }

            text = FormulaValueCoercion.ToText(argument.ScalarValue);
        }

        if (string.IsNullOrEmpty(text))
        {
            separator = null;
            error = default!;
            return true;
        }

        var candidate = text[0];
        if (char.IsDigit(candidate) ||
            !allowWhitespace && char.IsWhiteSpace(candidate) ||
            candidate is '+' or '-' or '%' or 'e' or 'E')
        {
            separator = null;
            error = InvalidValue();
            return false;
        }

        separator = candidate;
        error = default!;
        return true;
    }

    private static bool TryParseNumberValue(
        string text,
        char? decimalSeparator,
        char? groupSeparator,
        out double value)
    {
        var compactBuilder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character))
            {
                compactBuilder.Append(character);
            }
        }

        if (compactBuilder.Length == 0)
        {
            value = 0d;
            return true;
        }

        var percentCount = 0;
        while (compactBuilder.Length > 0 &&
               compactBuilder[compactBuilder.Length - 1] == '%')
        {
            percentCount++;
            compactBuilder.Length--;
        }
        if (compactBuilder.Length == 0 ||
            compactBuilder.ToString().Contains('%'))
        {
            value = default;
            return false;
        }

        var compact = compactBuilder.ToString();
        var normalized = new StringBuilder(compact.Length);
        var decimalSeen = false;
        foreach (var character in compact)
        {
            if (groupSeparator.HasValue &&
                character == groupSeparator.Value)
            {
                if (decimalSeen)
                {
                    value = default;
                    return false;
                }

                continue;
            }
            if (decimalSeparator.HasValue &&
                character == decimalSeparator.Value)
            {
                if (decimalSeen)
                {
                    value = default;
                    return false;
                }

                decimalSeen = true;
                normalized.Append('.');
                continue;
            }

            normalized.Append(character);
        }

        const NumberStyles styles =
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowExponent;
        if (!double.TryParse(
                normalized.ToString(),
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
