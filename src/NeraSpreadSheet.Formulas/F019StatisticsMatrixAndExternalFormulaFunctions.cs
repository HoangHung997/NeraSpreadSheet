using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>Explicit provider boundary for functions that require host/network/data services.</summary>
public interface IFormulaExternalFunctionContext : IFormulaEvaluationContext
{
    bool TryEvaluateExternalFunction(
        string functionName,
        IReadOnlyList<CellValue> arguments,
        out CellValue value);

    bool TryEvaluateExternalArrayFunction(
        string functionName,
        IReadOnlyList<CellValue> arguments,
        out FormulaArrayValue value);
}

internal static class F019StatisticsMatrixAndExternalFormulaFunctions
{
    private const int MaximumValues = 2_000_000;
    private const int MaximumTextLength = 1_000_000;
    private const FormulaFunctionCapabilities ScalarRange =
        FormulaFunctionCapabilities.ScalarArguments |
        FormulaFunctionCapabilities.RangeArguments;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return Definition("FORECAST.ETS", 3, 6, EvaluateForecastEts);
        yield return Definition("FORECAST.ETS.CONFINT", 3, 6, EvaluateForecastEtsConfidence);
        yield return Definition("FORECAST.ETS.SEASONALITY", 2, 4, EvaluateForecastEtsSeasonality);
        yield return Definition("FORECAST.ETS.STAT", 3, 6, EvaluateForecastEtsStat);
        yield return Definition("MAXIFS", 3, 255, i => EvaluateIfsExtrema(i, maximum: true));
        yield return Definition("MINIFS", 3, 255, i => EvaluateIfsExtrema(i, maximum: false));
        yield return ExternalDefinition("IMAGE", 1, 5);
        yield return ExternalDefinition("DETECTLANGUAGE", 1, 1);
        yield return ExternalDefinition("TRANSLATE", 2, 3);
        yield return ExternalDefinition("WEBSERVICE", 1, 1);
    }

    private static FormulaFunctionDefinition Definition(
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
                ScalarRange | FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy: FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaFunctionDefinition ExternalDefinition(
        string name,
        int minimumArguments,
        int maximumArguments) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                ScalarRange | FormulaFunctionCapabilities.ReturnsScalar,
                FormulaFunctionVolatility.ExternalState,
                FormulaFunctionSecurityClassification.ExternalState,
                FormulaFunctionDependencyPolicy.EngineCapturedOnly,
                propagateArgumentErrors: true,
                argumentCountPolicy: FormulaFunctionArgumentCountPolicy.LogicalArguments),
            invocation => EvaluateExternal(invocation, name));

    private static FormulaEvaluationResult EvaluateExternal(
        FormulaFunctionInvocation invocation,
        string name)
    {
        if (invocation.Context is not IFormulaExternalFunctionContext external)
        {
            return NotAvailable();
        }
        var flattened = invocation.FlattenValues();
        if (flattened.Any(static value => value.Kind == CellValueKind.Error))
        {
            return InvalidValue();
        }
        if (flattened.Any(static value =>
                value.Kind == CellValueKind.Text &&
                ((string)value.RawValue!).Length > MaximumTextLength))
        {
            return NumericError();
        }
        return external.TryEvaluateExternalFunction(name, flattened, out var value)
            ? FormulaEvaluationResult.Success(value)
            : NotAvailable();
    }

    private static FormulaEvaluationResult EvaluateForecastEts(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var target, out var error) ||
            !TryGetPairedSeries(invocation.Arguments[1], invocation.Arguments[2], out var values, out var timeline, out error))
        {
            return error;
        }
        if (!TryValidateTimeline(timeline))
        {
            return NumericError();
        }
        var seasonality = ResolveSeasonality(invocation, values, timeline, seasonalityArgumentIndex: 3);
        if (seasonality < 0)
        {
            return NumericError();
        }
        return TryForecast(values, timeline, target, seasonality, out var result)
            ? Number(result)
            : NumericError();
    }

    private static FormulaEvaluationResult EvaluateForecastEtsConfidence(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var target, out var error) ||
            !TryGetPairedSeries(invocation.Arguments[1], invocation.Arguments[2], out var values, out var timeline, out error))
        {
            return error;
        }
        if (!TryValidateTimeline(timeline) ||
            !TryForecast(values, timeline, target, 0, out _))
        {
            return NumericError();
        }
        var confidence = 0.95d;
        if (invocation.Arguments.Count > 3 &&
            !TryGetScalarNumber(invocation.Arguments[3], out confidence, out error))
        {
            return error;
        }
        if (confidence <= 0d || confidence >= 1d)
        {
            return NumericError();
        }
        LinearFit(timeline, values, out var slope, out var intercept);
        var residual = RootMeanSquareError(timeline, values, slope, intercept);
        if (residual == 0d)
        {
            return Number(0d);
        }
        var z = InverseNormal(0.5d + (confidence / 2d));
        return Number(z * residual);
    }

    private static FormulaEvaluationResult EvaluateForecastEtsSeasonality(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetPairedSeries(invocation.Arguments[0], invocation.Arguments[1], out var values, out var timeline, out var error))
        {
            return error;
        }
        if (!TryValidateTimeline(timeline))
        {
            return NumericError();
        }
        return Number(DetectSeasonality(values));
    }

    private static FormulaEvaluationResult EvaluateForecastEtsStat(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetPairedSeries(invocation.Arguments[0], invocation.Arguments[1], out var values, out var timeline, out var error) ||
            !TryGetTruncatedInteger(invocation.Arguments[2], out var statistic, out error))
        {
            return error;
        }
        if (!TryValidateTimeline(timeline) || statistic is < 1 or > 8)
        {
            return NumericError();
        }
        LinearFit(timeline, values, out var slope, out var intercept);
        var errors = new double[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            errors[index] = values[index] - ((slope * timeline[index]) + intercept);
        }
        var mae = errors.Sum(static value => Math.Abs(value)) / errors.Length;
        var rmse = Math.Sqrt(errors.Sum(static value => value * value) / errors.Length);
        var result = statistic switch
        {
            1 => slope,
            2 => intercept,
            3 => rmse,
            4 => mae,
            5 => DetectSeasonality(values),
            6 => mae,
            7 => rmse,
            8 => values.Length,
            _ => double.NaN,
        };
        return Number(result);
    }

    private static FormulaEvaluationResult EvaluateIfsExtrema(
        FormulaFunctionInvocation invocation,
        bool maximum)
    {
        if (invocation.Arguments.Count < 3 || invocation.Arguments.Count % 2 == 0)
        {
            return InvalidValue();
        }
        var target = invocation.Arguments[0].Values;
        if (target.Count == 0 || target.Count > MaximumValues)
        {
            return NumericError();
        }
        var criteria = new List<(IReadOnlyList<CellValue> Values, FormulaCriterion Criterion)>();
        for (var argumentIndex = 1; argumentIndex < invocation.Arguments.Count; argumentIndex += 2)
        {
            var range = invocation.Arguments[argumentIndex].Values;
            var criterionArgument = invocation.Arguments[argumentIndex + 1];
            if (range.Count != target.Count ||
                criterionArgument.Kind != FormulaFunctionArgumentKind.Scalar)
            {
                return InvalidValue();
            }
            criteria.Add((range, FormulaCriterion.Parse(criterionArgument.ScalarValue)));
        }

        var found = false;
        var best = maximum ? double.NegativeInfinity : double.PositiveInfinity;
        for (var index = 0; index < target.Count; index++)
        {
            var matched = true;
            foreach (var pair in criteria)
            {
                if (!pair.Criterion.Matches(pair.Values[index]))
                {
                    matched = false;
                    break;
                }
            }
            if (!matched || !TryRangeNumber(target[index], out var number))
            {
                continue;
            }
            found = true;
            best = maximum ? Math.Max(best, number) : Math.Min(best, number);
        }
        return Number(found ? best : 0d);
    }

    private static bool TryGetPairedSeries(
        FormulaFunctionArgument valuesArgument,
        FormulaFunctionArgument timelineArgument,
        out double[] values,
        out double[] timeline,
        out FormulaEvaluationResult error)
    {
        timeline = [];
        if (!TryCollectNumbers(valuesArgument, out values, out error) ||
            !TryCollectNumbers(timelineArgument, out timeline, out error))
        {
            return false;
        }
        if (values.Length < 2 || values.Length != timeline.Length)
        {
            error = NotAvailable();
            return false;
        }
        return true;
    }

    private static int ResolveSeasonality(
        FormulaFunctionInvocation invocation,
        double[] values,
        double[] timeline,
        int seasonalityArgumentIndex)
    {
        if (invocation.Arguments.Count <= seasonalityArgumentIndex)
        {
            return DetectSeasonality(values);
        }
        if (!TryGetTruncatedInteger(invocation.Arguments[seasonalityArgumentIndex], out var seasonality, out _))
        {
            return -1;
        }
        if (seasonality == 1)
        {
            return DetectSeasonality(values);
        }
        if (seasonality == 0)
        {
            return 0;
        }
        return seasonality is >= 2 and <= 8784 && seasonality <= timeline.Length / 2
            ? seasonality
            : -1;
    }

    private static bool TryForecast(
        double[] values,
        double[] timeline,
        double target,
        int seasonality,
        out double result)
    {
        LinearFit(timeline, values, out var slope, out var intercept);
        result = (slope * target) + intercept;
        if (seasonality <= 1 || values.Length < seasonality * 2)
        {
            return double.IsFinite(result);
        }
        var averageStep = (timeline[^1] - timeline[0]) / (timeline.Length - 1d);
        if (averageStep <= 0d)
        {
            return false;
        }
        var projectedIndex = (int)Math.Round((target - timeline[0]) / averageStep);
        var phase = ((projectedIndex % seasonality) + seasonality) % seasonality;
        var residualSum = 0d;
        var residualCount = 0;
        for (var index = phase; index < values.Length; index += seasonality)
        {
            residualSum += values[index] - ((slope * timeline[index]) + intercept);
            residualCount++;
        }
        if (residualCount > 0)
        {
            result += residualSum / residualCount;
        }
        return double.IsFinite(result);
    }

    private static int DetectSeasonality(double[] values)
    {
        var maximumPeriod = Math.Min(8784, values.Length / 2);
        for (var period = 2; period <= maximumPeriod; period++)
        {
            var scale = 1d;
            var difference = 0d;
            for (var index = period; index < values.Length; index++)
            {
                difference += Math.Abs(values[index] - values[index - period]);
                scale += Math.Abs(values[index]);
            }
            if (difference <= scale * 1e-10)
            {
                return period;
            }
        }
        return 1;
    }

    private static bool TryValidateTimeline(double[] timeline)
    {
        for (var index = 1; index < timeline.Length; index++)
        {
            if (timeline[index] <= timeline[index - 1])
            {
                return false;
            }
        }
        return true;
    }

    internal static void LinearFit(
        double[] x,
        double[] y,
        out double slope,
        out double intercept)
    {
        var count = x.Length;
        var meanX = x.Sum() / count;
        var meanY = y.Sum() / count;
        var numerator = 0d;
        var denominator = 0d;
        for (var index = 0; index < count; index++)
        {
            var dx = x[index] - meanX;
            numerator += dx * (y[index] - meanY);
            denominator += dx * dx;
        }
        if (denominator == 0d)
        {
            slope = 0d;
            intercept = meanY;
            return;
        }
        slope = numerator / denominator;
        intercept = meanY - (slope * meanX);
    }

    private static double RootMeanSquareError(
        double[] x,
        double[] y,
        double slope,
        double intercept)
    {
        var sum = 0d;
        for (var index = 0; index < x.Length; index++)
        {
            var residual = y[index] - ((slope * x[index]) + intercept);
            sum += residual * residual;
        }
        return Math.Sqrt(sum / x.Length);
    }

    private static double InverseNormal(double probability)
    {
        // Acklam-style rational approximation; sufficient for confidence intervals.
        var a = new[] { -3.969683028665376e+01, 2.209460984245205e+02, -2.759285104469687e+02, 1.383577518672690e+02, -3.066479806614716e+01, 2.506628277459239e+00 };
        var b = new[] { -5.447609879822406e+01, 1.615858368580409e+02, -1.556989798598866e+02, 6.680131188771972e+01, -1.328068155288572e+01 };
        var c = new[] { -7.784894002430293e-03, -3.223964580411365e-01, -2.400758277161838e+00, -2.549732539343734e+00, 4.374664141464968e+00, 2.938163982698783e+00 };
        var d = new[] { 7.784695709041462e-03, 3.224671290700398e-01, 2.445134137142996e+00, 3.754408661907416e+00 };
        const double low = 0.02425;
        const double high = 1d - low;
        if (probability < low)
        {
            var q = Math.Sqrt(-2d * Math.Log(probability));
            return (((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                   ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1d);
        }
        if (probability > high)
        {
            var q = Math.Sqrt(-2d * Math.Log(1d - probability));
            return -(((((c[0] * q + c[1]) * q + c[2]) * q + c[3]) * q + c[4]) * q + c[5]) /
                   ((((d[0] * q + d[1]) * q + d[2]) * q + d[3]) * q + 1d);
        }
        var r = probability - 0.5d;
        var s = r * r;
        return (((((a[0] * s + a[1]) * s + a[2]) * s + a[3]) * s + a[4]) * s + a[5]) * r /
               (((((b[0] * s + b[1]) * s + b[2]) * s + b[3]) * s + b[4]) * s + 1d);
    }

    internal static bool TryRangeNumber(CellValue value, out double number)
    {
        if (value.Kind is CellValueKind.Number or CellValueKind.DateTime)
        {
            return FormulaValueCoercion.TryNumber(value, out number) && double.IsFinite(number);
        }
        number = default;
        return false;
    }

    internal static bool TryCollectNumbers(
        FormulaFunctionArgument argument,
        out double[] values,
        out FormulaEvaluationResult error)
    {
        var collected = new List<double>();
        var direct = argument.Kind == FormulaFunctionArgumentKind.Scalar;
        foreach (var value in argument.Values)
        {
            if (TryRangeNumber(value, out var number))
            {
                collected.Add(number);
            }
            else if (direct && value.Kind == CellValueKind.Boolean)
            {
                collected.Add((bool)value.RawValue! ? 1d : 0d);
            }
            else if (direct && value.Kind == CellValueKind.Text &&
                     FormulaValueCoercion.TryNumber(value, out number, allowText: true) &&
                     double.IsFinite(number))
            {
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

    internal static bool TryGetScalarNumber(
        FormulaFunctionArgument argument,
        out double number,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !FormulaValueCoercion.TryNumber(argument.ScalarValue, out number, allowText: true) ||
            !double.IsFinite(number))
        {
            number = default;
            error = InvalidValue();
            return false;
        }
        error = default!;
        return true;
    }

    internal static bool TryGetTruncatedInteger(
        FormulaFunctionArgument argument,
        out int value,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarNumber(argument, out var number, out error) ||
            number < int.MinValue || number > int.MaxValue)
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

    private static FormulaEvaluationResult NotAvailable() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.NotAvailable);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());
}
