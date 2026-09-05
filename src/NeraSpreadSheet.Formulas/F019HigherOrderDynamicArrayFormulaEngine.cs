using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraDynamicArrayFormulaEngine
{
    private FormulaArrayEvaluationResult EvaluateF019GroupCDynamic(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        return FormulaFunctionName.Normalize(function.Name) switch
        {
            "BYCOL" => EvaluateF019ByColumn(function, context, dependencies),
            "BYROW" => EvaluateF019ByRow(function, context, dependencies),
            "MAKEARRAY" => EvaluateF019MakeArray(function, context, dependencies),
            "MAP" => EvaluateF019Map(function, context, dependencies),
            "REDUCE" => EvaluateF019Reduce(function, context, dependencies, scan: false),
            "SCAN" => EvaluateF019Reduce(function, context, dependencies, scan: true),
            _ => Failure("#NAME?", FormulaErrorCode.InvalidName, dependencies),
        };
    }

    private FormulaArrayEvaluationResult EvaluateF019ByColumn(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 2 ||
            !NeraFormulaEngine.TryParseF019Lambda(function.Arguments[1], out var parameters, out var body) ||
            parameters.Length != 1)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var sourceResult = EvaluateNodeAsArray(function.Arguments[0], context, dependencies);
        if (!sourceResult.IsSuccess) return sourceResult;
        var source = sourceResult.Value!;
        var output = new CellValue[source.ColumnCount];
        for (var column = 0; column < source.ColumnCount; column++)
        {
            var slice = FormulaArrayValue.Create(source.RowCount, 1, (row, _) => source[row, column]);
            var value = EvaluateF019LambdaArray(body, new Dictionary<string, FormulaArrayValue>(StringComparer.OrdinalIgnoreCase)
            {
                [parameters[0]] = slice,
            }, context, dependencies);
            if (!value.IsSuccess || value.Value!.Count != 1)
            {
                return value.IsSuccess ? Failure("#CALC!", FormulaErrorCode.InvalidValue, dependencies) : value;
            }
            output[column] = value.Value[0, 0];
        }
        return FormulaArrayEvaluationResult.Success(new FormulaArrayValue(1, source.ColumnCount, output), DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateF019ByRow(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 2 ||
            !NeraFormulaEngine.TryParseF019Lambda(function.Arguments[1], out var parameters, out var body) ||
            parameters.Length != 1)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var sourceResult = EvaluateNodeAsArray(function.Arguments[0], context, dependencies);
        if (!sourceResult.IsSuccess) return sourceResult;
        var source = sourceResult.Value!;
        var output = new CellValue[source.RowCount];
        for (var row = 0; row < source.RowCount; row++)
        {
            var slice = FormulaArrayValue.Create(1, source.ColumnCount, (_, column) => source[row, column]);
            var value = EvaluateF019LambdaArray(body, new Dictionary<string, FormulaArrayValue>(StringComparer.OrdinalIgnoreCase)
            {
                [parameters[0]] = slice,
            }, context, dependencies);
            if (!value.IsSuccess || value.Value!.Count != 1)
            {
                return value.IsSuccess ? Failure("#CALC!", FormulaErrorCode.InvalidValue, dependencies) : value;
            }
            output[row] = value.Value[0, 0];
        }
        return FormulaArrayEvaluationResult.Success(new FormulaArrayValue(source.RowCount, 1, output), DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateF019MakeArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 3 ||
            !TryF019Dimension(function.Arguments[0], context, dependencies, out var rows) ||
            !TryF019Dimension(function.Arguments[1], context, dependencies, out var columns) ||
            (long)rows * columns > FormulaArrayValue.MaximumCellCount ||
            !NeraFormulaEngine.TryParseF019Lambda(function.Arguments[2], out var parameters, out var body) ||
            parameters.Length != 2)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var values = new CellValue[checked(rows * columns)];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var scope = new Dictionary<string, FormulaArrayValue>(StringComparer.OrdinalIgnoreCase)
                {
                    [parameters[0]] = ScalarArray(CellValue.FromNumber(row + 1d)),
                    [parameters[1]] = ScalarArray(CellValue.FromNumber(column + 1d)),
                };
                var result = EvaluateF019LambdaArray(body, scope, context, dependencies);
                if (!result.IsSuccess || result.Value!.Count != 1)
                {
                    return result.IsSuccess ? Failure("#CALC!", FormulaErrorCode.InvalidValue, dependencies) : result;
                }
                values[(row * columns) + column] = result.Value[0, 0];
            }
        }
        return FormulaArrayEvaluationResult.Success(new FormulaArrayValue(rows, columns, values), DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateF019Map(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count < 2 ||
            !NeraFormulaEngine.TryParseF019Lambda(function.Arguments[^1], out var parameters, out var body) ||
            parameters.Length != function.Arguments.Count - 1)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var inputs = new List<FormulaArrayValue>();
        for (var index = 0; index < function.Arguments.Count - 1; index++)
        {
            var result = EvaluateNodeAsArray(function.Arguments[index], context, dependencies);
            if (!result.IsSuccess) return result;
            inputs.Add(result.Value!);
        }
        var rows = inputs.Max(static value => value.RowCount);
        var columns = inputs.Max(static value => value.ColumnCount);
        foreach (var input in inputs)
        {
            if (!((input.RowCount == rows || input.RowCount == 1) && (input.ColumnCount == columns || input.ColumnCount == 1)))
            {
                return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
            }
        }
        var output = new CellValue[checked(rows * columns)];
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var scope = new Dictionary<string, FormulaArrayValue>(StringComparer.OrdinalIgnoreCase);
                for (var inputIndex = 0; inputIndex < inputs.Count; inputIndex++)
                {
                    var input = inputs[inputIndex];
                    scope[parameters[inputIndex]] = ScalarArray(input[input.RowCount == 1 ? 0 : row, input.ColumnCount == 1 ? 0 : column]);
                }
                var result = EvaluateF019LambdaArray(body, scope, context, dependencies);
                if (!result.IsSuccess || result.Value!.Count != 1)
                {
                    return result.IsSuccess ? Failure("#CALC!", FormulaErrorCode.InvalidValue, dependencies) : result;
                }
                output[(row * columns) + column] = result.Value[0, 0];
            }
        }
        return FormulaArrayEvaluationResult.Success(new FormulaArrayValue(rows, columns, output), DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateF019Reduce(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        bool scan)
    {
        if (function.Arguments.Count != 3 ||
            !NeraFormulaEngine.TryParseF019Lambda(function.Arguments[2], out var parameters, out var body) ||
            parameters.Length != 2)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var initial = EvaluateNodeAsArray(function.Arguments[0], context, dependencies);
        var sourceResult = EvaluateNodeAsArray(function.Arguments[1], context, dependencies);
        if (!initial.IsSuccess) return initial;
        if (!sourceResult.IsSuccess) return sourceResult;
        if (initial.Value!.Count != 1)
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        var accumulator = initial.Value;
        var source = sourceResult.Value!;
        var output = scan ? new CellValue[source.Count] : [];
        var position = 0;
        foreach (var cell in source.ToArray())
        {
            var scope = new Dictionary<string, FormulaArrayValue>(StringComparer.OrdinalIgnoreCase)
            {
                [parameters[0]] = accumulator,
                [parameters[1]] = ScalarArray(cell),
            };
            var result = EvaluateF019LambdaArray(body, scope, context, dependencies);
            if (!result.IsSuccess || result.Value!.Count != 1)
            {
                return result.IsSuccess ? Failure("#CALC!", FormulaErrorCode.InvalidValue, dependencies) : result;
            }
            accumulator = result.Value;
            if (scan) output[position++] = accumulator[0, 0];
        }
        return FormulaArrayEvaluationResult.Success(
            scan ? new FormulaArrayValue(source.RowCount, source.ColumnCount, output) : accumulator,
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateF019LambdaArray(
        FormulaNode node,
        IReadOnlyDictionary<string, FormulaArrayValue> scope,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        switch (node)
        {
            case NameNode name:
                return scope.TryGetValue(name.Name, out var scoped)
                    ? FormulaArrayEvaluationResult.Success(scoped, DistinctDependencies(dependencies))
                    : Failure("#NAME?", FormulaErrorCode.InvalidName, dependencies);
            case ConstantNode constant:
                return FormulaArrayEvaluationResult.Success(ScalarArray(constant.Value), DistinctDependencies(dependencies));
            case MissingArgumentNode:
                return FormulaArrayEvaluationResult.Success(ScalarArray(CellValue.Blank), DistinctDependencies(dependencies));
            case CellNode or RangeNode:
                return EvaluateNodeAsArray(node, context, dependencies);
            case UnaryNode unary:
            {
                var operand = EvaluateF019LambdaArray(unary.Operand, scope, context, dependencies);
                if (!operand.IsSuccess) return operand;
                return ApplyF019Unary(operand.Value!, unary.Operator, dependencies);
            }
            case BinaryNode binary:
            {
                var left = EvaluateF019LambdaArray(binary.Left, scope, context, dependencies);
                if (!left.IsSuccess) return left;
                var right = EvaluateF019LambdaArray(binary.Right, scope, context, dependencies);
                if (!right.IsSuccess) return right;
                return ApplyF019Binary(left.Value!, right.Value!, binary.Operator, dependencies);
            }
            case FunctionNode function when string.Equals(function.Name, "SUM", StringComparison.OrdinalIgnoreCase):
            {
                var sum = 0d;
                foreach (var argument in function.Arguments)
                {
                    var value = EvaluateF019LambdaArray(argument, scope, context, dependencies);
                    if (!value.IsSuccess) return value;
                    foreach (var cell in value.Value!.ToArray())
                    {
                        if (F019StatisticsMatrixAndExternalFormulaFunctions.TryRangeNumber(cell, out var number)) sum += number;
                    }
                }
                return FormulaArrayEvaluationResult.Success(ScalarArray(CellValue.FromNumber(sum)), DistinctDependencies(dependencies));
            }
            case FunctionNode function when string.Equals(function.Name, "ISOMITTED", StringComparison.OrdinalIgnoreCase):
                return FormulaArrayEvaluationResult.Success(ScalarArray(CellValue.FromBoolean(false)), DistinctDependencies(dependencies));
            default:
                return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
    }

    private static FormulaArrayEvaluationResult ApplyF019Unary(FormulaArrayValue source, FormulaTokenKind operation, IReadOnlyList<FormulaDependency> dependencies)
    {
        var values = new CellValue[source.Count];
        var index = 0;
        foreach (var cell in source.ToArray())
        {
            if (!FormulaValueCoercion.TryNumber(cell, out var number, allowText: true))
            {
                return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
            }
            values[index++] = CellValue.FromNumber(operation == FormulaTokenKind.Minus ? -number : number);
        }
        return FormulaArrayEvaluationResult.Success(new FormulaArrayValue(source.RowCount, source.ColumnCount, values), dependencies);
    }

    private static FormulaArrayEvaluationResult ApplyF019Binary(FormulaArrayValue left, FormulaArrayValue right, FormulaTokenKind operation, IReadOnlyList<FormulaDependency> dependencies)
    {
        var rows = Math.Max(left.RowCount, right.RowCount);
        var columns = Math.Max(left.ColumnCount, right.ColumnCount);
        if (!CanBroadcastF019(left, rows, columns) || !CanBroadcastF019(right, rows, columns))
        {
            return Failure("#VALUE!", FormulaErrorCode.InvalidValue, dependencies);
        }
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(rows, columns, (row, column) =>
            {
                var l = left[left.RowCount == 1 ? 0 : row, left.ColumnCount == 1 ? 0 : column];
                var r = right[right.RowCount == 1 ? 0 : row, right.ColumnCount == 1 ? 0 : column];
                if (operation == FormulaTokenKind.Concat) return CellValue.FromText(l.ToString() + r.ToString());
                if (!FormulaValueCoercion.TryNumber(l, out var a, allowText: true) || !FormulaValueCoercion.TryNumber(r, out var b, allowText: true)) return CellValue.FromError("#VALUE!");
                if (operation == FormulaTokenKind.Divide && Math.Abs(b) <= double.Epsilon) return CellValue.FromError("#DIV/0!");
                var value = operation switch
                {
                    FormulaTokenKind.Plus => a + b,
                    FormulaTokenKind.Minus => a - b,
                    FormulaTokenKind.Multiply => a * b,
                    FormulaTokenKind.Divide => a / b,
                    FormulaTokenKind.Power => Math.Pow(a, b),
                    FormulaTokenKind.Equal => a == b ? 1d : 0d,
                    FormulaTokenKind.NotEqual => a != b ? 1d : 0d,
                    FormulaTokenKind.Less => a < b ? 1d : 0d,
                    FormulaTokenKind.LessOrEqual => a <= b ? 1d : 0d,
                    FormulaTokenKind.Greater => a > b ? 1d : 0d,
                    FormulaTokenKind.GreaterOrEqual => a >= b ? 1d : 0d,
                    _ => double.NaN,
                };
                return double.IsFinite(value) ? CellValue.FromNumber(value) : CellValue.FromError("#NUM!");
            }),
            dependencies);
    }

    private bool TryF019Dimension(FormulaNode node, IFormulaEvaluationContext context, List<FormulaDependency> dependencies, out int value)
    {
        var cell = EvaluateScalarNode(node, context, dependencies);
        if (!FormulaValueCoercion.TryNumber(cell, out var number, allowText: true) || !double.IsFinite(number) || number < 1d || number > int.MaxValue)
        {
            value = default;
            return false;
        }
        value = checked((int)Math.Truncate(number));
        return value > 0;
    }

    private static bool CanBroadcastF019(FormulaArrayValue value, int rows, int columns) =>
        (value.RowCount == 1 || value.RowCount == rows) &&
        (value.ColumnCount == 1 || value.ColumnCount == columns);

    private static FormulaArrayValue ScalarArray(CellValue value) => new(1, 1, [value]);
}
