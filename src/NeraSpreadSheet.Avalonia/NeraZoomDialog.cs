using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

/// <summary>View-only zoom result. The embedding host applies it to its active viewport;
/// no workbook transaction is created by this dialog.</summary>
public sealed class NeraZoomDialog : NeraSettingsDialog
{
    private readonly TextBox _percent;
    private bool _fitSelection;

    public NeraZoomDialog(double zoom, PresentationLocalization? localization = null, NeraIconTheme theme = NeraIconTheme.Light)
        : base("Thu phóng", "nera-zoom-dialog", localization, theme)
    {
        if (!double.IsFinite(zoom) || zoom < 0.1 || zoom > 4) throw new ArgumentOutOfRangeException(nameof(zoom));
        Width = 560; Height = 360; MinHeight = 300;
        var panel = Panel();
        _percent = TextField(panel, "zoom-percent", "Thu phóng (%)", (zoom * 100).ToString("G", Localization.Culture));
        _percent.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) _fitSelection = false;
        };

        panel.Children.Add(new TextBlock { Text = L("Mức thu phóng"), Margin = new Thickness(0, 8, 0, 4) });
        var presets = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 8, LineSpacing = 8 };
        panel.Children.Add(presets);
        foreach (var value in new[] { 200, 100, 75, 50, 25 })
        {
            var button = new Button { Content = $"{value}%", MinWidth = 68, Padding = new Thickness(10, 5) };
            AutomationProperties.SetAutomationId(button, $"zoom-preset-{value}");
            var captured = value;
            button.Click += (_, _) =>
            {
                _fitSelection = false;
                _percent.Text = captured.ToString(Localization.Culture);
            };
            presets.Children.Add(button);
        }
        var fit = new Button { Content = L("Vừa vùng chọn"), MinWidth = 130, Padding = new Thickness(10, 5) };
        AutomationProperties.SetAutomationId(fit, "zoom-fit-selection");
        fit.Click += (_, _) => _fitSelection = true;
        presets.Children.Add(fit);

        Note(panel, "Chỉ thay đổi chế độ xem, không thay đổi dữ liệu hoặc tỷ lệ in. Vừa vùng chọn được tính bởi viewport hiện hành khi nhấn OK.");
        DialogBody.Children.Add(panel); Zoom = zoom;
    }

    public double Zoom { get; private set; }
    public bool FitSelectionRequested => _fitSelection;

    protected override bool TryApply()
    {
        if (_fitSelection) return true;
        Zoom = ReadNumber(_percent, 10, 400) / 100;
        return true;
    }
}
