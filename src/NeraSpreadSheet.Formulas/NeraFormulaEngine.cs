using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed class NeraFormulaEngine : IFormulaEngine
{
    private readonly IFormulaFunctionRegistry _functions;

    public NeraFormulaEngine(IFormulaFunctionRegistry? functions = null)
    {
        _functions = functions ?? new BuiltInFormulaFunctionRegistry();
    }

    public FormulaEvaluationResult Evaluate(string formula, IFormulaEvaluationContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var node = new FormulaParser(formula).Parse();
            var dependencies = new List<FormulaDependency>();
            var value = EvaluateNode(node, context, dependencies);
            var error = ToErrorCode(value);
            return error == FormulaErrorCode.None
                ? FormulaEvaluationResult.Success(value, dependencies)
                : new FormulaEvaluationResult(value, error, dependencies);
        }
        catch (FormatException)
        {
            return FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);
        }
        catch (OverflowException)
        {
            return FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);
        }
    }

    private CellValue EvaluateNode(FormulaNode node, IFormulaEvaluationContext context, List<FormulaDependency> dependencies)
    {
        return node switch
        {
            ConstantNode constant => constant.Value,
            CellNode cell => EvaluateCell(cell, context, dependencies),
            RangeNode => CellValue.FromError("#VALUE!"),
            UnaryNode unary => EvaluateUnary(unary, context, dependencies),
            BinaryNode binary => EvaluateBinary(binary, context, dependencies),
            FunctionNode function => EvaluateFunction(function, context, dependencies),
            _ => CellValue.FromError("#VALUE!"),
        };
    }

    private static CellValue EvaluateCell(CellNode cell, IFormulaEvaluationContext context, ICollection<FormulaDependency> dependencies)
    {
        dependencies.Add(new FormulaDependency(cell.WorksheetName, new CellRange(cell.Address, cell.Address)));
        return context.GetCellValue(cell.WorksheetName, cell.Address);
    }

    private CellValue EvaluateUnary(UnaryNode unary, IFormulaEvaluationContext context, List<FormulaDependency> dependencies)
    {
        var value = EvaluateNode(unary.Operand, context, dependencies);
        if (!TryNumber(value, out var number))
        {
            return CellValue.FromError("#VALUE!");
        }

        return unary.Operator == FormulaTokenKind.Minus ? SafeNumber(-number) : SafeNumber(number);
    }

    private CellValue EvaluateBinary(BinaryNode binary, IFormulaEvaluationContext context, List<FormulaDependency> dependencies)
    {
        var left = EvaluateNode(binary.Left, context, dependencies);
        var right = EvaluateNode(binary.Right, context, dependencies);
        if (left.Kind == CellValueKind.Error)
        {
            return left;
        }

        if (right.Kind == CellValueKind.Error)
        {
            return right;
        }

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

        var value = binary.Operator switch
        {
            FormulaTokenKind.Plus => leftNumber + rightNumber,
            FormulaTokenKind.Minus => leftNumber - rightNumber,
            FormulaTokenKind.Multiply => leftNumber * rightNumber,
            FormulaTokenKind.Divide => leftNumber / rightNumber,
            FormulaTokenKind.Power => Math.Pow(leftNumber, rightNumber),
            _ => double.NaN,
        };

        return SafeNumber(value);
    }

    private CellValue EvaluateFunction(FunctionNode function, IFormulaEvaluationContext context, List<FormulaDependency> dependencies)
    {
        if (string.Equals(function.Name, "IF", StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateIf(function, context, dependencies);
        }

        if (!_functions.TryResolve(function.Name, out var formulaFunction))
        {
            return CellValue.FromError("#NAME?");
        }

        var values = new List<CellValue>();
        foreach (var argument in function.Arguments)
        {
            if (argument is RangeNode range)
            {
                dependencies.Add(new FormulaDependency(range.WorksheetName, range.Range));
                AppendRange(values, range, context);
                continue;
            }

            values.Add(EvaluateNode(argument, context, dependencies));
        }

        return formulaFunction.Invoke(values, context).Value;
    }

    private CellValue EvaluateIf(FunctionNode function, IFormulaEvaluationContext context, List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 3)
        {
            return CellValue.FromError("#VALUE!");
        }

        var condition = EvaluateNode(function.Arguments[0], context, dependencies);
        if (!TryBoolean(condition, out var isTrue))
        {
            return CellValue.FromError("#VALUE!");
        }

        if (isTrue)
        {
            return EvaluateNode(function.Arguments[1], context, dependencies);
        }

        return function.Arguments.Count == 3
            ? EvaluateNode(function.Arguments[2], context, dependencies)
            : CellValue.FromBoolean(false);
    }

    private static void AppendRange(List<CellValue> values, RangeNode range, IFormulaEvaluationContext context)
    {
        for (var row = range.Range.Top; row <= range.Range.Bottom; row++)
        {
            for (var column = range.Range.Left; column <= range.Range.Right; column++)
            {
                values.Add(context.GetCellValue(range.WorksheetName, new CellAddress(row, column)));
            }
        }
    }

    private static bool TryNumber(CellValue value, out double number)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                number = (double)value.RawValue!;
                return true;
            case CellValueKind.Boolean:
                number = (bool)value.RawValue! ? 1d : 0d;
                return true;
            case CellValueKind.Blank:
                number = 0d;
                return true;
            default:
                number = 0d;
                return false;
        }
    }

    private static bool TryBoolean(CellValue value, out bool result)
    {
        switch (value.Kind)
        {
            case CellValueKind.Boolean:
                result = (bool)value.RawValue!;
                return true;
            case CellValueKind.Number:
                result = Math.Abs((double)value.RawValue!) > double.Epsilon;
                return true;
            case CellValueKind.Blank:
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static int Compare(CellValue left, CellValue right)
    {
        if (TryNumber(left, out var leftNumber) && TryNumber(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static CellValue SafeNumber(double value) => double.IsFinite(value) ? CellValue.FromNumber(value) : CellValue.FromError("#NUM!");

    private static FormulaErrorCode ToErrorCode(CellValue value)
    {
        if (value.Kind != CellValueKind.Error)
        {
            return FormulaErrorCode.None;
        }

        var code = Convert.ToString(value.RawValue, CultureInfo.InvariantCulture);
        return code switch
        {
            "#DIV/0!" => FormulaErrorCode.DivisionByZero,
            "#REF!" => FormulaErrorCode.InvalidReference,
            "#NAME?" => FormulaErrorCode.InvalidName,
            "#CIRC!" => FormulaErrorCode.CircularReference,
            "#N/A" => FormulaErrorCode.NotAvailable,
            _ => FormulaErrorCode.InvalidValue,
        };
    }
}
