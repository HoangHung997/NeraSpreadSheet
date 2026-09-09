using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia;

public sealed partial class NeraRibbonControl
{
    private Button BuildDialogLauncher(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var glyph = new global::Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M 1,5 L 1,1 L 5,1 M 3,3 L 9,9 M 5,9 L 9,9 L 9,5"),
            Stroke = _chrome.Brush("Foreground"), StrokeThickness = 1,
            Width = 10, Height = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        var content = new Grid(); content.Children.Add(glyph);
        if (KeyTipScope == RibbonKeyTipScope.Tab && _runtime.KeyTips.TryGetCommandTip(command.CommandId, out var tip))
            content.Children.Add(new Border
            {
                Child = new TextBlock { Text = tip, FontSize = 8 }, Background = _chrome.Brush("Surface"),
                BorderBrush = _chrome.Brush("Accent"), BorderThickness = new Thickness(1), IsHitTestVisible = false,
            });
        var button = new Button
        {
            Content = content, IsEnabled = command.IsEnabled, Padding = new Thickness(2), MinWidth = 0, MinHeight = 0,
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        SetIdentity(button, "ribbon-command-" + command.CommandId.Value, item.Presentation.AutomationName);
        ToolTip.SetTip(button, ToolTipText(command));
        button.Click += async (_, _) => { ClosePopups(); await ActivateCommandAsync(command.CommandId); };
        return button;
    }
}
