using System.Globalization;
using System.Windows;
using System.Windows.Media;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Wpf;

internal sealed class WpfDisplayListRenderer
{
    private readonly Dictionary<ColorRgba, SolidColorBrush> _brushes = [];
    private readonly Dictionary<(ColorRgba Color, double Width), Pen> _pens = [];
    private readonly Dictionary<TextStyle, Typeface> _typefaces = [];

    public void Render(DrawingContext drawingContext, DisplayList displayList, double pixelsPerDip)
    {
        ArgumentNullException.ThrowIfNull(drawingContext);
        ArgumentNullException.ThrowIfNull(displayList);
        if (!double.IsFinite(pixelsPerDip) || pixelsPerDip <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(pixelsPerDip));
        }

        var clipDepth = 0;
        try
        {
            foreach (var command in displayList.Commands)
            {
                switch (command)
                {
                    case FillRectangleCommand fill:
                        drawingContext.DrawRectangle(GetBrush(fill.Color), null, ToRect(fill.Bounds));
                        break;
                    case DrawLineCommand line:
                        drawingContext.DrawLine(GetPen(line.Color, line.StrokeWidth), ToPoint(line.Start), ToPoint(line.End));
                        break;
                    case DrawTextCommand text:
                        DrawText(drawingContext, text, pixelsPerDip);
                        break;
                    case PushClipCommand pushClip:
                        drawingContext.PushClip(new RectangleGeometry(ToRect(pushClip.Bounds)));
                        clipDepth++;
                        break;
                    case PopClipCommand:
                        if (clipDepth <= 0)
                        {
                            throw new InvalidOperationException("Display-list clip stack is unbalanced.");
                        }
                        drawingContext.Pop();
                        clipDepth--;
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported render command '{command.GetType().Name}'.");
                }
            }
        }
        finally
        {
            while (clipDepth > 0)
            {
                drawingContext.Pop();
                clipDepth--;
            }
        }
    }

    private void DrawText(DrawingContext drawingContext, DrawTextCommand command, double pixelsPerDip)
    {
        if (string.IsNullOrEmpty(command.Text) || command.Bounds.Width <= 0d || command.Bounds.Height <= 0d)
        {
            return;
        }

        var formatted = new FormattedText(
            command.Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            GetTypeface(command.Style),
            command.Style.FontSize,
            GetBrush(command.Style.Color),
            pixelsPerDip)
        {
            MaxTextWidth = Math.Max(0.1d, command.Bounds.Width),
            MaxTextHeight = Math.Max(0.1d, command.Bounds.Height),
            Trimming = TextTrimming.CharacterEllipsis,
        };

        drawingContext.DrawText(formatted, new Point(command.Bounds.X, command.Bounds.Y));
    }

    private SolidColorBrush GetBrush(ColorRgba color)
    {
        if (_brushes.TryGetValue(color, out var brush))
        {
            return brush;
        }

        brush = new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
        brush.Freeze();
        _brushes.Add(color, brush);
        return brush;
    }

    private Pen GetPen(ColorRgba color, double width)
    {
        var key = (color, width);
        if (_pens.TryGetValue(key, out var pen))
        {
            return pen;
        }

        pen = new Pen(GetBrush(color), width);
        pen.Freeze();
        _pens.Add(key, pen);
        return pen;
    }

    private Typeface GetTypeface(TextStyle style)
    {
        if (_typefaces.TryGetValue(style, out var typeface))
        {
            return typeface;
        }

        typeface = new Typeface(
            new FontFamily(style.FontFamily),
            FontStyles.Normal,
            FontWeight.FromOpenTypeWeight(Math.Clamp(style.FontWeight, 1, 999)),
            FontStretches.Normal);
        _typefaces.Add(style, typeface);
        return typeface;
    }

    private static Rect ToRect(RectD bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);
    private static Point ToPoint(PointD point) => new(point.X, point.Y);
}
