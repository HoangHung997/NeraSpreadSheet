using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Media;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

public enum NeraFormatCellsTab { Number, Font, Alignment, Border, Fill }

/// <summary>Reusable Format Cells UI. Native controls edit only a pending property patch;
/// OK uses the existing shared formatting transaction. Cancel and previews do not mutate cells.</summary>
public sealed class NeraFormatCellsDialog : NeraSettingsDialog
{
    private readonly SpreadsheetFormatCellsDraft _draft;
    private readonly SpreadsheetSession _session;
    private readonly Func<bool>? _contextIsCurrent;
    private readonly Dictionary<string, Func<CellStylePatch, CellStylePatch>> _updates = [];
    private readonly HashSet<string> _dirty = [];
    private readonly TabControl _tabs = new();

    public NeraFormatCellsDialog(SpreadsheetSession session, NeraFormatCellsTab initialTab = NeraFormatCellsTab.Number,
        PresentationLocalization? localization = null, NeraIconTheme theme = NeraIconTheme.Light, Func<bool>? contextIsCurrent = null)
        : base("Định dạng ô", "nera-format-cells-dialog", localization, theme)
    {
        if (!Enum.IsDefined(initialTab)) throw new ArgumentOutOfRangeException(nameof(initialTab));
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _contextIsCurrent = contextIsCurrent;
        _draft = new SpreadsheetFormatCellsDraft(session);
        try
        {
            _tabs.Items.Add(Tab("number", "Số", BuildNumber()));
            _tabs.Items.Add(Tab("font", "Phông chữ", BuildFont()));
            _tabs.Items.Add(Tab("alignment", "Căn chỉnh", BuildAlignment()));
            _tabs.Items.Add(Tab("border", "Đường viền", BuildBorder()));
            _tabs.Items.Add(Tab("fill", "Màu nền", BuildFill()));
            _tabs.SelectedIndex = (int)initialTab;
            AddTabs(_tabs);
            Closed += (_, _) => _draft.Dispose();
        }
        catch { _draft.Dispose(); throw; }
    }

