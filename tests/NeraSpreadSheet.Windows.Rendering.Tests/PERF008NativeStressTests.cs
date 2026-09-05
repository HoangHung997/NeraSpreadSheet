using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;
using Wpf = NeraSpreadSheet.Wpf;
using Win = NeraSpreadSheet.WinForms;
using Forms = System.Windows.Forms;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class PERF008NativeStressTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [Timeout(180_000)]
    public void PERF008WpfShouldKeepScrollIndependentAndReleaseSubscriptionsAfterRepeatedChromeChanges()
    {
        var evidence = new List<object>();
        RunSta(() =>
        {
            var session = CreateSession();
            var runtime = new RibbonRuntimeController(RibbonProductionCommandCatalog.CreateDefaultDefinition(), session.Commands);
            using var grid = new Wpf.NeraSpreadsheetControl { Session = session };
            var root = new System.Windows.Controls.DockPanel();
            root.Children.Add(new System.Windows.Documents.AdornerDecorator { Child = grid });
            var window = new System.Windows.Window { Content = root, Width = 1280, Height = 800, ShowInTaskbar = false };
            try
            {
                window.Show();
                PumpWpf();
                var subscriptions = CountSubscriptions(runtime);
                var gridSubscriptions = CountSubscriptions(grid);
                for (var cycle = 0; cycle < 12; cycle++)
                {
                    using (var ribbon = new Wpf.NeraRibbonControl(runtime))
                    using (var filter = new Wpf.NeraAutoFilterPagedPopupPresenter(grid))
                    using (ribbon.BindTableDesign(session))
                    using (ribbon.BindShortcuts(window))
                    {
                        System.Windows.Controls.DockPanel.SetDock(ribbon, System.Windows.Controls.Dock.Top);
                        root.Children.Insert(0, ribbon);
                        window.Width = new[] { 1536d, 1280d, 1024d, 820d }[cycle % 4];
                        ribbon.IconTheme = (NeraIconTheme)(cycle % 4);
                        filter.IconTheme = ribbon.IconTheme;
                        runtime.SetCustomization(new RibbonCustomization([new RibbonTabCustomization(runtime.Definition.Tabs[0].Id, caption: "Tùy biến PERF008")]));
                        PumpWpf();
                        runtime.SetCustomization(null);
                        grid.ScrollTo(0, 0);
                        PumpWpf();
                        Assert.IsTrue(filter.TryOpenForActiveCell());
                        PumpUntil(() => ReadField<Wpf.NeraWpfAutoFilterPagedBinding>(filter, "_binding").Items.Count > 0, PumpWpf);
                        var binding = ReadField<Wpf.NeraWpfAutoFilterPagedBinding>(filter, "_binding");
                        var source = FilterSource(binding);
                        var refreshes = 0;
                        source.Refreshed += (_, _) => refreshes++;
                        var layout = ribbon.LayoutSnapshot;
                        var snapshot = runtime.Snapshot;
                        var generation = source.Generation;
                        for (var step = 1; step <= 8; step++)
                        {
                            grid.ScrollTo(0, step * 13.25);
                            PumpWpf();
                        }
                        PumpUntil(() => Math.Abs(grid.ScrollSnapshot.OffsetY - 106d) < .001, PumpWpf);
                        Assert.AreSame(layout, ribbon.LayoutSnapshot, "Pure scrolling rebuilt native WPF Ribbon.");
                        Assert.AreSame(snapshot, runtime.Snapshot);
                        Assert.AreEqual(0, refreshes, "Pure scrolling rescanned the filter catalog.");
                        Assert.AreEqual(generation, source.Generation);
                        Assert.AreSame(binding, ReadField<Wpf.NeraWpfAutoFilterPagedBinding>(filter, "_binding"));
                        Assert.IsTrue(binding.Items.Count <= 100);
                        AssertSentinel(session);
                        var search = binding.SearchAsync("Value0001");
                        PumpUntil(() => search.IsCompleted, PumpWpf);
                        search.GetAwaiter().GetResult();
                        Assert.AreEqual("Value0001", binding.SearchText);
                        ReadField<System.Windows.Controls.TextBox>(filter, "_searchBox").Text = "cancelled pending search";
                        filter.Close();
                        Assert.IsFalse(filter.IsOpen);
                        root.Children.Remove(ribbon);
                    }
                    PumpWpf();
                    Assert.AreEqual(subscriptions, CountSubscriptions(runtime), "Disposed WPF Ribbon retained runtime subscriptions.");
                    Assert.AreEqual(gridSubscriptions, CountSubscriptions(grid), "Disposed WPF filter retained grid subscriptions.");
                    if (cycle % 3 == 2) evidence.Add(Memory(cycle + 1, subscriptions, gridSubscriptions));
                }
            }
            finally { window.Close(); PumpWpf(); }
        });
        SaveEvidence("wpf", evidence);
    }

    [TestMethod]
    [Timeout(180_000)]
    public void PERF008WinFormsShouldKeepScrollIndependentAndReleaseSubscriptionsAfterRepeatedChromeChanges()
    {
        var evidence = new List<object>();
        RunSta(() =>
        {
            var session = CreateSession();
            var runtime = new RibbonRuntimeController(RibbonProductionCommandCatalog.CreateDefaultDefinition(), session.Commands);
            using var form = new Forms.Form { ClientSize = new System.Drawing.Size(1280, 800), ShowInTaskbar = false };
            using var grid = new Win.NeraSpreadsheetControl { Session = session, Dock = Forms.DockStyle.Fill };
            form.Controls.Add(grid);
            form.Show();
            Forms.Application.DoEvents();
            var subscriptions = CountSubscriptions(runtime);
            var gridSubscriptions = CountSubscriptions(grid);
            for (var cycle = 0; cycle < 12; cycle++)
            {
                using (var ribbon = new Win.NeraRibbonControl(runtime) { Dock = Forms.DockStyle.Top, Height = 180 })
                using (var filter = new Win.NeraAutoFilterPagedDropDownPresenter(grid))
                using (ribbon.BindTableDesign(session))
                using (ribbon.BindShortcuts(form))
                {
                    form.Controls.Add(ribbon);
                    form.ClientSize = new System.Drawing.Size(new[] { 1536, 1280, 1024, 820 }[cycle % 4], 800);
                    ribbon.IconTheme = (NeraIconTheme)(cycle % 4);
                    filter.IconTheme = ribbon.IconTheme;
                    runtime.SetCustomization(new RibbonCustomization([new RibbonTabCustomization(runtime.Definition.Tabs[0].Id, caption: "Tùy biến PERF008")]));
                    Forms.Application.DoEvents();
                    runtime.SetCustomization(null);
                    grid.ScrollTo(0, 0);
                    Forms.Application.DoEvents();
                    Assert.IsTrue(filter.TryOpenForActiveCell());
                    PumpUntil(() => ReadField<Win.NeraWinFormsAutoFilterPagedBinding>(filter, "_binding").Items.Count > 0, Forms.Application.DoEvents);
                    var binding = ReadField<Win.NeraWinFormsAutoFilterPagedBinding>(filter, "_binding");
                    var source = FilterSource(binding);
                    var refreshes = 0;
                    source.Refreshed += (_, _) => refreshes++;
                    var layout = ribbon.LayoutSnapshot;
                    var snapshot = runtime.Snapshot;
                    var generation = source.Generation;
                    for (var step = 1; step <= 8; step++)
                    {
                        grid.ScrollTo(0, step * 13.25);
                        Forms.Application.DoEvents();
                    }
                    PumpUntil(() => Math.Abs(grid.ScrollSnapshot.OffsetY - 106d) < .001, Forms.Application.DoEvents);
                    Assert.AreSame(layout, ribbon.LayoutSnapshot, "Pure scrolling rebuilt native WinForms Ribbon.");
                    Assert.AreSame(snapshot, runtime.Snapshot);
                    Assert.AreEqual(0, refreshes, "Pure scrolling rescanned the filter catalog.");
                    Assert.AreEqual(generation, source.Generation);
                    Assert.IsTrue(binding.Items.Count <= 100);
                    AssertSentinel(session);
                    var search = binding.SearchAsync("Value0001");
                    PumpUntil(() => search.IsCompleted, Forms.Application.DoEvents);
                    search.GetAwaiter().GetResult();
                    Assert.AreEqual("Value0001", binding.SearchText);
                    ReadField<Forms.TextBox>(filter, "_searchBox").Text = "cancelled pending search";
                    filter.Close();
                    Assert.IsFalse(filter.IsOpen);
                    form.Controls.Remove(ribbon);
                }
                Forms.Application.DoEvents();
                Assert.AreEqual(subscriptions, CountSubscriptions(runtime), "Disposed WinForms Ribbon retained runtime subscriptions.");
                Assert.AreEqual(gridSubscriptions, CountSubscriptions(grid), "Disposed WinForms filter retained grid subscriptions.");
                if (cycle % 3 == 2) evidence.Add(Memory(cycle + 1, subscriptions, gridSubscriptions));
            }
            form.Close();
            Forms.Application.DoEvents();
        });
        SaveEvidence("winforms", evidence);
    }

    private void SaveEvidence(string host, List<object> evidence)
    {
        var path = Path.Combine(TestContext.TestRunDirectory!, $"PERF008-{host}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { host, cycles = 12, samples = evidence,
            scope = "native synthetic lifecycle, subscriptions and managed/private memory observations; not physical input latency or leak-free process certification" }));
        TestContext.AddResultFile(path);
    }

    private static object Memory(int cycles, int runtimeSubscriptions, int gridSubscriptions)
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        using var process = Process.GetCurrentProcess();
        return new { cycles, managedBytes = GC.GetTotalMemory(false), privateBytes = process.PrivateMemorySize64,
            workingSetBytes = process.WorkingSet64, handleCount = process.HandleCount, runtimeSubscriptions, gridSubscriptions };
    }

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "Code");
        for (var row = 1; row <= 2_000; row++) sheet.SetValue(new CellAddress(row, 0), $"Value{row:D6}");
        sheet.AddTable(new SpreadsheetTable(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Values",
            new CellRange(default, new CellAddress(2_000, 0)),
            [new SpreadsheetTableColumn(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Code")]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        sheet.SetValue(new CellAddress(0, 20), 12345d);
        sheet.SetFormula(new CellAddress(0, 20), "=1+1");
        return session;
    }

    private static void AssertSentinel(SpreadsheetSession session)
    {
        Assert.AreEqual(CellValue.FromObject(12345d), session.ActiveWorksheet.GetCell(new CellAddress(0, 20)).Value,
            "Scroll-only native input recalculated a formula.");
        Assert.AreEqual(0, session.History.UndoCount);
    }

    // Probes fail closed if implementation fields change; no substitute filter or
    // Ribbon controller is used to infer the behavior of an unobserved native host.
    private static ISpreadsheetAutoFilterPagedSession FilterSource(object binding) =>
        ReadField<ISpreadsheetAutoFilterPagedSession>(ReadField<SpreadsheetAutoFilterPagedView>(
            ReadField<SpreadsheetAutoFilterPagedPresenter>(binding, "_presenter"), "_view"), "_session");

    private static T ReadField<T>(object source, string name) where T : class =>
        source.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(source) as T
        ?? throw new AssertFailedException($"Required PERF008 probe missing: {source.GetType().Name}.{name}");

    private static int CountSubscriptions(object source) => source.GetType()
        .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
        .Where(field => typeof(Delegate).IsAssignableFrom(field.FieldType))
        .Sum(field => (field.GetValue(source) as Delegate)?.GetInvocationList().Length ?? 0);

    private static void PumpUntil(Func<bool> complete, Action pump)
    {
        var timer = Stopwatch.StartNew();
        while (!complete() && timer.Elapsed < TimeSpan.FromSeconds(10)) { pump(); Thread.Sleep(5); }
        Assert.IsTrue(complete(), "Native operation did not finish within the bounded wait.");
    }

    private static void PumpWpf() => System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
        System.Windows.Threading.DispatcherPriority.ApplicationIdle, static () => { });

    private static void RunSta(Action action)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(150)), "Native stress STA timed out.");
        failure?.Throw();
    }
}
