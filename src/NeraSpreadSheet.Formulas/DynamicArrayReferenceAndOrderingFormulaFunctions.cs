using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraDynamicArrayFormulaEngine
{
    private FormulaArrayEvaluationResult EvaluateOffsetArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (!TryResolveAdvancedReferenceForArray(
                function,
                context,
                dependencies,
                out var target,
                out var error))
        {
            return ReferenceError(error, dependencies);
        }

        dependencies.Add(new FormulaDependency(
            target.WorksheetName,
            target.Range));
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                target.Range.RowCount,
                target.Range.ColumnCount,
                (row, column) => context.GetCellValue(
                    target.WorksheetName,
                    new CellAddress(
                        target.Range.Top + row,
                        target.Range.Left + column))),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateRowArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count == 0)
        {
            if (context is not IFormulaReferenceIntrospectionContext
                introspection)
            {
                return Failure(
                    "#VALUE!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
            }

            return FormulaArrayEvaluationResult.Success(
                new FormulaArrayValue(
                    1,
                    1,
                    [CellValue.FromNumber(
                        introspection.CurrentCellAddress.RowIndex + 1d)]),
                DistinctDependencies(dependencies));
        }
        if (function.Arguments.Count != 1)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryResolveAdvancedReferenceForArray(
                function.Arguments[0],
                context,
                dependencies,
                out var target,
                out var error))
        {
            return ReferenceError(error, dependencies);
        }

        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                target.Range.RowCount,
                1,
                (row, _) => CellValue.FromNumber(
                    target.Range.Top + row + 1d)),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateRowsArray(
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

        if (AdvancedReferenceFormulaEvaluation.TryResolve(
                function.Arguments[0],
                node => EvaluateScalarNode(
                    node,
                    context,
                    dependencies),
                context,
                out var target,
                out _))
        {
            return FormulaArrayEvaluationResult.Success(
                new FormulaArrayValue(
                    1,
                    1,
                    [CellValue.FromNumber(target.Range.RowCount)]),
                DistinctDependencies(dependencies));
        }
        if (function.Arguments[0] is ReferenceUnionNode)
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
        if (!source.IsSuccess)
        {
            return source;
        }

        return FormulaArrayEvaluationResult.Success(
            new FormulaArrayValue(
                1,
                1,
                [CellValue.FromNumber(source.Value!.RowCount)]),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateSortBy(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 253)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var sourceResult = EvaluateNodeAsArray(
            function.Arguments[0],
            context,
            dependencies);
        if (!sourceResult.IsSuccess)
        {
            return sourceResult;
        }
        var source = sourceResult.Value!;
        var keys = new List<SortByKey>();
        SortByOrientation? orientation = null;
        for (var index = 1;
             index < function.Arguments.Count;
             index += 2)
        {
            var keyResult = EvaluateNodeAsArray(
                function.Arguments[index],
                context,
                dependencies);
            if (!keyResult.IsSuccess)
            {
                return keyResult;
            }
            var key = keyResult.Value!;
            if (!TryGetSortByOrientation(
                    source,
                    key,
                    out var keyOrientation) ||
                orientation is not null &&
                orientation != keyOrientation)
            {
                return Failure(
                    "#VALUE!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
            }

            var order = 1;
            if (index + 1 < function.Arguments.Count &&
                function.Arguments[index + 1] is not MissingArgumentNode)
            {
                if (!TryEvaluateIntegerArgument(
                        function.Arguments[index + 1],
                        context,
                        dependencies,
                        out order,
                        out var argumentError))
                {
                    return argumentError!;
                }
                if (order is not 1 and not -1)
                {
                    return Failure(
                        "#VALUE!",
                        FormulaErrorCode.InvalidValue,
                        dependencies);
                }
            }

            orientation = keyOrientation;
            keys.Add(new SortByKey(key, order));
        }

        var effectiveOrientation = orientation!.Value;
        var itemCount = effectiveOrientation == SortByOrientation.Rows
            ? source.RowCount
            : source.ColumnCount;
        var ordered = Enumerable.Range(0, itemCount)
            .OrderBy(
                static index => index,
                Comparer<int>.Create((left, right) =>
                    CompareSortByItems(
                        left,
                        right,
                        effectiveOrientation,
                        keys)))
            .ThenBy(static index => index)
            .ToArray();

        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                source.RowCount,
                source.ColumnCount,
                (row, column) =>
                    effectiveOrientation == SortByOrientation.Rows
                        ? source[ordered[row], column]
                        : source[row, ordered[column]]),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateTake(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 3)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var sourceResult = EvaluateNodeAsArray(
            function.Arguments[0],
            context,
            dependencies);
        if (!sourceResult.IsSuccess)
        {
            return sourceResult;
        }
        var source = sourceResult.Value!;

        var rowsProvided = function.Arguments[1] is not MissingArgumentNode;
        var rows = source.RowCount;
        if (rowsProvided &&
            !TryReadTruncatedArrayInteger(
                function.Arguments[1],
                context,
                dependencies,
                out rows,
                out var argumentError))
        {
            return argumentError!;
        }

        var columnsProvided =
            function.Arguments.Count == 3 &&
            function.Arguments[2] is not MissingArgumentNode;
        var columns = source.ColumnCount;
        if (columnsProvided &&
            !TryReadTruncatedArrayInteger(
                function.Arguments[2],
                context,
                dependencies,
                out columns,
                out argumentError))
        {
            return argumentError!;
        }

        if ((!rowsProvided && !columnsProvided) ||
            (rowsProvided && rows == 0) ||
            (columnsProvided && columns == 0))
        {
            return Failure(
                "#CALC!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var outputRows = rowsProvided
            ? Math.Min(source.RowCount, checked((int)Math.Abs((long)rows)))
            : source.RowCount;
        var outputColumns = columnsProvided
            ? Math.Min(
                source.ColumnCount,
                checked((int)Math.Abs((long)columns)))
            : source.ColumnCount;
        var rowOffset = rowsProvided && rows < 0
            ? source.RowCount - outputRows
            : 0;
        var columnOffset = columnsProvided && columns < 0
            ? source.ColumnCount - outputColumns
            : 0;

        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                outputRows,
                outputColumns,
                (row, column) => source[
                    rowOffset + row,
                    columnOffset + column]),
            DistinctDependencies(dependencies));
    }

    private bool TryResolveAdvancedReferenceForArray(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out FormulaReferenceTarget target,
        out CellValue error) =>
        AdvancedReferenceFormulaEvaluation.TryResolve(
            node,
            candidate => EvaluateScalarNode(
                candidate,
                context,
                dependencies),
            context,
            out target,
            out error);

    private bool TryReadTruncatedArrayInteger(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out int value,
        out FormulaArrayEvaluationResult? error)
    {
        var scalar = EvaluateScalarNode(node, context, dependencies);
        if (scalar.Kind == CellValueKind.Error)
        {
            value = default;
            error = FormulaArrayEvaluationResult.Failure(
                scalar,
                ToErrorCode(scalar),
                DistinctDependencies(dependencies));
            return false;
        }
        if (!FormulaValueCoercion.TryNumber(
                scalar,
                out var number,
                allowText: true) ||
            !double.IsFinite(number))
        {
            value = default;
            error = Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
            return false;
        }

        var truncated = Math.Truncate(number);
        if (truncated < int.MinValue || truncated > int.MaxValue)
        {
            value = default;
            error = Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
            return false;
        }
        value = checked((int)truncated);
        error = null;
        return true;
    }

    private static bool TryGetSortByOrientation(
        FormulaArrayValue source,
        FormulaArrayValue key,
        out SortByOrientation orientation)
    {
        if (key.RowCount == source.RowCount && key.ColumnCount == 1)
        {
            orientation = SortByOrientation.Rows;
            return true;
        }
        if (key.RowCount == 1 && key.ColumnCount == source.ColumnCount)
        {
            orientation = SortByOrientation.Columns;
            return true;
        }

        orientation = default;
        return false;
    }

    private static int CompareSortByItems(
        int left,
        int right,
        SortByOrientation orientation,
        IReadOnlyList<SortByKey> keys)
    {
        foreach (var key in keys)
        {
            var leftValue = orientation == SortByOrientation.Rows
                ? key.Values[left, 0]
                : key.Values[0, left];
            var rightValue = orientation == SortByOrientation.Rows
                ? key.Values[right, 0]
                : key.Values[0, right];
            var comparison = (key.Order == 1
                    ? CellValueSortComparer.Ascending
                    : CellValueSortComparer.Descending)
                .Compare(leftValue, rightValue);
            if (comparison != 0)
            {
                return comparison;
            }
        }
        return left.CompareTo(right);
    }

    private readonly record struct SortByKey(
        FormulaArrayValue Values,
        int Order);

    private enum SortByOrientation
    {
        Rows = 0,
        Columns,
    }
}
