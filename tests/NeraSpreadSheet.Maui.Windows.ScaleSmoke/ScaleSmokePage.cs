using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Maui;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Windows;

namespace NeraSpreadSheet.Maui.Windows.ScaleSmoke;

internal sealed class ScaleSmokePage : ContentPage
{
    private static readonly TimeSpan SmokeTimeout = TimeSpan.FromSeconds(60d);
    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        WriteIndented = true,
    };
    private static readonly SurfaceScenario[] Scenarios =
    [
        new(
            "portrait-physical-canvas",
            IgnorePixelScaling: false,
            Width: 420d,
            Height: 560d,
            Orientation: NeraSurfaceOrientation.Portrait,
            WidthClass: NeraSurfaceWidthClass.Compact),
        new(
            "landscape-logical-canvas",
            IgnorePixelScaling: true,
            Width: 900d,
            Height: 500d,
            Orientation: NeraSurfaceOrientation.Landscape,
            WidthClass: NeraSurfaceWidthClass.Expanded),
        new(
            "square-logical-canvas",
            IgnorePixelScaling: true,
            Width: 600d,
            Height: 600d,
            Orientation: NeraSurfaceOrientation.Square,
            WidthClass: NeraSurfaceWidthClass.Medium),
    ];

    private const double ExpectedZoom = 1.25d;
    private const double ExpectedOffsetX = 29.5d;
    private const double ExpectedOffsetY = 53.75d;
    private const double LayoutTolerance = 3d;
    private const double PixelTolerance = 3d;
    private const double ScaleTolerance = 0.03d;

    private readonly Grid _host = new();
    private readonly Workbook _workbook = CreateWorkbook();
    private readonly List<SurfaceObservation> _observations = [];
    private readonly List<IElementHandler> _handlers = [];
    private readonly List<GRContext> _contexts = [];

    private NeraSpreadsheetView? _view;
    private object? _sessionIdentity;
    private CellAddress _expectedSelectionActive;
    private CellAddress _expectedSelectionAnchor;
    private CellRange[] _expectedSelectionRanges = [];
    private long _expectedSelectionVersion;
    private NeraGpuContextDiagnostics _initialGpu;
    private IElementHandler? _oldHandler;
    private object? _oldPlatformView;
    private GRContext? _oldContext;
    private NeraGpuContextDiagnostics _beforeDetachGpu;
    private NeraGpuContextDiagnostics _lostGpu;
    private SmokeStage _stage;
    private int _scenarioIndex;
    private int _frameCount;
    private int _finished;
    private bool _baselineApplied;
    private bool _scenarioApplied;
    private bool _recreationApplied;

    public ScaleSmokePage()
    {
        Title = "NeraSpreadSheet MAUI DPI and viewport-class smoke";
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
            var observation = Observe(view, e);

            switch (_stage)
            {
                case SmokeStage.InitialFrame:
                    CaptureInitial(view, observation);
                    QueueBaseline(view);
                    break;
                case SmokeStage.AwaitBaseline when _baselineApplied:
                    ValidatePersistentState(view);
                    QueueScenario(view);
                    break;
                case SmokeStage.AwaitScenario
                    when _scenarioApplied &&
                         MatchesCurrentScenario(observation.Surface):
                    ValidateScenario(view, observation);
                    _observations.Add(observation with
                    {
                        Scenario = Scenarios[_scenarioIndex].Name,
                    });
                    QueueRecreation(view);
                    break;
                case SmokeStage.AwaitRecreation
                    when _recreationApplied &&
                         MatchesCurrentScenario(observation.Surface):
                    ValidateRecreated(view, observation);
                    _observations.Add(observation with
                    {
                        Scenario = Scenarios[_scenarioIndex].Name,
                        Recreated = true,
                    });
                    AdvanceScenario(view, observation.Surface);
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static SurfaceObservation Observe(
        NeraSpreadsheetView view,
        SKPaintGLSurfaceEventArgs e)
    {
        Require(e.Info.Width > 0 && e.Info.Height > 0,
            "Renderer canvas is empty.");
        Require(e.RawInfo.Width > 0 && e.RawInfo.Height > 0,
            "Raw backing surface is empty.");

        var handler = view.Handler
            ?? throw new InvalidOperationException("MAUI handler is missing.");
        var platform = handler.PlatformView as SKSwapChainPanel
            ?? throw new InvalidOperationException(
                "Windows handler did not create an SKSwapChainPanel.");
        var context = view.GRContext
            ?? throw new InvalidOperationException("GRContext is missing.");
        Require(view.Session is not null, "Spreadsheet session is missing.");
        Require(view.CachedTypefaceCount > 0,
            "Skia typeface cache was not exercised.");

        var gpu = view.GpuContextDiagnostics;
        Require(gpu.HasActiveContext && !gpu.HasActiveFrame,
            "GPU lifecycle state is invalid.");
        Require(gpu.FramesStarted == gpu.FramesCompleted &&
                gpu.FramesFailed == 0L &&
                gpu.FramesAbandoned == 0L &&
                gpu.StaleFrameTransitionsRejected == 0L,
            "GPU frame accounting is unbalanced.");

        var surface = NeraSurfaceMetrics.Capture(view, e);
        Require(surface.ContextGeneration == gpu.ContextGeneration &&
                surface.FrameSequence == gpu.FramesStarted,
            "Surface metrics are not aligned with the completed GPU frame.");
        Require(surface.CanvasWidth == e.Info.Width &&
                surface.CanvasHeight == e.Info.Height &&
                surface.RawPixelWidth == e.RawInfo.Width &&
                surface.RawPixelHeight == e.RawInfo.Height,
            "Surface metrics do not match the paint event.");

        var observation = new SurfaceObservation(
            Scenario: string.Empty,
            Recreated: false,
            ContentsScale: platform.ContentsScale,
            NativeWidth: platform.ActualWidth,
            NativeHeight: platform.ActualHeight,
            Surface: surface,
            HandlerIdentity: RuntimeHelpers.GetHashCode(handler),
            ContextIdentity: RuntimeHelpers.GetHashCode(context));
        ValidateScale(observation);
        return observation;
    }

    private static void ValidateScale(SurfaceObservation observation)
    {
        var surface = observation.Surface;
        Require(double.IsFinite(observation.ContentsScale) &&
                observation.ContentsScale > 0d,
            "ContentsScale is invalid.");
        Require(Math.Abs(surface.ViewportWidth -
                    observation.NativeWidth) <= LayoutTolerance &&
                Math.Abs(surface.ViewportHeight -
                    observation.NativeHeight) <= LayoutTolerance,
            "Logical viewport does not match the native panel.");
        Require(Math.Abs(surface.RawPixelWidth -
                    (observation.NativeWidth * observation.ContentsScale)) <=
                    PixelTolerance &&
                Math.Abs(surface.RawPixelHeight -
                    (observation.NativeHeight * observation.ContentsScale)) <=
                    PixelTolerance,
            "Raw backing dimensions do not match ContentsScale.");
        Require(Math.Abs(surface.RawPixelsPerViewportUnitX -
                    observation.ContentsScale) <= ScaleTolerance &&
                Math.Abs(surface.RawPixelsPerViewportUnitY -
                    observation.ContentsScale) <= ScaleTolerance &&
                surface.IsRawPixelScaleUniform(ScaleTolerance) &&
                surface.IsCanvasScaleUniform(ScaleTolerance),
            "Surface scaling is not uniform.");

        if (surface.IgnorePixelScaling)
        {
            Require(Math.Abs(surface.CanvasUnitsPerViewportUnitX - 1d) <=
                        ScaleTolerance &&
                    Math.Abs(surface.CanvasUnitsPerViewportUnitY - 1d) <=
                        ScaleTolerance,
                "Logical canvas is not one unit per viewport unit.");
            Require(Math.Abs(surface.RawPixelsPerCanvasUnitX -
                        observation.ContentsScale) <= ScaleTolerance &&
                    Math.Abs(surface.RawPixelsPerCanvasUnitY -
                        observation.ContentsScale) <= ScaleTolerance,
                "Logical canvas lost the raw DPI scale.");
        }
        else
        {
            Require(surface.CanvasWidth == surface.RawPixelWidth &&
                    surface.CanvasHeight == surface.RawPixelHeight,
                "Physical canvas does not match the raw backing surface.");
            Require(Math.Abs(surface.CanvasUnitsPerViewportUnitX -
                        observation.ContentsScale) <= ScaleTolerance &&
                    Math.Abs(surface.CanvasUnitsPerViewportUnitY -
                        observation.ContentsScale) <= ScaleTolerance &&
                    Math.Abs(surface.RawPixelsPerCanvasUnitX - 1d) <=
                        ScaleTolerance &&
                    Math.Abs(surface.RawPixelsPerCanvasUnitY - 1d) <=
                        ScaleTolerance,
                "Physical canvas is not mapped one-to-one to raw pixels.");
        }
    }

    private void CaptureInitial(
        NeraSpreadsheetView view,
        SurfaceObservation observation)
    {
        Require(observation.Surface.IgnorePixelScaling,
            "Nera view did not start in logical-canvas mode.");
        Require(
            observation.Surface.Orientation ==
                NeraSurfaceOrientation.Landscape &&
            observation.Surface.WidthClass ==
                NeraSurfaceWidthClass.Expanded,
            "Initial viewport is not expanded landscape.");

        _initialGpu = view.GpuContextDiagnostics;
        _handlers.Add(view.Handler!);
        _contexts.Add(view.GRContext!);
        _observations.Add(observation with { Scenario = "initial" });
    }

    private void QueueBaseline(NeraSpreadsheetView view)
    {
        _stage = SmokeStage.AwaitBaseline;
        Dispatcher.Dispatch(() =>
        {
            try
            {
                view.ZoomTo(ExpectedZoom, 420d, 280d);
                view.ScrollTo(
                    ExpectedOffsetX,
                    ExpectedOffsetY,
                    animated: false);

                var session = view.Session
                    ?? throw new InvalidOperationException(
                        "Baseline session is missing.");
                session.Selection.SetActiveCell(new CellAddress(7, 5));
                _workbook.Worksheets[0].SetValue(
                    new CellAddress(0, 0),
                    "Nera MAUI DPI and viewport-class stress");

                var selection = session.Selection.Capture();
                _expectedSelectionActive = selection.ActiveCell;
                _expectedSelectionAnchor = selection.AnchorCell;
                _expectedSelectionRanges = selection.Ranges.ToArray();
                _expectedSelectionVersion = selection.Version;
                _sessionIdentity = session;
                _baselineApplied = true;
                view.InvalidateSurface();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private void QueueScenario(NeraSpreadsheetView view)
    {
        Require(_scenarioIndex >= 0 &&
                _scenarioIndex < Scenarios.Length,
            "Scenario index is invalid.");

        var scenario = Scenarios[_scenarioIndex];
        _scenarioApplied = false;
        _recreationApplied = false;
        _stage = SmokeStage.AwaitScenario;
        Dispatcher.Dispatch(() =>
        {
            try
            {
                view.HorizontalOptions = LayoutOptions.Start;
                view.VerticalOptions = LayoutOptions.Start;
                view.IgnorePixelScaling = scenario.IgnorePixelScaling;
                view.WidthRequest = scenario.Width;
                view.HeightRequest = scenario.Height;
                _scenarioApplied = true;
                view.InvalidateSurface();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private bool MatchesCurrentScenario(NeraSurfaceMetrics surface)
    {
        var scenario = Scenarios[_scenarioIndex];
        return surface.IgnorePixelScaling ==
                scenario.IgnorePixelScaling &&
            surface.Orientation == scenario.Orientation &&
            surface.WidthClass == scenario.WidthClass &&
            Math.Abs(surface.ViewportWidth - scenario.Width) <=
                LayoutTolerance &&
            Math.Abs(surface.ViewportHeight - scenario.Height) <=
                LayoutTolerance;
    }

    private void ValidateScenario(
        NeraSpreadsheetView view,
        SurfaceObservation observation)
    {
        var scenario = Scenarios[_scenarioIndex];
        Require(observation.Surface.IgnorePixelScaling ==
                scenario.IgnorePixelScaling &&
                observation.Surface.Orientation ==
                scenario.Orientation &&
                observation.Surface.WidthClass ==
                scenario.WidthClass,
            "Requested scaling/orientation/width class was not reached.");
        ValidatePersistentState(view);

        _oldHandler = view.Handler;
        _oldPlatformView = view.Handler?.PlatformView;
        _oldContext = view.GRContext;
        _beforeDetachGpu = view.GpuContextDiagnostics;
    }

    private void QueueRecreation(NeraSpreadsheetView view)
    {
        _stage = SmokeStage.AwaitRecreation;
        Dispatcher.Dispatch(() =>
        {
            try
            {
                _host.Children.Remove(view);
                view.Handler = null!;
                _lostGpu = view.GpuContextDiagnostics;
                Require(!_lostGpu.HasActiveContext &&
                        !_lostGpu.HasActiveFrame,
                    "Detached view retained GPU state.");
                Require(
                    _lostGpu.ContextLostCount ==
                    _beforeDetachGpu.ContextLostCount + 1L,
                    "Detachment did not record one context loss.");
                Require(
                    _lostGpu.FramesAbandoned ==
                    _beforeDetachGpu.FramesAbandoned,
                    "Detachment abandoned a completed frame.");
                ValidateClearedInput(view);

                _recreationApplied = true;
                _host.Children.Add(view);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private void ValidateRecreated(
        NeraSpreadsheetView view,
        SurfaceObservation observation)
    {
        Require(!ReferenceEquals(_oldHandler, view.Handler) &&
                !ReferenceEquals(
                    _oldPlatformView,
                    view.Handler?.PlatformView) &&
                !ReferenceEquals(_oldContext, view.GRContext),
            "Native handler/surface/context was reused.");

        var gpu = view.GpuContextDiagnostics;
        Require(
            gpu.ContextGeneration ==
                _beforeDetachGpu.ContextGeneration + 1L &&
            gpu.ContextCreatedCount ==
                _beforeDetachGpu.ContextCreatedCount + 1L &&
            gpu.ContextRecreatedCount ==
                _beforeDetachGpu.ContextRecreatedCount + 1L &&
            gpu.ContextLostCount == _lostGpu.ContextLostCount,
            "Context recreation counters are invalid.");
        Require(observation.Surface.ContextGeneration ==
                gpu.ContextGeneration,
            "Recreated metrics retained the old generation.");
        Require(_handlers.All(handler =>
                !ReferenceEquals(handler, view.Handler)) &&
                _contexts.All(context =>
                !ReferenceEquals(context, view.GRContext)),
            "An earlier handler or GRContext was reused.");

        _handlers.Add(view.Handler!);
        _contexts.Add(view.GRContext!);
        ValidatePersistentState(view);
    }

    private void AdvanceScenario(
        NeraSpreadsheetView view,
        NeraSurfaceMetrics finalSurface)
    {
        _scenarioIndex++;
        if (_scenarioIndex < Scenarios.Length)
        {
            QueueScenario(view);
            return;
        }

        CompleteSuccessfully(view, finalSurface);
    }

    private void ValidatePersistentState(NeraSpreadsheetView view)
    {
        Require(Math.Abs(view.Zoom - ExpectedZoom) <= 1e-9,
            "Zoom state was lost.");
        Require(Math.Abs(view.ScrollSnapshot.OffsetX -
                    ExpectedOffsetX) <= 1e-6 &&
                Math.Abs(view.ScrollSnapshot.OffsetY -
                    ExpectedOffsetY) <= 1e-6 &&
                Math.Abs(view.ScrollSnapshot.TargetX -
                    ExpectedOffsetX) <= 1e-6 &&
                Math.Abs(view.ScrollSnapshot.TargetY -
                    ExpectedOffsetY) <= 1e-6 &&
                !view.HasRenderLoop,
            "Fractional viewport state was lost.");

        var session = view.Session
            ?? throw new InvalidOperationException(
                "Spreadsheet session was lost.");
        if (_sessionIdentity is not null)
        {
            Require(ReferenceEquals(_sessionIdentity, session),
                "Spreadsheet session was replaced.");
        }

        var selection = session.Selection.Capture();
        Require(selection.ActiveCell == _expectedSelectionActive &&
                selection.AnchorCell == _expectedSelectionAnchor &&
                selection.Version == _expectedSelectionVersion &&
                selection.Ranges.SequenceEqual(_expectedSelectionRanges),
            "Selection state was lost.");
        ValidateClearedInput(view);
    }

    private static void ValidateClearedInput(NeraSpreadsheetView view)
    {
        var input = view.InputDiagnostics;
        Require(input.PressedEvents == 0L &&
                input.MovedEvents == 0L &&
                input.ReleasedEvents == 0L &&
                input.CancelledEvents == 0L &&
                input.WheelEvents == 0L &&
                input.PanUpdates == 0L &&
                input.PinchUpdates == 0L &&
                input.TapSelections == 0L &&
                input.IgnoredEvents == 0L &&
                input.ActiveTouchCount == 0 &&
                !input.IsPinching &&
                !input.IsTapEligible,
            "Pointer state was introduced or retained.");
    }

    private void CompleteSuccessfully(
        NeraSpreadsheetView view,
        NeraSurfaceMetrics finalSurface)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }
        _stage = SmokeStage.Completed;

        var gpu = view.GpuContextDiagnostics;
        Require(
            gpu.ContextGeneration ==
                _initialGpu.ContextGeneration + Scenarios.Length &&
            gpu.ContextCreatedCount ==
                _initialGpu.ContextCreatedCount + Scenarios.Length &&
            gpu.ContextLostCount ==
                _initialGpu.ContextLostCount + Scenarios.Length &&
            gpu.ContextRecreatedCount ==
                _initialGpu.ContextRecreatedCount + Scenarios.Length,
            "Final context recreation counts are invalid.");
        Require(gpu.FramesStarted == gpu.FramesCompleted &&
                gpu.FramesFailed == 0L &&
                gpu.FramesAbandoned == 0L &&
                gpu.StaleFrameTransitionsRejected == 0L,
            "Final GPU frame accounting is invalid.");
        ValidatePersistentState(view);

        WriteResult(new
        {
            status = "success",
            frameCount = _frameCount,
            recreationCycles = Scenarios.Length,
            observations = _observations,
            firstContextGeneration = _initialGpu.ContextGeneration,
            finalContextGeneration = gpu.ContextGeneration,
            contextCreatedCount = gpu.ContextCreatedCount,
            contextLostCount = gpu.ContextLostCount,
            contextRecreatedCount = gpu.ContextRecreatedCount,
            framesStarted = gpu.FramesStarted,
            framesCompleted = gpu.FramesCompleted,
            framesFailed = gpu.FramesFailed,
            framesAbandoned = gpu.FramesAbandoned,
            staleFrameTransitions = gpu.StaleFrameTransitionsRejected,
            finalSurface,
            zoom = view.Zoom,
            offsetX = view.ScrollSnapshot.OffsetX,
            offsetY = view.ScrollSnapshot.OffsetY,
            selectionVersion = _expectedSelectionVersion,
            selectionRangeCount = _expectedSelectionRanges.Length,
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
                stage = _stage.ToString(),
                scenarioIndex = _scenarioIndex,
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
                    $"The MAUI scale smoke did not complete within {SmokeTimeout}."));
            }
        });
    }

    private static void WriteResult(object result)
    {
        var path = Environment.GetEnvironmentVariable(
            "NERA_MAUI_SMOKE_RESULT");
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "NERA_MAUI_SMOKE_RESULT must identify the scale-smoke result file.");
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "The scale-smoke result file has no parent directory."));
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
                        ? "Nera MAUI scale smoke"
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

    private sealed record SurfaceObservation(
        string Scenario,
        bool Recreated,
        double ContentsScale,
        double NativeWidth,
        double NativeHeight,
        NeraSurfaceMetrics Surface,
        int HandlerIdentity,
        int ContextIdentity);

    private readonly record struct SurfaceScenario(
        string Name,
        bool IgnorePixelScaling,
        double Width,
        double Height,
        NeraSurfaceOrientation Orientation,
        NeraSurfaceWidthClass WidthClass);

    private enum SmokeStage
    {
        InitialFrame,
        AwaitBaseline,
        AwaitScenario,
        AwaitRecreation,
        Completed,
    }
}
