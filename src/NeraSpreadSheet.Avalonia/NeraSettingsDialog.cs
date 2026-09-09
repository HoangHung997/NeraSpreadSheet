using System.Globalization;
using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Input;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using global::Avalonia.Styling;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Owner-modal settings shell with scoped palette, native keyboard navigation,
/// inline validation and one confirmation path. Does not replace application resources.</summary>
public abstract class NeraSettingsDialog : Window
{
    private readonly TextBlock _error = new() { TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private bool _confirming;
    protected readonly DockPanel DialogBody = new() { Margin = new Thickness(16) };
    protected PresentationLocalization Localization { get; }
    protected NeraSettingsDialog(string titleKey, string id, PresentationLocalization? localization, NeraIconTheme theme)
    {
        Localization = localization ?? PresentationLocalization.Default;
        Title = L(titleKey);
        Width = 740; Height = 600; MinWidth = 540; MinHeight = 430;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var palette = new NeraRibbonResources(); palette.Apply(theme);
        Resources.MergedDictionaries.Add(palette);
        RequestedThemeVariant = theme is NeraIconTheme.Dark or NeraIconTheme.HighContrastDark ? ThemeVariant.Dark : ThemeVariant.Light;
        Background = palette.Brush("Surface"); Foreground = palette.Brush("Foreground");
        FontFamily = new FontFamily("Segoe UI, Inter, $Default"); FontSize = 13;
        Identify(this, id, Title);
        var footer = new StackPanel { Spacing = 8, Margin = new Thickness(0, 12, 0, 0) };
        Identify(_error, "dialog-validation", L("Không thể áp dụng")); footer.Children.Add(_error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        var ok = new Button { Content = L("Đồng ý"), MinWidth = 88, IsDefault = true };
        var cancel = new Button { Content = L("Hủy"), MinWidth = 88, IsCancel = true };
        Identify(ok, "dialog-ok", L("Đồng ý")); Identify(cancel, "dialog-cancel", L("Hủy"));
        ok.Click += (_, _) => Confirm(); cancel.Click += (_, _) => Close(false);
        buttons.Children.Add(ok); buttons.Children.Add(cancel); footer.Children.Add(buttons);
        DockPanel.SetDock(footer, Dock.Bottom); DialogBody.Children.Add(footer); Content = DialogBody;
        KeyDown += (_, e) => { if (e.Key == Key.Escape && !e.Handled) { e.Handled = true; Close(false); } };
    }

    protected string L(string key) => Localization.Get(key);
    protected virtual bool TryApply() => true;
    private void Confirm()
    {
        if (_confirming) return;
        _confirming = true;
        try { if (TryApply()) Close(true); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            _error.Text = L("Không thể áp dụng") + ": " + exception.Message;
            _error.IsVisible = true;
        }
        finally { _confirming = false; }
    }
    protected void AddTabs(TabControl tabs) => DialogBody.Children.Add(tabs);
    protected TabItem Tab(string id, string title, Control content)
    {
        var scroll = new ScrollViewer { Content = content, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var tab = new TabItem { Header = L(title), Content = scroll, Tag = id };
        Identify(tab, "dialog-tab-" + id, L(title)); return tab;
    }
    protected static StackPanel Panel() => new() { Spacing = 10, Margin = new Thickness(10) };
    protected TextBox TextField(Panel parent, string id, string label, string? text)
    {
        var input = new TextBox { Text = text, MinWidth = 130, Watermark = L("Giữ nguyên / nhiều giá trị") };
        Identify(input, id, L(label)); AddField(parent, label, input); return input;
    }
    protected ComboBox ChoiceField(Panel parent, string id, string label, IEnumerable<(string Id, string Caption)> values, string? current)
    {
        var input = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch, MinWidth = 160 };
        foreach (var (value, caption) in values)
        {
            var item = new ComboBoxItem { Content = L(caption), Tag = value };
            input.Items.Add(item); if (value == current) input.SelectedItem = item;
        }
        Identify(input, id, L(label)); AddField(parent, label, input); return input;
    }
    protected CheckBox CheckField(Panel parent, string id, string label, bool? value)
    {
        var input = new CheckBox { Content = L(label), IsThreeState = true, IsChecked = value };
        Identify(input, id, L(label)); parent.Children.Add(input); return input;
    }
    protected void Note(Panel parent, string text) => parent.Children.Add(new TextBlock { Text = L(text), TextWrapping = TextWrapping.Wrap });
    private void AddField(Panel parent, string label, Control input)
    {
        var field = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*"), ColumnSpacing = 10 };
        field.Children.Add(new TextBlock { Text = L(label), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
        Grid.SetColumn(input, 1); field.Children.Add(input); parent.Children.Add(field);
    }
    protected double ReadNumber(TextBox input, double minimum, double maximum)
    {
        if (!double.TryParse(input.Text, NumberStyles.Float, Localization.Culture, out var value) || !double.IsFinite(value) || value < minimum || value > maximum)
            throw new ArgumentException(Localization.Format("Giá trị phải nằm trong khoảng {0} đến {1}.", minimum, maximum));
        return value;
    }
    protected int ReadInteger(TextBox input, int minimum, int maximum)
    {
        var value = ReadNumber(input, minimum, maximum);
        if (value != Math.Truncate(value)) throw new ArgumentException(L("Cần nhập số nguyên."));
        return (int)value;
    }
    protected static string? Selected(ComboBox input) => (input.SelectedItem as ComboBoxItem)?.Tag as string;
    protected static void Identify(Control control, string id, string? name)
    { AutomationProperties.SetAutomationId(control, id); AutomationProperties.SetName(control, name ?? id); }
}
