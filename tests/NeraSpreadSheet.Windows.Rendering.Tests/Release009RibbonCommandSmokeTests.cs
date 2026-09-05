using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Windows;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Ribbon.Core;
using NeraSpreadSheet.Wpf;
using NeraSpreadSheet.Wpf.Sample;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

[TestClass]
[DoNotParallelize]
public sealed class Release009RibbonCommandSmokeTests
{
    [TestMethod]
    [Timeout(60_000)]
    public void LoadedShellShouldExposeEveryRegisteredCommandAndRetainContextGates() => RunLoaded(
        (window, session, runtime, registry, _) =>
        {
            // Audit the actual registry, not a second catalog used by the application.
            RibbonCommandCatalogAudit.ValidateExact(session.Commands, runtime.Definition,
                RibbonProductionCommandCatalog.CommandIds);
            RibbonCommandCatalogAudit.Validate(registry, runtime.Definition, registry.RegisteredCommandIds);
            Assert.HasCount(49, session.Commands.RegisteredCommandIds);
            var hostIds = registry.RegisteredCommandIds.Except(session.Commands.RegisteredCommandIds).ToArray();
            Assert.HasCount(36, hostIds);
            Assert.IsTrue(hostIds.All(id => id.Value.StartsWith("Sample.", StringComparison.Ordinal)));
            foreach (var id in registry.RegisteredCommandIds)
            {
                Assert.IsTrue(registry.TryResolve(id, out var descriptor, out var handler));
                Assert.IsNotNull(descriptor);
                Assert.IsNotNull(handler);
            }

            Assert.IsFalse(runtime.Snapshot.Tabs.Any(tab => tab.Id == "table-design"));
            AssertDisabled(registry, "Table.Rename");
            AssertDisabled(registry, "Table.TotalsFunction");
            AssertDisabled(registry, "Sample.FilterClear");
            AssertDisabled(registry, "Sample.FilterReapply");
            AssertDisabled(registry, "View.UnfreezePanes");
            AssertDisabled(registry, "View.Split.Undo");
            AssertDisabled(registry, "View.Split.Redo");
            var cell = session.ActiveWorksheet.GetCell(default);
            var history = session.History.UndoCount;
            Assert.IsFalse(runtime.TryActivateAsync("Table.Rename").AsTask().GetAwaiter().GetResult());
            Assert.IsFalse(runtime.TryActivateAsync("Sample.FilterClear").AsTask().GetAwaiter().GetResult());
            Pump(window);
            Assert.AreEqual(cell, session.ActiveWorksheet.GetCell(default));
            Assert.AreEqual(history, session.History.UndoCount);
        });

    [TestMethod]
    [Timeout(60_000)]
    public void LoadedStyleCommandsShouldChangeRealStylesAndCreateExactlyOneUndo() => RunLoaded(
        (window, session, runtime, _, _) =>
        {
            (string Id, string? Choice, Func<CellStyle, bool> Verify)[] cases =
            [
                ("Cell.Format.Bold", null, style => style.Font.Weight == 700),
                ("Cell.Format.Italic", null, style => style.Font.Italic),
                ("Sample.Font", "Arial", style => style.Font.Family == "Arial"),
                ("Sample.FontSize", "18", style => style.Font.Size == 18),
                ("Sample.Underline", null, style => style.Font.Underline),
                ("Sample.Fill", "#C00000", style => style.Fill.Color == new ColorRgba(192, 0, 0)),
                ("Sample.FontColor", "#C00000", style => style.Font.Color == new ColorRgba(192, 0, 0)),
                ("Sample.Align.Left", null, style => style.Alignment.Horizontal == CellHorizontalAlignment.Left),
                ("Sample.Align.Center", null, style => style.Alignment.Horizontal == CellHorizontalAlignment.Center),
                ("Sample.Align.Right", null, style => style.Alignment.Horizontal == CellHorizontalAlignment.Right),
                ("Sample.Wrap", null, style => style.Alignment.WrapText),
                ("Sample.Borders", null, style => style.Border.Left.Style == CellBorderLineStyle.Thin &&
                    style.Border.Right.Style == CellBorderLineStyle.Thin &&
                    style.Border.Top.Style == CellBorderLineStyle.Thin &&
                    style.Border.Bottom.Style == CellBorderLineStyle.Thin),
                ("Sample.Number", "dd/mm/yyyy", style => style.NumberFormat.FormatCode == "dd/mm/yyyy"),
                ("Sample.Percent", null, style => style.NumberFormat.FormatCode == "0%"),
                ("Sample.Decimal", null, style => style.NumberFormat.FormatCode == "#,##0.00"),
            ];
            foreach (var test in cases)
            {
                var before = session.Styles.ActiveCellStyle;
                var value = session.ActiveWorksheet.GetCell(default).Value;
                var history = session.History.UndoCount;
                Activate(window, runtime, test.Id, test.Choice);
                Assert.IsTrue(test.Verify(session.Styles.ActiveCellStyle), test.Id);
                Assert.AreNotEqual(before, session.Styles.ActiveCellStyle, test.Id);
                Assert.AreEqual(value, session.ActiveWorksheet.GetCell(default).Value, test.Id);
                Assert.AreEqual(history + 1, session.History.UndoCount, test.Id);
                Activate(window, runtime, "Edit.Undo");
                Assert.AreEqual(before, session.Styles.ActiveCellStyle, test.Id);
                Assert.AreEqual(history, session.History.UndoCount, test.Id);
            }
        });

