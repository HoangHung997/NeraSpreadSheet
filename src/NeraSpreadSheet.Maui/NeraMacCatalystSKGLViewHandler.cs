#if MACCATALYST
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CoreGraphics;
using Foundation;
using Metal;
using MetalKit;
using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using UIKit;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Mac Catalyst GPU handler backed by Apple's native MTKView. UIKit 26 sends a
/// private class-initialization selector to UIView subclasses; SkiaSharp's managed
/// SKMetalView hierarchy does not implement that selector and therefore cannot be
/// instantiated on current hosted Mac Catalyst runtimes. This handler keeps the
/// same Skia Metal rendering pipeline without introducing a managed UIView subclass.
/// </summary>
[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "MAUI handlers release the renderer from DisconnectHandler, which is the framework lifecycle boundary.")]
internal sealed class NeraMacCatalystSKGLViewHandler :
    ViewHandler<ISKGLView, MTKView>
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
    private SKSizeI _lastCanvasSize;
    private GRContext? _lastGrContext;

    public NeraMacCatalystSKGLViewHandler()
        : base(NeraMapper, NeraCommandMapper)
    {
    }

    protected override MTKView CreatePlatformView()
    {
        var device = MTLDevice.SystemDefault
            ?? throw new PlatformNotSupportedException(
                "Metal is not available on this Mac Catalyst runtime.");
        return new MTKView(CGRect.Empty, device)
        {
            BackgroundColor = UIColor.Clear,
            Opaque = false,
            ColorPixelFormat = MTLPixelFormat.BGRA8Unorm,
            DepthStencilPixelFormat = MTLPixelFormat.Depth32Float_Stencil8,
            DepthStencilStorageMode = MTLStorageMode.Shared,
            SampleCount = 1,
            FramebufferOnly = false,
        };
    }

    protected override void ConnectHandler(MTKView platformView)
    {
        base.ConnectHandler(platformView);
        if (VirtualView is NeraSpreadsheetView neraView)
        {
            NeraMacCatalystGpuDiagnostics.Clear(neraView);
        }
        _renderer = new NeraMetalRenderer(this, platformView);
        platformView.Delegate = _renderer;
        UpdateRenderLoop(platformView, VirtualView.HasRenderLoop);
        UpdateTouchEvents(platformView, VirtualView.EnableTouchEvents);
    }

    protected override void DisconnectHandler(MTKView platformView)
    {
        platformView.Delegate = null;
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

    private void Paint(
        MTKView platformView,
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
            var logicalWidth = (int)platformView.Bounds.Width;
            var logicalHeight = (int)platformView.Bounds.Height;
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

    private static void DrawableSizeChanged(MTKView platformView)
    {
        if (platformView.Paused && platformView.EnableSetNeedsDisplay)
        {
            platformView.SetNeedsDisplay();
        }
    }

    private void UpdateTouchEvents(MTKView platformView, bool enabled)
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
        object? args)
    {
        if (handler.PlatformView is { Paused: true, EnableSetNeedsDisplay: true } platformView)
        {
            platformView.SetNeedsDisplay();
        }
    }

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
        ISKGLView view)
    {
        if (handler.PlatformView is { Paused: true, EnableSetNeedsDisplay: true } platformView)
        {
            platformView.SetNeedsDisplay();
        }
    }

    private static void MapHasRenderLoop(
        NeraMacCatalystSKGLViewHandler handler,
        ISKGLView view)
    {
        if (handler.PlatformView is { } platformView)
        {
            UpdateRenderLoop(platformView, view.HasRenderLoop);
        }
    }

    private static void UpdateRenderLoop(MTKView platformView, bool hasRenderLoop)
    {
        platformView.Paused = !hasRenderLoop;
        platformView.EnableSetNeedsDisplay = !hasRenderLoop;
    }

    private sealed class NeraMetalRenderer : NSObject, IMTKViewDelegate
    {
        private NeraMacCatalystSKGLViewHandler? _handler;
        private readonly GRMtlBackendContext _backendContext;
        private GRContext? _context;

        internal NeraMetalRenderer(
            NeraMacCatalystSKGLViewHandler handler,
            MTKView platformView)
        {
            _handler = handler;
            var device = platformView.Device
                ?? throw new PlatformNotSupportedException(
                    "The native MTKView does not have a Metal device.");
            var queue = device.CreateCommandQueue()
                ?? throw new PlatformNotSupportedException(
                    "The Metal device could not create a command queue.");
            _backendContext = new GRMtlBackendContext
            {
                Device = device,
                Queue = queue,
            };
        }

        void IMTKViewDelegate.DrawableSizeWillChange(MTKView view, CGSize size) =>
            NeraMacCatalystSKGLViewHandler.DrawableSizeChanged(view);

        [SuppressMessage(
            "Design",
            "CA1031:Do not catch general exception types",
            Justification = "Exceptions must not escape an Objective-C MTKView delegate callback; the failure is retained in Nera diagnostics and the render loop is stopped.")]
        void IMTKViewDelegate.Draw(MTKView view)
        {
            try
            {
                DrawCore(view);
            }
            catch (Exception exception)
            {
                if (_handler?.VirtualView is NeraSpreadsheetView neraView)
                {
                    NeraMacCatalystGpuDiagnostics.RecordFailure(neraView, exception);
                }
                Trace.TraceError(
                    "Nera Mac Catalyst Metal frame failed: {0}",
                    exception);
                view.Paused = true;
                view.EnableSetNeedsDisplay = false;
            }
        }

        private void DrawCore(MTKView view)
        {
            var handler = _handler;
            var queue = _backendContext.Queue;
            var drawable = view.CurrentDrawable;
            var texture = drawable?.Texture;
            if (handler is null ||
                _backendContext.Device is null ||
                queue is null ||
                texture is null ||
                drawable is null)
            {
                return;
            }

            var drawableSize = view.DrawableSize;
            var width = (int)drawableSize.Width;
            var height = (int)drawableSize.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            _context ??= GRContext.CreateMetal(_backendContext);
            var context = _context;
            if (context is null)
            {
                return;
            }

            const GRSurfaceOrigin origin = GRSurfaceOrigin.TopLeft;
            var metalInfo = new GRMtlTextureInfo(texture);
            using var renderTarget = new GRBackendRenderTarget(
                width,
                height,
                metalInfo);
            using var surface = SKSurface.Create(
                context,
                renderTarget,
                origin,
                SKColorType.Bgra8888);
            if (surface is null)
            {
                return;
            }

            handler.Paint(view, context, surface, renderTarget, origin);

            surface.Canvas.Flush();
            surface.Flush();
            context.Flush();

            using var commandBuffer = queue.CommandBuffer();
            if (commandBuffer is null)
            {
                return;
            }
            commandBuffer.PresentDrawable(drawable);
            commandBuffer.Commit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _handler = null;
                _context?.Dispose();
                _context = null;
                if (_backendContext.Queue is IDisposable disposableQueue)
                {
                    disposableQueue.Dispose();
                }
            }
            base.Dispose(disposing);
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
