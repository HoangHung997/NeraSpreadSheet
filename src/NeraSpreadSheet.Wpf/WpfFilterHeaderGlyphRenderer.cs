using System.Windows;
using System.Windows.Media;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Wpf;

internal static class WpfFilterHeaderGlyphRenderer
{
    public static void Draw(
        DrawingContext drawingContext,
        Rect bounds,
        SpreadsheetFilterHeaderState state,
        bool? sortDescending,
        Brush brush)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        ArgumentNullException.ThrowIfNull(brush);
        var centerX = bounds.Left + (bounds.Width / 2d);
        var centerY = bounds.Top + (bounds.Height / 2d) + 0.5d;
        if (state is SpreadsheetFilterHeaderState.Sorted or
            SpreadsheetFilterHeaderState.FilteredAndSorted)
        {
            DrawSortArrow(
                drawingContext,
                centerX,
                centerY,
                descending: sortDescending == true,
                brush);
            if (state == SpreadsheetFilterHeaderState.FilteredAndSorted)
            {
                drawingContext.DrawEllipse(
                    brush,
                    null,
                    new Point(bounds.Left + 3d, bounds.Top + 3d),
                    1.4d,
                    1.4d);
            }
            return;
        }

        if (state == SpreadsheetFilterHeaderState.Filtered)
        {
            var funnel = new StreamGeometry();
            using (var context = funnel.Open())
            {
                context.BeginFigure(
                    new Point(centerX - 4d, centerY - 3d),
                    isFilled: false,
                    isClosed: false);
                context.LineTo(
                    new Point(centerX + 4d, centerY - 3d),
                    true,
                    false);
                context.LineTo(
                    new Point(centerX + 1.2d, centerY),
                    true,
                    false);
                context.LineTo(
                    new Point(centerX + 1.2d, centerY + 3.5d),
                    true,
                    false);
                context.LineTo(
                    new Point(centerX - 1.2d, centerY + 2.4d),
                    true,
                    false);
                context.LineTo(
                    new Point(centerX - 1.2d, centerY),
                    true,
                    false);
                context.LineTo(
                    new Point(centerX - 4d, centerY - 3d),
                    true,
                    false);
            }
            drawingContext.DrawGeometry(null, new Pen(brush, 1.2d), funnel);
            return;
        }

        var chevron = new StreamGeometry();
        using (var context = chevron.Open())
        {
            context.BeginFigure(
                new Point(centerX - 3.5d, centerY - 1.5d),
                isFilled: false,
                isClosed: false);
            context.LineTo(new Point(centerX, centerY + 2d), true, false);
            context.LineTo(
                new Point(centerX + 3.5d, centerY - 1.5d),
                true,
                false);
        }
        drawingContext.DrawGeometry(null, new Pen(brush, 1.2d), chevron);
    }

    private static void DrawSortArrow(
        DrawingContext drawingContext,
        double centerX,
        double centerY,
        bool descending,
        Brush brush)
    {
        var direction = descending ? 1d : -1d;
        var startY = centerY - (direction * 3.5d);
        var tipY = centerY + (direction * 3.5d);
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(
                new Point(centerX, startY),
                isFilled: false,
                isClosed: false);
            context.LineTo(new Point(centerX, tipY), true, false);
            context.BeginFigure(
                new Point(centerX - 2.7d, tipY - (direction * 2.7d)),
                isFilled: false,
                isClosed: false);
            context.LineTo(new Point(centerX, tipY), true, false);
            context.LineTo(
                new Point(centerX + 2.7d, tipY - (direction * 2.7d)),
                true,
                false);
        }
        drawingContext.DrawGeometry(null, new Pen(brush, 1.35d), geometry);
    }
}
