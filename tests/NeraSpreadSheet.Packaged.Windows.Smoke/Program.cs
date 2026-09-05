using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.OpenXml;
using NeraSpreadSheet.Ribbon.Core;
using Forms = System.Windows.Forms;
using Win = NeraSpreadSheet.WinForms;
using Wpf = NeraSpreadSheet.Wpf;

namespace Packaged.Windows.Smoke;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Expected version, source SHA and evidence path.", nameof(args));
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("vi-VN");
        VerifyRoundTrip();
        VerifyWpf();
        VerifyWinForms();
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly != typeof(Program).Assembly &&
                assembly.GetName().Name!.StartsWith("NeraSpreadSheet.", StringComparison.Ordinal))
            .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
            .Select(assembly => new
            {
                name = assembly.GetName().Name,
                version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            }).ToArray();
        Require(assemblies.Any(assembly => assembly.name == "NeraSpreadSheet.Wpf") &&
            assemblies.Any(assembly => assembly.name == "NeraSpreadSheet.WinForms") &&
            assemblies.Any(assembly => assembly.name == "NeraSpreadSheet.OpenXml"), "Required packaged hosts were not loaded.");
        foreach (var assembly in assemblies)
        {
            Require(assembly.version?.StartsWith(args[0] + "+", StringComparison.Ordinal) == true &&
                assembly.version.Contains(args[1], StringComparison.Ordinal),
                $"Loaded SDK assembly does not match the packed version/source: {assembly.name} ({assembly.version}).");
        }
        File.WriteAllText(args[2], JsonSerializer.Serialize(new
        {
            schema = "release009-windows-consumer-v1", sourceSha = args[1], packageVersion = args[0],
            wpfLoaded = true, winFormsLoaded = true, editorCommitUndo = true, tableFilterRibbon = true,
            syntheticXlsxRoundTrip = true, assemblies,
            scope = "PackageReference integration and loaded default desktop hosts; not physical keyboard, GPU or complete Excel parity acceptance",
        }));
        return 0;
    }

    private static SpreadsheetSession CreateSession()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "Code");
        for (var row = 1; row <= 50; row++) sheet.SetValue(new CellAddress(row, 0), $"Item {row}");
        sheet.AddTable(new SpreadsheetTable(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Items",
            new CellRange(default, new CellAddress(50, 0)),
            [new SpreadsheetTableColumn(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Code")]));
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        return session;
    }

    private static RibbonRuntimeController CreateRibbon(SpreadsheetSession session)
    {
        var definition = RibbonProductionCommandCatalog.CreateDefaultDefinition();
        RibbonCommandCatalogAudit.ValidateExact(session.Commands, definition, RibbonProductionCommandCatalog.CommandIds);
        return new RibbonRuntimeController(definition, session.Commands);
    }

    private static void VerifyEdit(SpreadsheetSession session)
    {
        var target = new CellAddress(0, 4);
        var before = session.ActiveWorksheet.GetCell(target);
        var history = session.History.UndoCount;
        session.Editor.BeginEdit(target);
        Require(session.Editor.Commit("=1+1") && !session.Editor.IsEditing, "Formula commit did not finish.");
        Require(session.ActiveWorksheet.GetCell(target).Value == CellValue.FromObject(2d), "Packaged formula engine did not calculate.");
        Require(session.Undo(), "Packaged Undo failed.");
        Require(session.ActiveWorksheet.GetCell(target) == before && session.History.UndoCount == history,
            "Undo did not restore the pre-edit cell and history.");
        session.Editor.BeginEdit(target);
        Require(session.Editor.Commit("Dòng một\nDòng hai"), "Multiline literal commit failed.");
        Require(session.ActiveWorksheet.GetCell(target).Value.ToString() == "Dòng một\nDòng hai", "Multiline literal changed.");
        var committed = session.ActiveWorksheet.GetCell(target);
        history = session.History.UndoCount;
        session.Editor.BeginEdit(target);
        Require(session.Editor.Cancel(), "Packaged Cancel failed.");
        Require(!session.Editor.IsEditing && session.ActiveWorksheet.GetCell(target) == committed && session.History.UndoCount == history,
            "Cancel changed committed content/history or left editing active.");
        session.Selection.SetActiveCell(new CellAddress(1, 0));
    }

    private static void VerifyNativeDraft(SpreadsheetSession session, Action<string> begin, Func<bool> commit, Func<bool> cancel, Action pump)
    {
        var target = new CellAddress(1, 4);
        session.Selection.SetActiveCell(target);
        var before = session.ActiveWorksheet.GetCell(target);
        var history = session.History.UndoCount;
        begin("=2+3");
        pump();
        Require(session.Editor.IsEditing && commit() && !session.Editor.IsEditing, "Native overlay commit failed.");
        Require(session.ActiveWorksheet.GetCell(target).Value == CellValue.FromObject(5d), "Native draft did not reach calculation.");
        Require(session.Undo() && session.ActiveWorksheet.GetCell(target) == before && session.History.UndoCount == history,
            "Native overlay Undo did not restore cell/history.");
        begin("Bản nháp không được lưu");
        pump();
        Require(session.Editor.IsEditing && cancel(), "Native changed-draft Cancel failed.");
        Require(!session.Editor.IsEditing && session.ActiveWorksheet.GetCell(target) == before && session.History.UndoCount == history,
            "Native Cancel committed a changed draft or altered history.");
        session.Selection.SetActiveCell(new CellAddress(1, 0));
    }

    private static void VerifyWpf()
    {
        var session = CreateSession();
        var runtime = CreateRibbon(session);
        using var grid = new Wpf.NeraSpreadsheetControl { Session = session };
        using var ribbon = new Wpf.NeraRibbonControl(runtime);
        using var filter = new Wpf.NeraAutoFilterPagedPopupPresenter(grid);
        using var tableBinding = ribbon.BindTableDesign(session);
        var panel = new System.Windows.Controls.DockPanel();
        System.Windows.Controls.DockPanel.SetDock(ribbon, System.Windows.Controls.Dock.Top);
        panel.Children.Add(ribbon);
        panel.Children.Add(new System.Windows.Documents.AdornerDecorator { Child = grid });
        var window = new System.Windows.Window { Content = panel, Width = 1000, Height = 700, ShowInTaskbar = false };
        using var shortcuts = ribbon.BindShortcuts(window);
        try
        {
            window.Show();
            PumpUntil(() => grid.IsLoaded && grid.ActualWidth > 0 && HasCurrentWpfLayout(ribbon), PumpWpf);
            VerifyEdit(session);
            VerifyNativeDraft(session, grid.BeginEdit, grid.CommitEditor, grid.CancelEditor, PumpWpf);
            PumpWpf();
            Require(filter.TryOpenForActiveCell(), "Packaged WPF Table filter did not open.");
            var binding = ReadBinding<Wpf.NeraWpfAutoFilterPagedBinding>(filter);
            PumpUntil(() => filter.IsOpen && !binding.IsBusy && binding.TotalItemCount == 50 && HasExpectedItems(binding.Items), PumpWpf);
            filter.Close();
            Require(!filter.IsOpen, "WPF Table filter did not close.");
            var previousWidth = grid.ActualWidth;
            var previousLayoutWidth = ribbon.LayoutSnapshot.AvailableWidth;
            window.Width = 820;
            PumpUntil(() => Math.Abs(window.ActualWidth - 820d) < 1d && grid.ActualWidth < previousWidth - 100d &&
                ribbon.LayoutSnapshot.AvailableWidth < previousLayoutWidth && HasCurrentWpfLayout(ribbon), PumpWpf);
        }
        finally { window.Close(); PumpWpf(); }
    }

    private static void VerifyWinForms()
    {
        var session = CreateSession();
        var runtime = CreateRibbon(session);
        using var form = new Forms.Form { ClientSize = new System.Drawing.Size(1000, 700), ShowInTaskbar = false };
        using var grid = new Win.NeraSpreadsheetControl { Session = session, Dock = Forms.DockStyle.Fill };
        using var ribbon = new Win.NeraRibbonControl(runtime) { Dock = Forms.DockStyle.Top, Height = 180 };
        using var filter = new Win.NeraAutoFilterPagedDropDownPresenter(grid);
        using var tableBinding = ribbon.BindTableDesign(session);
        using var shortcuts = ribbon.BindShortcuts(form);
        form.Controls.Add(grid);
        form.Controls.Add(ribbon);
        form.Show();
        PumpUntil(() => grid.IsHandleCreated && grid.ClientSize.Width > 0 && HasCurrentWinFormsLayout(ribbon), Forms.Application.DoEvents);
        VerifyEdit(session);
        VerifyNativeDraft(session, grid.BeginEdit, grid.CommitEditor, grid.CancelEditor, Forms.Application.DoEvents);
        Forms.Application.DoEvents();
        Require(filter.TryOpenForActiveCell(), "Packaged WinForms Table filter did not open.");
        var binding = ReadBinding<Win.NeraWinFormsAutoFilterPagedBinding>(filter);
        PumpUntil(() => filter.IsOpen && !binding.IsBusy && binding.TotalItemCount == 50 && HasExpectedItems(binding.Items), Forms.Application.DoEvents);
        filter.Close();
        Require(!filter.IsOpen, "WinForms Table filter did not close.");
        var previousWidth = grid.ClientSize.Width;
        var previousLayoutWidth = ribbon.LayoutSnapshot.AvailableWidth;
        form.ClientSize = new System.Drawing.Size(820, 700);
        PumpUntil(() => form.ClientSize.Width == 820 && grid.ClientSize.Width < previousWidth - 100 &&
            ribbon.LayoutSnapshot.AvailableWidth < previousLayoutWidth && HasCurrentWinFormsLayout(ribbon), Forms.Application.DoEvents);
        form.Close();
        Forms.Application.DoEvents();
    }

    private static void VerifyRoundTrip()
    {
        // No dispatcher is active here. Only an in-memory synthetic workbook is used.
        var session = CreateSession();
        VerifyEdit(session);
        var serializer = new NeraOpenXmlWorkbookSerializer();
        using var stream = new MemoryStream();
        serializer.SaveAsync(session.Workbook, stream, new OpenXmlExportOptions()).GetAwaiter().GetResult();
        stream.Position = 0;
        var loaded = serializer.LoadAsync(stream, new OpenXmlImportOptions()).GetAwaiter().GetResult();
        Require(loaded.Worksheets[0].Tables.Single().Name == "Items", "Packaged Table XLSX roundtrip failed.");
        Require(loaded.Worksheets[0].GetCell(new CellAddress(0, 4)).Value.ToString() == "Dòng một\nDòng hai", "Packaged XLSX literal roundtrip failed.");
    }

    private static void PumpWpf() => System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
        System.Windows.Threading.DispatcherPriority.ApplicationIdle, static () => { });

    private static bool HasCurrentWpfLayout(Wpf.NeraRibbonControl ribbon) => ribbon.ActualWidth > 0 &&
        Math.Abs(ribbon.LayoutSnapshot.AvailableWidth - ribbon.ActualWidth * System.Windows.Media.VisualTreeHelper.GetDpi(ribbon).DpiScaleX) < 1d;

    private static bool HasCurrentWinFormsLayout(Win.NeraRibbonControl ribbon) => ribbon.ClientSize.Width > 0 &&
        Math.Abs(ribbon.LayoutSnapshot.AvailableWidth - Math.Max(0d, ribbon.ClientSize.Width - 8d * ribbon.DeviceDpi / 96d)) < 1d;

    private static bool HasExpectedItems(IEnumerable<SpreadsheetTableFilterValueItem> items)
    {
        var values = items.ToArray();
        return values.Length == 50 && values.All(item => item.IsSelected) &&
            values.Select(item => item.DisplayText).ToHashSet(StringComparer.Ordinal)
                .SetEquals(Enumerable.Range(1, 50).Select(row => $"Item {row}"));
    }

    // Fail closed if the actual popup binding changes; never initialize a substitute view.
    private static T ReadBinding<T>(object presenter) where T : class =>
        presenter.GetType().GetField("_binding", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(presenter) as T
        ?? throw new InvalidOperationException("The actual packaged popup paging binding was not found.");

    private static void PumpUntil(Func<bool> complete, Action pump)
    {
        var timer = Stopwatch.StartNew();
        while (!complete() && timer.Elapsed < TimeSpan.FromSeconds(15)) { pump(); Thread.Sleep(5); }
        Require(complete(), "Loaded consumer did not reach the required state before timeout.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
