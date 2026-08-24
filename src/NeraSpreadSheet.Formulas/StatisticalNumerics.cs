namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Bounded numerical primitives shared by statistical distribution functions.
/// The algorithms avoid unbounded root finding and report convergence failure
/// to the caller instead of looping indefinitely.
/// </summary>
internal static class StatisticalNumerics
{
    public const int MaximumIterations = 512;

    private const double SqrtTwo = 1.4142135623730950488d;
    private const double SqrtTwoPi = 2.5066282746310005024d;
    private const double ConvergenceTolerance = 1e-14d;
    private const double MinimumNormal = 1e-300d;

    private static readonly double[] LanczosCoefficients =
    [
        676.5203681218851d,
        -1259.1392167224028d,
        771.32342877765313d,
        -176.61502916214059d,
        12.507343278686905d,
        -0.13857109526572012d,
        9.9843695780195716e-6d,
        1.5056327351493116e-7d,
    ];

    public static double NormalDensity(double value) =>
        Math.Exp(-0.5d * value * value) / SqrtTwoPi;

    public static double NormalCumulative(double value)
    {
        if (double.IsNegativeInfinity(value))
        {
            return 0d;
        }
        if (double.IsPositiveInfinity(value))
        {
            return 1d;
        }
        return 0.5d * ComplementaryErrorFunction(-value / SqrtTwo);
    }

    public static bool TryInverseNormal(
        double probability,
        out double value)
    {
        if (!double.IsFinite(probability) ||
            probability <= 0d ||
            probability >= 1d)
        {
            value = default;
            return false;
        }

        // Peter J. Acklam's piecewise rational approximation.
        const double lowerTail = 0.02425d;
        const double upperTail = 1d - lowerTail;
        double estimate;
        if (probability < lowerTail)
        {
            var q = Math.Sqrt(-2d * Math.Log(probability));
            estimate = EvaluateTailNumerator(q) /
                       EvaluateTailDenominator(q);
        }
        else if (probability > upperTail)
        {
            var q = Math.Sqrt(-2d * Math.Log(1d - probability));
            estimate = -EvaluateTailNumerator(q) /
                       EvaluateTailDenominator(q);
        }
        else
        {
            var q = probability - 0.5d;
            var r = q * q;
            estimate = EvaluateCentralNumerator(r) * q /
                       EvaluateCentralDenominator(r);
        }

        // Two bounded Newton refinements improve agreement between the
        // rational inverse and the CDF approximation used by this engine.
        for (var iteration = 0; iteration < 2; iteration++)
        {
            var density = NormalDensity(estimate);
            if (!double.IsFinite(density) || density <= MinimumNormal)
            {
                break;
            }
            var correction = (NormalCumulative(estimate) - probability) /
                             density;
            estimate -= correction;
            if (!double.IsFinite(estimate))
            {
                value = default;
                return false;
            }
            if (Math.Abs(correction) <= 2e-14d *
                Math.Max(1d, Math.Abs(estimate)))
            {
                break;
            }
        }

        value = estimate;
        return double.IsFinite(value);
    }

