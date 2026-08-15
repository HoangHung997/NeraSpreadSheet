using System.Numerics;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;
using Vortice;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using static Vortice.Direct2D1.D2D1;
using static Vortice.DirectWrite.DWrite;

namespace NeraSpreadSheet.Rendering.Direct2D;

public sealed class Direct2DHwndDisplayListRenderer : IDisposable
{
    private readonly nint _windowHandle;
    private readonly ID2D1Factory1 _d2dFactory;
    private readonly IDWriteFactory1 _writeFactory;
    private readonly Dictionary<ColorRgba, ID2D1SolidColorBrush> _brushes = [];
    private readonly Dictionary<TextStyle, IDWriteTextFormat> _textFormats = [];
    private ID2D1HwndRenderTarget? _renderTarget;
    private int _pixelWidth;
    private int _pixelHeight;
    private bool _disposed;

    public Direct2DHwndDisplayListRenderer(nint windowHandle, int pixelWidth, int pixelHeight)
    {
        if (windowHandle == 0)
        {
            throw new ArgumentException("A valid HWND is required.", nameof(windowHandle));
        }
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelHeight);

        _windowHandle = windowHandle;
        _pixelWidth = pixelWidth;
        _pixelHeight = pixelHeight;
        _d2dFactory = D2D1CreateFactory<ID2D1Factory1>();
        _writeFactory = DWriteCreateFactory<IDWriteFactory1>();
        CreateRenderTarget();
    }

    public int PixelWidth => _pixelWidth;
    public int PixelHeight => _pixelHeight;

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
        var target = _renderTarget ?? throw new InvalidOperationException("Direct2D render target is not initialized.");
        var clipDepth = 0;

        target.BeginDraw();
        foreach (var command in displayList.Commands)
        {
            switch (command)
            {
                case FillRectangleCommand fill:
                    target.FillRectangle(ToRawRect(fill.Bounds), GetBrush(fill.Color));
                    break;
                case DrawLineCommand line:
                    target.DrawLine(
                        new Vector2((float)line.Start.X, (float)line.Start.Y),
                        new Vector2((float)line.End.X, (float)line.End.Y),
                        GetBrush(line.Color),
                        (float)line.StrokeWidth);
                    break;
                case DrawTextCommand text:
                    DrawText(target, text);
                    break;
                case PushClipCommand pushClip:
                    target.PushAxisAlignedClip(ToRawRect(pushClip.Bounds), AntialiasMode.Aliased);
                    clipDepth++;
                    break;
                case PopClipCommand:
                    if (clipDepth <= 0)
                    {
                        throw new InvalidOperationException("Display-list clip stack is unbalanced.");
                    }
                    target.PopAxisAlignedClip();
                    clipDepth--;
                    break;
                default:
                    throw new NotSupportedException($"Unsupported render command '{command.GetType().Name}'.");
            }
        }

        if (clipDepth != 0)
        {
            throw new InvalidOperationException("Display-list clip stack is unbalanced.");
        }
        target.EndDraw();
    }

    public void RecreateDeviceResources()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CreateRenderTarget();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeTargetResources();
        foreach (var format in _textFormats.Values)
        {
            format.Dispose();
        }
        _textFormats.Clear();
        _writeFactory.Dispose();
        _d2dFactory.Dispose();
        _disposed = true;
    }

    private void CreateRenderTarget()
    {
        DisposeTargetResources();
        var hwndProperties = new HwndRenderTargetProperties
        {
            Hwnd = _windowHandle,
            PixelSize = new SizeI(_pixelWidth, _pixelHeight),
            PresentOptions = PresentOptions.Immediately,
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
        _textFormats.Add(style, format);
        return format;
    }

    private void DrawText(ID2D1RenderTarget target, DrawTextCommand command)
    {
        if (string.IsNullOrEmpty(command.Text) || command.Bounds.Width <= 0d || command.Bounds.Height <= 0d)
        {
            return;
        }

        var bounds = ToRawRect(command.Bounds);
        target.DrawText(
            command.Text,
            (uint)command.Text.Length,
            GetTextFormat(command.Style),
            bounds,
            GetBrush(command.Style.Color),
            DrawTextOptions.Clip,
            MeasuringMode.Natural);
    }

    private static RawRectF ToRawRect(RectD bounds) => new(
        (float)bounds.Left,
        (float)bounds.Top,
        (float)bounds.Right,
        (float)bounds.Bottom);
}
