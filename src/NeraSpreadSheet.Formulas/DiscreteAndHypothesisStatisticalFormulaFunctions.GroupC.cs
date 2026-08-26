using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class DiscreteAndHypothesisStatisticalFormulaFunctionsGroupC
{
    private const int MaximumValues = 2_000_000;
    private const int MaximumDiscreteTerms = 1_000_000;

    private static readonly IVersionedFormulaFunction BinomialDistributionTarget =
        AdvancedStatisticalFormulaFunctions.Create()
            .OfType<IVersionedFormulaFunction>()
            .Single(static function =>
                string.Equals(
                    function.Name,
                    "BINOM.DIST",
                    StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateScalarDefinition("BINOM.INV", 3, 3, EvaluateBinomialInverse);
        yield return CreateScalarDefinition(
            "NEGBINOM.DIST",
            4,
            4,
            static invocation => EvaluateNegativeBinomial(invocation, legacyMassOnly: false));
        yield return CreateScalarDefinition(
            "HYPGEOM.DIST",
            5,
            5,
            static invocation => EvaluateHypergeometric(invocation, legacyMassOnly: false));
        yield return CreateRangeDefinition("F.TEST", 2, 2, EvaluateFTest);
        yield return CreateRangeDefinition("Z.TEST", 2, 3, EvaluateZTest);

        yield return CreateScalarDefinition("CRITBINOM", 3, 3, EvaluateBinomialInverse);
        yield return CreateScalarDefinition(
            "NEGBINOMDIST",
            3,
            3,
            static invocation => EvaluateNegativeBinomial(invocation, legacyMassOnly: true));
        yield return CreateScalarDefinition(
            "HYPGEOMDIST",
            4,
            4,
            static invocation => EvaluateHypergeometric(invocation, legacyMassOnly: true));
        yield return CreateRangeDefinition("FTEST", 2, 2, EvaluateFTest);
        yield return CreateRangeDefinition("ZTEST", 2, 3, EvaluateZTest);
    }

    private static FormulaFunctionDefinition CreateScalarDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        CreateDefinition(
            name,
            minimumArguments,
            maximumArguments,
            FormulaFunctionCapabilities.ScalarArguments,
            evaluator);

    private static FormulaFunctionDefinition CreateRangeDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        CreateDefinition(
            name,
            minimumArguments,
            maximumArguments,
            FormulaFunctionCapabilities.ScalarArguments |
            FormulaFunctionCapabilities.RangeArguments,
            evaluator);

    private static FormulaFunctionDefinition CreateDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        FormulaFunctionCapabilities argumentCapabilities,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                argumentCapabilities |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaEvaluationResult EvaluateBinomialInverse(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetTruncatedInteger(invocation.Arguments[0], out var trials, out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var probability, out error) ||
            !TryGetScalarNumber(invocation.Arguments[2], out var alpha, out error))
        {
            return error;
        }
        if (trials < 0 || probability < 0d || probability > 1d ||
            alpha < 0d || alpha > 1d)
        {
            return NumericError();
        }
        if (alpha == 0d || probability == 0d)
        {
            return Number(0d);
        }
        if (alpha == 1d || probability == 1d)
        {
            return Number(trials);
        }

        var lower = 0;
        var upper = trials;
        while (lower < upper)
        {
            var middle = lower + ((upper - lower) / 2);
            var cumulative = BinomialDistributionTarget.Invoke(
                new FormulaFunctionInvocation(
                    [
                        FormulaFunctionArgument.Scalar(CellValue.FromNumber(middle)),
                        FormulaFunctionArgument.Scalar(CellValue.FromNumber(trials)),
                        FormulaFunctionArgument.Scalar(CellValue.FromNumber(probability)),
                        FormulaFunctionArgument.Scalar(CellValue.FromBoolean(true)),
                    ],
                    invocation.Context));
            if (!cumulative.IsSuccess)
            {
                return cumulative;
            }
            if (cumulative.Value.Kind != CellValueKind.Number)
            {
                return InvalidValue();
            }
            var value = (double)cumulative.Value.RawValue!;
            if (value >= alpha)
            {
                upper = middle;
            }
            else
            {
                lower = middle + 1;
            }
        }
        return Number(lower);
    }

    private static FormulaEvaluationResult EvaluateNegativeBinomial(
        FormulaFunctionInvocation invocation,
        bool legacyMassOnly)
    {
        if (!TryGetTruncatedInteger(invocation.Arguments[0], out var failures, out var error) ||
            !TryGetTruncatedInteger(invocation.Arguments[1], out var successes, out error) ||
            !TryGetScalarNumber(invocation.Arguments[2], out var probability, out error))
        {
            return error;
        }
        var cumulative = false;
        if (!legacyMassOnly &&
            !TryGetScalarBoolean(invocation.Arguments[3], out cumulative, out error))
        {
            return error;
        }
        if (failures < 0 || successes < 1 ||
            probability < 0d || probability > 1d)
        {
            return NumericError();
        }
        if (probability == 0d)
        {
            return Number(0d);
        }
        if (probability == 1d)
        {
            return Number(cumulative || failures == 0 ? 1d : 0d);
        }
        if (cumulative)
        {
            return AdvancedDistributionNumerics.TryRegularizedBeta(
                    successes,
                    failures + 1d,
                    probability,
                    out var result)
                ? Number(result)
                : NotAvailable();
        }

        var logProbability =
            StatisticalNumerics.LogGamma(failures + successes) -
            StatisticalNumerics.LogGamma(successes) -
            StatisticalNumerics.LogGamma(failures + 1d) +
            (successes * Math.Log(probability)) +
            (failures * Math.Log(1d - probability));
        return Number(Math.Exp(logProbability));
    }

    private static FormulaEvaluationResult EvaluateHypergeometric(
        FormulaFunctionInvocation invocation,
        bool legacyMassOnly)
    {
        if (!TryGetTruncatedInteger(invocation.Arguments[0], out var sampleSuccesses, out var error) ||
            !TryGetTruncatedInteger(invocation.Arguments[1], out var sampleSize, out error) ||
            !TryGetTruncatedInteger(invocation.Arguments[2], out var populationSuccesses, out error) ||
            !TryGetTruncatedInteger(invocation.Arguments[3], out var populationSize, out error))
        {
            return error;
        }
        var cumulative = false;
        if (!legacyMassOnly &&
            !TryGetScalarBoolean(invocation.Arguments[4], out cumulative, out error))
        {
            return error;
        }
        if (!TryGetHypergeometricBounds(
                sampleSize,
                populationSuccesses,
                populationSize,
                out var minimum,
                out var maximum) ||
            sampleSuccesses < minimum || sampleSuccesses > maximum)
        {
            return NumericError();
        }
        if (!cumulative)
        {
            return Number(Math.Exp(LogHypergeometricProbability(
                sampleSuccesses,
                sampleSize,
                populationSuccesses,
                populationSize)));
        }

        var terms = sampleSuccesses - minimum + 1L;
        if (terms > MaximumDiscreteTerms)
        {
            return NumericError();
        }
        var accumulator = new LogSumAccumulator();
        for (var successes = minimum; successes <= sampleSuccesses; successes++)
        {
            accumulator.Add(LogHypergeometricProbability(
                successes,
                sampleSize,
                populationSuccesses,
                populationSize));
        }
        return Number(Math.Clamp(accumulator.Value, 0d, 1d));
    }

    private static FormulaEvaluationResult EvaluateFTest(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(invocation.Arguments[0], out var first, out var error) ||
            !TryCollectNumbers(invocation.Arguments[1], out var second, out error))
        {
            return error;
        }
        if (first.Length < 2 || second.Length < 2)
        {
            return DivisionByZero();
        }
        var firstVariance = SampleVariance(first);
        var secondVariance = SampleVariance(second);
        if (firstVariance <= 0d || secondVariance <= 0d)
        {
            return DivisionByZero();
        }

        var ratio = firstVariance / secondVariance;
        if (!AdvancedDistributionNumerics.TryFCumulative(
                ratio,
                first.Length - 1d,
                second.Length - 1d,
                out var cumulative))
        {
            return NotAvailable();
        }
        var probability = 2d * Math.Min(cumulative, 1d - cumulative);
        return Number(Math.Clamp(probability, 0d, 1d));
    }

    private static FormulaEvaluationResult EvaluateZTest(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(invocation.Arguments[0], out var values, out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var hypothesizedMean, out error))
        {
            return error;
        }
        if (values.Length == 0)
        {
            return NotAvailable();
        }

        double standardDeviation;
        if (invocation.Arguments.Count == 3)
        {
            if (!TryGetScalarNumber(
                    invocation.Arguments[2],
                    out standardDeviation,
                    out error))
            {
                return error;
            }
            if (standardDeviation <= 0d)
            {
                return NumericError();
            }
        }
        else
        {
            if (values.Length < 2)
            {
                return DivisionByZero();
            }
            standardDeviation = Math.Sqrt(SampleVariance(values));
            if (standardDeviation <= 0d)
            {
                return DivisionByZero();
            }
        }

        var mean = Mean(values);
        var z = (mean - hypothesizedMean) /
                (standardDeviation / Math.Sqrt(values.Length));
        return Number(1d - StatisticalNumerics.NormalCumulative(z));
    }

    private static bool TryGetHypergeometricBounds(
        int sampleSize,
        int populationSuccesses,
        int populationSize,
        out int minimum,
        out int maximum)
    {
        if (populationSize < 0 || populationSuccesses < 0 ||
            populationSuccesses > populationSize ||
            sampleSize < 0 || sampleSize > populationSize)
        {
            minimum = default;
            maximum = default;
            return false;
        }
        minimum = Math.Max(0, sampleSize - (populationSize - populationSuccesses));
        maximum = Math.Min(sampleSize, populationSuccesses);
        return true;
    }

    private static double LogHypergeometricProbability(
        int sampleSuccesses,
        int sampleSize,
        int populationSuccesses,
        int populationSize) =>
        LogCombination(populationSuccesses, sampleSuccesses) +
        LogCombination(
            populationSize - populationSuccesses,
            sampleSize - sampleSuccesses) -
        LogCombination(populationSize, sampleSize);

    private static double LogCombination(int total, int selected) =>
        StatisticalNumerics.LogGamma(total + 1d) -
        StatisticalNumerics.LogGamma(selected + 1d) -
        StatisticalNumerics.LogGamma(total - selected + 1d);

    private static bool TryCollectNumbers(
        FormulaFunctionArgument argument,
        out double[] values,
        out FormulaEvaluationResult error)
    {
        var collected = new List<double>();
        var direct = argument.Kind == FormulaFunctionArgumentKind.Scalar;
        foreach (var value in argument.Values)
        {
            if (value.Kind is CellValueKind.Number or CellValueKind.DateTime)
            {
                if (!FormulaValueCoercion.TryNumber(value, out var number) ||
                    !double.IsFinite(number))
                {
                    values = [];
                    error = NumericError();
                    return false;
                }
                collected.Add(number);
            }
            else if (direct && value.Kind == CellValueKind.Boolean)
            {
                collected.Add((bool)value.RawValue! ? 1d : 0d);
            }
            else if (direct && value.Kind == CellValueKind.Text)
            {
                if (!FormulaValueCoercion.TryNumber(
                        value,
                        out var number,
                        allowText: true))
                {
                    values = [];
                    error = InvalidValue();
                    return false;
                }
                collected.Add(number);
            }
            if (collected.Count > MaximumValues)
            {
                values = [];
                error = NumericError();
                return false;
            }
        }
        values = collected.ToArray();
        error = default!;
        return true;
    }

    private static double Mean(double[] values)
    {
        var sum = 0d;
        var compensation = 0d;
        foreach (var value in values)
        {
            AddCompensated(value, ref sum, ref compensation);
        }
        return sum / values.Length;
    }

    private static double SampleVariance(double[] values)
    {
        var mean = Mean(values);
        var sum = 0d;
        var compensation = 0d;
        foreach (var value in values)
        {
            var deviation = value - mean;
            AddCompensated(deviation * deviation, ref sum, ref compensation);
        }
        return sum / (values.Length - 1d);
    }

    private static void AddCompensated(
        double value,
        ref double sum,
        ref double compensation)
    {
        var corrected = value - compensation;
        var updated = sum + corrected;
        compensation = (updated - sum) - corrected;
        sum = updated;
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

    private static bool TryGetScalarBoolean(
        FormulaFunctionArgument argument,
        out bool value,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryBoolean(argument.ScalarValue, out value))
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
        if (!TryGetScalarNumber(argument, out var number, out error))
        {
            value = default;
            return false;
        }
        if (number < int.MinValue || number > int.MaxValue)
        {
            value = default;
            error = NumericError();
            return false;
        }
        value = checked((int)Math.Truncate(number));
        return true;
    }

    private static FormulaEvaluationResult Number(double value) =>
        double.IsFinite(value)
            ? FormulaEvaluationResult.Success(CellValue.FromNumber(value))
            : NumericError();

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult DivisionByZero() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.DivisionByZero);

    private static FormulaEvaluationResult NotAvailable() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.NotAvailable);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());

    private sealed class LogSumAccumulator
    {
        private double _maximum = double.NegativeInfinity;
        private double _scaled;

        public void Add(double logarithm)
        {
            if (double.IsNegativeInfinity(_maximum))
            {
                _maximum = logarithm;
                _scaled = 1d;
                return;
            }
            if (logarithm <= _maximum)
            {
                _scaled += Math.Exp(logarithm - _maximum);
            }
            else
            {
                _scaled = (_scaled * Math.Exp(_maximum - logarithm)) + 1d;
                _maximum = logarithm;
            }
        }

        public double Value =>
            double.IsNegativeInfinity(_maximum)
                ? 0d
                : Math.Exp(_maximum) * _scaled;
    }
}
