using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class StatisticalCompatibilityFormulaFunctionsGroupB
{
    private const int MaximumValues = 2_000_000;
    private const double ProbabilityTolerance = 1e-10d;

    private static readonly Dictionary<string, IVersionedFormulaFunction>
        Targets = CreateTargetMap();

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateAlias("STDEV", 1, 255, "STDEV.S");
        yield return CreateAlias("STDEVP", 1, 255, "STDEV.P");
        yield return CreateAlias("VAR", 1, 255, "VAR.S");
        yield return CreateAlias("VARP", 1, 255, "VAR.P");
        yield return CreateAlias("TINV", 2, 2, "T.INV.2T");
        yield return CreateScalarDefinition("TDIST", 3, 3, EvaluateLegacyTDistribution);
        yield return CreateScalarDefinition("CONFIDENCE", 3, 3, EvaluateConfidenceNormal);
        yield return CreateScalarDefinition("CONFIDENCE.NORM", 3, 3, EvaluateConfidenceNormal);
        yield return CreateScalarDefinition("CONFIDENCE.T", 3, 3, EvaluateConfidenceT);
        yield return CreateRangeDefinition("PROB", 3, 4, EvaluateProbabilityRange);
    }

    private static FormulaFunctionDefinition CreateAlias(
        string name,
        int minimumArguments,
        int maximumArguments,
        string targetName) =>
        CreateDefinition(
            name,
            minimumArguments,
            maximumArguments,
            FormulaFunctionCapabilities.ScalarArguments |
            FormulaFunctionCapabilities.RangeArguments,
            invocation => InvokeTarget(targetName, invocation.Arguments, invocation.Context));

    private static FormulaFunctionDefinition CreateScalarDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        CreateDefinition(
            name,
            minimumArguments,
            maximumArguments,
            FormulaFunctionCapabilities.ScalarArguments,
            evaluator);

    private static FormulaFunctionDefinition CreateRangeDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        CreateDefinition(
            name,
            minimumArguments,
            maximumArguments,
            FormulaFunctionCapabilities.ScalarArguments |
            FormulaFunctionCapabilities.RangeArguments,
            evaluator);

    private static FormulaFunctionDefinition CreateDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        FormulaFunctionCapabilities argumentCapabilities,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                argumentCapabilities |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaEvaluationResult EvaluateLegacyTDistribution(
        FormulaFunctionInvocation invocation)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var x, out var error) ||
            !TryGetTruncatedInteger(invocation.Arguments[2], out var tails, out error))
        {
            return error;
        }
        if (x < 0d || tails is < 1 or > 2)
        {
            return NumericError();
        }

        var targetName = tails == 1 ? "T.DIST.RT" : "T.DIST.2T";
        return InvokeTarget(
            targetName,
            [invocation.Arguments[0], invocation.Arguments[1]],
            invocation.Context);
    }

    private static FormulaEvaluationResult EvaluateConfidenceNormal(
        FormulaFunctionInvocation invocation) =>
        EvaluateConfidence(invocation, studentT: false);

    private static FormulaEvaluationResult EvaluateConfidenceT(
        FormulaFunctionInvocation invocation) =>
        EvaluateConfidence(invocation, studentT: true);

    private static FormulaEvaluationResult EvaluateConfidence(
        FormulaFunctionInvocation invocation,
        bool studentT)
    {
        if (!TryGetScalarNumber(invocation.Arguments[0], out var alpha, out var error) ||
            !TryGetScalarNumber(invocation.Arguments[1], out var standardDeviation, out error) ||
            !TryGetTruncatedInteger(invocation.Arguments[2], out var size, out error))
        {
            return error;
        }
        var minimumSize = studentT ? 2 : 1;
        if (alpha <= 0d || alpha >= 1d ||
            standardDeviation <= 0d || size < minimumSize)
        {
            return NumericError();
        }

        FormulaEvaluationResult quantile;
        if (studentT)
        {
            quantile = InvokeTarget(
                "T.INV.2T",
                [
                    FormulaFunctionArgument.Scalar(CellValue.FromNumber(alpha)),
                    FormulaFunctionArgument.Scalar(CellValue.FromNumber(size - 1d)),
                ],
                invocation.Context);
        }
        else
        {
            quantile = InvokeTarget(
                "NORM.S.INV",
                [FormulaFunctionArgument.Scalar(
                    CellValue.FromNumber(1d - (alpha / 2d)))],
                invocation.Context);
        }
        if (!quantile.IsSuccess)
        {
            return quantile;
        }
        if (quantile.Value.Kind != CellValueKind.Number)
        {
            return InvalidValue();
        }
        var multiplier = (double)quantile.Value.RawValue!;
        return Number(multiplier * standardDeviation / Math.Sqrt(size));
    }

    private static FormulaEvaluationResult EvaluateProbabilityRange(
        FormulaFunctionInvocation invocation)
    {
        var xValues = invocation.Arguments[0].Values;
        var probabilities = invocation.Arguments[1].Values;
        if (xValues.Count == 0 || xValues.Count != probabilities.Count)
        {
            return NotAvailable();
        }
        if (xValues.Count > MaximumValues)
        {
            return NumericError();
        }
        if (!TryGetScalarNumber(invocation.Arguments[2], out var lower, out var error))
        {
            return error;
        }
        var upper = lower;
        if (invocation.Arguments.Count == 4 &&
            !TryGetScalarNumber(invocation.Arguments[3], out upper, out error))
        {
            return error;
        }

        var totalProbability = 0d;
        var selectedProbability = 0d;
        var totalCompensation = 0d;
        var selectedCompensation = 0d;
        for (var index = 0; index < xValues.Count; index++)
        {
            if (!TryGetValueNumber(xValues[index], out var x) ||
                !TryGetValueNumber(probabilities[index], out var probability))
            {
                return InvalidValue();
            }
            if (probability < 0d || probability > 1d)
            {
                return NumericError();
            }
            AddCompensated(
                probability,
                ref totalProbability,
                ref totalCompensation);
            if (x >= lower && x <= upper)
            {
                AddCompensated(
                    probability,
                    ref selectedProbability,
                    ref selectedCompensation);
            }
        }
        if (Math.Abs(totalProbability - 1d) > ProbabilityTolerance)
        {
            return NumericError();
        }
        return Number(selectedProbability);
    }

    private static FormulaEvaluationResult InvokeTarget(
        string targetName,
        IReadOnlyList<FormulaFunctionArgument> arguments,
        IFormulaEvaluationContext context)
    {
        if (!Targets.TryGetValue(targetName, out var target))
        {
            throw new InvalidOperationException(
                $"Missing statistical compatibility target '{targetName}'.");
        }
        return target.Invoke(new FormulaFunctionInvocation(arguments, context));
    }

    private static Dictionary<string, IVersionedFormulaFunction>
        CreateTargetMap()
    {
        var result = new Dictionary<string, IVersionedFormulaFunction>(
            StringComparer.OrdinalIgnoreCase);
        AddTargets(result, StatisticalFormulaFunctions.Create());
        AddTargets(result, AdvancedStatisticalFormulaFunctions.Create());
        AddTargets(result, ContinuousDistributionFormulaFunctions.Create());
        return result;
    }

    private static void AddTargets(
        Dictionary<string, IVersionedFormulaFunction> targets,
        IEnumerable<IFormulaFunction> functions)
    {
        foreach (var function in functions)
        {
            if (function is not IVersionedFormulaFunction versioned)
            {
                throw new InvalidOperationException(
                    $"Target '{function.Name}' is not versioned.");
            }
            targets.TryAdd(function.Name, versioned);
        }
    }

    private static bool TryGetScalarNumber(
        FormulaFunctionArgument argument,
        out double number,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            !TryGetValueNumber(argument.ScalarValue, out number))
        {
            number = default;
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
        if (!TryGetScalarNumber(argument, out var number, out error))
        {
            value = default;
            return false;
        }
        if (number < int.MinValue || number > int.MaxValue)
        {
            value = default;
            error = NumericError();
            return false;
        }
        value = checked((int)Math.Truncate(number));
        return true;
    }

    private static bool TryGetValueNumber(CellValue value, out double number) =>
        FormulaValueCoercion.TryNumber(value, out number, allowText: true) &&
        double.IsFinite(number);

    private static void AddCompensated(
        double value,
        ref double sum,
        ref double compensation)
    {
        var corrected = value - compensation;
        var updated = sum + corrected;
        compensation = (updated - sum) - corrected;
        sum = updated;
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
