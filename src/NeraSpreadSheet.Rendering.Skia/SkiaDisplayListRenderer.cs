using SkiaSharp;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Skia;

/// <summary>
/// Executes the platform-neutral Nera display list on a Skia canvas.
/// The renderer owns reusable paint/font/typeface resources but never owns the supplied canvas.
/// </summary>
public sealed class SkiaDisplayListRenderer : IDisposable
{
    private readonly Dictionary<TypefaceKey, SKTypeface> _typefaces = [];
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly SKFont _font = new();
    private bool _disposed;

    public int CachedTypefaceCount => _typefaces.Count;

    public void Render(SKCanvas canvas, DisplayList displayList)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(displayList);
        RenderList(canvas, displayList);
    }

    public void ClearCaches()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (var typeface in _typefaces.Values)
        {
            typeface.Dispose();
        }
        _typefaces.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ClearCaches();
        _font.Dispose();
        _paint.Dispose();
        _disposed = true;
    }

    private void RenderList(SKCanvas canvas, DisplayList displayList)
    {
        foreach (var command in displayList.Commands)
        {
            switch (command)
            {
                case FillRectangleCommand fill:
                    DrawFill(canvas, fill);
                    break;
                case DrawLineCommand line:
                    DrawLine(canvas, line);
                    break;
                case DrawTextCommand text:
                    DrawText(canvas, text);
                    break;
                case DrawDisplayListCommand nested:
                    RenderList(canvas, nested.DisplayList);
                    break;
                case PushClipCommand clip:
                    canvas.Save();
                    canvas.ClipRect(ToRect(clip.Bounds), SKClipOperation.Intersect, antialias: false);
                    break;
                case PopClipCommand:
                    canvas.Restore();
                    break;
                case PushTranslationCommand translation:
                    canvas.Save();
                    canvas.Translate((float)translation.DeltaX, (float)translation.DeltaY);
                    break;
                case PopTranslationCommand:
                    canvas.Restore();
                    break;
                default:
                    throw new NotSupportedException(
                        $"Skia renderer does not support command '{command.GetType().Name}'.");
            }
        }
    }

    private void DrawFill(SKCanvas canvas, FillRectangleCommand command)
    {
        ConfigurePaint(command.Color, SKPaintStyle.Fill, strokeWidth: 1f);
        canvas.DrawRect(ToRect(command.Bounds), _paint);
    }

    private void DrawLine(SKCanvas canvas, DrawLineCommand command)
    {
        ConfigurePaint(
            command.Color,
            SKPaintStyle.Stroke,
            checked((float)command.StrokeWidth));
        canvas.DrawLine(
            (float)command.Start.X,
            (float)command.Start.Y,
            (float)command.End.X,
            (float)command.End.Y,
            _paint);
    }

    private void DrawText(SKCanvas canvas, DrawTextCommand command)
    {
        if (command.Bounds.IsEmpty || string.IsNullOrEmpty(command.Text))
        {
            return;
        }

        ConfigurePaint(command.Style.Color, SKPaintStyle.Fill, strokeWidth: 1f);
        _font.Typeface = ResolveTypeface(command.Style);
        _font.Size = checked((float)command.Style.FontSize);
        _font.Subpixel = true;

        canvas.Save();
        canvas.ClipRect(ToRect(command.Bounds), SKClipOperation.Intersect, antialias: true);
        try
        {
            if (command.Style.Wrap)
            {
                DrawWrappedText(canvas, command.Text, command.Bounds);
            }
            else
            {
                DrawTextLine(canvas, command.Text, command.Bounds.X, command.Bounds.Y);
            }
        }
        finally
        {
            canvas.Restore();
        }
    }

    private void DrawWrappedText(
        SKCanvas canvas,
        string text,
        RectD bounds)
    {
        var lineHeight = Math.Max(1f, _font.Spacing);
        var baselineY = (float)bounds.Top - _font.Metrics.Ascent;
        var maxY = (float)bounds.Bottom;
        var maxWidth = (float)bounds.Width;

        foreach (var paragraph in text.Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Replace('\r', '\n')
                     .Split('\n'))
        {
            if (baselineY > maxY)
            {
                break;
            }

            if (paragraph.Length == 0)
            {
                baselineY += lineHeight;
                continue;
            }

            var words = paragraph.Split(' ', StringSplitOptions.None);
            var line = string.Empty;
            foreach (var word in words)
            {
                var candidate = line.Length == 0 ? word : $"{line} {word}";
                if (line.Length > 0 && _font.MeasureText(candidate) > maxWidth)
                {
                    canvas.DrawText(
                        line,
                        (float)bounds.Left,
                        baselineY,
                        SKTextAlign.Left,
                        _font,
                        _paint);
                    baselineY += lineHeight;
                    if (baselineY > maxY)
                    {
                        return;
                    }
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }

            if (line.Length > 0 && baselineY <= maxY)
            {
                canvas.DrawText(
                    line,
                    (float)bounds.Left,
                    baselineY,
                    SKTextAlign.Left,
                    _font,
                    _paint);
                baselineY += lineHeight;
            }
        }
    }

    private void DrawTextLine(SKCanvas canvas, string text, double x, double y)
    {
        var baseline = checked((float)y) - _font.Metrics.Ascent;
        canvas.DrawText(
            text,
            checked((float)x),
            baseline,
            SKTextAlign.Left,
            _font,
            _paint);
    }

    private SKTypeface ResolveTypeface(TextStyle style)
    {
        var key = new TypefaceKey(style.FontFamily, style.FontWeight);
        if (_typefaces.TryGetValue(key, out var typeface))
        {
            return typeface;
        }

        var fontStyle = new SKFontStyle(
            Math.Clamp(style.FontWeight, 1, 1000),
            (int)SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);
        typeface = SKTypeface.FromFamilyName(style.FontFamily, fontStyle)
            ?? SKTypeface.Default;
        if (!ReferenceEquals(typeface, SKTypeface.Default))
        {
            _typefaces.Add(key, typeface);
        }
        return typeface;
    }

    private void ConfigurePaint(ColorRgba color, SKPaintStyle style, float strokeWidth)
    {
        _paint.Color = new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
        _paint.Style = style;
        _paint.StrokeWidth = strokeWidth;
        _paint.IsAntialias = true;
    }

    private static SKRect ToRect(RectD bounds) => new(
        checked((float)bounds.Left),
        checked((float)bounds.Top),
        checked((float)bounds.Right),
        checked((float)bounds.Bottom));

    private readonly record struct TypefaceKey(string FamilyName, int Weight);
}
