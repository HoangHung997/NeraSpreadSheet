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
        NeraIconTheme.HighContrastDark => new(Color.FromArgb("#252525"), Color.FromArgb("#202020"), Color.FromArgb("#F4F4F4"), Colors.White, Colors.White, Color.FromArgb("#3A3A3A"), Color.FromArgb("#496052"), Color.FromArgb("#3A3A3A"), Color.FromArgb("#FFEF00")),
        NeraIconTheme.HighContrastLight => new(Colors.White, Color.FromArgb("#F5F7F6"), Color.FromArgb("#24292D"), Colors.Black, Colors.Black, Color.FromArgb("#D9E5FF"), Color.FromArgb("#C8E2D2"), Color.FromArgb("#D9E5FF"), Color.FromArgb("#0035B2")),
        _ => new(Colors.White, Color.FromArgb("#F5F7F6"), Color.FromArgb("#24292D"), Color.FromArgb("#60676C"), Color.FromArgb("#DEE3E0"), Color.FromArgb("#EAF2ED"), Color.FromArgb("#C8E2D2"), Color.FromArgb("#DDEFE4"), Color.FromArgb("#18734A")),
    };
}

internal static class NeraMauiRibbonChrome
{
    internal static void Configure(Button button, NeraMauiRibbonPalette palette, bool isChecked)
    {
        button.FontFamily = "Segoe UI";
        button.FontSize = 12d;
        button.Padding = new Thickness(3d, 0d);
        button.MinimumHeightRequest = 0d;
        button.MinimumWidthRequest = 0d;
        button.CornerRadius = 3;
        button.TextColor = palette.Text;
        button.BackgroundColor = isChecked ? palette.Checked : palette.Surface;
        button.BorderColor = palette.Accent;
        button.BorderWidth = isChecked ? 1d : 0d;
        var states = new VisualStateGroup { Name = "CommonStates" };
        AddState(states, "Normal", isChecked ? palette.Checked : palette.Surface, palette.Text);
        AddState(states, "PointerOver", palette.Hover, palette.Text);
        AddState(states, "Pressed", palette.Pressed, palette.Text);
        AddState(states, "Disabled", palette.Surface, palette.Muted);
        var focused = new VisualState { Name = "Focused" };
        focused.Setters.Add(new Setter { Property = Button.BorderWidthProperty, Value = 1d });
        focused.Setters.Add(new Setter { Property = Button.BorderColorProperty, Value = palette.Accent });
        states.States.Add(focused);
        VisualStateManager.SetVisualStateGroups(button, [states]);
        RemoveNativeMinimums(button);
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

    private static void AddState(VisualStateGroup group, string name, Color background, Color foreground)
    {
        var state = new VisualState { Name = name };
        state.Setters.Add(new Setter { Property = VisualElement.BackgroundColorProperty, Value = background });
        state.Setters.Add(new Setter { Property = Button.TextColorProperty, Value = foreground });
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
