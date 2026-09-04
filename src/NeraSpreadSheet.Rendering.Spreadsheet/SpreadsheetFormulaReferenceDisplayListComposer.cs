using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public readonly record struct SpreadsheetFormulaReferenceHighlight(
    CellRange Range,
    ColorRgba Color);

/// <summary>
/// Adds bounded, visible-only outlines for formula precedent ranges while
/// retaining the nested body display list by reference.
/// </summary>
public static class SpreadsheetFormulaReferenceDisplayListComposer
{
    public static DisplayList Compose(
        DisplayList body,
        ViewportLayout layout,
        IReadOnlyList<SpreadsheetFormulaReferenceHighlight> highlights,
        double strokeWidth)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(highlights);
        if (!double.IsFinite(strokeWidth) || strokeWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(strokeWidth));
        }
        if (highlights.Count == 0)
        {
            return body;
        }

        var builder = new DisplayListBuilder();
        builder.Append(body);
        foreach (var highlight in highlights)
        {
            if (TryGetVisibleRangeBounds(
                    layout,
                    highlight.Range,
                    out var bounds))
            {
                DrawRectangleOutline(
                    builder,
                    bounds,
                    strokeWidth,
                    highlight.Color);
            }
        }
        return builder.Build();
    }

    private static bool TryGetVisibleRangeBounds(
        ViewportLayout layout,
        CellRange range,
        out RectD bounds)
    {
        var rows = layout.Rows
            .Where(slot => slot.Index >= range.Top && slot.Index <= range.Bottom)
            .ToArray();
        var columns = layout.Columns
            .Where(slot =>
                slot.Index >= range.Left && slot.Index <= range.Right)
            .ToArray();
        if (rows.Length == 0 || columns.Length == 0)
        {
            bounds = RectD.Empty;
            return false;
        }

        var firstRow = rows[0];
        var lastRow = rows[^1];
        var firstColumn = columns[0];
        var lastColumn = columns[^1];
        bounds = new RectD(
            firstColumn.Start,
            firstRow.Start,
            lastColumn.End - firstColumn.Start,
            lastRow.End - firstRow.Start);
        return bounds.Width > 0d && bounds.Height > 0d;
    }

    private static void DrawRectangleOutline(
        DisplayListBuilder builder,
        RectD bounds,
        double width,
        ColorRgba color)
    {
        var topLeft = new PointD(bounds.Left, bounds.Top);
        var topRight = new PointD(bounds.Right, bounds.Top);
        var bottomRight = new PointD(bounds.Right, bounds.Bottom);
        var bottomLeft = new PointD(bounds.Left, bounds.Bottom);
        builder.DrawLine(topLeft, topRight, width, color);
        builder.DrawLine(topRight, bottomRight, width, color);
        builder.DrawLine(bottomRight, bottomLeft, width, color);
        builder.DrawLine(bottomLeft, topLeft, width, color);
    }
}
