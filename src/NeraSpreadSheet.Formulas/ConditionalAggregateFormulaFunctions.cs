using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraFormulaEngine
{
    public const long MaximumConditionalAggregatePositions = 2_000_000L;

    private bool TryEvaluateConditionalAggregate(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out CellValue value)
    {
        if (string.Equals(
                function.Name,
                "COUNTIF",
                StringComparison.OrdinalIgnoreCase))
        {
            value = EvaluateCountIf(function, context, dependencies);
            return true;
        }
        if (string.Equals(
                function.Name,
                "COUNTIFS",
                StringComparison.OrdinalIgnoreCase))
        {
            value = EvaluateCountIfs(function, context, dependencies);
            return true;
        }
        if (string.Equals(
                function.Name,
                "SUMIF",
                StringComparison.OrdinalIgnoreCase))
        {
            value = EvaluateSingleConditionalAggregate(
                function,
                context,
                dependencies,
                ConditionalAggregateKind.Sum);
            return true;
        }
        if (string.Equals(
                function.Name,
                "AVERAGEIF",
                StringComparison.OrdinalIgnoreCase))
        {
            value = EvaluateSingleConditionalAggregate(
                function,
                context,
                dependencies,
                ConditionalAggregateKind.Average);
            return true;
        }
        if (string.Equals(
                function.Name,
                "SUMIFS",
                StringComparison.OrdinalIgnoreCase))
        {
            value = EvaluateMultipleConditionalAggregate(
                function,
                context,
                dependencies,
                ConditionalAggregateKind.Sum);
            return true;
        }
        if (string.Equals(
                function.Name,
                "AVERAGEIFS",
                StringComparison.OrdinalIgnoreCase))
        {
            value = EvaluateMultipleConditionalAggregate(
                function,
                context,
                dependencies,
                ConditionalAggregateKind.Average);
            return true;
        }

        value = default;
        return false;
    }

    private CellValue EvaluateCountIf(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count != 2)
        {
            return CellValue.FromError("#VALUE!");
        }
        if (!TryGetRangeOperand(
                function.Arguments[0],
                dependencies,
                out var range,
                out var rangeError))
        {
            return rangeError;
        }
        if (!TryGetCriteria(
                function.Arguments[1],
                context,
                dependencies,
                out var criteria,
                out var criteriaError))
        {
            return criteriaError;
        }
        if (!IsWithinConditionalAggregateLimit(range, rangePasses: 1))
        {
            return CellValue.FromError("#NUM!");
        }

        var count = 0L;
        for (var row = 0; row < range.RowCount; row++)
        {
            for (var column = 0;
                 column < range.ColumnCount;
                 column++)
            {
                if (criteria.Matches(
                        range.GetValue(row, column, context)))
                {
                    count++;
                }
            }
        }
        return CellValue.FromNumber(count);
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
        if (!TryGetCriteriaPairs(
                function.Arguments,
                startIndex: 0,
                context,
                dependencies,
                out var pairs,
                out var error))
        {
            return error;
        }
        var shape = pairs[0].Range;
        if (!IsWithinConditionalAggregateLimit(
                shape,
                pairs.Length))
        {
            return CellValue.FromError("#NUM!");
        }

        var count = 0L;
        for (var row = 0; row < shape.RowCount; row++)
        {
            for (var column = 0;
                 column < shape.ColumnCount;
                 column++)
            {
                if (MatchesAll(
                        pairs,
                        row,
                        column,
                        context))
                {
                    count++;
                }
            }
        }
        return CellValue.FromNumber(count);
    }

    private CellValue EvaluateSingleConditionalAggregate(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        ConditionalAggregateKind kind)
    {
        if (function.Arguments.Count is < 2 or > 3)
        {
            return CellValue.FromError("#VALUE!");
        }
        if (!TryGetRangeOperand(
                function.Arguments[0],
                dependencies,
                out var criteriaRange,
                out var rangeError))
        {
            return rangeError;
        }
        if (!TryGetCriteria(
                function.Arguments[1],
                context,
                dependencies,
                out var criteria,
                out var criteriaError))
        {
            return criteriaError;
        }

        var aggregateRange = criteriaRange;
        if (function.Arguments.Count == 3 &&
            !TryGetRangeOperand(
                function.Arguments[2],
                dependencies,
                out aggregateRange,
                out rangeError))
        {
            return rangeError;
        }
        if (!HasSameShape(criteriaRange, aggregateRange))
        {
            return CellValue.FromError("#VALUE!");
        }
        if (!IsWithinConditionalAggregateLimit(
                criteriaRange,
                rangePasses: 2))
        {
            return CellValue.FromError("#NUM!");
        }

        return AggregateMatches(
            aggregateRange,
            [new CriteriaRangePair(criteriaRange, criteria)],
            context,
            kind);
    }

    private CellValue EvaluateMultipleConditionalAggregate(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        ConditionalAggregateKind kind)
    {
        if (function.Arguments.Count < 3 ||
            (function.Arguments.Count & 1) == 0)
        {
            return CellValue.FromError("#VALUE!");
        }
        if (!TryGetRangeOperand(
                function.Arguments[0],
                dependencies,
                out var aggregateRange,
                out var rangeError))
        {
            return rangeError;
        }
        if (!TryGetCriteriaPairs(
                function.Arguments,
                startIndex: 1,
                context,
                dependencies,
                out var pairs,
                out var error))
        {
            return error;
        }
        if (pairs.Any(pair =>
                !HasSameShape(aggregateRange, pair.Range)))
        {
            return CellValue.FromError("#VALUE!");
        }
        if (!IsWithinConditionalAggregateLimit(
                aggregateRange,
                checked(pairs.Length + 1)))
        {
            return CellValue.FromError("#NUM!");
        }

        return AggregateMatches(
            aggregateRange,
            pairs,
            context,
            kind);
    }

    private static CellValue AggregateMatches(
        RangeOperand aggregateRange,
        IReadOnlyList<CriteriaRangePair> pairs,
        IFormulaEvaluationContext context,
        ConditionalAggregateKind kind)
    {
        var total = 0d;
        var numericCount = 0L;
        for (var row = 0;
             row < aggregateRange.RowCount;
             row++)
        {
            for (var column = 0;
                 column < aggregateRange.ColumnCount;
                 column++)
            {
                if (!MatchesAll(pairs, row, column, context))
                {
                    continue;
                }

                var value = aggregateRange.GetValue(
                    row,
                    column,
                    context);
                if (value.Kind == CellValueKind.Error)
                {
                    return value;
                }
                if (!TryConditionalAggregateNumber(
                        value,
                        out var number))
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
        }

        if (kind == ConditionalAggregateKind.Average)
        {
            return numericCount == 0
                ? CellValue.FromError("#DIV/0!")
                : FormulaValueCoercion.SafeNumber(
                    total / numericCount);
        }
        return FormulaValueCoercion.SafeNumber(total);
    }

    private bool TryGetCriteriaPairs(
        IReadOnlyList<FormulaNode> arguments,
        int startIndex,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out CriteriaRangePair[] pairs,
        out CellValue error)
    {
        var result = new List<CriteriaRangePair>(
            (arguments.Count - startIndex) / 2);
        RangeOperand? firstRange = null;
        for (var index = startIndex;
             index < arguments.Count;
             index += 2)
        {
            if (!TryGetRangeOperand(
                    arguments[index],
                    dependencies,
                    out var range,
                    out error))
            {
                pairs = [];
                return false;
            }
            if (firstRange is { } shape &&
                !HasSameShape(shape, range))
            {
                pairs = [];
                error = CellValue.FromError("#VALUE!");
                return false;
            }
            if (!TryGetCriteria(
                    arguments[index + 1],
                    context,
                    dependencies,
                    out var criteria,
                    out error))
            {
                pairs = [];
                return false;
            }
            firstRange ??= range;
            result.Add(new CriteriaRangePair(range, criteria));
        }

        pairs = result.ToArray();
        error = CellValue.Blank;
        return true;
    }

    private static bool TryGetRangeOperand(
        FormulaNode node,
        List<FormulaDependency> dependencies,
        out RangeOperand range,
        out CellValue error)
    {
        switch (node)
        {
            case RangeNode rangeNode:
                range = new RangeOperand(
                    rangeNode.WorksheetName,
                    rangeNode.Range);
                dependencies.Add(new FormulaDependency(
                    rangeNode.WorksheetName,
                    rangeNode.Range));
                error = CellValue.Blank;
                return true;
            case CellNode cellNode:
                var cellRange = new CellRange(
                    cellNode.Address,
                    cellNode.Address);
                range = new RangeOperand(
                    cellNode.WorksheetName,
                    cellRange);
                dependencies.Add(new FormulaDependency(
                    cellNode.WorksheetName,
                    cellRange));
                error = CellValue.Blank;
                return true;
            default:
                range = default;
                error = CellValue.FromError("#VALUE!");
                return false;
        }
    }

    private bool TryGetCriteria(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out FormulaCriteria criteria,
        out CellValue error)
    {
        var value = EvaluateNode(node, context, dependencies);
        if (value.Kind == CellValueKind.Error)
        {
            criteria = null!;
            error = value;
            return false;
        }
        try
        {
            criteria = FormulaCriteria.Parse(value);
            error = CellValue.Blank;
            return true;
        }
        catch (FormatException)
        {
            criteria = null!;
            error = CellValue.FromError("#VALUE!");
            return false;
        }
    }

    private static bool MatchesAll(
        IReadOnlyList<CriteriaRangePair> pairs,
        int rowOffset,
        int columnOffset,
        IFormulaEvaluationContext context)
    {
        foreach (var pair in pairs)
        {
            if (!pair.Criteria.Matches(
                    pair.Range.GetValue(
                        rowOffset,
                        columnOffset,
                        context)))
            {
                return false;
            }
        }
        return true;
    }

    private static bool HasSameShape(
        RangeOperand left,
        RangeOperand right) =>
        left.RowCount == right.RowCount &&
        left.ColumnCount == right.ColumnCount;

    private static bool IsWithinConditionalAggregateLimit(
        RangeOperand range,
        int rangePasses)
    {
        if (rangePasses <= 0)
        {
            return false;
        }
        var positions = checked(
            (long)range.RowCount * range.ColumnCount);
        return positions <=
               MaximumConditionalAggregatePositions / rangePasses;
    }

    private static bool TryConditionalAggregateNumber(
        CellValue value,
        out double number)
    {
        if (value.Kind == CellValueKind.Number)
        {
            number = (double)value.RawValue!;
            return true;
        }
        if (value.Kind == CellValueKind.DateTime)
        {
            try
            {
                number = ((DateTime)value.RawValue!).ToOADate();
                return double.IsFinite(number);
            }
            catch (OverflowException)
            {
                // Fall through to non-numeric result.
            }
        }
        number = default;
        return false;
    }

    private readonly record struct RangeOperand(
        string? WorksheetName,
        CellRange Range)
    {
        public int RowCount => Range.RowCount;

        public int ColumnCount => Range.ColumnCount;

        public CellValue GetValue(
            int rowOffset,
            int columnOffset,
            IFormulaEvaluationContext context) =>
            context.GetCellValue(
                WorksheetName,
                new CellAddress(
                    Range.Top + rowOffset,
                    Range.Left + columnOffset));
    }

    private readonly record struct CriteriaRangePair(
        RangeOperand Range,
        FormulaCriteria Criteria);

    private enum ConditionalAggregateKind
    {
        Sum = 0,
        Average,
    }
}
