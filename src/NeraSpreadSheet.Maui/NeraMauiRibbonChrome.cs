using Microsoft.Maui.Controls;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;
using SkiaSharp;

namespace NeraSpreadSheet.Maui;

internal sealed record NeraMauiRibbonPalette(
    Color Surface, Color Chrome, Color Text, Color Muted, Color Separator,
    Color Hover, Color Pressed, Color Checked, Color Accent)
{
    internal static NeraMauiRibbonPalette For(NeraIconTheme theme) => theme switch
    {
        NeraIconTheme.Dark => new(Color.FromArgb("#252525"), Color.FromArgb("#202020"), Color.FromArgb("#F4F4F4"), Color.FromArgb("#BDBDBD"), Color.FromArgb("#494949"), Color.FromArgb("#3A4640"), Color.FromArgb("#496052"), Color.FromArgb("#354D40"), Color.FromArgb("#69D5A0")),
        NeraIconTheme.HighContrastDark => new(Colors.Black, Colors.Black, Colors.White, Colors.White, Colors.White, Color.FromArgb("#3A3A3A"), Color.FromArgb("#555555"), Color.FromArgb("#3A3A3A"), Color.FromArgb("#FFEF00")),
        NeraIconTheme.HighContrastLight => new(Colors.White, Colors.White, Colors.Black, Colors.Black, Colors.Black, Color.FromArgb("#D9E5FF"), Color.FromArgb("#B8CCFF"), Color.FromArgb("#D9E5FF"), Color.FromArgb("#0035B2")),
        _ => new(Colors.White, Color.FromArgb("#F5F7F6"), Color.FromArgb("#24292D"), Color.FromArgb("#60676C"), Color.FromArgb("#DEE3E0"), Color.FromArgb("#EAF2ED"), Color.FromArgb("#C8E2D2"), Color.FromArgb("#DDEFE4"), Color.FromArgb("#18734A")),
    };
}

internal static class NeraMauiRibbonChrome
{
#if WINDOWS
    private static readonly BindableProperty FilterCheckGlyphProperty = BindableProperty.CreateAttached(
        "FilterCheckGlyph", typeof(Color), typeof(NeraMauiRibbonChrome), Colors.White,
        propertyChanged: static (target, _, _) => ConfigureNativeCheckGlyph(target, EventArgs.Empty));

    private static void ConfigureNativeCheckGlyph(object? sender, EventArgs args)
    {
        if (sender is not CheckBox { Handler.PlatformView: Microsoft.UI.Xaml.Controls.CheckBox native } checkBox) return;
        var color = (Color)checkBox.GetValue(FilterCheckGlyphProperty);
        var brush = new Microsoft.UI.Xaml.Media.SolidColorBrush(global::Windows.UI.Color.FromArgb(
            255, (byte)(color.Red * 255), (byte)(color.Green * 255), (byte)(color.Blue * 255)));
        foreach (var state in new[] { "Checked", "CheckedPointerOver", "CheckedPressed", "CheckedDisabled",
            "Indeterminate", "IndeterminatePointerOver", "IndeterminatePressed", "IndeterminateDisabled" })
            native.Resources["CheckBoxCheckGlyphForeground" + state] = brush;
        native.RequestedTheme = color == Colors.Black ? Microsoft.UI.Xaml.ElementTheme.Dark : Microsoft.UI.Xaml.ElementTheme.Light;
    }
#endif

    internal static void ConfigureFilter(VisualElement root, NeraMauiRibbonPalette palette)
    {
        // A loaded WinUI Label cannot acquire a new background container without
        // reparenting its TextBlock. Text inherits the existing sheet/row surface.
        if (root is not Label and not CheckBox) root.BackgroundColor = palette.Surface;
        switch (root)
        {
            case Button button:
                Configure(button, palette, false);
                button.MinimumHeightRequest = 32d;
                break;
            case Label label: label.TextColor = palette.Text; break;
            case Entry entry:
                entry.TextColor = palette.Text;
                entry.PlaceholderColor = palette.Muted;
                break;
            case Picker picker:
                picker.TextColor = palette.Text;
                picker.TitleColor = palette.Muted;
                break;
            case CheckBox checkBox:
                checkBox.Color = palette.Accent;
#if WINDOWS
                // The dark palettes use a light accent; keep the native check glyph
                // legible without changing selection or application theme resources.
                checkBox.SetValue(FilterCheckGlyphProperty, palette.Surface.Red < 0.5f ? Colors.Black : Colors.White);
                checkBox.HandlerChanged -= ConfigureNativeCheckGlyph;
                checkBox.HandlerChanged += ConfigureNativeCheckGlyph;
                ConfigureNativeCheckGlyph(checkBox, EventArgs.Empty);
#endif
                break;
        }
        IEnumerable<VisualElement> children = root switch
        {
            Microsoft.Maui.Controls.Layout layout => layout.Children.OfType<VisualElement>(),
            Border { Content: { } content } => [content],
            ContentView { Content: { } content } => [content],
            ScrollView { Content: { } content } => [content],
            _ => [],
        };
        foreach (var child in children) ConfigureFilter(child, palette);
    }

