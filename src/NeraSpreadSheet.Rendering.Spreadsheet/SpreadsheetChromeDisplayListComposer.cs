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
        SpreadsheetHeaderDisplayListComposer.ValidateTheme(theme);

        var headerWidth = theme.RowHeaderWidth;
        var headerHeight = theme.ColumnHeaderHeight;
        var bodyWidth = layout.ViewportSize.Width;
        var bodyHeight = layout.ViewportSize.Height;
        var paneBounds = new RectD(0d, 0d, bodyWidth, bodyHeight);
        var builder = new DisplayListBuilder();

        builder.FillRectangle(
            new RectD(0d, 0d, headerWidth + bodyWidth, headerHeight + bodyHeight),
            theme.Background);
        builder.PushClip(new RectD(headerWidth, headerHeight, bodyWidth, bodyHeight));
        builder.PushTranslation(headerWidth, headerHeight);
        builder.Append(body);
        builder.PopTranslation();
        builder.PopClip();

        SpreadsheetHeaderDisplayListComposer.DrawCorner(
            builder,
            selection,
            theme,
            headerWidth,
            headerHeight);
        SpreadsheetHeaderDisplayListComposer.DrawColumnHeaders(
            builder,
            layout,
            paneBounds,
            selection,
            theme,
            headerWidth,
            headerHeight);
        SpreadsheetHeaderDisplayListComposer.DrawRowHeaders(
            builder,
            layout,
            paneBounds,
            selection,
            theme,
            headerWidth,
            headerHeight);
        SpreadsheetHeaderDisplayListComposer.DrawFreezeHeaderSeparators(
            builder,
            layout,
            paneBounds,
            theme,
            headerWidth,
            headerHeight,
            drawColumnSeparator: true,
            drawRowSeparator: true);
        return builder.Build();
    }
}
