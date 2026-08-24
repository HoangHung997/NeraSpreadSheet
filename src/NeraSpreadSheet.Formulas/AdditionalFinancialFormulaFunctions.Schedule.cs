using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static partial class AdditionalFinancialFormulaFunctions
{
    private static bool TryCollectSchedule(
        FormulaFunctionArgument valuesArgument,
        FormulaFunctionArgument datesArgument,
        int maximumValues,
        out ScheduledCashFlow[] schedule,
        out bool hasLaterDate,
        out FormulaEvaluationResult error)
    {
        schedule = [];
        hasLaterDate = false;
        error = default!;

        if (valuesArgument.Values.Count == 0 ||
            valuesArgument.Values.Count != datesArgument.Values.Count ||
            valuesArgument.Values.Count > maximumValues)
        {
            error = NumericError();
            return false;
        }

        var result = new ScheduledCashFlow[valuesArgument.Values.Count];
        var firstDayNumber = default(long);
        for (var index = 0; index < result.Length; index++)
        {
            if (!TryGetScheduledNumber(
                    valuesArgument.Values[index],
                    valuesArgument.Kind ==
                        FormulaFunctionArgumentKind.Scalar,
                    out var value) ||
                !TryGetScheduledDayNumber(
                    datesArgument.Values[index],
                    datesArgument.Kind ==
                        FormulaFunctionArgumentKind.Scalar,
                    out var dayNumber))
            {
                error = InvalidValue();
                return false;
            }

            if (index == 0)
            {
                firstDayNumber = dayNumber;
            }
            else if (dayNumber < firstDayNumber)
            {
                error = NumericError();
                return false;
            }
            else if (dayNumber > firstDayNumber)
            {
                hasLaterDate = true;
            }

            result[index] = new ScheduledCashFlow(value, dayNumber);
        }

        schedule = result;
        return true;
    }

    private static bool TryGetScheduledNumber(
        CellValue value,
        bool isScalar,
        out double number)
    {
        if (!isScalar &&
            value.Kind is not (
                CellValueKind.Number or
                CellValueKind.DateTime))
        {
            number = default;
            return false;
        }

        return FormulaValueCoercion.TryNumber(
                   value,
                   out number,
                   allowText: isScalar) &&
               double.IsFinite(number);
    }

    private static bool TryGetScheduledDayNumber(
        CellValue value,
        bool isScalar,
        out long dayNumber)
    {
        DateTime date;
        if (value.Kind == CellValueKind.DateTime)
        {
            date = ((DateTime)value.RawValue!).Date;
        }
        else
        {
            var supportedKind =
                value.Kind == CellValueKind.Number ||
                (isScalar && value.Kind == CellValueKind.Text);
            if (!supportedKind ||
                !FormulaValueCoercion.TryNumber(
                    value,
                    out var serial,
                    allowText: isScalar) ||
                !double.IsFinite(serial))
            {
                dayNumber = default;
                return false;
            }

            try
            {
                date = DateTime.FromOADate(
                    Math.Truncate(serial)).Date;
            }
            catch (ArgumentException)
            {
                dayNumber = default;
                return false;
            }
        }

        dayNumber = date.Ticks / TimeSpan.TicksPerDay;
        return true;
    }

    private static bool HasBothCashFlowSigns(
        IReadOnlyList<ScheduledCashFlow> schedule)
    {
        var hasPositive = false;
        var hasNegative = false;
        foreach (var cashFlow in schedule)
        {
            hasPositive |= cashFlow.Value > 0d;
            hasNegative |= cashFlow.Value < 0d;
        }
        return hasPositive && hasNegative;
    }

    private static bool TryEvaluateSchedule(
        IReadOnlyList<ScheduledCashFlow> schedule,
        double rate,
        out double value,
        out double derivative)
    {
        if (!IsValidRate(rate) || schedule.Count == 0)
        {
            value = default;
            derivative = default;
            return false;
        }

        var logarithm = LogOnePlus(rate);
        if (!double.IsFinite(logarithm))
        {
            value = default;
            derivative = default;
            return false;
        }

        var firstDayNumber = schedule[0].DayNumber;
        var discountBase = 1d + rate;
        var sum = 0d;
        var compensation = 0d;
        var derivativeSum = 0d;
        var derivativeCompensation = 0d;
        foreach (var cashFlow in schedule)
        {
            var yearFraction =
                (cashFlow.DayNumber - firstDayNumber) / 365d;
            var exponent = -yearFraction * logarithm;
            if (!double.IsFinite(exponent) ||
                exponent > MaximumLogarithm)
            {
                value = default;
                derivative = default;
                return false;
            }

            var factor = Math.Exp(exponent);
            var term = cashFlow.Value * factor;
            if (!double.IsFinite(term))
            {
                value = default;
                derivative = default;
                return false;
            }
            AddCompensated(ref sum, ref compensation, term);

            if (yearFraction == 0d)
            {
                continue;
            }
            var derivativeTerm =
                -(yearFraction * term) / discountBase;
            if (!double.IsFinite(derivativeTerm))
            {
                value = default;
                derivative = default;
                return false;
            }
            AddCompensated(
                ref derivativeSum,
                ref derivativeCompensation,
                derivativeTerm);
        }

        value = sum;
        derivative = derivativeSum;
        return double.IsFinite(value) &&
               double.IsFinite(derivative);
    }
}
