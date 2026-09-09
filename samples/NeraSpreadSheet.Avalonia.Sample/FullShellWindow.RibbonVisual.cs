using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media.Imaging;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    private static readonly int[] RibbonVisualWidths = [820, 1024, 1536];
    private static readonly double[] RibbonRasterScales = [1, 1.25, 1.5, 2];
    private static readonly JsonSerializerOptions RibbonVisualJson = new() { WriteIndented = true };

    /// <summary>Native attached-control geometry and raster evidence, not a pixel-perfect
    /// Excel oracle, physical-input claim, or simulation of changing monitor DPI.</summary>
    internal async void StartRibbonVisualSmoke(IClassicDesktopStyleApplicationLifetime lifetime)
    {
        var checks = new List<string>();
        var captures = new List<RibbonCapture>();
        var layouts = new List<RibbonGeometry>();
        void Check(string id, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Ribbon visual check failed: " + id);
            if (checks.Contains(id)) throw new InvalidOperationException("Duplicate Ribbon check: " + id);
            checks.Add(id);
        }
        try
        {
            var sha = Environment.GetEnvironmentVariable("NERA_SOURCE_SHA");
            Check("exact-source", sha is { Length: 40 } && sha.All(Uri.IsHexDigit));
            await SettleRibbonAsync();
            Check("native-window", IsVisible && _split.ActiveSpreadsheet.RenderedFrameCount > 0);
            var missingIcons = new List<string>();
            foreach (var id in _registry.RegisteredCommandIds)
            {
                if (!_registry.TryResolve(id, out var descriptor, out _) || descriptor is null)
                    throw new InvalidOperationException("Missing sample command: " + id);
                if (descriptor.IconKey is { } key && !NeraIconCatalog.TryGetDescriptor(key, out _))
                    missingIcons.Add(id.Value + "=" + key);
            }
            if (missingIcons.Count > 0) throw new InvalidOperationException("Sample command icons are missing: " + string.Join(", ", missingIcons));
            Check("all-sample-icon-keys-resolve", missingIcons.Count == 0);
            var sheet = Session.ActiveWorksheet;
            var version = sheet.Version;
            var selectionVersion = Session.Selection.Capture().Version;
            var executions = _commandExecutions;
            var originalCustomization = _runtime.Customization;
            var tabs = _runtime.Snapshot.Tabs.Select(tab => tab.Id).ToArray();
            Check("seven-real-tabs", tabs.Length == 7);
            var directory = Path.Combine(Environment.GetEnvironmentVariable("NERA_AVALONIA_ARTIFACTS") ?? "artifacts/avalonia", "ribbon-visual");
            Directory.CreateDirectory(directory);
            foreach (var theme in Enum.GetValues<NeraIconTheme>())
            {
                SetRibbonTheme(theme);
                foreach (var width in RibbonVisualWidths)
                {
                    Width = width;
                    _ribbon.Width = width;
                    foreach (var tab in tabs)
                    {
                        Check($"select-{theme}-{width}-{tab}", _ribbon.SelectTab(tab));
                        await SettleRibbonAsync();
                        var geometry = VerifyRibbonGeometry(theme, width, tab);
                        layouts.Add(geometry);
                        Check($"geometry-{theme}-{width}-{tab}", geometry.items > 0);
                        CaptureRibbonScene(_ribbon, $"{theme}-{width}-{tab}", 1, directory, captures);
                    }
                }
                Width = 1280; _ribbon.Width = 1280; _ribbon.SelectTab("home");
                await SettleRibbonAsync();
                var combo = FindRibbonControl<ComboBox>("ribbon-command-Ui.FontFamily");
                combo.IsDropDownOpen = true;
                await SettleRibbonAsync();
                var popup = combo.GetVisualDescendants().OfType<Popup>().FirstOrDefault(part => part.Name == "PART_Popup");
                Check($"combo-open-{theme}", combo.IsDropDownOpen && popup?.Child is Control { Bounds.Width: > 0 });
                CaptureRibbonScene(popup!.Child!, $"{theme}-font-popup", 1, directory, captures);
                combo.IsDropDownOpen = false;

                var bold = FindRibbonControl<ToggleButton>("ribbon-command-Cell.Bold");
                var wasEnabled = bold.IsEnabled;
                bold.IsEnabled = false;
                CaptureRibbonScene(_ribbon, $"{theme}-disabled", 1, directory, captures);
                bold.IsEnabled = wasEnabled;
                bold.Focus(NavigationMethod.Tab);
                Check($"focus-{theme}", bold.IsFocused);
                CaptureRibbonScene(_ribbon, $"{theme}-keyboard-focus", 1, directory, captures);

                _ribbon.OpenBackstage(); await SettleRibbonAsync();
                Check($"backstage-{theme}", _ribbon.IsBackstageOpen && _ribbon.NativeTabControl.Items.OfType<TabItem>().All(tab => tab.Content is null));
                CaptureRibbonScene(_ribbon, $"{theme}-backstage", 1, directory, captures);
                _ribbon.CloseBackstage(); await SettleRibbonAsync();

                var dialogCount = _dialogs.Count;
                FindRibbonControl<Button>("ribbon-customize").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check($"customization-open-{theme}", _dialogs.Count == dialogCount + 1);
                var dialog = _dialogs.Last();
                var editor = dialog.Content as NeraRibbonCustomizationControl ?? throw new InvalidOperationException("Customization command opened the wrong content.");
                try
                {
                    await Dispatcher.UIThread.InvokeAsync(dialog.UpdateLayout, DispatcherPriority.Background);
                    Check($"customization-no-edit-{theme}", editor.IconTheme == theme && !editor.HasChanges && ReferenceEquals(editor.Apply(), originalCustomization));
                    // Apply refreshes both ItemsSources. Let native item containers
                    // realize again before the evidence capture, not just the catalog.
                    await Dispatcher.UIThread.InvokeAsync(dialog.UpdateLayout, DispatcherPriority.Background);
                    var lists = dialog.GetVisualDescendants().OfType<ListBox>().ToArray();
                    var structure = lists.Single(list => AutomationProperties.GetName(list) == "Cấu trúc Ribbon");
                    var qat = lists.Single(list => AutomationProperties.GetName(list) == "Thanh truy cập nhanh");
                    Check($"customization-items-rendered-{theme}", structure.ItemCount > 0 && qat.ItemCount > 0 &&
                        structure.GetVisualDescendants().OfType<TextBlock>().Any(text => !string.IsNullOrWhiteSpace(text.Text)) &&
                        qat.GetVisualDescendants().OfType<TextBlock>().Any(text => !string.IsNullOrWhiteSpace(text.Text)));
                    CaptureRibbonScene(dialog, $"{theme}-customization", 1, directory, captures);
                }
                finally { dialog.Close(); }
            }
            SetRibbonTheme(NeraIconTheme.Light);
            Width = 1280; _ribbon.Width = 1280; _ribbon.SelectTab("home"); await SettleRibbonAsync();
            foreach (var scale in RibbonRasterScales)
                CaptureRibbonScene(_ribbon, "Light-home-raster-" + scale.ToString("0.##", CultureInfo.InvariantCulture), scale, directory, captures);
            // Matrix widths are deliberate off-screen test constraints. Release
            // them before capturing the real client: a runner can clamp Window.Width
            // to its display, otherwise an oversized Ribbon is centred and cropped.
            _ribbon.ClearValue(WidthProperty);
            await SettleRibbonAsync();
            var client = Content as Control ?? throw new InvalidOperationException("Missing shell client.");
            var ribbonOrigin = _ribbon.TranslatePoint(default, client)
                ?? throw new InvalidOperationException("Detached Ribbon before client capture.");
            Check("full-window-ribbon-contained", ribbonOrigin.X >= -0.6 &&
                ribbonOrigin.X + _ribbon.Bounds.Width <= client.Bounds.Width + 0.6);
            CaptureRibbonScene(this, "full-window", 1, directory, captures);
            Check("no-workbook-mutation", sheet.Version == version && ReferenceEquals(sheet, Session.ActiveWorksheet));
            Check("no-selection-mutation", Session.Selection.Capture().Version == selectionVersion);
            Check("no-command-execution", _commandExecutions == executions);
            Check("profile-unchanged", ReferenceEquals(_runtime.Customization, originalCustomization));
            Check("layout-matrix-complete", layouts.Count == 84);
            Check("captures-complete", captures.Count == 109 && captures.Select(item => item.name).Distinct(StringComparer.Ordinal).Count() == captures.Count);
            var report = new
            {
                schema = "nera.ribbon.visual.v1", sha, assertions = checks.Count, checks,
                nativeWindow = true, physicalInputTested = false,
                monitorDpiSwitchTested = false, liveRenderScaling = RenderScaling,
                rasterScaleIsNotMonitorDpi = true, layouts, captures,
                assemblySha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(NeraRibbonControl).Assembly.Location))).ToLowerInvariant(),
            };
            File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonSerializer.Serialize(report, RibbonVisualJson));
            Console.WriteLine("NERA_AVALONIA_RIBBON_VISUAL_SUCCESS " + JsonSerializer.Serialize(new
            {
                sha, assertions = checks.Count, nativeWindow = true, physicalInputTested = false,
                layouts = layouts.Count, captures = captures.Count, liveRenderScaling = RenderScaling,
            }));
            lifetime.Shutdown(0);
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("NERA_AVALONIA_RIBBON_VISUAL_FAILURE " + exception);
            lifetime.Shutdown(1);
        }
    }

    private async Task SettleRibbonAsync()
    {
        // Drain pending resize projections without rebuilding a live popup's owner.
        UpdateLayout();
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Background);
        await Dispatcher.UIThread.InvokeAsync(UpdateLayout, DispatcherPriority.Render);
    }

    private T FindRibbonControl<T>(string id) where T : Control => _ribbon.GetVisualDescendants()
        .OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == id);

    private RibbonGeometry VerifyRibbonGeometry(NeraIconTheme theme, int width, string tabId)
    {
        var nativeTabs = _ribbon.NativeTabControl.Items.OfType<TabItem>().ToArray();
        Require(nativeTabs.Count(tab => tab.Content is not null) == 1, "exactly one attached command body");
        Require(nativeTabs.All(tab => tab.FontSize == 13 && tab.Bounds.Height is >= 30 and <= 38), "tab typography/density");
        Require(Math.Abs(_ribbon.Bounds.Width - width) <= 1, "requested logical width");
        Require(_ribbon.Bounds.Height <= 170, "Ribbon vertical density");
        var snapshot = _ribbon.LayoutSnapshot;
        Require(snapshot.SelectedTabId == tabId, "selected-tab identity");
        var layout = snapshot.Tabs.Single(tab => tab.Presentation.Id == tabId);
        Require(layout.InlineWidth <= snapshot.AvailableWidth + 1, "shared inline width");
        var itemCount = 0;
        var iconCount = 0;
        var groupBounds = new List<Rect>();
        foreach (var group in layout.Groups.Where(group => group.Mode != RibbonGroupLayoutMode.Overflow))
        {
            var nativeGroup = FindRibbonControl<Border>("ribbon-group-" + group.Presentation.Id);
            Require(Math.Abs(nativeGroup.Bounds.Width - group.Width / snapshot.Scale) <= 0.6, "group width " + group.Presentation.Id);
            var groupOrigin = nativeGroup.TranslatePoint(default, _ribbon) ?? throw new InvalidOperationException("Detached group.");
            var bounds = new Rect(groupOrigin, nativeGroup.Bounds.Size);
            Require(bounds.Left >= -0.6 && bounds.Right <= _ribbon.Bounds.Width + 0.6, "group clipping");
            Require(groupBounds.All(previous => previous.Intersect(bounds).Width <= 0.6), "group overlap");
            groupBounds.Add(bounds);
            foreach (var item in group.Items)
            {
                if (item.Presentation.Kind == RibbonItemKind.Separator) continue;
                var id = item.Presentation.Command.CommandId.Value;
                var control = FindRibbonControl<Control>("ribbon-command-" + id);
                var origin = control.TranslatePoint(default, nativeGroup) ?? throw new InvalidOperationException("Detached command.");
                Require(Math.Abs(origin.X - item.X / snapshot.Scale) <= 0.6 && Math.Abs(origin.Y - item.Y / snapshot.Scale) <= 0.6, "command position " + id);
                Require(Math.Abs(control.Bounds.Width - item.Width / snapshot.Scale) <= 0.6 && Math.Abs(control.Bounds.Height - item.Height / snapshot.Scale) <= 0.6, "command bounds " + id);
                Require(origin.Y + control.Bounds.Height <= group.CaptionY / snapshot.Scale + 0.6, "caption area intrusion " + id);
                if (item.Presentation.Command.IconKey is { } key)
                {
                    Require(NeraIconCatalog.TryGetDescriptor(key, out _), "missing catalog icon " + key);
                    if (control is Button)
                    {
                        var images = control.GetVisualDescendants().OfType<Image>().ToArray();
                        Require(images.Any(image => image.Source is not null && image.Bounds.Width > 0 && image.Bounds.Height > 0), "missing rendered icon " + id);
                        iconCount += images.Length;
                    }
                }
                itemCount++;
            }
        }
        return new RibbonGeometry(theme.ToString(), width, tabId, _ribbon.Bounds.Height,
            groupBounds.Count, itemCount, iconCount, layout.HasOverflow, snapshot.Scale);

        static void Require(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException("Native Ribbon geometry: " + detail);
        }
    }

    private static void CaptureRibbonScene(Control control, string name, double rasterScale, string directory, List<RibbonCapture> captures)
    {
        if (control.Bounds.Width <= 0 || control.Bounds.Height <= 0) throw new InvalidOperationException("Cannot capture an unarranged visual: " + name);
        var pixels = new PixelSize((int)Math.Ceiling(control.Bounds.Width * rasterScale), (int)Math.Ceiling(control.Bounds.Height * rasterScale));
        if (pixels.Width > 4096 || pixels.Height > 4096) throw new InvalidOperationException("Capture exceeds bounded dimensions.");
        using var bitmap = new RenderTargetBitmap(pixels, new Vector(96 * rasterScale, 96 * rasterScale));
        bitmap.Render(control);
        var filename = name + ".png";
        var path = Path.Combine(directory, filename);
        using (var output = File.Create(path)) bitmap.Save(output, new PngBitmapEncoderOptions());
        using var input = File.OpenRead(path);
        captures.Add(new RibbonCapture(filename, Convert.ToHexString(SHA256.HashData(input)).ToLowerInvariant(), pixels.Width, pixels.Height, rasterScale));
    }
    private sealed record RibbonCapture(string name, string sha256, int width, int height, double rasterScale);
    private sealed record RibbonGeometry(string theme, int width, string tab, double height, int groups, int items, int icons, bool overflow, double liveScale);
}
