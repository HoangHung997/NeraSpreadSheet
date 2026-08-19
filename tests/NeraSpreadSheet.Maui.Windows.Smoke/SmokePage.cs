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
    private int _stage;
    private int _frameCount;
    private int _finished;
    private bool _mutationApplied;

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
                    CaptureFirstFrame(view);
                    QueueViewportMutation(view);
                    break;
                case 1 when _mutationApplied:
                    ValidateViewportMutation(view);
                    QueueSurfaceRecreation(view);
                    break;
                case 2:
                    ValidateRecreatedSurface(view);
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

    private void CaptureFirstFrame(NeraSpreadsheetView view)
    {
        _firstHandler = view.Handler;
        _firstPlatformView = view.Handler?.PlatformView;
        _firstContext = view.GRContext;
        _firstGpuDiagnostics = view.GpuContextDiagnostics;
        _stage = 1;
    }

    private void QueueViewportMutation(NeraSpreadsheetView view)
    {
        Dispatcher.Dispatch(() =>
        {
            try
            {
                view.ZoomTo(ExpectedZoom, 320d, 220d);
                view.ScrollTo(ExpectedOffsetX, ExpectedOffsetY, animated: false);
                _workbook.Worksheets[0].SetValue(
                    new CellAddress(0, 0),
                    "Nera MAUI mutation rendered");
                _mutationApplied = true;
                view.InvalidateSurface();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private static void ValidateViewportMutation(NeraSpreadsheetView view)
    {
        Require(Math.Abs(view.Zoom - ExpectedZoom) <= 1e-9,
            $"Anchored zoom did not reach {ExpectedZoom}.");
        Require(Math.Abs(view.ScrollSnapshot.OffsetX - ExpectedOffsetX) <= 1e-6,
            $"Fractional horizontal scroll did not reach {ExpectedOffsetX}.");
        Require(Math.Abs(view.ScrollSnapshot.OffsetY - ExpectedOffsetY) <= 1e-6,
            $"Fractional vertical scroll did not reach {ExpectedOffsetY}.");
    }

    private void QueueSurfaceRecreation(NeraSpreadsheetView view)
    {
        _stage = 2;
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

                _host.Children.Add(view);
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private void ValidateRecreatedSurface(NeraSpreadsheetView view)
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
        Require(Math.Abs(view.ScrollSnapshot.OffsetX - ExpectedOffsetX) <= 1e-6 &&
                Math.Abs(view.ScrollSnapshot.OffsetY - ExpectedOffsetY) <= 1e-6,
            "The same view lost its fractional scroll state during handler recreation.");
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
            width = e.Info.Width,
            height = e.Info.Height,
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
