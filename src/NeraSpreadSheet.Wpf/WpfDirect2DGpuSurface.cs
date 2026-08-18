using System.Windows;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Direct2D;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct2D1.D2D1;

namespace NeraSpreadSheet.Wpf;

internal sealed class WpfDirect2DGpuSurface : NeraD3D11ImageSurface
{
    private ID2D1Factory1? _factory;
    private ID2D1Device? _direct2DDevice;
    private ID2D1DeviceContext? _direct2DContext;
    private ID2D1Bitmap1? _targetBitmap;
    private Direct2DDisplayListExecutor? _executor;
    private DisplayList? _displayList;
    private RectD? _pendingRenderBounds;
    private bool _fullRenderPending = true;

    public int CachedTextLayoutCount => _executor?.CachedTextLayoutCount ?? 0;

    public long TextLayoutCacheHits => _executor?.TextLayoutCacheHits ?? 0L;

    public long TextLayoutCacheMisses => _executor?.TextLayoutCacheMisses ?? 0L;

    public long TextLayoutCacheEvictions =>
        _executor?.TextLayoutCacheEvictions ?? 0L;

    public void SetDisplayList(DisplayList? displayList)
    {
        ThrowIfDisposed();
        if (ReferenceEquals(_displayList, displayList) &&
            _fullRenderPending)
        {
            return;
        }

        _displayList = displayList;
        _pendingRenderBounds = null;
        _fullRenderPending = true;
        InvalidateSurface();
    }

    public void SetDisplayList(
        DisplayList? displayList,
        RectD dirtyBounds)
    {
        ThrowIfDisposed();
        if (displayList is null)
        {
            SetDisplayList(null);
            return;
        }
        if (dirtyBounds.IsEmpty)
        {
            _displayList = displayList;
            return;
        }

        _displayList = displayList;
        if (_fullRenderPending)
        {
            InvalidateSurface();
            return;
        }

        _pendingRenderBounds = _pendingRenderBounds is { } existing
            ? Union(existing, dirtyBounds)
            : dirtyBounds;
        InvalidateSurface(ToInt32Rect(_pendingRenderBounds.Value));
    }

    protected override void OnDeviceCreated(
        ID3D11Device1 device,
        ID3D11DeviceContext1 context)
    {
        _factory = D2D1CreateFactory<ID2D1Factory1>();
        using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
        _direct2DDevice = _factory.CreateDevice(dxgiDevice);
        _direct2DContext = _direct2DDevice.CreateDeviceContext(
            DeviceContextOptions.EnableMultithreadedOptimizations);
        _executor = new Direct2DDisplayListExecutor(
            Direct2DHwndDisplayListRenderer.DefaultTextLayoutCacheCapacity);
        _fullRenderPending = true;
        _pendingRenderBounds = null;
        EnsureTargetBitmap();
    }

    protected override void OnRenderTargetChanging()
    {
        _fullRenderPending = true;
        _pendingRenderBounds = null;
        DisposeTargetBitmap();
    }

    protected override void OnRenderTargetChanged()
    {
        _fullRenderPending = true;
        _pendingRenderBounds = null;
        EnsureTargetBitmap();
    }

    protected override void OnRenderFrame(
        ID3D11Device1 device,
        ID3D11DeviceContext1 context)
    {
        EnsureTargetBitmap();
        var direct2DContext = _direct2DContext;
        var executor = _executor;
        var displayList = _displayList;
        if (direct2DContext is null || executor is null || displayList is null)
        {
            return;
        }

        var renderList = !_fullRenderPending &&
            _pendingRenderBounds is { } dirtyBounds
                ? CreateDirtyClippedDisplayList(displayList, dirtyBounds)
                : displayList;
        direct2DContext.Target = _targetBitmap;
        executor.Render(direct2DContext, renderList);
        _fullRenderPending = false;
        _pendingRenderBounds = null;
    }

    protected override void OnDeviceDestroying()
    {
        _fullRenderPending = true;
        _pendingRenderBounds = null;
        DisposeTargetBitmap();
        _executor?.Dispose();
        _executor = null;
        _direct2DContext?.Dispose();
        _direct2DContext = null;
        _direct2DDevice?.Dispose();
        _direct2DDevice = null;
        _factory?.Dispose();
        _factory = null;
    }

    protected override void DisposeManagedResources()
    {
        _displayList = null;
        _pendingRenderBounds = null;
        _fullRenderPending = true;
    }

    private void EnsureTargetBitmap()
    {
        if (_targetBitmap is not null)
        {
            return;
        }
        var direct2DContext = _direct2DContext;
        var texture = ColorTexture;
        if (direct2DContext is null || texture is null)
        {
            return;
        }

        using var surface = texture.QueryInterface<IDXGISurface>();
        _targetBitmap = direct2DContext.CreateBitmapFromDxgiSurface(
            surface,
            new BitmapProperties1(
                new Vortice.DCommon.PixelFormat(
                    Format.B8G8R8A8_UNorm,
                    Vortice.DCommon.AlphaMode.Premultiplied),
                dpiX: 96f,
                dpiY: 96f,
                BitmapOptions.Target | BitmapOptions.CannotDraw));
        _fullRenderPending = true;
        _pendingRenderBounds = null;
    }

    private void DisposeTargetBitmap()
    {
        if (_direct2DContext is not null)
        {
            _direct2DContext.Target = null;
        }
        _targetBitmap?.Dispose();
        _targetBitmap = null;
    }

    private static DisplayList CreateDirtyClippedDisplayList(
        DisplayList displayList,
        RectD dirtyBounds)
    {
        var builder = new DisplayListBuilder();
        builder.PushClip(dirtyBounds);
        builder.DrawDisplayList(displayList);
        builder.PopClip();
        return builder.Build();
    }

    private static RectD Union(RectD first, RectD second)
    {
        var left = Math.Min(first.Left, second.Left);
        var top = Math.Min(first.Top, second.Top);
        var right = Math.Max(first.Right, second.Right);
        var bottom = Math.Max(first.Bottom, second.Bottom);
        return new RectD(
            left,
            top,
            right - left,
            bottom - top);
    }

    private static Int32Rect ToInt32Rect(RectD bounds)
    {
        var left = (int)Math.Floor(bounds.Left);
        var top = (int)Math.Floor(bounds.Top);
        var right = (int)Math.Ceiling(bounds.Right);
        var bottom = (int)Math.Ceiling(bounds.Bottom);
        return right <= left || bottom <= top
            ? Int32Rect.Empty
            : new Int32Rect(
                left,
                top,
                right - left,
                bottom - top);
    }
}
