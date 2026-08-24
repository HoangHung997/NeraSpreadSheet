using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Bounded cumulative-payment and accelerated-depreciation functions.
/// </summary>
internal static class RemainingFinancialFormulaFunctions
{
    public const int MaximumSchedulePeriods = 2_000_000;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition(
            "CUMIPMT",
            6,
            6,
            EvaluateCumulativeInterest);
        yield return CreateDefinition(
            "CUMPRINC",
            6,
            6,
            EvaluateCumulativePrincipal);
        yield return CreateDefinition(
            "DB",
            4,
            5,
            EvaluateFixedDecliningBalance);
        yield return CreateDefinition(
            "DDB",
            4,
            5,
            EvaluateDoubleDecliningBalance);
        yield return CreateDefinition(
            "VDB",
            5,
            7,
            EvaluateVariableDecliningBalance);
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

    private static FormulaEvaluationResult EvaluateCumulativeInterest(
        FormulaFunctionInvocation invocation) =>
        EvaluateCumulativePayment(invocation, returnInterest: true);

    private static FormulaEvaluationResult EvaluateCumulativePrincipal(
        FormulaFunctionInvocation invocation) =>
        EvaluateCumulativePayment(invocation, returnInterest: false);

    private static FormulaEvaluationResult EvaluateCumulativePayment(
        FormulaFunctionInvocation invocation,
        bool returnInterest)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var rate,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var totalPeriods,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var presentValue,
                out error) ||
            !TryGetScalarInteger(
                invocation.Arguments[3],
                out var startPeriod,
                out error) ||
            !TryGetScalarInteger(
                invocation.Arguments[4],
                out var endPeriod,
                out error) ||
            !TryGetScalarInteger(
                invocation.Arguments[5],
                out var timing,
                out error))
        {
            return error;
        }

        var scheduleLength = (long)endPeriod - startPeriod + 1L;
        if (rate <= 0d ||
            totalPeriods <= 0d ||
            presentValue <= 0d ||
            startPeriod < 1 ||
            endPeriod < startPeriod ||
            endPeriod > totalPeriods + 1e-10d ||
            timing is < 0 or > 1 ||
            scheduleLength > MaximumSchedulePeriods)
        {
            return NumericError();
        }
        if (!TryCalculatePayment(
                rate,
                totalPeriods,
                presentValue,
                timing,
                out var payment))
        {
            return NumericError();
        }

        var sum = 0d;
        var compensation = 0d;
        for (var period = startPeriod;
             period <= endPeriod;
             period++)
        {
            if (!TryCalculateInterestPayment(
                    rate,
                    period,
                    payment,
                    presentValue,
                    timing,
                    out var interest))
            {
                return NumericError();
            }
            AddCompensated(
                ref sum,
                ref compensation,
                returnInterest
                    ? interest
                    : payment - interest);
        }
        return Number(sum);
    }

    private static FormulaEvaluationResult EvaluateFixedDecliningBalance(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var cost,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var salvage,
                out error) ||
            !TryGetScalarInteger(
                invocation.Arguments[2],
                out var life,
                out error) ||
            !TryGetScalarInteger(
                invocation.Arguments[3],
                out var targetPeriod,
                out error))
        {
            return error;
        }

        var month = 12;
        if (invocation.Arguments.Count == 5 &&
            !TryGetScalarInteger(
                invocation.Arguments[4],
                out month,
                out error))
        {
            return error;
        }

        var maximumPeriod = (long)life + (month < 12 ? 1L : 0L);
        if (!ValidateDepreciationInputs(cost, salvage, life) ||
            targetPeriod < 1 ||
            targetPeriod > maximumPeriod ||
            month is < 1 or > 12 ||
            targetPeriod > MaximumSchedulePeriods)
        {
            return NumericError();
        }

        var rate = 1d -
                   Math.Pow(
                       salvage / cost,
                       1d / life);
        rate = Math.Round(
            rate,
            3,
            MidpointRounding.AwayFromZero);
        if (!double.IsFinite(rate) || rate < 0d || rate > 1d)
        {
            return NumericError();
        }

        var accumulated = 0d;
        var depreciation = 0d;
        for (var period = 1;
             period <= targetPeriod;
             period++)
        {
            double rawDepreciation;
            if (period == 1)
            {
                rawDepreciation =
                    cost * rate * month / 12d;
            }
            else if (period <= life)
            {
                rawDepreciation =
                    (cost - accumulated) * rate;
            }
            else
            {
                rawDepreciation =
                    (cost - accumulated) *
                    rate *
                    (12d - month) /
                    12d;
            }

            if (!TryCapDepreciation(
                    cost,
                    salvage,
                    accumulated,
                    rawDepreciation,
                    out depreciation))
            {
                return NumericError();
            }
            accumulated += depreciation;
        }
        return Number(depreciation);
    }

    private static FormulaEvaluationResult EvaluateDoubleDecliningBalance(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var cost,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var salvage,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var life,
                out error) ||
            !TryGetScalarInteger(
                invocation.Arguments[3],
                out var targetPeriod,
                out error))
        {
            return error;
        }

        var factor = 2d;
        if (invocation.Arguments.Count == 5 &&
            !TryGetScalarNumber(
                invocation.Arguments[4],
                out factor,
                out error))
        {
            return error;
        }

        if (!ValidateDepreciationInputs(cost, salvage, life) ||
            factor <= 0d ||
            targetPeriod < 1 ||
            targetPeriod > life + 1e-10d ||
            targetPeriod > MaximumSchedulePeriods)
        {
            return NumericError();
        }

        var accumulated = 0d;
        var depreciation = 0d;
        for (var period = 1;
             period <= targetPeriod;
             period++)
        {
            var rawDepreciation =
                (cost - accumulated) *
                factor /
                life;
            if (!TryCapDepreciation(
                    cost,
                    salvage,
                    accumulated,
                    rawDepreciation,
                    out depreciation))
            {
                return NumericError();
            }
            accumulated += depreciation;
        }
        return Number(depreciation);
    }

    private static FormulaEvaluationResult EvaluateVariableDecliningBalance(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var cost,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var salvage,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var life,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[3],
                out var startPeriod,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[4],
                out var endPeriod,
                out error))
        {
            return error;
        }

        var factor = 2d;
        if (invocation.Arguments.Count >= 6 &&
            !TryGetScalarNumber(
                invocation.Arguments[5],
                out factor,
                out error))
        {
            return error;
        }

        var noSwitch = false;
        if (invocation.Arguments.Count == 7 &&
            !TryGetScalarBoolean(
                invocation.Arguments[6],
                out noSwitch,
                out error))
        {
            return error;
        }

        var schedulePeriods = Math.Ceiling(endPeriod);
        if (!ValidateDepreciationInputs(cost, salvage, life) ||
            startPeriod < 0d ||
            endPeriod <= startPeriod ||
            endPeriod > life ||
            factor <= 0d ||
            !double.IsFinite(schedulePeriods) ||
            schedulePeriods > MaximumSchedulePeriods)
        {
            return NumericError();
        }

        var accumulated = 0d;
        var result = 0d;
        var compensation = 0d;
        var switchedToStraightLine = false;
        var integerPeriods = checked((int)schedulePeriods);
        for (var period = 0;
             period < integerPeriods;
             period++)
        {
            var openingBookValue = cost - accumulated;
            var remainingDepreciable =
                Math.Max(0d, openingBookValue - salvage);
            var remainingLife = life - period;
            if (remainingLife <= 0d)
            {
                break;
            }

            var decliningDepreciation =
                Math.Min(
                    openingBookValue * factor / life,
                    remainingDepreciable);
            var straightLineDepreciation =
                remainingDepreciable / remainingLife;
            var useStraightLine =
                !noSwitch &&
                (switchedToStraightLine ||
                 straightLineDepreciation >
                 decliningDepreciation);
            if (useStraightLine)
            {
                switchedToStraightLine = true;
            }

            var fullPeriodDepreciation =
                useStraightLine
                    ? straightLineDepreciation
                    : decliningDepreciation;
            if (!double.IsFinite(fullPeriodDepreciation) ||
                fullPeriodDepreciation < 0d)
            {
                return NumericError();
            }
            fullPeriodDepreciation = Math.Min(
                fullPeriodDepreciation,
                remainingDepreciable);

            var overlap = Math.Max(
                0d,
                Math.Min(endPeriod, period + 1d) -
                Math.Max(startPeriod, period));
            if (overlap > 0d)
            {
                AddCompensated(
                    ref result,
                    ref compensation,
                    fullPeriodDepreciation * overlap);
            }
            accumulated += fullPeriodDepreciation;
        }
        return Number(result);
    }

    private static bool TryCalculatePayment(
        double rate,
        double totalPeriods,
        double presentValue,
        int timing,
        out double payment)
    {
        var growth = Math.Pow(1d + rate, totalPeriods);
        var denominator =
            (1d + (rate * timing)) *
            (growth - 1d);
        if (!double.IsFinite(growth) ||
            growth <= 0d ||
            !double.IsFinite(denominator) ||
            denominator == 0d)
        {
            payment = default;
            return false;
        }

        payment =
            -(presentValue * growth * rate) /
            denominator;
        return double.IsFinite(payment);
    }

    private static bool TryCalculateInterestPayment(
        double rate,
        int period,
        double payment,
        double presentValue,
        int timing,
        out double interest)
    {
        if (timing == 1 && period == 1)
        {
            interest = 0d;
            return true;
        }
        if (!TryCalculateFutureValue(
                rate,
                period - 1d,
                payment,
                presentValue,
                timing,
                out var balance))
        {
            interest = default;
            return false;
        }

        interest = balance * rate;
        if (timing == 1)
        {
            interest /= 1d + rate;
        }
        return double.IsFinite(interest);
    }

    private static bool TryCalculateFutureValue(
        double rate,
        double periods,
        double payment,
        double presentValue,
        int timing,
        out double result)
    {
        var growth = Math.Pow(1d + rate, periods);
        if (!double.IsFinite(growth) || growth <= 0d)
        {
            result = default;
            return false;
        }
        var annuity =
            (1d + (rate * timing)) *
            ((growth - 1d) / rate);
        result =
            -((presentValue * growth) +
              (payment * annuity));
        return double.IsFinite(result);
    }

    private static bool ValidateDepreciationInputs(
        double cost,
        double salvage,
        double life) =>
        cost > 0d &&
        salvage >= 0d &&
        salvage <= cost &&
        life > 0d;

    private static bool TryCapDepreciation(
        double cost,
        double salvage,
        double accumulated,
        double rawDepreciation,
        out double depreciation)
    {
        if (!double.IsFinite(rawDepreciation))
        {
            depreciation = default;
            return false;
        }
        var remaining =
            Math.Max(
                0d,
                cost - salvage - accumulated);
        depreciation =
            Math.Max(
                0d,
                Math.Min(rawDepreciation, remaining));
        return double.IsFinite(depreciation);
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

    private static bool TryGetScalarInteger(
        FormulaFunctionArgument argument,
        out int value,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryInteger(
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
