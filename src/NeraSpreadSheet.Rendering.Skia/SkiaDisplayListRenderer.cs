using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Foundation.Collections;
using NeraSpreadSheet.Rendering;
using SkiaSharp;

namespace NeraSpreadSheet.Rendering.Skia;

/// <summary>
/// Executes the platform-neutral Nera display list on a caller-owned Skia canvas.
/// Instances are thread-affine and reuse bounded native font resources across frames.
/// </summary>
public sealed class SkiaDisplayListRenderer : IDisposable
{
    public const int DefaultTypefaceCacheCapacity = 64;

    private readonly BoundedLruCache<TypefaceKey, TypefaceResource> _typefaces;
    private readonly SKPaint _paint = new() { IsAntialias = true };
    private readonly SKFont _font = new();
    private long _successfulRenderCount;
    private long _failedRenderCount;
    private long _executedCommandCount;
    private bool _disposed;

    public SkiaDisplayListRenderer(int typefaceCacheCapacity = DefaultTypefaceCacheCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(typefaceCacheCapacity);
        _typefaces = new BoundedLruCache<TypefaceKey, TypefaceResource>(
            typefaceCacheCapacity,
            static resource => resource.Dispose());
    }

    public int TypefaceCacheCapacity => _typefaces.Capacity;

    public int CachedTypefaceCount => _typefaces.Count;

    public long TypefaceCacheHits => _typefaces.HitCount;

    public long TypefaceCacheMisses => _typefaces.MissCount;

    public long TypefaceCacheEvictions => _typefaces.EvictionCount;

    public long SuccessfulRenderCount => _successfulRenderCount;

    public long FailedRenderCount => _failedRenderCount;

    public long ExecutedCommandCount => _executedCommandCount;

    public SkiaRendererDiagnostics GetDiagnostics() => new(
        TypefaceCacheCapacity,
        CachedTypefaceCount,
        TypefaceCacheHits,
        TypefaceCacheMisses,
        TypefaceCacheEvictions,
        SuccessfulRenderCount,
        FailedRenderCount,
        ExecutedCommandCount);

    public void Render(SKCanvas canvas, DisplayList displayList) =>
        Render(canvas, displayList, dpiScaleX: 1d, dpiScaleY: 1d);

    public void Render(
        SKCanvas canvas,
        DisplayList displayList,
        RenderFrameContext context) =>
        Render(canvas, displayList, context.DpiScaleX, context.DpiScaleY);

    public void Render(
        SKCanvas canvas,
        DisplayList displayList,
        double dpiScaleX,
        double dpiScaleY)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(displayList);
        ValidateScale(dpiScaleX, nameof(dpiScaleX));
        ValidateScale(dpiScaleY, nameof(dpiScaleY));

        var originalSaveCount = canvas.SaveCount;
        var states = new Stack<RenderStateKind>();
        var executedThisFrame = 0L;

