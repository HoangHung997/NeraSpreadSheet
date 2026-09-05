using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

internal static class SpreadsheetHeaderDisplayListComposer
{
    public static void DrawCorner(
        DisplayListBuilder builder,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme,
        double width,
        double height)
    {
        var selected = selection.Ranges.Any(IsWholeWorksheet);
        builder.FillRectangle(
            new RectD(0d, 0d, width, height),
            selected ? theme.HeaderActiveBackground : theme.HeaderBackground);
        DrawHeaderBorder(builder, new RectD(0d, 0d, width, height), theme);
    }

    public static void DrawColumnHeaders(
        DisplayListBuilder builder,
        ViewportLayout layout,
        RectD paneBounds,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme,
        double headerWidth,
        double headerHeight)
    {
        var frozen = layout.Columns.Where(static slot => slot.IsFrozen).ToArray();
        var scrolling = layout.Columns.Where(static slot => !slot.IsFrozen).ToArray();
        var frozenWidth = Math.Clamp(layout.FrozenWidth, 0d, paneBounds.Width);
        var bodyOffsetX = headerWidth + paneBounds.X;

        DrawColumnHeaderGroup(
            builder,
            frozen,
            new RectD(bodyOffsetX, 0d, frozenWidth, headerHeight),
            selection,
            theme,
            bodyOffsetX,
            headerHeight);
        DrawColumnHeaderGroup(
            builder,
            scrolling,
            new RectD(
                bodyOffsetX + frozenWidth,
                0d,
                Math.Max(0d, paneBounds.Width - frozenWidth),
                headerHeight),
            selection,
            theme,
            bodyOffsetX,
            headerHeight);
    }

    public static void DrawRowHeaders(
        DisplayListBuilder builder,
        ViewportLayout layout,
        RectD paneBounds,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme,
        double headerWidth,
        double headerHeight)
    {
        var frozen = layout.Rows.Where(static slot => slot.IsFrozen).ToArray();
        var scrolling = layout.Rows.Where(static slot => !slot.IsFrozen).ToArray();
        var frozenHeight = Math.Clamp(layout.FrozenHeight, 0d, paneBounds.Height);
        var bodyOffsetY = headerHeight + paneBounds.Y;

        DrawRowHeaderGroup(
            builder,
            frozen,
            new RectD(0d, bodyOffsetY, headerWidth, frozenHeight),
            selection,
            theme,
            headerWidth,
            bodyOffsetY);
        DrawRowHeaderGroup(
            builder,
            scrolling,
            new RectD(
                0d,
                bodyOffsetY + frozenHeight,
                headerWidth,
                Math.Max(0d, paneBounds.Height - frozenHeight)),
            selection,
            theme,
            headerWidth,
            bodyOffsetY);
    }

    public static void DrawFreezeHeaderSeparators(
        DisplayListBuilder builder,
        ViewportLayout layout,
        RectD paneBounds,
        SpreadsheetRenderTheme theme,
        double headerWidth,
        double headerHeight,
        bool drawColumnSeparator,
        bool drawRowSeparator)
    {
        if (drawColumnSeparator)
        {
            var frozenWidth = Math.Clamp(layout.FrozenWidth, 0d, paneBounds.Width);
            if (frozenWidth > 0d && frozenWidth < paneBounds.Width)
            {
                var x = headerWidth + paneBounds.X + frozenWidth;
                builder.DrawLine(
                    new PointD(x, 0d),
                    new PointD(x, headerHeight),
                    theme.FreezePaneStrokeWidth,
                    theme.FreezePaneLine);
            }
        }

        if (drawRowSeparator)
        {
            var frozenHeight = Math.Clamp(layout.FrozenHeight, 0d, paneBounds.Height);
            if (frozenHeight > 0d && frozenHeight < paneBounds.Height)
            {
                var y = headerHeight + paneBounds.Y + frozenHeight;
                builder.DrawLine(
                    new PointD(0d, y),
                    new PointD(headerWidth, y),
                    theme.FreezePaneStrokeWidth,
                    theme.FreezePaneLine);
            }
        }
    }

    public static void DrawSplitHeaderSeparators(
        DisplayListBuilder builder,
        SpreadsheetSplitLayout splitLayout,
        SpreadsheetRenderTheme theme,
        double headerWidth,
        double headerHeight)
    {
        if (splitLayout.HasVerticalSplit)
        {
            builder.FillRectangle(
                new RectD(
                    headerWidth + splitLayout.VerticalSeparator.X,
                    0d,
                    splitLayout.VerticalSeparator.Width,
                    headerHeight),
                theme.SplitPaneSeparator);
        }

        if (splitLayout.HasHorizontalSplit)
        {
            builder.FillRectangle(
                new RectD(
                    0d,
                    headerHeight + splitLayout.HorizontalSeparator.Y,
                    headerWidth,
                    splitLayout.HorizontalSeparator.Height),
                theme.SplitPaneSeparator);
        }
    }

