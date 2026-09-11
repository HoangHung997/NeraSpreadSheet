using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

public enum NeraPageSetupTab { Page, Margins, HeaderFooter, Sheet }
public enum NeraPageSetupAction { None, Print, PrintPreview, Options }

/// <summary>Four-tab Page Setup over canonical worksheet print settings. Local fields stay pending
/// until OK/action and untouched imported metadata is preserved without lossy unit conversion.</summary>
public sealed class NeraPageSetupDialog : NeraSettingsDialog, IDisposable
{
    private readonly SpreadsheetPageSetupDraft _draft;
    private readonly WorksheetPrintSettings _initial;
    private readonly Func<bool>? _contextIsCurrent;
    private readonly Dictionary<string, Func<SpreadsheetPageSetup, SpreadsheetPageSetup>> _setupUpdates = [];
    private readonly Dictionary<string, Func<WorksheetPrintSettings, WorksheetPrintSettings>> _settingsUpdates = [];
    private readonly HashSet<string> _dirty = [];
    private readonly TabControl _tabs = new();

    public NeraPageSetupDialog(
        SpreadsheetSession session,
        NeraPageSetupTab initialTab = NeraPageSetupTab.Page,
        PresentationLocalization? localization = null,
        NeraIconTheme theme = NeraIconTheme.Light,
        Func<bool>? contextIsCurrent = null)
        : base("Thiết lập trang", "nera-page-setup-dialog", localization, theme)
    {
        if (!Enum.IsDefined(initialTab)) throw new ArgumentOutOfRangeException(nameof(initialTab));
        _contextIsCurrent = contextIsCurrent;
        _draft = new SpreadsheetPageSetupDraft(session);
        _initial = _draft.Initial;
        try
        {
            _tabs.Items.Add(Tab("page", "Trang", BuildPage()));
            _tabs.Items.Add(Tab("margins", "Lề trang", BuildMargins()));
            _tabs.Items.Add(Tab("header-footer", "Đầu/Chân trang", BuildHeaderFooter()));
            _tabs.Items.Add(Tab("sheet", "Trang tính", BuildSheet()));
            _tabs.SelectedIndex = (int)initialTab;
            AddTabs(_tabs);
            AddPageActions();
            Closed += (_, _) => _draft.Dispose();
        }
        catch { _draft.Dispose(); throw; }
    }

