using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class StatisticalFormulaFunctions
{
    public const int MaximumStatisticalValues = 2_000_000;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition(
            "MEDIAN",
            1,
            int.MaxValue,
            EvaluateMedian);
        yield return CreateDefinition(
            "MODE.SNGL",
            1,
            int.MaxValue,
            EvaluateModeSingle);
        yield return CreateDefinition(
            "PERCENTILE.INC",
            2,
            2,
            EvaluatePercentileInclusive);
        yield return CreateDefinition(
            "QUARTILE.INC",
            2,
            2,
            EvaluateQuartileInclusive);
        yield return CreateDefinition(
            "VAR.P",
            1,
            int.MaxValue,
            static invocation => EvaluateVariance(
                invocation,
                sample: false,
                squareRoot: false));
        yield return CreateDefinition(
            "VAR.S",
            1,
            int.MaxValue,
            static invocation => EvaluateVariance(
                invocation,
                sample: true,
                squareRoot: false));
        yield return CreateDefinition(
            "STDEV.P",
            1,
            int.MaxValue,
            static invocation => EvaluateVariance(
                invocation,
                sample: false,
                squareRoot: true));
        yield return CreateDefinition(
            "STDEV.S",
            1,
            int.MaxValue,
            static invocation => EvaluateVariance(
                invocation,
                sample: true,
                squareRoot: true));
        yield return CreateDefinition(
            "RANK.EQ",
            2,
            3,
            EvaluateRankEqual);
        yield return CreateDefinition(
            "LARGE",
            2,
            2,
            static invocation => EvaluateOrderStatistic(
                invocation,
                largest: true));
        yield return CreateDefinition(
            "SMALL",
            2,
            2,
            static invocation => EvaluateOrderStatistic(
                invocation,
                largest: false));
    }

    private static IFormulaFunction CreateDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "NERA.BUILTIN",
                    name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaEvaluationResult EvaluateMedian(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                invocation.Arguments,
                out var numbers,
                out var error))
        {
            return error;
        }
        if (numbers.Length == 0)
        {
            return NumericError();
        }

        Array.Sort(numbers);
        var middle = numbers.Length / 2;
        var result = (numbers.Length & 1) == 1
            ? numbers[middle]
            : (numbers[middle - 1] / 2d) +
              (numbers[middle] / 2d);
        return Number(result);
    }

    private static FormulaEvaluationResult EvaluateModeSingle(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                invocation.Arguments,
                out var numbers,
                out var error))
        {
            return error;
        }
        if (numbers.Length == 0)
        {
            return NotAvailable();
        }

        var counts = new Dictionary<double, int>();
        foreach (var number in numbers)
        {
            counts[number] = counts.TryGetValue(number, out var count)
                ? checked(count + 1)
                : 1;
        }

        var maximumCount = counts.Values.Max();
        if (maximumCount < 2)
        {
            return NotAvailable();
        }
        return Number(counts
            .Where(pair => pair.Value == maximumCount)
            .Min(static pair => pair.Key));
    }

    private static FormulaEvaluationResult EvaluatePercentileInclusive(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                [invocation.Arguments[0]],
                out var numbers,
                out var error))
        {
            return error;
        }
        if (numbers.Length == 0 ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var percentile,
                out error))
        {
            return numbers.Length == 0
                ? NumericError()
                : error;
        }
        return EvaluatePercentile(numbers, percentile);
    }

    private static FormulaEvaluationResult EvaluateQuartileInclusive(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                [invocation.Arguments[0]],
                out var numbers,
                out var error))
        {
            return error;
        }
        if (numbers.Length == 0)
        {
            return NumericError();
        }
        if (!TryGetScalarInteger(
                invocation.Arguments[1],
                out var quartile,
                out error))
        {
            return error;
        }
        if (quartile is < 0 or > 4)
        {
            return NumericError();
        }
        return EvaluatePercentile(numbers, quartile / 4d);
    }

    private static FormulaEvaluationResult EvaluatePercentile(
        double[] numbers,
        double percentile)
    {
        if (!double.IsFinite(percentile) ||
            percentile is < 0d or > 1d)
        {
            return NumericError();
        }

        Array.Sort(numbers);
        if (numbers.Length == 1)
        {
            return Number(numbers[0]);
        }

        var position = (numbers.Length - 1d) * percentile;
        var lowerIndex = checked((int)Math.Floor(position));
        var upperIndex = checked((int)Math.Ceiling(position));
        if (lowerIndex == upperIndex)
        {
            return Number(numbers[lowerIndex]);
        }

        var fraction = position - lowerIndex;
        var result = numbers[lowerIndex] +
                     ((numbers[upperIndex] - numbers[lowerIndex]) * fraction);
        return Number(result);
    }

    private static FormulaEvaluationResult EvaluateVariance(
        FormulaFunctionInvocation invocation,
        bool sample,
        bool squareRoot)
    {
        if (!TryCollectNumbers(
                invocation.Arguments,
                out var numbers,
                out var error))
        {
            return error;
        }
        var minimumCount = sample ? 2 : 1;
        if (numbers.Length < minimumCount)
        {
            return DivisionByZero();
        }

        var mean = 0d;
        var sumOfSquares = 0d;
        var count = 0L;
        foreach (var number in numbers)
        {
            count++;
            var delta = number - mean;
            mean += delta / count;
            var deltaAfterMean = number - mean;
            sumOfSquares += delta * deltaAfterMean;
            if (!double.IsFinite(mean) ||
                !double.IsFinite(sumOfSquares))
            {
                return NumericError();
            }
        }

        var denominator = sample ? count - 1d : count;
        var variance = sumOfSquares / denominator;
        if (variance < 0d && variance > -1e-12d)
        {
            variance = 0d;
        }
        if (variance < 0d || !double.IsFinite(variance))
        {
            return NumericError();
        }
        return Number(squareRoot ? Math.Sqrt(variance) : variance);
    }

    private static FormulaEvaluationResult EvaluateRankEqual(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var number,
                out var error))
        {
            return error;
        }
        if (!TryCollectNumbers(
                [invocation.Arguments[1]],
                out var reference,
                out error))
        {
            return error;
        }
        if (reference.Length == 0)
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
            ascending = Math.Abs(order) > double.Epsilon;
        }

        var preceding = ascending
            ? reference.LongCount(value => value < number)
            : reference.LongCount(value => value > number);
        return Number(checked(preceding + 1L));
    }

    private static FormulaEvaluationResult EvaluateOrderStatistic(
        FormulaFunctionInvocation invocation,
        bool largest)
    {
        if (!TryCollectNumbers(
                [invocation.Arguments[0]],
                out var numbers,
                out var error))
        {
            return error;
        }
        if (!TryGetScalarInteger(
                invocation.Arguments[1],
                out var rank,
                out error))
        {
            return error;
        }
        if (rank <= 0 || rank > numbers.Length)
        {
            return NumericError();
        }

        Array.Sort(numbers);
        var index = largest
            ? numbers.Length - rank
            : rank - 1;
        return Number(numbers[index]);
    }

    private static bool TryCollectNumbers(
        IReadOnlyList<FormulaFunctionArgument> arguments,
        out double[] numbers,
        out FormulaEvaluationResult error)
    {
        var values = new List<double>();
        foreach (var argument in arguments)
        {
            foreach (var value in argument.Values)
            {
                if (value.Kind is
                    CellValueKind.Number or CellValueKind.DateTime)
                {
                    if (!FormulaValueCoercion.TryNumber(
                            value,
                            out var number) ||
                        !double.IsFinite(number))
                    {
                        numbers = [];
                        error = NumericError();
                        return false;
                    }
                    values.Add(number);
                }
                else if (argument.Kind ==
                         FormulaFunctionArgumentKind.Scalar)
                {
                    if (value.Kind == CellValueKind.Boolean)
                    {
                        values.Add((bool)value.RawValue! ? 1d : 0d);
                    }
                    else if (value.Kind == CellValueKind.Text)
                    {
                        if (!FormulaValueCoercion.TryNumber(
                                value,
                                out var number,
                                allowText: true))
                        {
                            numbers = [];
                            error = InvalidValue();
                            return false;
                        }
                        values.Add(number);
                    }
                }

                if (values.Count > MaximumStatisticalValues)
                {
                    numbers = [];
                    error = NumericError();
                    return false;
                }
            }
        }

        numbers = values.ToArray();
        error = default!;
        return true;
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

    private static FormulaEvaluationResult Number(double value) =>
        FormulaEvaluationResult.Success(
            FormulaValueCoercion.SafeNumber(value));

    private static FormulaEvaluationResult Number(long value) =>
        FormulaEvaluationResult.Success(
            CellValue.FromNumber(value));

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(
            FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult DivisionByZero() =>
        FormulaEvaluationResult.Failure(
            FormulaErrorCode.DivisionByZero);

    private static FormulaEvaluationResult NotAvailable() =>
        FormulaEvaluationResult.Failure(
            FormulaErrorCode.NotAvailable);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());
}
