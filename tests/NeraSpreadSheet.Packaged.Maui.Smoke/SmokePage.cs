using Microsoft.Maui;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Maui;
using NeraSpreadSheet.Ribbon.Core;
using SkiaSharp.Views.Maui;

namespace Packaged.Maui.Smoke;

internal sealed class SmokePage : ContentPage, IDisposable
{
    private readonly NeraSpreadsheetTableHost _host;
    private readonly NeraMauiRibbonView _ribbon;
    private readonly NeraMauiTableDesignRibbonBinding _binding;
    private readonly Grid _root = new() { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
    private readonly Guid _tableId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private readonly Guid _columnId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private int _frames;
    private bool _disposed;

    public SmokePage()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "Code");
        for (var row = 1; row <= 20; row++) sheet.SetValue(new CellAddress(row, 0), $"Item {row:00}");
        sheet.AddTable(new SpreadsheetTable(_tableId, "Items", new CellRange(default, new CellAddress(20, 0)),
            [new SpreadsheetTableColumn(_columnId, "Code")]));
        _host = new NeraSpreadsheetTableHost { Workbook = workbook };
        var session = _host.Session ?? throw new InvalidOperationException("Package host did not create its session.");
        var definition = RibbonProductionCommandCatalog.CreateDefaultDefinition();
        RibbonCommandCatalogAudit.ValidateExact(session.Commands, definition, RibbonProductionCommandCatalog.CommandIds);
        var runtime = new RibbonRuntimeController(definition, session.Commands);
        _ribbon = new NeraMauiRibbonView(runtime);
        _binding = new NeraMauiTableDesignRibbonBinding(session, runtime, Dispatcher);
        _root.Add(_ribbon, 0, 0);
        _root.Add(_host, 0, 1);
        Content = _root;
        _host.Spreadsheet.PaintSurface += OnFrame;
        Loaded += OnLoaded;
    }

    // The public event follows the SDK GPU lease completion; keep the cross-thread count atomic.
    private void OnFrame(object? sender, SKPaintGLSurfaceEventArgs e) => Interlocked.Increment(ref _frames);

    private void OnLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnLoaded;
        // Always leave the native Loaded/paint stack before any workbook mutation.
        Dispatcher.Dispatch(() => _ = RunAsync());
    }

    private async Task RunAsync()
    {
        var stage = "native-frames";
        try
        {
            var view = _host.Spreadsheet;
            await UntilAsync(() => Volatile.Read(ref _frames) >= 3 && view.Width > 0 && view.Height > 0,
                view.InvalidateSurface);
            PackageProvenance.Require(view.Handler?.PlatformView is not null && view.GRContext is not null &&
                _ribbon.Handler?.PlatformView is not null, "Public package hosts did not attach native handlers.");
            var session = _host.Session!;
            stage = "controller-edit-undo-cancel";
            var address = new CellAddress(1, 2);
            var before = session.ActiveWorksheet.GetCell(address);
            var history = session.History.UndoCount;
            session.Editor.BeginEdit(address);
            PackageProvenance.Require(session.Editor.Commit("=1+1") && session.ActiveWorksheet.GetCell(address).Value == CellValue.FromObject(2d),
                "Packaged editor/formula operation failed.");
            PackageProvenance.Require(session.Undo() && session.ActiveWorksheet.GetCell(address) == before &&
                session.History.UndoCount == history, "Undo did not restore the entire cell/history.");
            // This verifies the public controller contract only, not native draft teardown.
            session.Editor.BeginEdit(address);
            PackageProvenance.Require(session.Editor.Cancel() && !session.Editor.IsEditing &&
                session.ActiveWorksheet.GetCell(address) == before && session.History.UndoCount == history,
                "Controller Cancel mutated workbook state.");
            stage = "native-filter-values";
            PackageProvenance.Require(_host.TryOpenFilter(_tableId, _columnId), "Packaged filter did not open.");
            await UntilAsync(() => Descendants(_host).OfType<CheckBox>().Count(box =>
                box.AutomationId?.StartsWith("NeraTableFilterValue_", StringComparison.Ordinal) == true &&
                box.Handler?.PlatformView is not null && box.IsChecked) == 20);
            PackageProvenance.Require(_host.IsFilterSheetOpen, "Filter closed before loading its actual values.");
            _host.CloseFilterSheet();
            PackageProvenance.Require(!_host.IsFilterSheetOpen, "Filter close did not complete.");
            var width = view.Width;
            stage = "actual-resize";
            var framesBeforeResize = view.GpuContextDiagnostics.FramesCompleted;
            _host.WidthRequest = width * 0.65d;
            _host.HorizontalOptions = LayoutOptions.Start;
            await UntilAsync(() => view.Width < width * 0.8d &&
                view.GpuContextDiagnostics.FramesCompleted > framesBeforeResize, view.InvalidateSurface);
            view.HasRenderLoop = false;
            stage = "idle-gpu";
            var gpu = await WaitForIdleGpuAsync(view);
            PackageProvenance.Require(gpu.FramesCompleted >= 3 && gpu.FramesFailed == 0 && !gpu.HasActiveFrame,
                "Package GPU lifecycle did not finish healthy frames.");
            stage = "assembly-provenance";
            var assemblies = PackageProvenance.VerifyLoadedAssemblies();
            stage = "dispose";
            Dispose();
            PackageProvenance.Emit("success", Volatile.Read(ref _frames), new { assemblies, gpu, controllerEditUndo = true,
                filterValues = 20, actualResize = true, publicApiOnly = true });
            Environment.Exit(0);
        }
        catch (Exception exception)
        {
            // Never serialize exception messages/stack traces containing runner paths.
            PackageProvenance.Emit("failure", Volatile.Read(ref _frames), new { stage, exceptionType = exception.GetType().FullName });
            Environment.Exit(1);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Loaded -= OnLoaded;
        _host.Spreadsheet.PaintSurface -= OnFrame;
        _host.Spreadsheet.HasRenderLoop = false;
        _binding.Dispose();
        _ribbon.Dispose();
        _host.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task<NeraGpuContextDiagnostics> WaitForIdleGpuAsync(NeraSpreadsheetView view)
    {
        var previous = view.GpuContextDiagnostics;
        var stable = previous;
        await UntilAsync(() =>
        {
            stable = view.GpuContextDiagnostics;
            var ready = stable == previous && stable.HasActiveContext && !stable.HasActiveFrame &&
                stable.FramesCompleted >= 3 && stable.FramesFailed == 0;
            previous = stable;
            return ready;
        }, requireInitialDelay: true);
        return stable;
    }

    private static async Task UntilAsync(Func<bool> ready, Action? requestFrame = null, bool requireInitialDelay = false)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        if (requireInitialDelay) await Task.Delay(30);
        while (!ready())
        {
            if (DateTime.UtcNow >= deadline) throw new TimeoutException("Package consumer condition timed out.");
            // Nera disables its continuous loop when scrolling settles; request frames on the UI dispatcher.
            requestFrame?.Invoke();
            await Task.Delay(30);
        }
    }

    private static IEnumerable<IVisualTreeElement> Descendants(IVisualTreeElement root)
    {
        yield return root;
        foreach (var child in root.GetVisualChildren())
            foreach (var descendant in Descendants(child)) yield return descendant;
    }
}
