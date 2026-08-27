using NeraSpreadSheet.Rendering;
using SharpGen.Runtime;
using Vortice.Direct2D1;
using static Vortice.Direct2D1.D2D1;

namespace NeraSpreadSheet.Rendering.Direct2D;

public sealed class Direct2DHwndDisplayListRenderer : IDisposable
{
    public const int DefaultTextLayoutCacheCapacity = 2048;

    private readonly nint _windowHandle;
    private readonly ID2D1Factory1 _d2dFactory;
    private readonly Direct2DDisplayListExecutor _executor;
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
        _executor = new Direct2DDisplayListExecutor(
            _d2dFactory,
            textLayoutCacheCapacity);
        CreateRenderTarget();
    }

    public int PixelWidth => _pixelWidth;
    public int PixelHeight => _pixelHeight;
    public int CachedTextLayoutCount => _executor.CachedTextLayoutCount;
    public long TextLayoutCacheHits => _executor.TextLayoutCacheHits;
    public long TextLayoutCacheMisses => _executor.TextLayoutCacheMisses;
    public long TextLayoutCacheEvictions => _executor.TextLayoutCacheEvictions;
    public long DeviceRecoveryCount { get; private set; }

    public Direct2DRendererDiagnostics Diagnostics => new(
        _pixelWidth,
        _pixelHeight,
        _executor.TextLayoutCacheCapacity,
        _executor.CachedTextLayoutCount,
        _executor.TextLayoutCacheHits,
        _executor.TextLayoutCacheMisses,
        _executor.TextLayoutCacheEvictions,
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
            _executor.Render(GetRenderTarget(), displayList);
        }
        catch (SharpGenException)
        {
            DeviceRecoveryCount++;
            CreateRenderTarget();
            _executor.Render(GetRenderTarget(), displayList);
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
        _executor.ClearTextLayoutCache();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DisposeRenderTarget();
        _executor.Dispose();
        _d2dFactory.Dispose();
        _disposed = true;
    }

    private ID2D1HwndRenderTarget GetRenderTarget() =>
        _renderTarget ?? throw new InvalidOperationException("Direct2D render target is not initialized.");

    private void CreateRenderTarget()
    {
        DisposeRenderTarget();
        var hwndProperties = new HwndRenderTargetProperties
        {
            Hwnd = _windowHandle,
            PixelSize = new SizeI(_pixelWidth, _pixelHeight),
            PresentOptions = PresentOptions.RetainContents | PresentOptions.Immediately,
        };
        _renderTarget = _d2dFactory.CreateHwndRenderTarget(new RenderTargetProperties(), hwndProperties);
    }

    private void DisposeRenderTarget()
    {
        _executor.InvalidateTargetResources();
        _renderTarget?.Dispose();
        _renderTarget = null;
    }
}
