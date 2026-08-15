using System.Globalization;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public static class SpreadsheetDisplayListComposer
{
    public static DisplayList Compose(WorksheetSnapshot worksheet, ViewportLayout layout, SelectionSnapshot? selection = null, SpreadsheetRenderTheme? theme = null, CellStyleCatalog? styles = null)
    {
        ArgumentNullException.ThrowIfNull(worksheet); ArgumentNullException.ThrowIfNull(layout); theme ??= new SpreadsheetRenderTheme(); ValidateTheme(theme);
        var builder = new DisplayListBuilder(); var viewport = new RectD(0d, 0d, layout.ViewportSize.Width, layout.ViewportSize.Height);
        builder.FillRectangle(viewport, theme.Background); builder.PushClip(viewport);
        foreach (var row in layout.Rows)
        foreach (var column in layout.Columns)
        {
            var bounds = new RectD(column.Start, row.Start, column.Size, row.Size); if (!bounds.IntersectsWith(viewport)) continue;
            var cell = worksheet.GetCell(new CellAddress(row.Index, column.Index)); var style = ResolveStyle(cell, styles);
            if (style.Fill.IsVisible) builder.FillRectangle(bounds, style.Fill.Color);
            if (!cell.Value.IsBlank)
            {
                var textBounds = new RectD(bounds.X + 4d, bounds.Y + 1d, Math.Max(0d, bounds.Width - 8d), Math.Max(0d, bounds.Height - 2d));
                builder.DrawText(FormatCellValue(cell.Value, style.NumberFormat.FormatCode), textBounds, new TextStyle(style.Font.Family, style.Font.Size, style.Font.Weight, style.Font.Color, style.Alignment.WrapText));
            }
            DrawCellBorders(builder, bounds, style.Border);
        }
        DrawGrid(builder, layout, viewport, theme); if (selection is not null) DrawSelection(builder, layout, selection, theme); builder.PopClip(); return builder.Build();
    }

    private static CellStyle ResolveStyle(CellData cell, CellStyleCatalog? styles) => styles is null || cell.StyleId == CellStyleCatalog.DefaultStyleId ? CellStyle.Default : styles.Get(cell.StyleId);

    private static string FormatCellValue(CellValue value, string formatCode)
    {
        if (string.Equals(formatCode, "General", StringComparison.OrdinalIgnoreCase)) return value.ToString();
        try
        {
            return value.RawValue switch
            {
                double number => number.ToString(formatCode, CultureInfo.CurrentCulture),
                DateTime dateTime => dateTime.ToString(formatCode, CultureInfo.CurrentCulture),
                _ => value.ToString(),
            };
        }
        catch (FormatException) { return value.ToString(); }
    }

    private static void DrawCellBorders(DisplayListBuilder builder, RectD bounds, CellBorderStyle border)
    {
        DrawBorder(builder, border.Top, new PointD(bounds.Left, bounds.Top), new PointD(bounds.Right, bounds.Top));
        DrawBorder(builder, border.Right, new PointD(bounds.Right, bounds.Top), new PointD(bounds.Right, bounds.Bottom));
        DrawBorder(builder, border.Bottom, new PointD(bounds.Right, bounds.Bottom), new PointD(bounds.Left, bounds.Bottom));
        DrawBorder(builder, border.Left, new PointD(bounds.Left, bounds.Bottom), new PointD(bounds.Left, bounds.Top));
    }

    private static void DrawBorder(DisplayListBuilder builder, CellBorderSide border, PointD start, PointD end)
    {
        if (border.Style == CellBorderLineStyle.None) return;
        var multiplier = border.Style switch
        {
            CellBorderLineStyle.Medium => 1.5d,
            CellBorderLineStyle.Thick or CellBorderLineStyle.DoubleLine => 2d,
            _ => 1d,
        };
        builder.DrawLine(start, end, border.Width * multiplier, border.Color);
    }

    private static void DrawGrid(DisplayListBuilder builder, ViewportLayout layout, RectD viewport, SpreadsheetRenderTheme theme)
    {
        foreach (var column in layout.Columns) { var x = column.End; if (x >= viewport.Left && x <= viewport.Right) builder.DrawLine(new PointD(x, viewport.Top), new PointD(x, viewport.Bottom), theme.GridStrokeWidth, theme.GridLine); }
        foreach (var row in layout.Rows) { var y = row.End; if (y >= viewport.Top && y <= viewport.Bottom) builder.DrawLine(new PointD(viewport.Left, y), new PointD(viewport.Right, y), theme.GridStrokeWidth, theme.GridLine); }
    }

    private static void DrawSelection(DisplayListBuilder builder, ViewportLayout layout, SelectionSnapshot selection, SpreadsheetRenderTheme theme)
    {
        foreach (var range in selection.Ranges) if (TryGetRangeBounds(layout, range, out var bounds)) DrawRectangleOutline(builder, bounds, theme.SelectionStrokeWidth, theme.Selection);
        if (TryGetRangeBounds(layout, new CellRange(selection.ActiveCell, selection.ActiveCell), out var activeBounds)) DrawRectangleOutline(builder, activeBounds, theme.SelectionStrokeWidth, theme.ActiveCell);
    }

    private static bool TryGetRangeBounds(ViewportLayout layout, CellRange range, out RectD bounds)
    {
        var firstRow = layout.Rows.FirstOrDefault(slot => slot.Index >= range.Top && slot.Index <= range.Bottom); var lastRow = layout.Rows.LastOrDefault(slot => slot.Index >= range.Top && slot.Index <= range.Bottom);
        var firstColumn = layout.Columns.FirstOrDefault(slot => slot.Index >= range.Left && slot.Index <= range.Right); var lastColumn = layout.Columns.LastOrDefault(slot => slot.Index >= range.Left && slot.Index <= range.Right);
        if (firstRow.Size <= 0d || lastRow.Size <= 0d || firstColumn.Size <= 0d || lastColumn.Size <= 0d) { bounds = RectD.Empty; return false; }
        bounds = new RectD(firstColumn.Start, firstRow.Start, lastColumn.End - firstColumn.Start, lastRow.End - firstRow.Start); return true;
    }

    private static void DrawRectangleOutline(DisplayListBuilder builder, RectD bounds, double strokeWidth, ColorRgba color)
    {
        builder.DrawLine(new PointD(bounds.Left, bounds.Top), new PointD(bounds.Right, bounds.Top), strokeWidth, color); builder.DrawLine(new PointD(bounds.Right, bounds.Top), new PointD(bounds.Right, bounds.Bottom), strokeWidth, color);
        builder.DrawLine(new PointD(bounds.Right, bounds.Bottom), new PointD(bounds.Left, bounds.Bottom), strokeWidth, color); builder.DrawLine(new PointD(bounds.Left, bounds.Bottom), new PointD(bounds.Left, bounds.Top), strokeWidth, color);
    }

    private static void ValidateTheme(SpreadsheetRenderTheme theme)
    {
        if (!double.IsFinite(theme.FontSize) || theme.FontSize <= 0d) throw new ArgumentOutOfRangeException(nameof(theme), "FontSize must be finite and positive.");
        if (!double.IsFinite(theme.SelectionStrokeWidth) || theme.SelectionStrokeWidth <= 0d) throw new ArgumentOutOfRangeException(nameof(theme), "SelectionStrokeWidth must be finite and positive.");
        if (!double.IsFinite(theme.GridStrokeWidth) || theme.GridStrokeWidth <= 0d) throw new ArgumentOutOfRangeException(nameof(theme), "GridStrokeWidth must be finite and positive.");
    }
}
