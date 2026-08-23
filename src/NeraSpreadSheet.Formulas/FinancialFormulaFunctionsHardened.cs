using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Routes the established financial family through a hardened IRR solver.
/// The original functions remain the implementation source for every other
/// financial name; only IRR is replaced so multiple-root selection can honor
/// the supplied guess deterministically.
/// </summary>
internal static class FinancialFormulaFunctionsHardened
{
    private const int MaximumIrrValues = 100_000;
    private const int MaximumIrrIterations = 100;
    private const int MaximumIrrBracketSamples = 64;
    private const double RootTolerance = 1e-10d;
    private const double MinimumIrrBase = 1e-12d;
    private const double MaximumIrrRate = 1e10d;

    public static IEnumerable<IFormulaFunction> Create()
    {
        foreach (var function in FinancialFormulaFunctions.Create())
        {
            if (!string.Equals(
                    function.Name,
                    "IRR",
                    StringComparison.OrdinalIgnoreCase))
            {
                yield return function;
            }
        }

        yield return new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", "IRR"),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                1,
                2,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            EvaluateInternalRateOfReturn);
    }

    private static FormulaEvaluationResult EvaluateInternalRateOfReturn(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectCashFlows(
                invocation.Arguments[0],
                out var cashFlows,
                out var error))
        {
            return error;
        }
        if (cashFlows.Length < 2 ||
            !cashFlows.Any(static value => value > 0d) ||
            !cashFlows.Any(static value => value < 0d))
        {
            return NumericError();
        }

        var guess = 0.1d;
        if (invocation.Arguments.Count == 2 &&
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out guess,
                out error))
        {
            return error;
        }
        if (guess <= -1d || guess > MaximumIrrRate)
        {
            return NumericError();
        }

        var maximumMagnitude = cashFlows.Max(static value => Math.Abs(value));
        var tolerance = RootTolerance * Math.Max(1d, maximumMagnitude);
        var foundNewton = TryNewtonIrr(
            cashFlows,
            guess,
            tolerance,
            out var newtonRate);
        var foundBracket = TryBracketedIrr(
            cashFlows,
            guess,
            tolerance,
            out var bracketRate);
        if (!foundNewton && !foundBracket)
        {
            return NumericError();
        }
        if (!foundNewton)
        {
            return Number(bracketRate);
        }
        if (!foundBracket)
        {
            return Number(newtonRate);
        }

        // Newton can cross several roots before converging. Compare it with
        // the deterministic bracket result and honor the caller's guess.
        var selected = Math.Abs(newtonRate - guess) <=
                       Math.Abs(bracketRate - guess)
            ? newtonRate
            : bracketRate;
        return Number(selected);
    }

    private static bool TryCollectCashFlows(
        FormulaFunctionArgument argument,
        out double[] values,
        out FormulaEvaluationResult error)
    {
        var result = new List<double>();
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
                result.Add(number);
            }
            else if (argument.Kind == FormulaFunctionArgumentKind.Scalar)
            {
                if (value.Kind == CellValueKind.Boolean)
                {
                    result.Add((bool)value.RawValue! ? 1d : 0d);
                }
                else if (value.Kind == CellValueKind.Text)
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
                    result.Add(number);
                }
            }
            if (result.Count > MaximumIrrValues)
            {
                values = [];
                error = NumericError();
                return false;
            }
        }

        values = result.ToArray();
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

    private static bool TryNewtonIrr(
        double[] cashFlows,
        double guess,
        double tolerance,
        out double rate)
    {
        rate = guess;
        for (var iteration = 0;
             iteration < MaximumIrrIterations;
             iteration++)
        {
            if (!TryEvaluateIrr(
                    cashFlows,
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

            var next = rate - (value / derivative);
            if (!double.IsFinite(next) ||
                next <= -1d + MinimumIrrBase ||
                next > MaximumIrrRate)
            {
                return false;
            }
            rate = next;
        }

        return TryEvaluateIrr(
                   cashFlows,
                   rate,
                   out var finalValue,
                   out _) &&
               Math.Abs(finalValue) <= tolerance;
    }

    private static bool TryBracketedIrr(
        double[] cashFlows,
        double guess,
        double tolerance,
        out double rate)
    {
        var minimumX = Math.Log(MinimumIrrBase);
        var maximumX = Math.Log(1d + MaximumIrrRate);
        var guessX = Math.Clamp(
            Math.Log(1d + guess),
            minimumX,
            maximumX);
        var xValues = new List<double>(MaximumIrrBracketSamples + 2);
        for (var index = 0;
             index <= MaximumIrrBracketSamples;
             index++)
        {
            xValues.Add(minimumX +
                ((maximumX - minimumX) * index /
                 MaximumIrrBracketSamples));
        }
        xValues.Add(guessX);

        var samples = xValues
            .Distinct()
            .OrderBy(static value => value)
            .Select(x => new IrrSample(
                x,
                Math.Exp(x) - 1d,
                EvaluateIrrAtX(cashFlows, x)))
            .Where(static sample => sample.Value.HasValue)
            .ToArray();

        var exact = samples
            .Where(sample => Math.Abs(sample.Value!.Value) <= tolerance)
            .OrderBy(sample => Math.Abs(sample.Rate - guess))
            .ThenBy(static sample => sample.Rate)
            .FirstOrDefault();
        if (exact.Value.HasValue)
        {
            rate = exact.Rate;
            return true;
        }

        var brackets = new List<IrrBracket>();
        for (var index = 1; index < samples.Length; index++)
        {
            var left = samples[index - 1];
            var right = samples[index];
            if (Math.Sign(left.Value!.Value) ==
                Math.Sign(right.Value!.Value))
            {
                continue;
            }
            brackets.Add(new IrrBracket(
                left,
                right,
                DistanceToInterval(guess, left.Rate, right.Rate)));
        }
        if (brackets.Count == 0)
        {
            rate = default;
            return false;
        }

        var selected = brackets
            .OrderBy(static bracket => bracket.DistanceFromGuess)
            .ThenBy(bracket => Math.Abs(
                ((bracket.Left.Rate + bracket.Right.Rate) / 2d) - guess))
            .First();
        return TryBisect(
            cashFlows,
            selected.Left,
            selected.Right,
            tolerance,
            out rate);
    }

    private static bool TryBisect(
        double[] cashFlows,
        IrrSample left,
        IrrSample right,
        double tolerance,
        out double rate)
    {
        var leftX = left.X;
        var rightX = right.X;
        var leftValue = left.Value!.Value;
        for (var iteration = 0;
             iteration < MaximumIrrIterations;
             iteration++)
        {
            var middleX = (leftX + rightX) / 2d;
            var middleValue = EvaluateIrrAtX(cashFlows, middleX);
            if (!middleValue.HasValue)
            {
                rate = default;
                return false;
            }
            if (Math.Abs(middleValue.Value) <= tolerance ||
                Math.Abs(rightX - leftX) <= RootTolerance)
            {
                rate = Math.Exp(middleX) - 1d;
                return double.IsFinite(rate) && rate > -1d;
            }
            if (Math.Sign(leftValue) == Math.Sign(middleValue.Value))
            {
                leftX = middleX;
                leftValue = middleValue.Value;
            }
            else
            {
                rightX = middleX;
            }
        }

        rate = Math.Exp((leftX + rightX) / 2d) - 1d;
        return TryEvaluateIrr(
                   cashFlows,
                   rate,
                   out var value,
                   out _) &&
               Math.Abs(value) <= tolerance;
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

    private static double? EvaluateIrrAtX(double[] cashFlows, double x)
    {
        var rate = Math.Exp(x) - 1d;
        return TryEvaluateIrr(cashFlows, rate, out var value, out _)
            ? value
            : null;
    }

    private static bool TryEvaluateIrr(
        double[] cashFlows,
        double rate,
        out double value,
        out double derivative)
    {
        if (!double.IsFinite(rate) || rate <= -1d)
        {
            value = default;
            derivative = default;
            return false;
        }

        var discountBase = 1d + rate;
        var discount = 1d;
        var sum = 0d;
        var compensation = 0d;
        var derivativeSum = 0d;
        var derivativeCompensation = 0d;
        for (var index = 0; index < cashFlows.Length; index++)
        {
            if (index > 0)
            {
                discount *= discountBase;
            }
            if (discount == 0d || double.IsNaN(discount))
            {
                value = default;
                derivative = default;
                return false;
            }

            var term = double.IsPositiveInfinity(discount)
                ? 0d
                : cashFlows[index] / discount;
            if (!double.IsFinite(term))
            {
                value = default;
                derivative = default;
                return false;
            }
            AddCompensated(ref sum, ref compensation, term);
            if (index == 0)
            {
                continue;
            }

            var derivativeTerm = -(index * term) / discountBase;
            if (!double.IsFinite(derivativeTerm))
            {
                value = default;
                derivative = default;
                return false;
            }
            AddCompensated(
                ref derivativeSum,
                ref derivativeCompensation,
                derivativeTerm);
        }

        value = sum;
        derivative = derivativeSum;
        return double.IsFinite(value) && double.IsFinite(derivative);
    }

    private static void AddCompensated(
        ref double sum,
        ref double compensation,
        double value)
    {
        var adjusted = value - compensation;
        var next = sum + adjusted;
        compensation = (next - sum) - adjusted;
        sum = next;
    }

    private static FormulaEvaluationResult Number(double value) =>
        double.IsFinite(value)
            ? FormulaEvaluationResult.Success(CellValue.FromNumber(value))
            : NumericError();

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());

    private readonly record struct IrrSample(
        double X,
        double Rate,
        double? Value);

    private readonly record struct IrrBracket(
        IrrSample Left,
        IrrSample Right,
        double DistanceFromGuess);
}
