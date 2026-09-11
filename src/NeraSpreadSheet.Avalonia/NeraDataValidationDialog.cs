using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

public sealed class NeraDataValidationDialog : NeraSettingsDialog, IDisposable
{
    private readonly SpreadsheetDataValidationDraft _draft;
    private readonly ComboBox _type;
    private readonly ComboBox _operator;
    private readonly TextBox _formula1;
    private readonly TextBox _formula2;
    private readonly CheckBox _allowBlank;
    private readonly CheckBox _dropDown;
    private readonly CheckBox _showInput;
    private readonly TextBox _promptTitle;
    private readonly TextBox _prompt;
    private readonly CheckBox _showError;
    private readonly ComboBox _errorStyle;
    private readonly TextBox _errorTitle;
    private readonly TextBox _error;
    private bool _clearRequested;

    public NeraDataValidationDialog(
        SpreadsheetSession session,
        PresentationLocalization? localization = null,
        NeraIconTheme theme = NeraIconTheme.Light)
        : base("Xác thực dữ liệu", "nera-data-validation-dialog", localization, theme)
    {
        Width = 680; Height = 620; MinWidth = 560; MinHeight = 500;
        _draft = new SpreadsheetDataValidationDraft(session);
        var existing = _draft.ExistingRule;
        try
        {
            var settings = Panel();
            _type = ChoiceField(settings, "validation-type", "Cho phép", Enum.GetValues<DataValidationType>().Select(value => (value.ToString(), TypeCaption(value))), existing?.Type.ToString() ?? DataValidationType.Whole.ToString());
            _operator = ChoiceField(settings, "validation-operator", "Dữ liệu", Enum.GetValues<DataValidationOperator>().Select(value => (value.ToString(), OperatorCaption(value))), existing?.Operator?.ToString() ?? DataValidationOperator.Between.ToString());
            _formula1 = TextField(settings, "validation-formula1", "Giá trị / Nguồn / Công thức", existing?.Formula1?.TrimStart('='));
            _formula2 = TextField(settings, "validation-formula2", "Giá trị thứ hai", existing?.Formula2?.TrimStart('='));
            _allowBlank = CheckField(settings, "validation-allow-blank", "Bỏ qua ô trống", existing?.AllowBlank ?? true); _allowBlank.IsThreeState = false;
            _dropDown = CheckField(settings, "validation-show-dropdown", "Hiển thị danh sách thả xuống trong ô", existing?.ShowDropDown ?? true); _dropDown.IsThreeState = false;
            var clear = new Button { Content = L("Xóa tất cả"), MinWidth = 110, HorizontalAlignment = HorizontalAlignment.Left };
            AutomationProperties.SetAutomationId(clear, "validation-clear-all");
            clear.Click += (_, _) => _clearRequested = true;
            settings.Children.Add(clear);
            Note(settings, "Danh sách có thể dùng chuỗi phân tách bằng dấu phẩy hoặc tham chiếu vùng/công thức. Custom dùng công thức trả về TRUE/FALSE.");

            var input = Panel();
            _showInput = CheckField(input, "validation-show-input", "Hiển thị thông báo khi chọn ô", existing?.ShowInputMessage ?? false); _showInput.IsThreeState = false;
            _promptTitle = TextField(input, "validation-prompt-title", "Tiêu đề", existing?.PromptTitle);
            _prompt = TextField(input, "validation-prompt", "Thông báo", existing?.Prompt);

            var error = Panel();
            _showError = CheckField(error, "validation-show-error", "Hiển thị cảnh báo khi dữ liệu không hợp lệ", existing?.ShowErrorMessage ?? true); _showError.IsThreeState = false;
            _errorStyle = ChoiceField(error, "validation-error-style", "Kiểu cảnh báo", Enum.GetValues<DataValidationErrorStyle>().Select(value => (value.ToString(), ErrorCaption(value))), existing?.ErrorStyle.ToString() ?? DataValidationErrorStyle.Stop.ToString());
            _errorTitle = TextField(error, "validation-error-title", "Tiêu đề", existing?.ErrorTitle);
            _error = TextField(error, "validation-error-message", "Thông báo lỗi", existing?.Error);

            var tabs = new TabControl();
            tabs.Items.Add(Tab("settings", "Thiết đặt", settings));
            tabs.Items.Add(Tab("input", "Thông báo nhập", input));
            tabs.Items.Add(Tab("error", "Cảnh báo lỗi", error));
            AddTabs(tabs);
            _type.SelectionChanged += (_, _) => RefreshTypeState();
            _operator.SelectionChanged += (_, _) => RefreshTypeState();
            RefreshTypeState();
            Closed += (_, _) => _draft.Dispose();
        }
        catch { _draft.Dispose(); throw; }
    }

