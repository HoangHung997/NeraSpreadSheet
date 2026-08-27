using System.Text.Json;
using Android.Graphics;
using Android.Views;
using AndroidX.Core.View;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Maui;
using SkiaSharp.Views.Maui;

namespace NeraSpreadSheet.Maui.Android.AnalyticsSmoke;

internal sealed class SmokePage : ContentPage, IDisposable
{
    private const string LogTag = "NeraAndroidAnalyticsSmoke";
    private const int RootVirtualViewId = Android.Views.View.NoId;
    private const int FirstAnalyticsVirtualViewId = 1;
    private const int ActionClickId = 16;
    private const int ActionAccessibilityFocusId = 64;
    private const int ActionClearAccessibilityFocusId = 128;
    private static readonly TimeSpan SmokeTimeout = TimeSpan.FromSeconds(30d);

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
        Require(view.Handler?.PlatformView is Android.Views.View,
            "The Android analytics smoke did not receive a native Android View.");
        Require(view.GRContext is not null,
            "The Android analytics smoke did not receive a live Skia GRContext.");
        Require(view.Session is not null,
            "The Android analytics smoke workbook did not create a spreadsheet session.");
        Require(view.GpuContextDiagnostics.FramesFailed == 0L,
            "The Android analytics smoke observed a failed GPU frame.");
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
                        "The Android analytics smoke lost its session before chart creation.");
                var chart = session.Analytics.InsertChart(
                    new CellRange(
                        new CellAddress(0, 0),
                        new CellAddress(3, 1)),
                    SpreadsheetChartType.Column,
                    title: "Android accessibility",
                    requestedName: "AndroidAccessibilityChart");
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
        var host = view.Handler?.PlatformView as Android.Views.View
            ?? throw new InvalidOperationException(
                "The Android analytics smoke lost its native Android View.");
        var projectedNode = view.AnalyticsAccessibilityNodes.Single();
        Require(projectedNode.Item == _item,
            "The Android accessibility projection did not match the inserted chart.");
        Require(projectedNode.Name == "AndroidAccessibilityChart",
            "The Android accessibility projection resolved the wrong chart name.");
        Require(!projectedNode.IsSelected,
            "The Android chart was unexpectedly selected before accessibility click.");

        var accessibilityDelegate = ViewCompat.GetAccessibilityDelegate(host)
            ?? throw new InvalidOperationException(
                "The Android spreadsheet did not expose an accessibility delegate.");
        var provider = accessibilityDelegate.GetAccessibilityNodeProvider(host)
            ?? throw new InvalidOperationException(
                "The Android spreadsheet accessibility delegate did not expose a node provider.");

        using var root = provider.CreateAccessibilityNodeInfo(RootVirtualViewId)
            ?? throw new InvalidOperationException(
                "The Android accessibility provider did not expose a root node.");
        Require(root.ChildCount >= 1,
            "The Android accessibility root did not expose the analytics virtual child.");

        using var child = provider.CreateAccessibilityNodeInfo(FirstAnalyticsVirtualViewId)
            ?? throw new InvalidOperationException(
                "The Android accessibility provider did not expose virtual chart child 1.");
        var description = child.ContentDescription?.ToString() ?? string.Empty;
        Require(description.Contains("AndroidAccessibilityChart", StringComparison.Ordinal),
            "The Android virtual chart description omitted the chart name.");
        Require(description.Contains("Biểu đồ", StringComparison.Ordinal),
            "The Android virtual chart description omitted the localized chart role.");
        Require(child.Clickable,
            "The Android virtual chart was not exposed as clickable.");
        Require(child.VisibleToUser,
            "The Android virtual chart was not exposed as visible to TalkBack.");
        Require(!child.Selected,
            "The Android virtual chart was selected before ACTION_CLICK.");

        using var bounds = new Rect();
        child.GetBoundsInScreen(bounds);
        Require(!bounds.IsEmpty,
            "The Android virtual chart exposed empty screen bounds.");
        Require(bounds.Width() > 0 && bounds.Height() > 0,
            "The Android virtual chart exposed non-positive dimensions.");

        Require(provider.PerformAction(
                FirstAnalyticsVirtualViewId,
                ActionAccessibilityFocusId,
                null),
            "The Android virtual chart rejected ACTION_ACCESSIBILITY_FOCUS.");
        using var focused = provider.CreateAccessibilityNodeInfo(FirstAnalyticsVirtualViewId)
            ?? throw new InvalidOperationException(
                "The Android accessibility provider lost the focused chart node.");
        Require(focused.AccessibilityFocused,
            "The Android virtual chart did not retain accessibility focus.");

        Require(provider.PerformAction(
                FirstAnalyticsVirtualViewId,
                ActionClickId,
                null),
            "The Android virtual chart rejected ACTION_CLICK.");
        Require(
            view.Session?.AnalyticsInteraction.SelectedItem == _item,
            "ACTION_CLICK on the Android virtual chart did not select the analytics item.");

        Require(provider.PerformAction(
                FirstAnalyticsVirtualViewId,
                ActionClearAccessibilityFocusId,
                null),
            "The Android virtual chart rejected ACTION_CLEAR_ACCESSIBILITY_FOCUS.");

        Complete(new
        {
            status = "success",
            frameCount = _frameCount,
            virtualChildCount = root.ChildCount,
            virtualId = FirstAnalyticsVirtualViewId,
            contentDescription = description,
            bounds = new
            {
                left = bounds.Left,
                top = bounds.Top,
                right = bounds.Right,
                bottom = bounds.Bottom,
            },
            accessibilityFocusVerified = true,
            clickSelectionVerified = true,
            selectedItem = view.Session?.AnalyticsInteraction.SelectedItem?.ToString(),
            cachedTypefaces = view.CachedTypefaceCount,
            contextGeneration = view.GpuContextDiagnostics.Generation,
        });
    }

    private void Complete(object result)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        Android.Util.Log.Info(LogTag, JsonSerializer.Serialize(result));
    }

    private void Fail(Exception exception)
    {
        if (Interlocked.Exchange(ref _finished, 1) != 0)
        {
            return;
        }

        Android.Util.Log.Error(
            LogTag,
            JsonSerializer.Serialize(new
            {
                status = "failure",
                frameCount = _frameCount,
                chartInserted = Volatile.Read(ref _chartInserted),
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
