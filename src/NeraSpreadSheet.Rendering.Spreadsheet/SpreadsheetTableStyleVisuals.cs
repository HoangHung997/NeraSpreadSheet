using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public sealed record SpreadsheetTableFilterButtonVisual(
    ColorRgba Background,
    ColorRgba ActiveBackground,
    ColorRgba Border,
    ColorRgba Glyph);

public static class SpreadsheetTableStyleVisuals
{
    public static SpreadsheetTableFilterButtonVisual ResolveFilterButton(
        Workbook workbook,
        SpreadsheetTable table,
        SpreadsheetRenderTheme theme)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(theme);
        if (table.StyleName is null ||
            !workbook.TableStyles.TryGet(table.StyleName, out var definition))
        {
            return CreateFallback(theme);
        }

        var style = TableStyleResolver
            .Resolve(definition!, workbook.Theme)
            .ResolveFilterButton(table);
        return new SpreadsheetTableFilterButtonVisual(
            style.Fill.IsVisible
                ? style.Fill.Color
                : theme.TableFilterButtonBackground,
            theme.TableFilterButtonFilteredBackground,
            ResolveBorderColor(style.Border, theme.TableFilterButtonBorder),
            style.Font.Color == CellStyle.Default.Font.Color
                ? theme.TableFilterButtonGlyph
                : style.Font.Color);
    }

    private static SpreadsheetTableFilterButtonVisual CreateFallback(
        SpreadsheetRenderTheme theme) =>
        new(
            theme.TableFilterButtonBackground,
            theme.TableFilterButtonFilteredBackground,
            theme.TableFilterButtonBorder,
            theme.TableFilterButtonGlyph);

    private static ColorRgba ResolveBorderColor(
        CellBorderStyle border,
        ColorRgba fallback)
    {
        foreach (var side in new[]
                 {
                     border.Left,
                     border.Top,
                     border.Right,
                     border.Bottom,
                 })
        {
            if (side.Style != CellBorderLineStyle.None)
            {
                return side.Color;
            }
        }
        return fallback;
    }
}
