namespace NeraSpreadSheet.Formulas;

internal static partial class AdditionalFinancialFormulaFunctions
{
    private static FormulaEvaluationResult
        EvaluateInterestOnEqualPrincipalSchedule(
            FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var rate,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var period,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var totalPeriods,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[3],
                out var presentValue,
                out error))
        {
            return error;
        }

        if (totalPeriods <= 0d ||
            period < 0d ||
            period > totalPeriods)
        {
            return NumericError();
        }

        var result =
            presentValue *
            rate *
            ((period / totalPeriods) - 1d);
        return Number(result);
    }

    private static FormulaEvaluationResult EvaluateEffectiveAnnualRate(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var nominalRate,
                out var error) ||
            !TryGetTruncatedPeriodsPerYear(
                invocation.Arguments[1],
                out var periodsPerYear,
                out error))
        {
            return error;
        }
        if (nominalRate <= 0d)
        {
            return NumericError();
        }

        var periodicRate = nominalRate / periodsPerYear;
        var logarithm =
            periodsPerYear * LogOnePlus(periodicRate);
        if (!double.IsFinite(logarithm) ||
            logarithm > MaximumLogarithm)
        {
            return NumericError();
        }

        return Number(ExponentialMinusOne(logarithm));
    }

    private static FormulaEvaluationResult EvaluateNominalAnnualRate(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var effectiveRate,
                out var error) ||
            !TryGetTruncatedPeriodsPerYear(
                invocation.Arguments[1],
                out var periodsPerYear,
                out error))
        {
            return error;
        }
        if (effectiveRate <= 0d)
        {
            return NumericError();
        }

        var periodicLogarithm =
            LogOnePlus(effectiveRate) / periodsPerYear;
        var periodicRate =
            ExponentialMinusOne(periodicLogarithm);
        return Number(periodsPerYear * periodicRate);
    }

    private static FormulaEvaluationResult EvaluateEquivalentGrowthRate(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var periods,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var presentValue,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var futureValue,
                out error))
        {
            return error;
        }
        if (periods <= 0d ||
            presentValue <= 0d ||
            futureValue <= 0d ||
            !TryLogPositiveRatio(
                futureValue,
                presentValue,
                out var logarithm))
        {
            return NumericError();
        }

        var periodicLogarithm = logarithm / periods;
        if (!double.IsFinite(periodicLogarithm) ||
            periodicLogarithm > MaximumLogarithm)
        {
            return NumericError();
        }
        return Number(
            ExponentialMinusOne(periodicLogarithm));
    }

    private static FormulaEvaluationResult EvaluatePeriodicDuration(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var rate,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var presentValue,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var futureValue,
                out error))
        {
            return error;
        }
        if (rate <= 0d ||
            presentValue <= 0d ||
            futureValue <= 0d ||
            !TryLogPositiveRatio(
                futureValue,
                presentValue,
                out var numerator))
        {
            return NumericError();
        }

        var denominator = LogOnePlus(rate);
        if (!double.IsFinite(denominator) ||
            denominator <= 0d)
        {
            return NumericError();
        }
        return Number(numerator / denominator);
    }

    private static bool TryGetTruncatedPeriodsPerYear(
        FormulaFunctionArgument argument,
        out double periodsPerYear,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarNumber(
                argument,
                out periodsPerYear,
                out error))
        {
            return false;
        }

        periodsPerYear = Math.Truncate(periodsPerYear);
        if (periodsPerYear < 1d)
        {
            error = NumericError();
            return false;
        }
        error = default!;
        return true;
    }

    private static bool TryLogPositiveRatio(
        double numerator,
        double denominator,
        out double logarithm)
    {
        var relativeDifference =
            (numerator - denominator) / denominator;
        logarithm = double.IsFinite(relativeDifference) &&
                    relativeDifference > -1d &&
                    Math.Abs(relativeDifference) <= 0.5d
            ? LogOnePlus(relativeDifference)
            : Math.Log(numerator) - Math.Log(denominator);
        return double.IsFinite(logarithm);
    }
}
