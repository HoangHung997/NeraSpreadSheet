namespace NeraSpreadSheet.Core;

public readonly record struct CellRange
{
    public CellRange(CellAddress first, CellAddress second)
    {
        Top = Math.Min(first.RowIndex, second.RowIndex);
        Left = Math.Min(first.ColumnIndex, second.ColumnIndex);
        Bottom = Math.Max(first.RowIndex, second.RowIndex);
        Right = Math.Max(first.ColumnIndex, second.ColumnIndex);
    }

    public int Top { get; }

    public int Left { get; }

    public int Bottom { get; }

    public int Right { get; }

    public int RowCount => checked(Bottom - Top + 1);

    public int ColumnCount => checked(Right - Left + 1);

    public CellAddress TopLeft => new(Top, Left);

    public CellAddress BottomRight => new(Bottom, Right);

    public bool Contains(CellAddress address) =>
        address.RowIndex >= Top && address.RowIndex <= Bottom &&
        address.ColumnIndex >= Left && address.ColumnIndex <= Right;

    public bool Intersects(CellRange other) =>
        other.Left <= Right && other.Right >= Left && other.Top <= Bottom && other.Bottom >= Top;

    public override string ToString() => TopLeft == BottomRight
        ? TopLeft.ToA1()
        : $"{TopLeft.ToA1()}:{BottomRight.ToA1()}";
}
