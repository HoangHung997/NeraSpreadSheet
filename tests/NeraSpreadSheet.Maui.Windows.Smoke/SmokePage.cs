using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Maui;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace NeraSpreadSheet.Maui.Windows.Smoke;

internal sealed class SmokePage : ContentPage
{
    private static readonly TimeSpan SmokeTimeout = TimeSpan.FromSeconds(45d);
    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        WriteIndented = true,
    };
    private const double ExpectedZoom = 1.375d;
    private const double ExpectedOffsetX = 17.25d;
    private const double ExpectedOffsetY = 31.75d;

    private readonly Grid _host = new();
    private readonly Workbook _workbook = CreateWorkbook();
    private NeraSpreadsheetView? _view;
    private IElementHandler? _firstHandler;
    private object? _firstPlatformView;
    private GRContext? _firstContext;
    private NeraGpuContextDiagnostics _firstGpuDiagnostics;
    private NeraGpuContextDiagnostics _lostGpuDiagnostics;
    private int _firstFrameWidth;
    private int _firstFrameHeight;
    private int _resizedFrameWidth;
    private int _resizedFrameHeight;
    private int _stage;
    private int _frameCount;
    private int _finished;
    private bool _inputApplied;
    private bool _resizeApplied;
    private bool _recreationApplied;

    public SmokePage()
    {
        Title = "NeraSpreadSheet MAUI runtime smoke";
        Content = _host;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        _ = MonitorTimeoutAsync();
        _view = CreateView();
        _host.Children.Add(_view);
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
            ValidateFrame(view, e);
            switch (_stage)
            {
                case 0:
                    CaptureFirstFrame(view, e);
                    QueueInputMutation(view);
                    break;
                case 1 when _inputApplied:
                    ValidateInputMutation(view);
                    QueueResize(view);
                    break;
                case 2 when _resizeApplied && IsResizedFrame(e):
                    ValidateResizedFrame(e);
                    QueueSurfaceRecreation(view);
                    break;
                case 3 when _recreationApplied && IsRecreatedResizedFrame(e):
                    ValidateRecreatedSurface(view, e);
                    CompleteSuccessfully(view, e);
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

    private void CaptureFirstFrame(
        NeraSpreadsheetView view,
        SKPaintGLSurfaceEventArgs e)
    {
        _firstHandler = view.Handler;
        _firstPlatformView = view.Handler?.PlatformView;
        _firstContext = view.GRContext;
        _firstGpuDiagnostics = view.GpuContextDiagnostics;
        _firstFrameWidth = e.Info.Width;
        _firstFrameHeight = e.Info.Height;
        _stage = 1;
    }

    private void QueueInputMutation(NeraSpreadsheetView view)
    {
        Dispatcher.Dispatch(() =>
        {
            try
            {
                ApplyPinch(view);
                ApplyPanTo(view, ExpectedOffsetX, ExpectedOffsetY);
                ApplyCornerTap(view);
                _workbook.Worksheets[0].SetValue(
                    new CellAddress(0, 0),
                    "Nera MAUI production input rendered");
                _inputApplied = true;
                view.InvalidateSurface();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private static void ValidateInputMutation(NeraSpreadsheetView view)
    {
        Require(Math.Abs(view.Zoom - ExpectedZoom) <= 1e-6,
            $"Production pinch did not reach {ExpectedZoom}.");
        Require(Math.Abs(view.ScrollSnapshot.OffsetX - ExpectedOffsetX) <= 1e-4,
            $"Production pan did not reach horizontal offset {ExpectedOffsetX}.");
        Require(Math.Abs(view.ScrollSnapshot.OffsetY - ExpectedOffsetY) <= 1e-4,
            $"Production pan did not reach vertical offset {ExpectedOffsetY}.");

        var diagnostics = view.InputDiagnostics;
        Require(diagnostics.PressedEvents >= 4L,
            "Production input did not receive all deterministic presses.");
        Require(diagnostics.MovedEvents >= 2L,
            "Production input did not receive deterministic pan/pinch movement.");
        Require(diagnostics.ReleasedEvents >= 4L,
            "Production input did not receive all deterministic releases.");
        Require(diagnostics.PanUpdates >= 1L,
            "Production input did not execute a pan update.");
        Require(diagnostics.PinchUpdates >= 1L,
            "Production input did not execute a pinch update.");
        Require(diagnostics.TapSelections >= 1L,
            "Production input did not execute tap selection.");
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

    private void QueueResize(NeraSpreadsheetView view)
    {
        _stage = 2;
        Dispatcher.Dispatch(() =>
        {
            try
            {
                view.HorizontalOptions = LayoutOptions.Start;
                view.VerticalOptions = LayoutOptions.Start;
                view.WidthRequest = Math.Max(360d, _firstFrameWidth - 160d);
                view.HeightRequest = Math.Max(260d, _firstFrameHeight - 120d);
                _resizeApplied = true;
                view.InvalidateSurface();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private bool IsResizedFrame(SKPaintGLSurfaceEventArgs e) =>
        e.Info.Width > 0 &&
        e.Info.Height > 0 &&
        e.Info.Width < _firstFrameWidth &&
        e.Info.Height < _firstFrameHeight;

    private void ValidateResizedFrame(SKPaintGLSurfaceEventArgs e)
    {
        _resizedFrameWidth = e.Info.Width;
        _resizedFrameHeight = e.Info.Height;
        Require(_resizedFrameWidth >= 360,
            "The resized native surface became unexpectedly narrow.");
        Require(_resizedFrameHeight >= 260,
            "The resized native surface became unexpectedly short.");
    }

    private void QueueSurfaceRecreation(NeraSpreadsheetView view)
    {
        _stage = 3;
        Dispatcher.Dispatch(() =>
        {
            try
            {
                _host.Children.Remove(view);
                view.Handler = null!;
                _lostGpuDiagnostics = view.GpuContextDiagnostics;
                Require(!_lostGpuDiagnostics.HasActiveContext,
                    "The detached view retained a stale GPU context.");
                Require(!_lostGpuDiagnostics.HasActiveFrame,
                    "The detached view retained a stale GPU frame.");
                Require(
                    _lostGpuDiagnostics.ContextLostCount >
                    _firstGpuDiagnostics.ContextLostCount,
                    "Handler detachment did not record GPU context loss.");

                _recreationApplied = true;
                _host.Children.Add(view);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private bool IsRecreatedResizedFrame(SKPaintGLSurfaceEventArgs e) =>
        Math.Abs(e.Info.Width - _resizedFrameWidth) <= 1 &&
        Math.Abs(e.Info.Height - _resizedFrameHeight) <= 1;

    private void ValidateRecreatedSurface(
        NeraSpreadsheetView view,
        SKPaintGLSurfaceEventArgs e)
    {
        Require(_firstHandler is not null &&
                !ReferenceEquals(_firstHandler, view.Handler),
            "The same view reused its disconnected MAUI handler.");
        Require(_firstPlatformView is not null &&
                !ReferenceEquals(_firstPlatformView, view.Handler?.PlatformView),
            "The same view reused its disconnected native surface.");
        Require(_firstContext is not null &&
                !ReferenceEquals(_firstContext, view.GRContext),
            "The same view did not receive a recreated Skia GRContext.");

        var diagnostics = view.GpuContextDiagnostics;
        Require(
            diagnostics.ContextGeneration >
            _firstGpuDiagnostics.ContextGeneration,
            "The recreated context did not advance its generation.");
        Require(
            diagnostics.ContextCreatedCount >
            _firstGpuDiagnostics.ContextCreatedCount,
            "The recreated context was not counted as a new context.");
        Require(
            diagnostics.ContextRecreatedCount >
            _firstGpuDiagnostics.ContextRecreatedCount,
            "The production lifecycle did not record context recreation.");
        Require(
            diagnostics.ContextLostCount >=
            _lostGpuDiagnostics.ContextLostCount,
            "The recreated view lost its recorded context-loss history.");
        Require(Math.Abs(view.Zoom - ExpectedZoom) <= 1e-9,
            "The same view lost its zoom state during handler recreation.");
        Require(Math.Abs(view.ScrollSnapshot.OffsetX - ExpectedOffsetX) <= 1e-4 &&
                Math.Abs(view.ScrollSnapshot.OffsetY - ExpectedOffsetY) <= 1e-4,
            "The same view lost its fractional scroll state during handler recreation.");
        Require(Math.Abs(e.Info.Width - _resizedFrameWidth) <= 1 &&
                Math.Abs(e.Info.Height - _resizedFrameHeight) <= 1,
            "The recreated native surface lost its resized dimensions.");

        var input = view.InputDiagnostics;
        Require(input.ActiveTouchCount == 0 && !input.IsPinching,
            "Handler recreation restored stale production input state.");
    }

    private void CompleteSuccessfully(
        NeraSpreadsheetView view,
        SKPaintGLSurfaceEventArgs e)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        WriteResult(new
        {
            status = "success",
            frameCount = _frameCount,
            firstWidth = _firstFrameWidth,
            firstHeight = _firstFrameHeight,
            resizedWidth = _resizedFrameWidth,
            resizedHeight = _resizedFrameHeight,
            recreatedWidth = e.Info.Width,
            recreatedHeight = e.Info.Height,
            zoom = ExpectedZoom,
            offsetX = ExpectedOffsetX,
            offsetY = ExpectedOffsetY,
            firstHandler = RuntimeHelpers.GetHashCode(_firstHandler!),
            recreatedHandler = RuntimeHelpers.GetHashCode(view.Handler!),
            firstContext = RuntimeHelpers.GetHashCode(_firstContext!),
            recreatedContext = RuntimeHelpers.GetHashCode(view.GRContext!),
            firstContextGeneration = _firstGpuDiagnostics.ContextGeneration,
            recreatedContextGeneration =
                view.GpuContextDiagnostics.ContextGeneration,
            contextCreatedCount =
                view.GpuContextDiagnostics.ContextCreatedCount,
            contextLostCount =
                view.GpuContextDiagnostics.ContextLostCount,
            contextRecreatedCount =
                view.GpuContextDiagnostics.ContextRecreatedCount,
            framesStarted = view.GpuContextDiagnostics.FramesStarted,
            framesCompleted = view.GpuContextDiagnostics.FramesCompleted,
            framesAbandoned = view.GpuContextDiagnostics.FramesAbandoned,
            inputPressed = view.InputDiagnostics.PressedEvents,
            inputMoved = view.InputDiagnostics.MovedEvents,
            inputReleased = view.InputDiagnostics.ReleasedEvents,
            inputPanUpdates = view.InputDiagnostics.PanUpdates,
            inputPinchUpdates = view.InputDiagnostics.PinchUpdates,
            inputTapSelections = view.InputDiagnostics.TapSelections,
            inputGestureResets = view.InputDiagnostics.GestureResetCount,
            cachedTypefaces = view.CachedTypefaceCount,
        });
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
                    $"The loaded MAUI GPU smoke did not complete within {SmokeTimeout}."));
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
}
