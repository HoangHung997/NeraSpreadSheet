using global::Avalonia.Controls;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

/// <summary>View-only zoom result. The embedding host applies it to its active viewport;
/// no workbook transaction is created by this dialog.</summary>
public sealed class NeraZoomDialog : NeraSettingsDialog
{
    private readonly TextBox _percent;
    public NeraZoomDialog(double zoom, PresentationLocalization? localization = null, NeraIconTheme theme = NeraIconTheme.Light)
        : base("Thu phóng", "nera-zoom-dialog", localization, theme)
    {
        if (!double.IsFinite(zoom) || zoom < 0.1 || zoom > 4) throw new ArgumentOutOfRangeException(nameof(zoom));
        Width = 560; Height = 260; MinHeight = 220;
        var panel = Panel();
        _percent = TextField(panel, "zoom-percent", "Thu phóng (%)", (zoom * 100).ToString("G", Localization.Culture));
        Note(panel, "Chỉ thay đổi chế độ xem, không thay đổi dữ liệu hoặc tỷ lệ in.");
        DialogBody.Children.Add(panel); Zoom = zoom;
    }
    public double Zoom { get; private set; }
    protected override bool TryApply() { Zoom = ReadNumber(_percent, 10, 400) / 100; return true; }
}
