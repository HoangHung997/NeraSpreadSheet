using System.Numerics;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Foundation.Collections;
using NeraSpreadSheet.Rendering;
using SharpGen.Runtime;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using static Vortice.DirectWrite.DWrite;

namespace NeraSpreadSheet.Rendering.Direct2D;

internal sealed class Direct2DDisplayListExecutor : IDisposable
{
    private readonly ID2D1Factory1 _d2dFactory;
    private readonly IDWriteFactory1 _writeFactory;
    private readonly Dictionary<ColorRgba, ID2D1SolidColorBrush> _brushes = [];
    private readonly Dictionary<TextStyle, IDWriteTextFormat> _textFormats = [];
    private readonly BoundedLruCache<TextLayoutKey, IDWriteTextLayout> _textLayouts;
    private ID2D1RenderTarget? _brushTarget;
    private bool _disposed;

    public Direct2DDisplayListExecutor(
        ID2D1Factory1 d2dFactory,
        int textLayoutCacheCapacity)
    {
        _d2dFactory = d2dFactory ?? throw new ArgumentNullException(nameof(d2dFactory));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textLayoutCacheCapacity);
        _writeFactory = DWriteCreateFactory<IDWriteFactory1>();
        _textLayouts = new BoundedLruCache<TextLayoutKey, IDWriteTextLayout>(
            textLayoutCacheCapacity,
            static layout => layout.Dispose());
    }

    public int TextLayoutCacheCapacity => _textLayouts.Capacity;
    public int CachedTextLayoutCount => _textLayouts.Count;
    public long TextLayoutCacheHits => _textLayouts.HitCount;
    public long TextLayoutCacheMisses => _textLayouts.MissCount;
    public long TextLayoutCacheEvictions => _textLayouts.EvictionCount;

    public void Render(ID2D1RenderTarget target, DisplayList displayList)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(displayList);
        EnsureBrushTarget(target);

        var states = new Stack<RenderState>();
        var offsetX = 0d;
        var offsetY = 0d;

        target.BeginDraw();
        try
        {
            ExecuteDisplayList(target, displayList, states, ref offsetX, ref offsetY);
            if (states.Count != 0)
            {
                throw new InvalidOperationException("Display-list render-state stack is unbalanced.");
            }
        }
        catch
        {
            UnwindRenderStates(target, states);
            target.EndDraw();
            throw;
        }

        target.EndDraw().CheckError();
    }

    public void InvalidateTargetResources()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        DisposeBrushes();
        _brushTarget = null;
    }

    public void ClearTextLayoutCache()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _textLayouts.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeBrushes();
        _brushTarget = null;
        _textLayouts.Dispose();
        foreach (var format in _textFormats.Values)
        {
            format.Dispose();
        }
        _textFormats.Clear();
        _writeFactory.Dispose();
        _disposed = true;
    }

    private void ExecuteDisplayList(
        ID2D1RenderTarget target,
        DisplayList displayList,
        Stack<RenderState> states,
        ref double offsetX,
        ref double offsetY)
    {
        foreach (var command in displayList.Commands)
        {
            switch (command)
            {
                case FillRectangleCommand fill:
                    target.FillRectangle(ToRawRect(fill.Bounds.Translate(offsetX, offsetY)), GetBrush(target, fill.Color));
                    break;
                case FillPolygonCommand polygon:
                    DrawPolygon(target, polygon, offsetX, offsetY);
                    break;
                case DrawLineCommand line:
                    target.DrawLine(
                        new Vector2((float)(line.Start.X + offsetX), (float)(line.Start.Y + offsetY)),
                        new Vector2((float)(line.End.X + offsetX), (float)(line.End.Y + offsetY)),
                        GetBrush(target, line.Color),
                        (float)line.StrokeWidth);
                    break;
                case DrawTextCommand text:
                    DrawText(target, text, offsetX, offsetY);
                    break;
                case DrawDisplayListCommand nested:
                    ExecuteDisplayList(target, nested.DisplayList, states, ref offsetX, ref offsetY);
                    break;
                case PushClipCommand pushClip:
                    target.PushAxisAlignedClip(ToRawRect(pushClip.Bounds.Translate(offsetX, offsetY)), AntialiasMode.Aliased);
                    states.Push(new RenderState(RenderStateKind.Clip, offsetX, offsetY));
                    break;
                case PopClipCommand:
                    EnsureTopState(states, RenderStateKind.Clip);
                    target.PopAxisAlignedClip();
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

    private void DrawPolygon(
        ID2D1RenderTarget target,
        FillPolygonCommand command,
        double offsetX,
        double offsetY)
    {
        using var geometry = _d2dFactory.CreatePathGeometry();
        using var sink = geometry.Open();
        sink.SetFillMode(FillMode.Winding);
        sink.BeginFigure(
            ToVector2(command.Points[0], offsetX, offsetY),
            FigureBegin.Filled);
        for (var index = 1; index < command.Points.Count; index++)
        {
            sink.AddLine(ToVector2(command.Points[index], offsetX, offsetY));
        }
        sink.EndFigure(FigureEnd.Closed);
        sink.Close();
        target.FillGeometry(geometry, GetBrush(target, command.Color));
    }

    private void EnsureBrushTarget(ID2D1RenderTarget target)
    {
        if (ReferenceEquals(_brushTarget, target))
        {
            return;
        }

        DisposeBrushes();
        _brushTarget = target;
    }

    private void DisposeBrushes()
    {
        foreach (var brush in _brushes.Values)
        {
            brush.Dispose();
        }
        _brushes.Clear();
    }

    private ID2D1SolidColorBrush GetBrush(ID2D1RenderTarget target, ColorRgba color)
    {
        if (_brushes.TryGetValue(color, out var brush))
        {
            return brush;
        }

        brush = target.CreateSolidColorBrush(new Color4(
            color.Red / 255f,
            color.Green / 255f,
            color.Blue / 255f,
            color.Alpha / 255f));
        _brushes.Add(color, brush);
        return brush;
    }

    private IDWriteTextFormat GetTextFormat(TextStyle style)
    {
        if (_textFormats.TryGetValue(style, out var format))
        {
            return format;
        }

        var weightValue = Math.Clamp(style.FontWeight, 100, 950);
        format = _writeFactory.CreateTextFormat(
            style.FontFamily,
            (FontWeight)weightValue,
            style.Italic ? FontStyle.Italic : FontStyle.Normal,
            (float)style.FontSize);
        format.WordWrapping = style.Wrap ? WordWrapping.Wrap : WordWrapping.NoWrap;
        format.TextAlignment = style.HorizontalAlignment switch
        {
            TextHorizontalAlignment.Center => TextAlignment.Center,
            TextHorizontalAlignment.Right => TextAlignment.Trailing,
            TextHorizontalAlignment.Justify => TextAlignment.Justified,
            _ => TextAlignment.Leading,
        };
        format.ParagraphAlignment = style.VerticalAlignment switch
        {
            TextVerticalAlignment.Center => ParagraphAlignment.Center,
            TextVerticalAlignment.Bottom => ParagraphAlignment.Far,
            _ => ParagraphAlignment.Near,
        };
        _textFormats.Add(style, format);
        return format;
    }

    private IDWriteTextLayout GetTextLayout(DrawTextCommand command)
    {
        var width = Math.Max(0.1f, (float)command.Bounds.Width);
        var height = Math.Max(0.1f, (float)command.Bounds.Height);
        var key = new TextLayoutKey(command.Text, command.Style, width, height);
        return _textLayouts.GetOrAdd(key, CreateTextLayout);
    }

    private IDWriteTextLayout CreateTextLayout(TextLayoutKey key)
    {
        var layout = _writeFactory.CreateTextLayout(
            key.Text,
            GetTextFormat(key.Style),
            key.Width,
            key.Height);
        var range = new TextRange(0, checked((uint)key.Text.Length));
        if (key.Style.Underline)
        {
            layout.SetUnderline(true, range);
        }
        if (key.Style.Strikethrough)
        {
            layout.SetStrikethrough(true, range);
        }
        return layout;
    }

    private void DrawText(
        ID2D1RenderTarget target,
        DrawTextCommand command,
        double offsetX,
        double offsetY)
    {
        if (string.IsNullOrEmpty(command.Text) || command.Bounds.Width <= 0d || command.Bounds.Height <= 0d)
        {
            return;
        }

        var origin = new Vector2(
            (float)(command.Bounds.X + offsetX),
            (float)(command.Bounds.Y + offsetY));
        var previousTransform = target.Transform;
        try
        {
            if (command.Style.TextRotationDegrees != 0)
            {
                var center = new Vector2(
                    (float)(command.Bounds.X + offsetX + (command.Bounds.Width / 2d)),
                    (float)(command.Bounds.Y + offsetY + (command.Bounds.Height / 2d)));
                target.Transform = Matrix3x2.CreateRotation(
                    (float)(-command.Style.TextRotationDegrees * Math.PI / 180d),
                    center) * previousTransform;
            }
            target.DrawTextLayout(
                origin,
                GetTextLayout(command),
                GetBrush(target, command.Style.Color),
                DrawTextOptions.Clip);
        }
        finally
        {
            target.Transform = previousTransform;
        }
    }

    private static void UnwindRenderStates(ID2D1RenderTarget target, Stack<RenderState> states)
    {
        while (states.TryPop(out var state))
        {
            if (state.Kind == RenderStateKind.Clip)
            {
                target.PopAxisAlignedClip();
            }
        }
    }

    private static RenderState EnsureTopState(Stack<RenderState> states, RenderStateKind expected)
    {
        if (!states.TryPeek(out var state) || state.Kind != expected)
        {
            throw new InvalidOperationException("Display-list render-state stack is unbalanced.");
        }
        return state;
    }

    private static Vector2 ToVector2(
        PointD point,
        double offsetX,
        double offsetY) =>
        new(
            checked((float)(point.X + offsetX)),
            checked((float)(point.Y + offsetY)));

    private static RawRectF ToRawRect(RectD bounds) => new(
        (float)bounds.Left,
        (float)bounds.Top,
        (float)bounds.Right,
        (float)bounds.Bottom);

    private readonly record struct TextLayoutKey(string Text, TextStyle Style, float Width, float Height);
    private readonly record struct RenderState(RenderStateKind Kind, double PreviousOffsetX, double PreviousOffsetY);

    private enum RenderStateKind
    {
        Clip,
        Translation,
    }
}
