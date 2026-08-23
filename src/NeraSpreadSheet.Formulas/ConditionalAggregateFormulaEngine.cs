using System.Globalization;
using System.Text;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Adds range-shape-aware conditional aggregates without changing the legacy
/// IFormulaFunction argument-flattening contract. Ordinary scalar evaluation
/// is delegated to the existing engine after conditional subexpressions have
/// been reduced to constants. Lazy control functions keep branch laziness.
/// </summary>
public sealed class ConditionalAggregateFormulaEngine : IFormulaEngine
{
    private readonly IFormulaEngine _fallback;

    public ConditionalAggregateFormulaEngine(
        IFormulaEngine? fallback = null)
    {
        _fallback = fallback ?? new NeraFormulaEngine();
    }

    public FormulaEvaluationResult Evaluate(
        string formula,
        IFormulaEvaluationContext context)
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
            if (!ContainsConditionalAggregate(root))
            {
                return _fallback.Evaluate(formula, context);
            }

            var dependencies = new List<FormulaDependency>();
            var value = EvaluateNode(root, context, dependencies);
            return CreateResult(value, dependencies);
        }
        catch (FormatException)
        {
            return FormulaEvaluationResult.Failure(
                FormulaErrorCode.InvalidValue);
        }
        catch (OverflowException)
        {
            return new FormulaEvaluationResult(
                CellValue.FromError("#NUM!"),
                FormulaErrorCode.InvalidValue,
                Array.Empty<FormulaDependency>());
        }
    }

    private CellValue EvaluateNode(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (node is FunctionNode function)
        {
            if (IsConditionalAggregate(function.Name))
            {
                return EvaluateConditionalAggregate(
                    function,
                    context,
                    dependencies);
            }
            if (IsLazyControl(function.Name) &&
                ContainsConditionalAggregate(function))
            {
                return EvaluateLazyControl(
                    function,
                    context,
                    dependencies);
            }
        }
        return EvaluateWithFallback(node, context, dependencies);
    }

    private CellValue EvaluateWithFallback(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        var formula = ConditionalFormulaWriter.Write(
            node,
            candidate =>
            {
                if (candidate is not FunctionNode function ||
                    (!IsConditionalAggregate(function.Name) &&
                     !(IsLazyControl(function.Name) &&
                       ContainsConditionalAggregate(function))))
                {
                    return null;
                }
                return EvaluateNode(candidate, context, dependencies);
            });
        var result = _fallback.Evaluate(formula, context);
        dependencies.AddRange(result.Dependencies);
        return result.Value;
    }

    private CellValue EvaluateLazyControl(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (NameEquals(function, "IF"))
        {
            if (function.Arguments.Count is < 2 or > 3)
            {
                return CellValue.FromError("#VALUE!");
            }
            var condition = EvaluateNode(
                function.Arguments[0],
                context,
                dependencies);
            if (condition.Kind == CellValueKind.Error)
            {
                return condition;
            }
            if (!FormulaValueCoercion.TryBoolean(condition, out var selected))
            {
                return CellValue.FromError("#VALUE!");
            }
            if (selected)
            {
                return EvaluateNode(
                    function.Arguments[1],
                    context,
                    dependencies);
            }
            return function.Arguments.Count == 3
                ? EvaluateNode(
                    function.Arguments[2],
                    context,
                    dependencies)
                : CellValue.Blank;
        }
        if (NameEquals(function, "IFERROR") ||
            NameEquals(function, "IFNA"))
        {
            if (function.Arguments.Count != 2)
            {
                return CellValue.FromError("#VALUE!");
            }
            var value = EvaluateNode(
                function.Arguments[0],
                context,
                dependencies);
            var useFallback = value.Kind == CellValueKind.Error &&
                (NameEquals(function, "IFERROR") ||
                 IsError(value, "#N/A"));
            return useFallback
                ? EvaluateNode(
                    function.Arguments[1],
                    context,
                    dependencies)
                : value;
        }
        if (NameEquals(function, "IFS"))
        {
            if (function.Arguments.Count < 2 ||
                (function.Arguments.Count & 1) != 0)
            {
                return CellValue.FromError("#VALUE!");
            }
            for (var index = 0;
                 index < function.Arguments.Count;
                 index += 2)
            {
                var condition = EvaluateNode(
                    function.Arguments[index],
                    context,
                    dependencies);
                if (condition.Kind == CellValueKind.Error)
                {
                    return condition;
                }
                if (!FormulaValueCoercion.TryBoolean(
                        condition,
                        out var selected))
                {
                    return CellValue.FromError("#VALUE!");
                }
                if (selected)
                {
                    return EvaluateNode(
                        function.Arguments[index + 1],
                        context,
                        dependencies);
                }
            }
            return CellValue.FromError("#N/A");
        }
        if (NameEquals(function, "CHOOSE"))
        {
            if (function.Arguments.Count < 2)
            {
                return CellValue.FromError("#VALUE!");
            }
            var indexValue = EvaluateNode(
                function.Arguments[0],
                context,
                dependencies);
            if (!TryPositiveInteger(indexValue, out var index) ||
                index >= function.Arguments.Count)
            {
                return CellValue.FromError("#VALUE!");
            }
            return EvaluateNode(
                function.Arguments[index],
                context,
                dependencies);
        }
        if (NameEquals(function, "SWITCH"))
        {
            if (function.Arguments.Count < 3)
            {
                return CellValue.FromError("#VALUE!");
            }
            var expression = EvaluateNode(
                function.Arguments[0],
                context,
                dependencies);
            if (expression.Kind == CellValueKind.Error)
            {
                return expression;
            }
            var hasDefault = (function.Arguments.Count & 1) == 0;
            var pairEnd = hasDefault
                ? function.Arguments.Count - 1
                : function.Arguments.Count;
            for (var index = 1; index < pairEnd; index += 2)
            {
                var candidate = EvaluateNode(
                    function.Arguments[index],
                    context,
                    dependencies);
                if (candidate.Kind == CellValueKind.Error)
                {
                    return candidate;
                }
                if (ValuesEqual(expression, candidate))
                {
                    return EvaluateNode(
                        function.Arguments[index + 1],
                        context,
                        dependencies);
                }
            }
            return hasDefault
                ? EvaluateNode(
                    function.Arguments[^1],
                    context,
                    dependencies)
                : CellValue.FromError("#N/A");
        }
        return EvaluateWithFallback(function, context, dependencies);
    }

    private CellValue EvaluateConditionalAggregate(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (NameEquals(function, "COUNTIF"))
        {
            return EvaluateCountIf(function, context, dependencies);
        }
        if (NameEquals(function, "COUNTIFS"))
        {
            return EvaluateCountIfs(function, context, dependencies);
        }
        if (NameEquals(function, "SUMIF"))
        {
            return EvaluateSingleRangeAggregate(
                function,
                context,
                dependencies,
                ConditionalAggregateKind.Sum);
        }
        if (NameEquals(function, "AVERAGEIF"))
        {
            return EvaluateSingleRangeAggregate(
                function,
                context,
                dependencies,
                ConditionalAggregateKind.Average);
        }
        if (NameEquals(function, "SUMIFS"))
        {
            return EvaluateMultipleRangeAggregate(
                function,
                context,
                dependencies,
                ConditionalAggregateKind.Sum);
        }
        if (NameEquals(function, "AVERAGEIFS"))
        {
            return EvaluateMultipleRangeAggregate(
                function,
                context,
                dependencies,
                ConditionalAggregateKind.Average);
        }
        return CellValue.FromError("#NAME?");
    }

    private CellValue EvaluateCountIf(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 2 ||
            !TryReadReference(
                function.Arguments[0],
                context,
                dependencies,
                out var range))
        {
            return CellValue.FromError("#VALUE!");
        }
        var criterionValue = EvaluateNode(
            function.Arguments[1],
            context,
            dependencies);
        if (criterionValue.Kind == CellValueKind.Error)
        {
            return criterionValue;
        }
        var criterion = FormulaCriterion.Parse(criterionValue);
        return CellValue.FromNumber(
            range.Values.Count(criterion.Matches));
    }

    private CellValue EvaluateCountIfs(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count < 2 ||
            (function.Arguments.Count & 1) != 0)
        {
            return CellValue.FromError("#VALUE!");
        }
        var criteria = new List<CriteriaRange>();
        ReferenceValues? shape = null;
        for (var index = 0;
             index < function.Arguments.Count;
             index += 2)
        {
            if (!TryReadReference(
                    function.Arguments[index],
                    context,
                    dependencies,
                    out var range))
            {
                return CellValue.FromError("#VALUE!");
            }
            shape ??= range;
            if (!HasSameShape(shape, range))
            {
                return CellValue.FromError("#VALUE!");
            }
            var criterionValue = EvaluateNode(
                function.Arguments[index + 1],
                context,
                dependencies);
            if (criterionValue.Kind == CellValueKind.Error)
            {
                return criterionValue;
            }
            criteria.Add(new CriteriaRange(
                range,
                FormulaCriterion.Parse(criterionValue)));
        }

        var count = 0;
        for (var offset = 0; offset < shape!.Values.Length; offset++)
        {
            if (criteria.All(item =>
                    item.Criterion.Matches(item.Range.Values[offset])))
            {
                count++;
            }
        }
        return CellValue.FromNumber(count);
    }

    private CellValue EvaluateSingleRangeAggregate(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        ConditionalAggregateKind aggregateKind)
    {
        if (function.Arguments.Count is < 2 or > 3 ||
            !TryReadReference(
                function.Arguments[0],
                context,
                dependencies,
                out var criteriaRange))
        {
            return CellValue.FromError("#VALUE!");
        }
        var criterionValue = EvaluateNode(
            function.Arguments[1],
            context,
            dependencies);
        if (criterionValue.Kind == CellValueKind.Error)
        {
            return criterionValue;
        }
        ReferenceValues aggregateRange;
        if (function.Arguments.Count == 2)
        {
            aggregateRange = criteriaRange;
        }
        else if (!TryReadReference(
                     function.Arguments[2],
                     context,
                     dependencies,
                     out aggregateRange) ||
                 !HasSameShape(criteriaRange, aggregateRange))
        {
            return CellValue.FromError("#VALUE!");
        }

        return AggregateMatches(
            aggregateRange,
            [new CriteriaRange(
                criteriaRange,
                FormulaCriterion.Parse(criterionValue))],
            aggregateKind);
    }

    private CellValue EvaluateMultipleRangeAggregate(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        ConditionalAggregateKind aggregateKind)
    {
        if (function.Arguments.Count < 3 ||
            (function.Arguments.Count & 1) == 0 ||
            !TryReadReference(
                function.Arguments[0],
                context,
                dependencies,
                out var aggregateRange))
        {
            return CellValue.FromError("#VALUE!");
        }
        var criteria = new List<CriteriaRange>();
        for (var index = 1;
             index < function.Arguments.Count;
             index += 2)
        {
            if (!TryReadReference(
                    function.Arguments[index],
                    context,
                    dependencies,
                    out var criteriaRange) ||
                !HasSameShape(aggregateRange, criteriaRange))
            {
                return CellValue.FromError("#VALUE!");
            }
            var criterionValue = EvaluateNode(
                function.Arguments[index + 1],
                context,
                dependencies);
            if (criterionValue.Kind == CellValueKind.Error)
            {
                return criterionValue;
            }
            criteria.Add(new CriteriaRange(
                criteriaRange,
                FormulaCriterion.Parse(criterionValue)));
        }
        return AggregateMatches(aggregateRange, criteria, aggregateKind);
    }

    private static CellValue AggregateMatches(
        ReferenceValues aggregateRange,
        IReadOnlyList<CriteriaRange> criteria,
        ConditionalAggregateKind aggregateKind)
    {
        var total = 0d;
        var numericCount = 0;
        for (var offset = 0;
             offset < aggregateRange.Values.Length;
             offset++)
        {
            if (!criteria.All(item =>
                    item.Criterion.Matches(item.Range.Values[offset])))
            {
                continue;
            }
            var value = aggregateRange.Values[offset];
            if (value.Kind == CellValueKind.Error)
            {
                return value;
            }
            if (!TryAggregateNumber(value, out var number))
            {
                continue;
            }
            total += number;
            numericCount++;
            if (!double.IsFinite(total))
            {
                return CellValue.FromError("#NUM!");
            }
        }
        if (aggregateKind == ConditionalAggregateKind.Average)
        {
            return numericCount == 0
                ? CellValue.FromError("#DIV/0!")
                : CellValue.FromNumber(total / numericCount);
        }
        return CellValue.FromNumber(total);
    }

    private static bool TryAggregateNumber(
        CellValue value,
        out double number)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                number = (double)value.RawValue!;
                return true;
            case CellValueKind.DateTime:
                number = ((DateTime)value.RawValue!).ToOADate();
                return true;
            default:
                number = default;
                return false;
        }
    }

    private static bool TryReadReference(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out ReferenceValues values)
    {
        if (node is CellNode cell)
        {
            var range = new CellRange(cell.Address, cell.Address);
            dependencies.Add(new FormulaDependency(
                cell.WorksheetName,
                range));
            values = new ReferenceValues(
                1,
                1,
                [context.GetCellValue(
                    cell.WorksheetName,
                    cell.Address)]);
            return true;
        }
        if (node is RangeNode reference)
        {
            dependencies.Add(new FormulaDependency(
                reference.WorksheetName,
                reference.Range));
            var cells = new CellValue[checked(
                reference.Range.RowCount *
                reference.Range.ColumnCount)];
            var offset = 0;
            for (var row = reference.Range.Top;
                 row <= reference.Range.Bottom;
                 row++)
            {
                for (var column = reference.Range.Left;
                     column <= reference.Range.Right;
                     column++)
                {
                    cells[offset++] = context.GetCellValue(
                        reference.WorksheetName,
                        new CellAddress(row, column));
                }
            }
            values = new ReferenceValues(
                reference.Range.RowCount,
                reference.Range.ColumnCount,
                cells);
            return true;
        }
        values = default!;
        return false;
    }

    private static bool HasSameShape(
        ReferenceValues left,
        ReferenceValues right) =>
        left.RowCount == right.RowCount &&
        left.ColumnCount == right.ColumnCount;

    private static bool ContainsConditionalAggregate(FormulaNode node) =>
        node switch
        {
            FunctionNode function =>
                IsConditionalAggregate(function.Name) ||
                function.Arguments.Any(ContainsConditionalAggregate),
            UnaryNode unary => ContainsConditionalAggregate(unary.Operand),
            BinaryNode binary =>
                ContainsConditionalAggregate(binary.Left) ||
                ContainsConditionalAggregate(binary.Right),
            _ => false,
        };

    private static bool IsConditionalAggregate(string name) =>
        name.Equals("COUNTIF", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("COUNTIFS", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("SUMIF", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("SUMIFS", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("AVERAGEIF", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("AVERAGEIFS", StringComparison.OrdinalIgnoreCase);

    private static bool IsLazyControl(string name) =>
        name.Equals("IF", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("IFERROR", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("IFNA", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("IFS", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("SWITCH", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("CHOOSE", StringComparison.OrdinalIgnoreCase);

    private static bool NameEquals(
        FunctionNode function,
        string name) =>
        function.Name.Equals(name, StringComparison.OrdinalIgnoreCase);

    private static bool TryPositiveInteger(
        CellValue value,
        out int result)
    {
        if (!FormulaValueCoercion.TryNumber(value, out var number) ||
            !double.IsFinite(number) ||
            number < 1d ||
            number > int.MaxValue ||
            number != Math.Truncate(number))
        {
            result = default;
            return false;
        }
        result = checked((int)number);
        return true;
    }

    private static bool ValuesEqual(CellValue left, CellValue right)
    {
        if (left.Kind == CellValueKind.Error ||
            right.Kind == CellValueKind.Error)
        {
            return left.Kind == right.Kind &&
                   string.Equals(
                       left.ToString(),
                       right.ToString(),
                       StringComparison.OrdinalIgnoreCase);
        }
        if (FormulaValueCoercion.TryNumber(left, out var leftNumber) &&
            FormulaValueCoercion.TryNumber(right, out var rightNumber))
        {
            return leftNumber.Equals(rightNumber);
        }
        if (left.Kind == CellValueKind.Text ||
            right.Kind == CellValueKind.Text)
        {
            return string.Equals(
                left.ToString(),
                right.ToString(),
                StringComparison.OrdinalIgnoreCase);
        }
        return left.Equals(right);
    }

    private static bool IsError(CellValue value, string error) =>
        value.Kind == CellValueKind.Error &&
        string.Equals(
            value.ToString(),
            error,
            StringComparison.OrdinalIgnoreCase);

    private static FormulaEvaluationResult CreateResult(
        CellValue value,
        IEnumerable<FormulaDependency> dependencies)
    {
        var distinct = dependencies.Distinct().ToArray();
        if (value.Kind != CellValueKind.Error)
        {
            return FormulaEvaluationResult.Success(value, distinct);
        }
        return new FormulaEvaluationResult(
            value,
            MapErrorCode(value),
            distinct);
    }

    private static FormulaErrorCode MapErrorCode(CellValue value) =>
        value.ToString() switch
        {
            "#DIV/0!" => FormulaErrorCode.DivisionByZero,
            "#REF!" => FormulaErrorCode.InvalidReference,
            "#NAME?" => FormulaErrorCode.InvalidName,
            "#CIRC!" => FormulaErrorCode.CircularReference,
            "#N/A" => FormulaErrorCode.NotAvailable,
            "#SPILL!" => FormulaErrorCode.Spill,
            _ => FormulaErrorCode.InvalidValue,
        };

    private sealed record ReferenceValues(
        int RowCount,
        int ColumnCount,
        CellValue[] Values);

    private sealed record CriteriaRange(
        ReferenceValues Range,
        FormulaCriterion Criterion);

    private enum ConditionalAggregateKind
    {
        Sum,
        Average,
    }

    private static class ConditionalFormulaWriter
    {
        public static string Write(
            FormulaNode node,
            Func<FormulaNode, CellValue?> replacement)
        {
            ArgumentNullException.ThrowIfNull(node);
            ArgumentNullException.ThrowIfNull(replacement);
            var builder = new StringBuilder("=");
            Append(builder, node, replacement);
            return builder.ToString();
        }

        private static void Append(
            StringBuilder builder,
            FormulaNode node,
            Func<FormulaNode, CellValue?> replacement)
        {
            var value = replacement(node);
            if (value is not null)
            {
                AppendConstant(builder, value.Value);
                return;
            }
            switch (node)
            {
                case ConstantNode constant:
                    AppendConstant(builder, constant.Value);
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
                case UnaryNode unary:
                    builder.Append(unary.Operator == FormulaTokenKind.Minus
                        ? '-'
                        : '+');
                    builder.Append('(');
                    Append(builder, unary.Operand, replacement);
                    builder.Append(')');
                    break;
                case BinaryNode binary:
                    builder.Append('(');
                    Append(builder, binary.Left, replacement);
                    builder.Append(GetOperator(binary.Operator));
                    Append(builder, binary.Right, replacement);
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
                        Append(
                            builder,
                            function.Arguments[index],
                            replacement);
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
                    builder.Append(value.ToString());
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
