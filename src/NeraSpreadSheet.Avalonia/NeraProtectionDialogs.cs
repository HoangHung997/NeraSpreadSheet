using global::Avalonia.Controls;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

public sealed class NeraProtectSheetDialog : NeraSettingsDialog
{
    private readonly SpreadsheetProtectionController _controller;
    private readonly WorksheetProtectionSettings _current;
    private readonly CheckBox _enabled;
    private readonly TextBox _password;
    private readonly TextBox _confirm;
    private readonly Dictionary<string, CheckBox> _permissions = [];

    public NeraProtectSheetDialog(SpreadsheetSession session, PresentationLocalization? localization = null, NeraIconTheme theme = NeraIconTheme.Light)
        : base("Bảo vệ trang tính", "nera-protect-sheet-dialog", localization, theme)
    {
        _controller = new SpreadsheetProtectionController(session ?? throw new ArgumentNullException(nameof(session)));
        _current = _controller.WorksheetSettings;
        Width = 650; Height = 680; MinWidth = 540; MinHeight = 520;
        var panel = Panel();
        _enabled = CheckField(panel, "protect-sheet-enabled", "Bảo vệ trang tính và nội dung các ô bị khóa", _current.Enabled || !_current.Enabled); _enabled.IsThreeState = false;
        _password = TextField(panel, "protect-sheet-password", _current.Enabled ? "Mật khẩu hiện tại" : "Mật khẩu (tùy chọn)", null); _password.PasswordChar = '●';
        _confirm = TextField(panel, "protect-sheet-confirm", "Xác nhận mật khẩu", null); _confirm.PasswordChar = '●'; _confirm.IsEnabled = !_current.Enabled;
        AddPermission(panel, "select-locked", "Chọn ô bị khóa", _current.SelectLockedCells);
        AddPermission(panel, "select-unlocked", "Chọn ô không khóa", _current.SelectUnlockedCells);
        AddPermission(panel, "format-cells", "Định dạng ô", _current.AllowFormatCells);
        AddPermission(panel, "format-columns", "Định dạng cột", _current.AllowFormatColumns);
        AddPermission(panel, "format-rows", "Định dạng hàng", _current.AllowFormatRows);
        AddPermission(panel, "insert-columns", "Chèn cột", _current.AllowInsertColumns);
        AddPermission(panel, "insert-rows", "Chèn hàng", _current.AllowInsertRows);
        AddPermission(panel, "insert-hyperlinks", "Chèn siêu liên kết", _current.AllowInsertHyperlinks);
        AddPermission(panel, "delete-columns", "Xóa cột", _current.AllowDeleteColumns);
        AddPermission(panel, "delete-rows", "Xóa hàng", _current.AllowDeleteRows);
        AddPermission(panel, "sort", "Sắp xếp", _current.AllowSort);
        AddPermission(panel, "auto-filter", "Dùng AutoFilter", _current.AllowAutoFilter);
        AddPermission(panel, "pivot", "Dùng PivotTable", _current.AllowPivotTables);
        AddPermission(panel, "objects", "Sửa đối tượng", _current.AllowEditObjects);
        AddPermission(panel, "scenarios", "Sửa kịch bản", _current.AllowEditScenarios);
        Note(panel, "Mật khẩu được chuyển ngay sang hash tương thích Excel; Nera không lưu mật khẩu thô. Các ô mặc định Locked nên sẽ không chỉnh được khi trang tính được bảo vệ.");
        DialogBody.Children.Add(panel);
    }

