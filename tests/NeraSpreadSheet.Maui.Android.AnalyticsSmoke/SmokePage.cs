using System.Text.Json;
using AndroidX.Core.View;
using AndroidX.Core.View.Accessibility;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Maui;
using SkiaSharp.Views.Maui;
using AndroidLog = global::Android.Util.Log;
using AndroidRect = global::Android.Graphics.Rect;
using AndroidView = global::Android.Views.View;

namespace NeraSpreadSheet.Maui.Android.AnalyticsSmoke;

internal sealed class SmokePage : ContentPage, IDisposable
{
    private const string LogTag = "NeraAnalyticsSmoke";
    private const int RootVirtualViewId = AndroidView.NoId;
    private const int FirstAnalyticsVirtualViewId = 1;
    private const int SecondAnalyticsVirtualViewId = 2;
    private const int ActionClickId = 16;
    private const int ActionAccessibilityFocusId = 64;
    private const int ActionClearAccessibilityFocusId = 128;
    private static readonly TimeSpan SmokeTimeout = TimeSpan.FromSeconds(30d);

    private readonly Grid _host = new();
    private readonly Workbook _workbook = CreateWorkbook();
    private NeraSpreadsheetView? _view;
    private NeraSpreadsheetEditorHost? _editorHost;
    private bool _editorVerified;
    private SpreadsheetAnalyticsItemKey _chartItem;
    private SpreadsheetAnalyticsItemKey _pivotItem;
    private int _frameCount;
    private int _analyticsInserted;
    private int _finished;
    private bool _disposed;

    public SmokePage()
    {
        Title = "Nera Android analytics accessibility smoke";
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
        _editorHost?.Dispose();
        if (_editorHost is not null) _host.Children.Remove(_editorHost);
        _editorHost = null;
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
        _editorHost = new NeraSpreadsheetEditorHost(_view);
        _host.Children.Add(_editorHost);
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
        Require(view.Handler?.PlatformView is AndroidView,
            "The Android analytics smoke did not receive a native Android View.");
        Require(view.GRContext is not null,
            "The Android analytics smoke did not receive a live Skia GRContext.");
        Require(view.Session is not null,
            "The Android analytics smoke workbook did not create a spreadsheet session.");
        Require(view.GpuContextDiagnostics.FramesFailed == 0L,
            "The Android analytics smoke observed a failed GPU frame.");
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
                        "The Android analytics smoke lost its session before analytics creation.");
                Table007EditorSmoke.Run(_editorHost!);
                _editorVerified = true;
                var sourceRange = new CellRange(
                    new CellAddress(0, 0),
                    new CellAddress(3, 1));
                var chart = session.Analytics.InsertChart(
                    sourceRange,
                    SpreadsheetChartType.Column,
                    title: "Android accessibility",
                    requestedName: "AndroidAccessibilityChart");
                var pivot = session.Analytics.InsertPivot(
                    sourceRange,
                    rowFieldColumnIndex: 0,
                    valueFieldColumnIndex: 1,
                    aggregation: SpreadsheetPivotAggregation.Sum,
                    requestedName: "AndroidAccessibilityPivot");
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
        var host = view.Handler?.PlatformView as AndroidView
            ?? throw new InvalidOperationException(
                "The Android analytics smoke lost its native Android View.");
        var projectedNodes = view.AnalyticsAccessibilityNodes;
        Require(projectedNodes.Count == 2,
            $"Expected two projected analytics nodes but found {projectedNodes.Count}.");
        Require(projectedNodes.Any(node =>
                node.Item == _chartItem &&
                node.Name == "AndroidAccessibilityChart" &&
                node.Role == SpreadsheetAnalyticsAccessibleRole.Chart),
            "The Android accessibility projection omitted the inserted chart.");
        Require(projectedNodes.Any(node =>
                node.Item == _pivotItem &&
                node.Name == "AndroidAccessibilityPivot" &&
                node.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable),
            "The Android accessibility projection omitted the inserted pivot.");

        var accessibilityDelegate = ViewCompat.GetAccessibilityDelegate(host)
            ?? throw new InvalidOperationException(
                "The Android spreadsheet did not expose an accessibility delegate.");
        var provider = accessibilityDelegate.GetAccessibilityNodeProvider(host)
            ?? throw new InvalidOperationException(
                "The Android spreadsheet accessibility delegate did not expose a node provider.");

        using var root = provider.CreateAccessibilityNodeInfo(RootVirtualViewId)
            ?? throw new InvalidOperationException(
                "The Android accessibility provider did not expose a root node.");
        Require(root.ChildCount >= 2,
            "The Android accessibility root did not expose both analytics virtual children.");

        using var first = provider.CreateAccessibilityNodeInfo(FirstAnalyticsVirtualViewId)
            ?? throw new InvalidOperationException(
                "The Android accessibility provider did not expose analytics virtual child 1.");
        using var second = provider.CreateAccessibilityNodeInfo(SecondAnalyticsVirtualViewId)
            ?? throw new InvalidOperationException(
                "The Android accessibility provider did not expose analytics virtual child 2.");

        var firstDescription = first.ContentDescription?.ToString() ?? string.Empty;
        var secondDescription = second.ContentDescription?.ToString() ?? string.Empty;
        var chartVirtualId = ResolveVirtualId(
            firstDescription,
            secondDescription,
            "AndroidAccessibilityChart");
        var pivotVirtualId = ResolveVirtualId(
            firstDescription,
            secondDescription,
            "AndroidAccessibilityPivot");
        Require(chartVirtualId != pivotVirtualId,
            "The Android chart and pivot resolved to the same virtual accessibility id.");

