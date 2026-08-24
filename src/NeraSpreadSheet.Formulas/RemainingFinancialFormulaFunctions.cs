using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Remaining first-generation financial functions that share bounded root
/// search and irregular-date cash-flow semantics.
/// </summary>
internal static class RemainingFinancialFormulaFunctions
{
    private const int MaximumCashFlowCount = 100_000;
    private const long MaximumTermEvaluations = 20_000_000L;
    private const int MaximumRootIterations = 256;
    private const int MaximumScanSegments = 512;
    private const double MinimumTransformedRate = -40d;
    private const double MaximumTransformedRate = 40d;
    private const double RootTolerance = 2e-12d;

    public static IReadOnlyList<FormulaFunctionDefinition> CreateAll() =>
    [
        new FormulaFunctionDefinition(
            CreateDescriptor(
                "RATE",
                3,
                6,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.ReturnsScalar),
            EvaluateRate),
        new FormulaFunctionDefinition(
            CreateDescriptor(
                "XNPV",
                3,
                3,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar),
            EvaluateXnpv),
        new FormulaFunctionDefinition(
            CreateDescriptor(
                "XIRR",
                2,
                3,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar),
            EvaluateXirr),
    ];

    private static FormulaFunctionDescriptor CreateDescriptor(
        string name,
        int minimumArguments,
        int maximumArguments,
        FormulaFunctionCapabilities capabilities) =>
        new(
            new FormulaFunctionIdentity("NERA.BUILTIN", name),
            new FormulaFunctionVersion(1, 0, 0),
            FormulaFunctionApiVersion.Current,
            minimumArguments,
            maximumArguments,
            capabilities,
            FormulaFunctionVolatility.Deterministic,
            FormulaFunctionSecurityClassification.Pure,
            argumentCountPolicy:
                FormulaFunctionArgumentCountPolicy.LogicalArguments);

    private static FormulaEvaluationResult EvaluateRate(
        FormulaFunctionInvocation invocation)
    {
        if (!TryReadScalar(invocation, 0, out var periods) ||
            !TryReadScalar(invocation, 1, out var payment) ||
            !TryReadScalar(invocation, 2, out var presentValue) ||
            !TryReadOptionalScalar(invocation, 3, 0d, out var futureValue) ||
            !TryReadOptionalScalar(invocation, 4, 0d, out var paymentTypeValue) ||
            !TryReadOptionalScalar(invocation, 5, 0.1d, out var guess) ||
            periods <= 0d ||
            !TryPaymentType(paymentTypeValue, out var paymentType) ||
            guess <= -1d ||
            (payment == 0d && presentValue == 0d && futureValue == 0d))
        {
            return NumericFailure();
        }

        var initial = LogOnePlus(guess);
        var budget = new EvaluationBudget(MaximumTermEvaluations);
        bool TryEvaluate(double transformedRate, out double value) =>
            TryEvaluateRateEquation(
                transformedRate,
                periods,
                payment,
                presentValue,
                futureValue,
                paymentType,
                budget,
                out value);

        if (!TryFindNearestRoot(
                TryEvaluate,
                initial,
                MaximumScanSegments,
                out var transformedRoot))
        {
            return NumericFailure();
        }

        var rate = ExpMinusOne(transformedRoot);
        return double.IsFinite(rate) && rate > -1d
            ? FormulaEvaluationResult.Success(CellValue.FromNumber(rate))
            : NumericFailure();
    }

    private static FormulaEvaluationResult EvaluateXnpv(
        FormulaFunctionInvocation invocation)
    {
        if (!TryReadScalar(invocation, 0, out var rate) ||
            rate <= -1d ||
            !TryReadSchedule(
                invocation.Arguments[1].Values,
                invocation.Arguments[2].Values,
                minimumCount: 1,
                requireOppositeSigns: false,
                out var cashFlows,
                out var years))
        {
            return NumericFailure();
        }

        var transformedRate = LogOnePlus(rate);
        if (!TryEvaluateXnpvActual(
                transformedRate,
                cashFlows,
                years,
                out var result))
        {
            return NumericFailure();
        }

        return FormulaEvaluationResult.Success(CellValue.FromNumber(result));
    }

