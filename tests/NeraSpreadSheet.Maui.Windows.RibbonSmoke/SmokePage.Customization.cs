using System.Globalization;
using Microsoft.UI.Xaml.Automation.Peers;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Maui;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Maui.Windows.RibbonSmoke;

internal sealed partial class SmokePage
{
    private async Task VerifyCustomizationShellAsync()
    {
        NativePopupCapture.VerifyGeometryContract();
        var registry = new CommandRegistry();
        var handler = new ToggleHandler();
        registry.Register(new CommandDescriptor("first", "Premier"), handler);
        registry.Register(new CommandDescriptor("second", "Second"), handler);
        var definition = new RibbonDefinition([new RibbonTabDefinition("home", "Trang đầu", [
            new RibbonGroupDefinition("tools", "Công cụ", [new RibbonItemDefinition("first"), new RibbonItemDefinition("second")])]) { CaptionResourceKey = "Trang đầu" }]);
        var runtime = new RibbonRuntimeController(definition, registry);
        var origin = new Button { Text = "Synthetic focus origin", AutomationId = "ux007-origin" };
        using var shell = new NeraMauiRibbonCustomizationView(runtime, focusOrigin: origin);
        var previousContent = Content;
        var previousWidth = Window.Width;
        var previousHeight = Window.Height;
        var stage = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        stage.Add(shell);
        stage.Add(origin, 0, 1);
        Content = stage;
        Window.Width = 980d;
        Window.Height = 820d;
        try
        {
            await Task.Delay(250).ConfigureAwait(true);
            Button Action(string id) => Descendants<Button>(shell).Single(button => button.AutomationId == "ribbon-customization-" + id);
            Picker Picker(string id) => Descendants<Picker>(shell).Single(picker => picker.AutomationId == "ribbon-customization-" + id);
            void Click(string id) => ((IButtonController)Action(id)).SendClicked();
            void Select(RibbonCustomizationTarget target) => Picker("targets").SelectedIndex = shell.Binding.Entries.ToList().FindIndex(entry => entry.Target == target);
            var caption = Descendants<Entry>(shell).Single(entry => entry.AutomationId == "ribbon-customization-caption");
            Require(!shell.IsNarrow && shell.Width > 720d, "Full native window did not show two customization panels.");
            foreach (var button in Descendants<Button>(shell))
            {
                Require(button.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Button, "Customization action has no native Button.");
                var peer = FrameworkElementAutomationPeer.CreatePeerForElement((Microsoft.UI.Xaml.FrameworkElement)button.Handler!.PlatformView!);
                Require(peer.GetAutomationControlType() == AutomationControlType.Button && !string.IsNullOrWhiteSpace(peer.GetName()),
                    "Customization action lacks a native role or accessible name.");
            }
            caption.Text = "Cá nhân";
            Click("add-tab");
            var customTab = shell.Binding.Entries.Single(static entry => entry.IsCustom && entry.Target.Kind == RibbonCustomizationTargetKind.Tab);
            Select(customTab.Target);
            caption.Text = "Thao tác";
            Click("add-group");
            var customGroup = shell.Binding.Entries.Single(static entry => entry.IsCustom && entry.Target.Kind == RibbonCustomizationTargetKind.Group);
            Picker("destination").SelectedIndex = shell.Binding.Entries.Where(static entry => entry.Target.Kind == RibbonCustomizationTargetKind.Group).ToList().FindIndex(entry => entry.Target == customGroup.Target);
            Picker("catalog").SelectedIndex = 0;
            Click("add-command");
            Require(runtime.Snapshot.Tabs.Single(tab => tab.Id == customTab.Target.TabId).Groups[0].Items.Count == 1,
                "Native add-command action did not preview the shared placement.");
            Click("qat-add");
            Picker("catalog").SelectedIndex = 1;
            Click("qat-add");
            Picker("qat").SelectedIndex = 1;
            Click("qat-up");
            Require(shell.Binding.QuickAccessToolbar.SequenceEqual(new CommandId[] { "second", "first" }), "QAT order did not change.");
            Click("apply");
            var applied = shell.ExportJson();
            Click("qat-remove");
            Require(runtime.Snapshot.QuickAccessToolbar.Count == 1, "QAT remove did not preview.");
            Click("cancel");
            Require(shell.ExportJson() == applied && runtime.Snapshot.QuickAccessToolbar.Count == 2, "Cancel did not restore the last Apply.");
            shell.LoadJson(applied);
            Require(shell.ExportJson() == applied, "Persisted profile changed during load/export.");

            // Adjacent palettes share RequestedTheme; resource updates cannot rely on a
            // Light/Dark toggle to invalidate an already loaded native control template.
            foreach (var theme in new[] { NeraIconTheme.Light, NeraIconTheme.HighContrastLight,
                NeraIconTheme.Dark, NeraIconTheme.HighContrastDark })
            {
                caption.Text = "Bản nháp chưa áp dụng";
                caption.Focus();
                caption.CursorPosition = 3;
                caption.SelectionLength = 2;
                await Task.Delay(50).ConfigureAwait(true);
                Require(caption.CursorPosition == 3 && caption.SelectionLength == 2,
                    $"Draft setup lost selection before presentation: cursor={caption.CursorPosition}, selection={caption.SelectionLength}.");
                runtime.SetLocalization(theme is NeraIconTheme.Light or NeraIconTheme.Dark
                    ? PresentationLocalization.Default : new PresentationLocalization(CultureInfo.GetCultureInfo("en-GB")));
                shell.SetPresentation(theme);
                await Task.Delay(100).ConfigureAwait(true);
                Require(caption.Text == "Bản nháp chưa áp dụng" && caption.CursorPosition == 3 && caption.SelectionLength == 2,
                    $"Theme/localization must preserve the pending caption draft and caret: theme={theme}, text={caption.Text}, cursor={caption.CursorPosition}, selection={caption.SelectionLength}.");
                await CaptureCustomizationAsync(shell, $"ux007-customization-{theme}.png").ConfigureAwait(true);
                await VerifyCustomizationInputPaletteAsync(shell, theme).ConfigureAwait(true);
                var picker = Picker("targets");
                var native = (Microsoft.UI.Xaml.Controls.ComboBox)picker.Handler!.PlatformView!;
                Require(native.RequestedTheme == (theme is NeraIconTheme.Dark or NeraIconTheme.HighContrastDark
                    ? Microsoft.UI.Xaml.ElementTheme.Dark : Microsoft.UI.Xaml.ElementTheme.Light), "Native Picker theme mismatch.");
                var index = native.SelectedIndex;
                native.Focus(Microsoft.UI.Xaml.FocusState.Keyboard);
                native.IsDropDownOpen = true;
                await Task.Delay(500).ConfigureAwait(true);
                Require(native.IsDropDownOpen && native.SelectedIndex == index, "Opening Picker changed selection.");
                var containers = Enumerable.Range(0, native.Items.Count)
                    .Select(item => native.ContainerFromIndex(item) as Microsoft.UI.Xaml.Controls.ComboBoxItem).ToArray();
                Require(containers.Length == shell.Binding.Entries.Count && containers.All(static item =>
                    item is { IsLoaded: true, ActualWidth: > 0d, ActualHeight: > 0d }),
                    "The synthetic target Picker must realize every entry before capture.");
                // A caption tooltip can also be open. Select by the actual item containers,
                // not popup enumeration order, which can capture only that tooltip.
                var popup = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetOpenPopupsForXamlRoot(native.XamlRoot)
                    .SingleOrDefault(candidate => candidate.IsOpen && candidate.Child is not null &&
                        containers.All(item => ContainsNativeCustomizationVisual(candidate.Child, item!)));
                Require(popup?.Child is Microsoft.UI.Xaml.FrameworkElement, "Open Picker has no native popup visual.");
                var popupVisual = (Microsoft.UI.Xaml.FrameworkElement)popup!.Child!;
                var popupPixels = NativePopupCapture.Capture(popupVisual, WinRT.Interop.WindowNative.GetWindowHandle(Window.Handler!.PlatformView!), native, containers);
                await SaveCustomizationPixelsAsync(popupPixels.Pixels, popupPixels.Width, popupPixels.Height, $"ux007-picker-{theme}.png").ConfigureAwait(true);
                foreach (var item in containers) VerifyCapturedCustomizationText(popupVisual, item!, popupPixels.Pixels, popupPixels.Width, popupPixels.Height);
                VerifyCustomizationPopupPalette(popupPixels.Pixels, popupPixels.Width, popupPixels.Height, theme);
                native.IsDropDownOpen = false;
                Require(native.SelectedIndex == index && shell.ExportJson() == applied, "Picker open/close changed profile.");
            }

            Window.Width = 420d;
            Window.Height = 760d;
            await Task.Delay(180).ConfigureAwait(true);
            Require(shell.IsNarrow && shell.Width < 720d, "Narrow native window did not stack the customization panels.");
            Require(Picker("targets").Width <= shell.Width - 24d + 1d,
                $"Narrow target Picker must fit: picker={Picker("targets").Width}; shell={shell.Width}.");
            var narrowPicker = (Microsoft.UI.Xaml.Controls.ComboBox)Picker("targets").Handler!.PlatformView!;
            Require(narrowPicker.ActualWidth <= shell.Width - 24d + 1d,
                $"Narrow native target Picker must fit: native={narrowPicker.ActualWidth}; shell={shell.Width}.");
            Require(Action("apply").Width > 0 && Action("cancel").Width > 0, "Narrow shell lost its transaction actions.");
            await CaptureCustomizationAsync(shell, "ux007-customization-narrow.png").ConfigureAwait(true);
            Require(shell.ExportJson() == applied, "Resize/localization/theme changed persisted identity.");
            var captures = Enum.GetValues<NeraIconTheme>().SelectMany(static theme => new[]
            {
                $"ux007-customization-{theme}.png", $"ux007-picker-{theme}.png",
            }).Append("ux007-customization-narrow.png").ToArray();
            Require(captures.Length == 9 && captures.All(name => new FileInfo(Path.Combine(AppContext.BaseDirectory, name)) is { Exists: true, Length: > 100 }),
                "The native shell and open Picker matrix must produce all nine nonempty captures.");
        }
        finally
        {
            Content = previousContent;
            Window.Width = previousWidth;
            Window.Height = previousHeight;
        }
    }

