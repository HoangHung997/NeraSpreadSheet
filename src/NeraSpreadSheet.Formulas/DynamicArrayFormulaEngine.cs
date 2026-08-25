using System.Globalization;
using System.Text;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed record FormulaArrayEvaluationResult(
    FormulaArrayValue? Value,
    CellValue ErrorValue,
    FormulaErrorCode ErrorCode,
    IReadOnlyList<FormulaDependency> Dependencies)
{
    public bool IsSuccess =>
        Value is not null && ErrorCode == FormulaErrorCode.None;

    public static FormulaArrayEvaluationResult Success(
        FormulaArrayValue value,
        IReadOnlyList<FormulaDependency>? dependencies = null) =>
        new(
            value ?? throw new ArgumentNullException(nameof(value)),
            CellValue.Blank,
            FormulaErrorCode.None,
            dependencies ?? Array.Empty<FormulaDependency>());

    public static FormulaArrayEvaluationResult Failure(
        CellValue errorValue,
        FormulaErrorCode errorCode,
        IReadOnlyList<FormulaDependency>? dependencies = null) =>
        new(
            null,
            errorValue,
            errorCode,
            dependencies ?? Array.Empty<FormulaDependency>());
}

public interface IDynamicArrayFormulaEngine
{
    bool TryEvaluate(
        string formula,
        IFormulaEvaluationContext context,
        out FormulaArrayEvaluationResult result);
}

