using System.Diagnostics;
using System.Runtime.CompilerServices;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Ribbon.Core;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Benchmarks;

internal static class PERF008Stress
{
    internal static Task<object> VerifyAsync(SpreadsheetSession session, SpreadsheetAutoFilterTarget target)
    {
        var source = SpreadsheetAutoFilterPagedSessionFactory.Create(session, target);
        using var view = new SpreadsheetAutoFilterPagedView(source, 100);
        var refreshes = 0;
        source.Refreshed += (_, _) => refreshes++;
        view.InitializeAsync().GetAwaiter().GetResult();
        for (var page = 0; page < 20; page++) view.GetPageAsync(page * 100).GetAwaiter().GetResult();
        PERF008Harness.Require(view.Capture().LoadedItemCount == 2_000, "Only requested pages should be cached.");
        view.SetSearchTextAsync("0001").GetAwaiter().GetResult();
        PERF008Harness.Require(view.Capture().LoadedItemCount <= 100, "Search must discard old pages.");
        view.SetSearchTextAsync(string.Empty).GetAwaiter().GetResult();
        var first = view.GetPageAsync(0).GetAwaiter().GetResult();
        var generation = view.Capture().Generation;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = false;
        try { view.SetSearchTextAsync("cancelled", cancellation.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { cancelled = true; }
        PERF008Harness.Require(cancelled && view.Capture().SearchText.Length == 0, "Cancellation published stale search.");

        // An intentionally stale cached formula acts as a recalculation tripwire.
        // Any full recalc would change 12345 to 2, even when all other values are stable.
        var sentinel = new CellAddress(0, 20);
        session.ActiveWorksheet.SetValue(sentinel, 12345d);
        session.ActiveWorksheet.SetFormula(sentinel, "=1+1");
        var sheetVersion = session.ActiveWorksheet.Version;
        var runtime = new RibbonRuntimeController(RibbonProductionCommandCatalog.CreateDefaultDefinition(), session.Commands);
        var ribbonChanges = 0;
        runtime.SnapshotChanged += (_, _) => ribbonChanges++;
        var viewport = new SpreadsheetViewportEngine(session);
        viewport.Compose(0, 0, 800, 500, 0);
        var snapshotRefreshes = viewport.SnapshotRefreshCount;
        var refreshesBefore = refreshes;
        for (var frame = 0; frame < 200; frame++)
        {
            var output = viewport.Compose(0, frame * 13.25, 800, 500, 0);
            PERF008Harness.Require(output.Layout.ScrollY == frame * 13.25, "Precision scroll was snapped.");
        }
        PERF008Harness.Require(viewport.SnapshotRefreshCount == snapshotRefreshes && ribbonChanges == 0 && refreshes == refreshesBefore,
            "Pure viewport composition refreshed workbook/Ribbon/filter state.");
        PERF008Harness.Require(session.ActiveWorksheet.Version == sheetVersion &&
            session.ActiveWorksheet.GetCell(sentinel).Value == CellValue.FromObject(12345d), "Scrolling recalculated or changed workbook cells.");
        PERF008Harness.Require(view.Capture().Generation == generation && ReferenceEquals(first, view.GetPageAsync(0).GetAwaiter().GetResult()),
            "Scrolling invalidated the filter generation/cache.");

        var weak = new List<WeakReference>();
        var memory = new List<object>();
        for (var batch = 0; batch < 5; batch++)
        {
            for (var cycle = 0; cycle < 8; cycle++) weak.Add(OpenSearchCancelDispose(session, target));
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            using var process = Process.GetCurrentProcess();
            memory.Add(new { cycles = (batch + 1) * 8, managedBytes = GC.GetTotalMemory(false),
                workingSetBytes = process.WorkingSet64, privateBytes = process.PrivateMemorySize64,
                survivingViews = weak.Count(reference => reference.IsAlive) });
        }
        PERF008Harness.Require(weak.All(reference => !reference.IsAlive), "Disposed paged views are still retained.");
        return Task.FromResult<object>(new
        {
            cycles = 40, requestedPages = 20, cachedItemsAtPeak = 2_000, currentPageSize = 100,
            catalogCap = 10_000, constantCacheBound = false, memory,
            scrollFrames = 200, snapshotRefreshDelta = viewport.SnapshotRefreshCount - snapshotRefreshes,
            ribbonRefreshDelta = ribbonChanges, filterRefreshDelta = refreshes - refreshesBefore,
            formulaSentinelUnchanged = true,
            scope = "headless viewport composition; native host subscriptions checked separately; no GPU/present/frame-time claim",
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OpenSearchCancelDispose(SpreadsheetSession session, SpreadsheetAutoFilterTarget target)
    {
        using var view = new SpreadsheetAutoFilterPagedView(SpreadsheetAutoFilterPagedSessionFactory.Create(session, target), 100);
        view.InitializeAsync().GetAwaiter().GetResult();
        view.SetSearchTextAsync("0001").GetAwaiter().GetResult();
        view.GetPageAsync(0).GetAwaiter().GetResult();
        return new WeakReference(view);
    }
}