    public NeraPageSetupTab SelectedPageTab => (NeraPageSetupTab)_tabs.SelectedIndex;
    public NeraPageSetupAction RequestedAction { get; private set; }

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
        var settings = _initial;
        foreach (var key in _settingsUpdates.Keys)
            if (_dirty.Contains(key)) settings = _settingsUpdates[key](settings);
        var setup = settings.PageSetup;
        foreach (var key in _setupUpdates.Keys)
            if (_dirty.Contains(key)) setup = _setupUpdates[key](setup);
        _draft.Apply(settings with { PageSetup = setup });
        return true;
    }

    private void BindSetup(string id, Control input, Func<SpreadsheetPageSetup, SpreadsheetPageSetup> update)
    {
        _setupUpdates.Add(id, update);
        BindDirty(id, input);
    }

    private void BindSettings(string id, Control input, Func<WorksheetPrintSettings, WorksheetPrintSettings> update)
    {
        _settingsUpdates.Add(id, update);
        BindDirty(id, input);
    }

    private void BindDirty(string id, Control input)
    {
        void Mark(bool changed) { if (changed) _dirty.Add(id); else _dirty.Remove(id); }
        if (input is TextBox text)
        {
            var initial = text.Text;
            text.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Mark(text.Text != initial); };
        }
        else if (input is ComboBox choice)
        {
            var initial = Selected(choice);
            choice.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, choice)) Mark(Selected(choice) != initial); };
        }
        else if (input is CheckBox check)
        {
            var initial = check.IsChecked;
            check.IsCheckedChanged += (_, _) => Mark(check.IsChecked != initial);
        }
    }

    private StackPanel BuildPage()
    {
        var panel = Panel();
        var setup = _initial.PageSetup;
        var orientation = ChoiceField(panel, "page-orientation", "Hướng giấy", [("Portrait", "Dọc"), ("Landscape", "Ngang")], setup.Orientation.ToString());
        BindSetup("orientation", orientation, value => value with { Orientation = Enum.Parse<SpreadsheetPageOrientation>(Selected(orientation)!) });

        var sizes = new Dictionary<string, SpreadsheetPaperSize> { ["A4"] = SpreadsheetPaperSize.A4, ["A3"] = SpreadsheetPaperSize.A3, ["Letter"] = SpreadsheetPaperSize.Letter, ["Legal"] = SpreadsheetPaperSize.Legal };
        var selected = sizes.FirstOrDefault(pair => pair.Value == setup.PaperSize).Key;
        if (selected is null) { selected = "Imported"; sizes.Add(selected, setup.PaperSize); }
        var paper = ChoiceField(panel, "page-paper", "Khổ giấy", sizes.Select(pair => (pair.Key, pair.Key == "Imported" ? "Khổ giấy từ file" : pair.Key)), selected);
        BindSetup("paper", paper, value => value with { PaperSize = sizes[Selected(paper)!] });

        var mode = ChoiceField(panel, "page-scale-mode", "Chế độ co giãn", [("scale", "Tỷ lệ phần trăm"), ("fit", "Vừa số trang")], setup.FitToPagesWide.HasValue || setup.FitToPagesTall.HasValue ? "fit" : "scale");
        var scale = TextField(panel, "page-scale", "Tỷ lệ in (%)", setup.ScalePercent.ToString("G", Localization.Culture));
        var wide = TextField(panel, "page-fit-wide", "Số trang theo chiều rộng", setup.FitToPagesWide?.ToString(Localization.Culture));
        var tall = TextField(panel, "page-fit-tall", "Số trang theo chiều cao", setup.FitToPagesTall?.ToString(Localization.Culture));
        SpreadsheetPageSetup Scaling(SpreadsheetPageSetup value)
        {
            if (Selected(mode) != "fit") return value with { ScalePercent = ReadNumber(scale, 10, 400), FitToPagesWide = null, FitToPagesTall = null };
            int? ReadFit(TextBox text) => string.IsNullOrWhiteSpace(text.Text) ? null : ReadInteger(text, 1, 32767);
            var width = ReadFit(wide); var height = ReadFit(tall);
            if (width is null && height is null) throw new ArgumentException(L("Nhập ít nhất một chiều số trang."));
            return value with { FitToPagesWide = width, FitToPagesTall = height };
        }
        BindSetup("scaleMode", mode, Scaling); BindSetup("scale", scale, Scaling); BindSetup("fitWide", wide, Scaling); BindSetup("fitTall", tall, Scaling);
        void Enable() { var fit = Selected(mode) == "fit"; scale.IsEnabled = !fit; wide.IsEnabled = fit; tall.IsEnabled = fit; }
        mode.SelectionChanged += (_, _) => Enable(); Enable();

        var quality = TextField(panel, "page-print-quality", "Chất lượng in (DPI)", setup.PrintQualityDpi?.ToString(Localization.Culture));
        BindSetup("quality", quality, value => value with { PrintQualityDpi = ReadOptionalInteger(quality, 1, 9600) });
        var firstPage = TextField(panel, "page-first-page", "Số trang đầu", setup.FirstPageNumber?.ToString(Localization.Culture));
        BindSetup("firstPage", firstPage, value => value with { FirstPageNumber = ReadOptionalInteger(firstPage, 1, 32767) });
        Note(panel, "Để trống số trang đầu để Excel dùng Automatic. Tỷ lệ in không thay đổi mức thu phóng trên màn hình.");
        return panel;
    }

    private StackPanel BuildMargins()
    {
        var panel = Panel();
        var margins = _initial.PageSetup.Margins;
        var values = new[] { ("left", "Lề trái (mm)", margins.LeftInches), ("right", "Lề phải (mm)", margins.RightInches),
            ("top", "Lề trên (mm)", margins.TopInches), ("bottom", "Lề dưới (mm)", margins.BottomInches),
            ("header", "Đầu trang (mm)", margins.HeaderInches), ("footer", "Cuối trang (mm)", margins.FooterInches) };
        foreach (var (id, label, inches) in values)
        {
            var input = TextField(panel, "page-margin-" + id, label, (inches * 25.4).ToString("0.###", Localization.Culture));
            BindSetup("margin" + id, input, setup =>
            {
                var value = ReadNumber(input, 0, 1000) / 25.4; var old = setup.Margins;
                return setup with { Margins = new SpreadsheetPageMargins(id == "left" ? value : old.LeftInches, id == "right" ? value : old.RightInches,
                    id == "top" ? value : old.TopInches, id == "bottom" ? value : old.BottomInches,
                    id == "header" ? value : old.HeaderInches, id == "footer" ? value : old.FooterInches) };
            });
        }
        var horizontal = CheckField(panel, "page-center-horizontal", "Giữa trang theo chiều ngang", _initial.PageSetup.CenterHorizontally); horizontal.IsThreeState = false;
        BindSetup("centerHorizontal", horizontal, setup => setup with { CenterHorizontally = horizontal.IsChecked == true });
        var vertical = CheckField(panel, "page-center-vertical", "Giữa trang theo chiều dọc", _initial.PageSetup.CenterVertically); vertical.IsThreeState = false;
        BindSetup("centerVertical", vertical, setup => setup with { CenterVertically = vertical.IsChecked == true });
        return panel;
    }

    private StackPanel BuildHeaderFooter()
    {
        var panel = Panel();
        var setup = _initial.PageSetup;
        var oddHeader = TextField(panel, "page-odd-header", "Đầu trang trang lẻ", setup.OddHeader);
        BindSetup("oddHeader", oddHeader, value => value with { OddHeader = NullIfEmpty(oddHeader.Text) });
        var oddFooter = TextField(panel, "page-odd-footer", "Chân trang trang lẻ", setup.OddFooter);
        BindSetup("oddFooter", oddFooter, value => value with { OddFooter = NullIfEmpty(oddFooter.Text) });
        var evenHeader = TextField(panel, "page-even-header", "Đầu trang trang chẵn", setup.EvenHeader);
        BindSetup("evenHeader", evenHeader, value => value with { EvenHeader = NullIfEmpty(evenHeader.Text) });
        var evenFooter = TextField(panel, "page-even-footer", "Chân trang trang chẵn", setup.EvenFooter);
        BindSetup("evenFooter", evenFooter, value => value with { EvenFooter = NullIfEmpty(evenFooter.Text) });
        var firstHeader = TextField(panel, "page-first-header", "Đầu trang trang đầu", setup.FirstHeader);
        BindSetup("firstHeader", firstHeader, value => value with { FirstHeader = NullIfEmpty(firstHeader.Text) });
        var firstFooter = TextField(panel, "page-first-footer", "Chân trang trang đầu", setup.FirstFooter);
        BindSetup("firstFooter", firstFooter, value => value with { FirstFooter = NullIfEmpty(firstFooter.Text) });
        var oddEven = CheckField(panel, "page-different-odd-even", "Trang chẵn và lẻ khác nhau", setup.DifferentOddEvenPages); oddEven.IsThreeState = false;
        BindSetup("oddEven", oddEven, value => value with { DifferentOddEvenPages = oddEven.IsChecked == true });
        var first = CheckField(panel, "page-different-first", "Trang đầu khác", setup.DifferentFirstPage); first.IsThreeState = false;
        BindSetup("differentFirst", first, value => value with { DifferentFirstPage = first.IsChecked == true });
        var scale = CheckField(panel, "page-header-scale", "Co giãn theo tài liệu", setup.ScaleHeaderFooterWithDocument); scale.IsThreeState = false;
        BindSetup("headerScale", scale, value => value with { ScaleHeaderFooterWithDocument = scale.IsChecked == true });
        var align = CheckField(panel, "page-header-align", "Căn theo lề trang", setup.AlignHeaderFooterWithMargins); align.IsThreeState = false;
        BindSetup("headerAlign", align, value => value with { AlignHeaderFooterWithMargins = align.IsChecked == true });
        Note(panel, "Hỗ trợ mã Excel như &P (trang), &N (tổng trang), &F (tên file), &A (tên sheet). Các chuỗi được round-trip qua XLSX.");
        return panel;
    }

    private StackPanel BuildSheet()
    {
        var panel = Panel();
        var setup = _initial.PageSetup;
        var area = TextField(panel, "page-print-area", "Vùng in", FormatRange(_initial.PrintArea));
        BindSettings("printArea", area, settings => settings with { PrintArea = ParseRange(area.Text) });
        var rows = TextField(panel, "page-repeat-rows", "Hàng lặp lại phía trên", FormatRows(setup.RepeatTitles.Rows));
        BindSetup("repeatRows", rows, value => value with { RepeatTitles = new SpreadsheetRepeatTitles(ParseRows(rows.Text), value.RepeatTitles.Columns) });
        var columns = TextField(panel, "page-repeat-columns", "Cột lặp lại bên trái", FormatColumns(setup.RepeatTitles.Columns));
        BindSetup("repeatColumns", columns, value => value with { RepeatTitles = new SpreadsheetRepeatTitles(value.RepeatTitles.Rows, ParseColumns(columns.Text)) });

        var grid = CheckField(panel, "page-gridlines", "In đường lưới", setup.PrintGridlines); grid.IsThreeState = false;
        BindSetup("grid", grid, value => value with { PrintGridlines = grid.IsChecked == true });
        var headings = CheckField(panel, "page-headings", "In tiêu đề hàng cột", setup.PrintHeadings); headings.IsThreeState = false;
        BindSetup("headings", headings, value => value with { PrintHeadings = headings.IsChecked == true });
        var black = CheckField(panel, "page-black-white", "Đen trắng", setup.BlackAndWhite); black.IsThreeState = false;
        BindSetup("black", black, value => value with { BlackAndWhite = black.IsChecked == true });
        var draft = CheckField(panel, "page-draft-quality", "Chất lượng nháp", setup.DraftQuality); draft.IsThreeState = false;
        BindSetup("draft", draft, value => value with { DraftQuality = draft.IsChecked == true });

        var comments = ChoiceField(panel, "page-comments", "Chú thích", [("None", "Không"), ("AsDisplayed", "Như hiển thị trên trang tính"), ("AtEnd", "Cuối trang tính")], setup.PrintComments.ToString());
        BindSetup("comments", comments, value => value with { PrintComments = Enum.Parse<SpreadsheetPrintComments>(Selected(comments)!) });
        var errors = ChoiceField(panel, "page-errors", "Lỗi ô khi in", [("Displayed", "Như hiển thị"), ("Blank", "Trống"), ("Dash", "Dấu gạch"), ("NotAvailable", "#N/A")], setup.PrintErrors.ToString());
        BindSetup("errors", errors, value => value with { PrintErrors = Enum.Parse<SpreadsheetPrintErrors>(Selected(errors)!) });
        var order = ChoiceField(panel, "page-order", "Thứ tự trang", [("DownThenOver", "Xuống rồi sang"), ("OverThenDown", "Sang rồi xuống")], setup.PageOrder.ToString());
        BindSetup("pageOrder", order, value => value with { PageOrder = Enum.Parse<SpreadsheetPageOrder>(Selected(order)!) });
        Note(panel, "Vùng in dùng A1:B20; hàng lặp dùng 1:2; cột lặp dùng A:B. Để trống để xóa thiết lập tương ứng.");
        return panel;
    }

    private void AddPageActions()
    {
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, Spacing = 8, Margin = new Thickness(16, 0, 16, 4) };
        AddAction(actions, "page-action-print", "In", NeraPageSetupAction.Print);
        AddAction(actions, "page-action-preview", "Xem trước in", NeraPageSetupAction.PrintPreview);
        AddAction(actions, "page-action-options", "Tùy chọn", NeraPageSetupAction.Options);
        DialogBody.Children.Add(actions);
    }

    private void AddAction(StackPanel parent, string id, string caption, NeraPageSetupAction action)
    {
        var button = new Button { Content = L(caption), MinWidth = 100 };
        Identify(button, id, L(caption));
        button.Click += (_, _) => RequestedAction = action;
        parent.Children.Add(button);
    }

    private int? ReadOptionalInteger(TextBox input, int minimum, int maximum) =>
        string.IsNullOrWhiteSpace(input.Text) ? null : ReadInteger(input, minimum, maximum);

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;
    private static string? FormatRange(CellRange? range) => range is { } value ? $"{value.TopLeft.ToA1()}:{value.BottomRight.ToA1()}" : null;
    private static string? FormatRows(CellRange? range) => range is { } value ? $"{value.Top + 1}:{value.Bottom + 1}" : null;
    private static string? FormatColumns(CellRange? range) => range is { } value ? $"{ColumnName(value.Left)}:{ColumnName(value.Right)}" : null;

    private static CellRange? ParseRange(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = text.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        var pieces = normalized.Split(':', StringSplitOptions.TrimEntries);
        if (pieces.Length is < 1 or > 2 || !CellAddress.TryParseA1(pieces[0], out var first) ||
            !CellAddress.TryParseA1(pieces.Length == 2 ? pieces[1] : pieces[0], out var second))
            throw new ArgumentException("Vùng in phải dùng địa chỉ A1, ví dụ A1:F100.");
        return new CellRange(first, second);
    }

    private static CellRange? ParseRows(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var pieces = text.Replace("$", string.Empty, StringComparison.Ordinal).Split(':', StringSplitOptions.TrimEntries);
        if (pieces.Length is < 1 or > 2 || !int.TryParse(pieces[0], out var first) ||
            !int.TryParse(pieces.Length == 2 ? pieces[1] : pieces[0], out var second) || first <= 0 || second <= 0 ||
            first > SpreadsheetLimits.MaxRows || second > SpreadsheetLimits.MaxRows)
            throw new ArgumentException("Hàng lặp phải dùng dạng 1:2.");
        return new CellRange(new CellAddress(Math.Min(first, second) - 1, 0), new CellAddress(Math.Max(first, second) - 1, SpreadsheetLimits.MaxColumns - 1));
    }

    private static CellRange? ParseColumns(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var pieces = text.Replace("$", string.Empty, StringComparison.Ordinal).Split(':', StringSplitOptions.TrimEntries);
        if (pieces.Length is < 1 or > 2 || !TryColumn(pieces[0], out var first) || !TryColumn(pieces.Length == 2 ? pieces[1] : pieces[0], out var second))
            throw new ArgumentException("Cột lặp phải dùng dạng A:B.");
        return new CellRange(new CellAddress(0, Math.Min(first, second)), new CellAddress(SpreadsheetLimits.MaxRows - 1, Math.Max(first, second)));
    }

    private static bool TryColumn(string text, out int column)
    {
        column = -1;
        if (!CellAddress.TryParseA1(text.Trim() + "1", out var address)) return false;
        column = address.ColumnIndex;
        return true;
    }

    private static string ColumnName(int column) => new CellAddress(0, column).ToA1().TrimEnd('1');
}
