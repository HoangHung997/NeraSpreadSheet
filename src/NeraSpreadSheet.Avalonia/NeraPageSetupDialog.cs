using global::Avalonia.Controls;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

public enum NeraPageSetupTab { Page, Margins, Sheet }

/// <summary>Page Setup over canonical worksheet print settings. Local fields stay pending
/// until OK and untouched imported metadata is preserved without lossy unit conversion.</summary>
public sealed class NeraPageSetupDialog : NeraSettingsDialog, IDisposable
{
    private readonly SpreadsheetPageSetupDraft _draft;
    private readonly WorksheetPrintSettings _initial;
    private readonly Func<bool>? _contextIsCurrent;
    private readonly Dictionary<string, Func<SpreadsheetPageSetup, SpreadsheetPageSetup>> _updates = [];
    private readonly HashSet<string> _dirty = [];
    private readonly TabControl _tabs = new();
    public NeraPageSetupDialog(SpreadsheetSession session, NeraPageSetupTab initialTab = NeraPageSetupTab.Page,
        PresentationLocalization? localization = null, NeraIconTheme theme = NeraIconTheme.Light, Func<bool>? contextIsCurrent = null)
        : base("Thiết lập trang", "nera-page-setup-dialog", localization, theme)
    {
        if (!Enum.IsDefined(initialTab)) throw new ArgumentOutOfRangeException(nameof(initialTab));
        _contextIsCurrent = contextIsCurrent; _draft = new SpreadsheetPageSetupDraft(session); _initial = _draft.Initial;
        try
        {
            _tabs.Items.Add(Tab("page", "Trang", BuildPage()));
            _tabs.Items.Add(Tab("margins", "Lề trang", BuildMargins()));
            _tabs.Items.Add(Tab("sheet", "Trang tính", BuildSheet()));
            _tabs.SelectedIndex = (int)initialTab; AddTabs(_tabs); Closed += (_, _) => _draft.Dispose();
        }
        catch { _draft.Dispose(); throw; }
    }
    public NeraPageSetupTab SelectedPageTab => (NeraPageSetupTab)_tabs.SelectedIndex;
    /// <summary>Releases target subscriptions even when this dialog has never been shown.
    /// Call on the UI thread. Closing a shown dialog also releases its draft.</summary>
    public void Dispose()
    {
        global::Avalonia.Threading.Dispatcher.UIThread.VerifyAccess();
        _draft.Dispose();
        if (IsVisible) Close(false);
        GC.SuppressFinalize(this);
    }

