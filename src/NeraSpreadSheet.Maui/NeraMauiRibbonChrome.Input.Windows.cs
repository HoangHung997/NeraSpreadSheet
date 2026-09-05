#if WINDOWS
using System.Runtime.CompilerServices;
using Microsoft.Maui.Controls;
using NativeControl = Microsoft.UI.Xaml.Controls.Control;
using SolidColorBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;

namespace NeraSpreadSheet.Maui;

internal static partial class NeraMauiRibbonChrome
{
    private static readonly BindableProperty InputPaletteProperty = BindableProperty.CreateAttached(
        "InputPalette", typeof(NeraMauiRibbonPalette), typeof(NeraMauiRibbonChrome), null,
        propertyChanged: static (target, _, _) => ConfigureNativeInputPalette(target, EventArgs.Empty));

    private static readonly ConditionalWeakTable<NativeControl, NativeInputResources> InputResources = new();

    private sealed class NativeInputResources
    {
        internal Microsoft.UI.Xaml.ResourceDictionary Dictionary { get; } = new();
        internal Dictionary<string, SolidColorBrush> Brushes { get; } = new(StringComparer.Ordinal);
        internal bool IsAttached { get; set; }
    }

    private static void ConfigureNativeInputPalette(object? sender, EventArgs args)
    {
        if (sender is not VisualElement { Handler.PlatformView: NativeControl native } element ||
            element.GetValue(InputPaletteProperty) is not NeraMauiRibbonPalette palette) return;

        var resources = InputResources.GetOrCreateValue(native);
        var brushes = resources.Brushes;
        SolidColorBrush Set(string key, Color color)
        {
            if (!brushes.TryGetValue(key, out var brush))
            {
                brushes.Add(key, brush = new SolidColorBrush());
                // IDictionary.Add checks WinUI HasKey, which also sees fallback
                // theme resources. Insert into our initially empty local dictionary.
                resources.Dictionary[key] = brush;
            }
            // Mutate only brushes owned by this control. Existing ThemeResource references
            // must update even when Dark -> HighContrastDark leaves RequestedTheme unchanged.
            brush.Color = global::Windows.UI.Color.FromArgb(255,
                (byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255));
            return brush;
        }

        native.FocusVisualPrimaryBrush = Set("FocusStrokeColorOuterBrush", palette.Accent);
        native.FocusVisualSecondaryBrush = Set("FocusStrokeColorInnerBrush", palette.Surface);
        if (native is Microsoft.UI.Xaml.Controls.ComboBox)
        {
            foreach (var state in new[] { "", "Disabled", "PointerOver", "Pressed", "Focused", "FocusedPressed" })
            {
                Set("ComboBoxForeground" + state, state == "Disabled" ? palette.Muted : palette.Text);
                Set("ComboBoxPlaceHolderForeground" + state, palette.Muted);
            }
            foreach (var state in new[] { "", "Disabled", "Focused", "FocusedPressed" })
                Set("ComboBoxDropDownGlyphForeground" + state, state == "Disabled" ? palette.Muted : palette.Text);
            foreach (var state in new[] { "", "Disabled", "PointerOver", "Pressed", "Focused", "Unfocused" })
                Set("ComboBoxBackground" + state, state == "PointerOver" ? palette.Hover :
                    state == "Pressed" ? palette.Pressed : palette.Surface);
            foreach (var state in new[] { "", "Disabled", "PointerOver", "Pressed" })
                Set("ComboBoxBorderBrush" + state, state is "PointerOver" or "Pressed" ? palette.Accent : palette.Separator);
            Set("ComboBoxBackgroundBorderBrushFocused", palette.Accent);
            Set("ComboBoxBackgroundBorderBrushUnfocused", palette.Separator);
            Set("ComboBoxDropDownBackground", palette.Surface);
            Set("ComboBoxDropDownBorderBrush", palette.Separator);
            Set("ComboBoxDropDownForeground", palette.Text);
            Set("ComboBoxItemPillFillBrush", palette.Accent);
            foreach (var state in new[] { "", "Disabled", "PointerOver", "Pressed", "Selected", "SelectedDisabled",
                "SelectedPointerOver", "SelectedPressed", "SelectedUnfocused" })
            {
                Set("ComboBoxItemBackground" + state, state.Contains("Pressed", StringComparison.Ordinal) ? palette.Pressed :
                    state.StartsWith("Selected", StringComparison.Ordinal) ? palette.Checked :
                    state == "PointerOver" ? palette.Hover : palette.Surface);
                Set("ComboBoxItemForeground" + state, state.Contains("Disabled", StringComparison.Ordinal) ? palette.Muted : palette.Text);
            }
            native.BorderBrush = brushes["ComboBoxBorderBrush"];
        }
        else if (native is Microsoft.UI.Xaml.Controls.TextBox textBox)
        {
            foreach (var state in new[] { "", "Disabled", "PointerOver", "Focused" })
            {
                Set("TextControlBackground" + state, palette.Surface);
                Set("TextControlForeground" + state, state == "Disabled" ? palette.Muted : palette.Text);
                Set("TextControlPlaceholderForeground" + state, palette.Muted);
                Set("TextControlBorderBrush" + state, state == "Focused" ? palette.Accent : palette.Separator);
            }
            Set("TextControlElevationBorderBrush", palette.Separator);
            Set("TextControlElevationBorderFocusedBrush", palette.Accent);
            // WinUI TextServicesHost fixes selected text to white outside OS high
            // contrast. Dark palettes therefore need a dark selection fill, even
            // though their focus/popup accent is light.
            textBox.SelectionHighlightColor = Set("TextControlSelectionHighlightColor",
                palette.Surface.Red < 0.5f ? palette.Pressed : palette.Accent);
            native.BorderBrush = brushes["TextControlBorderBrush"];
        }
        if (!resources.IsAttached)
        {
            // Keep MAUI's local resource keys intact. Re-inserting those keys in the
            // native dictionary can conflict with MAUI's own deferred resources.
            native.Resources.MergedDictionaries.Add(resources.Dictionary);
            resources.IsAttached = true;
        }
        native.RequestedTheme = palette.Surface.Red < 0.5f
            ? Microsoft.UI.Xaml.ElementTheme.Dark : Microsoft.UI.Xaml.ElementTheme.Light;
    }
}
#endif
