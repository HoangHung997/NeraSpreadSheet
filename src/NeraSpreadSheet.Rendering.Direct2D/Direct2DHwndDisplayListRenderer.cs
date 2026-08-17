using System.Numerics;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Foundation.Collections;
using NeraSpreadSheet.Rendering;
using SharpGen.Runtime;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using static Vortice.Direct2D1.D2D1;
using static Vortice.DirectWrite.DWrite;

namespace NeraSpreadSheet.Rendering.Direct2D;

public sealed class Direct2DHwndDisplayListRenderer : IDisposable
{
    public const int DefaultTextLayoutCacheCapacity = 2048;

    private readonly nint _windowHandle;
    private readonly ID2D1Factory1 _d2dFactory;
    private readonly IDWriteFactory1 _writeFactory;
    private readonly Dictionary<ColorRgba, ID2D1SolidColorBrush> _brushes = [];
    private readonly Dictionary<TextStyle, IDWriteTextFormat> _textFormats = [];
    private readonly BoundedLruCache<TextLayoutKey, IDWriteTextLayout> _textLayouts;
    private ID2D1HwndRenderTarget? _renderTarget;
    private int _pixelWidth;
    private int _pixelHeight;
    private bool _disposed;

    public Direct2DHwndDisplayListRenderer(
        nint windowHandle,
        int pixelWidth,
        int pixelHeight,
        int textLayoutCacheCapacity = DefaultTextLayoutCacheCapacity)
    {
        if (windowHandle == 0)
        {
            throw new ArgumentException("A valid HWND is required.", nameof(windowHandle));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(textLayoutCacheCapacity);

        _windowHandle = windowHandle;
        _pixelWidth = pixelWidth;
        _pixelHeight = pixelHeight;
        _d2dFactory = D2D1CreateFactory<ID2D1Factory1>();
        _writeFactory = DWriteCreateFactory<IDWriteFactory1>();
        _textLayouts = new BoundedLruCache<TextLayoutKey, IDWriteTextLayout>(
            textLayoutCacheCapacity,
            static layout => layout.Dispose());
        CreateRenderTarget();
    }

    public int PixelWidth => _pixelWidth;

    public int PixelHeight => _pixelHeight;

    public int CachedTextLayoutCount => _textLayouts.Count;

    public long TextLayoutCacheHits => _textLayouts.HitCount;

    public long TextLayoutCacheMisses => _textLayouts.MissCount;

    public long TextLayoutCacheEvictions => _textLayouts.EvictionCount;

    public long DeviceRecoveryCount { get; private set; }

    public Direct2DRendererDiagnostics Diagnostics => new(
        _pixelWidth,
        _pixelHeight,
        _textLayouts.Capacity,
        _textLayouts.Count,
        _textLayouts.HitCount,
        _textLayouts.MissCount,
        _textLayouts.EvictionCount,
        DeviceRecoveryCount);

    public void Resize(int pixelWidth, int pixelHeight)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);
        if (_pixelWidth == pixelWidth && _pixelHeight == pixelHeight)
        {
            return;
        }

        _pixelWidth = pixelWidth;
        _pixelHeight = pixelHeight;
        CreateRenderTarget();
    }

    public void Render(DisplayList displayList)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(displayList);

        try
        {
            RenderCore(displayList);
        }
        catch (SharpGenException)
        {
            DeviceRecoveryCount++;
            CreateRenderTarget();
            RenderCore(displayList);
        }
    }

    public void RecreateDeviceResources()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CreateRenderTarget();
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

        DisposeTargetResources();
        _textLayouts.Dispose();
        foreach (var format in _textFormats.Values)
        {
            format.Dispose();
        }
        _textFormats.Clear();
        _writeFactory.Dispose();
        _d2dFactory.Dispose();
        _disposed = true;
    }

    private void RenderCore(DisplayList displayList)
    {
        var target = _renderTarget ?? throw new InvalidOperationException("Direct2D render target is not initialized.");
        var states = new Stack<RenderState>();
        var offsetX = 0d;
        var offsetY = 0d;

        target.BeginDraw();
        try
        {
            foreach (var command in displayList.Commands)
            {
                switch (command)
                {
                    case FillRectangleCommand fill:
                        target.FillRectangle(ToRawRect(fill.Bounds.Translate(offsetX, offsetY)), GetBrush(fill.Color));
                        break;
                    case DrawLineCommand line:
                        target.DrawLine(
                            new Vector2((float)(line.Start.X + offsetX), (float)(line.Start.Y + offsetY)),
                            new Vector2((float)(line.End.X + offsetX), (float)(line.End.Y + offsetY)),
                            GetBrush(line.Color),
                            (float)line.StrokeWidth);
                        break;
                    case DrawTextCommand text:
                        DrawText(target, text, offsetX, offsetY);
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

    private void CreateRenderTarget()
    {
        DisposeTargetResources();
        var hwndProperties = new HwndRenderTargetProperties
        {
            Hwnd = _windowHandle,
            PixelSize = new SizeI(_pixelWidth, _pixelHeight),
            PresentOptions = PresentOptions.RetainContents | PresentOptions.Immediately,
        };
        _renderTarget = _d2dFactory.CreateHwndRenderTarget(new RenderTargetProperties(), hwndProperties);
    }

    private void DisposeTargetResources()
    {
        foreach (var brush in _brushes.Values)
        {
            brush.Dispose();
        }
        _brushes.Clear();
        _renderTarget?.Dispose();
        _renderTarget = null;
    }

    private ID2D1SolidColorBrush GetBrush(ColorRgba color)
    {
        if (_brushes.TryGetValue(color, out var brush))
        {
            return brush;
        }

        var target = _renderTarget ?? throw new InvalidOperationException("Direct2D render target is not initialized.");
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
            FontStyle.Normal,
            (float)style.FontSize);
        format.WordWrapping = style.Wrap ? WordWrapping.Wrap : WordWrapping.NoWrap;
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

    private IDWriteTextLayout CreateTextLayout(TextLayoutKey key) =>
        _writeFactory.CreateTextLayout(
            key.Text,
            GetTextFormat(key.Style),
            key.Width,
            key.Height);

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
        target.DrawTextLayout(
            origin,
            GetTextLayout(command),
            GetBrush(command.Style.Color),
            DrawTextOptions.Clip);
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