    public static void ValidateTheme(SpreadsheetRenderTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (!double.IsFinite(theme.RowHeaderWidth) || theme.RowHeaderWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(theme), "RowHeaderWidth must be finite and positive.");
        }
        if (!double.IsFinite(theme.ColumnHeaderHeight) || theme.ColumnHeaderHeight <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(theme), "ColumnHeaderHeight must be finite and positive.");
        }
        if (!double.IsFinite(theme.HeaderFontSize) || theme.HeaderFontSize <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(theme), "HeaderFontSize must be finite and positive.");
        }
        if (!double.IsFinite(theme.HeaderStrokeWidth) || theme.HeaderStrokeWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(theme), "HeaderStrokeWidth must be finite and positive.");
        }
    }

    private static void DrawColumnHeaderGroup(
        DisplayListBuilder builder,
        AxisSlot[] columns,
        RectD clip,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme,
        double bodyOffsetX,
        double headerHeight)
    {
        if (columns.Length == 0 || clip.Width <= 0d || clip.Height <= 0d)
        {
            return;
        }

        builder.PushClip(clip);
        foreach (var column in columns)
        {
            var bounds = new RectD(bodyOffsetX + column.Start, 0d, column.Size, headerHeight);
            if (!bounds.IntersectsWith(clip))
            {
                continue;
            }
            DrawHeaderCell(
                builder,
                bounds,
                FormatColumnLabel(column.Index),
                GetColumnHeaderBackground(selection, column.Index, theme),
                theme);
        }
        builder.PopClip();
    }

    private static void DrawRowHeaderGroup(
        DisplayListBuilder builder,
        AxisSlot[] rows,
        RectD clip,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme,
        double headerWidth,
        double bodyOffsetY)
    {
        if (rows.Length == 0 || clip.Width <= 0d || clip.Height <= 0d)
        {
            return;
        }

        builder.PushClip(clip);
        foreach (var row in rows)
        {
            var bounds = new RectD(0d, bodyOffsetY + row.Start, headerWidth, row.Size);
            if (!bounds.IntersectsWith(clip))
            {
                continue;
            }
            DrawHeaderCell(
                builder,
                bounds,
                (row.Index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                GetRowHeaderBackground(selection, row.Index, theme),
                theme);
        }
        builder.PopClip();
    }

    private static void DrawHeaderCell(
        DisplayListBuilder builder,
        RectD bounds,
        string text,
        ColorRgba background,
        SpreadsheetRenderTheme theme)
    {
        builder.FillRectangle(bounds, background);
        DrawHeaderBorder(builder, bounds, theme);
        builder.DrawText(
            text,
            new RectD(
                bounds.X + 4d,
                bounds.Y + 1d,
                Math.Max(0d, bounds.Width - 8d),
                Math.Max(0d, bounds.Height - 2d)),
            theme.CreateHeaderTextStyle());
    }

    private static void DrawHeaderBorder(
        DisplayListBuilder builder,
        RectD bounds,
        SpreadsheetRenderTheme theme)
    {
        builder.DrawLine(
            new PointD(bounds.Right, bounds.Top),
            new PointD(bounds.Right, bounds.Bottom),
            theme.HeaderStrokeWidth,
            theme.HeaderBorder);
        builder.DrawLine(
            new PointD(bounds.Left, bounds.Bottom),
            new PointD(bounds.Right, bounds.Bottom),
            theme.HeaderStrokeWidth,
            theme.HeaderBorder);
    }

    private static ColorRgba GetRowHeaderBackground(
        SelectionSnapshot selection,
        int rowIndex,
        SpreadsheetRenderTheme theme)
    {
        if (selection.Ranges.Any(range =>
            IsWholeRowRange(range) && rowIndex >= range.Top && rowIndex <= range.Bottom))
        {
            return theme.HeaderActiveBackground;
        }
        return selection.ActiveCell.RowIndex == rowIndex
            ? theme.HeaderSelectedBackground
            : theme.HeaderBackground;
    }

    private static ColorRgba GetColumnHeaderBackground(
        SelectionSnapshot selection,
        int columnIndex,
        SpreadsheetRenderTheme theme)
    {
        if (selection.Ranges.Any(range =>
            IsWholeColumnRange(range) && columnIndex >= range.Left && columnIndex <= range.Right))
        {
            return theme.HeaderActiveBackground;
        }
        return selection.ActiveCell.ColumnIndex == columnIndex
            ? theme.HeaderSelectedBackground
            : theme.HeaderBackground;
    }

    private static bool IsWholeRowRange(CellRange range) =>
        range.Left == 0 && range.Right == SpreadsheetLimits.MaxColumns - 1;

    private static bool IsWholeColumnRange(CellRange range) =>
        range.Top == 0 && range.Bottom == SpreadsheetLimits.MaxRows - 1;

    private static bool IsWholeWorksheet(CellRange range) =>
        IsWholeRowRange(range) && IsWholeColumnRange(range);

    private static string FormatColumnLabel(int zeroBasedColumn)
    {
        var value = zeroBasedColumn + 1;
        Span<char> buffer = stackalloc char[8];
        var position = buffer.Length;
        while (value > 0)
        {
            value--;
            buffer[--position] = (char)('A' + (value % 26));
            value /= 26;
        }
        return new string(buffer[position..]);
    }
}
