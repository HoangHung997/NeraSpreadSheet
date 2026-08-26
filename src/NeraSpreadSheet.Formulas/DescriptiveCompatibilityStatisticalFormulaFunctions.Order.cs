using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static partial class DescriptiveCompatibilityStatisticalFormulaFunctions
{
    private static FormulaEvaluationResult EvaluateTrimMean(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                [invocation.Arguments[0]],
                CollectionMode.Standard,
                out var values,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var percent,
                out error))
        {
            return error;
        }
        if (values.Length == 0 || percent < 0d || percent >= 1d)
        {
            return NumericError();
        }
        Array.Sort(values);
        var trimmed = (int)Math.Floor(values.Length * percent);
        trimmed -= trimmed & 1;
        var perSide = trimmed / 2;
        var remaining = values.Length - trimmed;
        if (remaining <= 0)
        {
            return NumericError();
        }
        var total = 0d;
        var compensation = 0d;
        for (var index = perSide; index < values.Length - perSide; index++)
        {
            AddCompensated(values[index], ref total, ref compensation);
        }
        return Number(total / remaining);
    }

    private static FormulaEvaluationResult EvaluatePercentileExclusive(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                [invocation.Arguments[0]],
                CollectionMode.Standard,
                out var values,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var percentile,
                out error))
        {
            return error;
        }
        return PercentileExclusive(values, percentile);
    }

    private static FormulaEvaluationResult EvaluateQuartileExclusive(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                [invocation.Arguments[0]],
                CollectionMode.Standard,
                out var values,
                out var error) ||
            !TryGetTruncatedInteger(
                invocation.Arguments[1],
                out var quartile,
                out error))
        {
            return error;
        }
        return quartile is < 1 or > 3
            ? NumericError()
            : PercentileExclusive(values, quartile / 4d);
    }

    private static FormulaEvaluationResult PercentileExclusive(
        double[] values,
        double percentile)
    {
        if (values.Length == 0 || percentile <= 0d || percentile >= 1d)
        {
            return NumericError();
        }
        Array.Sort(values);
        var rank = percentile * (values.Length + 1d);
        if (rank < 1d || rank > values.Length)
        {
            return NumericError();
        }
        var lowerRank = (int)Math.Floor(rank);
        var fraction = rank - lowerRank;
        if (fraction == 0d)
        {
            return Number(values[lowerRank - 1]);
        }
        var lower = values[lowerRank - 1];
        var upper = values[lowerRank];
        return Number(lower + ((upper - lower) * fraction));
    }

    private static FormulaEvaluationResult EvaluateRankAverage(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var number,
                out var error) ||
            !TryCollectNumbers(
                [invocation.Arguments[1]],
                CollectionMode.Standard,
                out var values,
                out error))
        {
            return error;
        }
        if (values.Length == 0)
        {
            return NotAvailable();
        }
        var ascending = false;
        if (invocation.Arguments.Count == 3)
        {
            if (!TryGetScalarNumber(
                    invocation.Arguments[2],
                    out var order,
                    out error))
            {
                return error;
            }
            ascending = order != 0d;
        }

        var before = 0;
        var equal = 0;
        foreach (var value in values)
        {
            if (value.Equals(number))
            {
                equal++;
            }
            else if (ascending ? value < number : value > number)
            {
                before++;
            }
        }
        var rank = equal == 0
            ? before + 1d
            : before + ((equal + 1d) / 2d);
        return Number(rank);
    }

    private static FormulaEvaluationResult EvaluatePercentRank(
        FormulaFunctionInvocation invocation,
        bool exclusive)
    {
        if (!TryCollectNumbers(
                [invocation.Arguments[0]],
                CollectionMode.Standard,
                out var values,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var number,
                out error))
        {
            return error;
        }
        if (values.Length < 2)
        {
            return NotAvailable();
        }

        var significance = 3;
        if (invocation.Arguments.Count == 3 &&
            !TryGetTruncatedInteger(
                invocation.Arguments[2],
                out significance,
                out error))
        {
            return error;
        }
        if (significance is < 1 or > MaximumSignificance)
        {
            return NumericError();
        }

        Array.Sort(values);
        if (number < values[0] || number > values[^1])
        {
            return NotAvailable();
        }

        var firstEqual = Array.BinarySearch(values, number);
        double position;
        if (firstEqual >= 0)
        {
            var first = firstEqual;
            var last = firstEqual;
            while (first > 0 && values[first - 1].Equals(number))
            {
                first--;
            }
            while (last + 1 < values.Length && values[last + 1].Equals(number))
            {
                last++;
            }
            position = (first + last) / 2d;
        }
        else
        {
            var upper = ~firstEqual;
            var lower = upper - 1;
            var span = values[upper] - values[lower];
            var fraction = span == 0d
                ? 0d
                : (number - values[lower]) / span;
            position = lower + fraction;
        }

        var result = exclusive
            ? (position + 1d) / (values.Length + 1d)
            : position / (values.Length - 1d);
        return Number(Math.Round(
            result,
            significance,
            MidpointRounding.AwayFromZero));
    }
}
