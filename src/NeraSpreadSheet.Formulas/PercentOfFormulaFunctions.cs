using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class PercentOfFormulaFunctions
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return new FormulaFunctionDefinition(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity(
                    "NERA.BUILTIN",
                    "PERCENTOF"),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                2,
                2,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                propagateArgumentErrors: false,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            EvaluatePercentOf);
    }

    private static FormulaEvaluationResult EvaluatePercentOf(
        FormulaFunctionInvocation invocation)
    {
        if (!TrySum(invocation.Arguments[0], out var subset, out var error) ||
            !TrySum(invocation.Arguments[1], out var total, out error))
        {
            return new FormulaEvaluationResult(
                error,
                FormulaErrorMapping.ToErrorCode(error),
                Array.Empty<FormulaDependency>());
        }
        if (Math.Abs(total) <= double.Epsilon)
        {
            return FormulaEvaluationResult.Failure(
                FormulaErrorCode.DivisionByZero);
        }

        var value = FormulaValueCoercion.SafeNumber(subset / total);
        return value.Kind == CellValueKind.Error
            ? new FormulaEvaluationResult(
                value,
                FormulaErrorMapping.ToErrorCode(value),
                Array.Empty<FormulaDependency>())
            : FormulaEvaluationResult.Success(value);
    }

    private static bool TrySum(
        FormulaFunctionArgument argument,
        out double sum,
        out CellValue error)
    {
        sum = 0d;
        foreach (var value in argument.Values)
        {
            if (value.Kind == CellValueKind.Error)
            {
                error = value;
                return false;
            }
            if (value.Kind is not (
                    CellValueKind.Number or CellValueKind.DateTime))
            {
                continue;
            }
            if (!FormulaValueCoercion.TryNumber(value, out var number))
            {
                error = CellValue.FromError("#VALUE!");
                return false;
            }
            sum += number;
            if (!double.IsFinite(sum))
            {
                error = CellValue.FromError("#NUM!");
                return false;
            }
        }

        error = default;
        return true;
    }
}