    public NeraFormatCellsTab SelectedFormatTab => (NeraFormatCellsTab)_tabs.SelectedIndex;
    public bool HasPendingChanges => _dirty.Count > 0;
    protected override bool TryApply()
    {
        if (_contextIsCurrent?.Invoke() == false || !_draft.IsCurrent)
            throw new InvalidOperationException(L("Vùng chọn hoặc trang tính đã thay đổi. Hãy mở lại hộp thoại."));
        var patch = new CellStylePatch();
        foreach (var key in _updates.Keys)
            if (_dirty.Contains(key)) patch = _updates[key](patch);
        _draft.Apply(patch);
        return true;
    }
    private string? CommonText(Func<CellStyle, string> selector) => _draft.TryGetCommon(selector, out var value) ? value : null;
    private bool? CommonFlag(Func<CellStyle, bool> selector) => _draft.TryGetCommon(selector, out var value) ? value : null;
    private string? CommonNumber(Func<CellStyle, double> selector) => _draft.TryGetCommon(selector, out var value) ? value.ToString("G", Localization.Culture) : null;
    private void Bind(string key, Control control, Func<CellStylePatch, CellStylePatch> update)
    {
        _updates.Add(key, update);
        if (control is TextBox text) text.TextChanged += (_, _) => _dirty.Add(key);
        else if (control is ComboBox choice) choice.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, choice)) _dirty.Add(key); };
        else if (control is CheckBox check) check.IsCheckedChanged += (_, _) => _dirty.Add(key);
    }
    private StackPanel BuildNumber()
    {
        var panel = Panel();
        Note(panel, _draft.IsSelectionInspected ? "Ô trống hoặc ô có nhiều định dạng: chỉ thuộc tính bạn chỉnh sẽ thay đổi." : "Vùng lớn: không quét toàn bộ ô. Chỉ thuộc tính bạn chỉnh sẽ thay đổi.");
        var current = CommonText(style => style.NumberFormat.FormatCode);
        var categories = Enum.GetValues<SpreadsheetNumberFormatCategory>().Select(value => (value.ToString(), CategoryCaption(value)));
        var category = ChoiceField(panel, "format-category", "Loại định dạng", categories, current == "General" ? "General" : "Custom");
        var decimals = TextField(panel, "format-decimals", "Số chữ số thập phân", "2");
        var thousands = CheckField(panel, "format-thousands", "Phân cách hàng nghìn", true); thousands.IsThreeState = false;
        var currency = TextField(panel, "format-currency", "Ký hiệu tiền tệ", "₫");
        var parentheses = CheckField(panel, "format-negative", "Số âm trong ngoặc", false); parentheses.IsThreeState = false;
        var code = TextField(panel, "format-code", "Mã định dạng", current);
        var preview = new TextBlock { TextWrapping = TextWrapping.Wrap, MinHeight = 40, FontSize = 18 };
        Identify(preview, "format-preview", L("Mẫu hiển thị")); panel.Children.Add(preview);
        Note(panel, "Mẫu dùng bộ định dạng hiện tại; mã tùy chỉnh và căn khoảng trắng kế toán chưa đảm bảo giống Excel hoàn toàn. Giá trị và công thức gốc không đổi.");
        Bind("number", code, patch =>
        {
            var value = code.Text?.Trim();
            SpreadsheetNumberFormats.Validate(value ?? string.Empty);
            return patch with { NumberFormatCode = value };
        });
        void Preview()
        {
            var value = _draft.PreviewValue.IsBlank ? CellValue.FromObject(1234.567) : _draft.PreviewValue;
            try
            {
                if (string.IsNullOrWhiteSpace(code.Text)) { preview.Text = L("Giữ nguyên / nhiều giá trị"); return; }
                SpreadsheetNumberFormats.Validate(code.Text);
                preview.Text = L("Mẫu hiển thị") + ": " + ExcelCellValueFormatter.Format(value, code.Text, _session.Workbook.DateSystem, Localization.Culture);
            }
            catch (ArgumentException) { preview.Text = L("Mã định dạng chưa hợp lệ."); }
        }
        void Generate()
        {
            if (!Enum.TryParse<SpreadsheetNumberFormatCategory>(Selected(category), out var selected) || selected == SpreadsheetNumberFormatCategory.Custom) return;
            try { code.Text = SpreadsheetNumberFormats.Create(selected, ReadInteger(decimals, 0, 15), thousands.IsChecked == true, currency.Text ?? string.Empty, parentheses.IsChecked == true); }
            catch (ArgumentException) { preview.Text = L("Thông số tạo định dạng chưa hợp lệ."); }
        }
        category.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, category)) Generate(); };
        decimals.TextChanged += (_, _) => Generate(); currency.TextChanged += (_, _) => Generate();
        thousands.IsCheckedChanged += (_, _) => Generate(); parentheses.IsCheckedChanged += (_, _) => Generate();
        code.TextChanged += (_, _) => Preview(); Preview();
        return panel;
    }
    private StackPanel BuildFont()
    {
        var panel = Panel();
        var family = TextField(panel, "format-font-family", "Phông chữ", CommonText(style => style.Font.Family));
        Bind("family", family, patch => { ArgumentException.ThrowIfNullOrWhiteSpace(family.Text); return patch with { FontFamily = family.Text.Trim() }; });
        var size = TextField(panel, "format-font-size", "Cỡ chữ", CommonNumber(style => style.Font.Size));
        Bind("size", size, patch => patch with { FontSize = ReadNumber(size, 1, 409) });
        var bold = CheckField(panel, "format-font-bold", "Đậm", CommonFlag(style => style.Font.Weight >= 600));
        Bind("bold", bold, patch => patch with { FontWeight = bold.IsChecked is { } value ? value ? 700 : 400 : null });
        var italic = CheckField(panel, "format-font-italic", "Nghiêng", CommonFlag(style => style.Font.Italic));
        Bind("italic", italic, patch => patch with { FontItalic = italic.IsChecked });
        var strike = CheckField(panel, "format-font-strike", "Gạch ngang", CommonFlag(style => style.Font.StrikeThrough));
        Bind("strike", strike, patch => patch with { FontStrikeThrough = strike.IsChecked });
        var underline = ChoiceField(panel, "format-font-underline", "Gạch chân", [("none", "Không"), ("single", "Đơn"), ("double", "Kép")],
            CommonText(style => style.Font.DoubleUnderline ? "double" : style.Font.Underline ? "single" : "none"));
        Bind("underline", underline, patch => Selected(underline) is { } value ? patch with { FontUnderline = value == "single", FontDoubleUnderline = value == "double" } : patch);
        var script = ChoiceField(panel, "format-font-script", "Chỉ số", [("None", "Bình thường"), ("Superscript", "Chỉ số trên"), ("Subscript", "Chỉ số dưới")], CommonText(style => style.Font.VerticalAlignment.ToString()));
        Bind("script", script, patch => Selected(script) is { } value ? patch with { FontVerticalAlignment = Enum.Parse<CellFontVerticalAlignment>(value) } : patch);
        var color = TextField(panel, "format-font-color", "Màu chữ (#RRGGBB)", CommonText(style => ColorText(style.Font.Color)));
        Bind("fontColor", color, patch => patch with { FontColor = ParseColor(color.Text) });
        Note(panel, "Phông chữ cần có trên máy chạy. Các thuộc tính không chỉnh được giữ nguyên.");
        return panel;
    }
    private StackPanel BuildAlignment()
    {
        var panel = Panel();
        var horizontal = ChoiceField(panel, "format-align-horizontal", "Căn ngang", Enum.GetValues<CellHorizontalAlignment>().Select(value => (value.ToString(), HorizontalCaption(value))), CommonText(style => style.Alignment.Horizontal.ToString()));
        Bind("horizontal", horizontal, patch => Selected(horizontal) is { } value ? patch with { HorizontalAlignment = Enum.Parse<CellHorizontalAlignment>(value) } : patch);
        var vertical = ChoiceField(panel, "format-align-vertical", "Căn dọc", Enum.GetValues<CellVerticalAlignment>().Select(value => (value.ToString(), VerticalCaption(value))), CommonText(style => style.Alignment.Vertical.ToString()));
        Bind("vertical", vertical, patch => Selected(vertical) is { } value ? patch with { VerticalAlignment = Enum.Parse<CellVerticalAlignment>(value) } : patch);
        var wrap = CheckField(panel, "format-align-wrap", "Ngắt dòng", CommonFlag(style => style.Alignment.WrapText));
        Bind("wrap", wrap, patch => patch with { WrapText = wrap.IsChecked });
        var shrink = CheckField(panel, "format-align-shrink", "Thu nhỏ vừa ô", CommonFlag(style => style.Alignment.ShrinkToFit));
        Bind("shrink", shrink, patch => patch with { ShrinkToFit = shrink.IsChecked });
        var indent = TextField(panel, "format-align-indent", "Thụt lề", CommonNumber(style => style.Alignment.Indent));
        Bind("indent", indent, patch => patch with { Indent = ReadInteger(indent, 0, 250) });
        var rotation = TextField(panel, "format-align-rotation", "Góc xoay chữ", CommonNumber(style => style.Alignment.TextRotationDegrees));
        Bind("rotation", rotation, patch => patch with { TextRotationDegrees = ReadInteger(rotation, -90, 90) });
        Note(panel, "Gộp ô là lệnh riêng trên Ribbon; không tự gộp hoặc xóa dữ liệu khi căn chỉnh.");
        return panel;
    }
    private StackPanel BuildBorder()
    {
        var panel = Panel();
        Note(panel, "Đường viền áp dụng cho từng ô. Chọn một mẫu để thay toàn bộ đường viền; không chọn thì giữ nguyên.");
        var preset = ChoiceField(panel, "format-border-preset", "Mẫu đường viền", [("none", "Không viền"), ("all", "Tất cả đường viền"), ("bottom", "Viền dưới")], null);
        var line = ChoiceField(panel, "format-border-line", "Kiểu nét", [("Thin", "Mảnh"), ("Medium", "Vừa"), ("Thick", "Dày"), ("Dashed", "Nét đứt"), ("Dotted", "Nét chấm"), ("DoubleLine", "Nét đôi")], "Thin");
        var color = TextField(panel, "format-border-color", "Màu viền (#RRGGBB)", "#000000");
        CellStylePatch Update(CellStylePatch patch)
        {
            var selected = Selected(preset);
            if (selected is null) return patch;
            if (selected == "none") return patch with { Border = new CellBorderStyle() };
            var style = Enum.Parse<CellBorderLineStyle>(Selected(line) ?? "Thin");
            var side = new CellBorderSide { Style = style, Color = ParseColor(color.Text), Width = style == CellBorderLineStyle.Thick ? 3 : style == CellBorderLineStyle.Medium ? 2 : 1 };
            var border = selected == "all" ? new CellBorderStyle { Left = side, Top = side, Right = side, Bottom = side } : new CellBorderStyle { Bottom = side };
            return patch with { Border = border };
        }
        Bind("borderPreset", preset, Update); Bind("borderLine", line, Update); Bind("borderColor", color, Update);
        return panel;
    }
    private StackPanel BuildFill()
    {
        var panel = Panel();
        var enabled = CheckField(panel, "format-fill-enabled", "Dùng màu nền", CommonFlag(style => style.Fill.IsVisible));
        var color = TextField(panel, "format-fill-color", "Màu nền (#RRGGBB)", CommonText(style => ColorText(style.Fill.Color)));
        CellStylePatch Update(CellStylePatch patch)
        {
            if (enabled.IsChecked == false) return patch with { Fill = new CellFillStyle() };
            if (enabled.IsChecked is null && !_dirty.Contains("fillColor")) return patch;
            return patch with { Fill = new CellFillStyle { IsVisible = true, Pattern = CellFillPattern.Solid, Color = ParseColor(color.Text) } };
        }
        Bind("fillEnabled", enabled, Update); Bind("fillColor", color, Update);
        Note(panel, "Mẫu tô khác trong file được giữ khi không chỉnh; thay màu nền sẽ dùng màu đặc.");
        return panel;
    }
    private static ColorRgba ParseColor(string? text)
    {
        if (!Color.TryParse(text, out var color)) throw new ArgumentException("Use a valid color, for example #217346.");
        return new ColorRgba(color.R, color.G, color.B, color.A);
    }
    private static string ColorText(ColorRgba color) => $"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
    private static string CategoryCaption(SpreadsheetNumberFormatCategory value) => value switch
    {
        SpreadsheetNumberFormatCategory.General => "Chung", SpreadsheetNumberFormatCategory.Number => "Số",
        SpreadsheetNumberFormatCategory.Currency => "Tiền tệ", SpreadsheetNumberFormatCategory.Accounting => "Kế toán",
        SpreadsheetNumberFormatCategory.Date => "Ngày tháng", SpreadsheetNumberFormatCategory.Time => "Thời gian",
        SpreadsheetNumberFormatCategory.Percentage => "Phần trăm", SpreadsheetNumberFormatCategory.Fraction => "Phân số",
        SpreadsheetNumberFormatCategory.Scientific => "Khoa học", SpreadsheetNumberFormatCategory.Text => "Văn bản", _ => "Tùy chỉnh",
    };
    private static string HorizontalCaption(CellHorizontalAlignment value) => value switch
    {
        CellHorizontalAlignment.General => "Chung", CellHorizontalAlignment.Left => "Căn trái", CellHorizontalAlignment.Center => "Căn giữa", CellHorizontalAlignment.Right => "Căn phải",
        CellHorizontalAlignment.Fill => "Lấp đầy", CellHorizontalAlignment.Justify => "Căn đều", CellHorizontalAlignment.CenterContinuous => "Giữa vùng chọn", _ => "Phân bố đều",
    };
    private static string VerticalCaption(CellVerticalAlignment value) => value switch
    { CellVerticalAlignment.Top => "Trên", CellVerticalAlignment.Center => "Giữa", CellVerticalAlignment.Bottom => "Dưới", CellVerticalAlignment.Justify => "Căn đều", _ => "Phân bố đều" };
}
