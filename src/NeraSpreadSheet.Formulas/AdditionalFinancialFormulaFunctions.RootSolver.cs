namespace NeraSpreadSheet.Formulas;

internal static partial class AdditionalFinancialFormulaFunctions
{
    private static bool TrySolveRoot(
        double guess,
        double tolerance,
        FinancialRootEvaluator evaluator,
        out double rate)
    {
        var foundNewton = TryNewtonRoot(
            guess,
            tolerance,
            evaluator,
            out var newtonRate);
        var foundBracket = TryBracketedRoot(
            guess,
            tolerance,
            evaluator,
            out var bracketRate);
        if (!foundNewton && !foundBracket)
        {
            rate = default;
            return false;
        }
        if (!foundNewton)
        {
            rate = bracketRate;
            return true;
        }
        if (!foundBracket)
        {
            rate = newtonRate;
            return true;
        }

        rate = Math.Abs(newtonRate - guess) <=
               Math.Abs(bracketRate - guess)
            ? newtonRate
            : bracketRate;
        return true;
    }

    private static bool TryNewtonRoot(
        double guess,
        double tolerance,
        FinancialRootEvaluator evaluator,
        out double rate)
    {
        rate = guess;
        for (var iteration = 0;
             iteration < MaximumRootIterations;
             iteration++)
        {
            if (!evaluator(
                    rate,
                    out var value,
                    out var derivative))
            {
                return false;
            }
            if (Math.Abs(value) <= tolerance)
            {
                return true;
            }
            if (Math.Abs(derivative) <= 1e-18d)
            {
                return false;
            }

            var step = value / derivative;
            var factor = 1d;
            var accepted = false;
            for (var backtrack = 0;
                 backtrack < MaximumNewtonBacktracks;
                 backtrack++)
            {
                var candidate = rate - (step * factor);
                if (IsValidSolverRate(candidate) &&
                    evaluator(
                        candidate,
                        out var candidateValue,
                        out _) &&
                    Math.Abs(candidateValue) < Math.Abs(value))
                {
                    rate = candidate;
                    accepted = true;
                    break;
                }
                factor /= 2d;
            }
            if (!accepted)
            {
                return false;
            }
            if (Math.Abs(step * factor) <=
                RateTolerance * Math.Max(1d, Math.Abs(rate)))
            {
                return evaluator(
                           rate,
                           out var finalValue,
                           out _) &&
                       Math.Abs(finalValue) <= tolerance;
            }
        }

        return evaluator(
                   rate,
                   out var valueAfterIterations,
                   out _) &&
               Math.Abs(valueAfterIterations) <= tolerance;
    }

    private static bool TryBracketedRoot(
        double guess,
        double tolerance,
        FinancialRootEvaluator evaluator,
        out double rate)
    {
        var minimumX = Math.Log(MinimumRateBase);
        var maximumX = Math.Log(1d + MaximumRate);
        var guessX = Math.Clamp(
            Math.Log(1d + guess),
            minimumX,
            maximumX);
        var xValues = new SortedSet<double>
        {
            minimumX,
            maximumX,
            guessX,
        };
        for (var index = 0;
             index <= MaximumRootBracketSamples;
             index++)
        {
            xValues.Add(
                minimumX +
                ((maximumX - minimumX) *
                 index /
                 MaximumRootBracketSamples));
        }

        ReadOnlySpan<double> localOffsets =
        [
            1d / 64d,
            1d / 32d,
            1d / 16d,
            1d / 8d,
            1d / 4d,
            1d / 2d,
            1d,
            2d,
            4d,
            8d,
            16d,
        ];
        foreach (var offset in localOffsets)
        {
            xValues.Add(Math.Clamp(
                guessX - offset,
                minimumX,
                maximumX));
            xValues.Add(Math.Clamp(
                guessX + offset,
                minimumX,
                maximumX));
        }

        var samples = new List<RootSample>(xValues.Count);
        foreach (var x in xValues)
        {
            var sampleRate = Math.Exp(x) - 1d;
            if (evaluator(
                    sampleRate,
                    out var value,
                    out _) &&
                double.IsFinite(value))
            {
                samples.Add(new RootSample(
                    x,
                    sampleRate,
                    value));
            }
        }

        var foundExact = false;
        var exactRate = default(double);
        var exactDistance = double.PositiveInfinity;
        foreach (var sample in samples)
        {
            if (Math.Abs(sample.Value) > tolerance)
            {
                continue;
            }

            var distance = Math.Abs(sample.Rate - guess);
            if (!foundExact ||
                distance < exactDistance ||
                (distance == exactDistance &&
                 sample.Rate < exactRate))
            {
                foundExact = true;
                exactRate = sample.Rate;
                exactDistance = distance;
            }
        }
        if (foundExact)
        {
            rate = exactRate;
            return true;
        }

        var brackets = new List<RootBracket>();
        for (var index = 1; index < samples.Count; index++)
        {
            var left = samples[index - 1];
            var right = samples[index];
            if (Math.Sign(left.Value) ==
                Math.Sign(right.Value))
            {
                continue;
            }
            brackets.Add(new RootBracket(
                left,
                right,
                DistanceToInterval(
                    guess,
                    left.Rate,
                    right.Rate)));
        }

        foreach (var bracket in brackets
                     .OrderBy(static candidate =>
                         candidate.DistanceFromGuess)
                     .ThenBy(candidate => Math.Abs(
                         ((candidate.Left.Rate +
                           candidate.Right.Rate) / 2d) -
                         guess))
                     .ThenBy(static candidate =>
                         candidate.Left.Rate))
        {
            if (TryBisectRoot(
                    bracket.Left,
                    bracket.Right,
                    tolerance,
                    evaluator,
                    out rate))
            {
                return true;
            }
        }

        rate = default;
        return false;
    }

    private static bool TryBisectRoot(
        RootSample left,
        RootSample right,
        double tolerance,
        FinancialRootEvaluator evaluator,
        out double rate)
    {
        var leftX = left.X;
        var rightX = right.X;
        var leftValue = left.Value;
        for (var iteration = 0;
             iteration < MaximumRootIterations;
             iteration++)
        {
            var middleX = (leftX + rightX) / 2d;
            var middleRate = Math.Exp(middleX) - 1d;
            if (!evaluator(
                    middleRate,
                    out var middleValue,
                    out _))
            {
                rate = default;
                return false;
            }
            if (Math.Abs(middleValue) <= tolerance ||
                Math.Abs(rightX - leftX) <= RateTolerance)
            {
                rate = middleRate;
                return true;
            }

            if (Math.Sign(leftValue) ==
                Math.Sign(middleValue))
            {
                leftX = middleX;
                leftValue = middleValue;
            }
            else
            {
                rightX = middleX;
            }
        }

        rate = Math.Exp((leftX + rightX) / 2d) - 1d;
        return evaluator(
                   rate,
                   out var finalValue,
                   out _) &&
               Math.Abs(finalValue) <= tolerance;
    }

    private static double DistanceToInterval(
        double value,
        double minimum,
        double maximum)
    {
        if (value < minimum)
        {
            return minimum - value;
        }
        if (value > maximum)
        {
            return value - maximum;
        }
        return 0d;
    }
}
