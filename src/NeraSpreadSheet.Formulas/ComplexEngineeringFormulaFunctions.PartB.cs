using System.Numerics;

namespace NeraSpreadSheet.Formulas;

internal static class ComplexEngineeringFormulaFunctionsPartB
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateScalarDefinition("IMSIN", 1, 1, invocation =>
            UnaryComplex(invocation, Complex.Sin));
        yield return CreateScalarDefinition("IMSINH", 1, 1, invocation =>
            UnaryComplex(invocation, Complex.Sinh));
        yield return CreateScalarDefinition("IMSQRT", 1, 1, invocation =>
            UnaryComplex(invocation, Complex.Sqrt));
        yield return CreateScalarDefinition("IMSUB", 2, 2, EvaluateSubtraction);
        yield return CreateRangeDefinition("IMSUM", 1, 255, EvaluateSum);
        yield return CreateScalarDefinition("IMTAN", 1, 1, invocation =>
            UnaryComplex(invocation, Complex.Tan));
    }

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

    private static FormulaEvaluationResult EvaluateSubtraction(
        FormulaFunctionInvocation invocation)
    {
        if (!ComplexFormulaMath.TryRead(
                invocation.Arguments[0],
                out var left,
                out var error) ||
            !ComplexFormulaMath.TryRead(
                invocation.Arguments[1],
                out var right,
                out error))
        {
            return error;
        }
        if (!ComplexFormulaMath.TryMergeSuffix(
                left.Suffix,
                right.Suffix,
                out var suffix))
        {
            return ComplexFormulaMath.InvalidValue();
        }
        return ComplexFormulaMath.ComplexText(
            left.Value - right.Value,
            suffix == '\0' ? 'i' : suffix);
    }

    private static FormulaEvaluationResult EvaluateSum(
        FormulaFunctionInvocation invocation)
    {
        var result = Complex.Zero;
        var suffix = '\0';
        var count = 0;
        foreach (var argument in invocation.Arguments)
        {
            foreach (var value in argument.Values)
            {
                if (!ComplexFormulaMath.TryRead(
                        value,
                        out var operand,
                        out var error))
                {
                    return error;
                }
                if (!ComplexFormulaMath.TryMergeSuffix(
                        suffix,
                        operand.Suffix,
                        out suffix))
                {
                    return ComplexFormulaMath.InvalidValue();
                }
                result += operand.Value;
                count++;
                if (!ComplexFormulaMath.IsFinite(result))
                {
                    return ComplexFormulaMath.NumericError();
                }
            }
        }
        return count == 0
            ? ComplexFormulaMath.InvalidValue()
            : ComplexFormulaMath.ComplexText(
                result,
                suffix == '\0' ? 'i' : suffix);
    }

    private static FormulaEvaluationResult UnaryComplex(
        FormulaFunctionInvocation invocation,
        Func<Complex, Complex> operation)
    {
        if (!ComplexFormulaMath.TryRead(
                invocation.Arguments[0],
                out var operand,
                out var error))
        {
            return error;
        }
        return ComplexFormulaMath.ComplexText(
            operation(operand.Value),
            operand.EffectiveSuffix);
    }
}