/// <summary>
/// Evaluates dynamic-array functions while preserving the existing scalar
/// formula engine. The scalar wrapper returns the top-left value so existing
/// dependencies can continue to consume array owners.
/// </summary>
public sealed partial class NeraDynamicArrayFormulaEngine :
    IDynamicArrayFormulaEngine
{
    private readonly IFormulaEngine _scalarEngine;

    public NeraDynamicArrayFormulaEngine(
        IFormulaEngine? scalarEngine = null)
    {
        _scalarEngine = scalarEngine ?? new NeraFormulaEngine();
    }

    public bool TryEvaluate(
        string formula,
        IFormulaEvaluationContext context,
        out FormulaArrayEvaluationResult result)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var effectiveFormula = context is
                IStructuredReferenceEvaluationContext structured
                ? structured.ExpandStructuredReferences(formula)
                : formula;
            var root = new FormulaParser(effectiveFormula).Parse();
            if (root is not FunctionNode function ||
                !IsDynamicFunction(function.Name))
            {
                result = default!;
                return false;
            }

            var dependencies = new List<FormulaDependency>();
            result = EvaluateFunction(function, context, dependencies);
            return true;
        }
        catch (FormatException)
        {
            result = Failure("#VALUE!", FormulaErrorCode.InvalidValue);
            return true;
        }
        catch (OverflowException)
        {
            result = Failure("#NUM!", FormulaErrorCode.InvalidValue);
            return true;
        }
    }

    private FormulaArrayEvaluationResult EvaluateFunction(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (string.Equals(
                function.Name,
                "SEQUENCE",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateSequence(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "TRANSPOSE",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateTranspose(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "FILTER",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateFilter(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "SORT",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateSort(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "UNIQUE",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateUnique(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "CHOOSE",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateChooseArray(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "CHOOSECOLS",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateChooseColumns(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "CHOOSEROWS",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateChooseRows(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "COLUMN",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateColumnArray(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "COLUMNS",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateColumnsArray(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "DROP",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateDrop(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "EXPAND",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateExpand(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "GROUPBY",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateGroupBy(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "HSTACK",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateHStack(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "INDIRECT",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateIndirectArray(function, context, dependencies);
        }
        return Failure("#NAME?", FormulaErrorCode.InvalidName, dependencies);
    }

    private FormulaArrayEvaluationResult EvaluateSequence(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 1 or > 4)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var arguments = new double[4]
        {
            0d,
            1d,
            1d,
            1d,
        };
        for (var index = 0;
             index < function.Arguments.Count;
             index++)
        {
            if (function.Arguments[index] is MissingArgumentNode &&
                index > 0)
            {
                continue;
            }

            var scalar = EvaluateScalarNode(
                function.Arguments[index],
                context,
                dependencies);
            if (scalar.Kind == CellValueKind.Error)
            {
                return Failure(
                    scalar,
                    ToErrorCode(scalar),
                    dependencies);
            }
            if (!FormulaValueCoercion.TryNumber(
                    scalar,
                    out arguments[index]))
            {
                return Failure(
                    "#VALUE!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
            }
        }

        if (!TryPositiveInteger(arguments[0], out var rows) ||
            !TryPositiveInteger(arguments[1], out var columns))
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        var cellCount = checked((long)rows * columns);
        if (cellCount > FormulaArrayValue.MaximumCellCount)
        {
            return Failure(
                "#NUM!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var start = arguments[2];
        var step = arguments[3];
        if (!double.IsFinite(start) || !double.IsFinite(step))
        {
            return Failure(
                "#NUM!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var values = new CellValue[checked((int)cellCount)];
        for (var index = 0; index < values.Length; index++)
        {
            var value = start + (index * step);
            if (!double.IsFinite(value))
            {
                return Failure(
                    "#NUM!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
            }
            values[index] = CellValue.FromNumber(value);
        }
        return FormulaArrayEvaluationResult.Success(
            new FormulaArrayValue(rows, columns, values),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateTranspose(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 1)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        var source = EvaluateNodeAsArray(
            function.Arguments[0],
            context,
            dependencies);
        return source.IsSuccess
            ? FormulaArrayEvaluationResult.Success(
                source.Value!.Transpose(),
                DistinctDependencies(dependencies))
            : source;
    }

    private FormulaArrayEvaluationResult EvaluateNodeAsArray(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        switch (node)
        {
            case MissingArgumentNode:
                return FormulaArrayEvaluationResult.Success(
                    new FormulaArrayValue(1, 1, [CellValue.Blank]),
                    DistinctDependencies(dependencies));
            case RangeNode range:
                dependencies.Add(new FormulaDependency(
                    range.WorksheetName,
                    range.Range));
                return FormulaArrayEvaluationResult.Success(
                    FormulaArrayValue.Create(
                        range.Range.RowCount,
                        range.Range.ColumnCount,
                        (row, column) => context.GetCellValue(
                            range.WorksheetName,
                            new CellAddress(
                                range.Range.Top + row,
                                range.Range.Left + column))),
                    DistinctDependencies(dependencies));
            case CellNode cell:
                dependencies.Add(new FormulaDependency(
                    cell.WorksheetName,
                    new CellRange(cell.Address, cell.Address)));
                return FormulaArrayEvaluationResult.Success(
                    new FormulaArrayValue(
                        1,
                        1,
                        [context.GetCellValue(
                            cell.WorksheetName,
                            cell.Address)]),
                    DistinctDependencies(dependencies));
            case ReferenceUnionNode:
                return Failure(
                    "#VALUE!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
            case FunctionNode function
                when IsDynamicFunction(function.Name):
                return EvaluateFunction(function, context, dependencies);
            default:
                var scalar = EvaluateScalarNode(node, context, dependencies);
                return scalar.Kind == CellValueKind.Error
                    ? FormulaArrayEvaluationResult.Failure(
                        scalar,
                        ToErrorCode(scalar),
                        DistinctDependencies(dependencies))
                    : FormulaArrayEvaluationResult.Success(
                        new FormulaArrayValue(1, 1, [scalar]),
                        DistinctDependencies(dependencies));
        }
    }

    private CellValue EvaluateScalarNode(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (node is MissingArgumentNode)
        {
            return CellValue.Blank;
        }

        var result = _scalarEngine.Evaluate(
            FormulaNodeWriter.Write(node),
            context);
        dependencies.AddRange(result.Dependencies);
        return result.Value;
    }

    private static bool IsDynamicFunction(string name) =>
        string.Equals(
            name,
            "SEQUENCE",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "TRANSPOSE",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "FILTER",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "SORT",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "UNIQUE",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "CHOOSE",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "CHOOSECOLS",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "CHOOSEROWS",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "COLUMN",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "COLUMNS",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "DROP",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "EXPAND",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "GROUPBY",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "HSTACK",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            name,
            "INDIRECT",
            StringComparison.OrdinalIgnoreCase);

    private static bool TryPositiveInteger(
        double value,
        out int result)
    {
        if (!double.IsFinite(value) ||
            value < 1d ||
            value > int.MaxValue)
        {
            result = default;
            return false;
        }
        var rounded = Math.Round(value);
        if (Math.Abs(value - rounded) > double.Epsilon)
        {
            result = default;
            return false;
        }
        result = checked((int)rounded);
        return true;
    }

    private static FormulaDependency[] DistinctDependencies(
        IEnumerable<FormulaDependency> dependencies) =>
        dependencies.Distinct().ToArray();

    private static FormulaArrayEvaluationResult Failure(
        string error,
        FormulaErrorCode code,
        IReadOnlyList<FormulaDependency>? dependencies = null) =>
        FormulaArrayEvaluationResult.Failure(
            CellValue.FromError(error),
            code,
            dependencies);

    private static FormulaArrayEvaluationResult Failure(
        CellValue error,
        FormulaErrorCode code,
        IReadOnlyList<FormulaDependency>? dependencies = null) =>
        FormulaArrayEvaluationResult.Failure(error, code, dependencies);

    private static FormulaErrorCode ToErrorCode(CellValue value)
    {
        if (value.Kind != CellValueKind.Error)
        {
            return FormulaErrorCode.None;
        }
        return Convert.ToString(
            value.RawValue,
            CultureInfo.InvariantCulture) switch
        {
            "#DIV/0!" => FormulaErrorCode.DivisionByZero,
            "#REF!" => FormulaErrorCode.InvalidReference,
            "#NAME?" => FormulaErrorCode.InvalidName,
            "#CIRC!" => FormulaErrorCode.CircularReference,
            "#N/A" => FormulaErrorCode.NotAvailable,
            "#SPILL!" => FormulaErrorCode.Spill,
            _ => FormulaErrorCode.InvalidValue,
        };
    }

    private static class FormulaNodeWriter
    {
        public static string Write(FormulaNode node)
        {
            var builder = new StringBuilder("=");
            Append(builder, node);
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, FormulaNode node)
        {
            switch (node)
            {
                case ConstantNode constant:
                    AppendConstant(builder, constant.Value);
                    break;
                case MissingArgumentNode:
                    break;
                case NameNode name:
                    builder.Append(name.Name);
                    break;
                case CellNode cell:
                    AppendWorksheet(builder, cell.WorksheetName);
                    builder.Append(cell.Address.ToA1());
                    break;
                case RangeNode range:
                    AppendWorksheet(builder, range.WorksheetName);
                    builder.Append(range.Range.TopLeft.ToA1());
                    builder.Append(':');
                    builder.Append(range.Range.BottomRight.ToA1());
                    break;
                case ReferenceUnionNode union:
                    builder.Append('(');
                    for (var index = 0;
                         index < union.Areas.Count;
                         index++)
                    {
                        if (index > 0)
                        {
                            builder.Append(',');
                        }
                        Append(builder, union.Areas[index]);
                    }
                    builder.Append(')');
                    break;
                case UnaryNode unary:
                    builder.Append(unary.Operator == FormulaTokenKind.Minus
                        ? '-'
                        : '+');
                    builder.Append('(');
                    Append(builder, unary.Operand);
                    builder.Append(')');
                    break;
                case BinaryNode binary:
                    builder.Append('(');
                    Append(builder, binary.Left);
                    builder.Append(GetOperator(binary.Operator));
                    Append(builder, binary.Right);
                    builder.Append(')');
                    break;
                case FunctionNode function:
                    builder.Append(function.Name);
                    builder.Append('(');
                    for (var index = 0;
                         index < function.Arguments.Count;
                         index++)
                    {
                        if (index > 0)
                        {
                            builder.Append(',');
                        }
                        Append(builder, function.Arguments[index]);
                    }
                    builder.Append(')');
                    break;
                default:
                    throw new InvalidOperationException(
                        "The formula node cannot be serialized.");
            }
        }

        private static void AppendConstant(
            StringBuilder builder,
            CellValue value)
        {
            switch (value.Kind)
            {
                case CellValueKind.Blank:
                    builder.Append('0');
                    break;
                case CellValueKind.Number:
                    builder.Append(((double)value.RawValue!).ToString(
                        "R",
                        CultureInfo.InvariantCulture));
                    break;
                case CellValueKind.Text:
                    builder.Append('"');
                    builder.Append(((string)value.RawValue!).Replace(
                        "\"",
                        "\"\"",
                        StringComparison.Ordinal));
                    builder.Append('"');
                    break;
                case CellValueKind.Boolean:
                    builder.Append((bool)value.RawValue!
                        ? "TRUE()"
                        : "FALSE()");
                    break;
                case CellValueKind.DateTime:
                    builder.Append(((DateTime)value.RawValue!).ToOADate()
                        .ToString("R", CultureInfo.InvariantCulture));
                    break;
                case CellValueKind.Error:
                    builder.Append(Convert.ToString(
                        value.RawValue,
                        CultureInfo.InvariantCulture));
                    break;
                default:
                    throw new InvalidOperationException(
                        "The formula constant kind is not supported.");
            }
        }

        private static void AppendWorksheet(
            StringBuilder builder,
            string? worksheetName)
        {
            if (worksheetName is null)
            {
                return;
            }
            builder.Append('\'');
            builder.Append(worksheetName.Replace(
                "'",
                "''",
                StringComparison.Ordinal));
            builder.Append("'!");
        }

        private static string GetOperator(FormulaTokenKind kind) =>
            kind switch
            {
                FormulaTokenKind.Plus => "+",
                FormulaTokenKind.Minus => "-",
                FormulaTokenKind.Multiply => "*",
                FormulaTokenKind.Divide => "/",
                FormulaTokenKind.Power => "^",
                FormulaTokenKind.Concat => "&",
                FormulaTokenKind.Equal => "=",
                FormulaTokenKind.NotEqual => "<>",
                FormulaTokenKind.Less => "<",
                FormulaTokenKind.LessOrEqual => "<=",
                FormulaTokenKind.Greater => ">",
                FormulaTokenKind.GreaterOrEqual => ">=",
                _ => throw new InvalidOperationException(
                    "The formula operator cannot be serialized."),
            };
    }
}

/// <summary>
/// Scalar compatibility wrapper for dynamic-array formulas. Existing scalar
/// consumers receive the array's top-left value while dependencies are kept.
/// </summary>
public sealed class DynamicArrayAwareFormulaEngine : IFormulaEngine
{
    private readonly IFormulaEngine _scalarEngine;
    private readonly IDynamicArrayFormulaEngine _arrayEngine;

    public DynamicArrayAwareFormulaEngine(
        IFormulaEngine? scalarEngine = null,
        IDynamicArrayFormulaEngine? arrayEngine = null)
    {
        _scalarEngine = scalarEngine ?? new NeraFormulaEngine();
        _arrayEngine = arrayEngine ??
            new NeraDynamicArrayFormulaEngine(_scalarEngine);
    }

    public FormulaEvaluationResult Evaluate(
        string formula,
        IFormulaEvaluationContext context)
    {
        if (!_arrayEngine.TryEvaluate(formula, context, out var arrayResult))
        {
            return _scalarEngine.Evaluate(formula, context);
        }
        return arrayResult.IsSuccess
            ? FormulaEvaluationResult.Success(
                arrayResult.Value![0, 0],
                arrayResult.Dependencies)
            : new FormulaEvaluationResult(
                arrayResult.ErrorValue,
                arrayResult.ErrorCode,
                arrayResult.Dependencies);
    }
}
