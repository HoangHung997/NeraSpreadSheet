using Microsoft.UI.Xaml.Media.Imaging;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Maui;
using NativeColor = global::Windows.UI.Color;
using SolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;

namespace NeraSpreadSheet.Maui.Windows.RibbonSmoke;

internal sealed partial class SmokePage
{
    private static (NativeColor Surface, NativeColor Border, NativeColor Selected, NativeColor Accent, NativeColor TextSelection) CustomizationColors(NeraIconTheme theme) => theme switch
    {
        NeraIconTheme.HighContrastDark => (Rgb(0, 0, 0), Rgb(255, 255, 255), Rgb(58, 58, 58), Rgb(255, 239, 0), Rgb(85, 85, 85)),
        NeraIconTheme.HighContrastLight => (Rgb(255, 255, 255), Rgb(0, 0, 0), Rgb(217, 229, 255), Rgb(0, 53, 178), Rgb(0, 53, 178)),
        NeraIconTheme.Dark => (Rgb(37, 37, 37), Rgb(73, 73, 73), Rgb(53, 77, 64), Rgb(105, 213, 160), Rgb(73, 96, 82)),
        _ => (Rgb(255, 255, 255), Rgb(222, 227, 224), Rgb(221, 239, 228), Rgb(24, 115, 74), Rgb(24, 115, 74)),
    };

    private static NativeColor Rgb(byte red, byte green, byte blue) => NativeColor.FromArgb(255, red, green, blue);

    private static async Task VerifyCustomizationInputPaletteAsync(NeraMauiRibbonCustomizationView shell, NeraIconTheme theme)
    {
        var colors = CustomizationColors(theme);
        foreach (var control in Descendants<VisualElement>(shell).Where(static view => view is Entry or Editor or Picker))
        {
            var native = (Microsoft.UI.Xaml.Controls.Control)control.Handler!.PlatformView!;
            Require(native.BorderBrush is SolidColorBrush border && border.Color == colors.Border,
                $"{theme} {control.AutomationId} did not receive its scoped native border.");
            Require(native.FocusVisualPrimaryBrush is SolidColorBrush focus && focus.Color == colors.Accent,
                $"{theme} {control.AutomationId} did not receive its scoped focus accent.");
            if (native is Microsoft.UI.Xaml.Controls.TextBox text)
                Require(text.SelectionHighlightColor is SolidColorBrush selection && selection.Color == colors.TextSelection,
                    $"{theme} text selection did not receive its contrasting native fill.");
        }

        if (theme is not (NeraIconTheme.HighContrastDark or NeraIconTheme.HighContrastLight)) return;
        // Verify rendered outlines, not just ResourceDictionary entries. The focused
        // caption and the idle Picker must both remain visible against the HC surface.
        foreach (var id in new[] { "caption", "targets" })
        {
            var control = Descendants<VisualElement>(shell).Single(view => view.AutomationId == "ribbon-customization-" + id);
            var native = FindCustomizationInputBorder((Microsoft.UI.Xaml.DependencyObject)control.Handler!.PlatformView!,
                id == "caption" ? "BorderElement" : "Background");
            Require(native is { IsLoaded: true, ActualWidth: > 0d, ActualHeight: > 0d, Child: null, Opacity: > 0d },
                $"{theme} {id} has no loaded native outline visual.");
            // ComboBox's invisible HighlightBackground has Margin=-4. Rendering the
            // whole control expands the bitmap, so its outer pixels are not the outline.
            // Measure the real template Border, with no caption pixels in this target.
            var bitmap = new RenderTargetBitmap();
            await bitmap.RenderAsync(native!);
            var buffer = await bitmap.GetPixelsAsync();
            var pixels = new byte[buffer.Length];
            using (var reader = global::Windows.Storage.Streams.DataReader.FromBuffer(buffer)) reader.ReadBytes(pixels);
            var expected = id == "caption" ? colors.Accent : colors.Border;
            var matches = 0;
            var edge = Math.Max(2, (int)Math.Ceiling(3d * bitmap.PixelWidth / native!.ActualWidth));
            for (var y = 0; y < bitmap.PixelHeight; y++)
            for (var x = 0; x < bitmap.PixelWidth; x++)
                if ((x < edge || x >= bitmap.PixelWidth - edge || y < edge || y >= bitmap.PixelHeight - edge) &&
                    MatchesCustomizationPixel(pixels, ((y * bitmap.PixelWidth) + x) * 4, expected)) matches++;
            Require(matches >= bitmap.PixelWidth / 2,
                $"{theme} {id} outline has insufficient rendered contrast: matches={matches}, width={bitmap.PixelWidth}.");
        }
    }

    private static Microsoft.UI.Xaml.Controls.Border? FindCustomizationInputBorder(Microsoft.UI.Xaml.DependencyObject root, string name)
    {
        if (root is Microsoft.UI.Xaml.Controls.Border border && border.Name == name) return border;
        for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(root); index++)
            if (FindCustomizationInputBorder(Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(root, index), name) is { } found) return found;
        return null;
    }

    private static void VerifyCustomizationPopupPalette(byte[] pixels, int width, int height, NeraIconTheme theme)
    {
        var colors = CustomizationColors(theme);
        var surface = 0;
        var selected = 0;
        var accent = 0;
        for (var offset = 0; offset < pixels.Length; offset += 4)
        {
            if (MatchesCustomizationPixel(pixels, offset, colors.Surface)) surface++;
            if (MatchesCustomizationPixel(pixels, offset, colors.Selected)) selected++;
            if (MatchesCustomizationPixel(pixels, offset, colors.Accent)) accent++;
        }
        Require(surface > width * height / 3 && selected > width * 4 && accent >= 8,
            $"{theme} open Picker did not render the scoped palette: surface={surface}, selected={selected}, accent={accent}, bounds={width}x{height}.");
    }

    private static bool MatchesCustomizationPixel(byte[] pixels, int offset, NativeColor color) => pixels[offset + 3] > 240 &&
        Math.Abs(pixels[offset] - color.B) <= 3 && Math.Abs(pixels[offset + 1] - color.G) <= 3 && Math.Abs(pixels[offset + 2] - color.R) <= 3;
}
