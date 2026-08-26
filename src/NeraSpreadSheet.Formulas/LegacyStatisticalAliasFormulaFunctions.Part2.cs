using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Legacy statistical names that delegate to the authoritative modern
/// implementations while preserving logical argument and range identity.
/// </summary>
internal static class LegacyStatisticalAliasFormulaFunctionsPart2
{
    private static readonly Dictionary<string, IVersionedFormulaFunction>
        Targets = CreateTargetMap();

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateAlias(
            "NORMDIST",
            4,
            4,
            "NORM.DIST",
            FormulaFunctionCapabilities.ScalarArguments);
        yield return CreateAlias(
            "NORMINV",
            3,
            3,
            "NORM.INV",
            FormulaFunctionCapabilities.ScalarArguments);
        yield return CreateAlias(
            "NORMSDIST",
            1,
            1,
            "NORM.S.DIST",
            FormulaFunctionCapabilities.ScalarArguments,
            AppendCumulativeTrue);
        yield return CreateAlias(
            "NORMSINV",
            1,
            1,
            "NORM.S.INV",
            FormulaFunctionCapabilities.ScalarArguments);
        yield return CreateAlias(
            "POISSON",
            3,
            3,
            "POISSON.DIST",
            FormulaFunctionCapabilities.ScalarArguments);
        yield return CreateAlias(
            "WEIBULL",
            4,
            4,
            "WEIBULL.DIST",
            FormulaFunctionCapabilities.ScalarArguments);
        yield return CreateAlias(
            "RANK",
            2,
            3,
            "RANK.EQ",
            FormulaFunctionCapabilities.ScalarArguments |
            FormulaFunctionCapabilities.RangeArguments);
        yield return CreateAlias(
            "PERCENTILE",
            2,
            2,
            "PERCENTILE.INC",
            FormulaFunctionCapabilities.ScalarArguments |
            FormulaFunctionCapabilities.RangeArguments);
        yield return CreateAlias(
            "QUARTILE",
            2,
            2,
            "QUARTILE.INC",
            FormulaFunctionCapabilities.ScalarArguments |
            FormulaFunctionCapabilities.RangeArguments);
        yield return CreateAlias(
            "FORECAST",
            3,
            3,
            "FORECAST.LINEAR",
            FormulaFunctionCapabilities.ScalarArguments |
            FormulaFunctionCapabilities.RangeArguments);
    }

    private static FormulaFunctionDefinition CreateAlias(
        string name,
        int minimumArguments,
        int maximumArguments,
        string targetName,
        FormulaFunctionCapabilities argumentCapabilities,
        Func<
            IReadOnlyList<FormulaFunctionArgument>,
            FormulaFunctionArgument[]>? argumentAdapter = null) =>
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
            invocation => InvokeTarget(
                targetName,
                invocation,
                argumentAdapter));

    private static FormulaEvaluationResult InvokeTarget(
        string targetName,
        FormulaFunctionInvocation invocation,
        Func<
            IReadOnlyList<FormulaFunctionArgument>,
            FormulaFunctionArgument[]>? argumentAdapter)
    {
        if (!Targets.TryGetValue(targetName, out var target))
        {
            throw new InvalidOperationException(
                $"Missing legacy statistical target '{targetName}'.");
        }

        var arguments = argumentAdapter is null
            ? invocation.Arguments.ToArray()
            : argumentAdapter(invocation.Arguments);
        return target.Invoke(new FormulaFunctionInvocation(
            arguments,
            invocation.Context));
    }

    private static FormulaFunctionArgument[] AppendCumulativeTrue(
        IReadOnlyList<FormulaFunctionArgument> arguments)
    {
        var result = new FormulaFunctionArgument[arguments.Count + 1];
        for (var index = 0; index < arguments.Count; index++)
        {
            result[index] = arguments[index];
        }
        result[^1] = FormulaFunctionArgument.Scalar(
            CellValue.FromBoolean(true));
        return result;
    }

    private static Dictionary<string, IVersionedFormulaFunction>
        CreateTargetMap()
    {
        var targets = new Dictionary<string, IVersionedFormulaFunction>(
            StringComparer.OrdinalIgnoreCase);
        AddTargets(targets, StatisticalFormulaFunctions.Create());
        AddTargets(targets, AdvancedStatisticalFormulaFunctions.Create());
        return targets;
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
            targets[function.Name] = versioned;
        }
    }
}
