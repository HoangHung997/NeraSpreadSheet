#if MACCATALYST
using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Handlers;
using SkiaSharp;
using SkiaSharp.Views.iOS;
using SkiaSharp.Views.Maui;
using UIKit;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Mac Catalyst GPU handler that keeps SkiaSharp's native SKMetalView but avoids
/// the extra managed MauiSKMetalView subclass that fails UIKit 26 class
/// initialization. It reproduces SkiaSharp's logical-pixel and touch semantics
/// directly at the handler boundary, so the cross-platform Nera view remains
/// unaware of the platform workaround.
/// </summary>
internal sealed class NeraMacCatalystSKGLViewHandler :
    ViewHandler<ISKGLView, SKMetalView>
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

    private NeraMacCatalystTouchHandler? _touchHandler;
    private SKSizeI _lastCanvasSize;
    private GRContext? _lastGrContext;

    public NeraMacCatalystSKGLViewHandler()
        : base(NeraMapper, NeraCommandMapper)
    {
    }

    protected override SKMetalView CreatePlatformView() =>
        new()
        {
            BackgroundColor = UIColor.Clear,
            Opaque = false,
        };

    protected override void ConnectHandler(SKMetalView platformView)
    {
        base.ConnectHandler(platformView);
        platformView.PaintSurface += OnPaintSurface;
        UpdateTouchEvents(platformView, VirtualView.EnableTouchEvents);
    }

    protected override void DisconnectHandler(SKMetalView platformView)
    {
        platformView.PaintSurface -= OnPaintSurface;
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

    private void OnPaintSurface(object? sender, SKPaintMetalSurfaceEventArgs e)
    {
        if (VirtualView is not { } view || sender is not SKMetalView platformView)
        {
            return;
        }

        var info = e.Info;
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
                e.Surface.Canvas.Scale((float)contentScale);
                info = e.Info.WithSize(new SKSizeI(logicalWidth, logicalHeight));
            }
        }

        var canvasSize = info.Size;
        if (_lastCanvasSize != canvasSize)
        {
            _lastCanvasSize = canvasSize;
            view.OnCanvasSizeChanged(canvasSize);
        }

        var grContext = platformView.GRContext;
        if (!ReferenceEquals(_lastGrContext, grContext))
        {
            _lastGrContext = grContext;
            view.OnGRContextChanged(grContext);
        }

        view.OnPaintSurface(new SKPaintGLSurfaceEventArgs(
            e.Surface,
            e.BackendRenderTarget,
            e.Origin,
            info,
            e.RawInfo));
    }

    private void UpdateTouchEvents(SKMetalView platformView, bool enabled)
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
        if (handler.PlatformView is not { } platformView)
        {
            return;
        }

        platformView.Paused = !view.HasRenderLoop;
        platformView.EnableSetNeedsDisplay = !view.HasRenderLoop;
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
