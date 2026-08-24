using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Deterministic covariance, regression and first-generation probability
/// distribution functions. Pairwise calculations use stable online moments;
/// inverse and cumulative distributions use bounded numerical primitives.
/// </summary>
internal static class AdvancedStatisticalFormulaFunctions
{
    public const int MaximumPairedValues = 2_000_000;
    public const int MaximumDiscreteTerms = 1_000_000;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateRangeDefinition(
            "COVARIANCE.P",
            2,
            2,
            static invocation => EvaluateCovariance(invocation, sample: false));
        yield return CreateRangeDefinition(
            "COVARIANCE.S",
            2,
            2,
            static invocation => EvaluateCovariance(invocation, sample: true));
        yield return CreateRangeDefinition(
            "CORREL",
            2,
            2,
            EvaluateCorrelation);
        yield return CreateRangeDefinition(
            "PEARSON",
            2,
            2,
            EvaluateCorrelation);
        yield return CreateRangeDefinition(
            "SLOPE",
            2,
            2,
            EvaluateSlope);
        yield return CreateRangeDefinition(
            "INTERCEPT",
            2,
            2,
            EvaluateIntercept);
        yield return CreateRangeDefinition(
            "RSQ",
            2,
            2,
            EvaluateRSquared);
        yield return CreateRangeDefinition(
            "STEYX",
            2,
            2,
            EvaluateStandardError);
        yield return CreateRangeDefinition(
            "FORECAST.LINEAR",
            3,
            3,
            EvaluateForecastLinear);

        yield return CreateScalarDefinition(
            "STANDARDIZE",
            3,
            3,
            EvaluateStandardize);
        yield return CreateScalarDefinition(
            "FISHER",
            1,
            1,
            EvaluateFisher);
        yield return CreateScalarDefinition(
            "FISHERINV",
            1,
            1,
            EvaluateFisherInverse);

