namespace NeraSpreadSheet.Core;

/// <summary>
/// Immutable rectangular formula result used by dynamic-array functions and
/// worksheet spill materialization. Values are stored in row-major order.
/// </summary>
public sealed class FormulaArrayValue : IEquatable<FormulaArrayValue>
{
    public const int MaximumCellCount = 1_000_000;

    private readonly CellValue[] _values;

    public FormulaArrayValue(
        int rowCount,
        int columnCount,
        IEnumerable<CellValue> values)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columnCount);
        ArgumentNullException.ThrowIfNull(values);
        var cellCount = checked((long)rowCount * columnCount);
        if (cellCount > MaximumCellCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(values),
                $"A formula array may contain at most " +
                $"{MaximumCellCount:N0} cells.");
        }

        _values = values.ToArray();
        if (_values.Length != cellCount)
        {
            throw new ArgumentException(
                $"The array shape requires {cellCount:N0} values, but " +
                $"{_values.Length:N0} values were supplied.",
                nameof(values));
        }

        RowCount = rowCount;
        ColumnCount = columnCount;
    }

    public int RowCount { get; }

    public int ColumnCount { get; }

    public int Count => _values.Length;

    public CellValue this[int rowIndex, int columnIndex]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                rowIndex,
                RowCount);
            ArgumentOutOfRangeException.ThrowIfNegative(columnIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
                columnIndex,
                ColumnCount);
            return _values[checked((rowIndex * ColumnCount) + columnIndex)];
        }
    }

    public static FormulaArrayValue Create(
        int rowCount,
        int columnCount,
        Func<int, int, CellValue> valueFactory)
    {
        ArgumentNullException.ThrowIfNull(valueFactory);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(rowCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(columnCount);
        var cellCount = checked((long)rowCount * columnCount);
        if (cellCount > MaximumCellCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(rowCount),
                $"A formula array may contain at most " +
                $"{MaximumCellCount:N0} cells.");
        }

        var values = new CellValue[checked((int)cellCount)];
        for (var row = 0; row < rowCount; row++)
        {
            for (var column = 0; column < columnCount; column++)
            {
                values[(row * columnCount) + column] =
                    valueFactory(row, column);
            }
        }
        return new FormulaArrayValue(rowCount, columnCount, values);
    }

    public static FormulaArrayValue FromRows(
        IEnumerable<IEnumerable<CellValue>> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var materializedRows = new List<CellValue[]>();
        foreach (var row in rows)
        {
            ArgumentNullException.ThrowIfNull(row);
            materializedRows.Add(row.ToArray());
        }
        if (materializedRows.Count == 0)
        {
            throw new ArgumentException(
                "A formula array must contain at least one row.",
                nameof(rows));
        }

        var columnCount = materializedRows[0].Length;
        if (columnCount == 0 ||
            materializedRows.Any(row => row.Length != columnCount))
        {
            throw new ArgumentException(
                "Formula-array rows must be non-empty and rectangular.",
                nameof(rows));
        }
        return new FormulaArrayValue(
            materializedRows.Count,
            columnCount,
            materializedRows.SelectMany(static row => row));
    }

    public FormulaArrayValue Transpose() =>
        Create(
            ColumnCount,
            RowCount,
            (row, column) => this[column, row]);

    public CellValue[] ToArray() => [.. _values];

    public IEnumerable<CellValue> EnumerateRow(int rowIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            rowIndex,
            RowCount);
        var start = checked(rowIndex * ColumnCount);
        for (var column = 0; column < ColumnCount; column++)
        {
            yield return _values[start + column];
        }
    }

    public bool Equals(FormulaArrayValue? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        RowCount == other.RowCount &&
        ColumnCount == other.ColumnCount &&
        _values.AsSpan().SequenceEqual(other._values);

    public override bool Equals(object? obj) =>
        obj is FormulaArrayValue other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(RowCount);
        hash.Add(ColumnCount);
        foreach (var value in _values)
        {
            hash.Add(value);
        }
        return hash.ToHashCode();
    }
}
