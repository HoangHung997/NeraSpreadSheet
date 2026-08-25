using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraDynamicArrayFormulaEngine
{
    private FormulaArrayEvaluationResult EvaluatePivotBy(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 4 or > 11)
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
        var columnFieldsResult = EvaluateNodeAsArray(
            function.Arguments[1],
            context,
            dependencies);
        if (!columnFieldsResult.IsSuccess)
        {
            return columnFieldsResult;
        }
        var valuesResult = EvaluateNodeAsArray(
            function.Arguments[2],
            context,
            dependencies);
        if (!valuesResult.IsSuccess)
        {
            return valuesResult;
        }

        var rowFields = rowFieldsResult.Value!;
        var columnFields = columnFieldsResult.Value!;
        var values = valuesResult.Value!;
        if (rowFields.RowCount != columnFields.RowCount ||
            rowFields.RowCount != values.RowCount ||
            values.ColumnCount != 1)
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryGetPivotAggregate(
                function.Arguments[3],
                out var aggregateKind))
        {
            return Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        if (!TryReadOptionalInteger(
                function,
                4,
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
                5,
                1,
                context,
                dependencies,
                out var rowTotalDepth,
                out argumentError) ||
            rowTotalDepth is < -1 or > 1)
        {
            return argumentError ?? Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryReadOptionalInteger(
                function,
                6,
                0,
                context,
                dependencies,
                out var rowSortOrder,
                out argumentError) ||
            rowSortOrder is < -1 or > 1)
        {
            return argumentError ?? Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryReadOptionalInteger(
                function,
                7,
                1,
                context,
                dependencies,
                out var columnTotalDepth,
                out argumentError) ||
            columnTotalDepth is < -1 or > 1)
        {
            return argumentError ?? Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryReadOptionalInteger(
                function,
                8,
                0,
                context,
                dependencies,
                out var columnSortOrder,
                out argumentError) ||
            columnSortOrder is < -1 or > 1)
        {
            return argumentError ?? Failure(
                "#VALUE!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }
        if (!TryReadOptionalInteger(
                function,
                10,
                0,
                context,
                dependencies,
                out var relativeTo,
                out argumentError) ||
            relativeTo is < 0 or > 4)
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
            _ => rowFields.ColumnCount > 1 ||
                 columnFields.ColumnCount > 1,
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
        if (function.Arguments.Count >= 10 &&
            function.Arguments[9] is not MissingArgumentNode)
        {
            if (!TryReadGroupByFilter(
                    function.Arguments[9],
                    rowFields.RowCount,
                    context,
                    dependencies,
                    out filter,
                    out argumentError))
            {
                return argumentError!;
            }
        }

        var includedRows = new List<int>();
        var rowGroups = new List<PivotByGroup>();
        var columnGroups = new List<PivotByGroup>();
        var rowLookup = new Dictionary<CellValue[], PivotByGroup>(
            GroupByKeyComparer.Instance);
        var columnLookup = new Dictionary<CellValue[], PivotByGroup>(
            GroupByKeyComparer.Instance);
        for (var row = dataStart; row < rowFields.RowCount; row++)
        {
            if (filter is not null && !filter[row])
            {
                continue;
            }

            var rowKey = rowFields.EnumerateRow(row).ToArray();
            var columnKey = columnFields.EnumerateRow(row).ToArray();
            var keyError = rowKey.Concat(columnKey)
                .FirstOrDefault(static cell =>
                    cell.Kind == CellValueKind.Error);
            if (keyError.Kind == CellValueKind.Error)
            {
                return FormulaArrayEvaluationResult.Failure(
                    keyError,
                    ToErrorCode(keyError),
                    DistinctDependencies(dependencies));
            }

            includedRows.Add(row);
            AddPivotByGroup(rowLookup, rowGroups, rowKey, row);
            AddPivotByGroup(
                columnLookup,
                columnGroups,
                columnKey,
                row);
        }
        if (includedRows.Count == 0)
        {
            return Failure(
                "#CALC!",
                FormulaErrorCode.InvalidValue,
                dependencies);
        }

        if (rowSortOrder != 0)
        {
            rowGroups.Sort((left, right) =>
            {
                var comparison = CellValueSortComparer.Ascending.Compare(
                    left.Key[0],
                    right.Key[0]);
                return rowSortOrder < 0 ? -comparison : comparison;
            });
        }
        if (columnSortOrder != 0)
        {
            columnGroups.Sort((left, right) =>
            {
                var comparison = CellValueSortComparer.Ascending.Compare(
                    left.Key[0],
                    right.Key[0]);
                return columnSortOrder < 0 ? -comparison : comparison;
            });
        }

        var headerRows = showHeaders
            ? Math.Max(1, columnFields.ColumnCount)
            : 0;
        var outputRows = checked(
            headerRows +
            rowGroups.Count +
            (rowTotalDepth == 0 ? 0 : 1));
        var outputColumns = checked(
            rowFields.ColumnCount +
            columnGroups.Count +
            (columnTotalDepth == 0 ? 0 : 1));
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
            for (var level = 0; level < headerRows; level++)
            {
                var header = new CellValue[outputColumns];
                if (level == headerRows - 1)
                {
                    for (var column = 0;
                         column < rowFields.ColumnCount;
                         column++)
                    {
                        header[column] = hasInputHeaders
                            ? rowFields[0, column]
                            : CellValue.FromText(
                                $"Row {column + 1}");
                    }
                }
                for (var group = 0;
                     group < columnGroups.Count;
                     group++)
                {
                    var keyLevel = Math.Min(
                        level,
                        columnGroups[group].Key.Length - 1);
                    header[rowFields.ColumnCount + group] =
                        columnGroups[group].Key[keyLevel];
                }
                if (columnTotalDepth != 0 &&
                    level == headerRows - 1)
                {
                    header[^1] = CellValue.FromText("Grand Total");
                }
                output.Add(header);
            }
        }

        var totalRow = CreatePivotByOutputRow(
            rowKey: CreateTotalKey(rowFields.ColumnCount),
            rowRows: includedRows,
            columnGroups,
            includedRows,
            values,
            aggregateKind,
            relativeTo,
            columnTotalDepth != 0);
        if (rowTotalDepth < 0)
        {
            output.Add(totalRow);
        }
        foreach (var rowGroup in rowGroups)
        {
            output.Add(CreatePivotByOutputRow(
                rowGroup.Key,
                rowGroup.Rows,
                columnGroups,
                includedRows,
                values,
                aggregateKind,
                relativeTo,
                columnTotalDepth != 0));
        }
        if (rowTotalDepth > 0)
        {
            output.Add(totalRow);
        }

        return FormulaArrayEvaluationResult.Success(
            new FormulaArrayValue(
                output.Count,
                outputColumns,
                output.SelectMany(static row => row)),
            DistinctDependencies(dependencies));
    }

    private static void AddPivotByGroup(
        Dictionary<CellValue[], PivotByGroup> lookup,
        List<PivotByGroup> groups,
        CellValue[] key,
        int row)
    {
        if (!lookup.TryGetValue(key, out var group))
        {
            group = new PivotByGroup(key);
            lookup.Add(key, group);
            groups.Add(group);
        }
        group.Rows.Add(row);
    }

    private static CellValue[] CreatePivotByOutputRow(
        CellValue[] rowKey,
        IReadOnlyList<int> rowRows,
        IReadOnlyList<PivotByGroup> columnGroups,
        IReadOnlyList<int> allRows,
        FormulaArrayValue values,
        PivotByAggregateKind aggregateKind,
        int relativeTo,
        bool includeRowTotal)
    {
        var output = new CellValue[
            checked(rowKey.Length +
                    columnGroups.Count +
                    (includeRowTotal ? 1 : 0))];
        Array.Copy(rowKey, output, rowKey.Length);
        for (var column = 0;
             column < columnGroups.Count;
             column++)
        {
            var subsetRows = IntersectRows(
                rowRows,
                columnGroups[column].Rows);
            output[rowKey.Length + column] = AggregatePivotBy(
                values,
                subsetRows,
                rowRows,
                columnGroups[column].Rows,
                allRows,
                aggregateKind,
                relativeTo);
        }
        if (includeRowTotal)
        {
            output[^1] = AggregatePivotBy(
                values,
                rowRows,
                rowRows,
                allRows,
                allRows,
                aggregateKind,
                relativeTo);
        }
        return output;
    }

    private static CellValue AggregatePivotBy(
        FormulaArrayValue values,
        IReadOnlyList<int> subsetRows,
        IReadOnlyList<int> rowRows,
        IReadOnlyList<int> columnRows,
        IReadOnlyList<int> allRows,
        PivotByAggregateKind aggregateKind,
        int relativeTo)
    {
        if (aggregateKind != PivotByAggregateKind.PercentOf)
        {
            return AggregateGroupByColumn(
                values,
                subsetRows,
                0,
                (GroupByAggregateKind)aggregateKind);
        }

        var denominatorRows = relativeTo switch
        {
            0 or 3 => columnRows,
            1 or 4 => rowRows,
            _ => allRows,
        };
        var numerator = SumPivotByValues(values, subsetRows, out var error);
        if (error.Kind == CellValueKind.Error)
        {
            return error;
        }
        var denominator = SumPivotByValues(
            values,
            denominatorRows,
            out error);
        if (error.Kind == CellValueKind.Error)
        {
            return error;
        }
        if (Math.Abs(denominator) <= double.Epsilon)
        {
            return CellValue.FromError("#DIV/0!");
        }
        return FormulaValueCoercion.SafeNumber(
            numerator / denominator);
    }

    private static double SumPivotByValues(
        FormulaArrayValue values,
        IReadOnlyList<int> rows,
        out CellValue error)
    {
        var sum = 0d;
        foreach (var row in rows)
        {
            var cell = values[row, 0];
            if (cell.Kind == CellValueKind.Error)
            {
                error = cell;
                return 0d;
            }
            if (cell.Kind != CellValueKind.Number)
            {
                continue;
            }
            sum += (double)cell.RawValue!;
            if (!double.IsFinite(sum))
            {
                error = CellValue.FromError("#NUM!");
                return 0d;
            }
        }
        error = default;
        return sum;
    }

    private static int[] IntersectRows(
        IReadOnlyList<int> left,
        IReadOnlyList<int> right)
    {
        var rightSet = new HashSet<int>(right);
        return left.Where(rightSet.Contains).ToArray();
    }

    private static bool TryGetPivotAggregate(
        FormulaNode node,
        out PivotByAggregateKind kind)
    {
        string? name = node switch
        {
            NameNode formulaName => formulaName.Name,
            ConstantNode constant
                when constant.Value.Kind == CellValueKind.Text =>
                (string)constant.Value.RawValue!,
            FunctionNode formula when formula.Arguments.Count == 0 =>
                formula.Name,
            _ => null,
        };
        kind = name?.Trim().ToUpperInvariant() switch
        {
            "SUM" => PivotByAggregateKind.Sum,
            "AVERAGE" => PivotByAggregateKind.Average,
            "COUNT" => PivotByAggregateKind.Count,
            "COUNTA" => PivotByAggregateKind.CountA,
            "MAX" => PivotByAggregateKind.Maximum,
            "MIN" => PivotByAggregateKind.Minimum,
            "PERCENTOF" => PivotByAggregateKind.PercentOf,
            _ => default,
        };
        return name is not null &&
               name.Trim().ToUpperInvariant() is
                   "SUM" or "AVERAGE" or "COUNT" or "COUNTA" or
                   "MAX" or "MIN" or "PERCENTOF";
    }

    private enum PivotByAggregateKind
    {
        Sum = 0,
        Average,
        Count,
        CountA,
        Maximum,
        Minimum,
        PercentOf,
    }

    private sealed class PivotByGroup
    {
        public PivotByGroup(CellValue[] key)
        {
            Key = key;
        }

        public CellValue[] Key { get; }

        public List<int> Rows { get; } = [];
    }
}
