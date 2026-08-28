#if MACCATALYST
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CoreAnimation;
using CoreGraphics;
using Foundation;
using Metal;
using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using UIKit;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Mac Catalyst GPU handler backed by a plain UIKit view plus CAMetalLayer.
/// Current hosted macOS 26 runtimes reject both SkiaSharp's SKMetalView hierarchy
/// and MetalKit.MTKView during UIView trait initialization. Keeping Metal in a
/// Core Animation sublayer avoids that MetalKit boundary while preserving the
/// Skia Metal render path and a single native spreadsheet view.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "MAUI handlers release renderer and touch resources from DisconnectHandler, which is the framework lifecycle boundary.")]
internal sealed class NeraMacCatalystSKGLViewHandler :
    ViewHandler<ISKGLView, UIView>
{
    private static readonly PropertyMapper<ISKGLView, NeraMacCatalystSKGLViewHandler>
        NeraMapper = new(ViewHandler.ViewMapper)
        {
            [nameof(ISKGLView.EnableTouchEvents)] = MapEnableTouchEvents,
            [nameof(ISKGLView.IgnorePixelScaling)] = MapIgnorePixelScaling,
            [nameof(ISKGLView.HasRenderLoop)] = MapHasRenderLoop,
        };

    private static readonly CommandMapper<ISKGLView, NeraMacCatalystSKGLViewHandler>
        NeraCommandMapper = new(ViewHandler.ViewCommandMapper)
        {
            [nameof(ISKGLView.InvalidateSurface)] = OnInvalidateSurface,
        };

    private NeraMetalRenderer? _renderer;
    private NeraMacCatalystTouchHandler? _touchHandler;
    private NeraSpreadsheetView? _subscribedView;
    private SKSizeI _lastCanvasSize;
    private GRContext? _lastGrContext;

    public NeraMacCatalystSKGLViewHandler()
        : base(NeraMapper, NeraCommandMapper)
    {
    }

    protected override UIView CreatePlatformView() =>
        new(CGRect.Empty)
        {
            BackgroundColor = UIColor.Clear,
            Opaque = false,
            ClipsToBounds = true,
        };

    protected override void ConnectHandler(UIView platformView)
    {
        base.ConnectHandler(platformView);
        if (VirtualView is NeraSpreadsheetView neraView)
        {
            NeraMacCatalystGpuDiagnostics.Clear(neraView);
            _subscribedView = neraView;
            neraView.SizeChanged += OnVirtualViewSizeChanged;
        }

        _renderer = new NeraMetalRenderer(this, platformView);
        _renderer.SetRenderLoop(VirtualView.HasRenderLoop);
        UpdateTouchEvents(platformView, VirtualView.EnableTouchEvents);
        _renderer.RequestRender();
    }

    protected override void DisconnectHandler(UIView platformView)
    {
        if (_subscribedView is not null)
        {
            _subscribedView.SizeChanged -= OnVirtualViewSizeChanged;
            _subscribedView = null;
        }

        _renderer?.Dispose();
        _renderer = null;
        if (_touchHandler is not null)
        {
            _touchHandler.SetEnabled(platformView, false);
            _touchHandler.Dispose();
            _touchHandler = null;
        }
        _lastCanvasSize = default;
        _lastGrContext = null;
        base.DisconnectHandler(platformView);
    }

    private void OnVirtualViewSizeChanged(object? sender, EventArgs e) =>
        _renderer?.RequestRender();

    private void Paint(
        UIView platformView,
        GRContext grContext,
        SKSurface surface,
        GRBackendRenderTarget renderTarget,
        GRSurfaceOrigin origin)
    {
        var view = VirtualView;
        if (view is null)
        {
            return;
        }

        var rawInfo = new SKImageInfo(
            renderTarget.Width,
            renderTarget.Height,
            SKColorType.Bgra8888);
        var info = rawInfo;
        if (view.IgnorePixelScaling)
        {
            var logicalWidth = (int)Math.Round((double)platformView.Bounds.Width);
            var logicalHeight = (int)Math.Round((double)platformView.Bounds.Height);
            var contentScale = (double)platformView.ContentScaleFactor;
            if (logicalWidth > 0 &&
                logicalHeight > 0 &&
                double.IsFinite(contentScale) &&
                contentScale > 0d)
            {
                surface.Canvas.Scale((float)contentScale);
                info = rawInfo.WithSize(new SKSizeI(logicalWidth, logicalHeight));
            }
        }

        var canvasSize = info.Size;
        if (_lastCanvasSize != canvasSize)
        {
            _lastCanvasSize = canvasSize;
            view.OnCanvasSizeChanged(canvasSize);
        }

        if (!ReferenceEquals(_lastGrContext, grContext))
        {
            _lastGrContext = grContext;
            view.OnGRContextChanged(grContext);
        }

        view.OnPaintSurface(new SKPaintGLSurfaceEventArgs(
            surface,
            renderTarget,
            origin,
            info,
            rawInfo));
    }

    private void UpdateTouchEvents(UIView platformView, bool enabled)
    {
        _touchHandler ??= new NeraMacCatalystTouchHandler(
            OnTouch,
            ScaleTouchPoint);
        _touchHandler.SetEnabled(platformView, enabled);
    }

    private void OnTouch(SKTouchEventArgs e) => VirtualView?.OnTouch(e);

    private SKPoint ScaleTouchPoint(double x, double y)
    {
        if (VirtualView is { IgnorePixelScaling: false } && PlatformView is { } platformView)
        {
            var scale = (double)platformView.ContentScaleFactor;
            if (double.IsFinite(scale) && scale > 0d)
            {
                x *= scale;
                y *= scale;
            }
        }
        return new SKPoint((float)x, (float)y);
    }

    private static void OnInvalidateSurface(
        NeraMacCatalystSKGLViewHandler handler,
        ISKGLView view,
        object? args) =>
        handler._renderer?.RequestRender();

    private static void MapEnableTouchEvents(
        NeraMacCatalystSKGLViewHandler handler,
        ISKGLView view)
    {
        if (handler.PlatformView is { } platformView)
        {
            handler.UpdateTouchEvents(platformView, view.EnableTouchEvents);
        }
    }

    private static void MapIgnorePixelScaling(
        NeraMacCatalystSKGLViewHandler handler,
        ISKGLView view) =>
        handler._renderer?.RequestRender();

    private static void MapHasRenderLoop(
        NeraMacCatalystSKGLViewHandler handler,
        ISKGLView view) =>
        handler._renderer?.SetRenderLoop(view.HasRenderLoop);

    private sealed class NeraMetalRenderer : IDisposable
    {
        private NeraMacCatalystSKGLViewHandler? _handler;
        private UIView? _platformView;
        private readonly CAMetalLayer _metalLayer;
        private readonly GRMtlBackendContext _backendContext;
        private CADisplayLink? _displayLink;
        private GRContext? _context;
        private int _renderPending;
        private bool _renderLoopRequested;
        private bool _disposed;

        internal NeraMetalRenderer(
            NeraMacCatalystSKGLViewHandler handler,
            UIView platformView)
        {
            _handler = handler;
            _platformView = platformView;

            var device = MTLDevice.SystemDefault
                ?? throw new PlatformNotSupportedException(
                    "Metal is not available on this Mac Catalyst runtime.");
            var queue = device.CreateCommandQueue()
                ?? throw new PlatformNotSupportedException(
                    "The Metal device could not create a command queue.");
            _backendContext = new GRMtlBackendContext
            {
                Device = device,
                Queue = queue,
            };

            _metalLayer = new CAMetalLayer
            {
                Device = device,
                PixelFormat = MTLPixelFormat.BGRA8Unorm,
                FramebufferOnly = false,
                Opaque = false,
                AllowsNextDrawableTimeout = true,
            };
            platformView.Layer.AddSublayer(_metalLayer);
        }

        internal void SetRenderLoop(bool enabled)
        {
            if (_disposed)
            {
                return;
            }

            _renderLoopRequested = enabled;
            UpdateDisplayLinkState();
            if (!enabled)
            {
                RequestRender();
            }
        }

        internal void RequestRender()
        {
            if (_disposed)
            {
                return;
            }

            NeraMacCatalystGpuDiagnostics.TraceStage("request-render");
            if (Interlocked.Exchange(ref _renderPending, 1) != 0)
            {
                return;
            }

            if (_platformView is null)
            {
                Interlocked.Exchange(ref _renderPending, 0);
                return;
            }

            NeraMacCatalystGpuDiagnostics.TraceStage("request-render-dispatch-maui-mainthread");
            Microsoft.Maui.ApplicationModel.MainThread.BeginInvokeOnMainThread(() =>
            {
                NeraMacCatalystGpuDiagnostics.TraceStage("request-render-mainthread");
                Interlocked.Exchange(ref _renderPending, 0);
                DrawSafely();
            });
            NeraMacCatalystGpuDiagnostics.TraceStage("request-render-dispatch-returned");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_displayLink is { } displayLink)
            {
                displayLink.Paused = true;
                displayLink.Invalidate();
                displayLink.Dispose();
                _displayLink = null;
            }
            _context?.Dispose();
            _context = null;
            _metalLayer.RemoveFromSuperLayer();
            _metalLayer.Dispose();
            if (_backendContext.Queue is IDisposable disposableQueue)
            {
                disposableQueue.Dispose();
            }
            _platformView = null;
            _handler = null;
        }

        private void DrawFromDisplayLink()
        {
            if (!_disposed && _renderLoopRequested)
            {
                NeraMacCatalystGpuDiagnostics.TraceStage("display-link");
                DrawSafely();
            }
        }

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Exceptions must not escape a Core Animation display callback; the failure is retained in Nera diagnostics and the render loop is stopped.")]
        private void DrawSafely()
        {
            try
            {
                NeraMacCatalystGpuDiagnostics.TraceStage("draw-safely-enter");
                if (DrawCore())
                {
                    NeraMacCatalystGpuDiagnostics.TraceStage("draw-core-success");
                }
                else
                {
                    NeraMacCatalystGpuDiagnostics.TraceStage("draw-core-no-frame");
                }
            }
            catch (Exception exception)
            {
                NeraMacCatalystGpuDiagnostics.TraceStage($"managed-exception:{exception.GetType().Name}");
                if (_handler?.VirtualView is NeraSpreadsheetView neraView)
                {
                    NeraMacCatalystGpuDiagnostics.RecordFailure(neraView, exception);
                }
                Trace.TraceError(
                    "Nera Mac Catalyst Metal frame failed: {0}",
                    exception);
                _renderLoopRequested = false;
                UpdateDisplayLinkState();
            }
        }

        private bool DrawCore()
        {
            NeraMacCatalystGpuDiagnostics.TraceStage("draw-core-enter");
            var handler = _handler;
            var platformView = _platformView;
            var queue = _backendContext.Queue;
            if (handler is null ||
                platformView is null ||
                _backendContext.Device is null ||
                queue is null ||
                platformView.Window is null)
            {
                NeraMacCatalystGpuDiagnostics.TraceStage("draw-core-prerequisite-missing");
                return false;
            }

            NeraMacCatalystGpuDiagnostics.TraceStage("before-update-layer-geometry");
            if (!UpdateLayerGeometry())
            {
                NeraMacCatalystGpuDiagnostics.TraceStage("layer-geometry-not-ready");
                return false;
            }
            NeraMacCatalystGpuDiagnostics.TraceStage(
                $"after-update-layer-geometry:{_metalLayer.DrawableSize.Width:F0}x{_metalLayer.DrawableSize.Height:F0}");

            NeraMacCatalystGpuDiagnostics.TraceStage("before-next-drawable");
            using var drawable = _metalLayer.NextDrawable();
            NeraMacCatalystGpuDiagnostics.TraceStage("after-next-drawable");

            NeraMacCatalystGpuDiagnostics.TraceStage("before-drawable-texture");
            var texture = drawable?.Texture;
            NeraMacCatalystGpuDiagnostics.TraceStage("after-drawable-texture");
            if (drawable is null || texture is null)
            {
                NeraMacCatalystGpuDiagnostics.TraceStage("drawable-or-texture-null");
                return false;
            }

            var width = checked((int)texture.Width);
            var height = checked((int)texture.Height);
            NeraMacCatalystGpuDiagnostics.TraceStage($"texture-size:{width}x{height}");
            if (width <= 0 || height <= 0)
            {
                NeraMacCatalystGpuDiagnostics.TraceStage("texture-size-invalid");
                return false;
            }

            if (_context is null)
            {
                NeraMacCatalystGpuDiagnostics.TraceStage("before-grcontext-create");
                _context = GRContext.CreateMetal(_backendContext);
                NeraMacCatalystGpuDiagnostics.TraceStage("after-grcontext-create");
            }
            var context = _context;
            if (context is null)
            {
                NeraMacCatalystGpuDiagnostics.TraceStage("grcontext-null");
                return false;
            }

            const GRSurfaceOrigin origin = GRSurfaceOrigin.TopLeft;
            NeraMacCatalystGpuDiagnostics.TraceStage("before-metal-texture-info");
            var metalInfo = new GRMtlTextureInfo(texture);
            NeraMacCatalystGpuDiagnostics.TraceStage("after-metal-texture-info");

            NeraMacCatalystGpuDiagnostics.TraceStage("before-render-target");
            using var renderTarget = new GRBackendRenderTarget(
                width,
                height,
                metalInfo);
            NeraMacCatalystGpuDiagnostics.TraceStage("after-render-target");

            NeraMacCatalystGpuDiagnostics.TraceStage("before-surface-create");
            using var surface = SKSurface.Create(
                context,
                renderTarget,
                origin,
                SKColorType.Bgra8888);
            NeraMacCatalystGpuDiagnostics.TraceStage("after-surface-create");
            if (surface is null)
            {
                NeraMacCatalystGpuDiagnostics.TraceStage("surface-null");
                return false;
            }

            NeraMacCatalystGpuDiagnostics.TraceStage("before-paint");
            handler.Paint(platformView, context, surface, renderTarget, origin);
            NeraMacCatalystGpuDiagnostics.TraceStage("after-paint");

            NeraMacCatalystGpuDiagnostics.TraceStage("before-flush");
            surface.Canvas.Flush();
            surface.Flush();
            context.Flush();
            NeraMacCatalystGpuDiagnostics.TraceStage("after-flush");

            NeraMacCatalystGpuDiagnostics.TraceStage("before-command-buffer");
            using var commandBuffer = queue.CommandBuffer();
            NeraMacCatalystGpuDiagnostics.TraceStage("after-command-buffer");
            if (commandBuffer is null)
            {
                NeraMacCatalystGpuDiagnostics.TraceStage("command-buffer-null");
                return false;
            }

            NeraMacCatalystGpuDiagnostics.TraceStage("before-present-drawable");
            commandBuffer.PresentDrawable(drawable);
            NeraMacCatalystGpuDiagnostics.TraceStage("after-present-drawable");
            NeraMacCatalystGpuDiagnostics.TraceStage("before-command-buffer-commit");
            commandBuffer.Commit();
            NeraMacCatalystGpuDiagnostics.TraceStage("after-command-buffer-commit");
            return true;
        }

        private bool UpdateLayerGeometry()
        {
            var platformView = _platformView;
            if (platformView is null)
            {
                return false;
            }

            var bounds = platformView.Bounds;
            var scale = (double)platformView.ContentScaleFactor;
            if (!double.IsFinite(scale) || scale <= 0d)
            {
                scale = 1d;
            }

            var width = (double)bounds.Width * scale;
            var height = (double)bounds.Height * scale;
            if (!double.IsFinite(width) ||
                !double.IsFinite(height) ||
                width <= 0d ||
                height <= 0d)
            {
                return false;
            }

            _metalLayer.Frame = bounds;
            _metalLayer.ContentsScale = (nfloat)scale;
            _metalLayer.DrawableSize = new CGSize(width, height);
            return true;
        }

        private void UpdateDisplayLinkState()
        {
            if (_disposed)
            {
                return;
            }

            if (!_renderLoopRequested)
            {
                if (_displayLink is { } pausedDisplayLink)
                {
                    pausedDisplayLink.Paused = true;
                }
                return;
            }

            var displayLink = _displayLink;
            if (displayLink is null)
            {
                displayLink = CADisplayLink.Create(DrawFromDisplayLink);
                displayLink.Paused = true;
                displayLink.AddToRunLoop(NSRunLoop.Main, NSRunLoopMode.Common);
                _displayLink = displayLink;
            }

            displayLink.Paused = false;
        }
    }

    private sealed class NeraMacCatalystTouchHandler : UIGestureRecognizer
    {
        private Action<SKTouchEventArgs>? _onTouch;
        private Func<double, double, SKPoint>? _scalePixels;

        internal NeraMacCatalystTouchHandler(
            Action<SKTouchEventArgs> onTouch,
            Func<double, double, SKPoint> scalePixels)
        {
            _onTouch = onTouch;
            _scalePixels = scalePixels;
        }

        internal void SetEnabled(UIView view, bool enabled)
        {
            view.UserInteractionEnabled = enabled;
            if (enabled && view.GestureRecognizers?.Contains(this) != true)
            {
                view.AddGestureRecognizer(this);
            }
            else if (!enabled && view.GestureRecognizers?.Contains(this) == true)
            {
                view.RemoveGestureRecognizer(this);
            }
        }

        public override void TouchesBegan(NSSet touches, UIEvent evt)
        {
            base.TouchesBegan(touches, evt);
            foreach (var touch in touches.Cast<UITouch>())
            {
                if (!FireEvent(SKTouchAction.Pressed, touch, inContact: true))
                {
                    IgnoreTouch(touch, evt);
                }
            }
        }

        public override void TouchesMoved(NSSet touches, UIEvent evt)
        {
            base.TouchesMoved(touches, evt);
            foreach (var touch in touches.Cast<UITouch>())
            {
                FireEvent(SKTouchAction.Moved, touch, inContact: true);
            }
        }

        public override void TouchesEnded(NSSet touches, UIEvent evt)
        {
            base.TouchesEnded(touches, evt);
            foreach (var touch in touches.Cast<UITouch>())
            {
                FireEvent(SKTouchAction.Released, touch, inContact: false);
            }
        }

        public override void TouchesCancelled(NSSet touches, UIEvent evt)
        {
            base.TouchesCancelled(touches, evt);
            foreach (var touch in touches.Cast<UITouch>())
            {
                FireEvent(SKTouchAction.Cancelled, touch, inContact: false);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _onTouch = null;
                _scalePixels = null;
            }
            base.Dispose(disposing);
        }

        private bool FireEvent(SKTouchAction action, UITouch touch, bool inContact)
        {
            if (_onTouch is null || _scalePixels is null)
            {
                return false;
            }

            var point = touch.LocationInView(View);
            var scaledPoint = _scalePixels(point.X, point.Y);
            var id = ((IntPtr)touch.Handle).ToInt64();
            var args = new SKTouchEventArgs(id, action, scaledPoint, inContact);
            _onTouch(args);
            return args.Handled;
        }
    }
}
#endif
