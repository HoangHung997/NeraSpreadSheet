using System.Text.Json;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Maui;
using NeraSpreadSheet.Rendering.Spreadsheet;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace NeraSpreadSheet.Maui.Windows.AnalyticsSmoke;

internal sealed class SmokePage : ContentPage, IDisposable
{
    private static readonly TimeSpan SmokeTimeout = TimeSpan.FromSeconds(45d);
    private static readonly JsonSerializerOptions ResultJsonOptions = new()
    {
        WriteIndented = true,
    };
    private static readonly string[] ExpectedActions =
        ["Select", "Move", "Resize", "Delete"];
    private const double MoveDeltaX = 28.5d;
    private const double MoveDeltaY = 17.25d;
    private const double Tolerance = 1e-6;

    private readonly Grid _host = new();
    private readonly Workbook _workbook = CreateWorkbook();
    private NeraSpreadsheetView? _view;
    private SpreadsheetAnalyticsItemKey _item;
    private RectD _beforeBounds;
    private RectD _expectedMovedBounds;
    private SmokeStage _stage;
    private int _frameCount;
    private int _finished;
    private int _surfaceWidth;
    private int _surfaceHeight;
    private int _historyBeforeDrag;
    private bool _chartInserted;
    private bool _touchApplied;
    private bool _disposed;

