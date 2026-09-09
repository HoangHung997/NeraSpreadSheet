using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using global::Avalonia.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Tests;

/// <summary>Identical portable harness runs against baseline and candidate product
/// assemblies. Short-run timings are observations, not a release performance gate.</summary>
[TestClass]
public sealed class RibbonRebuildProbeTests
{
    private static readonly int[] Widths = [820, 1280, 1536];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    [TestMethod]
    public Task RebuildProbeShouldPreserveAllCommandsWithoutExecutingThem() => AvaloniaTestEnvironment.OnUiAsync(() =>
    {
        var registry = new CommandRegistry();
        var handler = new Counter();
        var tabs = new List<RibbonTabDefinition>();
        for (var tab = 0; tab < 9; tab++)
        {
            var groups = new List<RibbonGroupDefinition>();
            for (var group = 0; group < 8; group++)
            {
                var items = new List<RibbonItemDefinition>();
                for (var item = 0; item < 10; item++)
                {
                    var id = $"Probe.{tab}.{group}.{item}";
                    registry.Register(new CommandDescriptor(id, "Lệnh " + item, iconKey: "file.save"), handler);
                    items.Add(new RibbonItemDefinition(id, IsLarge: item == 0));
                }
                groups.Add(new RibbonGroupDefinition("group" + group, "Nhóm " + group, items));
            }
            tabs.Add(new RibbonTabDefinition("tab" + tab, "Trang " + tab, groups));
        }
        var runtime = new RibbonRuntimeController(new RibbonDefinition(tabs), registry);
        using var ribbon = new NeraRibbonControl(runtime);
        var window = new Window { Width = 1280, Height = 240, Content = ribbon };
        var results = new List<Measurement>();
        window.Show();
        try
        {
            foreach (var width in Widths)
            {
                ribbon.Width = width; window.Width = width;
                for (var warmup = 0; warmup < 3; warmup++) { ribbon.Rebuild(); window.UpdateLayout(); }
                var durations = new double[7];
                var allocations = new long[7];
                for (var iteration = 0; iteration < durations.Length; iteration++)
                {
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    var start = Stopwatch.GetTimestamp();
                    ribbon.Rebuild(); window.UpdateLayout();
                    durations[iteration] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    allocations[iteration] = GC.GetAllocatedBytesForCurrentThread() - allocated;
                }
                var bodies = ribbon.NativeTabControl.Items.OfType<TabItem>().Count(tab => tab.Content is not null);
                results.Add(new Measurement(width, durations, allocations, bodies));
                Assert.HasCount(9, runtime.Snapshot.Tabs);
                Assert.AreEqual(720, runtime.Snapshot.Tabs.SelectMany(tab => tab.Groups).Sum(group => group.Items.Count));
                Assert.AreEqual("tab0", ribbon.SelectedTabId);
                Assert.AreEqual(0, handler.Count);
            }
            var product = typeof(NeraRibbonControl).Assembly;
            using var assembly = File.OpenRead(product.Location);
            var report = new
            {
                schema = "nera.ribbon.rebuild-probe.v1", sha = Environment.GetEnvironmentVariable("NERA_SOURCE_SHA"),
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                assemblySha256 = Convert.ToHexString(SHA256.HashData(assembly)).ToLowerInvariant(),
                commandCount = 720, tabCount = 9, iterations = 7, results,
            };
            var json = JsonSerializer.Serialize(report, JsonOptions);
            Console.WriteLine("NERA_RIBBON_REBUILD_PROBE " + JsonSerializer.Serialize(report));
            if (Environment.GetEnvironmentVariable("NERA_RIBBON_PROBE_OUTPUT") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
                File.WriteAllText(output, json);
            }
        }
        finally { window.Content = null; window.Close(); }
    });

    private sealed record Measurement(int width, double[] milliseconds, long[] allocatedBytes, int nativeBodies);
    private sealed class Counter : ICommandHandler
    {
        public int Count { get; private set; }
        public bool CanExecute(CommandContext context) => true;
        public ValueTask ExecuteAsync(CommandContext context) { Count++; return ValueTask.CompletedTask; }
    }
}
