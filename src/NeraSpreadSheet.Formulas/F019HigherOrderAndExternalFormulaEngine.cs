using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class F019HigherOrderExternalFormulaFunctions
{
    private const FormulaFunctionCapabilities ScalarRange =
        FormulaFunctionCapabilities.ScalarArguments |
        FormulaFunctionCapabilities.RangeArguments;

    private static readonly (string Name, int Min, int Max)[] ExternalFunctions =
    [
        ("CALL", 3, 255),
        ("REGISTER.ID", 2, 3),
        ("CUBEKPIMEMBER", 3, 4),
        ("CUBEMEMBER", 2, 3),
        ("CUBEMEMBERPROPERTY", 3, 3),
        ("CUBERANKEDMEMBER", 3, 4),
        ("CUBESET", 2, 5),
        ("CUBESETCOUNT", 1, 1),
        ("CUBEVALUE", 2, 255),
        ("RTD", 3, 255),
        ("COPILOT", 1, 255),
    ];

    public static IEnumerable<IFormulaFunction> Create()
    {
        foreach (var function in ExternalFunctions)
        {
            yield return CreateExternal(function.Name, function.Min, function.Max);
        }
    }

    private static FormulaFunctionDefinition CreateExternal(string name, int min, int max) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                min,
                max,
                ScalarRange | FormulaFunctionCapabilities.ReturnsScalar,
                FormulaFunctionVolatility.ExternalState,
                FormulaFunctionSecurityClassification.ExternalState,
                FormulaFunctionDependencyPolicy.EngineCapturedOnly,
                propagateArgumentErrors: true,
                argumentCountPolicy: FormulaFunctionArgumentCountPolicy.LogicalArguments),
            invocation => EvaluateExternal(invocation, name));

    private static FormulaEvaluationResult EvaluateExternal(FormulaFunctionInvocation invocation, string name)
    {
        if (invocation.Context is not IFormulaExternalFunctionContext external)
        {
            return FormulaEvaluationResult.Failure(FormulaErrorCode.NotAvailable);
        }
        try
        {
            return external.TryEvaluateExternalFunction(name, invocation.FlattenValues(), out var value)
                ? FormulaEvaluationResult.Success(value)
                : FormulaEvaluationResult.Failure(FormulaErrorCode.NotAvailable);
        }
        catch (Exception)
        {
            return FormulaEvaluationResult.Failure(FormulaErrorCode.NotAvailable);
        }
    }
}