        yield return CreateScalarDefinition(
            "NORM.DIST",
            4,
            4,
            EvaluateNormalDistribution);
        yield return CreateScalarDefinition(
            "NORM.S.DIST",
            2,
            2,
            EvaluateStandardNormalDistribution);
        yield return CreateScalarDefinition(
            "NORM.INV",
            3,
            3,
            EvaluateNormalInverse);
        yield return CreateScalarDefinition(
            "NORM.S.INV",
            1,
            1,
            EvaluateStandardNormalInverse);
        yield return CreateScalarDefinition(
            "LOGNORM.DIST",
            4,
            4,
            EvaluateLogNormalDistribution);
        yield return CreateScalarDefinition(
            "LOGNORM.INV",
            3,
            3,
            EvaluateLogNormalInverse);
        yield return CreateScalarDefinition(
            "EXPON.DIST",
            3,
            3,
            EvaluateExponentialDistribution);
        yield return CreateScalarDefinition(
            "BINOM.DIST",
            4,
            4,
            EvaluateBinomialDistribution);
        yield return CreateScalarDefinition(
            "POISSON.DIST",
            3,
            3,
            EvaluatePoissonDistribution);
        yield return CreateScalarDefinition(
            "WEIBULL.DIST",
            4,
            4,
            EvaluateWeibullDistribution);
    }

    private static FormulaFunctionDefinition CreateRangeDefinition(
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
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaFunctionDefinition CreateScalarDefinition(
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

    private static FormulaEvaluationResult EvaluateCovariance(
        FormulaFunctionInvocation invocation,
        bool sample)
    {
        if (!TryCollectPairs(
                invocation.Arguments[0],
                invocation.Arguments[1],
                out var statistics,
                out var error))
        {
            return error;
        }
        var minimumCount = sample ? 2L : 1L;
        if (statistics.Count < minimumCount)
        {
            return DivisionByZero();
        }
        var denominator = sample
            ? statistics.Count - 1d
            : statistics.Count;
        return Number(statistics.CoMoment / denominator);
    }

    private static FormulaEvaluationResult EvaluateCorrelation(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectPairs(
                invocation.Arguments[0],
                invocation.Arguments[1],
                out var statistics,
                out var error))
        {
            return error;
        }
        if (statistics.Count < 2L ||
            statistics.M2X <= 0d ||
            statistics.M2Y <= 0d)
        {
            return DivisionByZero();
        }
        var denominator = Math.Sqrt(
            statistics.M2X * statistics.M2Y);
        return Number(ClampCorrelation(
            statistics.CoMoment / denominator));
    }

    private static FormulaEvaluationResult EvaluateSlope(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectRegressionPairs(
                invocation,
                out var statistics,
                out var error))
        {
            return error;
        }
        if (statistics.Count < 2L || statistics.M2X <= 0d)
        {
            return DivisionByZero();
        }
        return Number(statistics.CoMoment / statistics.M2X);
    }

    private static FormulaEvaluationResult EvaluateIntercept(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectRegressionPairs(
                invocation,
                out var statistics,
                out var error))
        {
            return error;
        }
        if (statistics.Count < 2L || statistics.M2X <= 0d)
        {
            return DivisionByZero();
        }
        var slope = statistics.CoMoment / statistics.M2X;
        return Number(statistics.MeanY - (slope * statistics.MeanX));
    }

    private static FormulaEvaluationResult EvaluateRSquared(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectRegressionPairs(
                invocation,
                out var statistics,
                out var error))
        {
            return error;
        }
        if (statistics.Count < 2L ||
            statistics.M2X <= 0d ||
            statistics.M2Y <= 0d)
        {
            return DivisionByZero();
        }
        var correlation = ClampCorrelation(
            statistics.CoMoment /
            Math.Sqrt(statistics.M2X * statistics.M2Y));
        return Number(correlation * correlation);
    }

    private static FormulaEvaluationResult EvaluateStandardError(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectRegressionPairs(
                invocation,
                out var statistics,
                out var error))
        {
            return error;
        }
        if (statistics.Count < 3L || statistics.M2X <= 0d)
        {
            return DivisionByZero();
        }
        var residual = statistics.M2Y -
                       ((statistics.CoMoment * statistics.CoMoment) /
                        statistics.M2X);
        if (residual < 0d && residual > -1e-12d *
            Math.Max(1d, statistics.M2Y))
        {
            residual = 0d;
        }
        if (residual < 0d || !double.IsFinite(residual))
        {
            return NumericError();
        }
        return Number(Math.Sqrt(
            residual / (statistics.Count - 2d)));
    }

    private static FormulaEvaluationResult EvaluateForecastLinear(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var x,
                out var error) ||
            !TryCollectPairs(
                invocation.Arguments[2],
                invocation.Arguments[1],
                out var statistics,
                out error))
        {
            return error;
        }
        if (statistics.Count < 2L || statistics.M2X <= 0d)
        {
            return DivisionByZero();
        }
        var slope = statistics.CoMoment / statistics.M2X;
        var intercept = statistics.MeanY -
                        (slope * statistics.MeanX);
        return Number(intercept + (slope * x));
    }

    private static FormulaEvaluationResult EvaluateStandardize(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var mean, out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var standardDeviation,
                out error))
        {
            return error;
        }
        if (standardDeviation <= 0d)
        {
            return NumericError();
        }
        return Number((x - mean) / standardDeviation);
    }

    private static FormulaEvaluationResult EvaluateFisher(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var value,
                out var error))
        {
            return error;
        }
        if (value <= -1d || value >= 1d)
        {
            return NumericError();
        }
        return Number(0.5d * Math.Log((1d + value) / (1d - value)));
    }

    private static FormulaEvaluationResult EvaluateFisherInverse(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var value,
                out var error))
        {
            return error;
        }
        return Number(Math.Tanh(value));
    }

    private static FormulaEvaluationResult EvaluateNormalDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var mean, out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var standardDeviation,
                out error) ||
            !TryGetScalarBoolean(
                invocation.Arguments[3],
                out var cumulative,
                out error))
        {
            return error;
        }
        if (standardDeviation <= 0d)
        {
            return NumericError();
        }
        var standardized = (x - mean) / standardDeviation;
        return Number(cumulative
            ? StatisticalNumerics.NormalCumulative(standardized)
            : StatisticalNumerics.NormalDensity(standardized) /
              standardDeviation);
    }

    private static FormulaEvaluationResult EvaluateStandardNormalDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var x,
                out var error) ||
            !TryGetScalarBoolean(
                invocation.Arguments[1],
                out var cumulative,
                out error))
        {
            return error;
        }
        return Number(cumulative
            ? StatisticalNumerics.NormalCumulative(x)
            : StatisticalNumerics.NormalDensity(x));
    }

    private static FormulaEvaluationResult EvaluateNormalInverse(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var probability,
                out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var mean, out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var standardDeviation,
                out error))
        {
            return error;
        }
        if (standardDeviation <= 0d ||
            !StatisticalNumerics.TryInverseNormal(
                probability,
                out var standardized))
        {
            return NumericError();
        }
        return Number(mean + (standardDeviation * standardized));
    }

    private static FormulaEvaluationResult EvaluateStandardNormalInverse(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var probability,
                out var error))
        {
            return error;
        }
        return StatisticalNumerics.TryInverseNormal(
                probability,
                out var value)
            ? Number(value)
            : NumericError();
    }

    private static FormulaEvaluationResult EvaluateLogNormalDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var mean, out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var standardDeviation,
                out error) ||
            !TryGetScalarBoolean(
                invocation.Arguments[3],
                out var cumulative,
                out error))
        {
            return error;
        }
        if (x <= 0d || standardDeviation <= 0d)
        {
            return NumericError();
        }
        var standardized = (Math.Log(x) - mean) / standardDeviation;
        return Number(cumulative
            ? StatisticalNumerics.NormalCumulative(standardized)
            : StatisticalNumerics.NormalDensity(standardized) /
              (x * standardDeviation));
    }

    private static FormulaEvaluationResult EvaluateLogNormalInverse(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var probability,
                out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var mean, out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var standardDeviation,
                out error))
        {
            return error;
        }
        if (standardDeviation <= 0d ||
            !StatisticalNumerics.TryInverseNormal(
                probability,
                out var standardized))
        {
            return NumericError();
        }
        return Number(Math.Exp(mean +
                               (standardDeviation * standardized)));
    }

    private static FormulaEvaluationResult EvaluateExponentialDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var lambda, out error) ||
            !TryGetScalarBoolean(
                invocation.Arguments[2],
                out var cumulative,
                out error))
        {
            return error;
        }
        if (x < 0d || lambda <= 0d)
        {
            return NumericError();
        }
        var exponent = Math.Exp(-lambda * x);
        return Number(cumulative
            ? 1d - exponent
            : lambda * exponent);
    }

    private static FormulaEvaluationResult EvaluateBinomialDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetTruncatedInteger(
                invocation.Arguments[0],
                out var successes,
                out var error) ||
            !TryGetTruncatedInteger(
                invocation.Arguments[1],
                out var trials,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var probability,
                out error) ||
            !TryGetScalarBoolean(
                invocation.Arguments[3],
                out var cumulative,
                out error))
        {
            return error;
        }
        if (successes < 0 || trials < 0 || successes > trials ||
            probability < 0d || probability > 1d)
        {
            return NumericError();
        }
        if (probability == 0d)
        {
            return Number(cumulative || successes == 0 ? 1d : 0d);
        }
        if (probability == 1d)
        {
            return Number(cumulative
                ? successes == trials ? 1d : 0d
                : successes == trials ? 1d : 0d);
        }
        if (!cumulative)
        {
            return Number(Math.Exp(LogBinomialProbability(
                trials,
                successes,
                probability)));
        }

        var mean = trials * probability;
        if (successes < mean)
        {
            var terms = (long)successes + 1L;
            if (terms > MaximumDiscreteTerms)
            {
                return NumericError();
            }
            var accumulator = new LogSumAccumulator();
            var logProbability = trials * Math.Log(1d - probability);
            for (var index = 0; index <= successes; index++)
            {
                accumulator.Add(logProbability);
                if (index == successes)
                {
                    break;
                }
                logProbability +=
                    Math.Log(trials - index) -
                    Math.Log(index + 1d) +
                    Math.Log(probability) -
                    Math.Log(1d - probability);
            }
            return Number(Math.Clamp(accumulator.Value, 0d, 1d));
        }

        var upperTerms = (long)trials - successes;
        if (upperTerms > MaximumDiscreteTerms)
        {
            return NumericError();
        }
        var upperAccumulator = new LogSumAccumulator();
        var upperLogProbability = trials * Math.Log(probability);
        for (var index = trials; index > successes; index--)
        {
            upperAccumulator.Add(upperLogProbability);
            if (index == successes + 1)
            {
                break;
            }
            upperLogProbability +=
                Math.Log(index) -
                Math.Log(trials - index + 1d) +
                Math.Log(1d - probability) -
                Math.Log(probability);
        }
        return Number(Math.Clamp(1d - upperAccumulator.Value, 0d, 1d));
    }

    private static FormulaEvaluationResult EvaluatePoissonDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetTruncatedInteger(
                invocation.Arguments[0],
                out var events,
                out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var mean, out error) ||
            !TryGetScalarBoolean(
                invocation.Arguments[2],
                out var cumulative,
                out error))
        {
            return error;
        }
        if (events < 0 || mean <= 0d)
        {
            return NumericError();
        }
        if (!cumulative)
        {
            return Number(Math.Exp(
                -mean +
                (events * Math.Log(mean)) -
                StatisticalNumerics.LogGamma(events + 1d)));
        }
        return StatisticalNumerics.TryRegularizedGammaQ(
                events + 1d,
                mean,
                out var probability)
            ? Number(probability)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateWeibullDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var alpha, out error) ||
            !TryGetScalarNumber(invocation.Arguments[2], out var beta, out error) ||
            !TryGetScalarBoolean(
                invocation.Arguments[3],
                out var cumulative,
                out error))
        {
            return error;
        }
        if (x < 0d || alpha <= 0d || beta <= 0d)
        {
            return NumericError();
        }
        if (x == 0d)
        {
            if (cumulative || alpha > 1d)
            {
                return Number(0d);
            }
            if (alpha == 1d)
            {
                return Number(1d / beta);
            }
            return NumericError();
        }
        var scaled = x / beta;
        var power = Math.Pow(scaled, alpha);
        var exponent = Math.Exp(-power);
        return Number(cumulative
            ? 1d - exponent
            : (alpha / beta) *
              Math.Pow(scaled, alpha - 1d) *
              exponent);
    }

    private static bool TryCollectRegressionPairs(
        FormulaFunctionInvocation invocation,
        out BivariateStatistics statistics,
        out FormulaEvaluationResult error) =>
        TryCollectPairs(
            invocation.Arguments[1],
            invocation.Arguments[0],
            out statistics,
            out error);

    private static bool TryCollectPairs(
        FormulaFunctionArgument xArgument,
        FormulaFunctionArgument yArgument,
        out BivariateStatistics statistics,
        out FormulaEvaluationResult error)
    {
        statistics = default;
        if (xArgument.Values.Count != yArgument.Values.Count)
        {
            error = NotAvailable();
            return false;
        }
        if (xArgument.Values.Count > MaximumPairedValues)
        {
            error = NumericError();
            return false;
        }

        for (var index = 0;
             index < xArgument.Values.Count;
             index++)
        {
            if (!TryGetPairNumber(
                    xArgument,
                    xArgument.Values[index],
                    out var x,
                    out var skipX,
                    out error) ||
                !TryGetPairNumber(
                    yArgument,
                    yArgument.Values[index],
                    out var y,
                    out var skipY,
                    out error))
            {
                return false;
            }
            if (skipX || skipY)
            {
                continue;
            }
            statistics.Add(x, y);
            if (!statistics.IsFinite)
            {
                error = NumericError();
                return false;
            }
        }
        error = default!;
        return true;
    }

    private static bool TryGetPairNumber(
        FormulaFunctionArgument argument,
        CellValue value,
        out double number,
        out bool skip,
        out FormulaEvaluationResult error)
    {
        if (value.Kind is CellValueKind.Number or CellValueKind.DateTime)
        {
            if (FormulaValueCoercion.TryNumber(value, out number) &&
                double.IsFinite(number))
            {
                skip = false;
                error = default!;
                return true;
            }
            number = default;
            skip = false;
            error = NumericError();
            return false;
        }
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar)
        {
            number = default;
            skip = true;
            error = default!;
            return true;
        }
        if (FormulaValueCoercion.TryNumber(
                value,
                out number,
                allowText: true) &&
            double.IsFinite(number))
        {
            skip = false;
            error = default!;
            return true;
        }
        number = default;
        skip = false;
        error = InvalidValue();
        return false;
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
            !FormulaValueCoercion.TryBoolean(
                argument.ScalarValue,
                out value))
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
        if (!TryGetScalarNumber(argument, out var number, out error) ||
            number < int.MinValue || number > int.MaxValue)
        {
            value = default;
            if (error is null)
            {
                error = NumericError();
            }
            return false;
        }
        value = checked((int)Math.Truncate(number));
        return true;
    }

    private static double LogBinomialProbability(
        int trials,
        int successes,
        double probability) =>
        StatisticalNumerics.LogGamma(trials + 1d) -
        StatisticalNumerics.LogGamma(successes + 1d) -
        StatisticalNumerics.LogGamma(trials - successes + 1d) +
        (successes * Math.Log(probability)) +
        ((trials - successes) * Math.Log(1d - probability));

    private static double ClampCorrelation(double value)
    {
        if (value > 1d && value < 1d + 1e-12d)
        {
            return 1d;
        }
        if (value < -1d && value > -1d - 1e-12d)
        {
            return -1d;
        }
        return value;
    }

    private static FormulaEvaluationResult Number(double value)
    {
        if (!double.IsFinite(value))
        {
            return NumericError();
        }
        return FormulaEvaluationResult.Success(
            CellValue.FromNumber(value));
    }

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

    private struct BivariateStatistics
    {
        public long Count { get; private set; }

        public double MeanX { get; private set; }

        public double MeanY { get; private set; }

        public double M2X { get; private set; }

        public double M2Y { get; private set; }

        public double CoMoment { get; private set; }

        public bool IsFinite =>
            double.IsFinite(MeanX) &&
            double.IsFinite(MeanY) &&
            double.IsFinite(M2X) &&
            double.IsFinite(M2Y) &&
            double.IsFinite(CoMoment);

        public void Add(double x, double y)
        {
            Count++;
            var deltaX = x - MeanX;
            MeanX += deltaX / Count;
            var deltaY = y - MeanY;
            MeanY += deltaY / Count;
            M2X += deltaX * (x - MeanX);
            M2Y += deltaY * (y - MeanY);
            CoMoment += deltaX * (y - MeanY);
        }
    }

    private struct LogSumAccumulator
    {
        private double _maximumLog;
        private double _scaledSum;
        private bool _hasValue;

        public double Value => !_hasValue
            ? 0d
            : Math.Exp(_maximumLog) * _scaledSum;

        public void Add(double logarithm)
        {
            if (double.IsNegativeInfinity(logarithm))
            {
                return;
            }
            if (!_hasValue)
            {
                _maximumLog = logarithm;
                _scaledSum = 1d;
                _hasValue = true;
                return;
            }
            if (logarithm > _maximumLog)
            {
                _scaledSum =
                    (_scaledSum * Math.Exp(_maximumLog - logarithm)) + 1d;
                _maximumLog = logarithm;
            }
            else
            {
                _scaledSum += Math.Exp(logarithm - _maximumLog);
            }
        }
    }
}
