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
    private const double ExpectedZoom = 1.375d;
    private const double ExpectedOffsetX = 17.25d;
    private const double ExpectedOffsetY = 31.75d;

    private readonly Grid _host = new();
    private readonly Workbook _workbook = CreateWorkbook();
    private readonly CancellationTokenSource _timeoutCancellation = new();
    private NeraSpreadsheetView? _view;
    private IElementHandler? _firstHandler;
    private object? _firstPlatformView;
    private GRContext? _firstContext;
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
        _ = MonitorTimeoutAsync(_timeoutCancellation.Token);
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
    }

    private void CaptureFirstFrame(NeraSpreadsheetView view)
    {
        _firstHandler = view.Handler;
        _firstPlatformView = view.Handler?.PlatformView;
        _firstContext = view.GRContext;
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

    private void QueueSurfaceRecreation(NeraSpreadsheetView oldView)
    {
        _stage = 2;
        _view = null;
        Dispatcher.Dispatch(() =>
        {
            try
            {
                oldView.PaintSurface -= OnPaintSurface;
                oldView.Loaded -= OnViewLoaded;
                _host.Children.Remove(oldView);
                oldView.Handler?.DisconnectHandler();
                oldView.Dispose();

                var replacement = CreateView();
                _view = replacement;
                _host.Children.Add(replacement);
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
            "The replacement view reused the disconnected MAUI handler.");
        Require(_firstPlatformView is not null &&
                !ReferenceEquals(_firstPlatformView, view.Handler?.PlatformView),
            "The replacement view reused the disconnected native surface.");
        Require(_firstContext is not null &&
                !ReferenceEquals(_firstContext, view.GRContext),
            "The replacement native surface did not create a new Skia GRContext.");
        Require(Math.Abs(view.Zoom - 1d) <= 1e-9,
            "The replacement view did not start with an independent zoom state.");
        Require(Math.Abs(view.ScrollSnapshot.OffsetX) <= 1e-9 &&
                Math.Abs(view.ScrollSnapshot.OffsetY) <= 1e-9,
            "The replacement view did not start with independent scroll state.");
    }

    private void CompleteSuccessfully(
        NeraSpreadsheetView view,
        SKPaintGLSurfaceEventArgs e)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        _timeoutCancellation.Cancel();
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
            replacementHandler = RuntimeHelpers.GetHashCode(view.Handler!),
            firstContext = RuntimeHelpers.GetHashCode(_firstContext!),
            replacementContext = RuntimeHelpers.GetHashCode(view.GRContext!),
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

        _timeoutCancellation.Cancel();
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

    private async Task MonitorTimeoutAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SmokeTimeout, cancellationToken).ConfigureAwait(false);
            Dispatcher.Dispatch(() =>
                Fail(new TimeoutException(
                    $"The loaded MAUI GPU smoke did not complete within {SmokeTimeout}.")));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
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
            JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions { WriteIndented = true }));
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