    public SmokePage()
    {
        Title = "NeraSpreadSheet MAUI analytics interaction smoke";
        Content = _host;
        Loaded += OnLoaded;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Loaded -= OnLoaded;
        if (_view is { } view)
        {
            view.Loaded -= OnViewLoaded;
            view.PaintSurface -= OnPaintSurface;
            _host.Children.Remove(view);
            view.Dispose();
            _view = null;
        }
        GC.SuppressFinalize(this);
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        _ = MonitorTimeoutAsync();
        _view = new NeraSpreadsheetView
        {
            Workbook = _workbook,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        _view.PaintSurface += OnPaintSurface;
        _view.Loaded += OnViewLoaded;
        _host.Children.Add(_view);
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
        if (_disposed ||
            sender is not NeraSpreadsheetView view ||
            !ReferenceEquals(view, _view) ||
            Volatile.Read(ref _finished) != 0)
        {
            return;
        }

        try
        {
            _frameCount++;
            _surfaceWidth = e.Info.Width;
            _surfaceHeight = e.Info.Height;
            ValidateLoadedHost(view);

            switch (_stage)
            {
                case SmokeStage.InitialFrame:
                    QueueChartCreation(view);
                    break;
                case SmokeStage.AwaitChart when _chartInserted:
                    ValidateInitialAccessibility(view);
                    QueueTouchTransform(view);
                    break;
                case SmokeStage.AwaitTouchValidation when _touchApplied:
                    ValidateTouchAndComplete(view);
                    break;
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void ValidateLoadedHost(NeraSpreadsheetView view)
    {
        var handler = view.Handler
            ?? throw new InvalidOperationException(
                "The analytics smoke view did not receive a MAUI handler.");
        Require(handler.PlatformView is not null,
            "The analytics smoke view did not receive a native platform view.");
        Require(view.GRContext is not null,
            "The analytics smoke view did not receive a live Skia GRContext.");
        Require(view.Session is not null,
            "The analytics smoke workbook did not create a spreadsheet session.");
        Require(view.GpuContextDiagnostics.FramesFailed == 0L,
            "The analytics smoke observed a failed GPU frame.");
    }

    private void QueueChartCreation(NeraSpreadsheetView view)
    {
        _stage = SmokeStage.AwaitChart;
        Dispatcher.Dispatch(() =>
        {
            try
            {
                var session = view.Session
                    ?? throw new InvalidOperationException(
                        "The loaded analytics view lost its session before chart creation.");
                var chart = session.Analytics.InsertChart(
                    new CellRange(
                        new CellAddress(0, 0),
                        new CellAddress(3, 1)),
                    SpreadsheetChartType.Column,
                    title: "Loaded analytics",
                    requestedName: "LoadedAnalyticsChart");
                _item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
                _beforeBounds = session.AnalyticsPlacements
                    .GetPlacement(_item)
                    .DocumentBounds;
                _expectedMovedBounds = _beforeBounds.Translate(
                    MoveDeltaX,
                    MoveDeltaY);
                _historyBeforeDrag = session.History.UndoCount;
                _chartInserted = true;
                view.InvalidateSurface();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private void ValidateInitialAccessibility(NeraSpreadsheetView view)
    {
        var node = GetNode(view);
        Require(node.Name == "LoadedAnalyticsChart",
            "The loaded chart accessibility name did not resolve from its definition.");
        Require(node.Role == SpreadsheetAnalyticsAccessibleRole.Chart,
            "The loaded chart exposed the wrong accessibility role.");
        Require(!node.IsSelected,
            "The loaded chart was unexpectedly selected before touch input.");
        Require(node.Actions.SequenceEqual(ExpectedActions),
            "The loaded chart exposed the wrong accessibility action set.");
        Require(
            node.AutomationId == $"analytics-chart-{_item.Id:N}",
            "The loaded chart automation ID was not deterministic.");
    }

    private void QueueTouchTransform(NeraSpreadsheetView view)
    {
        _stage = SmokeStage.AwaitTouchValidation;
        Dispatcher.Dispatch(() =>
        {
            try
            {
                var session = view.Session
                    ?? throw new InvalidOperationException(
                        "The loaded analytics view lost its session before touch input.");
                var node = GetNode(view);
                var visible = node.ViewportBounds.Intersect(node.ClipBounds);
                Require(!visible.IsEmpty,
                    "The loaded chart did not expose a visible interaction fragment.");

                var startBody = new PointD(
                    visible.Left + (visible.Width / 2d),
                    visible.Top + (visible.Height / 2d));
                var endBody = new PointD(
                    startBody.X + MoveDeltaX,
                    startBody.Y + MoveDeltaY);
                var start = BodyToScreen(view, startBody);
                var secondary = BodyToScreen(
                    view,
                    new PointD(startBody.X + 32d, startBody.Y + 24d));
                var end = BodyToScreen(view, endBody);

                ProcessTouch(view, 501L, SKTouchAction.Pressed, start, true);
                Require(session.AnalyticsInteraction.SelectedItem == _item,
                    "The loaded chart touch press did not select the analytics item.");
                Require(session.AnalyticsInteraction.IsTransforming,
                    "The loaded chart touch press did not begin a transform.");

                ProcessTouch(view, 502L, SKTouchAction.Pressed, secondary, true);
                ProcessTouch(view, 502L, SKTouchAction.Released, secondary, false);
                Require(session.AnalyticsInteraction.IsTransforming,
                    "A secondary touch stole the active analytics transform.");

                ProcessTouch(view, 501L, SKTouchAction.Moved, end, true);
                ProcessTouch(view, 501L, SKTouchAction.Released, end, false);
                _touchApplied = true;
                view.InvalidateSurface();
            }
            catch (Exception exception)
            {
                Fail(exception);
            }
        });
    }

    private void ValidateTouchAndComplete(NeraSpreadsheetView view)
    {
        var session = view.Session
            ?? throw new InvalidOperationException(
                "The loaded analytics view lost its session after touch input.");
        var moved = session.AnalyticsPlacements.GetPlacement(_item).DocumentBounds;
        Require(AreClose(moved, _expectedMovedBounds),
            "The loaded chart touch drag did not commit the expected document bounds.");
        Require(session.History.UndoCount == _historyBeforeDrag + 1,
            "The loaded chart touch drag did not create exactly one undo entry.");
        Require(session.AnalyticsInteraction.SelectedItem == _item,
            "The loaded chart lost analytics selection after touch release.");
        Require(!session.AnalyticsInteraction.IsTransforming,
            "The loaded chart retained a stale transform after touch release.");

        var node = GetNode(view);
        Require(node.IsSelected,
            "The loaded chart accessibility node did not reflect analytics selection.");
        Require(node.Name == "LoadedAnalyticsChart" &&
                node.Role == SpreadsheetAnalyticsAccessibleRole.Chart,
            "The loaded chart accessibility metadata changed after touch movement.");

        var input = view.InputDiagnostics;
        Require(input.PressedEvents == 0L &&
                input.MovedEvents == 0L &&
                input.ReleasedEvents == 0L &&
                input.PanUpdates == 0L &&
                input.PinchUpdates == 0L &&
                input.TapSelections == 0L &&
                input.ActiveTouchCount == 0,
            "Analytics-owned touches leaked into the spreadsheet pan/pinch/tap state machine.");

        Require(session.Undo(),
            "The loaded analytics touch transform could not be undone.");
        Require(
            AreClose(
                session.AnalyticsPlacements.GetPlacement(_item).DocumentBounds,
                _beforeBounds),
            "Undo did not restore the loaded chart placement.");
        Require(session.Redo(),
            "The loaded analytics touch transform could not be redone.");
        Require(
            AreClose(
                session.AnalyticsPlacements.GetPlacement(_item).DocumentBounds,
                _expectedMovedBounds),
            "Redo did not restore the loaded chart touch placement.");

        CompleteSuccessfully(view, input);
    }

    private SpreadsheetAnalyticsAccessibleNode GetNode(NeraSpreadsheetView view) =>
        view.AnalyticsAccessibilityNodes.Single(node => node.Item == _item);

    private SKPoint BodyToScreen(
        NeraSpreadsheetView view,
        PointD bodyPoint)
    {
        var zoom = view.Zoom;
        var chrome = SpreadsheetChromeGeometry.Calculate(
            _surfaceWidth / zoom,
            _surfaceHeight / zoom,
            view.RenderTheme);
        return new SKPoint(
            (float)((chrome.RowHeaderWidth + bodyPoint.X) * zoom),
            (float)((chrome.ColumnHeaderHeight + bodyPoint.Y) * zoom));
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
            "The loaded analytics touch path did not mark the event handled.");
    }

    private void CompleteSuccessfully(
        NeraSpreadsheetView view,
        NeraSpreadsheetInputDiagnostics input)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }
        _stage = SmokeStage.Completed;
        Require(_frameCount >= 3,
            "The loaded analytics smoke completed without the required frame lifecycle.");

        var gpu = view.GpuContextDiagnostics;
        Require(gpu.FramesFailed == 0L &&
                gpu.StaleFrameTransitionsRejected == 0L,
            "The loaded analytics smoke introduced a failed or stale GPU frame.");

        WriteResult(new
        {
            status = "success",
            frameCount = _frameCount,
            analyticsName = "LoadedAnalyticsChart",
            analyticsAutomationId = $"analytics-chart-{_item.Id:N}",
            moveDeltaX = MoveDeltaX,
            moveDeltaY = MoveDeltaY,
            historyBeforeDrag = _historyBeforeDrag,
            historyAfterRedo = view.Session?.History.UndoCount,
            inputPressed = input.PressedEvents,
            inputMoved = input.MovedEvents,
            inputReleased = input.ReleasedEvents,
            inputPanUpdates = input.PanUpdates,
            inputPinchUpdates = input.PinchUpdates,
            inputTapSelections = input.TapSelections,
            gpuFramesCompleted = gpu.FramesCompleted,
            gpuFramesFailed = gpu.FramesFailed,
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
        if (_disposed || Volatile.Read(ref _finished) != 0)
        {
            return;
        }

        Dispatcher.Dispatch(() =>
        {
            if (!_disposed && Volatile.Read(ref _finished) == 0)
            {
                Fail(new TimeoutException(
                    $"The loaded analytics smoke did not complete within {SmokeTimeout}."));
            }
        });
    }

    private static bool AreClose(RectD actual, RectD expected) =>
        Math.Abs(actual.X - expected.X) <= Tolerance &&
        Math.Abs(actual.Y - expected.Y) <= Tolerance &&
        Math.Abs(actual.Width - expected.Width) <= Tolerance &&
        Math.Abs(actual.Height - expected.Height) <= Tolerance;

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
                "The analytics smoke result file has no parent directory."));
        File.WriteAllText(
            fullPath,
            JsonSerializer.Serialize(result, ResultJsonOptions));
    }

    private static Workbook CreateWorkbook()
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        worksheet.SetValue(new CellAddress(0, 0), "Category");
        worksheet.SetValue(new CellAddress(0, 1), "Value");
        worksheet.SetValue(new CellAddress(1, 0), "A");
        worksheet.SetValue(new CellAddress(1, 1), 10d);
        worksheet.SetValue(new CellAddress(2, 0), "B");
        worksheet.SetValue(new CellAddress(2, 1), 20d);
        worksheet.SetValue(new CellAddress(3, 0), "C");
        worksheet.SetValue(new CellAddress(3, 1), 30d);
        return workbook;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private enum SmokeStage
    {
        InitialFrame,
        AwaitChart,
        AwaitTouchValidation,
        Completed,
    }
}
