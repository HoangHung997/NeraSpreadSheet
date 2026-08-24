namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Bounded regularized beta/gamma and inverse-distribution primitives used by
/// higher statistical functions. Every continued fraction and root search has
/// a hard iteration limit and reports non-convergence to the caller.
/// </summary>
internal static class AdvancedDistributionNumerics
{
    public const int MaximumIterations = 512;
    public const int MaximumInverseIterations = 256;

    private const double ConvergenceTolerance = 3e-14d;
    private const double InverseTolerance = 2e-13d;
    private const double ContinuedFractionFloor = 1e-300d;

    public static bool TryRegularizedGammaP(
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
            result = 0d;
            return true;
        }

        if (x < shape + 1d)
        {
            return TryRegularizedGammaPSeries(shape, x, out result);
        }
        if (!StatisticalNumerics.TryRegularizedGammaQ(
                shape,
                x,
                out var upper))
        {
            result = default;
            return false;
        }
        result = ClampProbability(1d - upper);
        return true;
    }

    public static bool TryInverseRegularizedGammaP(
        double probability,
        double shape,
        out double result)
    {
        if (!double.IsFinite(probability) ||
            probability <= 0d || probability >= 1d ||
            !double.IsFinite(shape) || shape <= 0d)
        {
            result = default;
            return false;
        }

        var lower = 0d;
        var upper = Math.Max(1d, shape);
        var bracketed = false;
        for (var iteration = 0; iteration < 128; iteration++)
        {
            if (!TryRegularizedGammaP(shape, upper, out var value))
            {
                result = default;
                return false;
            }
            if (value >= probability)
            {
                bracketed = true;
                break;
            }
            upper *= 2d;
            if (!double.IsFinite(upper))
            {
                result = default;
                return false;
            }
        }
        if (!bracketed)
        {
            result = default;
            return false;
        }

        for (var iteration = 0;
             iteration < MaximumInverseIterations;
             iteration++)
        {
            var middle = lower + ((upper - lower) / 2d);
            if (!TryRegularizedGammaP(shape, middle, out var value))
            {
                result = default;
                return false;
            }
            if (Math.Abs(value - probability) <= InverseTolerance)
            {
                result = middle;
                return true;
            }
            if (value < probability)
            {
                lower = middle;
            }
            else
            {
                upper = middle;
            }
            if (upper - lower <= InverseTolerance *
                Math.Max(1d, middle))
            {
                result = lower + ((upper - lower) / 2d);
                return double.IsFinite(result);
            }
        }

        result = default;
        return false;
    }

    public static bool TryRegularizedBeta(
        double alpha,
        double beta,
        double x,
        out double result)
    {
        if (!double.IsFinite(alpha) || alpha <= 0d ||
            !double.IsFinite(beta) || beta <= 0d ||
            !double.IsFinite(x) || x < 0d || x > 1d)
        {
            result = default;
            return false;
        }
        if (x == 0d)
        {
            result = 0d;
            return true;
        }
        if (x == 1d)
        {
            result = 1d;
            return true;
        }

        var logBeta = StatisticalNumerics.LogGamma(alpha) +
                      StatisticalNumerics.LogGamma(beta) -
                      StatisticalNumerics.LogGamma(alpha + beta);
        if (!double.IsFinite(logBeta))
        {
            result = default;
            return false;
        }
        var front = Math.Exp(
            (alpha * Math.Log(x)) +
            (beta * Math.Log(1d - x)) -
            logBeta);
        if (!double.IsFinite(front))
        {
            result = default;
            return false;
        }

        if (x < (alpha + 1d) / (alpha + beta + 2d))
        {
            if (!TryBetaContinuedFraction(
                    alpha,
                    beta,
                    x,
                    out var fraction))
            {
                result = default;
                return false;
            }
            result = ClampProbability(front * fraction / alpha);
            return true;
        }
        if (!TryBetaContinuedFraction(
                beta,
                alpha,
                1d - x,
                out var complementFraction))
        {
            result = default;
            return false;
        }
        result = ClampProbability(
            1d -
            (front * complementFraction / beta));
        return true;
    }

    public static bool TryInverseRegularizedBeta(
        double probability,
        double alpha,
        double beta,
        out double result)
    {
        if (!double.IsFinite(probability) ||
            probability < 0d || probability > 1d ||
            !double.IsFinite(alpha) || alpha <= 0d ||
            !double.IsFinite(beta) || beta <= 0d)
        {
            result = default;
            return false;
        }
        if (probability == 0d)
        {
            result = 0d;
            return true;
        }
        if (probability == 1d)
        {
            result = 1d;
            return true;
        }

        var lower = 0d;
        var upper = 1d;
        for (var iteration = 0;
             iteration < MaximumInverseIterations;
             iteration++)
        {
            var middle = lower + ((upper - lower) / 2d);
            if (!TryRegularizedBeta(
                    alpha,
                    beta,
                    middle,
                    out var value))
            {
                result = default;
                return false;
            }
            if (Math.Abs(value - probability) <= InverseTolerance)
            {
                result = middle;
                return true;
            }
            if (value < probability)
            {
                lower = middle;
            }
            else
            {
                upper = middle;
            }
            if (upper - lower <= InverseTolerance)
            {
                result = lower + ((upper - lower) / 2d);
                return true;
            }
        }

        result = default;
        return false;
    }

    public static double StudentTDensity(double x, double degreesOfFreedom)
    {
        if (!double.IsFinite(x) ||
            !double.IsFinite(degreesOfFreedom) ||
            degreesOfFreedom <= 0d)
        {
            return double.NaN;
        }
        var logDensity =
            StatisticalNumerics.LogGamma((degreesOfFreedom + 1d) / 2d) -
            StatisticalNumerics.LogGamma(degreesOfFreedom / 2d) -
            (0.5d * Math.Log(degreesOfFreedom * Math.PI)) -
            (((degreesOfFreedom + 1d) / 2d) *
             Math.Log(1d + ((x * x) / degreesOfFreedom)));
        return Math.Exp(logDensity);
    }

    public static bool TryStudentTCumulative(
        double x,
        double degreesOfFreedom,
        out double result)
    {
        if (!double.IsFinite(x) ||
            !double.IsFinite(degreesOfFreedom) ||
            degreesOfFreedom <= 0d)
        {
            result = default;
            return false;
        }
        if (x == 0d)
        {
            result = 0.5d;
            return true;
        }
        var betaX = degreesOfFreedom /
                    (degreesOfFreedom + (x * x));
        if (!TryRegularizedBeta(
                degreesOfFreedom / 2d,
                0.5d,
                betaX,
                out var tail))
        {
            result = default;
            return false;
        }
        result = x > 0d
            ? 1d - (0.5d * tail)
            : 0.5d * tail;
        result = ClampProbability(result);
        return true;
    }

    public static bool TryInverseStudentT(
        double probability,
        double degreesOfFreedom,
        out double result)
    {
        if (!double.IsFinite(probability) ||
            probability <= 0d || probability >= 1d ||
            !double.IsFinite(degreesOfFreedom) ||
            degreesOfFreedom <= 0d)
        {
            result = default;
            return false;
        }
        if (probability == 0.5d)
        {
            result = 0d;
            return true;
        }

        var negative = probability < 0.5d;
        var target = negative ? 1d - probability : probability;
        var lower = 0d;
        var upper = 1d;
        var bracketed = false;
        for (var iteration = 0; iteration < 128; iteration++)
        {
            if (!TryStudentTCumulative(
                    upper,
                    degreesOfFreedom,
                    out var value))
            {
                result = default;
                return false;
            }
            if (value >= target)
            {
                bracketed = true;
                break;
            }
            upper *= 2d;
            if (!double.IsFinite(upper))
            {
                result = default;
                return false;
            }
        }
        if (!bracketed)
        {
            result = default;
            return false;
        }

        for (var iteration = 0;
             iteration < MaximumInverseIterations;
             iteration++)
        {
            var middle = lower + ((upper - lower) / 2d);
            if (!TryStudentTCumulative(
                    middle,
                    degreesOfFreedom,
                    out var value))
            {
                result = default;
                return false;
            }
            if (Math.Abs(value - target) <= InverseTolerance)
            {
                result = negative ? -middle : middle;
                return true;
            }
            if (value < target)
            {
                lower = middle;
            }
            else
            {
                upper = middle;
            }
            if (upper - lower <= InverseTolerance *
                Math.Max(1d, middle))
            {
                var magnitude = lower + ((upper - lower) / 2d);
                result = negative ? -magnitude : magnitude;
                return double.IsFinite(result);
            }
        }

        result = default;
        return false;
    }

    public static double FDensity(
        double x,
        double degreesOfFreedom1,
        double degreesOfFreedom2)
    {
        if (!double.IsFinite(x) || x < 0d ||
            !double.IsFinite(degreesOfFreedom1) ||
            degreesOfFreedom1 <= 0d ||
            !double.IsFinite(degreesOfFreedom2) ||
            degreesOfFreedom2 <= 0d)
        {
            return double.NaN;
        }
        if (x == 0d)
        {
            var alpha = degreesOfFreedom1 / 2d;
            if (alpha > 1d)
            {
                return 0d;
            }
            if (alpha == 1d)
            {
                return 1d;
            }
            return double.PositiveInfinity;
        }

        var a = degreesOfFreedom1 / 2d;
        var b = degreesOfFreedom2 / 2d;
        var logBeta = StatisticalNumerics.LogGamma(a) +
                      StatisticalNumerics.LogGamma(b) -
                      StatisticalNumerics.LogGamma(a + b);
        var ratio = degreesOfFreedom1 / degreesOfFreedom2;
        var logDensity =
            (a * Math.Log(ratio)) +
            ((a - 1d) * Math.Log(x)) -
            logBeta -
            ((a + b) * Math.Log(1d + (ratio * x)));
        return Math.Exp(logDensity);
    }

    public static bool TryFCumulative(
        double x,
        double degreesOfFreedom1,
        double degreesOfFreedom2,
        out double result)
    {
        if (!double.IsFinite(x) || x < 0d ||
            !double.IsFinite(degreesOfFreedom1) ||
            degreesOfFreedom1 <= 0d ||
            !double.IsFinite(degreesOfFreedom2) ||
            degreesOfFreedom2 <= 0d)
        {
            result = default;
            return false;
        }
        if (x == 0d)
        {
            result = 0d;
            return true;
        }
        var scaled = degreesOfFreedom1 * x;
        var betaX = scaled /
                    (scaled + degreesOfFreedom2);
        return TryRegularizedBeta(
            degreesOfFreedom1 / 2d,
            degreesOfFreedom2 / 2d,
            betaX,
            out result);
    }

    public static bool TryInverseF(
        double probability,
        double degreesOfFreedom1,
        double degreesOfFreedom2,
        out double result)
    {
        if (!double.IsFinite(probability) ||
            probability < 0d || probability >= 1d ||
            !double.IsFinite(degreesOfFreedom1) ||
            degreesOfFreedom1 <= 0d ||
            !double.IsFinite(degreesOfFreedom2) ||
            degreesOfFreedom2 <= 0d)
        {
            result = default;
            return false;
        }
        if (probability == 0d)
        {
            result = 0d;
            return true;
        }
        if (!TryInverseRegularizedBeta(
                probability,
                degreesOfFreedom1 / 2d,
                degreesOfFreedom2 / 2d,
                out var betaX) ||
            betaX >= 1d)
        {
            result = default;
            return false;
        }
        result = degreesOfFreedom2 * betaX /
                 (degreesOfFreedom1 * (1d - betaX));
        return double.IsFinite(result);
    }

    private static bool TryRegularizedGammaPSeries(
        double shape,
        double x,
        out double result)
    {
        var logGamma = StatisticalNumerics.LogGamma(shape);
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

    private static bool TryBetaContinuedFraction(
        double alpha,
        double beta,
        double x,
        out double result)
    {
        var qab = alpha + beta;
        var qap = alpha + 1d;
        var qam = alpha - 1d;
        var c = 1d;
        var d = 1d - ((qab * x) / qap);
        if (Math.Abs(d) < ContinuedFractionFloor)
        {
            d = Math.CopySign(
                ContinuedFractionFloor,
                d == 0d ? 1d : d);
        }
        d = 1d / d;
        var h = d;

        for (var iteration = 1;
             iteration <= MaximumIterations;
             iteration++)
        {
            var twice = 2d * iteration;
            var coefficient = iteration * (beta - iteration) * x /
                              ((qam + twice) * (alpha + twice));
            d = 1d + (coefficient * d);
            if (Math.Abs(d) < ContinuedFractionFloor)
            {
                d = Math.CopySign(
                    ContinuedFractionFloor,
                    d == 0d ? 1d : d);
            }
            c = 1d + (coefficient / c);
            if (Math.Abs(c) < ContinuedFractionFloor)
            {
                c = Math.CopySign(
                    ContinuedFractionFloor,
                    c == 0d ? 1d : c);
            }
            d = 1d / d;
            h *= d * c;

            coefficient = -((alpha + iteration) *
                            (qab + iteration) * x) /
                          ((alpha + twice) * (qap + twice));
            d = 1d + (coefficient * d);
            if (Math.Abs(d) < ContinuedFractionFloor)
            {
                d = Math.CopySign(
                    ContinuedFractionFloor,
                    d == 0d ? 1d : d);
            }
            c = 1d + (coefficient / c);
            if (Math.Abs(c) < ContinuedFractionFloor)
            {
                c = Math.CopySign(
                    ContinuedFractionFloor,
                    c == 0d ? 1d : c);
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
                result = h;
                return true;
            }
        }

        result = default;
        return false;
    }

    private static double ClampProbability(double value) =>
        Math.Clamp(value, 0d, 1d);
}
