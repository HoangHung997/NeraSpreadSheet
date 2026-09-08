using System.Text.Json;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Foundation;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
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
    private const string DefaultResultFileName = "nera-maccatalyst-analytics-smoke.json";
    private static readonly TimeSpan SmokeTimeout = TimeSpan.FromSeconds(45d);
    private static readonly TimeSpan DiagnosticPollInterval = TimeSpan.FromMilliseconds(250d);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };
    private static readonly NSString AccessibilityElementsKey =
        new("accessibilityElements");

    private readonly Grid _host = new();
    private readonly Workbook _workbook = CreateWorkbook();
    private NeraSpreadsheetView? _view;
    private NeraSpreadsheetEditorHost? _editorHost;
    private bool _editorVerified;
    private SpreadsheetAnalyticsItemKey _chartItem;
    private SpreadsheetAnalyticsItemKey _pivotItem;
    private int _frameCount;
    private int _analyticsInserted;
    private int _nativeValidationStarted;
    private int _finished;
    private int _paintCallbackDepth;
    private bool _pageLoadedObserved;
    private int _orchestrationBoundaryChecks;
    private int _initialReadyFrame;
    private int _editorHostReadyFrame;
    private int _analyticsReadyFrame;
    private TaskCompletionSource? _frameReadiness;
    private Func<bool>? _frameReadinessCondition;
    private bool _disposed;

    public SmokePage()
    {
        CaptureNativeStandardError();
        SmokeTrace.Append("smoke-page-constructor-enter");
        Title = "Nera Mac Catalyst analytics accessibility smoke";
        Content = _host;

        try
        {
            SmokeTrace.Append("smoke-page-constructor-before-view-constructor");
            var view = new NeraSpreadsheetView();
            SmokeTrace.Append("smoke-page-constructor-after-view-constructor");
            _view = view;

            SmokeTrace.Append("smoke-page-constructor-before-workbook-assign");
            view.Workbook = _workbook;
            SmokeTrace.Append("smoke-page-constructor-after-workbook-assign");

            view.HorizontalOptions = LayoutOptions.Fill;
            view.VerticalOptions = LayoutOptions.Fill;
            SmokeTrace.Append("smoke-page-constructor-layout-options-set");

            view.PaintSurface += OnPaintSurface;
            view.Loaded += OnViewLoaded;
            SmokeTrace.Append("smoke-page-constructor-events-subscribed");

            SmokeTrace.Append("smoke-page-constructor-before-host-add");
            _host.Children.Add(view);
            SmokeTrace.Append("smoke-page-constructor-after-host-add");
        }
        catch (Exception exception)
        {
            SmokeTrace.Append($"smoke-page-constructor-catch:{exception.GetType().FullName}");
            throw new InvalidOperationException(
                "The Mac Catalyst smoke failed while constructing the Nera spreadsheet content before native window attachment.",
                exception);
        }

        Loaded += OnLoaded;
        SmokeTrace.Append("smoke-page-constructor-success");
    }

    private static void CaptureNativeStandardError()
    {
        var runKey = Environment.GetEnvironmentVariable("NERA_MAUI_NATIVE_STDERR_RUN");
        if (string.IsNullOrEmpty(runKey)) return;
        Require(Guid.TryParseExact(runKey, "D", out _), "The native diagnostic run key is invalid.");
        var resultPath = Environment.GetEnvironmentVariable("NERA_MAUI_SMOKE_RESULT");
        Require(!string.IsNullOrEmpty(resultPath), "The native diagnostic result directory is missing.");
        var directory = Path.GetDirectoryName(resultPath!)
            ?? throw new InvalidOperationException("The native diagnostic result directory is invalid.");
        var fileName = $"nera-native-stderr-{runKey}-{Environment.ProcessId}.log";
        using var output = new FileStream(Path.Combine(directory, fileName), new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.ReadWrite | FileShare.Delete,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        });
        output.Write(System.Text.Encoding.ASCII.GetBytes($"NERA_NATIVE_STDERR_V1:{Environment.ProcessId}:{runKey}\n"));
        output.Flush();
        Require(DuplicateDescriptor(output.SafeFileHandle.DangerousGetHandle().ToInt32(), 2) == 2,
            "The native diagnostic stderr descriptor could not be redirected.");
        SmokeTrace.Append("native-stderr-capture-installed");
    }

    [DllImport("/usr/lib/libSystem.dylib", EntryPoint = "dup2", SetLastError = true)]
    private static extern int DuplicateDescriptor(int source, int destination);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _frameReadiness?.TrySetCanceled();
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
        SmokeTrace.Append("smoke-page-loaded-enter");
        Loaded -= OnLoaded;
        _pageLoadedObserved = true;
        _ = MonitorRuntimeAsync();
        SmokeTrace.Append("smoke-page-monitor-started");

        var view = _view;
        if (view is null)
        {
            Fail(new InvalidOperationException(
                "The Mac Catalyst smoke reached Loaded without its Nera spreadsheet view."));
            return;
        }

        SmokeTrace.Append("smoke-page-loaded-before-invalidate");
        view.InvalidateSurface();
        SmokeTrace.Append("smoke-page-loaded-after-invalidate");
        SmokeTrace.Append("smoke-page-orchestration-queued");
        bool dispatched;
        try
        {
            var dispatcher = Dispatcher;
            SmokeTrace.Append("smoke-page-orchestration-dispatcher-resolved");
            Action callback = () =>
            {
                SmokeTrace.Append("smoke-page-orchestration-enter");
                try
                {
                    InvokeLoadedSmoke(view);
                    SmokeTrace.Append("smoke-page-orchestration-callback-returned");
                }
                catch (Exception exception)
                {
                    SmokeTrace.Append($"smoke-page-orchestration-callback-catch:{ClassifyDispatchException(exception)}");
                    throw;
                }
            };
            SmokeTrace.Append("smoke-page-orchestration-dispatch-call-enter");
            dispatched = dispatcher.Dispatch(callback);
            SmokeTrace.Append(dispatched
                ? "smoke-page-orchestration-dispatch-returned-true"
                : "smoke-page-orchestration-dispatch-returned-false");
        }
        catch (Exception exception)
        {
            SmokeTrace.Append($"smoke-page-orchestration-dispatch-catch:{ClassifyDispatchException(exception)}");
            throw;
        }
        if (!dispatched)
        {
            Fail(new InvalidOperationException("The loaded Mac smoke orchestration could not be dispatched."));
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private void InvokeLoadedSmoke(NeraSpreadsheetView view)
    {
        SmokeTrace.Append("smoke-page-orchestration-invoke-enter");
        _ = RunLoadedSmokeAsync(view);
        SmokeTrace.Append("smoke-page-orchestration-invoke-returned");
    }

    private static string ClassifyDispatchException(Exception exception) =>
        exception.GetType().FullName switch
        {
            "System.InvalidOperationException" => "invalid-operation",
            "System.ObjectDisposedException" => "object-disposed",
            "System.NullReferenceException" => "null-reference",
            "ObjCRuntime.ObjCException" => "objective-c",
            _ => "other",
        };

    private static void OnViewLoaded(object? sender, EventArgs e)
    {
        SmokeTrace.Append("nera-view-loaded");
        if (sender is NeraSpreadsheetView view)
        {
            view.InvalidateSurface();
            SmokeTrace.Append("nera-view-invalidated-from-loaded");
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

        Interlocked.Increment(ref _paintCallbackDepth);
        try
        {
            Interlocked.Increment(ref _frameCount);
            if (_frameCount == 1)
            {
                SmokeTrace.Append("nera-first-paint-surface");
            }
            ValidateLoadedHost(view);

            // Completion never resumes orchestration inline on this paint stack.
            if (_frameReadinessCondition?.Invoke() == true &&
                _frameReadiness?.TrySetResult() == true)
            {
                SmokeTrace.Append("smoke-completed-frame-observed");
            }
        }
        catch (Exception exception)
        {
            SmokeTrace.Append($"nera-paint-catch:{exception.GetType().FullName}");
            Fail(exception);
        }
        finally
        {
            Interlocked.Decrement(ref _paintCallbackDepth);
        }
    }

    private async Task RunLoadedSmokeAsync(NeraSpreadsheetView view)
    {
        try
        {
            if (_disposed || Volatile.Read(ref _finished) != 0) return;
            RequireOrchestrationBoundary();
            SmokeTrace.Append("smoke-initial-frame-wait");
            await WaitForCompletedFrameAsync(view);
            RequireOrchestrationBoundary();
            _initialReadyFrame = Volatile.Read(ref _frameCount);
            SmokeTrace.Append("smoke-initial-frame-ready");

            await CreateAnalyticsForNextFrameAsync(view);
            if (_disposed || Volatile.Read(ref _finished) != 0) return;
            SmokeTrace.Append("smoke-analytics-frame-wait");
            await WaitForCompletedFrameAsync(view, additionalReady: () =>
                Volatile.Read(ref _analyticsInserted) == 1 && view.AnalyticsAccessibilityNodes.Count == 2);
            RequireOrchestrationBoundary();
            _analyticsReadyFrame = Volatile.Read(ref _frameCount);
            SmokeTrace.Append("smoke-analytics-frame-ready");
            Require(_editorVerified, "The true native editor phase did not complete before analytics validation.");
            Require(Interlocked.CompareExchange(ref _nativeValidationStarted, 1, 0) == 0,
                "Native analytics validation was started more than once.");
            SmokeTrace.Append("native-validation-start");
            ValidateNativeAccessibility(view);
        }
        catch (Exception exception)
        {
            SmokeTrace.Append($"smoke-orchestration-catch:{exception.GetType().FullName}");
            Fail(exception);
        }
    }

    private void RequireOrchestrationBoundary()
    {
        Require(!_disposed && Volatile.Read(ref _finished) == 0,
            "The Mac smoke orchestration has already ended.");
        Require(_pageLoadedObserved, "The Mac smoke orchestration started before page Loaded.");
        Require(Microsoft.Maui.ApplicationModel.MainThread.IsMainThread,
            "The Mac smoke orchestration did not resume on the UI thread.");
        Require(Volatile.Read(ref _paintCallbackDepth) == 0,
            "The Mac smoke orchestration resumed inside a PaintSurface callback.");
        Interlocked.Increment(ref _orchestrationBoundaryChecks);
    }

    private async Task WaitForCompletedFrameAsync(NeraSpreadsheetView view,
        VisualElement? layoutHost = null, Func<bool>? additionalReady = null)
    {
        RequireOrchestrationBoundary();
        Require(_frameReadiness is null, "Only one native frame readiness wait may be active.");
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var minimumFrame = Volatile.Read(ref _frameCount) + 1;
        var frameQueued = false;
        _frameReadiness = completion;
        _frameReadinessCondition = () =>
        {
            var gpu = view.GpuContextDiagnostics;
            return Volatile.Read(ref _frameCount) >= minimumFrame &&
                view.IsLoaded && view.Width > 0d && view.Height > 0d &&
                gpu.HasActiveContext && !gpu.HasActiveFrame && gpu.FramesCompleted > 0 &&
                (layoutHost is null || (layoutHost.IsLoaded && layoutHost.Width > 0d && layoutHost.Height > 0d)) &&
                (additionalReady?.Invoke() ?? true);
        };

        void QueueFrame(object? sender, EventArgs e)
        {
            if (frameQueued || completion.Task.IsCompleted) return;
            frameQueued = true;
            if (!view.Dispatcher.Dispatch(() =>
            {
                frameQueued = false;
                if (completion.Task.IsCompleted) return;
                if (_disposed || Volatile.Read(ref _finished) != 0)
                {
                    completion.TrySetCanceled();
                    return;
                }
                try { view.InvalidateSurface(); }
                catch (Exception exception) { completion.TrySetException(exception); }
            }))
            {
                completion.TrySetException(new InvalidOperationException("Native frame readiness could not be dispatched."));
            }
        }

        // Layout/Loaded events request coalesced frames. Paint only observes them;
        // the existing overall smoke timeout still owns the failure deadline.
        try
        {
            view.Loaded += QueueFrame;
            view.SizeChanged += QueueFrame;
            if (layoutHost is not null)
            {
                layoutHost.Loaded += QueueFrame;
                layoutHost.SizeChanged += QueueFrame;
            }
            QueueFrame(null, EventArgs.Empty);
            await completion.Task;
        }
        finally
        {
            void DetachReadiness()
            {
                view.Loaded -= QueueFrame;
                view.SizeChanged -= QueueFrame;
                if (layoutHost is not null)
                {
                    layoutHost.Loaded -= QueueFrame;
                    layoutHost.SizeChanged -= QueueFrame;
                }
                _frameReadiness = null;
                _frameReadinessCondition = null;
            }

            if (Microsoft.Maui.ApplicationModel.MainThread.IsMainThread) DetachReadiness();
            else await view.Dispatcher.DispatchAsync(DetachReadiness);
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

    private async Task CreateAnalyticsForNextFrameAsync(NeraSpreadsheetView view)
    {
        if (Interlocked.CompareExchange(ref _analyticsInserted, -1, 0) != 0)
        {
            return;
        }

        try
        {
            SmokeTrace.Append("analytics-create-enter");
            var session = view.Session
                ?? throw new InvalidOperationException(
                    "The Mac Catalyst analytics smoke lost its session before analytics creation.");
            // Called only by loaded orchestration after an observed completed frame.
            Task? editorPhase = null;
            await view.Dispatcher.DispatchAsync(() =>
            {
                SmokeTrace.Append("table-editor-dispatch-action-enter");
                editorPhase = RunEditorPhaseAsync(view);
            });
            Require(editorPhase is not null, "The dispatched editor phase did not start.");
            await editorPhase!;
            RequireOrchestrationBoundary();
            _editorVerified = true;
            var sourceRange = new CellRange(
                new CellAddress(0, 0),
                new CellAddress(3, 1));

            SmokeTrace.Append("analytics-before-chart-insert");
            var chart = session.Analytics.InsertChart(
                sourceRange,
                SpreadsheetChartType.Column,
                title: "Mac Catalyst accessibility",
                requestedName: "MacAccessibilityChart");
            SmokeTrace.Append("analytics-after-chart-insert");

            SmokeTrace.Append("analytics-before-pivot-insert");
            var pivot = session.Analytics.InsertPivot(
                sourceRange,
                rowFieldColumnIndex: 0,
                valueFieldColumnIndex: 1,
                aggregation: SpreadsheetPivotAggregation.Sum,
                requestedName: "MacAccessibilityPivot");
            SmokeTrace.Append("analytics-after-pivot-insert");

            _chartItem = SpreadsheetAnalyticsItemKey.ForChart(chart.Id);
            _pivotItem = SpreadsheetAnalyticsItemKey.ForPivot(pivot.Id);
            Volatile.Write(ref _analyticsInserted, 1);
            SmokeTrace.Append("analytics-create-ready-for-next-frame");
            view.InvalidateSurface();
            SmokeTrace.Append("analytics-validation-frame-requested");
        }
        catch (Exception exception)
        {
            Volatile.Write(ref _analyticsInserted, 1);
            SmokeTrace.Append($"analytics-create-catch:{exception.GetType().FullName}");
            Fail(exception);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private async Task RunEditorPhaseAsync(NeraSpreadsheetView view)
    {
        RequireOrchestrationBoundary();
        SmokeTrace.Append("table-editor-host-attach-enter");
        _host.Children.Remove(view);
        SmokeTrace.Append("table-editor-bare-view-removed");
        var editorHost = new NeraSpreadsheetEditorHost(view);
        _editorHost = editorHost;
        SmokeTrace.Append("table-editor-host-created");
        _host.Children.Add(editorHost);
        SmokeTrace.Append("table-editor-host-attach-returned");
        await WaitForCompletedFrameAsync(view, editorHost);
        RequireOrchestrationBoundary();
        _editorHostReadyFrame = Volatile.Read(ref _frameCount);
        SmokeTrace.Append("table-editor-host-frame-ready");
        await Table007EditorSmoke.RunAsync(editorHost);
    }

    private void ValidateNativeAccessibility(NeraSpreadsheetView view)
    {
        var host = view.Handler?.PlatformView as UIView
            ?? throw new InvalidOperationException(
                "The Mac Catalyst analytics smoke lost its native UIView.");
        Require(!host.IsAccessibilityElement,
            "The GPU host should be an accessibility container, not one monolithic element.");
        SmokeTrace.Append("native-validation-host-ready");

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
        SmokeTrace.Append("native-validation-projection-verified");

        var nativeElements = GetNativeAccessibilityElements(host)
            .ToArray<UIAccessibilityElement>();
        SmokeTrace.Append($"native-validation-elements-read:{nativeElements.Length}");
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
        SmokeTrace.Append("native-validation-metadata-verified");

        SmokeTrace.Append("native-validation-before-chart-activate");
        ActivateAndRequireSelection(view, chart, _chartItem, "chart");
        SmokeTrace.Append("native-validation-after-chart-activate");
        SmokeTrace.Append("native-validation-before-pivot-activate");
        ActivateAndRequireSelection(view, pivot, _pivotItem, "pivot");
        SmokeTrace.Append("native-validation-after-pivot-activate");

        Complete(new
        {
            status = "success",
            table007Editor = _editorVerified,
            table007NativeKeys = "UIKit InsertText Enter and marked-text guard; hardware keys pending",
            table007Readiness = DescribeReadiness(),
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

    private static NSArray GetNativeAccessibilityElements(UIView host)
    {
        return host.ValueForKey(AccessibilityElementsKey) as NSArray
            ?? throw new InvalidOperationException(
                "The Mac Catalyst accessibilityElements payload was not an NSArray.");
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
            $"The native Mac Catalyst {itemKind} value omitted its localized role.");
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

        WriteResult(result);
        Environment.Exit(0);
    }

    private object DescribeReadiness() => new
    {
        pageLoaded = _pageLoadedObserved,
        boundaryChecks = Volatile.Read(ref _orchestrationBoundaryChecks),
        initialFrame = _initialReadyFrame,
        editorHostFrame = _editorHostReadyFrame,
        analyticsFrame = _analyticsReadyFrame,
        paintDepth = Volatile.Read(ref _paintCallbackDepth),
    };

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
                analyticsInserted = Volatile.Read(ref _analyticsInserted),
                nativeValidationStarted = Volatile.Read(ref _nativeValidationStarted),
                table007Readiness = DescribeReadiness(),
                accessibilityNodeCount = _view?.AnalyticsAccessibilityNodes.Count,
                gpuDiagnostics = _view?.GpuContextDiagnostics,
                error = exception.ToString(),
            });
        }
        finally
        {
            Environment.Exit(1);
        }
    }

    private async Task MonitorRuntimeAsync()
    {
        var deadline = DateTime.UtcNow + SmokeTimeout;
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(DiagnosticPollInterval).ConfigureAwait(false);
            if (_disposed || Volatile.Read(ref _finished) != 0)
            {
                return;
            }

            var view = _view;
            if (view is null)
            {
                continue;
            }

            var renderingFailure = NeraMacCatalystGpuDiagnostics.GetLastFailure(view);
            if (renderingFailure is null)
            {
                continue;
            }

            Dispatcher.Dispatch(() =>
            {
                if (!_disposed && Volatile.Read(ref _finished) == 0)
                {
                    Fail(new InvalidOperationException(
                        "The Mac Catalyst native CAMetalLayer renderer failed inside the render callback boundary.",
                        renderingFailure));
                }
            });
            return;
        }

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
        var serialized = JsonSerializer.Serialize(result, JsonOptions);
        var primaryPath = ResolveResultPath();
        try
        {
            WriteResultFile(primaryPath, serialized);
        }
        catch when (!string.Equals(
            Path.GetFullPath(primaryPath),
            Path.GetFullPath(GetDefaultResultPath()),
            StringComparison.Ordinal))
        {
            WriteResultFile(GetDefaultResultPath(), serialized);
        }
    }

    private static void WriteResultFile(string path, string serialized)
    {
        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(
            Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException(
                "The Mac Catalyst smoke result file has no parent directory."));
        File.WriteAllText(fullPath, serialized);
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

        return GetDefaultResultPath();
    }

    private static string GetDefaultResultPath() =>
        Path.Combine(Path.GetTempPath(), DefaultResultFileName);

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
