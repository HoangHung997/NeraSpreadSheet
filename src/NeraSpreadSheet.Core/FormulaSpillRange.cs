namespace NeraSpreadSheet.Core;

public enum FormulaSpillApplyStatus
{
    Applied,
    InvalidOwner,
    OutOfBounds,
    Blocked,
}

public sealed record FormulaSpillApplyResult(
    FormulaSpillApplyStatus Status,
    FormulaSpillRange? Spill = null,
    CellAddress? BlockingAddress = null)
{
    public bool IsApplied => Status == FormulaSpillApplyStatus.Applied;
}

/// <summary>
/// Immutable ownership metadata for one materialized dynamic-array result.
/// The owner is always the top-left cell of the spill range.
/// </summary>
public sealed class FormulaSpillRange : IEquatable<FormulaSpillRange>
{
    public FormulaSpillRange(
        CellAddress owner,
        CellRange range,
        FormulaArrayValue values)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (range.TopLeft != owner)
        {
            throw new ArgumentException(
                "A spill owner must be the top-left cell of its range.",
                nameof(owner));
        }
        if (range.RowCount != values.RowCount ||
            range.ColumnCount != values.ColumnCount)
        {
            throw new ArgumentException(
                "The spill range and array shape must match.",
                nameof(range));
        }

        Owner = owner;
        Range = range;
        Values = values;
    }

    public CellAddress Owner { get; }

    public CellRange Range { get; }

    public FormulaArrayValue Values { get; }

    public int RowCount => Values.RowCount;

    public int ColumnCount => Values.ColumnCount;

    public bool IsChild(CellAddress address) =>
        address != Owner && Range.Contains(address);

    public CellValue GetValue(CellAddress address)
    {
        if (!Range.Contains(address))
        {
            throw new ArgumentOutOfRangeException(
                nameof(address),
                address,
                "The address is outside the spill range.");
        }
        return Values[
            address.RowIndex - Range.Top,
            address.ColumnIndex - Range.Left];
    }

    public IEnumerable<KeyValuePair<CellAddress, CellValue>>
        EnumerateValues()
    {
        for (var row = 0; row < RowCount; row++)
        {
            for (var column = 0; column < ColumnCount; column++)
            {
                yield return new KeyValuePair<CellAddress, CellValue>(
                    new CellAddress(
                        Range.Top + row,
                        Range.Left + column),
                    Values[row, column]);
            }
        }
    }

    public FormulaSpillRange Copy() =>
        new(Owner, Range, Values);

    public bool Equals(FormulaSpillRange? other) =>
        ReferenceEquals(this, other) ||
        other is not null &&
        Owner == other.Owner &&
        Range == other.Range &&
        Values.Equals(other.Values);

    public override bool Equals(object? obj) =>
        obj is FormulaSpillRange other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine(Owner, Range, Values);
}
