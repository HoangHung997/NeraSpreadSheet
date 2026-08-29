using System.Text.Json;
using Foundation;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Maui;
using ObjCRuntime;
using SkiaSharp.Views.Maui;
using UIKit;

namespace NeraSpreadSheet.Maui.iOS.AnalyticsSmoke;

internal sealed class SmokePage : ContentPage, IDisposable
{
    private const string ResultPrefix = "NERA_IOS_ANALYTICS_SMOKE:";
    private static readonly TimeSpan SmokeTimeout = TimeSpan.FromSeconds(45d);

    private readonly Grid _host = new();
    private readonly Workbook _workbook = CreateWorkbook();
    private NeraSpreadsheetView? _view;
    private SpreadsheetAnalyticsItemKey _chartItem;
    private SpreadsheetAnalyticsItemKey _pivotItem;
    private int _frameCount;
    private int _analyticsInserted;
    private int _nativeValidationStarted;
    private int _finished;
    private bool _disposed;

    public SmokePage()
    {
        Title = "Nera iOS analytics accessibility smoke";
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

            var analyticsState = Volatile.Read(ref _analyticsInserted);
            if (analyticsState == 0)
            {
                QueueAnalyticsCreation(view);
                return;
            }

            if (analyticsState < 0)
            {
                return;
            }

            if (view.AnalyticsAccessibilityNodes.Count == 2 &&
                Interlocked.CompareExchange(ref _nativeValidationStarted, 1, 0) == 0)
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
            "The iOS analytics smoke did not receive a native UIView.");
        Require(view.GRContext is not null,
            "The iOS analytics smoke did not receive a live Skia GRContext.");
        Require(view.Session is not null,
            "The iOS analytics smoke workbook did not create a spreadsheet session.");
        Require(view.GpuContextDiagnostics.FramesFailed == 0L,
            "The iOS analytics smoke observed a failed GPU frame.");
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
                        "The iOS analytics smoke lost its session before analytics creation.");
                var sourceRange = new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(3, 1));
                var chart = session.Analytics.InsertChart(
                    sourceRange,
                    SpreadsheetChartType.Column,
                    title: "iOS accessibility",
                    requestedName: "iOSAccessibilityChart");
                var pivot = session.Analytics.InsertPivot(
                    sourceRange,
                    rowFieldColumnIndex: 0,
                    valueFieldColumnIndex: 1,
                    aggregation: SpreadsheetPivotAggregation.Sum,
                    requestedName: "iOSAccessibilityPivot");
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
                "The iOS analytics smoke lost its native UIView.");
        Require(!host.IsAccessibilityElement,
            "The iOS GPU host should remain an accessibility container, not one monolithic element.");

        var projectedNodes = view.AnalyticsAccessibilityNodes;
        Require(projectedNodes.Count == 2,
            $"Expected two projected analytics nodes but found {projectedNodes.Count}.");
        Require(projectedNodes.Any(node =>
                node.Item == _chartItem &&
                node.Name == "iOSAccessibilityChart" &&
                node.Role == SpreadsheetAnalyticsAccessibleRole.Chart),
            "The iOS accessibility projection omitted the inserted chart.");
        Require(projectedNodes.Any(node =>
                node.Item == _pivotItem &&
                node.Name == "iOSAccessibilityPivot" &&
                node.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable),
            "The iOS accessibility projection omitted the inserted pivot.");

        var container = Runtime.GetINativeObject<IUIAccessibilityContainer>(
            host.Handle,
            owns: false)
            ?? throw new InvalidOperationException(
                "The iOS native UIView could not be wrapped as IUIAccessibilityContainer.");
        var accessibilityElements = container.GetAccessibilityElements() as NSArray
            ?? throw new InvalidOperationException(
                "The iOS accessibility container did not expose an NSArray payload.");
        var nativeElements = accessibilityElements.ToArray<UIAccessibilityElement>();
        Require(nativeElements.Length == 2,
            $"Expected two native iOS analytics accessibility elements but found {nativeElements.Length}.");

        var chart = FindNativeElement(
            nativeElements,
            $"analytics-chart-{_chartItem.Id:N}");
        var pivot = FindNativeElement(
            nativeElements,
            $"analytics-pivot-{_pivotItem.Id:N}");
        ValidateNativeElement(
            chart,
            "iOSAccessibilityChart",
            "Biểu đồ",
            "chart");
        ValidateNativeElement(
            pivot,
            "iOSAccessibilityPivot",
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
            gpuDiagnostics = view.GpuContextDiagnostics,
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
            $"The iOS accessibility container did not expose '{identifier}'.");

    private static void ValidateNativeElement(
        UIAccessibilityElement element,
        string expectedLabel,
        string expectedRole,
        string itemKind)
    {
        Require(element.AccessibilityLabel == expectedLabel,
            $"The native iOS {itemKind} label did not match the analytics name.");
        Require(
            element.AccessibilityValue?.Contains(expectedRole, StringComparison.Ordinal) == true,
            $"The native iOS {itemKind} value omitted its localized role.");
        Require(
            element.AccessibilityHint?.Contains("Chạm hai lần để chọn", StringComparison.Ordinal) == true,
            $"The native iOS {itemKind} hint omitted activation guidance.");

        var frame = element.AccessibilityFrame;
        Require(!frame.IsEmpty &&
                double.IsFinite(frame.X) &&
                double.IsFinite(frame.Y) &&
                double.IsFinite(frame.Width) &&
                double.IsFinite(frame.Height) &&
                frame.Width > 0d &&
                frame.Height > 0d,
            $"The native iOS {itemKind} element exposed invalid screen bounds.");

        var activationSelector = new Selector("accessibilityActivate");
        Require(element.RespondsToSelector(activationSelector),
            $"The native iOS {itemKind} element does not expose accessibilityActivate.");
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
            $"UIKit did not dispatch accessibilityActivate to the native iOS {itemKind} element.");
        Require(
            view.Session?.AnalyticsInteraction.SelectedItem == expectedItem,
            $"iOS accessibilityActivate did not select the {itemKind} in the spreadsheet session.");
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

        Console.Out.WriteLine($"{ResultPrefix}{JsonSerializer.Serialize(result)}");
        Console.Out.Flush();
        Environment.Exit(0);
    }

    private void Fail(Exception exception)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        Console.Error.WriteLine(
            $"{ResultPrefix}{JsonSerializer.Serialize(new
            {
                status = "failure",
                frameCount = _frameCount,
                analyticsInserted = Volatile.Read(ref _analyticsInserted),
                nativeValidationStarted = Volatile.Read(ref _nativeValidationStarted),
                accessibilityNodeCount = _view?.AnalyticsAccessibilityNodes.Count,
                gpuDiagnostics = _view?.GpuContextDiagnostics,
                error = exception.ToString(),
            })}");
        Console.Error.Flush();
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
                    $"The iOS analytics accessibility smoke did not complete within {SmokeTimeout}."));
            }
        });
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
