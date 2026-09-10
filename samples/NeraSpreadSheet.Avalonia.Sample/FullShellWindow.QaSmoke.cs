using System.Security.Cryptography;
using System.Text.Json;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Interactivity;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.OpenXml;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    private static readonly int[] QaRibbonWidths = [768, 820, 1024, 1366, 1920];

    /// <summary>
    /// Scripted native regression for QA-GAPS-005. Routed command activation is
    /// intentionally not described as physical mouse/keyboard proof.
    /// </summary>
    internal async void StartQaGapsSmoke(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var checks = new List<string>();
        var captures = new List<RibbonCapture>();
        void Check(string id, bool condition)
        {
            if (!condition) throw new InvalidOperationException("QA gaps smoke failed: " + id);
            if (checks.Contains(id)) throw new InvalidOperationException("Duplicate QA check: " + id);
            checks.Add(id);
        }

        try
        {
            var sha = Environment.GetEnvironmentVariable("NERA_SOURCE_SHA");
            Check("exact-source", sha is { Length: 40 } && sha.All(Uri.IsHexDigit));
            await SettleRibbonAsync();
            Check("native-window", IsVisible && _split.ActiveSpreadsheet.RenderedFrameCount > 0);
            var directory = Path.Combine(Environment.GetEnvironmentVariable("NERA_AVALONIA_ARTIFACTS") ?? "artifacts/avalonia", "qa-gaps");
            Directory.CreateDirectory(directory);

            // Exact QA widths, including the old 768-DIP report case and the
            // 820-DIP pixel-rounding regression. Shared layout must keep all
            // groups inside the native client or move them to overflow.
            SetRibbonTheme(NeraIconTheme.Light);
            _ribbon.SelectTab("home");
            foreach (var width in QaRibbonWidths)
            {
                Width = width;
                _ribbon.Width = width;
                await SettleRibbonAsync();
                var geometry = VerifyRibbonGeometry(NeraIconTheme.Light, width, "home");
                Check("responsive-" + width, geometry.items > 0);
                CaptureRibbonScene(_ribbon, "qa-home-" + width, 1, directory, captures);
            }

            // A dynamic value that is valid but absent from the common-value list
            // must remain visible instead of blanking the ComboBox.
            Width = 1280; _ribbon.Width = 1280;
            _split.SetZoom(1.1);
            _runtime.Refresh();
            _ribbon.SelectTab("view");
            await SettleRibbonAsync();
            var zoom = FindRibbonControl<ComboBox>("ribbon-command-Ui.Zoom");
            Check("zoom-110-selected", zoom.SelectedItem is ComboBoxItem { Tag: "110", Content: "110%" });
            CaptureRibbonScene(_ribbon, "qa-zoom-110", 1, directory, captures);

            // Per-sheet native view state. Add distant sparse cells so the host
            // has real scroll extents; these direct fixture writes predate the
            // history assertions and do not simulate user edits.
            var session = Session;
            var first = session.Workbook.Worksheets[0];
            var second = session.Workbook.Worksheets[1];
            first.SetValue(new CellAddress(220, 24), "extent-a");
            second.SetValue(new CellAddress(260, 28), "extent-b");
            session.ActivateWorksheet(first);
            session.Selection.SetActiveCell(new CellAddress(9, 4));
            _split.SetZoom(1.35);
            _split.ActiveSpreadsheet.ScrollTo(31.125, 57.375);
            var firstState = session.View.GetWorksheetState(first);
            session.ActivateWorksheet(second);
            session.Selection.SetActiveCell(new CellAddress(15, 10));
            _split.SetZoom(0.85);
            _split.ActiveSpreadsheet.ScrollTo(409.25, 917.625);
            var secondState = session.View.GetWorksheetState(second);
            session.ActivateWorksheet(first);
            await SettleRibbonAsync();
            Check("view-a-selection", session.Selection.ActiveCell == new CellAddress(9, 4));
            Check("view-a-zoom", Math.Abs(_split.ActiveSpreadsheet.Zoom - firstState.Zoom) < 1e-9);
            Check("view-a-scroll-x", Math.Abs(_split.ActiveSpreadsheet.ScrollSnapshot.OffsetX - firstState.OffsetX) < 1e-9);
            Check("view-a-scroll-y", Math.Abs(_split.ActiveSpreadsheet.ScrollSnapshot.OffsetY - firstState.OffsetY) < 1e-9);
            session.ActivateWorksheet(second);
            await SettleRibbonAsync();
            Check("view-b-selection", session.Selection.ActiveCell == new CellAddress(15, 10));
            Check("view-b-zoom", Math.Abs(_split.ActiveSpreadsheet.Zoom - secondState.Zoom) < 1e-9);
            Check("view-b-scroll-x", Math.Abs(_split.ActiveSpreadsheet.ScrollSnapshot.OffsetX - secondState.OffsetX) < 1e-9);
            Check("view-b-scroll-y", Math.Abs(_split.ActiveSpreadsheet.ScrollSnapshot.OffsetY - secondState.OffsetY) < 1e-9);

            // Create a Table through the actual Ribbon command and verify the
            // shared contextual Table Design projection becomes visible.
            session.ActivateWorksheet(first);
            session.Selection.Select(new CellRange(new CellAddress(2, 0), new CellAddress(10, 3)));
            _runtime.Refresh(); _ribbon.SelectTab("insert"); await SettleRibbonAsync();
            Check("table-create-command-visible", FindRibbonControl<Button>("ribbon-command-Table.Create").IsEnabled);
            Check("table-create-executed", await _ribbon.ActivateCommandAsync("Table.Create"));
            Check("table-created", first.TableCount == 1 && first.TryGetTable(session.Selection.ActiveCell, out _));
            RefreshQaRibbonContext();
            Check("table-context-present", _runtime.Snapshot.Tabs.Any(tab => tab.Id == "table-design"));
            Check("table-context-selectable", _ribbon.SelectTab("table-design"));
            await SettleRibbonAsync();
            CaptureRibbonScene(_ribbon, "qa-table-design", 1, directory, captures);

            // The filter window is a native bounded projection over the existing
            // shared paged presenter, not a second filter engine.
            Check("filter-target", session.TryResolveActiveAutoFilterTarget(out var target));
            Check("filter-command", await _ribbon.ActivateCommandAsync("Ui.Filter"));
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
            Check("filter-window-open", _filterWindow is { IsVisible: true });
            Check("filter-page-bounded", _filterWindow!.Snapshot.Values.Count <= NeraAutoFilterWindow.PageSize);
            CaptureRibbonScene(_filterWindow, "qa-filter-window", 1, directory, captures);
            _filterWindow.Close(false);
            await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);

            // Exercise the same Table/AutoFilter engine, then the Ribbon reapply
            // and clear commands. Search/paging criteria are kept out of UI code.
            using (var presenter = new SpreadsheetAutoFilterPagedPresenter(session, target))
            {
                await presenter.InitializeAsync();
                if (presenter.Capture().Values.Count > 1)
                {
                    await presenter.SetSelectedAsync(0, false);
                    await presenter.ApplyValueSelectionAsync();
                }
            }
            Check("filter-reapply-command", await _ribbon.ActivateCommandAsync("Ui.FilterReapply"));
            Check("filter-clear-command", await _ribbon.ActivateCommandAsync("Ui.FilterClear"));

            // The Home/Cells Format menu must expose all four existing visibility
            // operations rather than hiding Unhide behind an undiscoverable command.
            _ribbon.SelectTab("home"); _runtime.Refresh(); await SettleRibbonAsync();
            var format = _runtime.Snapshot.Tabs.SelectMany(tab => tab.Groups).SelectMany(group => group.Items)
                .Single(item => item.Command.CommandId.Value == "Ui.CellsFormat").Command;
            Check("format-menu-four-actions", format.SelectableItems.Select(item => item.Value).Order().SequenceEqual(
                new[] { "column-hide", "column-unhide", "row-hide", "row-unhide" }));
            session.Selection.SetActiveCell(new CellAddress(20, 0));
            Check("row-hide", await _ribbon.ActivateChoiceAsync("Ui.CellsFormat", "row-hide") && first.Dimensions.IsRowHidden(20));
            Check("row-unhide", await _ribbon.ActivateChoiceAsync("Ui.CellsFormat", "row-unhide") && !first.Dimensions.IsRowHidden(20));
            session.Selection.SetActiveCell(new CellAddress(2, 2));
            Check("column-hide", await _ribbon.ActivateChoiceAsync("Ui.CellsFormat", "column-hide") && first.Dimensions.IsColumnHidden(2));
            Check("column-unhide", await _ribbon.ActivateChoiceAsync("Ui.CellsFormat", "column-unhide") && !first.Dimensions.IsColumnHidden(2));

            // Save/reopen the current session and prove that the Table, formulas
            // and ordinary data survive the end-to-end sample serializer path.
            var tableName = first.Tables[0].Name;
            using var output = new MemoryStream();
            await SaveCompatibleStreamAsync(session, output);
            output.Position = 0;
            var reloaded = await _serializer.LoadSessionAsync(output, OpenXmlImportOptions.ForMode(OpenXmlImportMode.Compatibility));
            Check("roundtrip-table", reloaded.ActiveWorksheet.TableCount == 1 && reloaded.ActiveWorksheet.Tables[0].Name == tableName);
            Check("roundtrip-formula", reloaded.ActiveWorksheet.GetFormula(new CellAddress(3, 3)) is { Length: > 0 });
            Check("no-history-from-view-switching", session.History.UndoCount > 0 && session.History.UndoCount < 20);

            Check("captures-complete", captures.Count == QaRibbonWidths.Length + 3);
            var manifest = new
            {
                schema = "nera.qa-gaps.native.v1",
                sha,
                nativeWindow = true,
                physicalInputTested = false,
                widths = QaRibbonWidths,
                assertions = checks.Count,
                checks,
                captures,
                assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(NeraRibbonControl).Assembly.Location))).ToLowerInvariant(),
            };
            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(manifest, RibbonVisualJson));
            Console.WriteLine("NERA_AVALONIA_QA_GAPS_SUCCESS " + JsonSerializer.Serialize(new
            {
                sha,
                nativeWindow = true,
                physicalInputTested = false,
                assertions = checks.Count,
                captures = captures.Count,
                widths = QaRibbonWidths,
            }));
            lifetime.Shutdown(0);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("NERA_AVALONIA_QA_GAPS_FAILURE " + exception);
            lifetime.Shutdown(1);
        }
    }
}
