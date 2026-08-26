using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraDynamicArrayFormulaEngine
{
    private FormulaArrayEvaluationResult EvaluateToColumn(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies) =>
        EvaluateFlatten(
            function,
            context,
            dependencies,
            toColumn: true);

    private FormulaArrayEvaluationResult EvaluateToRow(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies) =>
        EvaluateFlatten(
            function,
            context,
            dependencies,
            toColumn: false);

    private FormulaArrayEvaluationResult EvaluateFlatten(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        bool toColumn)
    {
        if (function.Arguments.Count is < 1 or > 3)
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

        if (!TryReadF012OptionalInteger(
                function,
                1,
                0,
                context,
                dependencies,
                out var ignore,
                out var argumentError) ||
            ignore is < 0 or > 3)
        {
            return argumentError ?? Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryReadF012OptionalBoolean(
                function,
                2,
                false,
                context,
                dependencies,
                out var scanByColumn,
                out argumentError))
        {
            return argumentError!;
        }

        var sourceValue = source.Value!;
        var values = new List<CellValue>(sourceValue.Count);
        if (scanByColumn)
        {
            for (var column = 0;
                 column < sourceValue.ColumnCount;
                 column++)
            {
                for (var row = 0;
                     row < sourceValue.RowCount;
                     row++)
                {
                    AddFlattenValue(
                        values,
                        sourceValue[row, column],
                        ignore);
                }
            }
        }
        else
        {
            for (var row = 0;
                 row < sourceValue.RowCount;
                 row++)
            {
                for (var column = 0;
                     column < sourceValue.ColumnCount;
                     column++)
                {
                    AddFlattenValue(
                        values,
                        sourceValue[row, column],
                        ignore);
                }
            }
        }

        if (values.Count == 0)
        {
            return Failure(
                "#CALC!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        return FormulaArrayEvaluationResult.Success(
            new FormulaArrayValue(
                toColumn ? values.Count : 1,
                toColumn ? 1 : values.Count,
                values),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateTrimRange(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 1 or > 3)
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

        if (!TryReadF012OptionalInteger(
                function,
                1,
                3,
                context,
                dependencies,
                out var trimRows,
                out var argumentError) ||
            trimRows is < 0 or > 3 ||
            !TryReadF012OptionalInteger(
                function,
                2,
                3,
                context,
                dependencies,
                out var trimColumns,
                out argumentError) ||
            trimColumns is < 0 or > 3)
        {
            return argumentError ?? Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var value = source.Value!;
        var top = 0;
        var bottom = value.RowCount - 1;
        var left = 0;
        var right = value.ColumnCount - 1;

        if (trimRows is 1 or 3)
        {
            while (top <= bottom &&
                   IsBlankRow(value, top))
            {
                top++;
            }
        }
        if (trimRows is 2 or 3)
        {
            while (bottom >= top &&
                   IsBlankRow(value, bottom))
            {
                bottom--;
            }
        }
        if (trimColumns is 1 or 3)
        {
            while (left <= right &&
                   IsBlankColumn(value, left))
            {
                left++;
            }
        }
        if (trimColumns is 2 or 3)
        {
            while (right >= left &&
                   IsBlankColumn(value, right))
            {
                right--;
            }
        }

        if (top > bottom || left > right)
        {
            return Failure(
                "#CALC!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                bottom - top + 1,
                right - left + 1,
                (row, column) => value[
                    top + row,
                    left + column]),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateVStack(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 1 or > 254)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var arrays = new List<FormulaArrayValue>(
            function.Arguments.Count);
        long rowCount = 0;
        var columnCount = 0;
        foreach (var argument in function.Arguments)
        {
            var source = EvaluateNodeAsArray(
                argument,
                context,
                dependencies);
            if (!source.IsSuccess)
            {
                return source;
            }

            arrays.Add(source.Value!);
            rowCount = checked(rowCount + source.Value!.RowCount);
            columnCount = Math.Max(
                columnCount,
                source.Value.ColumnCount);
        }

        var cellCount = checked(rowCount * columnCount);
        if (rowCount > int.MaxValue ||
            cellCount > FormulaArrayValue.MaximumCellCount)
        {
            return Failure(
                "#NUM!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var offsets = new int[arrays.Count];
        var runningOffset = 0;
        for (var index = 0; index < arrays.Count; index++)
        {
            offsets[index] = runningOffset;
            runningOffset = checked(
                runningOffset + arrays[index].RowCount);
        }

        var padding = CellValue.FromError("#N/A");
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                checked((int)rowCount),
                columnCount,
                (row, column) =>
                {
                    for (var index = arrays.Count - 1;
                         index >= 0;
                         index--)
                    {
                        if (row < offsets[index])
                        {
                            continue;
                        }

                        var localRow = row - offsets[index];
                        if (localRow >= arrays[index].RowCount)
                        {
                            continue;
                        }

                        return column < arrays[index].ColumnCount
                            ? arrays[index][localRow, column]
                            : padding;
                    }

                    return padding;
                }),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateWrapColumns(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies) =>
        EvaluateWrap(
            function,
            context,
            dependencies,
            wrapByColumns: true);

    private FormulaArrayEvaluationResult EvaluateWrapRows(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies) =>
        EvaluateWrap(
            function,
            context,
            dependencies,
            wrapByColumns: false);

    private FormulaArrayEvaluationResult EvaluateWrap(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        bool wrapByColumns)
    {
        if (function.Arguments.Count is < 2 or > 3)
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

        var sourceValue = source.Value!;
        if (sourceValue.RowCount > 1 &&
            sourceValue.ColumnCount > 1)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        if (!TryReadF012Integer(
                function.Arguments[1],
                context,
                dependencies,
                out var wrapCount,
                out var argumentError))
        {
            return argumentError!;
        }
        if (wrapCount < 1)
        {
            return Failure(
                "#NUM!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var vector = sourceValue.ToArray();
        var padding = CellValue.FromError("#N/A");
        if (function.Arguments.Count == 3 &&
            function.Arguments[2] is not MissingArgumentNode)
        {
            padding = EvaluateScalarNode(
                function.Arguments[2],
                context,
                dependencies);
            if (padding.Kind == CellValueKind.Error)
            {
                return FormulaArrayEvaluationResult.Failure(
                    padding,
                    ToErrorCode(padding),
                    DistinctDependencies(dependencies));
            }
        }

        var primary = Math.Min(wrapCount, vector.Length);
        var secondary = checked(
            (vector.Length + primary - 1) / primary);
        var rows = wrapByColumns ? primary : secondary;
        var columns = wrapByColumns ? secondary : primary;
        var cellCount = checked((long)rows * columns);
        if (cellCount > FormulaArrayValue.MaximumCellCount)
        {
            return Failure(
                "#NUM!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                rows,
                columns,
                (row, column) =>
                {
                    var index = wrapByColumns
                        ? (column * rows) + row
                        : (row * columns) + column;
                    return index < vector.Length
                        ? vector[index]
                        : padding;
                }),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateIfErrorArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies) =>
        EvaluateErrorFallbackArray(
            function,
            context,
            dependencies,
            onlyNotAvailable: false);

    private FormulaArrayEvaluationResult EvaluateIfNaArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies) =>
        EvaluateErrorFallbackArray(
            function,
            context,
            dependencies,
            onlyNotAvailable: true);

    private FormulaArrayEvaluationResult EvaluateErrorFallbackArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        bool onlyNotAvailable)
    {
        if (function.Arguments.Count != 2)
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
            if (!ShouldReplaceError(
                    source.ErrorValue,
                    onlyNotAvailable))
            {
                return source;
            }

            return EvaluateNodeAsArray(
                function.Arguments[1],
                context,
                dependencies);
        }

        var sourceValue = source.Value!;
        var needsFallback = sourceValue.ToArray().Any(
            value => ShouldReplaceError(
                value,
                onlyNotAvailable));
        if (!needsFallback)
        {
            return FormulaArrayEvaluationResult.Success(
                sourceValue,
                DistinctDependencies(dependencies));
        }

        var fallback = EvaluateNodeAsArray(
            function.Arguments[1],
            context,
            dependencies);
        if (!fallback.IsSuccess)
        {
            return fallback;
        }
        if (!IsScalarOrSameShape(
                fallback.Value!,
                sourceValue.RowCount,
                sourceValue.ColumnCount))
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                sourceValue.RowCount,
                sourceValue.ColumnCount,
                (row, column) =>
                {
                    var value = sourceValue[row, column];
                    return ShouldReplaceError(
                            value,
                            onlyNotAvailable)
                        ? GetBroadcastValue(
                            fallback.Value!,
                            row,
                            column)
                        : value;
                }),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateSwitchArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 3 or > 254)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var expression = EvaluateNodeAsArray(
            function.Arguments[0],
            context,
            dependencies);
        if (!expression.IsSuccess)
        {
            return expression;
        }

        var expressionValue = expression.Value!;
        var hasDefault = function.Arguments.Count % 2 == 0;
        var pairLimit = hasDefault
            ? function.Arguments.Count - 1
            : function.Arguments.Count;

        if (expressionValue.Count == 1)
        {
            var scalarExpression = expressionValue[0, 0];
            for (var index = 1;
                 index + 1 < pairLimit;
                 index += 2)
            {
                var candidate = EvaluateNodeAsArray(
                    function.Arguments[index],
                    context,
                    dependencies);
                if (!candidate.IsSuccess)
                {
                    return candidate;
                }
                if (candidate.Value!.Count != 1)
                {
                    return Failure(
                        "#VALUE!",
                        FormulaErrorCode.InvalidValue,
                        dependencies);
                }
                if (!XMatchFormulaEvaluation.ValuesEqual(
                        scalarExpression,
                        candidate.Value[0, 0]))
                {
                    continue;
                }

                return EvaluateNodeAsArray(
                    function.Arguments[index + 1],
                    context,
                    dependencies);
            }

            return hasDefault
                ? EvaluateNodeAsArray(
                    function.Arguments[^1],
                    context,
                    dependencies)
                : FormulaArrayEvaluationResult.Failure(
                    CellValue.FromError("#N/A"),
                    FormulaErrorCode.NotAvailable,
                    DistinctDependencies(dependencies));
        }

        var resultValues = new CellValue[expressionValue.Count];
        var resolved = new bool[expressionValue.Count];
        for (var index = 1;
             index + 1 < pairLimit;
             index += 2)
        {
            var candidate = EvaluateNodeAsArray(
                function.Arguments[index],
                context,
                dependencies);
            if (!candidate.IsSuccess)
            {
                return candidate;
            }
            if (!IsScalarOrSameShape(
                    candidate.Value!,
                    expressionValue.RowCount,
                    expressionValue.ColumnCount))
            {
                return Failure(
                    "#VALUE!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
            }

            var matches = new List<(int Flat, int Row, int Column)>();
            for (var row = 0;
                 row < expressionValue.RowCount;
                 row++)
            {
                for (var column = 0;
                     column < expressionValue.ColumnCount;
                     column++)
                {
                    var flat = checked(
                        (row * expressionValue.ColumnCount) + column);
                    if (resolved[flat])
                    {
                        continue;
                    }

                    if (XMatchFormulaEvaluation.ValuesEqual(
                            expressionValue[row, column],
                            GetBroadcastValue(
                                candidate.Value!,
                                row,
                                column)))
                    {
                        matches.Add((flat, row, column));
                    }
                }
            }
            if (matches.Count == 0)
            {
                continue;
            }

            var branch = EvaluateNodeAsArray(
                function.Arguments[index + 1],
                context,
                dependencies);
            if (!branch.IsSuccess)
            {
                return branch;
            }
            if (!IsScalarOrSameShape(
                    branch.Value!,
                    expressionValue.RowCount,
                    expressionValue.ColumnCount))
            {
                return Failure(
                    "#VALUE!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
            }

            foreach (var match in matches)
            {
                resultValues[match.Flat] = GetBroadcastValue(
                    branch.Value!,
                    match.Row,
                    match.Column);
                resolved[match.Flat] = true;
            }
        }

        var unresolved = Enumerable.Range(0, resolved.Length)
            .Where(index => !resolved[index])
            .ToArray();
        if (unresolved.Length > 0)
        {
            FormulaArrayValue? fallback = null;
            if (hasDefault)
            {
                var fallbackResult = EvaluateNodeAsArray(
                    function.Arguments[^1],
                    context,
                    dependencies);
                if (!fallbackResult.IsSuccess)
                {
                    return fallbackResult;
                }
                if (!IsScalarOrSameShape(
                        fallbackResult.Value!,
                        expressionValue.RowCount,
                        expressionValue.ColumnCount))
                {
                    return Failure(
                        "#VALUE!",
                        FormulaErrorCode.InvalidValue,
                        dependencies);
                }

                fallback = fallbackResult.Value;
            }

            foreach (var flat in unresolved)
            {
                var row = flat / expressionValue.ColumnCount;
                var column = flat % expressionValue.ColumnCount;
                resultValues[flat] = fallback is null
                    ? CellValue.FromError("#N/A")
                    : GetBroadcastValue(fallback, row, column);
            }
        }

        return FormulaArrayEvaluationResult.Success(
            new FormulaArrayValue(
                expressionValue.RowCount,
                expressionValue.ColumnCount,
                resultValues),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateXMatchArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 4)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var lookup = EvaluateNodeAsArray(
            function.Arguments[0],
            context,
            dependencies);
        if (!lookup.IsSuccess)
        {
            return lookup;
        }
        if (lookup.Value!.Count != 1)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var lookupArray = EvaluateNodeAsArray(
            function.Arguments[1],
            context,
            dependencies);
        if (!lookupArray.IsSuccess)
        {
            return lookupArray;
        }
        if (lookupArray.Value!.RowCount > 1 &&
            lookupArray.Value.ColumnCount > 1)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        if (!TryReadF012OptionalInteger(
                function,
                2,
                0,
                context,
                dependencies,
                out var matchMode,
                out var argumentError) ||
            !TryReadF012OptionalInteger(
                function,
                3,
                1,
                context,
                dependencies,
                out var searchMode,
                out argumentError))
        {
            return argumentError!;
        }

        return XMatchFormulaEvaluation.TryMatch(
                lookup.Value[0, 0],
                lookupArray.Value.ToArray(),
                matchMode,
                searchMode,
                out var position,
                out var error)
            ? FormulaArrayEvaluationResult.Success(
                new FormulaArrayValue(
                    1,
                    1,
                    [CellValue.FromNumber(position)]),
                DistinctDependencies(dependencies))
            : FormulaArrayEvaluationResult.Failure(
                error,
                ToErrorCode(error),
                DistinctDependencies(dependencies));
    }

    private static void AddFlattenValue(
        List<CellValue> values,
        CellValue value,
        int ignore)
    {
        var ignoreBlank = ignore is 1 or 3;
        var ignoreError = ignore is 2 or 3;
        if (ignoreBlank && value.IsBlank ||
            ignoreError && value.Kind == CellValueKind.Error)
        {
            return;
        }

        values.Add(value);
    }

    private static bool IsBlankRow(
        FormulaArrayValue value,
        int row)
    {
        for (var column = 0;
             column < value.ColumnCount;
             column++)
        {
            if (!value[row, column].IsBlank)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsBlankColumn(
        FormulaArrayValue value,
        int column)
    {
        for (var row = 0; row < value.RowCount; row++)
        {
            if (!value[row, column].IsBlank)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryReadF012OptionalInteger(
        FunctionNode function,
        int index,
        int defaultValue,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out int value,
        out FormulaArrayEvaluationResult? error)
    {
        if (function.Arguments.Count <= index ||
            function.Arguments[index] is MissingArgumentNode)
        {
            value = defaultValue;
            error = null;
            return true;
        }

        return TryReadF012Integer(
            function.Arguments[index],
            context,
            dependencies,
            out value,
            out error);
    }

    private bool TryReadF012Integer(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out int value,
        out FormulaArrayEvaluationResult? error)
    {
        var scalar = EvaluateScalarNode(
            node,
            context,
            dependencies);
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
            !double.IsFinite(number) ||
            number < int.MinValue ||
            number > int.MaxValue)
        {
            value = default;
            error = Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
            return false;
        }

        value = checked((int)Math.Truncate(number));
        error = null;
        return true;
    }

    private bool TryReadF012OptionalBoolean(
        FunctionNode function,
        int index,
        bool defaultValue,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out bool value,
        out FormulaArrayEvaluationResult? error)
    {
        if (function.Arguments.Count <= index ||
            function.Arguments[index] is MissingArgumentNode)
        {
            value = defaultValue;
            error = null;
            return true;
        }

        var scalar = EvaluateScalarNode(
            function.Arguments[index],
            context,
            dependencies);
        if (scalar.Kind == CellValueKind.Error)
        {
            value = default;
            error = FormulaArrayEvaluationResult.Failure(
                scalar,
                ToErrorCode(scalar),
                DistinctDependencies(dependencies));
            return false;
        }
        if (!FormulaValueCoercion.TryBoolean(
                scalar,
                out value,
                allowText: true))
        {
            error = Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
            return false;
        }

        error = null;
        return true;
    }

    private static bool ShouldReplaceError(
        CellValue value,
        bool onlyNotAvailable) =>
        value.Kind == CellValueKind.Error &&
        (!onlyNotAvailable ||
         string.Equals(
             Convert.ToString(
                 value.RawValue,
                 CultureInfo.InvariantCulture),
             "#N/A",
             StringComparison.OrdinalIgnoreCase));

    private static bool IsScalarOrSameShape(
        FormulaArrayValue value,
        int rowCount,
        int columnCount) =>
        value.Count == 1 ||
        value.RowCount == rowCount &&
        value.ColumnCount == columnCount;

    private static CellValue GetBroadcastValue(
        FormulaArrayValue value,
        int row,
        int column) =>
        value.Count == 1
            ? value[0, 0]
            : value[row, column];
}
