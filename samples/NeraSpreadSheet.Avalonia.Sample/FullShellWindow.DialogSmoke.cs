using System.Security.Cryptography;
using System.Text.Json;
using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Interactivity;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    /// <summary>Exercises the registered modal commands on a native attached window.
    /// Automated routed activation is not a claim about physical mouse/IME or screen readers.</summary>
    internal async void StartDialogsSmoke(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var checks = new List<string>(); var captures = new List<RibbonCapture>();
        void Check(string id, bool condition)
        { if (!condition) throw new InvalidOperationException("Dialog smoke: " + id); if (checks.Contains(id)) throw new InvalidOperationException("Duplicate dialog assertion."); checks.Add(id); }
        try
        {
            var sha = Environment.GetEnvironmentVariable("NERA_SOURCE_SHA");
            Check("exact-source", sha is { Length: 40 } && sha.All(Uri.IsHexDigit));
            Check("native-window", IsVisible && _split.ActiveSpreadsheet.RenderedFrameCount > 0);
            var directory = Path.Combine(Environment.GetEnvironmentVariable("NERA_AVALONIA_ARTIFACTS") ?? "artifacts/avalonia", "dialogs");
            Directory.CreateDirectory(directory);
            var sheet = Session.ActiveWorksheet; var target = new CellAddress(8, 8); sheet.SetValue(target, 1234.5); Session.Selection.SetActiveCell(target);
            var version = sheet.Version; var history = Session.History.UndoCount;
            foreach (var theme in Enum.GetValues<NeraIconTheme>())
            {
                SetRibbonTheme(theme); await SettleRibbonAsync();
                foreach (var (id, tab, name) in new[] { ("Ui.Dialog.Number", NeraFormatCellsTab.Number, "number"), ("Ui.Dialog.Font", NeraFormatCellsTab.Font, "font"), ("Ui.Dialog.Alignment", NeraFormatCellsTab.Alignment, "alignment") })
                {
                    var activation = _ribbon.ActivateCommandAsync(id); await SettleRibbonAsync();
                    var dialog = _dialogs.OfType<NeraFormatCellsDialog>().Single(); await SettleDialog(dialog);
                    Check($"{theme}-{name}-modal", dialog.IsVisible && dialog.Owner == this && dialog.SelectedFormatTab == tab);
                    Check($"{theme}-{name}-clean", !dialog.HasPendingChanges && Session.History.UndoCount == history && sheet.Version == version);
                    CaptureRibbonScene(dialog, theme + "-" + name, 1, directory, captures);
                    if (tab == NeraFormatCellsTab.Number)
                    {
                        var tabs = dialog.GetVisualDescendants().OfType<TabControl>().Single();
                        foreach (var (index, extra) in new[] { (3, "border"), (4, "fill") })
                        {
                            tabs.SelectedIndex = index; await SettleDialog(dialog);
                            Check($"{theme}-{extra}-tab", dialog.SelectedFormatTab == (NeraFormatCellsTab)index);
                            CaptureRibbonScene(dialog, theme + "-" + extra, 1, directory, captures);
                        }
                    }
                    DialogControl<Button>(dialog, "dialog-cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await activation;
                    Check($"{theme}-{name}-cancel", !dialog.IsVisible && !_settingsOpen && sheet.Version == version && Session.History.UndoCount == history);
                }
                foreach (var (id, tab, name) in new[] { ("Ui.Dialog.PageSetup", NeraPageSetupTab.Page, "page"), ("Ui.Dialog.PrintOptions", NeraPageSetupTab.Sheet, "sheet") })
                {
                    var activation = _ribbon.ActivateCommandAsync(id); await SettleRibbonAsync();
                    var dialog = _dialogs.OfType<NeraPageSetupDialog>().Single(); await SettleDialog(dialog);
                    Check($"{theme}-{name}-modal", dialog.IsVisible && dialog.Owner == this && dialog.SelectedPageTab == tab);
                    CaptureRibbonScene(dialog, theme + "-" + name, 1, directory, captures);
                    if (tab == NeraPageSetupTab.Page)
                    {
                        dialog.GetVisualDescendants().OfType<TabControl>().Single().SelectedIndex = 1; await SettleDialog(dialog);
                        Check($"{theme}-margins-tab", dialog.SelectedPageTab == NeraPageSetupTab.Margins);
                        CaptureRibbonScene(dialog, theme + "-margins", 1, directory, captures);
                    }
                    DialogControl<Button>(dialog, "dialog-cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await activation;
                    Check($"{theme}-{name}-cancel", !_settingsOpen && Session.History.UndoCount == history);
                }
                var zoomActivation = _ribbon.ActivateCommandAsync("Ui.Dialog.Zoom"); await SettleRibbonAsync();
                var zoom = _dialogs.OfType<NeraZoomDialog>().Single(); await SettleDialog(zoom);
                Check($"{theme}-zoom-modal", zoom.IsVisible && zoom.Owner == this);
                CaptureRibbonScene(zoom, theme + "-zoom", 1, directory, captures);
                DialogControl<Button>(zoom, "dialog-cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await zoomActivation;
            }

            await CaptureDialogVisual010Async(directory, captures, checks, sheet, version, history);

            SetRibbonTheme(NeraIconTheme.Light);
            var editActivation = _ribbon.ActivateCommandAsync("Ui.Dialog.Number"); await SettleRibbonAsync();
            var edit = _dialogs.OfType<NeraFormatCellsDialog>().Single(); await SettleDialog(edit);
            var code = DialogControl<TextBox>(edit, "format-code"); code.Text = "\"invalid";
            DialogControl<Button>(edit, "dialog-ok").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("invalid-keeps-open", edit.IsVisible && DialogControl<TextBlock>(edit, "dialog-validation").IsVisible);
            Check("invalid-keeps-history", Session.History.UndoCount == history && sheet.Version == version);
            code.Text = "#,##0.000"; await SettleDialog(edit);
            Check("actual-preview", DialogControl<TextBlock>(edit, "format-preview").Text?.Contains("1.234,500", StringComparison.Ordinal) == true);
            DialogControl<Button>(edit, "dialog-ok").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await editActivation;
            Check("one-format-transaction", Session.History.UndoCount == history + 1 && Session.Styles.ActiveCellStyle.NumberFormat.FormatCode == "#,##0.000");
            Check("raw-number-retained", Equals(sheet.GetValue(target), 1234.5));
            Check("undo-format", Session.Undo() && Session.Styles.ActiveCellStyle.NumberFormat.FormatCode == "General");
            Check("redo-format", Session.Redo() && Session.Styles.ActiveCellStyle.NumberFormat.FormatCode == "#,##0.000");
            var staleActivation = _ribbon.ActivateCommandAsync("Ui.Dialog.Number"); await SettleRibbonAsync();
            var stale = _dialogs.OfType<NeraFormatCellsDialog>().Single(); await SettleDialog(stale);
            var priorHistory = Session.History.UndoCount;
            DialogControl<TextBox>(stale, "format-code").Text = "0%"; Session.Selection.SetActiveCell(new CellAddress(9, 8));
            DialogControl<Button>(stale, "dialog-ok").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check("stale-target-refused", stale.IsVisible && Session.History.UndoCount == priorHistory);
            DialogControl<Button>(stale, "dialog-cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await staleActivation;
            Check("all-dialogs-closed", !_settingsOpen && !_dialogs.OfType<NeraSettingsDialog>().Any());
            Check("all-captures", captures.Count == 85 && captures.Select(capture => capture.name).Distinct(StringComparer.Ordinal).Count() == 85);
            var report = new
            {
                schema = "nera.ribbon.dialogs.v2",
                sha,
                nativeWindow = true,
                physicalInputTested = false,
                monitorDpiSwitchTested = false,
                rasterScaleIsNotMonitorDpi = true,
                liveRenderScaling = RenderScaling,
                assertions = checks.Count,
                checks,
                captures,
                assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(NeraFormatCellsDialog).Assembly.Location))).ToLowerInvariant(),
            };
            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(report, RibbonVisualJson));
            Console.WriteLine("NERA_AVALONIA_DIALOGS_SUCCESS " + JsonSerializer.Serialize(new
            {
                sha,
                nativeWindow = true,
                physicalInputTested = false,
                monitorDpiSwitchTested = false,
                assertions = checks.Count,
                captures = captures.Count,
                liveRenderScaling = RenderScaling,
            }));
            lifetime.Shutdown(0);
        }
        catch (Exception exception) { Console.Error.WriteLine("NERA_AVALONIA_DIALOGS_FAILURE " + exception); lifetime.Shutdown(1); }
    }
    private static T DialogControl<T>(Window dialog, string id) where T : Control => dialog.GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == id);
    private static async Task SettleDialog(Window dialog)
    {
        dialog.UpdateLayout(); await Dispatcher.UIThread.InvokeAsync(dialog.UpdateLayout, DispatcherPriority.Background);
        var ok = DialogControl<Button>(dialog, "dialog-ok"); var client = (Control)dialog.Content!;
        var position = ok.TranslatePoint(default, client) ?? throw new InvalidOperationException("Detached dialog button.");
        if (position.X < -1 || position.Y < -1 || position.X + ok.Bounds.Width > client.Bounds.Width + 1 || position.Y + ok.Bounds.Height > client.Bounds.Height + 1)
            throw new InvalidOperationException("Dialog confirmation button is clipped.");
    }
}