    protected override bool TryApply()
    {
        if (_current.Enabled && _current.PasswordHash is not null && !_current.VerifyPassword(_password.Text))
            throw new ArgumentException(L("Mật khẩu hiện tại không đúng."));
        if (_enabled.IsChecked != true)
        {
            _controller.SetWorksheetProtection(new WorksheetProtectionSettings());
            return true;
        }
        string? hash;
        if (_current.Enabled) hash = _current.PasswordHash;
        else
        {
            if (!string.Equals(_password.Text, _confirm.Text, StringComparison.Ordinal)) throw new ArgumentException(L("Xác nhận mật khẩu không khớp."));
            hash = SpreadsheetProtectionPassword.HashLegacy(_password.Text);
        }
        _controller.SetWorksheetProtection(new WorksheetProtectionSettings
        {
            Enabled = true,
            PasswordHash = hash,
            SelectLockedCells = P("select-locked"),
            SelectUnlockedCells = P("select-unlocked"),
            AllowFormatCells = P("format-cells"),
            AllowFormatColumns = P("format-columns"),
            AllowFormatRows = P("format-rows"),
            AllowInsertColumns = P("insert-columns"),
            AllowInsertRows = P("insert-rows"),
            AllowInsertHyperlinks = P("insert-hyperlinks"),
            AllowDeleteColumns = P("delete-columns"),
            AllowDeleteRows = P("delete-rows"),
            AllowSort = P("sort"),
            AllowAutoFilter = P("auto-filter"),
            AllowPivotTables = P("pivot"),
            AllowEditObjects = P("objects"),
            AllowEditScenarios = P("scenarios"),
        });
        return true;
    }

    private bool P(string id) => _permissions[id].IsChecked == true;

    private void AddPermission(StackPanel panel, string id, string caption, bool value)
    {
        var check = CheckField(panel, "protect-sheet-" + id, caption, value); check.IsThreeState = false;
        _permissions[id] = check;
    }
}

public sealed class NeraProtectWorkbookDialog : NeraSettingsDialog
{
    private readonly SpreadsheetProtectionController _controller;
    private readonly WorkbookProtectionSettings _current;
    private readonly CheckBox _enabled;
    private readonly CheckBox _structure;
    private readonly CheckBox _windows;
    private readonly TextBox _password;
    private readonly TextBox _confirm;

    public NeraProtectWorkbookDialog(SpreadsheetSession session, PresentationLocalization? localization = null, NeraIconTheme theme = NeraIconTheme.Light)
        : base("Bảo vệ sổ làm việc", "nera-protect-workbook-dialog", localization, theme)
    {
        _controller = new SpreadsheetProtectionController(session ?? throw new ArgumentNullException(nameof(session)));
        _current = _controller.WorkbookSettings;
        Width = 560; Height = 430; MinWidth = 500; MinHeight = 360;
        var panel = Panel();
        _enabled = CheckField(panel, "protect-workbook-enabled", "Bảo vệ sổ làm việc", _current.Enabled || !_current.Enabled); _enabled.IsThreeState = false;
        _structure = CheckField(panel, "protect-workbook-structure", "Cấu trúc", _current.Enabled ? _current.LockStructure : true); _structure.IsThreeState = false;
        _windows = CheckField(panel, "protect-workbook-windows", "Cửa sổ", _current.LockWindows); _windows.IsThreeState = false;
        _password = TextField(panel, "protect-workbook-password", _current.Enabled ? "Mật khẩu hiện tại" : "Mật khẩu (tùy chọn)", null); _password.PasswordChar = '●';
        _confirm = TextField(panel, "protect-workbook-confirm", "Xác nhận mật khẩu", null); _confirm.PasswordChar = '●'; _confirm.IsEnabled = !_current.Enabled;
        Note(panel, "Bảo vệ cấu trúc được round-trip bằng workbookProtection. Mật khẩu thô không được lưu.");
        DialogBody.Children.Add(panel);
    }

    protected override bool TryApply()
    {
        if (_current.Enabled && _current.PasswordHash is not null && !_current.VerifyPassword(_password.Text))
            throw new ArgumentException(L("Mật khẩu hiện tại không đúng."));
        if (_enabled.IsChecked != true)
        {
            _controller.SetWorkbookProtection(new WorkbookProtectionSettings());
            return true;
        }
        var hash = _current.Enabled ? _current.PasswordHash : SpreadsheetProtectionPassword.HashLegacy(_password.Text);
        if (!_current.Enabled && !string.Equals(_password.Text, _confirm.Text, StringComparison.Ordinal))
            throw new ArgumentException(L("Xác nhận mật khẩu không khớp."));
        _controller.SetWorkbookProtection(new WorkbookProtectionSettings
        {
            Enabled = true,
            LockStructure = _structure.IsChecked == true,
            LockWindows = _windows.IsChecked == true,
            PasswordHash = hash,
        });
        return true;
    }
}
