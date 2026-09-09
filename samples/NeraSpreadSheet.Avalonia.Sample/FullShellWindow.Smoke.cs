using System.Diagnostics;
using System.Text.Json;
using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    private static readonly string[] ProductTabIds = ["home", "insert", "page-layout", "formulas", "data", "review", "view"];
    private DispatcherTimer? _smokeTimer;
    internal void StartFullSmoke(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var elapsed = Stopwatch.StartNew();
        _smokeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _smokeTimer.Tick += async (_, _) =>
        {
            if (_split.ActiveSpreadsheet.RenderedFrameCount == 0 && elapsed.Elapsed < TimeSpan.FromSeconds(15)) return;
            _smokeTimer?.Stop();
            try
            {
                var sha = Environment.GetEnvironmentVariable("NERA_SOURCE_SHA");
                var checks = new List<string>();
                void Check(string id, bool condition)
                {
                    if (!condition) throw new InvalidOperationException("Full shell check failed: " + id);
                    checks.Add(id);
                }
                Check("exact-source", sha is { Length: 40 } && sha.All(Uri.IsHexDigit));
                Check("loaded-window", _split.ActiveSpreadsheet.RenderedFrameCount > 0);
                // The richer product preset intentionally replaces the old three-tab fixture.
                // Assert identities/order, not merely a larger count or a weaker minimum.
                Check("ribbon-projection", _ribbon.LayoutSnapshot.Tabs.Select(tab => tab.Presentation.Id).SequenceEqual(ProductTabIds));
                Check("native-menu", _menu.NativeControl is Menu);
                var workbook = new Workbook(); workbook.AddWorksheet("Khác");
                _split.Session = new SpreadsheetSession(workbook);
                Session.SetValue(default, 21d); Session.SetFormula(new CellAddress(0, 1), "=A1*2");
                _ribbon.Rebuild(); UpdateLayout();
                var before = _commandExecutions;
                var bold = _ribbon.GetVisualDescendants().OfType<Button>().Single(button => AutomationProperties.GetAutomationId(button) == "ribbon-command-Cell.Bold");
                bold.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check("native-ribbon-dispatch", _commandExecutions == before + 1);
                before = _commandExecutions;
                RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Z,
                    KeyModifiers = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control });
                Check("ribbon-bar-shortcut-once", _commandExecutions == before + 1);
                Check("os-copy", await _split.ActiveSpreadsheet.CopyToClipboardAsync());
                Session.Selection.SetActiveCell(new CellAddress(1, 0));
                Check("os-paste", await _split.ActiveSpreadsheet.PasteFromClipboardAsync());
                Check("os-paste-value", Session.ActiveWorksheet.GetCell(new CellAddress(1, 0)).Value.ToString() == "21");
                Check("os-paste-undo", Session.Undo() && Session.ActiveWorksheet.GetCell(new CellAddress(1, 0)).IsEmpty);
                Session.Selection.SetActiveCell(default);
                var sheet = _split.ActiveSpreadsheet;
                sheet.BeginEdit("=SU");
                var suggestion = sheet.CurrentFormulaSuggestions.ToList().FindIndex(item => item.Name == "SUM");
                Check("formula-suggestion", suggestion >= 0);
                Check("formula-completion", sheet.ApplyFormulaSuggestion(sheet.CurrentStructuredReferenceSuggestions.Count + suggestion) && sheet.EditorText == "=SUM(");
                Check("formula-argument-help", sheet.CurrentFormulaHelp?.Function.Name == "SUM");
                _formula.Focus();
                const string draft = "=SUM(B1:B3)";
                _updating = true;
                _formula.Text = draft; _formula.CaretIndex = draft.Length; _formula.SelectionStart = draft.Length; _formula.SelectionEnd = draft.Length;
                _updating = false; PushFormulaDraft();
                Check("formula-bar-one-draft", sheet.EditorText == draft && ReferenceEquals(sheet, _split.EditingSpreadsheet));
                Check("reference-highlights", sheet.CurrentFormulaReferenceHighlights.Count == 1);
                Check("draft-no-commit", Session.ActiveWorksheet.GetCell(default).Value.ToString() == "21");
                sheet.CancelEditor();
                _split.SetMode(SpreadsheetSplitViewMode.Both); UpdateLayout();
                Check("four-stable-panes", _split.Children.Count(control => control.IsVisible) == 4);
                _split.GetPane(SpreadsheetSplitViewPane.TopRight).ScrollTo(10.25, 20.75);
                Check("independent-fractional-scroll", _split.State.TopRightScroll.OffsetX == 10.25 && _split.State.TopLeftScroll.OffsetX == 0);
                var profileEditor = new NeraRibbonCustomizationControl(_runtime);
                var dialog = new Window { Title = "Kiểm thử tùy biến Ribbon", Width = 1040, Height = 650, Content = profileEditor };
                dialog.Show(this); dialog.UpdateLayout();
                try
                {
                    var baseline = _runtime.Customization;
                    Check("customization-no-op", ReferenceEquals(profileEditor.Apply(), baseline));
                    profileEditor.AddTab("native-test", "Tùy chỉnh"); profileEditor.AddGroup("native-test", "commands", "Lệnh");
                    profileEditor.AddCommand("View.ZoomReset", "native-test", "commands"); profileEditor.AddToQuickAccessToolbar("Edit.Copy");
                    var json = profileEditor.ExportJson(); profileEditor.Apply();
                    Check("customization-apply", _runtime.EffectiveDefinition.Tabs.Any(tab => tab.Id == "native-test"));
                    var imported = new NeraRibbonCustomizationControl(_runtime); imported.ImportJson(json);
                    Check("customization-json", imported.QuickAccessToolbar.Contains(new NeraSpreadSheet.Commands.CommandId("Edit.Copy")));
                    CaptureNative(dialog, "full-ui-customization.png");
                    _runtime.SetCustomization(baseline);
                }
                finally { dialog.Close(); }
                using (var icons = new NeraAvaloniaIconProvider())
                {
                    var request = new NeraIconRequest("file.save", 16, NeraIconTheme.Light);
                    var first = icons.Resolve(request);
                    Check("native-icon-cache", first is not null && ReferenceEquals(first, icons.Resolve(request)));
                    Check("native-icon-fallback", icons.Resolve(new NeraIconRequest("unknown-icon", 16, NeraIconTheme.Light)) is null);
                }
                _split.Session = CreateWorkbook(); _split.SetMode(SpreadsheetSplitViewMode.Vertical);
                _ribbon.SelectTab("home"); _ribbon.Rebuild(); UpdateLayout();
                await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
                CaptureNative(this, "full-ui-window.png");
                Console.WriteLine("NERA_AVALONIA_FULL_UI_SUCCESS " + JsonSerializer.Serialize(new
                {
                    sha, assertions = checks.Count, checks, osClipboard = true, nativeWindow = true, physicalInputTested = false,
                    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    assembly = typeof(NeraSpreadsheetControl).Assembly.Location,
                }));
                lifetime.Shutdown(0);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("NERA_AVALONIA_FULL_UI_FAILURE " + exception);
                lifetime.Shutdown(1);
            }
        };
        _smokeTimer.Start();
    }
    private static void CaptureNative(Window window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("NERA_AVALONIA_ARTIFACTS") ?? "artifacts/avalonia";
        Directory.CreateDirectory(directory);
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.Bounds.Width), (int)Math.Ceiling(window.Bounds.Height)), new Vector(96, 96));
        bitmap.Render(window);
        using var stream = File.Create(Path.Combine(directory, name));
        bitmap.Save(stream, new PngBitmapEncoderOptions());
    }
}
