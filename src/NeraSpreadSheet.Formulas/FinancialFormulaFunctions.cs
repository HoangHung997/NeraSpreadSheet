using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class FinancialFormulaFunctions
{
    public const int MaximumCashFlowValues = 2_000_000;
    public const int MaximumIrrValues = 100_000;
    public const int MaximumIrrIterations = 100;
    public const int MaximumIrrBracketSamples = 64;

    private const double RootTolerance = 1e-10d;
    private const double MinimumIrrBase = 1e-12d;
    private const double MaximumIrrRate = 1e10d;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition("PV", 3, 5, EvaluatePresentValue);
        yield return CreateDefinition("FV", 3, 5, EvaluateFutureValue);
        yield return CreateDefinition("PMT", 3, 5, EvaluatePayment);
        yield return CreateDefinition("NPER", 3, 5, EvaluateNumberOfPeriods);
        yield return CreateDefinition(
            "NPV",
            2,
            int.MaxValue,
            EvaluateNetPresentValue,
            allowRanges: true);
        yield return CreateDefinition(
            "IRR",
            1,
            2,
            EvaluateInternalRateOfReturn,
            allowRanges: true);
        yield return CreateDefinition("IPMT", 4, 6, EvaluateInterestPayment);
        yield return CreateDefinition("PPMT", 4, 6, EvaluatePrincipalPayment);
        yield return CreateDefinition("SLN", 3, 3, EvaluateStraightLine);
        yield return CreateDefinition("SYD", 4, 4, EvaluateSumOfYearsDigits);
    }

    private static FormulaFunctionDefinition CreateDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator,
        bool allowRanges = false) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                FormulaFunctionCapabilities.ScalarArguments |
                (allowRanges
                    ? FormulaFunctionCapabilities.RangeArguments
                    : FormulaFunctionCapabilities.None) |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaEvaluationResult EvaluatePresentValue(
        FormulaFunctionInvocation invocation)
    {
        if (!TryReadAnnuityArguments(
                invocation,
                out var rate,
                out var periods,
                out var payment,
                out var futureValue,
                out var timing,
                out var error))
        {
            return error;
        }
        if (!IsValidRate(rate) || periods < 0d)
        {
            return NumericError();
        }
        if (rate == 0d)
        {
            return Number(-(futureValue + (payment * periods)));
        }
        if (!TryGrowth(rate, periods, out var growth))
        {
            return NumericError();
        }
        var annuity = (1d + (rate * timing)) *
                      ((growth - 1d) / rate);
        return Number(-(futureValue + (payment * annuity)) / growth);
    }

    private static FormulaEvaluationResult EvaluateFutureValue(
        FormulaFunctionInvocation invocation)
    {
        if (!TryReadAnnuityArguments(
                invocation,
                out var rate,
                out var periods,
                out var payment,
                out var presentValue,
                out var timing,
                out var error))
        {
            return error;
        }
        if (!IsValidRate(rate) || periods < 0d)
        {
            return NumericError();
        }
        return TryCalculateFutureValue(
                rate,
                periods,
                payment,
                presentValue,
                timing,
                out var result)
            ? Number(result)
            : NumericError();
    }

    private static FormulaEvaluationResult EvaluatePayment(
        FormulaFunctionInvocation invocation)
    {
        if (!TryReadAnnuityArguments(
                invocation,
                out var rate,
                out var periods,
                out var presentValue,
                out var futureValue,
                out var timing,
                out var error))
        {
            return error;
        }
        if (!IsValidRate(rate) || periods <= 0d)
        {
            return NumericError();
        }
        return TryCalculatePayment(
                rate,
                periods,
                presentValue,
                futureValue,
                timing,
                out var result,
                out error)
            ? Number(result)
            : error;
    }

    private static FormulaEvaluationResult EvaluateNumberOfPeriods(
        FormulaFunctionInvocation invocation)
    {
        if (!TryReadAnnuityArguments(
                invocation,
                out var rate,
                out var payment,
                out var presentValue,
                out var futureValue,
                out var timing,
                out var error))
        {
            return error;
        }
        if (!IsValidRate(rate))
        {
            return NumericError();
        }

        double periods;
        if (rate == 0d)
        {
            if (payment == 0d)
            {
                return DivisionByZero();
            }
            periods = -(presentValue + futureValue) / payment;
        }
        else
        {
            var adjustedPayment = payment * (1d + (rate * timing));
            var denominator = (presentValue * rate) + adjustedPayment;
            var numerator = adjustedPayment - (futureValue * rate);
            if (denominator == 0d)
            {
                return NumericError();
            }
            var ratio = numerator / denominator;
            var logarithm = Math.Log(1d + rate);
            if (ratio <= 0d || logarithm == 0d ||
                !double.IsFinite(ratio) || !double.IsFinite(logarithm))
            {
                return NumericError();
            }
            periods = Math.Log(ratio) / logarithm;
        }

        return !double.IsFinite(periods) || periods < 0d
            ? NumericError()
            : Number(periods);
    }

    private static FormulaEvaluationResult EvaluateNetPresentValue(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var rate,
                out var error))
        {
            return error;
        }
        if (!IsValidRate(rate))
        {
            return NumericError();
        }
        if (!TryCollectCashFlows(
                invocation.Arguments,
                startIndex: 1,
                MaximumCashFlowValues,
                out var values,
                out error))
        {
            return error;
        }

        var discountBase = 1d + rate;
        var discount = discountBase;
        var sum = 0d;
        var compensation = 0d;
        foreach (var value in values)
        {
            if (discount == 0d || double.IsNaN(discount))
            {
                return NumericError();
            }
            var term = double.IsPositiveInfinity(discount)
                ? 0d
                : value / discount;
            if (!double.IsFinite(term))
            {
                return NumericError();
            }
            AddCompensated(ref sum, ref compensation, term);
            discount *= discountBase;
        }
        return Number(sum);
    }

    private static FormulaEvaluationResult EvaluateInternalRateOfReturn(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectCashFlows(
                invocation.Arguments,
                startIndex: 0,
                argumentCount: 1,
                MaximumIrrValues,
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
        if (TryNewtonIrr(cashFlows, guess, tolerance, out var rate) ||
            TryBracketedIrr(cashFlows, guess, tolerance, out rate))
        {
            return Number(rate);
        }
        return NumericError();
    }

    private static FormulaEvaluationResult EvaluateInterestPayment(
        FormulaFunctionInvocation invocation)
    {
        if (!TryReadPaymentBreakdownArguments(
                invocation,
                out var rate,
                out var period,
                out var totalPeriods,
                out var presentValue,
                out var futureValue,
                out var timing,
                out var error))
        {
            return error;
        }
        if (!ValidatePaymentPeriod(rate, period, totalPeriods))
        {
            return NumericError();
        }
        if (!TryCalculatePayment(
                rate,
                totalPeriods,
                presentValue,
                futureValue,
                timing,
                out var payment,
                out error))
        {
            return error;
        }
        return TryCalculateInterestPayment(
                rate,
                period,
                payment,
                presentValue,
                timing,
                out var interest)
            ? Number(interest)
            : NumericError();
    }

    private static FormulaEvaluationResult EvaluatePrincipalPayment(
        FormulaFunctionInvocation invocation)
    {
        if (!TryReadPaymentBreakdownArguments(
                invocation,
                out var rate,
                out var period,
                out var totalPeriods,
                out var presentValue,
                out var futureValue,
                out var timing,
                out var error))
        {
            return error;
        }
        if (!ValidatePaymentPeriod(rate, period, totalPeriods))
        {
            return NumericError();
        }
        if (!TryCalculatePayment(
                rate,
                totalPeriods,
                presentValue,
                futureValue,
                timing,
                out var payment,
                out error))
        {
            return error;
        }
        if (!TryCalculateInterestPayment(
                rate,
                period,
                payment,
                presentValue,
                timing,
                out var interest))
        {
            return NumericError();
        }
        return Number(payment - interest);
    }

    private static FormulaEvaluationResult EvaluateStraightLine(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var cost, out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var salvage, out error) ||
            !TryGetScalarNumber(invocation.Arguments[2], out var life, out error))
        {
            return error;
        }
        return life <= 0d
            ? NumericError()
            : Number((cost - salvage) / life);
    }

    private static FormulaEvaluationResult EvaluateSumOfYearsDigits(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var cost, out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var salvage, out error) ||
            !TryGetScalarNumber(invocation.Arguments[2], out var life, out error) ||
            !TryGetScalarNumber(invocation.Arguments[3], out var period, out error))
        {
            return error;
        }
        if (life <= 0d || period <= 0d || period > life)
        {
            return NumericError();
        }
        var denominator = life * (life + 1d);
        return denominator == 0d || !double.IsFinite(denominator)
            ? NumericError()
            : Number(
                (cost - salvage) *
                (life - period + 1d) *
                2d /
                denominator);
    }

    private static bool TryReadAnnuityArguments(
        FormulaFunctionInvocation invocation,
        out double first,
        out double second,
        out double third,
        out double fourth,
        out int timing,
        out FormulaEvaluationResult error)
    {
        first = default;
        second = default;
        third = default;
        fourth = 0d;
        timing = 0;
        error = default!;

        if (!TryGetScalarNumber(invocation.Arguments[0], out first, out error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out second, out error) ||
            !TryGetScalarNumber(invocation.Arguments[2], out third, out error))
        {
            return false;
        }
        if (invocation.Arguments.Count >= 4 &&
            !TryGetScalarNumber(invocation.Arguments[3], out fourth, out error))
        {
            return false;
        }
        return invocation.Arguments.Count < 5 ||
               TryGetPaymentTiming(invocation.Arguments[4], out timing, out error);
    }

    private static bool TryReadPaymentBreakdownArguments(
        FormulaFunctionInvocation invocation,
        out double rate,
        out int period,
        out double totalPeriods,
        out double presentValue,
        out double futureValue,
        out int timing,
        out FormulaEvaluationResult error)
    {
        rate = default;
        period = default;
        totalPeriods = default;
        presentValue = default;
        futureValue = 0d;
        timing = 0;
        error = default!;

        if (!TryGetScalarNumber(invocation.Arguments[0], out rate, out error) ||
            !TryGetScalarInteger(invocation.Arguments[1], out period, out error) ||
            !TryGetScalarNumber(invocation.Arguments[2], out totalPeriods, out error) ||
            !TryGetScalarNumber(invocation.Arguments[3], out presentValue, out error))
        {
            return false;
        }
        if (invocation.Arguments.Count >= 5 &&
            !TryGetScalarNumber(invocation.Arguments[4], out futureValue, out error))
        {
            return false;
        }
        return invocation.Arguments.Count < 6 ||
               TryGetPaymentTiming(invocation.Arguments[5], out timing, out error);
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

    private static bool TryGetPaymentTiming(
        FormulaFunctionArgument argument,
        out int timing,
        out FormulaEvaluationResult error)
    {
        if (!TryGetScalarInteger(argument, out timing, out error) ||
            timing is < 0 or > 1)
        {
            timing = default;
            error = InvalidValue();
            return false;
        }
        return true;
    }

    private static bool TryCollectCashFlows(
        IReadOnlyList<FormulaFunctionArgument> arguments,
        int startIndex,
        int maximumValues,
        out double[] values,
        out FormulaEvaluationResult error) =>
        TryCollectCashFlows(
            arguments,
            startIndex,
            arguments.Count - startIndex,
            maximumValues,
            out values,
            out error);

    private static bool TryCollectCashFlows(
        IReadOnlyList<FormulaFunctionArgument> arguments,
        int startIndex,
        int argumentCount,
        int maximumValues,
        out double[] values,
        out FormulaEvaluationResult error)
    {
        var result = new List<double>();
        var endIndex = checked(startIndex + argumentCount);
        for (var argumentIndex = startIndex;
             argumentIndex < endIndex;
             argumentIndex++)
        {
            var argument = arguments[argumentIndex];
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
                if (result.Count > maximumValues)
                {
                    values = [];
                    error = NumericError();
                    return false;
                }
            }
        }
        values = result.ToArray();
        error = default!;
        return true;
    }

    private static bool TryCalculateFutureValue(
        double rate,
        double periods,
        double payment,
        double presentValue,
        int timing,
        out double result)
    {
        if (rate == 0d)
        {
            result = -(presentValue + (payment * periods));
            return double.IsFinite(result);
        }
        if (!TryGrowth(rate, periods, out var growth))
        {
            result = default;
            return false;
        }
        var annuity = (1d + (rate * timing)) *
                      ((growth - 1d) / rate);
        result = -((presentValue * growth) + (payment * annuity));
        return double.IsFinite(result);
    }

    private static bool TryCalculatePayment(
        double rate,
        double periods,
        double presentValue,
        double futureValue,
        int timing,
        out double result,
        out FormulaEvaluationResult error)
    {
        result = default;
        error = default!;
        if (periods <= 0d)
        {
            error = NumericError();
            return false;
        }
        if (rate == 0d)
        {
            result = -(presentValue + futureValue) / periods;
            if (!double.IsFinite(result))
            {
                error = NumericError();
                return false;
            }
            return true;
        }
        if (!TryGrowth(rate, periods, out var growth))
        {
            error = NumericError();
            return false;
        }
        var denominator = (1d + (rate * timing)) * (growth - 1d);
        if (denominator == 0d)
        {
            error = DivisionByZero();
            return false;
        }
        result = -((futureValue + (presentValue * growth)) * rate) /
                 denominator;
        if (!double.IsFinite(result))
        {
            error = NumericError();
            return false;
        }
        return true;
    }

    private static bool TryCalculateInterestPayment(
        double rate,
        int period,
        double payment,
        double presentValue,
        int timing,
        out double result)
    {
        if (rate == 0d || (timing == 1 && period == 1))
        {
            result = 0d;
            return true;
        }
        if (!TryCalculateFutureValue(
                rate,
                period - 1d,
                payment,
                presentValue,
                timing,
                out var balance))
        {
            result = default;
            return false;
        }
        result = balance * rate;
        if (timing == 1)
        {
            result /= 1d + rate;
        }
        return double.IsFinite(result);
    }

    private static bool ValidatePaymentPeriod(
        double rate,
        int period,
        double totalPeriods) =>
        IsValidRate(rate) &&
        period > 0 &&
        totalPeriods > 0d &&
        period <= totalPeriods + 1e-10d;

    private static bool TryGrowth(
        double rate,
        double periods,
        out double growth)
    {
        if (!IsValidRate(rate) || periods < 0d)
        {
            growth = default;
            return false;
        }
        growth = Math.Pow(1d + rate, periods);
        return double.IsFinite(growth) && growth > 0d;
    }

    private static bool IsValidRate(double rate) =>
        double.IsFinite(rate) && rate > -1d;

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
        return TryEvaluateIrr(cashFlows, rate, out var finalValue, out _) &&
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
            .Select(x => new IrrSample(x, EvaluateIrrAtX(cashFlows, x)))
            .Where(static sample => sample.Value.HasValue)
            .ToArray();

        var bestExactDistance = double.PositiveInfinity;
        var bestExactX = default(double);
        var foundExact = false;
        foreach (var sample in samples)
        {
            if (Math.Abs(sample.Value!.Value) > tolerance)
            {
                continue;
            }
            var distance = Math.Abs(sample.X - guessX);
            if (distance < bestExactDistance)
            {
                bestExactDistance = distance;
                bestExactX = sample.X;
                foundExact = true;
            }
        }
        if (foundExact)
        {
            rate = Math.Exp(bestExactX) - 1d;
            return true;
        }

        var foundBracket = false;
        var bestBracketDistance = double.PositiveInfinity;
        var leftX = default(double);
        var rightX = default(double);
        var leftValue = default(double);
        for (var index = 1; index < samples.Length; index++)
        {
            var left = samples[index - 1];
            var right = samples[index];
            if (Math.Sign(left.Value!.Value) == Math.Sign(right.Value!.Value))
            {
                continue;
            }
            var distance = Math.Abs(((left.X + right.X) / 2d) - guessX);
            if (distance < bestBracketDistance)
            {
                bestBracketDistance = distance;
                leftX = left.X;
                rightX = right.X;
                leftValue = left.Value.Value;
                foundBracket = true;
            }
        }
        if (!foundBracket)
        {
            rate = default;
            return false;
        }

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
        return TryEvaluateIrr(cashFlows, rate, out var value, out _) &&
               Math.Abs(value) <= tolerance;
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
        if (!IsValidRate(rate))
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
            if (index > 0)
            {
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

    private static FormulaEvaluationResult DivisionByZero() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.DivisionByZero);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());

    private readonly record struct IrrSample(double X, double? Value);
}
