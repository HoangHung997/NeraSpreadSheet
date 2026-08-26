using System.Numerics;

namespace NeraSpreadSheet.Formulas;

internal static class ComplexEngineeringFormulaFunctionsPartA
{
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateScalarDefinition("COMPLEX", 2, 3, EvaluateComplex);
        yield return CreateScalarDefinition("IMABS", 1, 1, invocation =>
            UnaryNumber(invocation, Complex.Abs));
        yield return CreateScalarDefinition("IMAGINARY", 1, 1, invocation =>
            UnaryNumber(invocation, static value => value.Imaginary));
        yield return CreateScalarDefinition("IMARGUMENT", 1, 1, EvaluateArgument);
        yield return CreateScalarDefinition("IMCONJUGATE", 1, 1, invocation =>
            UnaryComplex(invocation, Complex.Conjugate));
        yield return CreateScalarDefinition("IMCOS", 1, 1, invocation =>
            UnaryComplex(invocation, Complex.Cos));
        yield return CreateScalarDefinition("IMCOSH", 1, 1, invocation =>
            UnaryComplex(invocation, Complex.Cosh));
        yield return CreateScalarDefinition("IMCOT", 1, 1, invocation =>
            ReciprocalUnary(invocation, Complex.Tan));
        yield return CreateScalarDefinition("IMCSC", 1, 1, invocation =>
            ReciprocalUnary(invocation, Complex.Sin));
        yield return CreateScalarDefinition("IMCSCH", 1, 1, invocation =>
            ReciprocalUnary(invocation, Complex.Sinh));
        yield return CreateScalarDefinition("IMDIV", 2, 2, EvaluateDivision);
        yield return CreateScalarDefinition("IMEXP", 1, 1, invocation =>
            UnaryComplex(invocation, Complex.Exp));
        yield return CreateScalarDefinition("IMLN", 1, 1, invocation =>
            Logarithm(invocation, Math.E));
        yield return CreateScalarDefinition("IMLOG10", 1, 1, invocation =>
            Logarithm(invocation, 10d));
        yield return CreateScalarDefinition("IMLOG2", 1, 1, invocation =>
            Logarithm(invocation, 2d));
        yield return CreateScalarDefinition("IMPOWER", 2, 2, EvaluatePower);
        yield return CreateRangeDefinition("IMPRODUCT", 1, 255, EvaluateProduct);
        yield return CreateScalarDefinition("IMREAL", 1, 1, invocation =>
            UnaryNumber(invocation, static value => value.Real));
        yield return CreateScalarDefinition("IMSEC", 1, 1, invocation =>
            ReciprocalUnary(invocation, Complex.Cos));
        yield return CreateScalarDefinition("IMSECH", 1, 1, invocation =>
            ReciprocalUnary(invocation, Complex.Cosh));
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

    private static FormulaEvaluationResult EvaluateComplex(
        FormulaFunctionInvocation invocation)
    {
        if (!ComplexFormulaMath.TryReadReal(
                invocation.Arguments[0],
                out var real,
                out var error) ||
            !ComplexFormulaMath.TryReadReal(
                invocation.Arguments[1],
                out var imaginary,
                out error))
        {
            return error;
        }
        var suffix = 'i';
        if (invocation.Arguments.Count == 3 &&
            !ComplexFormulaMath.TryReadSuffix(
                invocation.Arguments[2],
                out suffix,
                out error))
        {
            return error;
        }
        return ComplexFormulaMath.ComplexText(
            new Complex(real, imaginary),
            suffix);
    }

    private static FormulaEvaluationResult EvaluateArgument(
        FormulaFunctionInvocation invocation)
    {
        if (!ComplexFormulaMath.TryRead(
                invocation.Arguments[0],
                out var operand,
                out var error))
        {
            return error;
        }
        return ComplexFormulaMath.IsZero(operand.Value)
            ? ComplexFormulaMath.DivisionByZero()
            : ComplexFormulaMath.Number(Math.Atan2(
                operand.Value.Imaginary,
                operand.Value.Real));
    }

