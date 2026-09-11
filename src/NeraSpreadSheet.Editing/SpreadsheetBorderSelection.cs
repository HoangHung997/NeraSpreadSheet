using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>
/// Describes Excel-style border edges for a finite selection. The same line style is
/// projected to selection perimeter/interior edges without inventing per-cell outline semantics.
/// </summary>
public sealed record SpreadsheetBorderSelection
{
    public CellBorderSide Side { get; init; } = new()
    {
        Style = CellBorderLineStyle.Thin,
        Width = 1d,
    };

    public bool ClearAll { get; init; }
    public bool Top { get; init; }
    public bool Bottom { get; init; }
    public bool Left { get; init; }
    public bool Right { get; init; }
    public bool InsideHorizontal { get; init; }
    public bool InsideVertical { get; init; }
    public bool DiagonalUp { get; init; }
    public bool DiagonalDown { get; init; }

    public bool HasChanges =>
        ClearAll || Top || Bottom || Left || Right ||
        InsideHorizontal || InsideVertical || DiagonalUp || DiagonalDown;

    internal CellStyle Apply(CellAddress address, IReadOnlyList<CellRange> ranges, CellStyle source)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        ArgumentNullException.ThrowIfNull(source);
        var range = ranges.FirstOrDefault(candidate => candidate.Contains(address));
        if (!range.Contains(address))
        {
            return source;
        }

        if (ClearAll)
        {
            return source with { Border = new CellBorderStyle() };
        }

        var border = source.Border;
        var left = border.Left;
        var top = border.Top;
        var right = border.Right;
        var bottom = border.Bottom;
        var diagonal = border.Diagonal;
        var diagonalUp = border.DiagonalUp;
        var diagonalDown = border.DiagonalDown;

        if (Left && address.ColumnIndex == range.Left) left = Side;
        if (Top && address.RowIndex == range.Top) top = Side;
        if (Right && address.ColumnIndex == range.Right) right = Side;
        if (Bottom && address.RowIndex == range.Bottom) bottom = Side;

        // Store interior separators once (right/bottom side of the leading cell). This
        // avoids double-thick shared edges while preserving the selection topology.
        if (InsideVertical && address.ColumnIndex < range.Right) right = Side;
        if (InsideHorizontal && address.RowIndex < range.Bottom) bottom = Side;

        if (DiagonalUp || DiagonalDown)
        {
            diagonal = Side;
            diagonalUp = DiagonalUp;
            diagonalDown = DiagonalDown;
        }

        return source with
        {
            Border = new CellBorderStyle
            {
                Left = left,
                Top = top,
                Right = right,
                Bottom = bottom,
                Diagonal = diagonal,
                DiagonalUp = diagonalUp,
                DiagonalDown = diagonalDown,
            },
        };
    }

    public static SpreadsheetBorderSelection None() => new() { ClearAll = true };

    public static SpreadsheetBorderSelection Outline(CellBorderSide side) => new()
    {
        Side = side,
        Top = true,
        Bottom = true,
        Left = true,
        Right = true,
    };

    public static SpreadsheetBorderSelection Inside(CellBorderSide side) => new()
    {
        Side = side,
        InsideHorizontal = true,
        InsideVertical = true,
    };

    public static SpreadsheetBorderSelection All(CellBorderSide side) => new()
    {
        Side = side,
        Top = true,
        Bottom = true,
        Left = true,
        Right = true,
        InsideHorizontal = true,
        InsideVertical = true,
    };

    public static SpreadsheetBorderSelection BottomEdge(CellBorderSide side) => new()
    {
        Side = side,
        Bottom = true,
    };
}