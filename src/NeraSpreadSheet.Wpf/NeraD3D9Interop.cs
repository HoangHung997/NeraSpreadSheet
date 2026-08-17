// Portions adapted from Vortice.Wpf (Copyright (c) Amer Koleci and Contributors),
// licensed under the MIT License. See docs/third-party-notices.md.

using System.Windows;
using System.Windows.Interop;
using Vortice.Direct3D11;
using Vortice.Direct3D9;
using Vortice.DXGI;
using static Vortice.Direct3D9.D3D9;
using D3D9Format = Vortice.Direct3D9.Format;
using D3D9PresentInterval = Vortice.Direct3D9.PresentInterval;
using D3D9PresentParameters = Vortice.Direct3D9.PresentParameters;
using D3D9SwapEffect = Vortice.Direct3D9.SwapEffect;
using D3D9Usage = Vortice.Direct3D9.Usage;
using DxgiFormat = Vortice.DXGI.Format;

namespace NeraSpreadSheet.Wpf;

internal static class NeraD3D9DeviceService
{
    private static readonly object SyncRoot = new();
    private static int _activeClients;
    private static IDirect3D9Ex? _d3dContext;
    private static IDirect3DDevice9Ex? _device;

    public static IDirect3DDevice9Ex Device
    {
        get
        {
            lock (SyncRoot)
            {
                return _device
                    ?? throw new InvalidOperationException("The shared Direct3D9Ex device is not initialized.");
            }
        }
    }

    public static void Start(Window parentWindow)
    {
        ArgumentNullException.ThrowIfNull(parentWindow);
        lock (SyncRoot)
        {
            _activeClients++;
            if (_activeClients > 1)
            {
                return;
            }

            try
            {
                _d3dContext = Direct3DCreate9Ex();
                var presentParameters = new D3D9PresentParameters
                {
                    Windowed = true,
                    SwapEffect = D3D9SwapEffect.Discard,
                    DeviceWindowHandle = new WindowInteropHelper(parentWindow).Handle,
                    PresentationInterval = D3D9PresentInterval.Default,
                };
                _device = _d3dContext.CreateDeviceEx(
                    0,
                    DeviceType.Hardware,
                    IntPtr.Zero,
                    CreateFlags.HardwareVertexProcessing |
                    CreateFlags.Multithreaded |
                    CreateFlags.FpuPreserve,
                    presentParameters);
            }
            catch
            {
                _activeClients = 0;
                DisposeDevice();
                throw;
            }
        }
    }

    public static void End()
    {
        lock (SyncRoot)
        {
            if (_activeClients == 0)
            {
                return;
            }

            _activeClients--;
            if (_activeClients != 0)
            {
                return;
            }

            DisposeDevice();
        }
    }

    private static void DisposeDevice()
    {
        _device?.Dispose();
        _device = null;
        _d3dContext?.Dispose();
        _d3dContext = null;
    }
}

internal static class NeraD3D9TextureExtensions
{
    public static bool IsShareable(this ID3D11Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return (texture.Description.MiscFlags & ResourceOptionFlags.Shared) != 0;
    }

    public static D3D9Format GetD3D9Format(this ID3D11Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        return texture.Description.Format switch
        {
            DxgiFormat.R10G10B10A2_UNorm => D3D9Format.A2B10G10R10,
            DxgiFormat.R16G16B16A16_Float => D3D9Format.A16B16G16R16F,
            DxgiFormat.B8G8R8A8_UNorm => D3D9Format.A8R8G8B8,
            _ => D3D9Format.Unknown,
        };
    }

    public static nint GetSharedHandle(this ID3D11Texture2D texture)
    {
        ArgumentNullException.ThrowIfNull(texture);
        using var resource = texture.QueryInterface<IDXGIResource>();
        return resource.SharedHandle;
    }
}

internal sealed class NeraD3D11ImageSource : D3DImage, IDisposable
{
    private IDirect3DTexture9? _renderTarget;
    private bool _disposed;

    public NeraD3D11ImageSource(Window parentWindow)
    {
        NeraD3D9DeviceService.Start(parentWindow);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        ClearRenderTarget();
        NeraD3D9DeviceService.End();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public void SetRenderTarget(ID3D11Texture2D? renderTarget)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ClearRenderTarget();
        if (renderTarget is null)
        {
            return;
        }
        if (!renderTarget.IsShareable())
        {
            throw new ArgumentException("Texture must be created with ResourceOptionFlags.Shared.", nameof(renderTarget));
        }

        var format = renderTarget.GetD3D9Format();
        if (format == D3D9Format.Unknown)
        {
            throw new ArgumentException("Texture format is not compatible with the WPF D3D9 bridge.", nameof(renderTarget));
        }

        var handle = renderTarget.GetSharedHandle();
        if (handle == IntPtr.Zero)
        {
            throw new ArgumentException("The shared texture handle could not be retrieved.", nameof(renderTarget));
        }

        _renderTarget = NeraD3D9DeviceService.Device.CreateTexture(
            renderTarget.Description.Width,
            renderTarget.Description.Height,
            1,
            D3D9Usage.RenderTarget,
            format,
            Pool.Default,
            ref handle);
        using var surface = _renderTarget.GetSurfaceLevel(0);
        Lock();
        try
        {
            SetBackBuffer(D3DResourceType.IDirect3DSurface9, surface.NativePointer);
        }
        finally
        {
            Unlock();
        }
    }

    public void InvalidateImage()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_renderTarget is null || !IsFrontBufferAvailable)
        {
            return;
        }

        Lock();
        try
        {
            AddDirtyRect(new Int32Rect(0, 0, PixelWidth, PixelHeight));
        }
        finally
        {
            Unlock();
        }
    }

    private void ClearRenderTarget()
    {
        if (_renderTarget is null)
        {
            return;
        }

        Lock();
        try
        {
            SetBackBuffer(D3DResourceType.IDirect3DSurface9, IntPtr.Zero);
        }
        finally
        {
            Unlock();
        }

        _renderTarget.Dispose();
        _renderTarget = null;
    }
}