    private static FormulaEvaluationResult EvaluateXirr(
        FormulaFunctionInvocation invocation)
    {
        if (!TryReadOptionalScalar(invocation, 2, 0.1d, out var guess) ||
            guess <= -1d ||
            !TryReadSchedule(
                invocation.Arguments[0].Values,
                invocation.Arguments[1].Values,
                minimumCount: 2,
                requireOppositeSigns: true,
                out var cashFlows,
                out var years))
        {
            return NumericFailure();
        }

        var budget = new EvaluationBudget(MaximumTermEvaluations);
        var scanSegments = Math.Clamp(
            (int)Math.Max(
                32L,
                Math.Min(
                    MaximumScanSegments,
                    (MaximumTermEvaluations / 2L) /
                    Math.Max(1, cashFlows.Length))),
            32,
            MaximumScanSegments);
        var initial = LogOnePlus(guess);
        bool TryEvaluate(double transformedRate, out double value) =>
            TryEvaluateXirrEquation(
                transformedRate,
                cashFlows,
                years,
                budget,
                out value);

        if (!TryFindNearestRoot(
                TryEvaluate,
                initial,
                scanSegments,
                out var transformedRoot))
        {
            return NumericFailure();
        }

        var rate = ExpMinusOne(transformedRoot);
        return double.IsFinite(rate) && rate > -1d
            ? FormulaEvaluationResult.Success(CellValue.FromNumber(rate))
            : NumericFailure();
    }

    private static bool TryEvaluateRateEquation(
        double transformedRate,
        double periods,
        double payment,
        double presentValue,
        double futureValue,
        int paymentType,
        EvaluationBudget budget,
        out double normalizedValue)
    {
        if (!budget.TryConsume(1) ||
            !double.IsFinite(transformedRate) ||
            transformedRate < MinimumTransformedRate ||
            transformedRate > MaximumTransformedRate)
        {
            normalizedValue = default;
            return false;
        }

        var rate = ExpMinusOne(transformedRate);
        if (!double.IsFinite(rate) || rate <= -1d)
        {
            normalizedValue = default;
            return false;
        }

        double first;
        double second;
        double third;
        if (Math.Abs(rate) <= 1e-10d)
        {
            first = presentValue;
            second = payment * periods;
            third = futureValue;
        }
        else if (transformedRate >= 0d)
        {
            var exponent = -periods * transformedRate;
            var discount = exponent < -745d ? 0d : Math.Exp(exponent);
            var annuity = -ExpMinusOne(exponent) / rate;
            first = presentValue;
            second = payment * (1d + (rate * paymentType)) * annuity;
            third = futureValue * discount;
        }
        else
        {
            var exponent = periods * transformedRate;
            var growth = exponent < -745d ? 0d : Math.Exp(exponent);
            var annuity = ExpMinusOne(exponent) / rate;
            first = futureValue;
            second = presentValue * growth;
            third = payment * (1d + (rate * paymentType)) * annuity;
        }

        if (!double.IsFinite(first) ||
            !double.IsFinite(second) ||
            !double.IsFinite(third))
        {
            normalizedValue = default;
            return false;
        }

        var scale = Math.Abs(first) + Math.Abs(second) + Math.Abs(third);
        if (!double.IsFinite(scale) || scale == 0d)
        {
            normalizedValue = default;
            return false;
        }
        normalizedValue = ((first + second) + third) / scale;
        return double.IsFinite(normalizedValue);
    }

    private static bool TryEvaluateXnpvActual(
        double transformedRate,
        IReadOnlyList<double> cashFlows,
        IReadOnlyList<double> years,
        out double result)
    {
        var sum = 0d;
        var compensation = 0d;
        for (var index = 0; index < cashFlows.Count; index++)
        {
            var exponent = -transformedRate * years[index];
            if (exponent > 709d)
            {
                result = default;
                return false;
            }
            var discount = exponent < -745d ? 0d : Math.Exp(exponent);
            var term = cashFlows[index] * discount;
            if (!double.IsFinite(term))
            {
                result = default;
                return false;
            }
            var adjusted = term - compensation;
            var next = sum + adjusted;
            compensation = (next - sum) - adjusted;
            sum = next;
        }
        result = sum;
        return double.IsFinite(result);
    }

    private static bool TryEvaluateXirrEquation(
        double transformedRate,
        IReadOnlyList<double> cashFlows,
        IReadOnlyList<double> years,
        EvaluationBudget budget,
        out double normalizedValue)
    {
        if (!budget.TryConsume(cashFlows.Count) ||
            !double.IsFinite(transformedRate) ||
            transformedRate < MinimumTransformedRate ||
            transformedRate > MaximumTransformedRate)
        {
            normalizedValue = default;
            return false;
        }

        var maximumExponent = 0d;
        if (transformedRate < 0d)
        {
            maximumExponent = -transformedRate * years[^1];
            if (!double.IsFinite(maximumExponent))
            {
                normalizedValue = default;
                return false;
            }
        }

        var sum = 0d;
        var compensation = 0d;
        var absoluteSum = 0d;
        for (var index = 0; index < cashFlows.Count; index++)
        {
            var exponent =
                (-transformedRate * years[index]) - maximumExponent;
            var factor = exponent < -745d ? 0d : Math.Exp(exponent);
            var term = cashFlows[index] * factor;
            if (!double.IsFinite(term))
            {
                normalizedValue = default;
                return false;
            }
            var adjusted = term - compensation;
            var next = sum + adjusted;
            compensation = (next - sum) - adjusted;
            sum = next;
            absoluteSum += Math.Abs(term);
        }

        if (!double.IsFinite(absoluteSum) || absoluteSum == 0d)
        {
            normalizedValue = default;
            return false;
        }
        normalizedValue = sum / absoluteSum;
        return double.IsFinite(normalizedValue);
    }