    internal static void Configure(Button button, NeraMauiRibbonPalette palette, bool isChecked)
    {
        var previousState = VisualStateManager.GetVisualStateGroups(button)
            .FirstOrDefault(static group => group.Name == "CommonStates")?.CurrentState?.Name;
        button.FontFamily = "Segoe UI";
        button.FontSize = 12d;
        button.Padding = new Thickness(3d, 0d);
        button.MinimumHeightRequest = 0d;
        button.MinimumWidthRequest = 0d;
        button.CornerRadius = 3;
        var states = new VisualStateGroup { Name = "CommonStates" };
        AddState(states, "Normal", isChecked ? palette.Checked : palette.Surface, palette.Text, isChecked ? 1d : 0d);
        AddState(states, "PointerOver", palette.Hover, palette.Text, 1d);
        AddState(states, "Pressed", palette.Pressed, palette.Text, 2d);
        AddState(states, "Disabled", palette.Surface, palette.Muted);
        var focused = new VisualState { Name = "Focused" };
        focused.Setters.Add(new Setter { Property = Button.BorderWidthProperty, Value = 2d });
        focused.Setters.Add(new Setter { Property = Button.BorderColorProperty, Value = palette.Accent });
        states.States.Add(focused);
        VisualStateManager.SetVisualStateGroups(button, [states]);
        // Replacing groups restores old state setters. Apply the new base colors
        // afterwards, then restore the current native interaction state.
        button.TextColor = palette.Text;
        button.BackgroundColor = isChecked ? palette.Checked : palette.Surface;
        button.BorderColor = palette.Accent;
        button.BorderWidth = isChecked ? 1d : 0d;
        VisualStateManager.GoToState(button, !button.IsEnabled ? "Disabled" :
            button.IsFocused ? "Focused" : previousState ?? "Normal");
        RemoveNativeMinimums(button);
        button.Loaded -= ConfigureNativeCaption;
        button.Loaded += ConfigureNativeCaption;
    }

    internal static void RemoveNativeMinimums(VisualElement element)
    {
#if WINDOWS
        static void ConfigureNative(object? sender, EventArgs args)
        {
            if (sender is VisualElement { Handler.PlatformView: Microsoft.UI.Xaml.Controls.Control native })
            {
                native.MinHeight = 0d;
                native.MinWidth = 0d;
                native.Padding = new Microsoft.UI.Xaml.Thickness(3d, 0d, 3d, 0d);
            }
        }
        element.HandlerChanged -= ConfigureNative;
        element.HandlerChanged += ConfigureNative;
        ConfigureNative(element, EventArgs.Empty);
#endif
    }

    internal static ImageSource CreatePreview(RibbonGalleryPreview preview)
    {
        const int width = 64;
        const int height = 32;
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        using var paint = new SKPaint();
        var cellWidth = (float)width / preview.Columns;
        var cellHeight = (float)height / preview.Rows;
        for (var row = 0; row < preview.Rows; row++)
        {
            for (var column = 0; column < preview.Columns; column++)
            {
                var cell = preview.Cells[(row * preview.Columns) + column];
                var x = column * cellWidth;
                var y = row * cellHeight;
                paint.Color = new SKColor(cell.BackgroundArgb);
                surface.Canvas.DrawRect(x, y, cellWidth, cellHeight, paint);
                paint.Color = new SKColor(cell.ForegroundArgb);
                surface.Canvas.DrawLine(x + 3f, y + (cellHeight / 2f), x + cellWidth - 3f, y + (cellHeight / 2f), paint);
            }
        }
        using var image = surface.Snapshot();
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var bytes = encoded.ToArray();
        return ImageSource.FromStream(() => new MemoryStream(bytes, writable: false));
    }

    private static void AddState(VisualStateGroup group, string name, Color background, Color foreground, double borderWidth = 0d)
    {
        var state = new VisualState { Name = name };
        state.Setters.Add(new Setter { Property = VisualElement.BackgroundColorProperty, Value = background });
        state.Setters.Add(new Setter { Property = Button.TextColorProperty, Value = foreground });
        state.Setters.Add(new Setter { Property = Button.BorderWidthProperty, Value = borderWidth });
        group.States.Add(state);
    }

    private static void ConfigureNativeCaption(object? sender, EventArgs args)
    {
        if (sender is not Button button)
        {
            return;
        }
#if WINDOWS
        if (button.Handler?.PlatformView is Microsoft.UI.Xaml.DependencyObject native)
        {
            SetCaption(native, button.LineBreakMode == LineBreakMode.WordWrap ? 2 : 1);
        }

        static void SetCaption(Microsoft.UI.Xaml.DependencyObject parent, int lines)
        {
            for (var index = 0; index < Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, index);
                if (child is Microsoft.UI.Xaml.Controls.TextBlock text)
                {
                    text.MaxLines = lines;
                    text.TextWrapping = lines > 1 ? Microsoft.UI.Xaml.TextWrapping.Wrap : Microsoft.UI.Xaml.TextWrapping.NoWrap;
                    text.TextTrimming = Microsoft.UI.Xaml.TextTrimming.CharacterEllipsis;
                }
                SetCaption(child, lines);
            }
        }
#elif ANDROID
        if (button.Handler?.PlatformView is Android.Widget.TextView native)
        {
            native.SetMaxLines(button.LineBreakMode == LineBreakMode.WordWrap ? 2 : 1);
        }
#elif IOS || MACCATALYST
        if (button.Handler?.PlatformView is UIKit.UIButton { TitleLabel: { } label })
        {
            label.Lines = button.LineBreakMode == LineBreakMode.WordWrap ? 2 : 1;
            label.LineBreakMode = button.LineBreakMode == LineBreakMode.WordWrap ? UIKit.UILineBreakMode.WordWrap : UIKit.UILineBreakMode.TailTruncation;
        }
#endif
    }
}
