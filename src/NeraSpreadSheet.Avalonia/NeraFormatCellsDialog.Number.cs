using global::Avalonia.Controls;
using global::Avalonia.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia;

public sealed partial class NeraFormatCellsDialog
{
    private StackPanel BuildNumber()
    {
        var panel = Panel();
        Note(panel, _draft.IsSelectionInspected ? "Ô trống hoặc ô có nhiều định dạng: chỉ thuộc tính bạn chỉnh sẽ thay đổi." : "Vùng lớn: không quét toàn bộ ô. Chỉ thuộc tính bạn chỉnh sẽ thay đổi.");
        var current = CommonText(style => style.NumberFormat.FormatCode);
        var initial = DescribeNumberControls(current);
        var categories = Enum.GetValues<SpreadsheetNumberFormatCategory>().Select(value => (value.ToString(), CategoryCaption(value)));
        var category = ChoiceField(panel, "format-category", "Loại định dạng", categories, initial.Category.ToString());
        var decimals = TextField(panel, "format-decimals", "Số chữ số thập phân", initial.Decimals.ToString(Localization.Culture));
        var thousands = CheckField(panel, "format-thousands", "Phân cách hàng nghìn", initial.GroupThousands); thousands.IsThreeState = false;
        var currency = TextField(panel, "format-currency", "Ký hiệu tiền tệ", "₫");
        var parentheses = CheckField(panel, "format-negative", "Số âm trong ngoặc", initial.Parentheses); parentheses.IsThreeState = false;
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
        var synchronizing = false;
        SpreadsheetNumberFormatCategory CurrentCategory() => Enum.TryParse<SpreadsheetNumberFormatCategory>(Selected(category), out var selected)
            ? selected : SpreadsheetNumberFormatCategory.Custom;
        void UpdateAvailability()
        {
            var selected = CurrentCategory();
            decimals.IsEnabled = HasDecimalParameter(selected);
            thousands.IsEnabled = selected is SpreadsheetNumberFormatCategory.Number or SpreadsheetNumberFormatCategory.Currency or SpreadsheetNumberFormatCategory.Accounting;
            currency.IsEnabled = selected is SpreadsheetNumberFormatCategory.Currency or SpreadsheetNumberFormatCategory.Accounting;
            parentheses.IsEnabled = selected is SpreadsheetNumberFormatCategory.Number or SpreadsheetNumberFormatCategory.Currency;
        }
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
            if (synchronizing) return;
            _numberParameterError = null;
            UpdateAvailability();
            var selected = CurrentCategory();
            if (selected == SpreadsheetNumberFormatCategory.Custom) { Preview(); return; }
            synchronizing = true;
            try
            {
                // Ignore irrelevant helper inputs. They are not part of the
                // selected category and must not invalidate an otherwise valid code.
                code.Text = SpreadsheetNumberFormats.Create(selected,
                    decimals.IsEnabled ? ReadInteger(decimals, 0, 15) : 2,
                    thousands.IsEnabled && thousands.IsChecked == true,
                    currency.IsEnabled ? currency.Text ?? string.Empty : "₫",
                    parentheses.IsEnabled && parentheses.IsChecked == true);
                Preview();
            }
            catch (ArgumentException)
            {
                _numberParameterError = L("Thông số tạo định dạng chưa hợp lệ.");
                preview.Text = _numberParameterError;
            }
            finally { synchronizing = false; }
        }
        category.SelectionChanged += (_, e) => { if (ReferenceEquals(e.Source, category)) Generate(); };
        decimals.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && decimals.IsEnabled) Generate(); };
        currency.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && currency.IsEnabled) Generate(); };
        thousands.IsCheckedChanged += (_, _) => { if (thousands.IsEnabled) Generate(); };
        parentheses.IsCheckedChanged += (_, _) => { if (parentheses.IsEnabled) Generate(); };
        code.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;
            if (!synchronizing)
            {
                // A directly entered code is authoritative. Do not let an old
                // generator error block it or regenerate it on a helper event.
                synchronizing = true;
                try
                {
                    _numberParameterError = null;
                    category.SelectedItem = category.Items.OfType<ComboBoxItem>().Single(item =>
                        (string?)item.Tag == nameof(SpreadsheetNumberFormatCategory.Custom));
                    UpdateAvailability();
                }
                finally { synchronizing = false; }
            }
            Preview();
        };
        UpdateAvailability();
        Preview();
        return panel;
    }

    private static bool HasDecimalParameter(SpreadsheetNumberFormatCategory category) => category is
        SpreadsheetNumberFormatCategory.Number or SpreadsheetNumberFormatCategory.Currency or SpreadsheetNumberFormatCategory.Accounting or
        SpreadsheetNumberFormatCategory.Percentage or SpreadsheetNumberFormatCategory.Scientific;

    // Recognize only exact forms supported by the shared code generator. This
    // initializes UI fields; it is neither an XLSX parser nor a cell-value parser.
    // Unknown, locale-qualified, currency and mixed formats stay verbatim Custom.
    private static (SpreadsheetNumberFormatCategory Category, int Decimals, bool GroupThousands, bool Parentheses) DescribeNumberControls(string? code)
    {
        var fixedCategory = code switch
        {
            "General" => SpreadsheetNumberFormatCategory.General,
            "dd/mm/yyyy" => SpreadsheetNumberFormatCategory.Date,
            "hh:mm:ss" => SpreadsheetNumberFormatCategory.Time,
            "# ?/?" => SpreadsheetNumberFormatCategory.Fraction,
            "@" => SpreadsheetNumberFormatCategory.Text,
            _ => SpreadsheetNumberFormatCategory.Custom,
        };
        if (fixedCategory != SpreadsheetNumberFormatCategory.Custom) return (fixedCategory, 2, true, false);
        var fallback = (SpreadsheetNumberFormatCategory.Custom, 2, true, false);
        if (string.IsNullOrEmpty(code) || code.Length > 255) return fallback;
        var body = code;
        var parentheses = false;
        var separator = body.IndexOf(';');
        if (separator >= 0)
        {
            var positive = body[..separator];
            if (!string.Equals(body, positive + ";(" + positive + ")", StringComparison.Ordinal)) return fallback;
            body = positive;
            parentheses = true;
        }
        var category = SpreadsheetNumberFormatCategory.Number;
        if (!parentheses && body.EndsWith('%'))
        {
            category = SpreadsheetNumberFormatCategory.Percentage;
            body = body[..^1];
        }
        else if (!parentheses && body.EndsWith("E+00", StringComparison.Ordinal))
        {
            category = SpreadsheetNumberFormatCategory.Scientific;
            body = body[..^4];
        }
        var groupThousands = body.StartsWith("#,##0", StringComparison.Ordinal);
        if (category != SpreadsheetNumberFormatCategory.Number && groupThousands) return fallback;
        var prefix = groupThousands ? "#,##0" : "0";
        if (!body.StartsWith(prefix, StringComparison.Ordinal)) return fallback;
        var decimals = 0;
        if (body.Length > prefix.Length)
        {
            if (body[prefix.Length] != '.') return fallback;
            decimals = body.Length - prefix.Length - 1;
            if (decimals is < 1 or > 15) return fallback;
            for (var index = prefix.Length + 1; index < body.Length; index++)
                if (body[index] != '0') return fallback;
        }
        return string.Equals(SpreadsheetNumberFormats.Create(category, decimals, groupThousands, negativeParentheses: parentheses), code, StringComparison.Ordinal)
            ? (category, decimals, groupThousands, parentheses) : fallback;
    }
}
