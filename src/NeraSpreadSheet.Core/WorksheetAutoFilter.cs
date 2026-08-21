namespace NeraSpreadSheet.Core;

public sealed class WorksheetAutoFilterColumn : IEquatable<WorksheetAutoFilterColumn>
{
    private readonly TableFilterColumn _criteria;

    public WorksheetAutoFilterColumn(
        int columnOffset,
        IEnumerable<CellValue>? values = null,
        bool includeBlank = false,
        TableFilterCondition? firstCondition = null,
        TableFilterCondition? secondCondition = null,
        bool combineWithAnd = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnOffset);
        ColumnOffset = columnOffset;
        _criteria = new TableFilterColumn(
            CreateSyntheticColumnId(columnOffset),
            values,
            includeBlank,
            firstCondition,
            secondCondition,
            combineWithAnd);
    }

    public int ColumnOffset { get; }

    public IReadOnlyList<CellValue> Values => _criteria.Values;

    public bool IncludeBlank => _criteria.IncludeBlank;

    public TableFilterCondition? FirstCondition =>
        _criteria.FirstCondition;

    public TableFilterCondition? SecondCondition =>
        _criteria.SecondCondition;

    public bool CombineWithAnd => _criteria.CombineWithAnd;

    public bool Matches(CellValue value) =>
        _criteria.Matches(value);

    public WorksheetAutoFilterColumn Copy() => new(
        ColumnOffset,
        Values,
        IncludeBlank,
        FirstCondition,
        SecondCondition,
        CombineWithAnd);

    public bool Equals(WorksheetAutoFilterColumn? other) =>
        other is not null &&
        ColumnOffset == other.ColumnOffset &&
        IncludeBlank == other.IncludeBlank &&
        CombineWithAnd == other.CombineWithAnd &&
        Equals(FirstCondition, other.FirstCondition) &&
        Equals(SecondCondition, other.SecondCondition) &&
        Values.SequenceEqual(other.Values);

    public override bool Equals(object? obj) =>
        Equals(obj as WorksheetAutoFilterColumn);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(ColumnOffset);
        hash.Add(IncludeBlank);
        hash.Add(CombineWithAnd);
        hash.Add(FirstCondition);
        hash.Add(SecondCondition);
        foreach (var value in Values)
        {
            hash.Add(value);
        }
        return hash.ToHashCode();
    }

    private static Guid CreateSyntheticColumnId(int columnOffset) =>
        new(
            columnOffset + 1,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1);
}

public sealed class WorksheetAutoFilter : IEquatable<WorksheetAutoFilter>
{
    private readonly WorksheetAutoFilterColumn[] _columns;

    public WorksheetAutoFilter(
        CellRange range,
        IEnumerable<WorksheetAutoFilterColumn>? columns = null,
        bool hasHeaderRow = true)
    {
        Range = range;
        HasHeaderRow = hasHeaderRow;
        _columns = columns?
            .Select(static column =>
                (column ?? throw new ArgumentException(
                    "A worksheet filter cannot contain a null column.",
                    nameof(columns))).Copy())
            .OrderBy(static column => column.ColumnOffset)
            .ToArray() ?? [];
        if (_columns.Select(static column => column.ColumnOffset)
            .Distinct()
            .Count() != _columns.Length)
        {
            throw new ArgumentException(
                "A worksheet filter cannot contain duplicate column offsets.",
                nameof(columns));
        }
        if (_columns.Any(column =>
                column.ColumnOffset >= range.ColumnCount))
        {
            throw new ArgumentException(
                "A worksheet filter column must be inside the filter range.",
                nameof(columns));
        }
    }

    public CellRange Range { get; }

    public bool HasHeaderRow { get; }

    public IReadOnlyList<WorksheetAutoFilterColumn> Columns =>
        _columns;

    public CellRange? DataRange
    {
        get
        {
            var top = Range.Top + (HasHeaderRow ? 1 : 0);
            return top <= Range.Bottom
                ? new CellRange(
                    new CellAddress(top, Range.Left),
                    Range.BottomRight)
                : null;
        }
    }

    public bool IsRowVisible(
        WorksheetSnapshot worksheet,
        int rowIndex)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        if (DataRange is not { } dataRange ||
            rowIndex < dataRange.Top ||
            rowIndex > dataRange.Bottom ||
            _columns.Length == 0)
        {
            return true;
        }

        foreach (var column in _columns)
        {
            var value = worksheet.GetCell(new CellAddress(
                rowIndex,
                Range.Left + column.ColumnOffset)).Value;
            if (!column.Matches(value))
            {
                return false;
            }
        }

