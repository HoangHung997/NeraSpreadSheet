using NeraSpreadSheet.Rendering;
using SharpGen.Runtime;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct2D1.D2D1;
using static Vortice.Direct3D11.D3D11;
using static Vortice.DXGI.DXGI;
using D2DAlphaMode = Vortice.DCommon.AlphaMode;
using DxgiAlphaMode = Vortice.DXGI.AlphaMode;
using DxgiFormat = Vortice.DXGI.Format;

namespace NeraSpreadSheet.Rendering.Direct2D;

/// <summary>
/// Flip-model D3D11/DXGI presentation surface feeding a Direct2D device context.
/// </summary>
public sealed class Direct2DSwapChainDisplayListRenderer : IDisposable
{
    private const int BufferCount = 2;
    private static readonly FeatureLevel[] SupportedFeatureLevels =
    [
        FeatureLevel.Level_11_1,
        FeatureLevel.Level_11_0,
        FeatureLevel.Level_10_1,
        FeatureLevel.Level_10_0,
    ];

    private readonly nint _windowHandle;
    private readonly ID2D1Factory1 _d2dFactory;
    private readonly Direct2DDisplayListExecutor _executor;
    private IDXGIFactory2? _dxgiFactory;
    private ID3D11Device1? _d3dDevice;
    private ID3D11DeviceContext1? _d3dContext;
    private IDXGISwapChain1? _swapChain;
    private ID2D1Device? _d2dDevice;
    private ID2D1DeviceContext? _d2dContext;
    private ID2D1Bitmap1? _targetBitmap;
    private FeatureLevel _featureLevel;
    private int _pixelWidth;
    private int _pixelHeight;
    private bool _disposed;