        var chartNode = chartVirtualId == FirstAnalyticsVirtualViewId ? first : second;
        var pivotNode = pivotVirtualId == FirstAnalyticsVirtualViewId ? first : second;
        ValidateAndroidNode(
            chartNode,
            "AndroidAccessibilityChart",
            "Biểu đồ",
            "chart");
        ValidateAndroidNode(
            pivotNode,
            "AndroidAccessibilityPivot",
            "Bảng tổng hợp",
            "pivot");

        var chartBounds = ReadBounds(chartNode, "chart");
        var pivotBounds = ReadBounds(pivotNode, "pivot");

        FocusClickAndVerify(
            provider,
            view,
            chartVirtualId,
            _chartItem,
            "chart");
        FocusClickAndVerify(
            provider,
            view,
            pivotVirtualId,
            _pivotItem,
            "pivot");

        Complete(new
        {
            status = "success",
            table007Editor = _editorVerified,
            table007NativeKeys = "Android EditText DispatchKeyEvent Enter/AltEnter/Escape",
            frameCount = _frameCount,
            virtualChildCount = root.ChildCount,
            chart = new
            {
                virtualId = chartVirtualId,
                contentDescription = chartNode.ContentDescription?.ToString(),
                bounds = DescribeBounds(chartBounds),
                accessibilityFocusVerified = true,
                clickSelectionVerified = true,
            },
            pivot = new
            {
                virtualId = pivotVirtualId,
                contentDescription = pivotNode.ContentDescription?.ToString(),
                bounds = DescribeBounds(pivotBounds),
                accessibilityFocusVerified = true,
                clickSelectionVerified = true,
            },
            selectedItem = view.Session?.AnalyticsInteraction.SelectedItem?.ToString(),
            cachedTypefaces = view.CachedTypefaceCount,
        });
    }

    private static int ResolveVirtualId(
        string firstDescription,
        string secondDescription,
        string expectedName)
    {
        var firstMatches = firstDescription.Contains(expectedName, StringComparison.Ordinal);
        var secondMatches = secondDescription.Contains(expectedName, StringComparison.Ordinal);
        Require(firstMatches ^ secondMatches,
            $"Expected exactly one Android virtual accessibility child for '{expectedName}'.");
        return firstMatches
            ? FirstAnalyticsVirtualViewId
            : SecondAnalyticsVirtualViewId;
    }

    private static void ValidateAndroidNode(
        AccessibilityNodeInfoCompat node,
        string expectedName,
        string expectedRole,
        string itemKind)
    {
        var description = node.ContentDescription?.ToString() ?? string.Empty;
        Require(description.Contains(expectedName, StringComparison.Ordinal),
            $"The Android virtual {itemKind} description omitted its analytics name.");
        Require(description.Contains(expectedRole, StringComparison.Ordinal),
            $"The Android virtual {itemKind} description omitted its localized role.");
        Require(node.Clickable,
            $"The Android virtual {itemKind} was not exposed as clickable.");
        Require(node.VisibleToUser,
            $"The Android virtual {itemKind} was not exposed as visible to TalkBack.");
        Require(!node.Selected,
            $"The Android virtual {itemKind} was selected before ACTION_CLICK.");
    }

    private static AndroidRect ReadBounds(
        AccessibilityNodeInfoCompat node,
        string itemKind)
    {
        var bounds = new AndroidRect();
        node.GetBoundsInScreen(bounds);
        Require(!bounds.IsEmpty,
            $"The Android virtual {itemKind} exposed empty screen bounds.");
        Require(bounds.Width() > 0 && bounds.Height() > 0,
            $"The Android virtual {itemKind} exposed non-positive dimensions.");
        return bounds;
    }

    private static object DescribeBounds(AndroidRect bounds) => new
    {
        left = bounds.Left,
        top = bounds.Top,
        right = bounds.Right,
        bottom = bounds.Bottom,
    };

    private static void FocusClickAndVerify(
        AccessibilityNodeProviderCompat provider,
        NeraSpreadsheetView view,
        int virtualId,
        SpreadsheetAnalyticsItemKey expectedItem,
        string itemKind)
    {
        Require(provider.PerformAction(
                virtualId,
                ActionAccessibilityFocusId,
                null),
            $"The Android virtual {itemKind} rejected ACTION_ACCESSIBILITY_FOCUS.");
        using var focused = provider.CreateAccessibilityNodeInfo(virtualId)
            ?? throw new InvalidOperationException(
                $"The Android accessibility provider lost the focused {itemKind} node.");
        Require(focused.AccessibilityFocused,
            $"The Android virtual {itemKind} did not retain accessibility focus.");

        Require(provider.PerformAction(
                virtualId,
                ActionClickId,
                null),
            $"The Android virtual {itemKind} rejected ACTION_CLICK.");
        Require(
            view.Session?.AnalyticsInteraction.SelectedItem == expectedItem,
            $"ACTION_CLICK on the Android virtual {itemKind} did not select the analytics item.");

        Require(provider.PerformAction(
                virtualId,
                ActionClearAccessibilityFocusId,
                null),
            $"The Android virtual {itemKind} rejected ACTION_CLEAR_ACCESSIBILITY_FOCUS.");
    }

    private void Complete(object result)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        AndroidLog.Info(LogTag, JsonSerializer.Serialize(result));
    }

    private void Fail(Exception exception)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        AndroidLog.Error(
            LogTag,
            JsonSerializer.Serialize(new
            {
                status = "failure",
                frameCount = _frameCount,
                analyticsInserted = Volatile.Read(ref _analyticsInserted),
                accessibilityNodeCount = _view?.AnalyticsAccessibilityNodes.Count,
                error = exception.ToString(),
            }));
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
                    $"The Android analytics accessibility smoke did not complete within {SmokeTimeout}."));
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
