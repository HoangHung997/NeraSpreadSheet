using System.Text.Json;
using Foundation;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Maui;
using ObjCRuntime;
using SkiaSharp.Views.Maui;
using UIKit;

namespace NeraSpreadSheet.Maui.MacCatalyst.AnalyticsSmoke;

internal sealed class SmokePage : ContentPage, IDisposable
{
    private static readonly TimeSpan SmokeTimeout = TimeSpan.FromSeconds(45d);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly Grid _host = new();
    private readonly Workbook _workbook = CreateWorkbook();
    private NeraSpreadsheetView? _view;
    private SpreadsheetAnalyticsItemKey _item;
    private int _frameCount;
    private int _chartInserted;
    private int _finished;
    private bool _disposed;

    public SmokePage()
    {
        Title = "Nera Mac Catalyst analytics accessibility smoke";
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
            Volatile.Read(ref _finished) != 0 ||
            sender is not NeraSpreadsheetView view ||
            !ReferenceEquals(view, _view))
        {
            return;
        }

        try
        {
            _frameCount++;
            ValidateLoadedHost(view);
            if (Volatile.Read(ref _chartInserted) == 0)
            {
                QueueChartCreation(view);
                return;
            }

            if (view.AnalyticsAccessibilityNodes.Count == 1)
            {
                ValidateNativeAccessibility(view);
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private static void ValidateLoadedHost(NeraSpreadsheetView view)
    {
        Require(view.Handler?.PlatformView is UIView,
            "The Mac Catalyst analytics smoke did not receive a native UIView.");
        Require(view.GRContext is not null,
            "The Mac Catalyst analytics smoke did not receive a live Skia GRContext.");
        Require(view.Session is not null,
            "The Mac Catalyst analytics smoke workbook did not create a spreadsheet session.");
        Require(view.GpuContextDiagnostics.FramesFailed == 0L,
            "The Mac Catalyst analytics smoke observed a failed GPU frame.");
    }

    private void QueueChartCreation(NeraSpreadsheetView view)
    {
        if (Interlocked.CompareExchange(ref _chartInserted, -1, 0) != 0)
        {
            return;
        }

        Dispatcher.Dispatch(() =>
        {
            try
            {
                var session = view.Session
                    ?? throw new InvalidOperationException(
                        "The Mac Catalyst analytics smoke lost its session before chart creation.");
                var chart = session.Analytics.InsertChart(
                    new CellRange(
                        new CellAddress(0, 0),
                        new CellAddress(3, 1)),
                    SpreadsheetChartType.Column,
                    title: "Mac Catalyst accessibility",
                    requestedName: "MacAccessibilityChart");
                _item = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
                Volatile.Write(ref _chartInserted, 1);
                view.InvalidateSurface();
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _chartInserted, 1);
                Fail(exception);
            }
        });
    }

    private void ValidateNativeAccessibility(NeraSpreadsheetView view)
    {
        var host = view.Handler?.PlatformView as UIView
            ?? throw new InvalidOperationException(
                "The Mac Catalyst analytics smoke lost its native UIView.");
        Require(!host.IsAccessibilityElement,
            "The GPU host should be an accessibility container, not one monolithic element.");
        Require(host is IUIAccessibilityContainer,
            "The GPU host does not expose the native UIAccessibilityContainer protocol.");
        var container = (IUIAccessibilityContainer)host;

        var rawElements = container.GetAccessibilityElements()
            ?? throw new InvalidOperationException(
                "The Mac Catalyst accessibility container did not expose accessibilityElements.");
        Require(rawElements is NSArray,
            "The Mac Catalyst accessibilityElements payload was not an NSArray.");
        var nativeElements = ((NSArray)rawElements).ToArray<UIAccessibilityElement>();
        Require(nativeElements.Length == 1,
            $"Expected one native analytics accessibility element but found {nativeElements.Length}.");

        var element = nativeElements[0]
            ?? throw new InvalidOperationException(
                "The Mac Catalyst accessibility array contained a null element.");
        Require(element.AccessibilityLabel == "MacAccessibilityChart",
            "The native Mac Catalyst accessibility label did not match the chart name.");
        Require(element.AccessibilityIdentifier == $"analytics-chart-{_item.Id:N}",
            "The native Mac Catalyst accessibility identifier was not deterministic.");
        Require(
            element.AccessibilityValue?.Contains("Biểu đồ", StringComparison.Ordinal) == true,
            "The native Mac Catalyst accessibility value omitted the localized chart role.");
        Require(
            element.AccessibilityHint?.Contains("Chạm hai lần để chọn", StringComparison.Ordinal) == true,
            "The native Mac Catalyst accessibility hint omitted activation guidance.");

        var frame = element.AccessibilityFrame;
        Require(!frame.IsEmpty &&
                double.IsFinite(frame.X) &&
                double.IsFinite(frame.Y) &&
                double.IsFinite(frame.Width) &&
                double.IsFinite(frame.Height) &&
                frame.Width > 0d &&
                frame.Height > 0d,
            "The native Mac Catalyst accessibility element exposed invalid screen bounds.");

        using var selector = new Selector("accessibilityActivate");
        Require(element.RespondsToSelector(selector),
            "The native Mac Catalyst chart element does not respond to accessibilityActivate.");
        Require(Messaging.bool_objc_msgSend(element.Handle, selector.Handle),
            "The native Mac Catalyst accessibilityActivate action returned false.");
        Require(view.Session?.AnalyticsInteraction.SelectedItem == _item,
            "Mac Catalyst accessibilityActivate did not select the chart in the spreadsheet session.");

        Complete(new
        {
            status = "success",
            frameCount = _frameCount,
            nativeElementCount = nativeElements.Length,
            label = element.AccessibilityLabel,
            identifier = element.AccessibilityIdentifier,
            value = element.AccessibilityValue,
            hint = element.AccessibilityHint,
            bounds = new
            {
                x = frame.X,
                y = frame.Y,
                width = frame.Width,
                height = frame.Height,
            },
            activationVerified = true,
            selectedItem = view.Session?.AnalyticsInteraction.SelectedItem?.ToString(),
            cachedTypefaces = view.CachedTypefaceCount,
        });
    }

    private void Complete(object result)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        WriteResult(result);
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
                chartInserted = Volatile.Read(ref _chartInserted),
                accessibilityNodeCount = _view?.AnalyticsAccessibilityNodes.Count,
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
                    $"The Mac Catalyst analytics accessibility smoke did not complete within {SmokeTimeout}."));
            }
        });
    }

    private static void WriteResult(object result)
    {
        var path = Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_RESULT");
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException(
                "NERA_MAUI_SMOKE_RESULT must identify the Mac Catalyst smoke result file.");
        }

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "The Mac Catalyst smoke result file has no parent directory."));
        File.WriteAllText(
            fullPath,
            JsonSerializer.Serialize(result, JsonOptions));
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
}
