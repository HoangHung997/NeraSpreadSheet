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
    private const string ResultArgument = "--nera-smoke-result";
    private const string UnifiedLogResultMarker = "NERA_MAUI_SMOKE_RESULT:";
    private const string UnifiedLogFileErrorMarker = "NERA_MAUI_SMOKE_RESULT_FILE_ERROR:";
    private static readonly TimeSpan SmokeTimeout = TimeSpan.FromSeconds(45d);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly Grid _host = new();
    private readonly Workbook _workbook = CreateWorkbook();
    private NeraSpreadsheetView? _view;
    private SpreadsheetAnalyticsItemKey _chartItem;
    private SpreadsheetAnalyticsItemKey _pivotItem;
    private int _frameCount;
    private int _analyticsInserted;
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
            if (Volatile.Read(ref _analyticsInserted) == 0)
            {
                QueueAnalyticsCreation(view);
                return;
            }

            if (view.AnalyticsAccessibilityNodes.Count == 2)
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

    private void QueueAnalyticsCreation(NeraSpreadsheetView view)
    {
        if (Interlocked.CompareExchange(ref _analyticsInserted, -1, 0) != 0)
        {
            return;
        }

        Dispatcher.Dispatch(() =>
        {
            try
            {
                var session = view.Session
                    ?? throw new InvalidOperationException(
                        "The Mac Catalyst analytics smoke lost its session before analytics creation.");
                var sourceRange = new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(3, 1));
                var chart = session.Analytics.InsertChart(
                    sourceRange,
                    SpreadsheetChartType.Column,
                    title: "Mac Catalyst accessibility",
                    requestedName: "MacAccessibilityChart");
                var pivot = session.Analytics.InsertPivot(
                    sourceRange,
                    rowFieldColumnIndex: 0,
                    valueFieldColumnIndex: 1,
                    aggregation: SpreadsheetPivotAggregation.Sum,
                    requestedName: "MacAccessibilityPivot");
                _chartItem = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
                _pivotItem = SpreadsheetAnalyticsItemKey.ForPivot(pivot.Id);
                Volatile.Write(ref _analyticsInserted, 1);
                view.InvalidateSurface();
            }
            catch (Exception exception)
            {
                Volatile.Write(ref _analyticsInserted, 1);
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

        var projectedNodes = view.AnalyticsAccessibilityNodes;
        Require(projectedNodes.Count == 2,
            $"Expected two projected analytics nodes but found {projectedNodes.Count}.");
        Require(projectedNodes.Any(node =>
                node.Item == _chartItem &&
                node.Name == "MacAccessibilityChart" &&
                node.Role == SpreadsheetAnalyticsAccessibleRole.Chart),
            "The Mac Catalyst projection omitted the inserted chart node.");
        Require(projectedNodes.Any(node =>
                node.Item == _pivotItem &&
                node.Name == "MacAccessibilityPivot" &&
                node.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable),
            "The Mac Catalyst projection omitted the inserted pivot node.");

        var container = (IUIAccessibilityContainer)host;
        var rawElements = container.GetAccessibilityElements()
            ?? throw new InvalidOperationException(
                "The Mac Catalyst accessibility container did not expose accessibilityElements.");
        Require(rawElements is NSArray,
            "The Mac Catalyst accessibilityElements payload was not an NSArray.");
        var nativeElements = ((NSArray)rawElements).ToArray<UIAccessibilityElement>();
        Require(nativeElements.Length == 2,
            $"Expected two native analytics accessibility elements but found {nativeElements.Length}.");

        var chart = FindNativeElement(
            nativeElements,
            $"analytics-chart-{_chartItem.Id:N}");
        var pivot = FindNativeElement(
            nativeElements,
            $"analytics-pivot-{_pivotItem.Id:N}");
        ValidateNativeElement(
            chart,
            "MacAccessibilityChart",
            "Biểu đồ",
            "chart");
        ValidateNativeElement(
            pivot,
            "MacAccessibilityPivot",
            "Bảng tổng hợp",
            "pivot");

        ActivateAndRequireSelection(view, chart, _chartItem, "chart");
        ActivateAndRequireSelection(view, pivot, _pivotItem, "pivot");

        Complete(new
        {
            status = "success",
            frameCount = _frameCount,
            nativeElementCount = nativeElements.Length,
            chart = DescribeNativeElement(chart),
            pivot = DescribeNativeElement(pivot),
            chartActivationVerified = true,
            pivotActivationVerified = true,
            selectedItem = view.Session?.AnalyticsInteraction.SelectedItem?.ToString(),
            cachedTypefaces = view.CachedTypefaceCount,
        });
    }

    private static UIAccessibilityElement FindNativeElement(
        IEnumerable<UIAccessibilityElement> elements,
        string identifier) =>
        elements.SingleOrDefault(element =>
            string.Equals(
                element.AccessibilityIdentifier,
                identifier,
                StringComparison.Ordinal))
        ?? throw new InvalidOperationException(
            $"The Mac Catalyst accessibility container did not expose '{identifier}'.");

    private static void ValidateNativeElement(
        UIAccessibilityElement element,
        string expectedLabel,
        string expectedRole,
        string itemKind)
    {
        Require(element.AccessibilityLabel == expectedLabel,
            $"The native Mac Catalyst {itemKind} label did not match the analytics name.");
        Require(
            element.AccessibilityValue?.Contains(expectedRole, StringComparison.Ordinal) == true,
            $"The native Mac Catalyst {itemKind} value omitted the localized role.");
        Require(
            element.AccessibilityHint?.Contains("Chạm hai lần để chọn", StringComparison.Ordinal) == true,
            $"The native Mac Catalyst {itemKind} hint omitted activation guidance.");

        var frame = element.AccessibilityFrame;
        Require(!frame.IsEmpty &&
                double.IsFinite(frame.X) &&
                double.IsFinite(frame.Y) &&
                double.IsFinite(frame.Width) &&
                double.IsFinite(frame.Height) &&
                frame.Width > 0d &&
                frame.Height > 0d,
            $"The native Mac Catalyst {itemKind} element exposed invalid screen bounds.");

        var activationSelector = new Selector("accessibilityActivate");
        Require(element.RespondsToSelector(activationSelector),
            $"The native Mac Catalyst {itemKind} element does not expose accessibilityActivate.");
    }

    private static void ActivateAndRequireSelection(
        NeraSpreadsheetView view,
        UIAccessibilityElement element,
        SpreadsheetAnalyticsItemKey expectedItem,
        string itemKind)
    {
        var activationSelector = new Selector("accessibilityActivate");
        Require(
            UIApplication.SharedApplication.SendAction(
                activationSelector,
                element,
                null,
                null),
            $"UIKit did not dispatch accessibilityActivate to the native {itemKind} element.");
        Require(view.Session?.AnalyticsInteraction.SelectedItem == expectedItem,
            $"Mac Catalyst accessibilityActivate did not select the {itemKind} in the spreadsheet session.");
    }

    private static object DescribeNativeElement(UIAccessibilityElement element)
    {
        var frame = element.AccessibilityFrame;
        return new
        {
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
        };
    }

    private void Complete(object result)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        EmitResult(result);
        Environment.Exit(0);
    }

    private void Fail(Exception exception)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        EmitResult(new
        {
            status = "failure",
            frameCount = _frameCount,
            analyticsInserted = Volatile.Read(ref _analyticsInserted),
            accessibilityNodeCount = _view?.AnalyticsAccessibilityNodes.Count,
            error = exception.ToString(),
        });
        Environment.Exit(1);
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

    private static void EmitResult(object result)
    {
        var compactJson = JsonSerializer.Serialize(result);
        ObjCRuntime.Runtime.NSLog(UnifiedLogResultMarker + compactJson);

        try
        {
            var fullPath = Path.GetFullPath(ResolveResultPath());
            Directory.CreateDirectory(
                Path.GetDirectoryName(fullPath)
                ?? throw new InvalidOperationException(
                    "The Mac Catalyst smoke result file has no parent directory."));
            File.WriteAllText(
                fullPath,
                JsonSerializer.Serialize(result, JsonOptions));
        }
        catch (Exception exception)
        {
            ObjCRuntime.Runtime.NSLog(
                UnifiedLogFileErrorMarker +
                JsonSerializer.Serialize(new
                {
                    error = exception.ToString(),
                }));
        }
    }

    private static string ResolveResultPath()
    {
        var environmentPath = Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_RESULT");
        if (!string.IsNullOrWhiteSpace(environmentPath))
        {
            return environmentPath;
        }

        var arguments = Environment.GetCommandLineArgs();
        for (var index = 0; index < arguments.Length - 1; index++)
        {
            if (string.Equals(arguments[index], ResultArgument, StringComparison.Ordinal) &&
                !string.IsNullOrWhiteSpace(arguments[index + 1]))
            {
                return arguments[index + 1];
            }
        }

        throw new InvalidOperationException(
            "The Mac Catalyst smoke result path was not supplied through NERA_MAUI_SMOKE_RESULT or --nera-smoke-result.");
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