        return true;
    }

    public WorksheetAutoFilter Copy() => new(
        Range,
        _columns,
        HasHeaderRow);

    public WorksheetAutoFilter WithColumns(
        IEnumerable<WorksheetAutoFilterColumn> columns) =>
        new(
            Range,
            columns,
            HasHeaderRow);

    public CellRange ExpandSignalRange(CellRange source) =>
        Range.Intersects(source)
            ? Union(source, Range)
            : source;

    public bool Equals(WorksheetAutoFilter? other) =>
        other is not null &&
        Range == other.Range &&
        HasHeaderRow == other.HasHeaderRow &&
        _columns.SequenceEqual(other._columns);

    public override bool Equals(object? obj) =>
        Equals(obj as WorksheetAutoFilter);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Range);
        hash.Add(HasHeaderRow);
        foreach (var column in _columns)
        {
            hash.Add(column);
        }
        return hash.ToHashCode();
    }

    internal WorksheetAutoFilter? CreateStructuralFilter(
        WorksheetStructuralChange change)
    {
        ValidateHeaderDeletion(change);
        if (!change.TryMapRange(Range, out var mappedRange))
        {
            return null;
        }
        if (change.Axis == WorksheetAxis.Row)
        {
            return new WorksheetAutoFilter(
                mappedRange,
                _columns,
                HasHeaderRow);
        }

        var mappedColumns =
            new List<WorksheetAutoFilterColumn>(_columns.Length);
        foreach (var column in _columns)
        {
            var sourceAddress = new CellAddress(
                Range.Top,
                Range.Left + column.ColumnOffset);
            if (!change.TryMapAddress(
                    sourceAddress,
                    out var mappedAddress))
            {
                continue;
            }

            var mappedOffset =
                mappedAddress.ColumnIndex - mappedRange.Left;
            if (mappedOffset < 0 ||
                mappedOffset >= mappedRange.ColumnCount)
            {
                throw new InvalidOperationException(
                    "A worksheet filter column moved outside its filter range.");
            }

            mappedColumns.Add(new WorksheetAutoFilterColumn(
                mappedOffset,
                column.Values,
                column.IncludeBlank,
                column.FirstCondition,
                column.SecondCondition,
                column.CombineWithAnd));
        }

        return new WorksheetAutoFilter(
            mappedRange,
            mappedColumns,
            HasHeaderRow);
    }

    internal WorksheetAutoFilter CreateAxisMoveFilter(
        WorksheetAxisMove move)
    {
        if (!move.TryMapUniformRange(Range, out var mappedRange))
        {
            throw new InvalidOperationException(
                "Cannot reorder because the worksheet AutoFilter range " +
                "would not remain one uniform translation.");
        }
        if (move.Axis == WorksheetAxis.Row)
        {
            return new WorksheetAutoFilter(
                mappedRange,
                _columns,
                HasHeaderRow);
        }

        var mappedColumns =
            new List<WorksheetAutoFilterColumn>(_columns.Length);
        foreach (var column in _columns)
        {
            var mappedAddress = move.MapAddress(new CellAddress(
                Range.Top,
                Range.Left + column.ColumnOffset));
            var mappedOffset =
                mappedAddress.ColumnIndex - mappedRange.Left;
            if (mappedOffset < 0 ||
                mappedOffset >= mappedRange.ColumnCount)
            {
                throw new InvalidOperationException(
                    "A worksheet filter column moved outside its filter range.");
            }

            mappedColumns.Add(new WorksheetAutoFilterColumn(
                mappedOffset,
                column.Values,
                column.IncludeBlank,
                column.FirstCondition,
                column.SecondCondition,
                column.CombineWithAnd));
        }

        return new WorksheetAutoFilter(
            mappedRange,
            mappedColumns,
            HasHeaderRow);
    }

    private void ValidateHeaderDeletion(
        WorksheetStructuralChange change)
    {
        if (!HasHeaderRow ||
            change.Axis != WorksheetAxis.Row ||
            change.Kind != WorksheetStructuralChangeKind.Delete)
        {
            return;
        }

        var deletesEntireFilter =
            change.Index <= Range.Top &&
            change.EndIndex >= Range.Bottom;
        if (!deletesEntireFilter &&
            change.Index <= Range.Top &&
            change.EndIndex >= Range.Top)
        {
            throw new InvalidOperationException(
                "Cannot delete the header row of a worksheet AutoFilter " +
                "without deleting the entire filter range.");
        }
    }

    private static CellRange Union(
        CellRange left,
        CellRange right) =>
        new(
            new CellAddress(
                Math.Min(left.Top, right.Top),
                Math.Min(left.Left, right.Left)),
            new CellAddress(
                Math.Max(left.Bottom, right.Bottom),
                Math.Max(left.Right, right.Right)));
}
