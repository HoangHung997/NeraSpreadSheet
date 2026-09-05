using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Benchmarks;

// This worker measures CPU batches in an otherwise idle process. The outer driver
// alternates fresh processes and freezes a baseline-only budget before candidates.
internal static class PERF008Harness
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    internal static async Task RunAsync(string[] args)
    {
        if (args.Length != 2 || (args[0] != "measure" && args[0] != "verify"))
        {
            throw new ArgumentException("Expected --perf-008 measure|verify output.json.");
        }
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi-VN");
        var measurements = new List<Measurement>();
        foreach (var width in new[] { 1536d, 1280d, 1024d, 820d })
        {
            var benchmark = new RibbonLayoutBenchmarks { Width = width };
            benchmark.Setup();
            var input = typeof(RibbonLayoutBenchmarks).GetField("_presentation", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(benchmark)!;
            var output = benchmark.PackAndCollapseSevenHundredTwentyCommands();
            measurements.Add(Measure($"ribbon.pack.{width}", 128, 32,
                () => GC.KeepAlive(benchmark.PackAndCollapseSevenHundredTwentyCommands()), input, output));
        }
        foreach (var unrelated in new[] { 0, 100_000 })
        {
            var benchmark = new TableCompatibilityBenchmarks { UnrelatedCells = unrelated };
            benchmark.Setup();
            var session = (SpreadsheetSession)typeof(TableCompatibilityBenchmarks)
                .GetField("_session", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(benchmark)!;
            benchmark.FilterButtonToggleAndUndo();
            Require(session.History.UndoCount == 0, "Table toggle/undo did not restore history.");
            var table = session.ActiveWorksheet.Tables.Single();
            var input = new { unrelated, rows = 10, columns = new[] { "Item", "Amount" }, formula = "=Sales[Am" };
            measurements.Add(Measure($"table.toggleUndo.{unrelated}", 64, 8,
                benchmark.FilterButtonToggleAndUndo, input, new { table.Name, table.Range,
                    columns = table.Columns.Select(column => column.Name).ToArray(), session.History.UndoCount }));
            Require(session.History.UndoCount == 0, "Measured Table toggle/undo did not restore history.");
            var suggestions = benchmark.ColumnCompletion();
            Require(suggestions > 0, "The Table completion fixture no longer returns suggestions.");
            measurements.Add(Measure($"table.completion.{unrelated}", 256, 32,
                () => GC.KeepAlive(benchmark.ColumnCompletion()), input, suggestions));
        }
        var filter = CreateFilterFixture();
        using var view = NewView(filter);
        await view.InitializeAsync();
        var first = await view.GetPageAsync(0);
        Require(first.Values.Count == 100 && first.TotalVisibleValueCount == 10_000 && first.IsSourceTruncated,
            "The large filter fixture must publish a bounded, explicitly truncated source.");
        var filterInput = new { rows = 100_000, distinct = 100_000, pageSize = 100, sourceCap = 10_000,
            generator = "Value{row:D6}, rows 1..100000, table Values, column Code" };
        measurements.Add(Measure("filter.open.100000", 2, 2, () =>
        {
            using var opened = NewView(filter);
            opened.InitializeAsync().GetAwaiter().GetResult();
        }, filterInput, PageEvidence(first)));
        measurements.Add(Measure("filter.cachedPage.100000", 512, 64,
            () => GC.KeepAlive(view.GetPageAsync(0).GetAwaiter().GetResult()), filterInput, PageEvidence(first)));
        measurements.Add(Measure("filter.searchCycle.100000", 4, 2, () =>
        {
            view.SetSearchTextAsync("0001").GetAwaiter().GetResult();
            view.SetSearchTextAsync(string.Empty).GetAwaiter().GetResult();
        }, filterInput, PageEvidence(first)));
        var stress = args[0] == "verify" ? await PERF008Stress.VerifyAsync(filter.Session, filter.Target) : null;
        var report = new
        {
            schema = "perf008-worker-v1", mode = args[0], runtime = Environment.Version.ToString(),
            framework = RuntimeInformation.FrameworkDescription, os = RuntimeInformation.OSDescription,
            architecture = RuntimeInformation.ProcessArchitecture.ToString(), processors = Environment.ProcessorCount,
            configuration = "Release", culture = CultureInfo.CurrentCulture.Name, uiCulture = CultureInfo.CurrentUICulture.Name,
            tieredCompilation = Environment.GetEnvironmentVariable("DOTNET_TieredCompilation"),
            serverGc = System.Runtime.GCSettings.IsServerGC, stopwatchFrequency = Stopwatch.Frequency,
            allocationScope = "GC.GetTotalAllocatedBytes(precise:true), process-wide, includes async worker allocations",
            measurements, stress,
        };
        await File.WriteAllTextAsync(args[1], JsonSerializer.Serialize(report, JsonOptions));
    }

    private static Measurement Measure(string name, int operations, int warmup, Action action, object input, object output)
    {
        for (var i = 0; i < warmup; i++) action();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        var allocated = GC.GetTotalAllocatedBytes(precise: true);
        var start = Stopwatch.GetTimestamp();
        for (var i = 0; i < operations; i++) action();
        var elapsed = Stopwatch.GetTimestamp() - start;
        var bytes = GC.GetTotalAllocatedBytes(precise: true) - allocated;
        return new Measurement(name, operations, warmup, elapsed, bytes,
            elapsed * 1_000_000d / Stopwatch.Frequency / operations, bytes / (double)operations,
            GC.CollectionCount(0) - gen0, GC.CollectionCount(1) - gen1, GC.CollectionCount(2) - gen2,
            Hash(input), Hash(output), JsonSerializer.SerializeToElement(input, input.GetType()),
            JsonSerializer.SerializeToElement(output, output.GetType()));
    }

    internal static string Hash(object value) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, value.GetType()))));

    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static object PageEvidence(SpreadsheetAutoFilterPagedPage page) => new
    {
        page.Offset, page.PageSize, page.TotalVisibleValueCount, page.IsSourceTruncated,
        values = page.Values.Select(item => new { item.DisplayText, item.IsSelected }).ToArray(),
    };

    private static SpreadsheetAutoFilterPagedView NewView(FilterFixture fixture) => new(
        SpreadsheetAutoFilterPagedSessionFactory.Create(fixture.Session, fixture.Target), pageSize: 100);

    private static FilterFixture CreateFilterFixture()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "Code");
        for (var row = 1; row <= 100_000; row++) sheet.SetValue(new CellAddress(row, 0), $"Value{row:D6}");
        sheet.AddTable(new SpreadsheetTable(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Values",
            new CellRange(default, new CellAddress(100_000, 0)),
            [new SpreadsheetTableColumn(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Code")]));
        var session = new SpreadsheetSession(workbook);
        Require(session.TryResolveAutoFilterTarget(new CellAddress(1, 0), out var target), "Missing Table filter target.");
        return new FilterFixture(session, target);
    }

    private sealed record FilterFixture(SpreadsheetSession Session, SpreadsheetAutoFilterTarget Target);
    private sealed record Measurement(string Name, int Operations, int Warmup, long ElapsedTicks, long AllocatedBytes,
        double MicrosecondsPerOperation, double BytesPerOperation, int Gen0, int Gen1, int Gen2,
        string InputHash, string OutputHash, JsonElement Input, JsonElement Output);
}