    [TestMethod]
    [Timeout(60_000)]
    public void LoadedPageCommandsShouldChangePrintSettingsAndUndoWithoutChangingCellData() => RunLoaded(
        (window, session, runtime, _, _) =>
        {
            (string Id, string? Choice, Func<SpreadsheetPageSetup, bool> Verify)[] cases =
            [
                ("Sample.Orientation", "landscape", setup => setup.Orientation == SpreadsheetPageOrientation.Landscape),
                ("Sample.Paper", "A3", setup => setup.PaperSize == SpreadsheetPaperSize.A3),
                ("Sample.Margins", "narrow", setup => setup.Margins == SpreadsheetPageMargins.Narrow),
                ("Sample.PrintGrid", null, setup => setup.PrintGridlines),
                ("Sample.PrintHeadings", null, setup => setup.PrintHeadings),
            ];
            foreach (var test in cases)
            {
                var before = JsonSerializer.Serialize(session.ActiveWorksheet.GetPrintSettings());
                var cell = session.ActiveWorksheet.GetCell(default);
                var history = session.History.UndoCount;
                Activate(window, runtime, test.Id, test.Choice);
                Assert.IsTrue(test.Verify(session.ActiveWorksheet.GetPrintSettings().PageSetup), test.Id);
                Assert.AreEqual(history + 1, session.History.UndoCount, test.Id);
                Assert.AreEqual(cell, session.ActiveWorksheet.GetCell(default), test.Id);
                Activate(window, runtime, "Edit.Undo");
                Assert.AreEqual(before, JsonSerializer.Serialize(session.ActiveWorksheet.GetPrintSettings()), test.Id);
                Assert.AreEqual(history, session.History.UndoCount, test.Id);
            }
        });

    [TestMethod]
    [Timeout(60_000)]
    public void LoadedFormulaButtonsShouldStartTheExistingEditorAndCancelWithoutMutation() => RunLoaded(
        (window, session, runtime, _, grid) =>
        {
            (string Id, string Draft)[] cases =
            [
                ("Sample.FormulaSum", "=SUM("), ("Sample.FormulaAverage", "=AVERAGE("),
                ("Sample.FormulaIf", "=IF("), ("Sample.FormulaLookup", "=XLOOKUP("),
            ];
            foreach (var test in cases)
            {
                var cell = session.ActiveWorksheet.GetCell(default);
                var history = session.History.UndoCount;
                Activate(window, runtime, test.Id);
                Assert.IsTrue(session.Editor.IsEditing, test.Id);
                Assert.AreEqual(test.Draft, grid.CurrentEditText, test.Id);
                Assert.AreEqual(Visibility.Visible, Field<System.Windows.Controls.TextBox>(grid, "_editor").Visibility);
                Assert.IsTrue(grid.CancelEditor(), test.Id);
                Pump(window);
                Assert.IsFalse(session.Editor.IsEditing, test.Id);
                Assert.AreEqual(Visibility.Collapsed, Field<System.Windows.Controls.TextBox>(grid, "_editor").Visibility);
                Assert.AreEqual(cell, session.ActiveWorksheet.GetCell(default), test.Id);
                Assert.AreEqual(history, session.History.UndoCount, test.Id);
            }
        });

