using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraDynamicArrayFormulaEngine
{
    private FormulaArrayEvaluationResult EvaluateFilter(
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

        var source = EvaluateNodeAsArray(
            function.Arguments[0],
            context,
            dependencies);
        if (!source.IsSuccess)
        {
            return source;
        }
        var include = EvaluateNodeAsArray(
            function.Arguments[1],
            context,
            dependencies);
        if (!include.IsSuccess)
        {
            return include;
        }

        var sourceValue = source.Value!;
        var includeValue = include.Value!;
        var filterRows =
            includeValue.RowCount == sourceValue.RowCount &&
            includeValue.ColumnCount == 1;
        var filterColumns =
            includeValue.RowCount == 1 &&
            includeValue.ColumnCount == sourceValue.ColumnCount;
        if (!filterRows && !filterColumns)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        if (filterRows)
        {
            var selectedRows = new List<int>();
            for (var row = 0; row < includeValue.RowCount; row++)
            {
                var condition = includeValue[row, 0];
                if (condition.Kind == CellValueKind.Error)
                {
                    return FormulaArrayEvaluationResult.Failure(
                        condition,
                        ToErrorCode(condition),
                        DistinctDependencies(dependencies));
                }
                if (!FormulaValueCoercion.TryBoolean(
                        condition,
                        out var selected))
                {
                    return Failure(
                        "#VALUE!",
                        FormulaErrorCode.InvalidValue,
                        dependencies);
                }
                if (selected)
                {
                    selectedRows.Add(row);
                }
            }
            if (selectedRows.Count == 0)
            {
                return EvaluateFilterEmpty(
                    function,
                    context,
                    dependencies);
            }
            return FormulaArrayEvaluationResult.Success(
                FormulaArrayValue.Create(
                    selectedRows.Count,
                    sourceValue.ColumnCount,
                    (row, column) => sourceValue[
                        selectedRows[row],
                        column]),
                DistinctDependencies(dependencies));
        }

        var selectedColumns = new List<int>();
        for (var column = 0;
             column < includeValue.ColumnCount;
             column++)
        {
            var condition = includeValue[0, column];
            if (condition.Kind == CellValueKind.Error)
            {
                return FormulaArrayEvaluationResult.Failure(
                    condition,
                    ToErrorCode(condition),
                    DistinctDependencies(dependencies));
            }
            if (!FormulaValueCoercion.TryBoolean(
                    condition,
                    out var selected))
            {
                return Failure(
                    "#VALUE!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
            }
            if (selected)
            {
                selectedColumns.Add(column);
            }
        }
        if (selectedColumns.Count == 0)
        {
            return EvaluateFilterEmpty(
                function,
                context,
                dependencies);
        }
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                sourceValue.RowCount,
                selectedColumns.Count,
                (row, column) => sourceValue[
                    row,
                    selectedColumns[column]]),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateFilterEmpty(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count < 3)
        {
            return Failure(
                "#CALC!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        var fallback = EvaluateNodeAsArray(
            function.Arguments[2],
            context,
            dependencies);
        return fallback.IsSuccess
            ? FormulaArrayEvaluationResult.Success(
                fallback.Value!,
                DistinctDependencies(dependencies))
            : fallback;
    }

    private FormulaArrayEvaluationResult EvaluateSort(
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
        var source = EvaluateNodeAsArray(
            function.Arguments[0],
            context,
            dependencies);
        if (!source.IsSuccess)
        {
            return source;
        }

        var sortIndex = 1;
        var sortOrder = 1;
        var byColumn = false;
        if (function.Arguments.Count >= 2 &&
            !TryEvaluateIntegerArgument(
                function.Arguments[1],
                context,
                dependencies,
                out sortIndex,
                out var argumentError))
        {
            return argumentError!;
        }
        if (function.Arguments.Count >= 3 &&
            !TryEvaluateIntegerArgument(
                function.Arguments[2],
                context,
                dependencies,
                out sortOrder,
                out argumentError))
        {
            return argumentError!;
        }
        if (sortOrder is not 1 and not -1)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (function.Arguments.Count >= 4 &&
            !TryEvaluateBooleanArgument(
                function.Arguments[3],
                context,
                dependencies,
                out byColumn,
                out argumentError))
        {
            return argumentError!;
        }

        var sourceValue = source.Value!;
        if ((!byColumn &&
             (sortIndex <= 0 || sortIndex > sourceValue.ColumnCount)) ||
            (byColumn &&
             (sortIndex <= 0 || sortIndex > sourceValue.RowCount)))
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        if (!byColumn)
        {
            var rows = Enumerable.Range(0, sourceValue.RowCount)
                .OrderBy(
                    row => sourceValue[row, sortIndex - 1],
                    sortOrder == 1
                        ? CellValueSortComparer.Ascending
                        : CellValueSortComparer.Descending)
                .ThenBy(static row => row)
                .ToArray();
            return FormulaArrayEvaluationResult.Success(
                FormulaArrayValue.Create(
                    sourceValue.RowCount,
                    sourceValue.ColumnCount,
                    (row, column) => sourceValue[
                        rows[row],
                        column]),
                DistinctDependencies(dependencies));
        }

        var columns = Enumerable.Range(0, sourceValue.ColumnCount)
            .OrderBy(
                column => sourceValue[sortIndex - 1, column],
                sortOrder == 1
                    ? CellValueSortComparer.Ascending
                    : CellValueSortComparer.Descending)
            .ThenBy(static column => column)
            .ToArray();
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                sourceValue.RowCount,
                sourceValue.ColumnCount,
                (row, column) => sourceValue[
                    row,
                    columns[column]]),
            DistinctDependencies(dependencies));
    }

    private FormulaArrayEvaluationResult EvaluateUnique(
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

        var byColumn = false;
        var exactlyOnce = false;
        FormulaArrayEvaluationResult? argumentError;
        if (function.Arguments.Count >= 2 &&
            !TryEvaluateBooleanArgument(
                function.Arguments[1],
                context,
                dependencies,
                out byColumn,
                out argumentError))
        {
            return argumentError!;
        }
        if (function.Arguments.Count >= 3 &&
            !TryEvaluateBooleanArgument(
                function.Arguments[2],
                context,
                dependencies,
                out exactlyOnce,
                out argumentError))
        {
            return argumentError!;
        }

        var sourceValue = source.Value!;
        if (!byColumn)
        {
            var entries = Enumerable.Range(0, sourceValue.RowCount)
                .Select(row => new SequenceEntry(
                    row,
                    sourceValue.EnumerateRow(row).ToArray()))
                .ToArray();
            var selected = SelectUnique(entries, exactlyOnce);
            if (selected.Length == 0)
            {
                return Failure(
                    "#CALC!",
                    FormulaErrorCode.InvalidValue,
                    dependencies);
            }
            return FormulaArrayEvaluationResult.Success(
                FormulaArrayValue.Create(
                    selected.Length,
                    sourceValue.ColumnCount,
                    (row, column) => sourceValue[
                        selected[row],
                        column]),
                DistinctDependencies(dependencies));
        }

        var columnEntries = Enumerable.Range(0, sourceValue.ColumnCount)
            .Select(column => new SequenceEntry(
                column,
                Enumerable.Range(0, sourceValue.RowCount)
                    .Select(row => sourceValue[row, column])
                    .ToArray()))
            .ToArray();
        var selectedColumns = SelectUnique(
            columnEntries,
            exactlyOnce);
        if (selectedColumns.Length == 0)
        {
            return Failure(
                "#CALC!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        return FormulaArrayEvaluationResult.Success(
            FormulaArrayValue.Create(
                sourceValue.RowCount,
                selectedColumns.Length,
                (row, column) => sourceValue[
                    row,
                    selectedColumns[column]]),
            DistinctDependencies(dependencies));
    }

    private bool TryEvaluateIntegerArgument(
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
        if (!FormulaValueCoercion.TryNumber(scalar, out var number) ||
            !double.IsFinite(number) ||
            number < int.MinValue ||
            number > int.MaxValue ||
            number != Math.Truncate(number))
        {
            value = default;
            error = Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
            return false;
        }
        value = checked((int)number);
        error = null;
        return true;
    }

    private bool TryEvaluateBooleanArgument(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out bool value,
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
        if (!FormulaValueCoercion.TryBoolean(scalar, out value))
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

    private static int[] SelectUnique(
        IEnumerable<SequenceEntry> entries,
        bool exactlyOnce)
    {
        var groups = new Dictionary<
            CellValue[],
            List<int>>(CellValueSequenceComparer.Instance);
        foreach (var entry in entries)
        {
            if (!groups.TryGetValue(entry.Values, out var indexes))
            {
                indexes = [];
                groups.Add(entry.Values, indexes);
            }
            indexes.Add(entry.Index);
        }
        return groups.Values
            .Where(indexes => !exactlyOnce || indexes.Count == 1)
            .Select(static indexes => indexes[0])
            .OrderBy(static index => index)
            .ToArray();
    }

    private sealed record SequenceEntry(int Index, CellValue[] Values);

    private sealed class CellValueSequenceComparer :
        IEqualityComparer<CellValue[]>
    {
        public static CellValueSequenceComparer Instance { get; } = new();

        public bool Equals(CellValue[]? left, CellValue[]? right) =>
            ReferenceEquals(left, right) ||
            left is not null &&
            right is not null &&
            left.AsSpan().SequenceEqual(right);

        public int GetHashCode(CellValue[] values)
        {
            var hash = new HashCode();
            foreach (var value in values)
            {
                hash.Add(value);
            }
            return hash.ToHashCode();
        }
    }

    private sealed class CellValueSortComparer : IComparer<CellValue>
    {
        public static CellValueSortComparer Ascending { get; } =
            new(descending: false);

        public static CellValueSortComparer Descending { get; } =
            new(descending: true);

        private readonly bool _descending;

        private CellValueSortComparer(bool descending)
        {
            _descending = descending;
        }

        public int Compare(CellValue left, CellValue right)
        {
            var result = CompareAscending(left, right);
            return _descending ? -result : result;
        }

        private static int CompareAscending(
            CellValue left,
            CellValue right)
        {
            var leftRank = GetRank(left);
            var rightRank = GetRank(right);
            var rankComparison = leftRank.CompareTo(rightRank);
            if (rankComparison != 0)
            {
                return rankComparison;
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
                CellValueKind.Text => CompareText(
                    (string)left.RawValue!,
                    (string)right.RawValue!),
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

        private static int CompareText(string left, string right)
        {
            var comparison = string.Compare(
                left,
                right,
                StringComparison.OrdinalIgnoreCase);
            return comparison != 0
                ? comparison
                : string.CompareOrdinal(left, right);
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
