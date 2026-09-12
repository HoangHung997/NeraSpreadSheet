using System.Globalization;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Avalonia;

public sealed partial class NeraRibbonControl
{
    private Control BuildItem(RibbonItemLayout item) => item.Presentation.Definition.IsDialogLauncher ? BuildDialogLauncher(item) : item.Presentation.Kind switch
    {
        RibbonItemKind.Separator => new Border { Background = Brushes.Gray, Margin = new Thickness(1, 3) },
        RibbonItemKind.ComboBox or RibbonItemKind.ColorPicker => BuildCombo(item),
        RibbonItemKind.SplitButton => BuildSplitButton(item),
        RibbonItemKind.Menu or RibbonItemKind.DropDown => BuildDropDown(item),
        RibbonItemKind.Gallery => BuildGallery(item),
        _ => BuildButton(item),
    };
    private Button BuildButton(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        Button button = item.Presentation.IsToggle ? new ToggleButton { IsChecked = command.IsChecked ?? false } : new Button();
        button.Content = BuildContent(item); button.IsEnabled = command.IsEnabled;
        button.Padding = new Thickness(3, 1); button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        SetIdentity(button, "ribbon-command-" + command.CommandId.Value, item.Presentation.AutomationName);
        ToolTip.SetTip(button, ToolTipText(command));
        button.Click += async (_, _) => await ActivateCommandAsync(command.CommandId);
        return button;
    }
    private Grid BuildContent(RibbonItemLayout item, bool arrow = false)
    {
        var command = item.Presentation.Command; var large = item.Size == RibbonItemSize.Large;
        var content = new StackPanel
        {
            Orientation = large ? Orientation.Vertical : Orientation.Horizontal, Spacing = 3,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = large ? HorizontalAlignment.Center : HorizontalAlignment.Left,
        };
        var icon = command.IconKey is { } key ? ResolveIcon(key, large ? 32 : 16) : null;
        if (icon is not null) content.Children.Add(new Image { Source = icon, Width = large ? 32 : 16, Height = large ? 32 : 16 });
        if (item.CaptionVisible || icon is null)
            content.Children.Add(new TextBlock
            {
                Text = command.Caption + (arrow ? " ⌄" : string.Empty), TextWrapping = large ? TextWrapping.Wrap : TextWrapping.NoWrap,
                TextAlignment = large ? TextAlignment.Center : TextAlignment.Left, TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = Math.Max(1, item.Width / LayoutSnapshot.Scale - (large || icon is null ? 8 : 28)),
                MaxHeight = large ? item.CaptionMaxLines * 15 : 18, FontSize = 11, VerticalAlignment = VerticalAlignment.Center,
            });
        else if (arrow) content.Children.Add(new TextBlock { Text = "⌄" });
        var wrapper = new Grid(); wrapper.Children.Add(content);
        if (KeyTipScope == RibbonKeyTipScope.Tab && _runtime.KeyTips.TryGetCommandTip(command.CommandId, out var tip))
            wrapper.Children.Add(new Border
            {
                Child = new TextBlock { Text = tip, FontSize = 10 }, Background = Background, BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1), Padding = new Thickness(2, 0), HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false,
            });
        return wrapper;
    }
    private DockPanel BuildSplitButton(RibbonItemLayout item)
    {
        var command = item.Presentation.Command; var panel = new DockPanel();
        var arrow = new Button { Content = "⌄", Width = 18, Padding = new Thickness(0), IsEnabled = command.IsEnabled };
        DockPanel.SetDock(arrow, Dock.Right); panel.Children.Add(arrow);
        var primary = BuildButton(item with { Width = Math.Max(1, item.Width - 18 * LayoutSnapshot.Scale) });
        SetIdentity(primary, $"ribbon-command-{command.CommandId.Value}-primary", item.Presentation.AutomationName); panel.Children.Add(primary);
        var menu = new ContextMenu(); foreach (var choice in command.SelectableItems) menu.Items.Add(BuildChoice(command.CommandId, choice, command.SelectedValue));
        arrow.ContextMenu = menu; arrow.Click += (_, _) => menu.Open(arrow);
        SetIdentity(arrow, $"ribbon-command-{command.CommandId.Value}-menu", item.Presentation.AutomationName + " — " + Localize("Lựa chọn"));
        return panel;
    }
    private Button BuildDropDown(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var button = new Button { Content = BuildContent(item, true), IsEnabled = command.IsEnabled, Padding = new Thickness(3, 1) };
        var menu = new ContextMenu(); foreach (var choice in command.SelectableItems) menu.Items.Add(BuildChoice(command.CommandId, choice, command.SelectedValue));
        button.ContextMenu = menu; button.Click += (_, _) => menu.Open(button);
        SetIdentity(button, "ribbon-command-" + command.CommandId.Value, item.Presentation.AutomationName); ToolTip.SetTip(button, ToolTipText(command));
        return button;
    }
    private MenuItem BuildChoice(CommandId id, CommandItem choice, string? selectedValue = null)
    {
        var selected = choice.IsChecked ?? (selectedValue is not null && string.Equals(choice.Value, selectedValue, StringComparison.Ordinal));
        var native = new MenuItem
        {
            Header = choice.Caption, IsEnabled = choice.IsEnabled,
            ToggleType = choice.IsChecked.HasValue || selectedValue is not null ? MenuItemToggleType.CheckBox : MenuItemToggleType.None, IsChecked = selected,
        };
        SetIdentity(native, $"ribbon-command-{id.Value}-choice-{choice.Value}", choice.Caption); ToolTip.SetTip(native, choice.Tooltip ?? choice.Caption);
        if (choice.IconKey is { } key && ResolveIcon(key, 16) is { } image) native.Icon = new Image { Source = image, Width = 16, Height = 16 };
        foreach (var child in choice.Children) native.Items.Add(BuildChoice(id, child, selectedValue));
        if (choice.Children.Count == 0) native.Click += async (_, e) => { e.Handled = true; await ActivateChoiceAsync(id, choice.Value); };
        return native;
    }
    private ComboBox BuildCombo(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var selectedValue = command.SelectedValue;
        if (command.CommandId.Value == "Ui.Zoom" &&
            double.TryParse(selectedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var zoomPercent))
            selectedValue = Math.Round(zoomPercent, 6).ToString("0.######", CultureInfo.InvariantCulture);
        var combo = new ComboBox { IsEnabled = command.IsEnabled, Padding = new Thickness(3, 1), MinHeight = 0 };
        foreach (var choice in command.SelectableItems)
        {
            object content = choice.Caption;
            if (item.Presentation.Kind == RibbonItemKind.ColorPicker)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
                if (Color.TryParse(choice.Value, out var color)) row.Children.Add(new Border { Background = new SolidColorBrush(color), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Width = 14, Height = 14 });
                row.Children.Add(new TextBlock { Text = choice.Caption }); content = row;
            }
            var native = new ComboBoxItem { Content = content, Tag = choice.Value, IsEnabled = choice.IsEnabled };
            SetIdentity(native, $"ribbon-command-{command.CommandId.Value}-choice-{choice.Value}", choice.Caption); combo.Items.Add(native);
            if (string.Equals(selectedValue, choice.Value, StringComparison.Ordinal)) combo.SelectedItem = native;
        }
        // Composite command values may be valid even when they are not one of the
        // preset choices (for example ZoomIn turns 100% into 110%). Do not render
        // an empty ComboBox merely because the host's common-value list is sparse.
        if (combo.SelectedItem is null && !string.IsNullOrWhiteSpace(selectedValue))
        {
            var caption = selectedValue;
            if (command.CommandId.Value == "Ui.Zoom" && !caption.EndsWith('%')) caption += "%";
            var current = new ComboBoxItem { Content = caption, Tag = selectedValue, IsEnabled = command.IsEnabled };
            SetIdentity(current, $"ribbon-command-{command.CommandId.Value}-choice-current", caption);
            combo.Items.Insert(0, current);
            combo.SelectedItem = current;
        }
        SetIdentity(combo, "ribbon-command-" + command.CommandId.Value, item.Presentation.AutomationName); ToolTip.SetTip(combo, ToolTipText(command));
        combo.SelectionChanged += async (_, e) =>
        {
            if (!_rebuilding && ReferenceEquals(e.Source, combo) && combo.SelectedItem is ComboBoxItem { Tag: string value, IsEnabled: true })
                await ActivateChoiceAsync(command.CommandId, value);
        };
        return combo;
    }
    private DockPanel BuildGallery(RibbonItemLayout item)
    {
        var command = item.Presentation.Command;
        var tiles = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2 };
        foreach (var choice in command.SelectableItems) tiles.Children.Add(BuildGalleryChoice(item, choice));
        var scroll = new ScrollViewer { Content = tiles, HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var arrows = new Grid { RowDefinitions = new RowDefinitions("*,*,*"), Width = 18 };
        var previous = new Button { Content = "⌃", Padding = new Thickness(0), IsEnabled = false };
        var next = new Button { Content = "⌄", Padding = new Thickness(0), IsEnabled = command.IsEnabled };
        var all = new Button { Content = "▾", Padding = new Thickness(0), IsEnabled = command.IsEnabled };
        previous.Click += (_, _) => scroll.Offset = new Vector(Math.Max(0, scroll.Offset.X - 74), 0);
        next.Click += (_, _) => scroll.Offset = new Vector(scroll.Offset.X + 74, 0);
        scroll.ScrollChanged += (_, _) =>
        {
            previous.IsEnabled = command.IsEnabled && scroll.Offset.X > 0;
            next.IsEnabled = command.IsEnabled && scroll.Offset.X + scroll.Viewport.Width < scroll.Extent.Width;
        };
        all.Click += (_, _) =>
        {
            ClosePopups(); var wrap = new WrapPanel { Width = 380, Orientation = Orientation.Horizontal };
            foreach (var choice in command.SelectableItems) wrap.Children.Add(BuildGalleryChoice(item, choice));
            CreatePopup(all, new ScrollViewer { Content = wrap, MaxHeight = 360, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }).IsOpen = true;
        };
        SetIdentity(previous, $"ribbon-command-{command.CommandId.Value}-previous", Localize("Kiểu trước"));
        SetIdentity(next, $"ribbon-command-{command.CommandId.Value}-next", Localize("Kiểu tiếp theo"));
        SetIdentity(all, $"ribbon-command-{command.CommandId.Value}-more", Localize("Tất cả kiểu"));
        arrows.Children.Add(previous); Grid.SetRow(next, 1); arrows.Children.Add(next); Grid.SetRow(all, 2); arrows.Children.Add(all);
        var panel = new DockPanel(); DockPanel.SetDock(arrows, Dock.Right); panel.Children.Add(arrows); panel.Children.Add(scroll);
        SetIdentity(panel, "ribbon-command-" + command.CommandId.Value, item.Presentation.AutomationName); return panel;
    }
    private ToggleButton BuildGalleryChoice(RibbonItemLayout item, CommandItem choice)
    {
        var command = item.Presentation.Command;
        var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 3 };
        if (item.Presentation.Definition.GalleryPreview?.Invoke(choice) is { } preview) content.Children.Add(new NeraRibbonGalleryThumbnail(preview) { Width = 60, Height = 38 });
        else if (choice.IconKey is { } key && ResolveIcon(key, 32) is { } icon) content.Children.Add(new Image { Source = icon, Width = 32, Height = 32 });
        content.Children.Add(new TextBlock { Text = choice.Caption, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontSize = 10, MaxWidth = 64 });
        var button = new ToggleButton
        {
            Content = content, Width = 72, Height = Math.Max(24, item.Height / LayoutSnapshot.Scale - 4), Margin = new Thickness(1), Padding = new Thickness(2),
            IsEnabled = command.IsEnabled && choice.IsEnabled, IsChecked = string.Equals(command.SelectedValue, choice.Value, StringComparison.Ordinal),
        };
        SetIdentity(button, $"ribbon-command-{command.CommandId.Value}-choice-{choice.Value}", choice.Caption); ToolTip.SetTip(button, choice.Tooltip ?? choice.Caption);
        button.Click += async (_, _) => await ActivateChoiceAsync(command.CommandId, choice.Value); return button;
    }
    private Control BuildOverflowItem(RibbonItemLayout item)
    {
        if (item.Presentation.Kind == RibbonItemKind.Separator) return new Separator();
        var command = item.Presentation.Command;
        var native = new MenuItem
        {
            Header = command.Caption, IsEnabled = command.IsEnabled, IsChecked = command.IsChecked ?? false,
            ToggleType = command.IsChecked.HasValue ? MenuItemToggleType.CheckBox : MenuItemToggleType.None,
        };
        SetIdentity(native, "ribbon-overflow-command-" + command.CommandId.Value, item.Presentation.AutomationName);
        if (command.IconKey is { } key && ResolveIcon(key, 16) is { } image) native.Icon = new Image { Source = image, Width = 16, Height = 16 };
        if (command.SelectableItems.Count > 0)
        {
            if (item.Presentation.Kind == RibbonItemKind.SplitButton)
            {
                var primary = new MenuItem { Header = command.Caption, IsEnabled = command.IsEnabled };
                primary.Click += async (_, e) => { e.Handled = true; await ActivateCommandAsync(command.CommandId); };
                native.Items.Add(primary); native.Items.Add(new Separator());
            }
            foreach (var choice in command.SelectableItems) native.Items.Add(BuildChoice(command.CommandId, choice, command.SelectedValue));
        }
        else native.Click += async (_, e) => { e.Handled = true; await ActivateCommandAsync(command.CommandId); };
        return native;
    }
}

/// <summary>A bounded, color-only native projection of a shared gallery thumbnail.</summary>
public sealed class NeraRibbonGalleryThumbnail : Control
{
    private readonly RibbonGalleryPreview _preview;
    public NeraRibbonGalleryThumbnail(RibbonGalleryPreview preview) => _preview = preview ?? throw new ArgumentNullException(nameof(preview));
    public override void Render(DrawingContext context)
    {
        base.Render(context); if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var width = Bounds.Width / _preview.Columns; var height = Bounds.Height / _preview.Rows;
        for (var row = 0; row < _preview.Rows; row++)
        for (var column = 0; column < _preview.Columns; column++)
        {
            var cell = _preview.Cells[row * _preview.Columns + column]; var bounds = new Rect(column * width, row * height, width, height);
            context.DrawRectangle(new SolidColorBrush(Color.FromUInt32(cell.BackgroundArgb)), null, bounds);
            context.DrawLine(new Pen(new SolidColorBrush(Color.FromUInt32(cell.ForegroundArgb)), Math.Min(1, height / 4)),
                new Point(bounds.X + width * 0.2, bounds.Y + height * 0.5), new Point(bounds.X + width * 0.75, bounds.Y + height * 0.5));
        }
    }
}
