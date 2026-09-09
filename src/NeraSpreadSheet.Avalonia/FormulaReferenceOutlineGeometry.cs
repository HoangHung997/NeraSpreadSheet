using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Avalonia;

internal readonly record struct FormulaOutlineEdge(PointD Start, PointD End);
internal readonly record struct FormulaOutlineHandle(PointD Position, bool IsTopLeft);

/// <summary>Projects real reference edges through the frozen/scrolling quadrants.
/// Clipping never creates a draggable artificial edge at a viewport boundary.</summary>
internal static class FormulaReferenceOutlineGeometry
{
    public static DisplayList Compose(DisplayList body, ViewportLayout layout,
        IReadOnlyList<SpreadsheetFormulaReferenceHighlight> highlights, double width)
    {
        if (highlights.Count == 0) return body;
        var builder = new DisplayListBuilder(); builder.Append(body);
        foreach (var highlight in highlights)
            foreach (var edge in Edges(layout, highlight.Range)) builder.DrawLine(edge.Start, edge.End, width, highlight.Color);
        return builder.Build();
    }
    public static IReadOnlyList<FormulaOutlineEdge> Edges(ViewportLayout layout, CellRange range)
    {
        var result = new List<FormulaOutlineEdge>();
        for (var rowPane = 0; rowPane < 2; rowPane++)
        for (var columnPane = 0; columnPane < 2; columnPane++)
        {
            var frozenRow = rowPane == 0; var frozenColumn = columnPane == 0;
            var rows = layout.Rows.Where(slot => slot.IsFrozen == frozenRow && slot.Index >= range.Top && slot.Index <= range.Bottom).ToArray();
            var columns = layout.Columns.Where(slot => slot.IsFrozen == frozenColumn && slot.Index >= range.Left && slot.Index <= range.Right).ToArray();
            if (rows.Length == 0 || columns.Length == 0) continue;
            var clip = PaneClip(layout, frozenRow, frozenColumn);
            if (clip.Width <= 0 || clip.Height <= 0) continue;
            var top = Math.Max(clip.Top, rows[0].Start); var bottom = Math.Min(clip.Bottom, rows[^1].End);
            var left = Math.Max(clip.Left, columns[0].Start); var right = Math.Min(clip.Right, columns[^1].End);
            if (top >= bottom || left >= right) continue;
            if (rows[0].Index == range.Top && rows[0].Start >= clip.Top && rows[0].Start <= clip.Bottom)
                result.Add(new(new PointD(left, rows[0].Start), new PointD(right, rows[0].Start)));
            if (rows[^1].Index == range.Bottom && rows[^1].End >= clip.Top && rows[^1].End <= clip.Bottom)
                result.Add(new(new PointD(left, rows[^1].End), new PointD(right, rows[^1].End)));
            if (columns[0].Index == range.Left && columns[0].Start >= clip.Left && columns[0].Start <= clip.Right)
                result.Add(new(new PointD(columns[0].Start, top), new PointD(columns[0].Start, bottom)));
            if (columns[^1].Index == range.Right && columns[^1].End >= clip.Left && columns[^1].End <= clip.Right)
                result.Add(new(new PointD(columns[^1].End, top), new PointD(columns[^1].End, bottom)));
        }
        return result;
    }
    public static IReadOnlyList<FormulaOutlineHandle> Handles(ViewportLayout layout, CellRange range)
    {
        var result = new List<FormulaOutlineHandle>(2);
        Add(range.Top, range.Left, true);
        Add(range.Bottom, range.Right, false);
        return result;
        void Add(int rowIndex, int columnIndex, bool first)
        {
            var row = layout.Rows.FirstOrDefault(slot => slot.Index == rowIndex);
            var column = layout.Columns.FirstOrDefault(slot => slot.Index == columnIndex);
            if (row.Size <= 0 || column.Size <= 0) return;
            var point = new PointD(first ? column.Start : column.End, first ? row.Start : row.End);
            var clip = PaneClip(layout, row.IsFrozen, column.IsFrozen);
            if (point.X >= clip.Left && point.X <= clip.Right && point.Y >= clip.Top && point.Y <= clip.Bottom)
                result.Add(new FormulaOutlineHandle(point, first));
        }
    }
    private static RectD PaneClip(ViewportLayout layout, bool frozenRow, bool frozenColumn)
    {
        var left = frozenColumn ? 0 : layout.FrozenWidth; var top = frozenRow ? 0 : layout.FrozenHeight;
        var right = frozenColumn ? layout.FrozenWidth : layout.ViewportSize.Width;
        var bottom = frozenRow ? layout.FrozenHeight : layout.ViewportSize.Height;
        return new RectD(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
    public static double Distance(PointD point, FormulaOutlineEdge edge)
    {
        var x = Math.Clamp(point.X, Math.Min(edge.Start.X, edge.End.X), Math.Max(edge.Start.X, edge.End.X));
        var y = Math.Clamp(point.Y, Math.Min(edge.Start.Y, edge.End.Y), Math.Max(edge.Start.Y, edge.End.Y));
        return Math.Sqrt((point.X - x) * (point.X - x) + (point.Y - y) * (point.Y - y));
    }
}