    private static bool TryReadSchedule(
        IReadOnlyList<CellValue> valueCells,
        IReadOnlyList<CellValue> dateCells,
        int minimumCount,
        bool requireOppositeSigns,
        out double[] cashFlows,
        out double[] years)
    {
        cashFlows = [];
        years = [];
        if (valueCells.Count != dateCells.Count ||
            valueCells.Count < minimumCount ||
            valueCells.Count > MaximumCashFlowCount)
        {
            return false;
        }

        cashFlows = new double[valueCells.Count];
        var dates = new double[dateCells.Count];
        var hasPositive = false;
        var hasNegative = false;
        for (var index = 0; index < valueCells.Count; index++)
        {
            if (!FormulaValueCoercion.TryNumber(
                    valueCells[index],
                    out var cashFlow) ||
                !double.IsFinite(cashFlow) ||
                !FormulaValueCoercion.TryNumber(
                    dateCells[index],
                    out var date) ||
                !double.IsFinite(date))
            {
                cashFlows = [];
                return false;
            }
            date = Math.Truncate(date);
            if (date < 0d)
            {
                cashFlows = [];
                return false;
            }
            cashFlows[index] = cashFlow;
            dates[index] = date;
            hasPositive |= cashFlow > 0d;
            hasNegative |= cashFlow < 0d;
        }

        var firstDate = dates[0];
        years = new double[dates.Length];
        for (var index = 0; index < dates.Length; index++)
        {
            if (dates[index] < firstDate)
            {
                cashFlows = [];
                years = [];
                return false;
            }
            years[index] = (dates[index] - firstDate) / 365d;
        }

        return !requireOppositeSigns || (hasPositive && hasNegative);
    }

    private static bool TryReadScalar(
        FormulaFunctionInvocation invocation,
        int index,
        out double value)
    {
        var values = invocation.Arguments[index].Values;
        return values.Count == 1 &&
               FormulaValueCoercion.TryNumber(values[0], out value) &&
               double.IsFinite(value);
    }

    private static bool TryReadOptionalScalar(
        FormulaFunctionInvocation invocation,
        int index,
        double defaultValue,
        out double value)
    {
        if (index >= invocation.Arguments.Count)
        {
            value = defaultValue;
            return true;
        }
        return TryReadScalar(invocation, index, out value);
    }

    private static bool TryPaymentType(double value, out int paymentType)
    {
        paymentType = (int)Math.Truncate(value);
        return paymentType is 0 or 1;
    }

    private static bool TryFindNearestRoot(
        TryEvaluateEquation evaluator,
        double initial,
        int scanSegments,
        out double root)
    {
        root = default;
        initial = Math.Clamp(
            initial,
            MinimumTransformedRate,
            MaximumTransformedRate);
        if (!evaluator(initial, out var initialValue))
        {
            return false;
        }
        if (Math.Abs(initialValue) <= RootTolerance)
        {
            root = initial;
            return true;
        }

        scanSegments = Math.Clamp(scanSegments, 32, MaximumScanSegments);
        var span = MaximumTransformedRate - MinimumTransformedRate;
        var step = span / scanSegments;
        var hasPrevious = false;
        var previousX = default(double);
        var previousValue = default(double);
        var bestX = initial;
        var bestAbsoluteValue = Math.Abs(initialValue);
        var hasBracket = false;
        var bestLower = default(double);
        var bestUpper = default(double);
        var bestDistance = double.PositiveInfinity;

        for (var segment = 0; segment <= scanSegments; segment++)
        {
            var x = segment == scanSegments
                ? MaximumTransformedRate
                : MinimumTransformedRate + (segment * step);
            if (!evaluator(x, out var value))
            {
                break;
            }
            var absoluteValue = Math.Abs(value);
            if (absoluteValue < bestAbsoluteValue)
            {
                bestAbsoluteValue = absoluteValue;
                bestX = x;
            }
            if (absoluteValue <= RootTolerance)
            {
                var distance = Math.Abs(x - initial);
                if (!hasBracket || distance < bestDistance)
                {
                    hasBracket = true;
                    bestLower = x;
                    bestUpper = x;
                    bestDistance = distance;
                }
            }
            else if (hasPrevious && HaveOppositeSigns(previousValue, value))
            {
                var distance = Math.Abs(
                    ((previousX + x) / 2d) - initial);
                if (!hasBracket || distance < bestDistance)
                {
                    hasBracket = true;
                    bestLower = previousX;
                    bestUpper = x;
                    bestDistance = distance;
                }
            }
            previousX = x;
            previousValue = value;
            hasPrevious = true;
        }

        if (hasBracket)
        {
            if (bestLower == bestUpper)
            {
                root = bestLower;
                return true;
            }
            return TryBisect(
                evaluator,
                bestLower,
                bestUpper,
                out root);
        }

        return TryRefineBestPoint(
            evaluator,
            bestX,
            bestAbsoluteValue,
            out root);
    }

