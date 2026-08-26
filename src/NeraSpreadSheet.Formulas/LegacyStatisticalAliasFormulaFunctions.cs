using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class LegacyStatisticalAliasFormulaFunctions
{
    private static readonly Dictionary<string, IVersionedFormulaFunction>
        Targets = CreateTargetMap();

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateAlias(
            "BETADIST",
            3,
            5,
            "BETA.DIST",
            InsertCumulativeArgument);
        yield return CreateAlias("BETAINV", 3, 5, "BETA.INV");
        yield return CreateAlias("BINOMDIST", 4, 4, "BINOM.DIST");
        yield return CreateAlias("CHIDIST", 2, 2, "CHISQ.DIST.RT");
        yield return CreateAlias("CHIINV", 2, 2, "CHISQ.INV.RT");
        yield return CreateAlias("COVAR", 2, 2, "COVARIANCE.P");
        yield return CreateAlias("EXPONDIST", 3, 3, "EXPON.DIST");
        yield return CreateAlias("FDIST", 3, 3, "F.DIST.RT");
        yield return CreateAlias("FINV", 3, 3, "F.INV.RT");
        yield return CreateAlias("GAMMADIST", 4, 4, "GAMMA.DIST");
        yield return CreateAlias("GAMMAINV", 3, 3, "GAMMA.INV");
        yield return CreateAlias("LOGINV", 3, 3, "LOGNORM.INV");
        yield return CreateAlias(
            "LOGNORMDIST",
            3,
            3,
            "LOGNORM.DIST",
            AppendCumulativeArgument);
        yield return CreateAlias("MODE", 1, 255, "MODE.SNGL");
    }

    private static FormulaFunctionDefinition CreateAlias(
        string name,
        int minimumArguments,
        int maximumArguments,
        string targetName,
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
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
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

    private static FormulaFunctionArgument[] InsertCumulativeArgument(
        IReadOnlyList<FormulaFunctionArgument> arguments)
    {
        var result = new FormulaFunctionArgument[arguments.Count + 1];
        for (var index = 0; index < 3; index++)
        {
            result[index] = arguments[index];
        }
        result[3] = FormulaFunctionArgument.Scalar(
            CellValue.FromBoolean(true));
        for (var index = 3; index < arguments.Count; index++)
        {
            result[index + 1] = arguments[index];
        }
        return result;
    }

    private static FormulaFunctionArgument[] AppendCumulativeArgument(
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
        AddTargets(targets, ContinuousDistributionFormulaFunctions.Create());
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
            targets.Add(function.Name, versioned);
        }
    }
}