    private static Task CaptureCustomizationAsync(VisualElement view, string fileName) =>
        CaptureNativeCustomizationAsync((Microsoft.UI.Xaml.FrameworkElement)view.Handler!.PlatformView!, fileName);

    private static bool ContainsNativeCustomizationVisual(Microsoft.UI.Xaml.DependencyObject root,
        Microsoft.UI.Xaml.DependencyObject target)
    {
        if (ReferenceEquals(root, target)) return true;
        for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); index++)
            if (ContainsNativeCustomizationVisual(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index), target)) return true;
        return false;
    }

    private static async Task CaptureNativeCustomizationAsync(Microsoft.UI.Xaml.FrameworkElement native, string fileName)
    {
        Require(native.IsLoaded && native.ActualWidth > 0 && native.ActualHeight > 0, "Capture target has no loaded bounds.");
        var bitmap = new Microsoft.UI.Xaml.Media.Imaging.RenderTargetBitmap();
        await bitmap.RenderAsync(native);
        Require(bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0, "Customization capture returned no pixels.");
        var buffer = await bitmap.GetPixelsAsync();
        var pixels = new byte[buffer.Length];
        using (var reader = global::Windows.Storage.Streams.DataReader.FromBuffer(buffer)) reader.ReadBytes(pixels);
        await SaveCustomizationPixelsAsync(pixels, bitmap.PixelWidth, bitmap.PixelHeight, fileName).ConfigureAwait(true);
    }

    private static async Task SaveCustomizationPixelsAsync(byte[] pixels, int width, int height, string fileName)
    {
        var directory = await global::Windows.Storage.StorageFolder.GetFolderFromPathAsync(AppContext.BaseDirectory);
        var file = await directory.CreateFileAsync(fileName, global::Windows.Storage.CreationCollisionOption.ReplaceExisting);
        using var stream = await file.OpenAsync(global::Windows.Storage.FileAccessMode.ReadWrite);
        var encoder = await global::Windows.Graphics.Imaging.BitmapEncoder.CreateAsync(global::Windows.Graphics.Imaging.BitmapEncoder.PngEncoderId, stream);
        encoder.SetPixelData(global::Windows.Graphics.Imaging.BitmapPixelFormat.Bgra8, global::Windows.Graphics.Imaging.BitmapAlphaMode.Premultiplied,
            (uint)width, (uint)height, 96d, 96d, pixels);
        await encoder.FlushAsync();
    }

    private static void VerifyCapturedCustomizationText(Microsoft.UI.Xaml.FrameworkElement root,
        Microsoft.UI.Xaml.DependencyObject item, byte[] pixels, int width, int height)
    {
        var text = FindNativeCustomizationText(item);
        Require(text is { ActualWidth: > 0d, ActualHeight: > 0d } &&
            text.Foreground is Microsoft.UI.Xaml.Media.SolidColorBrush, "A realized Picker item has no measurable caption brush.");
        var color = ((Microsoft.UI.Xaml.Media.SolidColorBrush)text!.Foreground).Color;
        var bounds = text.TransformToVisual(root).TransformBounds(new global::Windows.Foundation.Rect(0d, 0d, text.ActualWidth, text.ActualHeight));
        var scaleX = width / root.ActualWidth;
        var scaleY = height / root.ActualHeight;
        var left = Math.Clamp((int)Math.Ceiling(bounds.Left * scaleX), 0, width);
        var right = Math.Clamp((int)Math.Floor(bounds.Right * scaleX), 0, width);
        var top = Math.Clamp((int)Math.Ceiling(bounds.Top * scaleY), 0, height);
        var bottom = Math.Clamp((int)Math.Floor(bounds.Bottom * scaleY), 0, height);
        Require(right > left && bottom > top, $"Picker caption has no raster bounds: '{text.Text}', bounds={bounds}.");

        // ClearType/grayscale anti-aliasing can leave no pixel within a small RGB
        // distance of the logical foreground brush even when the glyph is visibly
        // rendered. Estimate the local background from the four corners of the
        // TextBlock raster rectangle and require a meaningful population of opaque
        // pixels that departs from that background in the direction of the declared
        // foreground. This keeps the test tied to the actual caption bounds and
        // foreground while allowing renderer/runner anti-aliasing differences.
        var corners = new[] { (left, top), (right - 1, top), (left, bottom - 1), (right - 1, bottom - 1) };
        var backgroundB = 0d;
        var backgroundG = 0d;
        var backgroundR = 0d;
        foreach (var (x, y) in corners)
        {
            var offset = ((y * width) + x) * 4;
            backgroundB += pixels[offset];
            backgroundG += pixels[offset + 1];
            backgroundR += pixels[offset + 2];
        }
        backgroundB /= corners.Length;
        backgroundG /= corners.Length;
        backgroundR /= corners.Length;

        var matches = 0;
        var strongestContrast = 0d;
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            var offset = ((y * width) + x) * 4;
            if (pixels[offset + 3] <= 200) continue;
            var db = pixels[offset] - backgroundB;
            var dg = pixels[offset + 1] - backgroundG;
            var dr = pixels[offset + 2] - backgroundR;
            var contrast = Math.Sqrt((db * db) + (dg * dg) + (dr * dr));
            strongestContrast = Math.Max(strongestContrast, contrast);
            if (contrast < 12d) continue;

            var foregroundDistance = Math.Sqrt(
                Math.Pow(pixels[offset] - color.B, 2d) +
                Math.Pow(pixels[offset + 1] - color.G, 2d) +
                Math.Pow(pixels[offset + 2] - color.R, 2d));
            var backgroundDistance = contrast;
            if (foregroundDistance <= backgroundDistance + 48d) matches++;
        }
        Require(matches >= 8, $"The open Picker capture omitted caption pixels for '{text.Text}': matches={matches}, strongestContrast={strongestContrast:F1}, bounds={bounds}, root={root.ActualWidth}x{root.ActualHeight}, bitmap={width}x{height}, foreground={color}, background=({backgroundR:F0},{backgroundG:F0},{backgroundB:F0}).");
    }

    private static Microsoft.UI.Xaml.Controls.TextBlock? FindNativeCustomizationText(Microsoft.UI.Xaml.DependencyObject root)
    {
        if (root is Microsoft.UI.Xaml.Controls.TextBlock { Text.Length: > 0 } text) return text;
        for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); index++)
            if (FindNativeCustomizationText(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index)) is { } found) return found;
        return null;
    }
}