    private static bool TryBisect(
        TryEvaluateEquation evaluator,
        double lower,
        double upper,
        out double root)
    {
        root = default;
        if (!evaluator(lower, out var lowerValue) ||
            !evaluator(upper, out var upperValue) ||
            !HaveOppositeSigns(lowerValue, upperValue))
        {
            return false;
        }

        for (var iteration = 0;
             iteration < MaximumRootIterations;
             iteration++)
        {
            var middle = lower + ((upper - lower) / 2d);
            if (!evaluator(middle, out var middleValue))
            {
                return false;
            }
            if (Math.Abs(middleValue) <= RootTolerance ||
                upper - lower <=
                RootTolerance * Math.Max(1d, Math.Abs(middle)))
            {
                root = middle;
                return true;
            }
            if (HaveOppositeSigns(lowerValue, middleValue))
            {
                upper = middle;
                upperValue = middleValue;
            }
            else
            {
                lower = middle;
                lowerValue = middleValue;
            }
        }
        return false;
    }

    private static bool TryRefineBestPoint(
        TryEvaluateEquation evaluator,
        double initial,
        double initialAbsoluteValue,
        out double root)
    {
        root = default;
        var x = initial;
        var bestAbsoluteValue = initialAbsoluteValue;
        for (var iteration = 0; iteration < 96; iteration++)
        {
            if (!evaluator(x, out var value))
            {
                return false;
            }
            var absoluteValue = Math.Abs(value);
            if (absoluteValue <= RootTolerance)
            {
                root = x;
                return true;
            }

            var h = 1e-5d * Math.Max(1d, Math.Abs(x));
            var left = Math.Max(MinimumTransformedRate, x - h);
            var right = Math.Min(MaximumTransformedRate, x + h);
            if (left == right ||
                !evaluator(left, out var leftValue) ||
                !evaluator(right, out var rightValue))
            {
                return false;
            }
            var derivative = (rightValue - leftValue) / (right - left);
            if (!double.IsFinite(derivative) ||
                Math.Abs(derivative) <= 1e-18d)
            {
                return false;
            }

            var candidate = Math.Clamp(
                x - (value / derivative),
                MinimumTransformedRate,
                MaximumTransformedRate);
            if (!evaluator(candidate, out var candidateValue))
            {
                return false;
            }
            var candidateAbsoluteValue = Math.Abs(candidateValue);
            if (candidateAbsoluteValue >= absoluteValue)
            {
                candidate = (candidate + x) / 2d;
                if (!evaluator(candidate, out candidateValue))
                {
                    return false;
                }
                candidateAbsoluteValue = Math.Abs(candidateValue);
            }
            if (candidateAbsoluteValue >= bestAbsoluteValue &&
                Math.Abs(candidate - x) <=
                RootTolerance * Math.Max(1d, Math.Abs(x)))
            {
                return false;
            }
            bestAbsoluteValue = Math.Min(
                bestAbsoluteValue,
                candidateAbsoluteValue);
            x = candidate;
        }
        return false;
    }

    private static bool HaveOppositeSigns(double left, double right) =>
        (left < 0d && right > 0d) ||
        (left > 0d && right < 0d);

    private static double ExpMinusOne(double value)
    {
        if (Math.Abs(value) > 1e-5d)
        {
            return Math.Exp(value) - 1d;
        }
        var square = value * value;
        return value +
               (square / 2d) +
               ((square * value) / 6d) +
               ((square * square) / 24d) +
               ((square * square * value) / 120d);
    }

    private static double LogOnePlus(double value)
    {
        if (Math.Abs(value) > 1e-4d)
        {
            return Math.Log(1d + value);
        }
        var square = value * value;
        return value -
               (square / 2d) +
               ((square * value) / 3d) -
               ((square * square) / 4d) +
               ((square * square * value) / 5d);
    }

    private static FormulaEvaluationResult NumericFailure() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.Numeric);

    private delegate bool TryEvaluateEquation(
        double transformedRate,
        out double value);

    private sealed class EvaluationBudget
    {
        private long _remaining;

        public EvaluationBudget(long maximum) => _remaining = maximum;

        public bool TryConsume(int count)
        {
            if (count < 0 || _remaining < count)
            {
                return false;
            }
            _remaining -= count;
            return true;
        }
    }
}