    protected override bool TryApply()
    {
        if (_contextIsCurrent?.Invoke() == false || !_draft.IsCurrent)
            throw new InvalidOperationException(L("Vùng chọn hoặc trang tính đã thay đổi. Hãy mở lại hộp thoại."));
        var setup = _initial.PageSetup;
        foreach (var key in _updates.Keys) if (_dirty.Contains(key)) setup = _updates[key](setup);
        _draft.Apply(_initial with { PageSetup = setup }); return true;
    }
    private void Bind(string id, Control input, Func<SpreadsheetPageSetup, SpreadsheetPageSetup> update)
    {
        _updates.Add(id, update);
        void Mark(bool changed) { if (changed) _dirty.Add(id); else _dirty.Remove(id); }
        if (input is TextBox text) { var initial = text.Text; text.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Mark(text.Text != initial); }; }
        else if (input is ComboBox choice) { var initial = Selected(choice); choice.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, choice)) Mark(Selected(choice) != initial); }; }
        else if (input is CheckBox check) { var initial = check.IsChecked; check.IsCheckedChanged += (_, _) => Mark(check.IsChecked != initial); }
    }
    private StackPanel BuildPage()
    {
        var panel = Panel(); var setup = _initial.PageSetup;
        var orientation = ChoiceField(panel, "page-orientation", "Hướng giấy", [("Portrait", "Dọc"), ("Landscape", "Ngang")], setup.Orientation.ToString());
        Bind("orientation", orientation, value => value with { Orientation = Enum.Parse<SpreadsheetPageOrientation>(Selected(orientation)!) });
        var sizes = new Dictionary<string, SpreadsheetPaperSize> { ["A4"] = SpreadsheetPaperSize.A4, ["A3"] = SpreadsheetPaperSize.A3, ["Letter"] = SpreadsheetPaperSize.Letter, ["Legal"] = SpreadsheetPaperSize.Legal };
        var selected = sizes.FirstOrDefault(pair => pair.Value == setup.PaperSize).Key;
        if (selected is null) { selected = "Imported"; sizes.Add(selected, setup.PaperSize); }
        var paper = ChoiceField(panel, "page-paper", "Khổ giấy", sizes.Select(pair => (pair.Key, pair.Key == "Imported" ? "Khổ giấy từ file" : pair.Key)), selected);
        Bind("paper", paper, value => value with { PaperSize = sizes[Selected(paper)!] });
        var mode = ChoiceField(panel, "page-scale-mode", "Chế độ co giãn", [("scale", "Tỷ lệ phần trăm"), ("fit", "Vừa số trang")], setup.FitToPagesWide.HasValue || setup.FitToPagesTall.HasValue ? "fit" : "scale");
        var scale = TextField(panel, "page-scale", "Tỷ lệ in (%)", setup.ScalePercent.ToString("G", Localization.Culture));
        var wide = TextField(panel, "page-fit-wide", "Số trang theo chiều rộng", setup.FitToPagesWide?.ToString(Localization.Culture));
        var tall = TextField(panel, "page-fit-tall", "Số trang theo chiều cao", setup.FitToPagesTall?.ToString(Localization.Culture));
        Note(panel, "Để trống số trang để tự động. Tỷ lệ in không thay đổi mức thu phóng trên màn hình.");
        SpreadsheetPageSetup Scaling(SpreadsheetPageSetup value)
        {
            if (Selected(mode) != "fit") return value with { ScalePercent = ReadNumber(scale, 10, 400), FitToPagesWide = null, FitToPagesTall = null };
            int? ReadFit(TextBox text) => string.IsNullOrWhiteSpace(text.Text) ? null : ReadInteger(text, 1, 32767);
            var width = ReadFit(wide); var height = ReadFit(tall);
            if (width is null && height is null) throw new ArgumentException(L("Nhập ít nhất một chiều số trang."));
            return value with { FitToPagesWide = width, FitToPagesTall = height };
        }
        Bind("scaleMode", mode, Scaling); Bind("scale", scale, Scaling); Bind("fitWide", wide, Scaling); Bind("fitTall", tall, Scaling);
        void Enable() { var fit = Selected(mode) == "fit"; scale.IsEnabled = !fit; wide.IsEnabled = fit; tall.IsEnabled = fit; }
        mode.SelectionChanged += (_, _) => Enable(); Enable();
        return panel;
    }
    private StackPanel BuildMargins()
    {
        var panel = Panel(); var margins = _initial.PageSetup.Margins;
        var values = new[] { ("left", "Lề trái (mm)", margins.LeftInches), ("right", "Lề phải (mm)", margins.RightInches),
            ("top", "Lề trên (mm)", margins.TopInches), ("bottom", "Lề dưới (mm)", margins.BottomInches),
            ("header", "Đầu trang (mm)", margins.HeaderInches), ("footer", "Cuối trang (mm)", margins.FooterInches) };
        foreach (var (id, label, inches) in values)
        {
            var input = TextField(panel, "page-margin-" + id, label, (inches * 25.4).ToString("0.###", Localization.Culture));
            Bind("margin" + id, input, setup =>
            {
                var value = ReadNumber(input, 0, 1000) / 25.4; var old = setup.Margins;
                return setup with { Margins = new SpreadsheetPageMargins(id == "left" ? value : old.LeftInches, id == "right" ? value : old.RightInches,
                    id == "top" ? value : old.TopInches, id == "bottom" ? value : old.BottomInches,
                    id == "header" ? value : old.HeaderInches, id == "footer" ? value : old.FooterInches) };
            });
        }
        var horizontal = CheckField(panel, "page-center-horizontal", "Giữa trang theo chiều ngang", _initial.PageSetup.CenterHorizontally); horizontal.IsThreeState = false;
        Bind("centerHorizontal", horizontal, setup => setup with { CenterHorizontally = horizontal.IsChecked == true });
        var vertical = CheckField(panel, "page-center-vertical", "Giữa trang theo chiều dọc", _initial.PageSetup.CenterVertically); vertical.IsThreeState = false;
        Bind("centerVertical", vertical, setup => setup with { CenterVertically = vertical.IsChecked == true });
        return panel;
    }
    private StackPanel BuildSheet()
    {
        var panel = Panel();
        var grid = CheckField(panel, "page-gridlines", "In đường lưới", _initial.PageSetup.PrintGridlines); grid.IsThreeState = false;
        Bind("grid", grid, setup => setup with { PrintGridlines = grid.IsChecked == true });
        var headings = CheckField(panel, "page-headings", "In tiêu đề hàng cột", _initial.PageSetup.PrintHeadings); headings.IsThreeState = false;
        Bind("headings", headings, setup => setup with { PrintHeadings = headings.IsChecked == true });
        var header = TextField(panel, "page-header", "Nội dung đầu trang", _initial.PageSetup.OddHeader);
        Bind("header", header, setup => setup with { OddHeader = header.Text });
        var footer = TextField(panel, "page-footer", "Nội dung cuối trang", _initial.PageSetup.OddFooter);
        Bind("footer", footer, setup => setup with { OddFooter = footer.Text });
        Note(panel, "Vùng in, tiêu đề lặp và ngắt trang từ file được giữ nguyên. Chưa có trình soạn đầu/cuối trang đầy đủ như Excel.");
        return panel;
    }
}