    public static double LogGamma(double value)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            return double.NaN;
        }
        if (value < 0.5d)
        {
            return Math.Log(Math.PI) -
                   Math.Log(Math.Sin(Math.PI * value)) -
                   LogGamma(1d - value);
        }

        var shifted = value - 1d;
        var series = 0.99999999999980993d;
        for (var index = 0;
             index < LanczosCoefficients.Length;
             index++)
        {
            series += LanczosCoefficients[index] /
                      (shifted + index + 1d);
        }
        var t = shifted + LanczosCoefficients.Length - 0.5d;
        return 0.91893853320467274178d +
               ((shifted + 0.5d) * Math.Log(t)) -
               t +
               Math.Log(series);
    }

    public static bool TryRegularizedGammaQ(
        double shape,
        double x,
        out double result)
    {
        if (!double.IsFinite(shape) || shape <= 0d ||
            !double.IsFinite(x) || x < 0d)
        {
            result = default;
            return false;
        }
        if (x == 0d)
        {
            result = 1d;
            return true;
        }

        if (x < shape + 1d)
        {
            if (!TryRegularizedGammaPSeries(shape, x, out var lower))
            {
                result = default;
                return false;
            }
            result = ClampProbability(1d - lower);
            return true;
        }
        return TryRegularizedGammaQContinuedFraction(
            shape,
            x,
            out result);
    }

    private static bool TryRegularizedGammaPSeries(
        double shape,
        double x,
        out double result)
    {
        var logGamma = LogGamma(shape);
        if (!double.IsFinite(logGamma))
        {
            result = default;
            return false;
        }

        var sum = 1d / shape;
        var term = sum;
        var denominator = shape;
        for (var iteration = 1;
             iteration <= MaximumIterations;
             iteration++)
        {
            denominator += 1d;
            term *= x / denominator;
            sum += term;
            if (!double.IsFinite(sum) || !double.IsFinite(term))
            {
                result = default;
                return false;
            }
            if (Math.Abs(term) <=
                Math.Abs(sum) * ConvergenceTolerance)
            {
                result = ClampProbability(
                    sum * Math.Exp(
                        -x +
                        (shape * Math.Log(x)) -
                        logGamma));
                return double.IsFinite(result);
            }
        }

        result = default;
        return false;
    }

    private static bool TryRegularizedGammaQContinuedFraction(
        double shape,
        double x,
        out double result)
    {
        const double floor = 1e-300d;
        var logGamma = LogGamma(shape);
        if (!double.IsFinite(logGamma))
        {
            result = default;
            return false;
        }

        var b = x + 1d - shape;
        var c = 1d / floor;
        var d = 1d / Math.Max(Math.Abs(b), floor) * Math.Sign(b == 0d ? 1d : b);
        var h = d;
        for (var iteration = 1;
             iteration <= MaximumIterations;
             iteration++)
        {
            var coefficient = -iteration * (iteration - shape);
            b += 2d;
            d = (coefficient * d) + b;
            if (Math.Abs(d) < floor)
            {
                d = Math.CopySign(floor, d == 0d ? 1d : d);
            }
            c = b + (coefficient / c);
            if (Math.Abs(c) < floor)
            {
                c = Math.CopySign(floor, c == 0d ? 1d : c);
            }
            d = 1d / d;
            var delta = d * c;
            h *= delta;
            if (!double.IsFinite(h) || !double.IsFinite(delta))
            {
                result = default;
                return false;
            }
            if (Math.Abs(delta - 1d) <= ConvergenceTolerance)
            {
                result = ClampProbability(
                    Math.Exp(
                        -x +
                        (shape * Math.Log(x)) -
                        logGamma) * h);
                return double.IsFinite(result);
            }
        }

        result = default;
        return false;
    }

    private static double ComplementaryErrorFunction(double value)
    {
        var absolute = Math.Abs(value);
        var t = 1d / (1d + (0.5d * absolute));
        var polynomial = t * Math.Exp(
            (-absolute * absolute) -
            1.26551223d +
            (t * (1.00002368d +
            (t * (0.37409196d +
            (t * (0.09678418d +
            (t * (-0.18628806d +
            (t * (0.27886807d +
            (t * (-1.13520398d +
            (t * (1.48851587d +
            (t * (-0.82215223d +
            (t * 0.17087277d))))))))))))))))));
        return value >= 0d ? polynomial : 2d - polynomial;
    }

    private static double EvaluateTailNumerator(double value) =>
        (((((-0.007784894002430293d * value -
              0.3223964580411365d) * value -
              2.400758277161838d) * value -
              2.549732539343734d) * value +
              4.374664141464968d) * value +
              2.938163982698783d);

    private static double EvaluateTailDenominator(double value) =>
        ((((0.007784695709041462d * value +
             0.3224671290700398d) * value +
             2.445134137142996d) * value +
             3.754408661907416d) * value +
             1d);

    private static double EvaluateCentralNumerator(double value) =>
        (((((-39.69683028665376d * value +
              220.9460984245205d) * value -
              275.9285104469687d) * value +
              138.357751867269d) * value -
              30.66479806614716d) * value +
              2.506628277459239d);

    private static double EvaluateCentralDenominator(double value) =>
        (((((-54.47609879822406d * value +
              161.5858368580409d) * value -
              155.6989798598866d) * value +
              66.80131188771972d) * value -
              13.28068155288572d) * value +
              1d);

    private static double ClampProbability(double value) =>
        Math.Clamp(value, 0d, 1d);
}
