using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public static class SpreadsheetChromeDisplayListComposer
{
    public static DisplayList Compose(
        DisplayList body,
        ViewportLayout layout,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(theme);

        if (!theme.ShowHeaders)
        {
            return body;
        }
        ValidateTheme(theme);

        var headerWidth = theme.RowHeaderWidth;
        var headerHeight = theme.ColumnHeaderHeight;
        var bodyWidth = layout.ViewportSize.Width;
        var bodyHeight = layout.ViewportSize.Height;
        var fullWidth = headerWidth + bodyWidth;
        var fullHeight = headerHeight + bodyHeight;
        var builder = new DisplayListBuilder();

        builder.FillRectangle(new RectD(0d, 0d, fullWidth, fullHeight), theme.Background);
        builder.PushClip(new RectD(headerWidth, headerHeight, bodyWidth, bodyHeight));
        builder.PushTranslation(headerWidth, headerHeight);
        builder.Append(body);
        builder.PopTranslation();
        builder.PopClip();

        DrawCorner(builder, selection, theme, headerWidth, headerHeight);
        DrawColumnHeaders(builder, layout, selection, theme, headerWidth, headerHeight, fullWidth);
        DrawRowHeaders(builder, layout, selection, theme, headerWidth, headerHeight, fullHeight);
        DrawFreezeHeaderSeparators(builder, layout, theme, headerWidth, headerHeight);
        return builder.Build();
    }

    private static void DrawCorner(
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

    private static void DrawColumnHeaders(
        DisplayListBuilder builder,
        ViewportLayout layout,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme,
        double headerWidth,
        double headerHeight,
        double fullWidth)
    {
        var frozen = layout.Columns.Where(static slot => slot.IsFrozen).ToArray();
        var scrolling = layout.Columns.Where(static slot => !slot.IsFrozen).ToArray();
        var frozenWidth = Math.Clamp(layout.FrozenWidth, 0d, layout.ViewportSize.Width);

        DrawColumnHeaderGroup(
            builder,
            frozen,
            new RectD(headerWidth, 0d, frozenWidth, headerHeight),
            selection,
            theme,
            headerWidth,
            headerHeight);
        DrawColumnHeaderGroup(
            builder,
            scrolling,
            new RectD(
                headerWidth + frozenWidth,
                0d,
                Math.Max(0d, fullWidth - headerWidth - frozenWidth),
                headerHeight),
            selection,
            theme,
            headerWidth,
            headerHeight);
    }

    private static void DrawColumnHeaderGroup(
        DisplayListBuilder builder,
        AxisSlot[] columns,
        RectD clip,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme,
        double headerWidth,
        double headerHeight)
    {
        if (columns.Length == 0 || clip.Width <= 0d || clip.Height <= 0d)
        {
            return;
        }

        builder.PushClip(clip);
        foreach (var column in columns)
        {
            var bounds = new RectD(headerWidth + column.Start, 0d, column.Size, headerHeight);
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

    private static void DrawRowHeaders(
        DisplayListBuilder builder,
        ViewportLayout layout,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme,
        double headerWidth,
        double headerHeight,
        double fullHeight)
    {
        var frozen = layout.Rows.Where(static slot => slot.IsFrozen).ToArray();
        var scrolling = layout.Rows.Where(static slot => !slot.IsFrozen).ToArray();
        var frozenHeight = Math.Clamp(layout.FrozenHeight, 0d, layout.ViewportSize.Height);

        DrawRowHeaderGroup(
            builder,
            frozen,
            new RectD(0d, headerHeight, headerWidth, frozenHeight),
            selection,
            theme,
            headerWidth,
            headerHeight);
        DrawRowHeaderGroup(
            builder,
            scrolling,
            new RectD(
                0d,
                headerHeight + frozenHeight,
                headerWidth,
                Math.Max(0d, fullHeight - headerHeight - frozenHeight)),
            selection,
            theme,
            headerWidth,
            headerHeight);
    }

    private static void DrawRowHeaderGroup(
        DisplayListBuilder builder,
        AxisSlot[] rows,
        RectD clip,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme,
        double headerWidth,
        double headerHeight)
    {
        if (rows.Length == 0 || clip.Width <= 0d || clip.Height <= 0d)
        {
            return;
        }

        builder.PushClip(clip);
        foreach (var row in rows)
        {
            var bounds = new RectD(0d, headerHeight + row.Start, headerWidth, row.Size);
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

    private static void DrawHeaderBorder(DisplayListBuilder builder, RectD bounds, SpreadsheetRenderTheme theme)
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

    private static void DrawFreezeHeaderSeparators(
        DisplayListBuilder builder,
        ViewportLayout layout,
        SpreadsheetRenderTheme theme,
        double headerWidth,
        double headerHeight)
    {
        var frozenWidth = Math.Clamp(layout.FrozenWidth, 0d, layout.ViewportSize.Width);
        if (frozenWidth > 0d && frozenWidth < layout.ViewportSize.Width)
        {
            var x = headerWidth + frozenWidth;
            builder.DrawLine(
                new PointD(x, 0d),
                new PointD(x, headerHeight),
                theme.FreezePaneStrokeWidth,
                theme.FreezePaneLine);
        }

        var frozenHeight = Math.Clamp(layout.FrozenHeight, 0d, layout.ViewportSize.Height);
        if (frozenHeight > 0d && frozenHeight < layout.ViewportSize.Height)
        {
            var y = headerHeight + frozenHeight;
            builder.DrawLine(
                new PointD(0d, y),
                new PointD(headerWidth, y),
                theme.FreezePaneStrokeWidth,
                theme.FreezePaneLine);
        }
    }

    private static ColorRgba GetRowHeaderBackground(
        SelectionSnapshot selection,
        int rowIndex,
        SpreadsheetRenderTheme theme)
    {
        if (selection.Ranges.Any(range => IsWholeRowRange(range) && rowIndex >= range.Top && rowIndex <= range.Bottom))
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
        if (selection.Ranges.Any(range => IsWholeColumnRange(range) && columnIndex >= range.Left && columnIndex <= range.Right))
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

    private static void ValidateTheme(SpreadsheetRenderTheme theme)
    {
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
}
