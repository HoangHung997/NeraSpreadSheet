using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Direct2D;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct2D1.D2D1;
using D2DAlphaMode = Vortice.DCommon.AlphaMode;
using DxgiFormat = Vortice.DXGI.Format;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// WPF-native D3DImage surface backed by a shared D3D11 texture and a Direct2D device context.
/// </summary>
internal sealed class WpfDirect2DGpuSurface : NeraD3D11ImageSurface
{
    private readonly Direct2DDisplayListExecutor _executor = new(Direct2DHwndDisplayListRenderer.DefaultTextLayoutCacheCapacity);
    private ID2D1Factory1? _d2dFactory;
    private ID2D1Device? _d2dDevice;
    private ID2D1DeviceContext? _d2dContext;
    private ID2D1Bitmap1? _targetBitmap;
    private nint _boundTexturePointer;
    private DisplayList? _displayList;

    public WpfDirect2DGpuSurface()
    {
        AlwaysRefresh = false;
    }

    public int CachedTextLayoutCount => _executor.CachedTextLayoutCount;
    public long TextLayoutCacheHits => _executor.TextLayoutCacheHits;
    public long TextLayoutCacheMisses => _executor.TextLayoutCacheMisses;
    public long TextLayoutCacheEvictions => _executor.TextLayoutCacheEvictions;

    public void SetDisplayList(DisplayList? displayList)
    {
        ThrowIfDisposed();
        _displayList = displayList;
        InvalidateSurface();
    }

    public void ClearTextLayoutCache()
    {
        ThrowIfDisposed();
        _executor.ClearTextLayoutCache();
    }

    protected override void OnDeviceCreated(ID3D11Device1 device, ID3D11DeviceContext1 context)
    {
        ReleaseD2DResources();
        _d2dFactory = D2D1CreateFactory<ID2D1Factory1>();
        using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
        _d2dDevice = _d2dFactory.CreateDevice(dxgiDevice);
        _d2dContext = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);
        EnsureTargetBitmap();
    }

    protected override void OnRenderTargetChanging() => ReleaseTargetBitmap();

    protected override void OnRenderTargetChanged() => EnsureTargetBitmap();

    protected override void OnRenderFrame(ID3D11Device1 device, ID3D11DeviceContext1 context)
    {
        if (_displayList is null || _d2dContext is null)
        {
            return;
        }

        EnsureTargetBitmap();
        _executor.Render(_d2dContext, _displayList);
    }

    protected override void OnDeviceDestroying() => ReleaseD2DResources();

    protected override void DisposeManagedResources()
    {
        _displayList = null;
        _executor.Dispose();
    }

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

    private void ReleaseD2DResources()
    {
        _executor.InvalidateTargetResources();
        ReleaseTargetBitmap();
        _d2dContext?.Dispose();
        _d2dContext = null;
        _d2dDevice?.Dispose();
        _d2dDevice = null;
        _d2dFactory?.Dispose();
        _d2dFactory = null;
    }
}
