using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Maui;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace NeraSpreadSheet.Maui.Windows.Smoke;

internal sealed class SmokePage : ContentPage, IDisposable
{
    private static readonly TimeSpan SmokeTimeout = TimeSpan.FromSeconds(60d);
    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        WriteIndented = true,
    };
    private static readonly ResizeDelta[] ResizeSequence =
    [
        new(160d, 120d),
        new(48d, 40d),
        new(240d, 180d),
    ];
    private const double ExpectedZoom = 1.375d;
    private const double ExpectedOffsetX = 17.25d;
    private const double ExpectedOffsetY = 31.75d;
    private const int WheelDelta = -120;
    private const double StateTolerance = 1e-4;
    private const double SizeTolerance = 2d;

    private readonly Grid _host = new();
    private readonly Workbook _workbook = CreateWorkbook();
    private readonly int[] _resizedWidths = new int[ResizeSequence.Length];
    private readonly int[] _resizedHeights = new int[ResizeSequence.Length];
    private readonly int[] _recreatedWidths = new int[ResizeSequence.Length];
    private readonly int[] _recreatedHeights = new int[ResizeSequence.Length];
    private readonly int[] _handlerIdentities = new int[ResizeSequence.Length + 1];
    private readonly int[] _contextIdentities = new int[ResizeSequence.Length + 1];
    private NeraSpreadsheetView? _view;
    private NeraSpreadsheetEditorHost? _editorHost;
    private bool _editorSmokePassed;
    private object? _sessionIdentity;
    private CellAddress _expectedSelectionActive;
    private CellAddress _expectedSelectionAnchor;
    private CellRange[] _expectedSelectionRanges = [];
    private long _expectedSelectionVersion;
    private IElementHandler? _initialHandler;
    private GRContext? _initialContext;
    private NeraGpuContextDiagnostics _initialGpuDiagnostics;
    private IElementHandler? _cycleHandler;
    private object? _cyclePlatformView;
    private GRContext? _cycleContext;
    private NeraGpuContextDiagnostics _cycleBeforeDetachDiagnostics;
    private NeraGpuContextDiagnostics _cycleLostDiagnostics;
    private SmokeStage _stage;
    private int _firstFrameWidth;
    private int _firstFrameHeight;
    private int _cycleFrameWidth;
    private int _cycleFrameHeight;
    private int _cycleIndex;
    private int _frameCount;
    private int _finished;
    private double _requestedWidth;
    private double _requestedHeight;
    private double _wheelTargetX;
    private double _wheelTargetY;
    private double _settledOffsetX;
    private double _settledOffsetY;
    private bool _primaryInputApplied;
    private bool _wheelQueued;
    private bool _resizeApplied;
    private bool _recreationApplied;

    public SmokePage()
    {
        Table007EditorSmoke.Trace("smoke-page-constructor");
        Title = "NeraSpreadSheet MAUI repeated runtime stress";
        Content = _host;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Table007EditorSmoke.Trace("smoke-page-loaded");
        Loaded -= OnLoaded;
        _ = MonitorTimeoutAsync();
        _view = CreateView();
        Table007EditorSmoke.Trace("smoke-view-created");
        _editorHost = new NeraSpreadsheetEditorHost(_view);
        Table007EditorSmoke.Trace("smoke-editor-host-created");
        _host.Children.Add(_editorHost);
        Table007EditorSmoke.Trace("smoke-editor-host-attached");
    }

    private NeraSpreadsheetView CreateView()
    {
        var view = new NeraSpreadsheetView
        {
            Workbook = _workbook,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        view.PaintSurface += OnPaintSurface;
        view.Loaded += OnViewLoaded;
        return view;
    }

    private static void OnViewLoaded(object? sender, EventArgs e)
    {
        if (sender is NeraSpreadsheetView view)
        {
            view.InvalidateSurface();
        }
    }

    private void OnPaintSurface(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        if (sender is not NeraSpreadsheetView view ||
            !ReferenceEquals(view, _view) ||
            Volatile.Read(ref _finished) != 0)
        {
            return;
        }

        try
        {
            _frameCount++;
            if (_frameCount == 1) Table007EditorSmoke.Trace("smoke-first-frame");
            ValidateFrame(view, e);
            switch (_stage)
            {
                case SmokeStage.InitialFrame:
                    CaptureInitialFrame(view, e);
                    QueuePrimaryInput(view);
                    break;

                case SmokeStage.AwaitPrimaryInput when _primaryInputApplied:
                    ValidatePrimaryInput(view);
                    QueueWheel(view);
                    break;

                case SmokeStage.AwaitWheelSettle when _wheelQueued:
                    if (IsWheelSettled(view))
                    {
                        ValidateWheelSettled(view);
                        QueueResize(view);
                    }
                    break;

                case SmokeStage.AwaitResize
                    when _resizeApplied && IsRequestedResizeFrame(e):
                    CaptureResizedFrame(e);
                    QueueSurfaceRecreation(view);
                    break;

                case SmokeStage.AwaitRecreation
                    when _recreationApplied && IsRecreatedFrame(e):
                    ValidateRecreatedSurface(view, e);
                    AdvanceStressCycle(view, e);
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void ValidateFrame(
        NeraSpreadsheetView view,
        SKPaintGLSurfaceEventArgs e)
    {
        Require(e.Info.Width > 0 && e.Info.Height > 0,
            "The native GPU surface reported an empty frame.");
        var handler = view.Handler
            ?? throw new InvalidOperationException(
                "The Nera view did not receive a platform handler.");
        Require(handler.PlatformView is not null,
            "The Nera handler did not create a native platform view.");
        _ = view.GRContext
            ?? throw new InvalidOperationException(
                "The Nera GPU surface did not expose a live Skia GRContext.");
        Require(view.Session is not null,
            "The workbook did not create a spreadsheet session.");
        Require(view.CachedTypefaceCount > 0,
            "The rendered spreadsheet did not exercise the Skia typeface cache.");

        var diagnostics = view.GpuContextDiagnostics;
        Require(diagnostics.HasActiveContext,
            "The production view did not retain its active GPU context.");
        Require(!diagnostics.HasActiveFrame,
            "The production view leaked an active GPU frame after PaintSurface.");
        Require(diagnostics.ContextGeneration > 0L,
            "The production view did not create a GPU context generation.");
        Require(diagnostics.FramesCompleted > 0L,
            "The production view did not complete a tracked GPU frame.");
        Require(diagnostics.FramesFailed == 0L,
            "The production view reported a failed GPU frame.");
        Require(diagnostics.StaleFrameTransitionsRejected == 0L,
            "The production view rejected a stale GPU frame transition.");
        Require(
            diagnostics.FramesStarted ==
            diagnostics.FramesCompleted +
            diagnostics.FramesFailed +
            diagnostics.FramesAbandoned,
            "The production GPU frame accounting is unbalanced.");
    }

    private void CaptureInitialFrame(
        NeraSpreadsheetView view,
        SKPaintGLSurfaceEventArgs e)
    {
        _initialHandler = view.Handler;
        _initialContext = view.GRContext;
        _initialGpuDiagnostics = view.GpuContextDiagnostics;
        _handlerIdentities[0] = RuntimeHelpers.GetHashCode(_initialHandler!);
        _contextIdentities[0] = RuntimeHelpers.GetHashCode(_initialContext!);
        _firstFrameWidth = e.Info.Width;
        _firstFrameHeight = e.Info.Height;
    }

    private void QueuePrimaryInput(NeraSpreadsheetView view)
    {
        _stage = SmokeStage.AwaitPrimaryInput;
        Dispatcher.Dispatch(async () =>
        {
            try
            {
                if (!_editorSmokePassed)
                {
                    await Table007EditorSmoke.RunAsync(_editorHost!);
                    _editorSmokePassed = true;
                    Table007EditorSmoke.Trace("smoke-editor-verified");
                }
                ApplyPinch(view);
                ApplyPanTo(view, ExpectedOffsetX, ExpectedOffsetY);
                ApplyCornerTap(view);
                _workbook.Worksheets[0].SetValue(
                    new CellAddress(0, 0),
                    "Nera MAUI repeated production stress");
                CaptureExpectedSelection(view);
                _sessionIdentity = view.Session;
                _primaryInputApplied = true;
                view.InvalidateSurface();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private void CaptureExpectedSelection(NeraSpreadsheetView view)
    {
        var session = view.Session
            ?? throw new InvalidOperationException(
                "The production input did not retain its spreadsheet session.");
        var selection = session.Selection.Capture();
        _expectedSelectionActive = selection.ActiveCell;
        _expectedSelectionAnchor = selection.AnchorCell;
        _expectedSelectionRanges = selection.Ranges.ToArray();
        _expectedSelectionVersion = selection.Version;
    }

    private void ValidatePrimaryInput(NeraSpreadsheetView view)
    {
        Require(Math.Abs(view.Zoom - ExpectedZoom) <= 1e-6,
            $"Production pinch did not reach {ExpectedZoom}.");
        Require(Math.Abs(view.ScrollSnapshot.OffsetX - ExpectedOffsetX) <= StateTolerance,
            $"Production pan did not reach horizontal offset {ExpectedOffsetX}.");
        Require(Math.Abs(view.ScrollSnapshot.OffsetY - ExpectedOffsetY) <= StateTolerance,
            $"Production pan did not reach vertical offset {ExpectedOffsetY}.");
        ValidateSessionAndSelection(view);

        var diagnostics = view.InputDiagnostics;
        Require(diagnostics.PressedEvents == 4L,
            "Production input did not receive exactly four deterministic presses.");
        Require(diagnostics.MovedEvents == 2L,
            "Production input did not receive exactly two deterministic moves.");
        Require(diagnostics.ReleasedEvents == 4L,
            "Production input did not receive exactly four deterministic releases.");
        Require(diagnostics.PanUpdates == 1L,
            "Production input did not execute exactly one pan update.");
        Require(diagnostics.PinchUpdates == 1L,
            "Production input did not execute exactly one pinch update.");
        Require(diagnostics.TapSelections == 1L,
            "Production input did not execute exactly one tap selection.");
        Require(diagnostics.WheelEvents == 0L,
            "Production input recorded a wheel event before wheel stress began.");
        Require(diagnostics.ActiveTouchCount == 0,
            "Production input retained active pointers after release.");
        Require(!diagnostics.IsPinching && !diagnostics.IsTapEligible,
            "Production input retained stale gesture state.");
    }

    private static void ApplyPinch(NeraSpreadsheetView view)
    {
        var first = new SKPoint(200f, 220f);
        var second = new SKPoint(300f, 220f);
        var expandedSecond = new SKPoint(
            first.X + (float)(100d * ExpectedZoom),
            second.Y);

        ProcessTouch(view, 101L, SKTouchAction.Pressed, first, true);
        ProcessTouch(view, 102L, SKTouchAction.Pressed, second, true);
        ProcessTouch(view, 102L, SKTouchAction.Moved, expandedSecond, true);
        ProcessTouch(view, 102L, SKTouchAction.Released, expandedSecond, false);
        ProcessTouch(view, 101L, SKTouchAction.Released, first, false);
    }

    private static void ApplyPanTo(
        NeraSpreadsheetView view,
        double targetX,
        double targetY)
    {
        var before = view.ScrollSnapshot;
        var start = new SKPoint(500f, 400f);
        var end = new SKPoint(
            start.X + (float)((before.OffsetX - targetX) * view.Zoom),
            start.Y + (float)((before.OffsetY - targetY) * view.Zoom));

        ProcessTouch(view, 103L, SKTouchAction.Pressed, start, true);
        ProcessTouch(view, 103L, SKTouchAction.Moved, end, true);
        ProcessTouch(view, 103L, SKTouchAction.Released, end, false);
    }

    private static void ApplyCornerTap(NeraSpreadsheetView view)
    {
        var point = new SKPoint(5f, 5f);
        ProcessTouch(view, 104L, SKTouchAction.Pressed, point, true);
        ProcessTouch(view, 104L, SKTouchAction.Released, point, false);
    }

    private static void ProcessTouch(
        NeraSpreadsheetView view,
        long id,
        SKTouchAction action,
        SKPoint point,
        bool inContact)
    {
        var input = new SKTouchEventArgs(
            id,
            action,
            SKMouseButton.Left,
            SKTouchDeviceType.Touch,
            point,
            inContact);
        view.ProcessTouchInput(input);
        Require(input.Handled,
            "The production input path did not mark the pointer event handled.");
    }

    private void QueueWheel(NeraSpreadsheetView view)
    {
        _stage = SmokeStage.AwaitWheelSettle;
        Dispatcher.Dispatch(() =>
        {
            try
            {
                var before = view.ScrollSnapshot;
                var input = new SKTouchEventArgs(
                    0L,
                    SKTouchAction.WheelChanged,
                    SKMouseButton.Middle,
                    SKTouchDeviceType.Mouse,
                    new SKPoint(600f, 420f),
                    false,
                    WheelDelta);
                view.ProcessTouchInput(input);
                Require(input.Handled,
                    "The production input path did not handle the wheel event.");

                var queued = view.ScrollSnapshot;
                _wheelTargetX = queued.TargetX;
                _wheelTargetY = queued.TargetY;
                var expectedTargetY = before.OffsetY +
                    (view.WheelPixelsPerNotch / view.Zoom);
                Require(Math.Abs(_wheelTargetX - before.OffsetX) <= StateTolerance,
                    "The vertical wheel unexpectedly changed the horizontal target.");
                Require(Math.Abs(_wheelTargetY - expectedTargetY) <= StateTolerance,
                    "The production wheel target did not scale by the current zoom.");
                Require(view.HasRenderLoop,
                    "The production wheel did not enable the native render loop.");
                _wheelQueued = true;
                view.InvalidateSurface();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private bool IsWheelSettled(NeraSpreadsheetView view)
    {
        var scroll = view.ScrollSnapshot;
        return !view.HasRenderLoop &&
            Math.Abs(scroll.OffsetX - scroll.TargetX) <= 1e-6 &&
            Math.Abs(scroll.OffsetY - scroll.TargetY) <= 1e-6;
    }

    private void ValidateWheelSettled(NeraSpreadsheetView view)
    {
        var scroll = view.ScrollSnapshot;
        Require(Math.Abs(scroll.TargetX - _wheelTargetX) <= StateTolerance &&
                Math.Abs(scroll.TargetY - _wheelTargetY) <= StateTolerance,
            "The wheel target changed while the animation was settling.");
        Require(Math.Abs(scroll.OffsetX - _wheelTargetX) <= StateTolerance &&
                Math.Abs(scroll.OffsetY - _wheelTargetY) <= StateTolerance,
            "The wheel animation did not settle at its target.");
        Require(scroll.OffsetY > ExpectedOffsetY + 1d,
            "The wheel animation did not move the fractional viewport.");
        Require(view.InputDiagnostics.WheelEvents == 1L,
            "The production input did not record exactly one wheel event.");
        Require(!view.HasRenderLoop,
            "The wheel animation left the native render loop enabled after settling.");
        ValidateSessionAndSelection(view);

        _settledOffsetX = scroll.OffsetX;
        _settledOffsetY = scroll.OffsetY;
    }

    private void QueueResize(NeraSpreadsheetView view)
    {
        Require(_cycleIndex >= 0 && _cycleIndex < ResizeSequence.Length,
            "The MAUI resize stress cycle index is invalid.");

        var delta = ResizeSequence[_cycleIndex];
        _requestedWidth = Math.Max(360d, _firstFrameWidth - delta.Width);
        _requestedHeight = Math.Max(260d, _firstFrameHeight - delta.Height);
        _resizeApplied = false;
        _recreationApplied = false;
        _stage = SmokeStage.AwaitResize;

        Dispatcher.Dispatch(() =>
        {
            try
            {
                view.HorizontalOptions = LayoutOptions.Start;
                view.VerticalOptions = LayoutOptions.Start;
                view.WidthRequest = _requestedWidth;
                view.HeightRequest = _requestedHeight;
                _resizeApplied = true;
                view.InvalidateSurface();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private bool IsRequestedResizeFrame(SKPaintGLSurfaceEventArgs e) =>
        Math.Abs(e.Info.Width - _requestedWidth) <= SizeTolerance &&
        Math.Abs(e.Info.Height - _requestedHeight) <= SizeTolerance;

    private void CaptureResizedFrame(SKPaintGLSurfaceEventArgs e)
    {
        _cycleFrameWidth = e.Info.Width;
        _cycleFrameHeight = e.Info.Height;
        _resizedWidths[_cycleIndex] = e.Info.Width;
        _resizedHeights[_cycleIndex] = e.Info.Height;

        Require(_cycleFrameWidth >= 360,
            "The resized native surface became unexpectedly narrow.");
        Require(_cycleFrameHeight >= 260,
            "The resized native surface became unexpectedly short.");
        if (_cycleIndex == 0)
        {
            Require(_cycleFrameWidth < _firstFrameWidth &&
                    _cycleFrameHeight < _firstFrameHeight,
                "The first stress resize did not shrink the native surface.");
        }
        else if (_cycleIndex == 1)
        {
            Require(_cycleFrameWidth > _resizedWidths[0] &&
                    _cycleFrameHeight > _resizedHeights[0],
                "The second stress resize did not grow the native surface.");
        }
        else
        {
            Require(_cycleFrameWidth < _resizedWidths[_cycleIndex - 1] &&
                    _cycleFrameHeight < _resizedHeights[_cycleIndex - 1],
                "The final stress resize did not shrink the native surface.");
        }
    }

    private void QueueSurfaceRecreation(NeraSpreadsheetView view)
    {
        _cycleHandler = view.Handler
            ?? throw new InvalidOperationException(
                "The stress cycle did not have a MAUI handler before recreation.");
        _cyclePlatformView = _cycleHandler.PlatformView
            ?? throw new InvalidOperationException(
                "The stress cycle did not have a native surface before recreation.");
        _cycleContext = view.GRContext
            ?? throw new InvalidOperationException(
                "The stress cycle did not have a GRContext before recreation.");
        _cycleBeforeDetachDiagnostics = view.GpuContextDiagnostics;
        _stage = SmokeStage.AwaitRecreation;

        Dispatcher.Dispatch(() =>
        {
            try
            {
                _editorHost!.Children.Remove(view);
                view.Handler = null!;
                _cycleLostDiagnostics = view.GpuContextDiagnostics;
                Require(!_cycleLostDiagnostics.HasActiveContext,
                    "The detached view retained a stale GPU context.");
                Require(!_cycleLostDiagnostics.HasActiveFrame,
                    "The detached view retained a stale GPU frame.");
                Require(
                    _cycleLostDiagnostics.ContextLostCount ==
                    _cycleBeforeDetachDiagnostics.ContextLostCount + 1L,
                    "Handler detachment did not record exactly one context loss.");
                Require(
                    _cycleLostDiagnostics.FramesAbandoned ==
                    _cycleBeforeDetachDiagnostics.FramesAbandoned,
                    "Handler detachment abandoned a frame after PaintSurface completed.");
                ValidateClearedPointerState(view);

                _recreationApplied = true;
                _editorHost.Children.Insert(0, view);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private bool IsRecreatedFrame(SKPaintGLSurfaceEventArgs e) =>
        Math.Abs(e.Info.Width - _cycleFrameWidth) <= 1 &&
        Math.Abs(e.Info.Height - _cycleFrameHeight) <= 1;

    private void ValidateRecreatedSurface(
        NeraSpreadsheetView view,
        SKPaintGLSurfaceEventArgs e)
    {
        Require(!ReferenceEquals(_cycleHandler, view.Handler),
            "The same view reused its disconnected MAUI handler.");
        Require(!ReferenceEquals(_cyclePlatformView, view.Handler?.PlatformView),
            "The same view reused its disconnected native surface.");
        Require(!ReferenceEquals(_cycleContext, view.GRContext),
            "The same view did not receive a recreated Skia GRContext.");

        var diagnostics = view.GpuContextDiagnostics;
        Require(
            diagnostics.ContextGeneration ==
            _cycleBeforeDetachDiagnostics.ContextGeneration + 1L,
            "The recreated context did not advance exactly one generation.");
        Require(
            diagnostics.ContextCreatedCount ==
            _cycleBeforeDetachDiagnostics.ContextCreatedCount + 1L,
            "The recreated context was not counted exactly once.");
        Require(
            diagnostics.ContextRecreatedCount ==
            _cycleBeforeDetachDiagnostics.ContextRecreatedCount + 1L,
            "The production lifecycle did not record exactly one recreation.");
        Require(
            diagnostics.ContextLostCount ==
            _cycleLostDiagnostics.ContextLostCount,
            "The recreated view lost or duplicated its context-loss history.");
        Require(diagnostics.FramesFailed == 0L &&
                diagnostics.FramesAbandoned == 0L &&
                diagnostics.StaleFrameTransitionsRejected == 0L,
            "Repeated recreation introduced a failed, abandoned or stale GPU frame.");
        Require(Math.Abs(view.Zoom - ExpectedZoom) <= 1e-9,
            "The same view lost its zoom state during repeated recreation.");
        Require(Math.Abs(view.ScrollSnapshot.OffsetX - _settledOffsetX) <= StateTolerance &&
                Math.Abs(view.ScrollSnapshot.OffsetY - _settledOffsetY) <= StateTolerance,
            "The same view lost its settled fractional scroll state during recreation.");
        Require(Math.Abs(view.ScrollSnapshot.TargetX - _settledOffsetX) <= StateTolerance &&
                Math.Abs(view.ScrollSnapshot.TargetY - _settledOffsetY) <= StateTolerance,
            "The same view restored a stale wheel target after recreation.");
        Require(Math.Abs(e.Info.Width - _cycleFrameWidth) <= 1 &&
                Math.Abs(e.Info.Height - _cycleFrameHeight) <= 1,
            "The recreated native surface lost its resized dimensions.");
        Require(!view.HasRenderLoop,
            "The recreated view unexpectedly restarted settled wheel motion.");
        ValidateSessionAndSelection(view);
        ValidateClearedPointerState(view);

        _recreatedWidths[_cycleIndex] = e.Info.Width;
        _recreatedHeights[_cycleIndex] = e.Info.Height;
        _handlerIdentities[_cycleIndex + 1] = RuntimeHelpers.GetHashCode(view.Handler!);
        _contextIdentities[_cycleIndex + 1] = RuntimeHelpers.GetHashCode(view.GRContext!);
    }

    private void AdvanceStressCycle(
        NeraSpreadsheetView view,
        SKPaintGLSurfaceEventArgs e)
    {
        _cycleIndex++;
        if (_cycleIndex < ResizeSequence.Length)
        {
            QueueResize(view);
            return;
        }

        CompleteSuccessfully(view, e);
    }

    private void ValidateSessionAndSelection(NeraSpreadsheetView view)
    {
        var session = view.Session
            ?? throw new InvalidOperationException(
                "The same view lost its spreadsheet session.");
        if (_sessionIdentity is not null)
        {
            Require(ReferenceEquals(_sessionIdentity, session),
                "The same view replaced its spreadsheet session during native stress.");
        }

        var selection = session.Selection.Capture();
        Require(selection.ActiveCell == _expectedSelectionActive &&
                selection.AnchorCell == _expectedSelectionAnchor &&
                selection.Version == _expectedSelectionVersion,
            "The same view lost its active/anchor selection state.");
        Require(selection.Ranges.SequenceEqual(_expectedSelectionRanges),
            "The same view lost its selected ranges during native stress.");
    }

    private static void ValidateClearedPointerState(NeraSpreadsheetView view)
    {
        var input = view.InputDiagnostics;
        Require(input.ActiveTouchCount == 0 &&
                !input.IsPinching &&
                !input.IsTapEligible,
            "Native lifecycle stress retained stale production input state.");
    }

    private void CompleteSuccessfully(
        NeraSpreadsheetView view,
        SKPaintGLSurfaceEventArgs e)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }
        _stage = SmokeStage.Completed;

        var gpu = view.GpuContextDiagnostics;
        var input = view.InputDiagnostics;
        Require(
            gpu.ContextGeneration ==
            _initialGpuDiagnostics.ContextGeneration + ResizeSequence.Length,
            "Repeated recreation did not reach the expected context generation.");
        Require(
            gpu.ContextCreatedCount ==
            _initialGpuDiagnostics.ContextCreatedCount + ResizeSequence.Length,
            "Repeated recreation did not create the expected number of contexts.");
        Require(
            gpu.ContextLostCount ==
            _initialGpuDiagnostics.ContextLostCount + ResizeSequence.Length,
            "Repeated recreation did not record the expected context losses.");
        Require(
            gpu.ContextRecreatedCount ==
            _initialGpuDiagnostics.ContextRecreatedCount + ResizeSequence.Length,
            "Repeated recreation did not record the expected recreations.");
        Require(gpu.FramesStarted == gpu.FramesCompleted &&
                gpu.FramesFailed == 0L &&
                gpu.FramesAbandoned == 0L &&
                gpu.StaleFrameTransitionsRejected == 0L,
            "Final repeated-stress GPU frame accounting is invalid.");
        Require(input.PressedEvents == 4L &&
                input.MovedEvents == 2L &&
                input.ReleasedEvents == 4L &&
                input.WheelEvents == 1L &&
                input.PanUpdates == 1L &&
                input.PinchUpdates == 1L &&
                input.TapSelections == 1L &&
                input.IgnoredEvents == 0L,
            "Final production input diagnostics do not match the stress sequence.");
        ValidateSessionAndSelection(view);
        ValidateClearedPointerState(view);

        WriteResult(new
        {
            status = "success",
            table007Editor = _editorSmokePassed,
            frameCount = _frameCount,
            recreationCycles = ResizeSequence.Length,
            firstWidth = _firstFrameWidth,
            firstHeight = _firstFrameHeight,
            resizedWidths = _resizedWidths,
            resizedHeights = _resizedHeights,
            recreatedWidths = _recreatedWidths,
            recreatedHeights = _recreatedHeights,
            finalWidth = e.Info.Width,
            finalHeight = e.Info.Height,
            zoom = view.Zoom,
            offsetX = view.ScrollSnapshot.OffsetX,
            offsetY = view.ScrollSnapshot.OffsetY,
            targetX = view.ScrollSnapshot.TargetX,
            targetY = view.ScrollSnapshot.TargetY,
            handlerIdentities = _handlerIdentities,
            contextIdentities = _contextIdentities,
            firstHandler = RuntimeHelpers.GetHashCode(_initialHandler!),
            finalHandler = RuntimeHelpers.GetHashCode(view.Handler!),
            firstContext = RuntimeHelpers.GetHashCode(_initialContext!),
            finalContext = RuntimeHelpers.GetHashCode(view.GRContext!),
            firstContextGeneration = _initialGpuDiagnostics.ContextGeneration,
            finalContextGeneration = gpu.ContextGeneration,
            contextCreatedCount = gpu.ContextCreatedCount,
            contextLostCount = gpu.ContextLostCount,
            contextRecreatedCount = gpu.ContextRecreatedCount,
            framesStarted = gpu.FramesStarted,
            framesCompleted = gpu.FramesCompleted,
            framesFailed = gpu.FramesFailed,
            framesAbandoned = gpu.FramesAbandoned,
            staleFrameTransitions = gpu.StaleFrameTransitionsRejected,
            inputPressed = input.PressedEvents,
            inputMoved = input.MovedEvents,
            inputReleased = input.ReleasedEvents,
            inputWheel = input.WheelEvents,
            inputPanUpdates = input.PanUpdates,
            inputPinchUpdates = input.PinchUpdates,
            inputTapSelections = input.TapSelections,
            inputGestureResets = input.GestureResetCount,
            selectionVersion = _expectedSelectionVersion,
            selectionRangeCount = _expectedSelectionRanges.Length,
            cachedTypefaces = view.CachedTypefaceCount,
        });
        Dispose();
        Environment.Exit(0);
    }

    private void Fail(Exception exception)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        try
        {
            WriteResult(new
            {
                status = "failure",
                stage = _stage.ToString(),
                cycleIndex = _cycleIndex,
                frameCount = _frameCount,
                error = exception.ToString(),
            });
        }
        finally
        {
            Environment.Exit(1);
        }
    }

    private async Task MonitorTimeoutAsync()
    {
        await Task.Delay(SmokeTimeout).ConfigureAwait(false);
        if (Volatile.Read(ref _finished) != 0)
        {
            return;
        }

        Dispatcher.Dispatch(() =>
        {
            if (Volatile.Read(ref _finished) == 0)
            {
                Fail(new TimeoutException(
                    $"The repeated MAUI runtime stress did not complete within {SmokeTimeout}."));
            }
        });
    }

    private static void WriteResult(object result)
    {
        var path = Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_RESULT");
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "NERA_MAUI_SMOKE_RESULT must identify the smoke result file.");
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "The smoke result file has no parent directory."));
        File.WriteAllText(
            fullPath,
            JsonSerializer.Serialize(result, ResultJsonOptions));
    }

    private static Workbook CreateWorkbook()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        for (var row = 0; row < 80; row++)
        {
            for (var column = 0; column < 20; column++)
            {
                worksheet.SetValue(
                    new CellAddress(row, column),
                    row == 0 && column == 0
                        ? "Nera MAUI GPU runtime smoke"
                        : $"R{row + 1}C{column + 1}");
            }
        }
        return workbook;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    public void Dispose()
    {
        _editorHost?.Dispose();
        _editorHost = null;
        if (_view is { } view)
        {
            view.PaintSurface -= OnPaintSurface;
            view.Loaded -= OnViewLoaded;
            view.Dispose();
            _view = null;
        }
        GC.SuppressFinalize(this);
    }

    private readonly record struct ResizeDelta(double Width, double Height);

    private enum SmokeStage
    {
        InitialFrame,
        AwaitPrimaryInput,
        AwaitWheelSettle,
        AwaitResize,
        AwaitRecreation,
        Completed,
    }
}
