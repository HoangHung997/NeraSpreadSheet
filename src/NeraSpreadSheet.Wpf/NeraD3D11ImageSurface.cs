using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using static Vortice.Direct3D11.D3D11;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// Idempotent WPF D3DImage host for a shared D3D11 render target.
/// </summary>
internal abstract class NeraD3D11ImageSurface : Image, IDisposable
{
    private ID3D11Device1? _device;
    private ID3D11DeviceContext1? _deviceContext;
    private NeraD3D11ImageSource? _imageSource;
    private Window? _attachedWindow;
    private Int32Rect? _pendingDirtyRectangle;
    private bool _isD3DStarted;
    private bool _renderingSubscribed;
    private bool _contentNeedsRefresh;
    private bool _disposed;

    protected NeraD3D11ImageSurface()
    {
        Stretch = Stretch.Fill;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public bool AlwaysRefresh { get; set; }

    public int TextureWidth { get; private set; }

    public int TextureHeight { get; private set; }

    public Int32Rect? LastPresentedDirtyRectangle { get; private set; }

    protected ID3D11Texture2D? ColorTexture { get; private set; }

    protected ID3D11RenderTargetView? ColorTextureView { get; private set; }

    public void InvalidateSurface()
    {
        ThrowIfDisposed();
        _contentNeedsRefresh = true;
        _pendingDirtyRectangle = null;
    }

    public void InvalidateSurface(Int32Rect dirtyRectangle)
    {
        ThrowIfDisposed();
        if (dirtyRectangle.IsEmpty)
        {
            return;
        }

        if (!_contentNeedsRefresh)
        {
            _pendingDirtyRectangle = dirtyRectangle;
        }
        else if (_pendingDirtyRectangle is { } existing)
        {
            _pendingDirtyRectangle = Union(existing, dirtyRectangle);
        }
        _contentNeedsRefresh = true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        VerifyAccess();
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        EndD3D();
        DisposeManagedResources();
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    protected void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(_disposed, this);

    protected virtual void OnDeviceCreated(
        ID3D11Device1 device,
        ID3D11DeviceContext1 context)
    {
    }

    protected virtual void OnRenderTargetChanging()
    {
    }

    protected virtual void OnRenderTargetChanged()
    {
    }

    protected virtual void OnRenderFrame(
        ID3D11Device1 device,
        ID3D11DeviceContext1 context)
    {
    }

    protected virtual void OnDeviceDestroying()
    {
    }

    protected virtual void DisposeManagedResources()
    {
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (!_isD3DStarted || _disposed)
        {
            return;
        }

        CreateAndBindRenderTarget(notifyTargetChange: true);
        _contentNeedsRefresh = true;
        _pendingDirtyRectangle = null;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_disposed || DesignerProperties.GetIsInDesignMode(this))
        {
            return;
        }

        StartD3D();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_disposed || DesignerProperties.GetIsInDesignMode(this))
        {
            return;
        }

        EndD3D();
    }

    private void OnWindowClosed(object? sender, EventArgs e) => EndD3D();

    private void StartD3D()
    {
        if (_isD3DStarted)
        {
            StartRendering();
            return;
        }

        var window = Window.GetWindow(this)
            ?? throw new InvalidOperationException(
                "A loaded WPF Window is required for the shared-texture renderer.");
        ID3D11Device? temporaryDevice = null;
        ID3D11DeviceContext? temporaryContext = null;
        ID3D11Device1 device;
        ID3D11DeviceContext1 context;
        try
        {
            D3D11CreateDevice(
                IntPtr.Zero,
                DriverType.Hardware,
                DeviceCreationFlags.BgraSupport,
                FeatureLevel.Level_11_0,
                out temporaryDevice,
                out temporaryContext).CheckError();
            if (temporaryDevice is null || temporaryContext is null)
            {
                throw new InvalidOperationException(
                    "D3D11CreateDevice returned incomplete device resources.");
            }
            device = temporaryDevice.QueryInterface<ID3D11Device1>();
            context = temporaryContext.QueryInterface<ID3D11DeviceContext1>();
            _device = device;
            _deviceContext = context;
        }
        finally
        {
            temporaryContext?.Dispose();
            temporaryDevice?.Dispose();
        }

        _attachedWindow = window;
        _imageSource = new NeraD3D11ImageSource(window);
        _imageSource.IsFrontBufferAvailableChanged +=
            OnFrontBufferAvailableChanged;
        window.Closed += OnWindowClosed;
        _isD3DStarted = true;

        try
        {
            CreateAndBindRenderTarget(notifyTargetChange: false);
            Source = _imageSource;
            OnDeviceCreated(device, context);
            _contentNeedsRefresh = true;
            _pendingDirtyRectangle = null;
            StartRendering();
        }
        catch
        {
            EndD3D();
            throw;
        }
    }