    private static FormulaEvaluationResult EvaluateDivision(
        FormulaFunctionInvocation invocation)
    {
        if (!TryReadPair(
                invocation,
                out var left,
                out var right,
                out var suffix,
                out var error))
        {
            return error;
        }
        if (ComplexFormulaMath.IsZero(right.Value))
        {
            return ComplexFormulaMath.NumericError();
        }
        return ComplexFormulaMath.ComplexText(
            left.Value / right.Value,
            suffix);
    }

    private static FormulaEvaluationResult EvaluatePower(
        FormulaFunctionInvocation invocation)
    {
        if (!ComplexFormulaMath.TryRead(
                invocation.Arguments[0],
                out var operand,
                out var error) ||
            !ComplexFormulaMath.TryReadReal(
                invocation.Arguments[1],
                out var exponent,
                out error))
        {
            return error;
        }
        if (ComplexFormulaMath.IsZero(operand.Value) && exponent <= 0d)
        {
            return ComplexFormulaMath.NumericError();
        }
        return ComplexFormulaMath.ComplexText(
            Complex.Pow(operand.Value, exponent),
            operand.EffectiveSuffix);
    }

    private static FormulaEvaluationResult EvaluateProduct(
        FormulaFunctionInvocation invocation)
    {
        var result = Complex.One;
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
                result *= operand.Value;
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

    private static FormulaEvaluationResult UnaryNumber(
        FormulaFunctionInvocation invocation,
        Func<Complex, double> operation)
    {
        if (!ComplexFormulaMath.TryRead(
                invocation.Arguments[0],
                out var operand,
                out var error))
        {
            return error;
        }
        return ComplexFormulaMath.Number(operation(operand.Value));
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

    private static FormulaEvaluationResult ReciprocalUnary(
        FormulaFunctionInvocation invocation,
        Func<Complex, Complex> denominatorFunction)
    {
        if (!ComplexFormulaMath.TryRead(
                invocation.Arguments[0],
                out var operand,
                out var error))
        {
            return error;
        }
        var denominator = denominatorFunction(operand.Value);
        if (ComplexFormulaMath.IsZero(denominator))
        {
            return ComplexFormulaMath.NumericError();
        }
        return ComplexFormulaMath.ComplexText(
            Complex.One / denominator,
            operand.EffectiveSuffix);
    }

    private static FormulaEvaluationResult Logarithm(
        FormulaFunctionInvocation invocation,
        double @base)
    {
        if (!ComplexFormulaMath.TryRead(
                invocation.Arguments[0],
                out var operand,
                out var error))
        {
            return error;
        }
        if (ComplexFormulaMath.IsZero(operand.Value))
        {
            return ComplexFormulaMath.NumericError();
        }
        var result = Complex.Log(operand.Value);
        if (@base != Math.E)
        {
            result /= Math.Log(@base);
        }
        return ComplexFormulaMath.ComplexText(
            result,
            operand.EffectiveSuffix);
    }

    private static bool TryReadPair(
        FormulaFunctionInvocation invocation,
        out ComplexFormulaMath.Operand left,
        out ComplexFormulaMath.Operand right,
        out char suffix,
        out FormulaEvaluationResult error)
    {
        right = default;
        if (!ComplexFormulaMath.TryRead(
                invocation.Arguments[0],
                out left,
                out error) ||
            !ComplexFormulaMath.TryRead(
                invocation.Arguments[1],
                out right,
                out error))
        {
            suffix = default;
            return false;
        }
        if (!ComplexFormulaMath.TryMergeSuffix(
                left.Suffix,
                right.Suffix,
                out suffix))
        {
            error = ComplexFormulaMath.InvalidValue();
            return false;
        }
        suffix = suffix == '\0' ? 'i' : suffix;
        error = default!;
        return true;
    }
}
