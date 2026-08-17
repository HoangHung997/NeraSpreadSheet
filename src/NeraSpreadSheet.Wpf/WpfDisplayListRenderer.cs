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

        var states = new Stack<RenderState>();
        var offsetX = 0d;
        var offsetY = 0d;
        try
        {
            ExecuteDisplayList(drawingContext, displayList, pixelsPerDip, states, ref offsetX, ref offsetY);
        }
        finally
        {
            while (states.TryPop(out var state))
            {
                if (state.Kind == RenderStateKind.Clip)
                {
                    drawingContext.Pop();
                }
                else
                {
                    offsetX = state.PreviousOffsetX;
                    offsetY = state.PreviousOffsetY;
                }
            }
        }
    }

    private void ExecuteDisplayList(
        DrawingContext drawingContext,
        DisplayList displayList,
        double pixelsPerDip,
        Stack<RenderState> states,
        ref double offsetX,
        ref double offsetY)
    {
        foreach (var command in displayList.Commands)
        {
            switch (command)
            {
                case FillRectangleCommand fill:
                    drawingContext.DrawRectangle(GetBrush(fill.Color), null, ToRect(fill.Bounds.Translate(offsetX, offsetY)));
                    break;
                case DrawLineCommand line:
                    drawingContext.DrawLine(
                        GetPen(line.Color, line.StrokeWidth),
                        ToPoint(line.Start, offsetX, offsetY),
                        ToPoint(line.End, offsetX, offsetY));
                    break;
                case DrawTextCommand text:
                    DrawText(drawingContext, text, pixelsPerDip, offsetX, offsetY);
                    break;
                case DrawDisplayListCommand nested:
                    ExecuteDisplayList(
                        drawingContext,
                        nested.DisplayList,
                        pixelsPerDip,
                        states,
                        ref offsetX,
                        ref offsetY);
                    break;
                case PushClipCommand pushClip:
                    drawingContext.PushClip(new RectangleGeometry(ToRect(pushClip.Bounds.Translate(offsetX, offsetY))));
                    states.Push(new RenderState(RenderStateKind.Clip, offsetX, offsetY));
                    break;
                case PopClipCommand:
                    EnsureTopState(states, RenderStateKind.Clip);
                    drawingContext.Pop();
                    states.Pop();
                    break;
                case PushTranslationCommand translation:
                    states.Push(new RenderState(RenderStateKind.Translation, offsetX, offsetY));
                    offsetX += translation.DeltaX;
                    offsetY += translation.DeltaY;
                    break;
                case PopTranslationCommand:
                {
                    var state = EnsureTopState(states, RenderStateKind.Translation);
                    states.Pop();
                    offsetX = state.PreviousOffsetX;
                    offsetY = state.PreviousOffsetY;
                    break;
                }
                default:
                    throw new NotSupportedException($"Unsupported render command '{command.GetType().Name}'.");
            }
        }
    }

    private void DrawText(
        DrawingContext drawingContext,
        DrawTextCommand command,
        double pixelsPerDip,
        double offsetX,
        double offsetY)
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

        drawingContext.DrawText(formatted, new Point(command.Bounds.X + offsetX, command.Bounds.Y + offsetY));
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

    private static RenderState EnsureTopState(Stack<RenderState> states, RenderStateKind expected)
    {
        if (!states.TryPeek(out var state) || state.Kind != expected)
        {
            throw new InvalidOperationException("Display-list render-state stack is unbalanced.");
        }
        return state;
    }

    private static Rect ToRect(RectD bounds) => new(bounds.X, bounds.Y, bounds.Width, bounds.Height);

    private static Point ToPoint(PointD point, double offsetX, double offsetY) =>
        new(point.X + offsetX, point.Y + offsetY);

    private readonly record struct RenderState(RenderStateKind Kind, double PreviousOffsetX, double PreviousOffsetY);

    private enum RenderStateKind
    {
        Clip,
        Translation,
    }
}