    private void EndD3D()
    {
        if (!_isD3DStarted)
        {
            return;
        }

        _isD3DStarted = false;
        StopRendering();
        if (_attachedWindow is not null)
        {
            _attachedWindow.Closed -= OnWindowClosed;
            _attachedWindow = null;
        }
        if (_imageSource is not null)
        {
            _imageSource.IsFrontBufferAvailableChanged -=
                OnFrontBufferAvailableChanged;
        }

        try
        {
            OnDeviceDestroying();
        }
        finally
        {
            Source = null;
            _imageSource?.Dispose();
            _imageSource = null;
            DisposeRenderTarget();

            if (_deviceContext is not null)
            {
                _deviceContext.ClearState();
                _deviceContext.Flush();
                _deviceContext.Dispose();
                _deviceContext = null;
            }
            _device?.Dispose();
            _device = null;
            TextureWidth = 0;
            TextureHeight = 0;
            _contentNeedsRefresh = false;
            _pendingDirtyRectangle = null;
            LastPresentedDirtyRectangle = null;
        }
    }

    private void CreateAndBindRenderTarget(bool notifyTargetChange)
    {
        var device = _device;
        var imageSource = _imageSource;
        if (!_isD3DStarted || device is null || imageSource is null)
        {
            return;
        }

        if (notifyTargetChange)
        {
            OnRenderTargetChanging();
        }
        imageSource.SetRenderTarget(null);
        DisposeRenderTarget();

        TextureWidth = Math.Max(1, (int)Math.Ceiling(ActualWidth));
        TextureHeight = Math.Max(1, (int)Math.Ceiling(ActualHeight));
        ColorTexture = device.CreateTexture2D(new Texture2DDescription
        {
            BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
            Format = Format.B8G8R8A8_UNorm,
            Width = (uint)TextureWidth,
            Height = (uint)TextureHeight,
            MipLevels = 1,
            SampleDescription = new SampleDescription(1, 0),
            Usage = ResourceUsage.Default,
            MiscFlags = ResourceOptionFlags.Shared,
            CPUAccessFlags = CpuAccessFlags.None,
            ArraySize = 1,
        });
        ColorTextureView = device.CreateRenderTargetView(ColorTexture);
        imageSource.SetRenderTarget(ColorTexture);
        _pendingDirtyRectangle = null;
        if (notifyTargetChange)
        {
            OnRenderTargetChanged();
        }
    }

    private void DisposeRenderTarget()
    {
        ColorTextureView?.Dispose();
        ColorTextureView = null;
        ColorTexture?.Dispose();
        ColorTexture = null;
    }

    private void StartRendering()
    {
        if (_renderingSubscribed)
        {
            return;
        }

        CompositionTarget.Rendering += OnRendering;
        _renderingSubscribed = true;
    }

    private void StopRendering()
    {
        if (!_renderingSubscribed)
        {
            return;
        }

        CompositionTarget.Rendering -= OnRendering;
        _renderingSubscribed = false;
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        if (!_isD3DStarted ||
            (!_contentNeedsRefresh && !AlwaysRefresh))
        {
            return;
        }

        var device = _device;
        var context = _deviceContext;
        var targetView = ColorTextureView;
        if (device is null || context is null || targetView is null)
        {
            return;
        }

        var dirtyRectangle = _pendingDirtyRectangle;
        context.OMSetRenderTargets(targetView, null);
        context.RSSetViewport(0, 0, TextureWidth, TextureHeight);
        context.RSSetScissorRect(0, 0, TextureWidth, TextureHeight);
        OnRenderFrame(device, context);
        context.Flush();
        LastPresentedDirtyRectangle =
            _imageSource?.InvalidateImage(dirtyRectangle);
        _contentNeedsRefresh = false;
        _pendingDirtyRectangle = null;
    }

    private void OnFrontBufferAvailableChanged(
        object? sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (!_isD3DStarted || _imageSource is null)
        {
            return;
        }

        if (_imageSource.IsFrontBufferAvailable)
        {
            CreateAndBindRenderTarget(notifyTargetChange: true);
            _contentNeedsRefresh = true;
            _pendingDirtyRectangle = null;
            StartRendering();
        }
        else
        {
            StopRendering();
        }
    }

    private static Int32Rect Union(Int32Rect first, Int32Rect second)
    {
        var left = Math.Min(first.X, second.X);
        var top = Math.Min(first.Y, second.Y);
        var right = Math.Max(
            first.X + first.Width,
            second.X + second.Width);
        var bottom = Math.Max(
            first.Y + first.Height,
            second.Y + second.Height);
        return new Int32Rect(
            left,
            top,
            right - left,
            bottom - top);
    }
}
