using System.Globalization;
using global::Avalonia;
using global::Avalonia.Media;
using global::Avalonia.Media.Immutable;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Executes the shared display list without owning a workbook or a Skia surface.</summary>
internal sealed class AvaloniaDisplayListRenderer
{
    private const int CacheCapacity = 2048;
    private readonly Dictionary<ColorRgba, ImmutableSolidColorBrush> _brushes = [];
    private readonly Dictionary<(ColorRgba Color, double Width), Pen> _pens = [];
    private readonly Dictionary<TextStyle, Typeface> _typefaces = [];
    private readonly Dictionary<TextCacheKey, FormattedText> _text = [];

    public void ClearCaches()
    {
        _brushes.Clear();
        _pens.Clear();
        _typefaces.Clear();
        _text.Clear();
    }

    public void Render(DrawingContext context, DisplayList displayList)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(displayList);
        var states = new Stack<RenderState>();
        var x = 0d;
        var y = 0d;
        try
        {
            Execute(context, displayList, states, ref x, ref y);
            if (states.Count != 0)
            {
                throw new InvalidOperationException("Unbalanced display-list render state.");
            }
        }
        finally
        {
            while (states.TryPop(out var state)) state.Clip?.Dispose();
        }
    }

    private void Execute(DrawingContext context, DisplayList list,
        Stack<RenderState> states, ref double x, ref double y)
    {
        foreach (var command in list.Commands)
        {
            switch (command)
            {
                case FillRectangleCommand fill:
                    context.DrawRectangle(Brush(fill.Color), null, ToRect(fill.Bounds.Translate(x, y)));
                    break;
                case DrawLineCommand line:
                    context.DrawLine(Pen(line.Color, line.StrokeWidth),
                        new Point(line.Start.X + x, line.Start.Y + y),
                        new Point(line.End.X + x, line.End.Y + y));
                    break;
                case FillPolygonCommand polygon:
                    DrawPolygon(context, polygon, x, y);
                    break;
                case DrawTextCommand text:
                    DrawText(context, text, x, y);
                    break;
                case DrawDisplayListCommand nested:
                    // Traverse references directly. Never flatten/copy command arrays.
                    Execute(context, nested.DisplayList, states, ref x, ref y);
                    break;
                case PushClipCommand clip:
                    states.Push(new RenderState(true, x, y,
                        context.PushClip(ToRect(clip.Bounds.Translate(x, y)))));
                    break;
                case PopClipCommand:
                    Pop(states, clip: true).Clip!.Dispose();
                    break;
                case PushTranslationCommand translation:
                    states.Push(new RenderState(false, x, y, null));
                    x += translation.DeltaX;
                    y += translation.DeltaY;
                    break;
                case PopTranslationCommand:
                    var previous = Pop(states, clip: false);
                    x = previous.X;
                    y = previous.Y;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported render command '{command.GetType().Name}'.");
            }
        }
    }

    private void DrawPolygon(DrawingContext context, FillPolygonCommand polygon, double x, double y)
    {
        var geometry = new StreamGeometry();
        using (var writer = geometry.Open())
        {
            writer.BeginFigure(new Point(polygon.Points[0].X + x, polygon.Points[0].Y + y), true);
            for (var index = 1; index < polygon.Points.Count; index++)
                writer.LineTo(new Point(polygon.Points[index].X + x, polygon.Points[index].Y + y));
            writer.EndFigure(true);
        }
        context.DrawGeometry(Brush(polygon.Color), null, geometry);
    }

    private void DrawText(DrawingContext context, DrawTextCommand command, double x, double y)
    {
        if (command.Text.Length == 0 || command.Bounds.Width <= 0 || command.Bounds.Height <= 0) return;
        var key = new TextCacheKey(command.Text, command.Style, command.Bounds.Width,
            command.Bounds.Height, CultureInfo.CurrentUICulture.Name);
        if (!_text.TryGetValue(key, out var formatted))
        {
            if (!_typefaces.TryGetValue(command.Style, out var typeface))
            {
                typeface = new Typeface(new FontFamily(command.Style.FontFamily),
                    command.Style.Italic ? FontStyle.Italic : FontStyle.Normal,
                    (FontWeight)Math.Clamp(command.Style.FontWeight, 1, 999));
                if (_typefaces.Count >= CacheCapacity) _typefaces.Clear();
                _typefaces.Add(command.Style, typeface);
            }
            formatted = new FormattedText(command.Text, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, typeface, command.Style.FontSize, Brush(command.Style.Color))
            {
                MaxTextWidth = command.Bounds.Width,
                MaxTextHeight = command.Bounds.Height,
                MaxLineCount = command.Style.Wrap ? int.MaxValue : 1,
                Trimming = TextTrimming.None,
                TextAlignment = command.Style.HorizontalAlignment switch
                {
                    TextHorizontalAlignment.Center => TextAlignment.Center,
                    TextHorizontalAlignment.Right => TextAlignment.Right,
                    TextHorizontalAlignment.Justify => TextAlignment.Justify,
                    _ => TextAlignment.Left,
                },
            };
            if (command.Style.Underline || command.Style.Strikethrough)
            {
                var decorations = new TextDecorationCollection();
                if (command.Style.Underline) decorations.Add(TextDecorations.Underline[0]);
                if (command.Style.Strikethrough) decorations.Add(TextDecorations.Strikethrough[0]);
                formatted.SetTextDecorations(decorations);
            }
            if (_text.Count >= CacheCapacity) _text.Clear();
            _text.Add(key, formatted);
        }
        var dy = command.Style.VerticalAlignment switch
        {
            TextVerticalAlignment.Center => Math.Max(0, (command.Bounds.Height - formatted.Height) / 2),
            TextVerticalAlignment.Bottom => Math.Max(0, command.Bounds.Height - formatted.Height),
            _ => 0d,
        };
        var origin = new Point(command.Bounds.X + x, command.Bounds.Y + y + dy);
        if (command.Style.TextRotationDegrees == 0)
        {
            context.DrawText(formatted, origin);
            return;
        }
        var cx = command.Bounds.X + x + command.Bounds.Width / 2;
        var cy = command.Bounds.Y + y + command.Bounds.Height / 2;
        var rotation = Matrix.CreateTranslation(-cx, -cy)
            * Matrix.CreateRotation(-command.Style.TextRotationDegrees * Math.PI / 180d)
            * Matrix.CreateTranslation(cx, cy);
        using (context.PushTransform(rotation)) context.DrawText(formatted, origin);
    }

    private ImmutableSolidColorBrush Brush(ColorRgba color)
    {
        if (_brushes.TryGetValue(color, out var brush)) return brush;
        if (_brushes.Count >= CacheCapacity) _brushes.Clear();
        brush = new ImmutableSolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
        _brushes.Add(color, brush);
        return brush;
    }

    private Pen Pen(ColorRgba color, double width)
    {
        var key = (color, width);
        if (_pens.TryGetValue(key, out var pen)) return pen;
        if (_pens.Count >= CacheCapacity) _pens.Clear();
        pen = new Pen(Brush(color), width);
        _pens.Add(key, pen);
        return pen;
    }

    private static RenderState Pop(Stack<RenderState> states, bool clip)
    {
        if (!states.TryPeek(out var state) || state.IsClip != clip)
            throw new InvalidOperationException("Unbalanced display-list render state.");
        return states.Pop();
    }

    private static Rect ToRect(RectD value) => new(value.X, value.Y, value.Width, value.Height);
    private readonly record struct RenderState(bool IsClip, double X, double Y, IDisposable? Clip);
    private readonly record struct TextCacheKey(string Text, TextStyle Style, double Width, double Height, string Culture);
}
