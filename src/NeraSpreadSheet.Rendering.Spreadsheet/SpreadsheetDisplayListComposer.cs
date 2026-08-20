using System.Globalization;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Formulas;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public static class SpreadsheetDisplayListComposer
{
    public static DisplayList Compose(
        WorksheetSnapshot worksheet,
        ViewportLayout layout,
        SelectionSnapshot? selection = null,
        SpreadsheetRenderTheme? theme = null,
        CellStyleCatalog? styles = null,
        bool includeFreezeSeparators = true)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(layout);
        theme ??= new SpreadsheetRenderTheme();
        ValidateTheme(theme);

        var builder = new DisplayListBuilder();
        var viewport = new RectD(
            0d,
            0d,
            layout.ViewportSize.Width,
            layout.ViewportSize.Height);
        builder.FillRectangle(viewport, theme.Background);
        builder.PushClip(viewport);

        var frozenRows = layout.Rows
            .Where(static slot => slot.IsFrozen)
            .ToArray();
        var scrollingRows = layout.Rows
            .Where(static slot => !slot.IsFrozen)
            .ToArray();
        var frozenColumns = layout.Columns
            .Where(static slot => slot.IsFrozen)
            .ToArray();
        var scrollingColumns = layout.Columns
            .Where(static slot => !slot.IsFrozen)
            .ToArray();
        var frozenWidth = Math.Clamp(
            layout.FrozenWidth,
            0d,
            viewport.Width);
        var frozenHeight = Math.Clamp(
            layout.FrozenHeight,
            0d,
            viewport.Height);

        DrawPane(
            builder,
            worksheet,
            frozenRows,
            frozenColumns,
            new RectD(
                0d,
                0d,
                frozenWidth,
                frozenHeight),
            selection,
            theme,
            styles);
        DrawPane(
            builder,
            worksheet,
            frozenRows,
            scrollingColumns,
            new RectD(
                frozenWidth,
                0d,
                Math.Max(
                    0d,
                    viewport.Width - frozenWidth),
                frozenHeight),
            selection,
            theme,
            styles);
        DrawPane(
            builder,
            worksheet,
            scrollingRows,
            frozenColumns,
            new RectD(
                0d,
                frozenHeight,
                frozenWidth,
                Math.Max(
                    0d,
                    viewport.Height - frozenHeight)),
            selection,
            theme,
            styles);
        DrawPane(
            builder,
            worksheet,
            scrollingRows,
            scrollingColumns,
            new RectD(
                frozenWidth,
                frozenHeight,
                Math.Max(
                    0d,
                    viewport.Width - frozenWidth),
                Math.Max(
                    0d,
                    viewport.Height - frozenHeight)),
            selection,
            theme,
            styles);

        if (includeFreezeSeparators)
        {
            AppendFreezeSeparators(
                builder,
                layout,
                theme);
        }

        builder.PopClip();
        return builder.Build();
    }

    public static void AppendFreezeSeparators(
        DisplayListBuilder builder,
        ViewportLayout layout,
        SpreadsheetRenderTheme theme)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(layout);
        ArgumentNullException.ThrowIfNull(theme);
        var viewport = new RectD(
            0d,
            0d,
            layout.ViewportSize.Width,
            layout.ViewportSize.Height);
        DrawFreezeSeparators(
            builder,
            viewport,
            Math.Clamp(
                layout.FrozenWidth,
                0d,
                viewport.Width),
            Math.Clamp(
                layout.FrozenHeight,
                0d,
                viewport.Height),
            theme);
    }

    private static void DrawPane(
        DisplayListBuilder builder,
        WorksheetSnapshot worksheet,
        AxisSlot[] rows,
        AxisSlot[] columns,
        RectD pane,
        SelectionSnapshot? selection,
        SpreadsheetRenderTheme theme,
        CellStyleCatalog? styles)
    {
        if (rows.Length == 0 ||
            columns.Length == 0 ||
            pane.Width <= 0d ||
            pane.Height <= 0d)
        {
            return;
        }

        builder.PushClip(pane);
        DrawUnmergedCells(
            builder,
            worksheet,
            rows,
            columns,
            pane,
            styles);
        DrawGrid(
            builder,
            rows,
            columns,
            pane,
            theme);
        DrawMergedCells(
            builder,
            worksheet,
            rows,
            columns,
            pane,
            theme,
            styles);
        if (selection is not null)
        {
            DrawSelection(
                builder,
                rows,
                columns,
                worksheet,
                selection,
                theme);
        }

        builder.PopClip();
    }

    private static void DrawUnmergedCells(
        DisplayListBuilder builder,
        WorksheetSnapshot worksheet,
        AxisSlot[] rows,
        AxisSlot[] columns,
        RectD pane,
        CellStyleCatalog? styles)
    {
        foreach (var row in rows)
        {
            foreach (var column in columns)
            {
                var address = new CellAddress(
                    row.Index,
                    column.Index);
                if (worksheet.TryGetMergedRange(
                        address,
                        out _))
                {
                    continue;
                }

                var bounds = new RectD(
                    column.Start,
                    row.Start,
                    column.Size,
                    row.Size);
                if (!bounds.IntersectsWith(pane))
                {
                    continue;
                }

                DrawCell(
                    builder,
                    worksheet,
                    address,
                    worksheet.GetCell(address),
                    bounds,
                    styles,
                    backgroundFallback: null);
            }
        }
    }

    private static void DrawMergedCells(
        DisplayListBuilder builder,
        WorksheetSnapshot worksheet,
        AxisSlot[] rows,
        AxisSlot[] columns,
        RectD pane,
        SpreadsheetRenderTheme theme,
        CellStyleCatalog? styles)
    {
        foreach (var range in worksheet.MergedCells)
        {
            if (!TryGetRangeBounds(
                    rows,
                    columns,
                    range,
                    out var bounds) ||
                !bounds.IntersectsWith(pane))
            {
                continue;
            }

            DrawCell(
                builder,
                worksheet,
                range.TopLeft,
                worksheet.GetCell(range.TopLeft),
                bounds,
                styles,
                theme.Background);
        }
    }

    private static void DrawCell(
        DisplayListBuilder builder,
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellData cell,
        RectD bounds,
        CellStyleCatalog? styles,
        ColorRgba? backgroundFallback)
    {
        var style = ResolveStyle(
            worksheet,
            address,
            styles);
        style = ConditionalFormattingEvaluator.ResolveStyle(
            worksheet,
            address,
            style);

        if (style.Fill.IsVisible)
        {
            builder.FillRectangle(
                bounds,
                style.Fill.Color);
        }
        else if (backgroundFallback is { } fallback)
        {
            builder.FillRectangle(
                bounds,
                fallback);
        }

        if (!cell.Value.IsBlank)
        {
            var textBounds = new RectD(
                bounds.X + 4d,
                bounds.Y + 1d,
                Math.Max(
                    0d,
                    bounds.Width - 8d),
                Math.Max(
                    0d,
                    bounds.Height - 2d));
            builder.DrawText(
                FormatCellValue(
                    cell.Value,
                    style.NumberFormat.FormatCode),
                textBounds,
                new TextStyle(
                    style.Font.Family,
                    style.Font.Size,
                    style.Font.Weight,
                    style.Font.Color,
                    style.Alignment.WrapText));
        }

        DrawCellBorders(
            builder,
            bounds,
            style.Border);
    }

    private static CellStyle ResolveStyle(
        WorksheetSnapshot worksheet,
        CellAddress address,
        CellStyleCatalog? styles) =>
        styles is null
            ? CellStyle.Default
            : worksheet.GetEffectiveStyle(
                address,
                styles);

    private static string FormatCellValue(
        CellValue value,
        string formatCode)
    {
        if (string.Equals(
                formatCode,
                "General",
                StringComparison.OrdinalIgnoreCase))
        {
            return value.ToString();
        }

        try
        {
            return value.RawValue switch
            {
                double number => number.ToString(
                    formatCode,
                    CultureInfo.CurrentCulture),
                DateTime dateTime => dateTime.ToString(
                    formatCode,
                    CultureInfo.CurrentCulture),
                _ => value.ToString(),
            };
        }
        catch (FormatException)
        {
            return value.ToString();
        }
    }

    private static void DrawCellBorders(
        DisplayListBuilder builder,
        RectD bounds,
        CellBorderStyle border)
    {
        DrawBorder(
            builder,
            border.Top,
            new PointD(
                bounds.Left,
                bounds.Top),
            new PointD(
                bounds.Right,
                bounds.Top));
        DrawBorder(
            builder,
            border.Right,
            new PointD(
                bounds.Right,
                bounds.Top),
            new PointD(
                bounds.Right,
                bounds.Bottom));
        DrawBorder(
            builder,
            border.Bottom,
            new PointD(
                bounds.Right,
                bounds.Bottom),
            new PointD(
                bounds.Left,
                bounds.Bottom));
        DrawBorder(
            builder,
            border.Left,
            new PointD(
                bounds.Left,
                bounds.Bottom),
            new PointD(
                bounds.Left,
                bounds.Top));
    }

    private static void DrawBorder(
        DisplayListBuilder builder,
        CellBorderSide border,
        PointD start,
        PointD end)
    {
        if (border.Style ==
            CellBorderLineStyle.None)
        {
            return;
        }

        var multiplier = border.Style switch
        {
            CellBorderLineStyle.Medium => 1.5d,
            CellBorderLineStyle.Thick or
            CellBorderLineStyle.DoubleLine => 2d,
            _ => 1d,
        };
        builder.DrawLine(
            start,
            end,
            border.Width * multiplier,
            border.Color);
    }

    private static void DrawGrid(
        DisplayListBuilder builder,
        AxisSlot[] rows,
        AxisSlot[] columns,
        RectD pane,
        SpreadsheetRenderTheme theme)
    {
        foreach (var column in columns)
        {
            var x = column.End;
            if (x >= pane.Left &&
                x <= pane.Right)
            {
                builder.DrawLine(
                    new PointD(
                        x,
                        pane.Top),
                    new PointD(
                        x,
                        pane.Bottom),
                    theme.GridStrokeWidth,
                    theme.GridLine);
            }
        }

        foreach (var row in rows)
        {
            var y = row.End;
            if (y >= pane.Top &&
                y <= pane.Bottom)
            {
                builder.DrawLine(
                    new PointD(
                        pane.Left,
                        y),
                    new PointD(
                        pane.Right,
                        y),
                    theme.GridStrokeWidth,
                    theme.GridLine);
            }
        }
    }

    private static void DrawSelection(
        DisplayListBuilder builder,
        AxisSlot[] rows,
        AxisSlot[] columns,
        WorksheetSnapshot worksheet,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme)
    {
        foreach (var range in selection.Ranges)
        {
            if (TryGetRangeBounds(
                    rows,
                    columns,
                    range,
                    out var bounds))
            {
                DrawRectangleOutline(
                    builder,
                    bounds,
                    theme.SelectionStrokeWidth,
                    theme.Selection);
            }
        }

        var activeRange = worksheet.TryGetMergedRange(
            selection.ActiveCell,
            out var mergedRange)
            ? mergedRange
            : new CellRange(
                selection.ActiveCell,
                selection.ActiveCell);
        if (TryGetRangeBounds(
                rows,
                columns,
                activeRange,
                out var activeBounds))
        {
            DrawRectangleOutline(
                builder,
                activeBounds,
                theme.SelectionStrokeWidth,
                theme.ActiveCell);
        }
    }

    private static bool TryGetRangeBounds(
        AxisSlot[] rows,
        AxisSlot[] columns,
        CellRange range,
        out RectD bounds)
    {
        var matchingRows = rows
            .Where(slot =>
                slot.Index >= range.Top &&
                slot.Index <= range.Bottom)
            .ToArray();
        var matchingColumns = columns
            .Where(slot =>
                slot.Index >= range.Left &&
                slot.Index <= range.Right)
            .ToArray();
        if (matchingRows.Length == 0 ||
            matchingColumns.Length == 0)
        {
            bounds = RectD.Empty;
            return false;
        }

        var firstRow = matchingRows[0];
        var lastRow = matchingRows[^1];
        var firstColumn = matchingColumns[0];
        var lastColumn = matchingColumns[^1];
        if (firstRow.Size <= 0d ||
            lastRow.Size <= 0d ||
            firstColumn.Size <= 0d ||
            lastColumn.Size <= 0d)
        {
            bounds = RectD.Empty;
            return false;
        }

        bounds = new RectD(
            firstColumn.Start,
            firstRow.Start,
            lastColumn.End - firstColumn.Start,
            lastRow.End - firstRow.Start);
        return true;
    }

    private static void DrawFreezeSeparators(
        DisplayListBuilder builder,
        RectD viewport,
        double frozenWidth,
        double frozenHeight,
        SpreadsheetRenderTheme theme)
    {
        if (frozenWidth > 0d &&
            frozenWidth < viewport.Width)
        {
            builder.DrawLine(
                new PointD(
                    frozenWidth,
                    viewport.Top),
                new PointD(
                    frozenWidth,
                    viewport.Bottom),
                theme.FreezePaneStrokeWidth,
                theme.FreezePaneLine);
        }

        if (frozenHeight > 0d &&
            frozenHeight < viewport.Height)
        {
            builder.DrawLine(
                new PointD(
                    viewport.Left,
                    frozenHeight),
                new PointD(
                    viewport.Right,
                    frozenHeight),
                theme.FreezePaneStrokeWidth,
                theme.FreezePaneLine);
        }
    }

    private static void DrawRectangleOutline(
        DisplayListBuilder builder,
        RectD bounds,
        double strokeWidth,
        ColorRgba color)
    {
        builder.DrawLine(
            new PointD(
                bounds.Left,
                bounds.Top),
            new PointD(
                bounds.Right,
                bounds.Top),
            strokeWidth,
            color);
        builder.DrawLine(
            new PointD(
                bounds.Right,
                bounds.Top),
            new PointD(
                bounds.Right,
                bounds.Bottom),
            strokeWidth,
            color);
        builder.DrawLine(
            new PointD(
                bounds.Right,
                bounds.Bottom),
            new PointD(
                bounds.Left,
                bounds.Bottom),
            strokeWidth,
            color);
        builder.DrawLine(
            new PointD(
                bounds.Left,
                bounds.Bottom),
            new PointD(
                bounds.Left,
                bounds.Top),
            strokeWidth,
            color);
    }

    private static void ValidateTheme(
        SpreadsheetRenderTheme theme)
    {
        if (!double.IsFinite(theme.FontSize) ||
            theme.FontSize <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(theme),
                "FontSize must be finite and positive.");
        }

        if (!double.IsFinite(
                theme.SelectionStrokeWidth) ||
            theme.SelectionStrokeWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(theme),
                "SelectionStrokeWidth must be finite and positive.");
        }

        if (!double.IsFinite(theme.GridStrokeWidth) ||
            theme.GridStrokeWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(theme),
                "GridStrokeWidth must be finite and positive.");
        }

        if (!double.IsFinite(
                theme.FreezePaneStrokeWidth) ||
            theme.FreezePaneStrokeWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(theme),
                "FreezePaneStrokeWidth must be finite and positive.");
        }
    }
}