    public void Dispose()
    {
        global::Avalonia.Threading.Dispatcher.UIThread.VerifyAccess();
        _draft.Dispose();
        if (IsVisible) Close(false);
        GC.SuppressFinalize(this);
    }

    protected override bool TryApply()
    {
        if (_clearRequested)
        {
            _draft.Apply(null);
            return true;
        }
        var type = Enum.Parse<DataValidationType>(Selected(_type)!);
        DataValidationOperator? op = type is DataValidationType.List or DataValidationType.Custom
            ? null
            : Enum.Parse<DataValidationOperator>(Selected(_operator)!);
        var first = _formula1.Text;
        ArgumentException.ThrowIfNullOrWhiteSpace(first);
        string? second = op is DataValidationOperator.Between or DataValidationOperator.NotBetween
            ? _formula2.Text
            : null;
        if (op is DataValidationOperator.Between or DataValidationOperator.NotBetween)
            ArgumentException.ThrowIfNullOrWhiteSpace(second);

        var existing = _draft.ExistingRule;
        var rule = new DataValidationRule(
            existing?.Id ?? Guid.NewGuid(),
            _draft.TargetRanges,
            type,
            op,
            first,
            second,
            _allowBlank.IsChecked == true,
            _showInput.IsChecked == true,
            _promptTitle.Text,
            _prompt.Text,
            _showError.IsChecked == true,
            Enum.Parse<DataValidationErrorStyle>(Selected(_errorStyle)!),
            _errorTitle.Text,
            _error.Text,
            _dropDown.IsChecked == true);
        _draft.Apply(rule);
        return true;
    }

    private void RefreshTypeState()
    {
        var type = Enum.Parse<DataValidationType>(Selected(_type) ?? DataValidationType.Whole.ToString());
        var listOrCustom = type is DataValidationType.List or DataValidationType.Custom;
        _operator.IsEnabled = !listOrCustom;
        _dropDown.IsEnabled = type == DataValidationType.List;
        var selectedOperator = Selected(_operator);
        _formula2.IsEnabled = !listOrCustom && selectedOperator is nameof(DataValidationOperator.Between) or nameof(DataValidationOperator.NotBetween);
    }

    private string TypeCaption(DataValidationType value) => value switch
    {
        DataValidationType.Whole => L("Số nguyên"),
        DataValidationType.Decimal => L("Số thập phân"),
        DataValidationType.List => L("Danh sách"),
        DataValidationType.Date => L("Ngày"),
        DataValidationType.Time => L("Thời gian"),
        DataValidationType.TextLength => L("Độ dài văn bản"),
        _ => L("Tùy chỉnh"),
    };

    private string OperatorCaption(DataValidationOperator value) => value switch
    {
        DataValidationOperator.Between => L("nằm giữa"),
        DataValidationOperator.NotBetween => L("không nằm giữa"),
        DataValidationOperator.Equal => L("bằng"),
        DataValidationOperator.NotEqual => L("khác"),
        DataValidationOperator.GreaterThan => L("lớn hơn"),
        DataValidationOperator.LessThan => L("nhỏ hơn"),
        DataValidationOperator.GreaterThanOrEqual => L("lớn hơn hoặc bằng"),
        _ => L("nhỏ hơn hoặc bằng"),
    };

    private string ErrorCaption(DataValidationErrorStyle value) => value switch
    {
        DataValidationErrorStyle.Stop => L("Dừng"),
        DataValidationErrorStyle.Warning => L("Cảnh báo"),
        _ => L("Thông tin"),
    };
}