public sealed partial class NeraFormulaEngine
{
    private CellValue EvaluateF019Let(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count < 3 || function.Arguments.Count % 2 == 0)
        {
            return CellValue.FromError("#VALUE!");
        }
        var scope = new Dictionary<string, CellValue>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < function.Arguments.Count - 1; index += 2)
        {
            if (function.Arguments[index] is not NameNode name || !IsF019ValidLambdaName(name.Name))
            {
                return CellValue.FromError("#VALUE!");
            }
            scope[name.Name] = EvaluateF019ScopedScalar(function.Arguments[index + 1], scope, context, dependencies);
            if (scope[name.Name].Kind == CellValueKind.Error)
            {
                return scope[name.Name];
            }
        }
        return EvaluateF019ScopedScalar(function.Arguments[^1], scope, context, dependencies);
    }

    private static CellValue EvaluateF019Lambda(FunctionNode function)
    {
        if (!TryParseF019Lambda(function, out _, out _))
        {
            return CellValue.FromError("#VALUE!");
        }
        // A standalone LAMBDA has no invocation value in a scalar cell.
        return CellValue.FromError("#CALC!");
    }

    private static CellValue EvaluateF019IsOmitted(FunctionNode function)
    {
        if (function.Arguments.Count == 0)
        {
            return CellValue.FromBoolean(true);
        }
        if (function.Arguments.Count != 1)
        {
            return CellValue.FromError("#VALUE!");
        }
        return CellValue.FromBoolean(function.Arguments[0] is MissingArgumentNode);
    }

    private CellValue EvaluateF019ScopedScalar(
        FormulaNode node,
        IReadOnlyDictionary<string, CellValue> scope,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        switch (node)
        {
            case NameNode name:
                return scope.TryGetValue(name.Name, out var value)
                    ? value
                    : CellValue.FromError("#NAME?");
            case ConstantNode constant:
                return constant.Value;
            case MissingArgumentNode:
                return CellValue.Blank;
            case CellNode cell:
                return EvaluateCell(cell, context, dependencies);
            case UnaryNode unary:
            {
                var operand = EvaluateF019ScopedScalar(unary.Operand, scope, context, dependencies);
                if (!TryNumber(operand, out var number))
                {
                    return CellValue.FromError("#VALUE!");
                }
                return SafeNumber(unary.Operator == FormulaTokenKind.Minus ? -number : number);
            }
            case BinaryNode binary:
            {
                var left = EvaluateF019ScopedScalar(binary.Left, scope, context, dependencies);
                var right = EvaluateF019ScopedScalar(binary.Right, scope, context, dependencies);
                if (left.Kind == CellValueKind.Error) return left;
                if (right.Kind == CellValueKind.Error) return right;
                if (binary.Operator == FormulaTokenKind.Concat)
                {
                    return CellValue.FromText(left.ToString() + right.ToString());
                }
                if (binary.Operator is FormulaTokenKind.Equal or FormulaTokenKind.NotEqual or FormulaTokenKind.Less or FormulaTokenKind.LessOrEqual or FormulaTokenKind.Greater or FormulaTokenKind.GreaterOrEqual)
                {
                    var comparison = Compare(left, right);
                    return CellValue.FromBoolean(binary.Operator switch
                    {
                        FormulaTokenKind.Equal => comparison == 0,
                        FormulaTokenKind.NotEqual => comparison != 0,
                        FormulaTokenKind.Less => comparison < 0,
                        FormulaTokenKind.LessOrEqual => comparison <= 0,
                        FormulaTokenKind.Greater => comparison > 0,
                        FormulaTokenKind.GreaterOrEqual => comparison >= 0,
                        _ => false,
                    });
                }
                if (!TryNumber(left, out var leftNumber) || !TryNumber(right, out var rightNumber))
                {
                    return CellValue.FromError("#VALUE!");
                }
                if (binary.Operator == FormulaTokenKind.Divide && Math.Abs(rightNumber) <= double.Epsilon)
                {
                    return CellValue.FromError("#DIV/0!");
                }
                return SafeNumber(binary.Operator switch
                {
                    FormulaTokenKind.Plus => leftNumber + rightNumber,
                    FormulaTokenKind.Minus => leftNumber - rightNumber,
                    FormulaTokenKind.Multiply => leftNumber * rightNumber,
                    FormulaTokenKind.Divide => leftNumber / rightNumber,
                    FormulaTokenKind.Power => Math.Pow(leftNumber, rightNumber),
                    _ => double.NaN,
                });
            }
            case FunctionNode function when string.Equals(function.Name, "IF", StringComparison.OrdinalIgnoreCase):
            {
                if (function.Arguments.Count is < 2 or > 3) return CellValue.FromError("#VALUE!");
                var condition = EvaluateF019ScopedScalar(function.Arguments[0], scope, context, dependencies);
                if (!TryBoolean(condition, out var predicate)) return CellValue.FromError("#VALUE!");
                var selected = predicate ? function.Arguments[1] : function.Arguments.Count == 3 ? function.Arguments[2] : new ConstantNode(CellValue.FromBoolean(false));
                return EvaluateF019ScopedScalar(selected, scope, context, dependencies);
            }
            case FunctionNode function when string.Equals(function.Name, "ISOMITTED", StringComparison.OrdinalIgnoreCase):
                return EvaluateF019IsOmitted(function);
            case FunctionNode:
                // Name-free function calls can safely fall through to the ordinary engine.
                if (!ContainsF019Name(node, scope.Keys))
                {
                    return EvaluateNode(node, context, dependencies);
                }
                return CellValue.FromError("#VALUE!");
            default:
                return CellValue.FromError("#VALUE!");
        }
    }

    internal static bool TryParseF019Lambda(
        FormulaNode node,
        out string[] parameters,
        out FormulaNode body)
    {
        if (node is not FunctionNode function ||
            !string.Equals(function.Name, "LAMBDA", StringComparison.OrdinalIgnoreCase) ||
            function.Arguments.Count < 1)
        {
            parameters = [];
            body = null!;
            return false;
        }
        parameters = new string[function.Arguments.Count - 1];
        for (var index = 0; index < parameters.Length; index++)
        {
            if (function.Arguments[index] is not NameNode name || !IsF019ValidLambdaName(name.Name) ||
                parameters.Take(index).Contains(name.Name, StringComparer.OrdinalIgnoreCase))
            {
                parameters = [];
                body = null!;
                return false;
            }
            parameters[index] = name.Name;
        }
        body = function.Arguments[^1];
        return true;
    }

    private static bool IsF019ValidLambdaName(string name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (char.IsLetter(name[0]) || name[0] == '_') &&
        name.All(static character => char.IsLetterOrDigit(character) || character is '_' or '.');

    private static bool ContainsF019Name(FormulaNode node, IEnumerable<string> names)
    {
        var set = names.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return ContainsF019NameCore(node, set);
    }

    private static bool ContainsF019NameCore(FormulaNode node, HashSet<string> names) => node switch
    {
        NameNode name => names.Contains(name.Name),
        UnaryNode unary => ContainsF019NameCore(unary.Operand, names),
        BinaryNode binary => ContainsF019NameCore(binary.Left, names) || ContainsF019NameCore(binary.Right, names),
        FunctionNode function => function.Arguments.Any(argument => ContainsF019NameCore(argument, names)),
        ReferenceUnionNode union => union.Areas.Any(area => ContainsF019NameCore(area, names)),
        _ => false,
    };
}