        try
        {
            canvas.Save();
            canvas.Scale((float)dpiScaleX, (float)dpiScaleY);
            ExecuteDisplayList(canvas, displayList, states, ref executedThisFrame);
            if (states.Count != 0)
            {
                throw new InvalidOperationException(
                    "Display-list render-state stack is unbalanced.");
            }

            _successfulRenderCount++;
        }
        catch
        {
            _failedRenderCount++;
            throw;
        }
        finally
        {
            _executedCommandCount += executedThisFrame;
            canvas.RestoreToCount(originalSaveCount);
        }
    }

    public void ClearCaches()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ResetFontTypeface();
        _typefaces.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ResetFontTypeface();
        _typefaces.Dispose();
        _font.Dispose();
        _paint.Dispose();
        _disposed = true;
    }

    private void ExecuteDisplayList(
        SKCanvas canvas,
        DisplayList displayList,
        Stack<RenderStateKind> states,
        ref long executedCommandCount)
    {
        foreach (var command in displayList.Commands)
        {
            executedCommandCount++;
            switch (command)
            {
                case FillRectangleCommand fill:
                    DrawFill(canvas, fill);
                    break;
                case FillPolygonCommand polygon:
                    DrawPolygon(canvas, polygon);
                    break;
                case DrawLineCommand line:
                    DrawLine(canvas, line);
                    break;
                case DrawTextCommand text:
                    DrawText(canvas, text);
                    break;
                case DrawDisplayListCommand nested:
                    ExecuteDisplayList(
                        canvas,
                        nested.DisplayList,
                        states,
                        ref executedCommandCount);
                    break;
                case PushClipCommand clip:
                    canvas.Save();
                    canvas.ClipRect(
                        ToRect(clip.Bounds),
                        SKClipOperation.Intersect,
                        antialias: false);
                    states.Push(RenderStateKind.Clip);
                    break;
                case PopClipCommand:
                    EnsureTopState(states, RenderStateKind.Clip);
                    canvas.Restore();
                    states.Pop();
                    break;
                case PushTranslationCommand translation:
                    canvas.Save();
                    canvas.Translate(
                        checked((float)translation.DeltaX),
                        checked((float)translation.DeltaY));
                    states.Push(RenderStateKind.Translation);
                    break;
                case PopTranslationCommand:
                    EnsureTopState(states, RenderStateKind.Translation);
                    canvas.Restore();
                    states.Pop();
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

    private void DrawPolygon(SKCanvas canvas, FillPolygonCommand command)
    {
        using var path = new SKPath();
        path.MoveTo(
            checked((float)command.Points[0].X),
            checked((float)command.Points[0].Y));
        for (var index = 1; index < command.Points.Count; index++)
        {
            path.LineTo(
                checked((float)command.Points[index].X),
                checked((float)command.Points[index].Y));
        }
        path.Close();
        ConfigurePaint(command.Color, SKPaintStyle.Fill, strokeWidth: 1f);
        canvas.DrawPath(path, _paint);
    }

    private void DrawLine(SKCanvas canvas, DrawLineCommand command)
    {
        var strokeWidth = checked((float)command.StrokeWidth);
        if (!float.IsFinite(strokeWidth) || strokeWidth <= 0f)
        {
            throw new InvalidOperationException(
                "Display-list line stroke width must be finite and greater than zero.");
        }

        ConfigurePaint(command.Color, SKPaintStyle.Stroke, strokeWidth);
        canvas.DrawLine(
            checked((float)command.Start.X),
            checked((float)command.Start.Y),
            checked((float)command.End.X),
            checked((float)command.End.Y),
            _paint);
    }

    private void DrawText(SKCanvas canvas, DrawTextCommand command)
    {
        if (command.Bounds.IsEmpty || string.IsNullOrEmpty(command.Text))
        {
            return;
        }

        var fontSize = checked((float)command.Style.FontSize);
        if (!float.IsFinite(fontSize) || fontSize <= 0f)
        {
            throw new InvalidOperationException(
                "Display-list font size must be finite and greater than zero.");
        }

        ConfigurePaint(command.Style.Color, SKPaintStyle.Fill, strokeWidth: 1f);
        _font.Typeface = ResolveTypeface(command.Style);
        _font.Size = fontSize;
        _font.Subpixel = true;

        canvas.Save();
        canvas.ClipRect(
            ToRect(command.Bounds),
            SKClipOperation.Intersect,
            antialias: true);
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
        var baselineY = checked((float)bounds.Top) - _font.Metrics.Ascent;
        var maxY = checked((float)bounds.Bottom);
        var maxWidth = checked((float)bounds.Width);

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
                    DrawTextAtBaseline(canvas, line, checked((float)bounds.Left), baselineY);
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
                DrawTextAtBaseline(canvas, line, checked((float)bounds.Left), baselineY);
                baselineY += lineHeight;
            }
        }
    }

    private void DrawTextLine(SKCanvas canvas, string text, double x, double y)
    {
        var baseline = checked((float)y) - _font.Metrics.Ascent;
        DrawTextAtBaseline(canvas, text, checked((float)x), baseline);
    }

    private void DrawTextAtBaseline(
        SKCanvas canvas,
        string text,
        float x,
        float baselineY) =>
        canvas.DrawText(
            text,
            x,
            baselineY,
            SKTextAlign.Left,
            _font,
            _paint);

    private SKTypeface ResolveTypeface(TextStyle style)
    {
        var key = new TypefaceKey(style.FontFamily, style.FontWeight);
        return _typefaces.GetOrAdd(key, CreateTypefaceResource).Typeface;
    }

    private static TypefaceResource CreateTypefaceResource(TypefaceKey key)
    {
        var fontStyle = new SKFontStyle(
            Math.Clamp(key.FontWeight, 1, 1000),
            (int)SKFontStyleWidth.Normal,
            SKFontStyleSlant.Upright);
        var typeface = SKTypeface.FromFamilyName(key.FamilyName, fontStyle)
            ?? SKTypeface.Default;
        return new TypefaceResource(
            typeface,
            ownsTypeface: !ReferenceEquals(typeface, SKTypeface.Default));
    }

    private void ConfigurePaint(
        ColorRgba color,
        SKPaintStyle style,
        float strokeWidth)
    {
        _paint.Color = new SKColor(color.Red, color.Green, color.Blue, color.Alpha);
        _paint.Style = style;
        _paint.StrokeWidth = strokeWidth;
        _paint.StrokeCap = SKStrokeCap.Butt;
        _paint.StrokeJoin = SKStrokeJoin.Miter;
        _paint.IsAntialias = true;
    }

    private void ResetFontTypeface() => _font.Typeface = SKTypeface.Default;

    private static void EnsureTopState(
        Stack<RenderStateKind> states,
        RenderStateKind expected)
    {
        if (!states.TryPeek(out var actual) || actual != expected)
        {
            throw new InvalidOperationException(
                "Display-list render-state stack is unbalanced.");
        }
    }

    private static void ValidateScale(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0d || value > float.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "DPI scale must be finite, greater than zero and representable by Skia.");
        }
    }

    private static SKRect ToRect(RectD bounds) => new(
        checked((float)bounds.Left),
        checked((float)bounds.Top),
        checked((float)bounds.Right),
        checked((float)bounds.Bottom));

    private readonly record struct TypefaceKey(string FamilyName, int FontWeight);

    private sealed class TypefaceResource : IDisposable
    {
        private readonly bool _ownsTypeface;

        public TypefaceResource(SKTypeface typeface, bool ownsTypeface)
        {
            Typeface = typeface;
            _ownsTypeface = ownsTypeface;
        }

        public SKTypeface Typeface { get; }

        public void Dispose()
        {
            if (_ownsTypeface)
            {
                Typeface.Dispose();
            }
        }
    }

    private enum RenderStateKind
    {
        Clip,
        Translation,
    }
}