    [TestMethod]
    [Timeout(60_000)]
    public void LoadedViewCommandsShouldChangeHostPresentationWithoutWorkbookHistory() => RunLoaded(
        (window, session, runtime, _, grid) =>
        {
            var cell = session.ActiveWorksheet.GetCell(default);
            var history = session.History.UndoCount;
            var headers = grid.RenderTheme.ShowHeaders;
            var gridLine = grid.RenderTheme.GridLine;
            Activate(window, runtime, "Sample.Zoom", "150");
            Assert.AreEqual(1.5, grid.Zoom);
            Activate(window, runtime, "Sample.ZoomReset");
            Assert.AreEqual(1d, grid.Zoom);
            Activate(window, runtime, "Sample.Headers");
            Assert.AreEqual(!headers, grid.RenderTheme.ShowHeaders);
            Activate(window, runtime, "Sample.Headers");
            Assert.AreEqual(headers, grid.RenderTheme.ShowHeaders);
            Activate(window, runtime, "Sample.Gridlines");
            Assert.AreEqual((byte)0, grid.RenderTheme.GridLine.Alpha);
            Activate(window, runtime, "Sample.Gridlines");
            Assert.AreEqual(gridLine, grid.RenderTheme.GridLine);
            Assert.AreEqual(cell, session.ActiveWorksheet.GetCell(default));
            Assert.AreEqual(history, session.History.UndoCount);
            Assert.AreEqual(default(CellAddress), session.Selection.ActiveCell);
        });

    private static void AssertDisabled(CommandRegistry registry, string id)
    {
        Assert.IsTrue(registry.TryResolve(id, out _, out var handler));
        Assert.IsNotNull(handler);
        Assert.IsFalse(handler.CanExecute(default), id);
    }

    private static void Activate(Window window, RibbonRuntimeController runtime, string id, string? choice = null)
    {
        // These cases only invoke in-memory commands; modal/file dialogs are deliberately excluded.
        var operation = choice is null ? runtime.TryActivateAsync(id) : runtime.TryActivateItemAsync(id, choice);
        Assert.IsTrue(operation.AsTask().GetAwaiter().GetResult(), id);
        Pump(window);
    }

    private static T Field<T>(object target, string name) =>
        (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(target)
            ?? throw new InvalidOperationException($"Expected native field {name} was not found."));

    private static void Pump(Window window) => window.Dispatcher.Invoke(
        System.Windows.Threading.DispatcherPriority.ApplicationIdle, static () => { });

    private static void RunLoaded(Action<RibbonPreviewWindow, SpreadsheetSession, RibbonRuntimeController,
        CommandRegistry, NeraSpreadsheetControl> verify)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var session = new SpreadsheetSession(new Workbook());
                session.SetValue(default, CellValue.FromNumber(45751));
                using var window = new RibbonPreviewWindow(session) { ShowInTaskbar = false };
                try
                {
                    window.Show();
                    Pump(window);
                    var grid = Field<NeraSpreadsheetControl>(window, "_sheet");
                    Assert.IsTrue(window.IsLoaded && grid.IsLoaded);
                    Assert.AreSame(session, grid.Session);
                    verify(window, session, Field<RibbonRuntimeController>(window, "_runtime"),
                        Field<CommandRegistry>(window, "_commands"), grid);
                }
                finally { window.Close(); Pump(window); }
            }
            catch (Exception exception) { failure = ExceptionDispatchInfo.Capture(exception); }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(45)), "Loaded command audit timed out.");
        failure?.Throw();
    }
}
