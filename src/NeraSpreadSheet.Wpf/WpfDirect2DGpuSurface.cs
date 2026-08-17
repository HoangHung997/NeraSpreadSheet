using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Direct2D;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Wpf;
using static Vortice.Direct2D1.D2D1;
using D2DAlphaMode = Vortice.DCommon.AlphaMode;
using DxgiFormat = Vortice.DXGI.Format;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// WPF-native D3DImage surface backed by a shared D3D11 texture and a Direct2D device context.
/// </summary>
internal sealed class WpfDirect2DGpuSurface : DrawingSurface
{
    private Direct2DDisplayListExecutor? _executor;
    private ID2D1Factory1? _d2dFactory;
    private ID2D1Device? _d2dDevice;
    private ID2D1DeviceContext? _d2dContext;
    private ID2D1Bitmap1? _targetBitmap;
    private nint _boundTexturePointer;
    private DisplayList? _displayList;

    public WpfDirect2DGpuSurface()
    {
        DepthStencilFormat = DxgiFormat.Unknown;
        AlwaysRefresh = false;
        LoadContent += OnLoadContent;
        Draw += OnDraw;
        UnloadContent += OnUnloadContent;
    }

    public int CachedTextLayoutCount => _executor?.CachedTextLayoutCount ?? 0;
    public long TextLayoutCacheHits => _executor?.TextLayoutCacheHits ?? 0L;
    public long TextLayoutCacheMisses => _executor?.TextLayoutCacheMisses ?? 0L;
    public long TextLayoutCacheEvictions => _executor?.TextLayoutCacheEvictions ?? 0L;

    public void SetDisplayList(DisplayList? displayList)
    {
        _displayList = displayList;
        Invalidate();
    }

    public void ClearTextLayoutCache() => _executor?.ClearTextLayoutCache();

    private void OnLoadContent(object? sender, DrawingSurfaceEventArgs e)
    {
        ReleaseD2DResources(disposeExecutor: true);
        _executor = new Direct2DDisplayListExecutor(Direct2DHwndDisplayListRenderer.DefaultTextLayoutCacheCapacity);
        _d2dFactory = D2D1CreateFactory<ID2D1Factory1>();
        using var dxgiDevice = e.Device.QueryInterface<IDXGIDevice>();
        _d2dDevice = _d2dFactory.CreateDevice(dxgiDevice);
        _d2dContext = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);
        EnsureTargetBitmap();
    }

    private void OnDraw(object? sender, DrawEventArgs e)
    {
        var executor = _executor;
        if (_displayList is null || _d2dContext is null || executor is null)
        {
            return;
        }

        EnsureTargetBitmap();
        executor.Render(_d2dContext, _displayList);
    }

    private void OnUnloadContent(object? sender, DrawingSurfaceEventArgs e) =>
        ReleaseD2DResources(disposeExecutor: true);

    private void EnsureTargetBitmap()
    {
        var texture = ColorTexture;
        var context = _d2dContext;
        if (texture is null || context is null)
        {
            return;
        }

        var texturePointer = texture.NativePointer;
        if (_targetBitmap is not null && _boundTexturePointer == texturePointer)
        {
            return;
        }

        ReleaseTargetBitmap();
        using var dxgiSurface = texture.QueryInterface<IDXGISurface>();
        var properties = new BitmapProperties1(
            new PixelFormat(DxgiFormat.B8G8R8A8_UNorm, D2DAlphaMode.Premultiplied),
            96f,
            96f,
            BitmapOptions.Target | BitmapOptions.CannotDraw);
        _targetBitmap = context.CreateBitmapFromDxgiSurface(dxgiSurface, properties);
        context.Target = _targetBitmap;
        _boundTexturePointer = texturePointer;
    }

    private void ReleaseTargetBitmap()
    {
        if (_d2dContext is not null)
        {
            _d2dContext.Target = null;
        }
        _targetBitmap?.Dispose();
        _targetBitmap = null;
        _boundTexturePointer = 0;
    }

    private void ReleaseD2DResources(bool disposeExecutor)
    {
        _executor?.InvalidateTargetResources();
        ReleaseTargetBitmap();
        _d2dContext?.Dispose();
        _d2dContext = null;
        _d2dDevice?.Dispose();
        _d2dDevice = null;
        _d2dFactory?.Dispose();
        _d2dFactory = null;
        if (disposeExecutor)
        {
            _executor?.Dispose();
            _executor = null;
        }
    }
}
