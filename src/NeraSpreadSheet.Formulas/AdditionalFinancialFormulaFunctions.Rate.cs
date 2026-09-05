namespace NeraSpreadSheet.Formulas;

internal static partial class AdditionalFinancialFormulaFunctions
{
    private static bool TryEvaluateRateEquation(
        double rate,
        double periods,
        double payment,
        double presentValue,
        double futureValue,
        int timing,
        out double value,
        out double derivative)
    {
        if (!TryEvaluateRateValue(
                rate,
                periods,
                payment,
                presentValue,
                futureValue,
                timing,
                out value))
        {
            derivative = default;
            return false;
        }

        if (rate == 0d)
        {
            derivative =
                (presentValue * periods) +
                (payment *
                 ((timing * periods) +
                  ((periods * (periods - 1d)) / 2d)));
            return double.IsFinite(derivative);
        }

        if (Math.Abs(rate) < 1e-7d)
        {
            var step = Math.Max(
                1e-8d,
                Math.Abs(rate) * 1e-3d);
            var lowerRate = Math.Max(
                -1d + MinimumRateBase,
                rate - step);
            var upperRate = Math.Min(
                MaximumRate,
                rate + step);
            if (upperRate <= lowerRate ||
                !TryEvaluateRateValue(
                    lowerRate,
                    periods,
                    payment,
                    presentValue,
                    futureValue,
                    timing,
                    out var lowerValue) ||
                !TryEvaluateRateValue(
                    upperRate,
                    periods,
                    payment,
                    presentValue,
                    futureValue,
                    timing,
                    out var upperValue))
            {
                derivative = default;
                return false;
            }

            derivative =
                (upperValue - lowerValue) /
                (upperRate - lowerRate);
            return double.IsFinite(derivative);
        }

        if (!TryGetRateTerms(
                rate,
                periods,
                out var growth,
                out var growthMinusOne,
                out var annuity))
        {
            derivative = default;
            return false;
        }

        var growthDerivative =
            periods * growth / (1d + rate);
        var annuityDerivative =
            ((growthDerivative * rate) - growthMinusOne) /
            (rate * rate);
        derivative =
            (presentValue * growthDerivative) +
            (payment *
             ((timing * annuity) +
              ((1d + (rate * timing)) *
               annuityDerivative)));
        return double.IsFinite(derivative);
    }

    private static bool TryEvaluateRateValue(
        double rate,
        double periods,
        double payment,
        double presentValue,
        double futureValue,
        int timing,
        out double value)
    {
        if (!IsValidRate(rate))
        {
            value = default;
            return false;
        }
        if (rate == 0d)
        {
            value =
                presentValue +
                (payment * periods) +
                futureValue;
            return double.IsFinite(value);
        }
        if (!TryGetRateTerms(
                rate,
                periods,
                out var growth,
                out _,
                out var annuity))
        {
            value = default;
            return false;
        }

        value =
            (presentValue * growth) +
            (payment * (1d + (rate * timing)) * annuity) +
            futureValue;
        return double.IsFinite(value);
    }

    private static bool TryGetRateTerms(
        double rate,
        double periods,
        out double growth,
        out double growthMinusOne,
        out double annuity)
    {
        var logarithm = LogOnePlus(rate);
        var exponent = periods * logarithm;
        if (!double.IsFinite(exponent) ||
            exponent > MaximumLogarithm)
        {
            growth = default;
            growthMinusOne = default;
            annuity = default;
            return false;
        }

        growth = Math.Exp(exponent);
        growthMinusOne = ExponentialMinusOne(exponent);
        annuity = growthMinusOne / rate;
        return double.IsFinite(growth) &&
               double.IsFinite(growthMinusOne) &&
               double.IsFinite(annuity);
    }
}
