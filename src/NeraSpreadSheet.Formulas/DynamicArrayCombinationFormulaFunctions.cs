using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraDynamicArrayFormulaEngine
{
    private FormulaArrayEvaluationResult EvaluateHStack(
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

        var arrays = new List<FormulaArrayValue>(function.Arguments.Count);
        var rowCount = 0;
        long columnCount = 0;
        foreach (var node in function.Arguments)
        {
            var source = EvaluateNodeAsArray(node, context, dependencies);
            if (!source.IsSuccess)
            {
                return source;
            }
            arrays.Add(source.Value!);
            rowCount = Math.Max(rowCount, source.Value!.RowCount);
            columnCount = checked(columnCount + source.Value.ColumnCount);
        }

        var cellCount = checked((long)rowCount * columnCount);
        if (columnCount > int.MaxValue ||
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
                runningOffset + arrays[index].ColumnCount);
        }

        var padding = CellValue.FromError("#N/A");
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                rowCount,
                checked((int)columnCount),
                (row, column) =>
                {
                    for (var index = arrays.Count - 1;
                         index >= 0;
                         index--)
                    {
                        if (column < offsets[index])
                        {
                            continue;
                        }
                        var localColumn = column - offsets[index];
                        if (localColumn >= arrays[index].ColumnCount)
                        {
                            continue;
                        }
                        return row < arrays[index].RowCount
                            ? arrays[index][row, localColumn]
                            : padding;
                    }
                    return padding;
                }),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateIndirectArray(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (!IndirectFormulaEvaluation.TryResolve(
                function,
                node => EvaluateScalarNode(
                    node,
                    context,
                    dependencies),
                context,
                out var target,
                out var error))
        {
            return FormulaArrayEvaluationResult.Failure(
                error,
                ToErrorCode(error),
                DistinctDependencies(dependencies));
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

    private FormulaArrayEvaluationResult EvaluateGroupBy(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 3 or > 8)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var rowFieldsResult = EvaluateNodeAsArray(
            function.Arguments[0],
            context,
            dependencies);
        if (!rowFieldsResult.IsSuccess)
        {
            return rowFieldsResult;
        }
        var valuesResult = EvaluateNodeAsArray(
            function.Arguments[1],
            context,
            dependencies);
        if (!valuesResult.IsSuccess)
        {
            return valuesResult;
        }

        var rowFields = rowFieldsResult.Value!;
        var values = valuesResult.Value!;
        if (rowFields.RowCount != values.RowCount)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryGetGroupByAggregate(
                function.Arguments[2],
                out var aggregateKind))
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        if (!TryReadOptionalInteger(
                function,
                3,
                -1,
                context,
                dependencies,
                out var fieldHeaders,
                out var argumentError) ||
            fieldHeaders is < -1 or > 3)
        {
            return argumentError ?? Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryReadOptionalInteger(
                function,
                4,
                1,
                context,
                dependencies,
                out var totalDepth,
                out argumentError) ||
            totalDepth is < -1 or > 1)
        {
            return argumentError ?? Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryReadOptionalInteger(
                function,
                5,
                0,
                context,
                dependencies,
                out var sortOrder,
                out argumentError))
        {
            return argumentError!;
        }
        if (!TryReadOptionalInteger(
                function,
                7,
                0,
                context,
                dependencies,
                out var fieldRelationship,
                out argumentError) ||
            fieldRelationship is < 0 or > 1)
        {
            return argumentError ?? Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var hasInputHeaders = fieldHeaders switch
        {
            1 or 3 => true,
            0 or 2 => false,
            _ => DetectGroupByHeaders(values),
        };
        var showHeaders = fieldHeaders switch
        {
            2 or 3 => true,
            0 or 1 => false,
            _ => rowFields.ColumnCount > 1 || values.ColumnCount > 1,
        };
        var dataStart = hasInputHeaders ? 1 : 0;
        if (dataStart >= rowFields.RowCount)
        {
            return Failure(
                "#CALC!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        bool[]? filter = null;
        if (function.Arguments.Count >= 7 &&
            function.Arguments[6] is not MissingArgumentNode)
        {
            if (!TryReadGroupByFilter(
                    function.Arguments[6],
                    rowFields.RowCount,
                    context,
                    dependencies,
                    out filter,
                    out argumentError))
            {
                return argumentError!;
            }
        }

        var groups = new List<GroupByGroup>();
        var lookup = new Dictionary<CellValue[], GroupByGroup>(
            GroupByKeyComparer.Instance);
        var includedRows = new List<int>();
        for (var row = dataStart; row < rowFields.RowCount; row++)
        {
            if (filter is not null && !filter[row])
            {
                continue;
            }
            includedRows.Add(row);
            var key = rowFields.EnumerateRow(row).ToArray();
            if (!lookup.TryGetValue(key, out var group))
            {
                group = new GroupByGroup(key);
                lookup.Add(key, group);
                groups.Add(group);
            }
            group.Rows.Add(row);
        }
        if (includedRows.Count == 0)
        {
            return Failure(
                "#CALC!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var results = groups
            .Select((group, index) => new GroupByResult(
                index,
                group.Key,
                AggregateColumns(values, group.Rows, aggregateKind)))
            .ToList();
        var outputColumns = checked(
            rowFields.ColumnCount + values.ColumnCount);
        if (sortOrder != 0)
        {
            var absoluteSort = Math.Abs((long)sortOrder);
            if (absoluteSort < 1 || absoluteSort > outputColumns)
            {
                return Failure(
                    "#VALUE!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
            }
            var sortIndex = checked((int)absoluteSort - 1);
            var descending = sortOrder < 0;
            results = results
                .OrderBy(
                    result => GetGroupByOutputValue(
                        result,
                        sortIndex,
                        rowFields.ColumnCount),
                    descending
                        ? GroupByCellComparer.Descending
                        : GroupByCellComparer.Ascending)
                .ThenBy(static result => result.OriginalIndex)
                .ToList();
        }

        var outputRows = checked(
            results.Count +
            (showHeaders ? 1 : 0) +
            (totalDepth == 0 ? 0 : 1));
        var outputCellCount = checked((long)outputRows * outputColumns);
        if (outputCellCount > FormulaArrayValue.MaximumCellCount)
        {
            return Failure(
                "#NUM!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        var output = new List<CellValue[]>(outputRows);
        if (showHeaders)
        {
            output.Add(CreateGroupByHeaders(
                rowFields,
                values,
                hasInputHeaders));
        }
        var total = new GroupByResult(
            -1,
            CreateTotalKey(rowFields.ColumnCount),
            AggregateColumns(values, includedRows, aggregateKind));
        if (totalDepth < 0)
        {
            output.Add(FlattenGroupByResult(total));
        }
        output.AddRange(results.Select(FlattenGroupByResult));
        if (totalDepth > 0)
        {
            output.Add(FlattenGroupByResult(total));
        }

        return FormulaArrayEvaluationResult.Success(
            new FormulaArrayValue(
                output.Count,
                outputColumns,
                output.SelectMany(static row => row)),
            DistinctDependencies(dependencies));
    }

    private bool TryReadOptionalInteger(
        FunctionNode function,
        int argumentIndex,
        int defaultValue,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out int value,
        out FormulaArrayEvaluationResult? error)
    {
        if (function.Arguments.Count <= argumentIndex ||
            function.Arguments[argumentIndex] is MissingArgumentNode)
        {
            value = defaultValue;
            error = null;
            return true;
        }

        var scalar = EvaluateScalarNode(
            function.Arguments[argumentIndex],
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
        if (scalar.Kind == CellValueKind.Blank)
        {
            value = defaultValue;
            error = null;
            return true;
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

    private bool TryReadGroupByFilter(
        FormulaNode node,
        int rowCount,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out bool[] filter,
        out FormulaArrayEvaluationResult? error)
    {
        var filterResult = EvaluateNodeAsArray(
            node,
            context,
            dependencies);
        if (!filterResult.IsSuccess)
        {
            filter = [];
            error = filterResult;
            return false;
        }
        var value = filterResult.Value!;
        var isColumn = value.RowCount == rowCount &&
                       value.ColumnCount == 1;
        var isRow = value.RowCount == 1 &&
                    value.ColumnCount == rowCount;
        if (!isColumn && !isRow)
        {
            filter = [];
            error = Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
            return false;
        }

        filter = new bool[rowCount];
        for (var index = 0; index < rowCount; index++)
        {
            var cell = isColumn ? value[index, 0] : value[0, index];
            if (cell.Kind == CellValueKind.Error)
            {
                error = FormulaArrayEvaluationResult.Failure(
                    cell,
                    ToErrorCode(cell),
                    DistinctDependencies(dependencies));
                return false;
            }
            if (!FormulaValueCoercion.TryBoolean(cell, out filter[index]))
            {
                error = Failure(
                    "#VALUE!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
                return false;
            }
        }
        error = null;
        return true;
    }

    private static bool TryGetGroupByAggregate(
        FormulaNode node,
        out GroupByAggregateKind kind)
    {
        string? name = node switch
        {
            NameNode formulaName => formulaName.Name,
            ConstantNode constant
                when constant.Value.Kind == CellValueKind.Text =>
                (string)constant.Value.RawValue!,
            FunctionNode function when function.Arguments.Count == 0 =>
                function.Name,
            _ => null,
        };
        kind = name?.Trim().ToUpperInvariant() switch
        {
            "SUM" => GroupByAggregateKind.Sum,
            "AVERAGE" => GroupByAggregateKind.Average,
            "COUNT" => GroupByAggregateKind.Count,
            "COUNTA" => GroupByAggregateKind.CountA,
            "MAX" => GroupByAggregateKind.Maximum,
            "MIN" => GroupByAggregateKind.Minimum,
            _ => default,
        };
        return name is not null &&
               name.Trim().ToUpperInvariant() is
                   "SUM" or "AVERAGE" or "COUNT" or
                   "COUNTA" or "MAX" or "MIN";
    }

    private static bool DetectGroupByHeaders(FormulaArrayValue values)
    {
        if (values.RowCount < 2)
        {
            return false;
        }
        var firstHasText = values.EnumerateRow(0)
            .Any(static value => value.Kind == CellValueKind.Text);
        var secondHasNumber = values.EnumerateRow(1)
            .Any(static value => value.Kind == CellValueKind.Number);
        return firstHasText && secondHasNumber;
    }

    private static CellValue[] AggregateColumns(
        FormulaArrayValue values,
        IReadOnlyList<int> rows,
        GroupByAggregateKind kind)
    {
        var result = new CellValue[values.ColumnCount];
        for (var column = 0;
             column < values.ColumnCount;
             column++)
        {
            result[column] = AggregateGroupByColumn(
                values,
                rows,
                column,
                kind);
        }
        return result;
    }

    private static CellValue AggregateGroupByColumn(
        FormulaArrayValue values,
        IReadOnlyList<int> rows,
        int column,
        GroupByAggregateKind kind)
    {
        CellValue? firstError = null;
        var numbers = new List<double>();
        var nonBlankCount = 0;
        foreach (var row in rows)
        {
            var value = values[row, column];
            if (value.Kind == CellValueKind.Error)
            {
                firstError ??= value;
                continue;
            }
            if (!value.IsBlank)
            {
                nonBlankCount++;
            }
            if (value.Kind == CellValueKind.Number)
            {
                numbers.Add((double)value.RawValue!);
            }
        }
        if (firstError is not null)
        {
            return firstError.Value;
        }

        return kind switch
        {
            GroupByAggregateKind.Sum =>
                FormulaValueCoercion.SafeNumber(numbers.Sum()),
            GroupByAggregateKind.Average => numbers.Count == 0
                ? CellValue.FromError("#DIV/0!")
                : FormulaValueCoercion.SafeNumber(numbers.Average()),
            GroupByAggregateKind.Count =>
                CellValue.FromNumber(numbers.Count),
            GroupByAggregateKind.CountA =>
                CellValue.FromNumber(nonBlankCount),
            GroupByAggregateKind.Maximum => numbers.Count == 0
                ? CellValue.FromNumber(0d)
                : FormulaValueCoercion.SafeNumber(numbers.Max()),
            GroupByAggregateKind.Minimum => numbers.Count == 0
                ? CellValue.FromNumber(0d)
                : FormulaValueCoercion.SafeNumber(numbers.Min()),
            _ => CellValue.FromError("#VALUE!"),
        };
    }

    private static CellValue[] CreateGroupByHeaders(
        FormulaArrayValue rowFields,
        FormulaArrayValue values,
        bool hasInputHeaders)
    {
        var headers = new CellValue[
            checked(rowFields.ColumnCount + values.ColumnCount)];
        for (var column = 0;
             column < rowFields.ColumnCount;
             column++)
        {
            headers[column] = hasInputHeaders
                ? rowFields[0, column]
                : CellValue.FromText(
                    $"Row {column + 1}");
        }
        for (var column = 0;
             column < values.ColumnCount;
             column++)
        {
            headers[rowFields.ColumnCount + column] = hasInputHeaders
                ? values[0, column]
                : CellValue.FromText(
                    $"Value {column + 1}");
        }
        return headers;
    }

    private static CellValue[] CreateTotalKey(int columnCount)
    {
        var key = new CellValue[columnCount];
        key[0] = CellValue.FromText("Grand Total");
        return key;
    }

    private static CellValue[] FlattenGroupByResult(
        GroupByResult result) =>
        [.. result.Key, .. result.Aggregates];

    private static CellValue GetGroupByOutputValue(
        GroupByResult result,
        int outputIndex,
        int keyColumnCount) =>
        outputIndex < keyColumnCount
            ? result.Key[outputIndex]
            : result.Aggregates[outputIndex - keyColumnCount];

    private enum GroupByAggregateKind
    {
        Sum = 0,
        Average,
        Count,
        CountA,
        Maximum,
        Minimum,
    }

    private sealed class GroupByGroup
    {
        public GroupByGroup(CellValue[] key)
        {
            Key = key;
        }

        public CellValue[] Key { get; }

        public List<int> Rows { get; } = [];
    }

    private sealed record GroupByResult(
        int OriginalIndex,
        CellValue[] Key,
        CellValue[] Aggregates);

    private sealed class GroupByKeyComparer :
        IEqualityComparer<CellValue[]>
    {
        public static GroupByKeyComparer Instance { get; } = new();

        public bool Equals(CellValue[]? left, CellValue[]? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }
            if (left is null || right is null ||
                left.Length != right.Length)
            {
                return false;
            }
            for (var index = 0; index < left.Length; index++)
            {
                if (!CellEquals(left[index], right[index]))
                {
                    return false;
                }
            }
            return true;
        }

        public int GetHashCode(CellValue[] values)
        {
            var hash = new HashCode();
            foreach (var value in values)
            {
                if (value.Kind == CellValueKind.Text)
                {
                    hash.Add(
                        (string)value.RawValue!,
                        StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    hash.Add(value);
                }
            }
            return hash.ToHashCode();
        }

        private static bool CellEquals(CellValue left, CellValue right) =>
            left.Kind == CellValueKind.Text &&
            right.Kind == CellValueKind.Text
                ? string.Equals(
                    (string)left.RawValue!,
                    (string)right.RawValue!,
                    StringComparison.OrdinalIgnoreCase)
                : left.Equals(right);
    }

    private sealed class GroupByCellComparer : IComparer<CellValue>
    {
        public static GroupByCellComparer Ascending { get; } =
            new(descending: false);

        public static GroupByCellComparer Descending { get; } =
            new(descending: true);

        private readonly bool _descending;

        private GroupByCellComparer(bool descending)
        {
            _descending = descending;
        }

        public int Compare(CellValue left, CellValue right)
        {
            var comparison = CompareAscending(left, right);
            return _descending ? -comparison : comparison;
        }

        private static int CompareAscending(
            CellValue left,
            CellValue right)
        {
            var rank = GetRank(left).CompareTo(GetRank(right));
            if (rank != 0)
            {
                return rank;
            }
            return left.Kind switch
            {
                CellValueKind.Blank => 0,
                CellValueKind.Number =>
                    ((double)left.RawValue!).CompareTo(
                        (double)right.RawValue!),
                CellValueKind.DateTime =>
                    ((DateTime)left.RawValue!).CompareTo(
                        (DateTime)right.RawValue!),
                CellValueKind.Text => string.Compare(
                    (string)left.RawValue!,
                    (string)right.RawValue!,
                    StringComparison.OrdinalIgnoreCase),
                CellValueKind.Boolean =>
                    ((bool)left.RawValue!).CompareTo(
                        (bool)right.RawValue!),
                CellValueKind.Error => string.Compare(
                    Convert.ToString(
                        left.RawValue,
                        CultureInfo.InvariantCulture),
                    Convert.ToString(
                        right.RawValue,
                        CultureInfo.InvariantCulture),
                    StringComparison.OrdinalIgnoreCase),
                _ => 0,
            };
        }

        private static int GetRank(CellValue value) =>
            value.Kind switch
            {
                CellValueKind.Blank => 0,
                CellValueKind.Number => 1,
                CellValueKind.DateTime => 2,
                CellValueKind.Text => 3,
                CellValueKind.Boolean => 4,
                CellValueKind.Error => 5,
                _ => 6,
            };
    }
}