    public Direct2DSwapChainDisplayListRenderer(
        nint windowHandle,
        int pixelWidth,
        int pixelHeight,
        int textLayoutCacheCapacity = Direct2DHwndDisplayListRenderer.DefaultTextLayoutCacheCapacity)
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
        CreateDeviceStack();
    }

    public int PixelWidth => _pixelWidth;
    public int PixelHeight => _pixelHeight;
    public bool VSync { get; set; } = true;
    public string DeviceFeatureLevel => _featureLevel.ToString();
    public string AdapterName { get; private set; } = string.Empty;
    public long DeviceRecoveryCount { get; private set; }
    public int CachedTextLayoutCount => _executor.CachedTextLayoutCount;
    public long TextLayoutCacheHits => _executor.TextLayoutCacheHits;
    public long TextLayoutCacheMisses => _executor.TextLayoutCacheMisses;
    public long TextLayoutCacheEvictions => _executor.TextLayoutCacheEvictions;

    public Direct2DSwapChainRendererDiagnostics Diagnostics => new(
        _pixelWidth,
        _pixelHeight,
        AdapterName,
        DeviceFeatureLevel,
        VSync,
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
        ResizeSwapChain();
    }

    public void Render(DisplayList displayList)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(displayList);

        try
        {
            RenderAndPresent(displayList);
        }
        catch (SharpGenException)
        {
            DeviceRecoveryCount++;
            CreateDeviceStack();
            RenderAndPresent(displayList);
        }
    }

    public void RecreateDeviceResources()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        CreateDeviceStack();
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

        DisposeDeviceStack();
        _executor.Dispose();
        _d2dFactory.Dispose();
        _disposed = true;
    }

    private void RenderAndPresent(DisplayList displayList)
    {
        var context = _d2dContext
            ?? throw new InvalidOperationException("Direct2D device context is not initialized.");
        var swapChain = _swapChain
            ?? throw new InvalidOperationException("DXGI swap chain is not initialized.");

        _executor.Render(context, displayList);
        swapChain.Present(VSync ? 1u : 0u, PresentFlags.None).CheckError();
    }

    private void CreateDeviceStack()
    {
        DisposeDeviceStack();
        _dxgiFactory = CreateDXGIFactory1<IDXGIFactory2>();

        using var adapter = TryGetHardwareAdapter(_dxgiFactory);
        AdapterName = adapter?.Description1.Description ?? "Default hardware adapter";
        CreateD3DDevice(adapter);
        CreateSwapChain();
        CreateD2DDeviceContext();
        CreateTargetBitmap();
    }

    private void CreateD3DDevice(IDXGIAdapter1? adapter)
    {
        var flags = DeviceCreationFlags.BgraSupport;
        Result result;
        ID3D11Device tempDevice;
        ID3D11DeviceContext tempContext;
        FeatureLevel featureLevel;

        if (adapter is not null)
        {
            result = D3D11CreateDevice(
                adapter,
                DriverType.Unknown,
                flags,
                SupportedFeatureLevels,
                out tempDevice,
                out featureLevel,
                out tempContext);
        }
        else
        {
            result = D3D11CreateDevice(
                IntPtr.Zero,
                DriverType.Hardware,
                flags,
                SupportedFeatureLevels,
                out tempDevice,
                out featureLevel,
                out tempContext);
        }

        if (result.Failure)
        {
            result = D3D11CreateDevice(
                IntPtr.Zero,
                DriverType.Warp,
                flags,
                SupportedFeatureLevels,
                out tempDevice,
                out featureLevel,
                out tempContext);
            result.CheckError();
            AdapterName = "Microsoft WARP";
        }

        _featureLevel = featureLevel;
        _d3dDevice = tempDevice.QueryInterface<ID3D11Device1>();
        _d3dContext = tempContext.QueryInterface<ID3D11DeviceContext1>();
        tempContext.Dispose();
        tempDevice.Dispose();
    }

    private void CreateSwapChain()
    {
        var factory = _dxgiFactory
            ?? throw new InvalidOperationException("DXGI factory is not initialized.");
        var device = _d3dDevice
            ?? throw new InvalidOperationException("D3D11 device is not initialized.");

        var description = new SwapChainDescription1
        {
            Width = (uint)_pixelWidth,
            Height = (uint)_pixelHeight,
            Format = DxgiFormat.B8G8R8A8_UNorm,
            BufferCount = BufferCount,
            BufferUsage = Usage.RenderTargetOutput,
            SampleDescription = SampleDescription.Default,
            Scaling = Scaling.Stretch,
            SwapEffect = SwapEffect.FlipDiscard,
            AlphaMode = DxgiAlphaMode.Ignore,
        };
        var fullscreen = new SwapChainFullscreenDescription { Windowed = true };
        _swapChain = factory.CreateSwapChainForHwnd(device, _windowHandle, description, fullscreen);
        factory.MakeWindowAssociation(_windowHandle, WindowAssociationFlags.IgnoreAltEnter);
    }

    private void CreateD2DDeviceContext()
    {
        var device = _d3dDevice
            ?? throw new InvalidOperationException("D3D11 device is not initialized.");
        using var dxgiDevice = device.QueryInterface<IDXGIDevice>();
        _d2dDevice = _d2dFactory.CreateDevice(dxgiDevice);
        _d2dContext = _d2dDevice.CreateDeviceContext(DeviceContextOptions.None);
    }

    private void CreateTargetBitmap()
    {
        var swapChain = _swapChain
            ?? throw new InvalidOperationException("DXGI swap chain is not initialized.");
        var context = _d2dContext
            ?? throw new InvalidOperationException("Direct2D device context is not initialized.");

        using var surface = swapChain.GetBuffer<IDXGISurface>(0);
        var bitmapProperties = new BitmapProperties1(
            new PixelFormat(DxgiFormat.B8G8R8A8_UNorm, D2DAlphaMode.Premultiplied),
            96f,
            96f,
            BitmapOptions.Target | BitmapOptions.CannotDraw);
        _targetBitmap = context.CreateBitmapFromDxgiSurface(surface, bitmapProperties);
        context.Target = _targetBitmap;
    }

    private void ResizeSwapChain()
    {
        var swapChain = _swapChain
            ?? throw new InvalidOperationException("DXGI swap chain is not initialized.");
        ReleaseTargetBitmap();
        swapChain.ResizeBuffers(
            BufferCount,
            (uint)_pixelWidth,
            (uint)_pixelHeight,
            DxgiFormat.B8G8R8A8_UNorm,
            SwapChainFlags.None).CheckError();
        CreateTargetBitmap();
    }

    private void ReleaseTargetBitmap()
    {
        _executor.InvalidateTargetResources();
        if (_d2dContext is not null)
        {
            _d2dContext.Target = null;
        }
        _targetBitmap?.Dispose();
        _targetBitmap = null;
    }

    private void DisposeDeviceStack()
    {
        ReleaseTargetBitmap();
        _d2dContext?.Dispose();
        _d2dContext = null;
        _d2dDevice?.Dispose();
        _d2dDevice = null;
        _swapChain?.Dispose();
        _swapChain = null;

        if (_d3dContext is not null)
        {
            _d3dContext.ClearState();
            _d3dContext.Flush();
            _d3dContext.Dispose();
            _d3dContext = null;
        }

        _d3dDevice?.Dispose();
        _d3dDevice = null;
        _dxgiFactory?.Dispose();
        _dxgiFactory = null;
    }

    private static IDXGIAdapter1? TryGetHardwareAdapter(IDXGIFactory2 factory)
    {
        using var factory6 = factory.QueryInterfaceOrNull<IDXGIFactory6>();
        if (factory6 is not null)
        {
            for (uint index = 0;
                factory6.EnumAdapterByGpuPreference(index, GpuPreference.HighPerformance, out IDXGIAdapter1? adapter).Success;
                index++)
            {
                if (adapter is null)
                {
                    continue;
                }
                if ((adapter.Description1.Flags & AdapterFlags.Software) == AdapterFlags.None)
                {
                    return adapter;
                }
                adapter.Dispose();
            }
        }

        for (uint index = 0; factory.EnumAdapters1(index, out IDXGIAdapter1? adapter).Success; index++)
        {
            if (adapter is null)
            {
                continue;
            }
            if ((adapter.Description1.Flags & AdapterFlags.Software) == AdapterFlags.None)
            {
                return adapter;
            }
            adapter.Dispose();
        }

        return null;
    }
}
