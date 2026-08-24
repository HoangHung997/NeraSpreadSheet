using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class StandardFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> CreateAll()
    {
        foreach (var function in AggregateLogicalFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in MathFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in TextFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in DateTimeFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in StatisticalFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in AdvancedStatisticalFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in FinancialFormulaFunctionsHardened.Create())
        {
            yield return function;
        }
        foreach (var function in EngineeringFormulaFunctions.Create())
        {
            yield return function;
        }
        foreach (var function in DatabaseFormulaFunctions.Create())
        {
            yield return function;
        }
    }
}

internal static class FormulaFunctionFactory
{
    public static IFormulaFunction Create(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<IReadOnlyList<CellValue>, IFormulaEvaluationContext, CellValue>
            evaluator,
        bool propagateErrors = true,
        FormulaFunctionVolatility volatility =
            FormulaFunctionVolatility.Deterministic,
        FormulaFunctionSecurityClassification securityClassification =
            FormulaFunctionSecurityClassification.Pure,
        FormulaFunctionDependencyPolicy dependencyPolicy =
            FormulaFunctionDependencyPolicy.EngineCapturedOnly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalizedName = FormulaFunctionName.Normalize(name);
        var readsClock = normalizedName is "TODAY" or "NOW";
        var effectiveVolatility = readsClock &&
                                  volatility ==
                                  FormulaFunctionVolatility.Deterministic
            ? FormulaFunctionVolatility.Volatile
            : volatility;
        var effectiveSecurity = readsClock &&
                                securityClassification ==
                                FormulaFunctionSecurityClassification.Pure
            ? FormulaFunctionSecurityClassification.ContextReadOnly
            : securityClassification;
        return new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "NERA.BUILTIN",
                    normalizedName),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                effectiveVolatility,
                effectiveSecurity,
                dependencyPolicy,
                propagateErrors,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.FlattenedValues),
            invocation => FormulaEvaluationResult.Success(
                evaluator(
                    invocation.FlattenValues(),
                    invocation.Context)));
    }
}
