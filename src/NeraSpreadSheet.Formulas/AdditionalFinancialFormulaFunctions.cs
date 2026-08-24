namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Adds bounded solvers for annuity rates and irregular dated cash flows.
/// </summary>
internal static partial class AdditionalFinancialFormulaFunctions
{
    public const int MaximumScheduledValues = 2_000_000;
    public const int MaximumXirrValues = 100_000;
    public const int MaximumRootIterations = 100;
    public const int MaximumRootBracketSamples = 128;

    private const int MaximumNewtonBacktracks = 20;
    private const double ResidualTolerance = 1e-12d;
    private const double RateTolerance = 1e-12d;
    private const double MinimumRateBase = 1e-12d;
    private const double MaximumRate = 1e10d;
    private const double MaximumLogarithm = 709.782712893384d;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition(
            "RATE",
            3,
            6,
            EvaluateRate);
        yield return CreateDefinition(
            "XNPV",
            3,
            3,
            EvaluateExtendedNetPresentValue,
            allowRanges: true);
        yield return CreateDefinition(
            "XIRR",
            2,
            3,
            EvaluateExtendedInternalRateOfReturn,
            allowRanges: true);
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

    private static FormulaEvaluationResult EvaluateRate(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(
                invocation.Arguments[0],
                out var periods,
                out var error) ||
            !TryGetScalarNumber(
                invocation.Arguments[1],
                out var payment,
                out error) ||
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out var presentValue,
                out error))
        {
            return error;
        }

        var futureValue = 0d;
        if (invocation.Arguments.Count >= 4 &&
            !TryGetScalarNumber(
                invocation.Arguments[3],
                out futureValue,
                out error))
        {
            return error;
        }

        var timing = 0;
        if (invocation.Arguments.Count >= 5 &&
            !TryGetPaymentTiming(
                invocation.Arguments[4],
                out timing,
                out error))
        {
            return error;
        }

        var guess = 0.1d;
        if (invocation.Arguments.Count >= 6 &&
            !TryGetScalarNumber(
                invocation.Arguments[5],
                out guess,
                out error))
        {
            return error;
        }

        if (periods <= 0d || !IsValidSolverRate(guess))
        {
            return NumericError();
        }

        var paymentMagnitude = Math.Abs(payment) * periods;
        if (!double.IsFinite(paymentMagnitude))
        {
            return NumericError();
        }
        var tolerance = ResidualTolerance *
            Math.Max(
                1d,
                Math.Max(
                    Math.Abs(presentValue),
                    Math.Max(
                        Math.Abs(futureValue),
                        paymentMagnitude)));

        if (!TrySolveRoot(
                guess,
                tolerance,
                (double rate, out double value, out double derivative) =>
                    TryEvaluateRateEquation(
                        rate,
                        periods,
                        payment,
                        presentValue,
                        futureValue,
                        timing,
                        out value,
                        out derivative),
                out var result))
        {
            return NumericError();
        }

        return Number(result);
    }

    private static FormulaEvaluationResult EvaluateExtendedNetPresentValue(
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
        if (!TryCollectSchedule(
                invocation.Arguments[1],
                invocation.Arguments[2],
                MaximumScheduledValues,
                out var schedule,
                out _,
                out error))
        {
            return error;
        }
        if (!HasBothCashFlowSigns(schedule))
        {
            return NumericError();
        }
        if (!TryEvaluateSchedule(
                schedule,
                rate,
                out var value,
                out _))
        {
            return NumericError();
        }
        return Number(value);
    }

    private static FormulaEvaluationResult EvaluateExtendedInternalRateOfReturn(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectSchedule(
                invocation.Arguments[0],
                invocation.Arguments[1],
                MaximumXirrValues,
                out var schedule,
                out var hasLaterDate,
                out var error))
        {
            return error;
        }
        if (schedule.Length < 2 ||
            !hasLaterDate ||
            !HasBothCashFlowSigns(schedule))
        {
            return NumericError();
        }

        var guess = 0.1d;
        if (invocation.Arguments.Count == 3 &&
            !TryGetScalarNumber(
                invocation.Arguments[2],
                out guess,
                out error))
        {
            return error;
        }
        if (!IsValidSolverRate(guess))
        {
            return NumericError();
        }

        var maximumMagnitude = schedule.Max(static cashFlow =>
            Math.Abs(cashFlow.Value));
        var tolerance = ResidualTolerance *
            Math.Max(1d, maximumMagnitude);
        if (!TrySolveRoot(
                guess,
                tolerance,
                (double rate, out double value, out double derivative) =>
                    TryEvaluateSchedule(
                        schedule,
                        rate,
                        out value,
                        out derivative),
                out var result))
        {
            return NumericError();
        }

        return Number(result);
    }
}
