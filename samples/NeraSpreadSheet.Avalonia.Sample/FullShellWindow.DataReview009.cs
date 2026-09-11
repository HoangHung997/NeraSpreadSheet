namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    private void RegisterDataReview009Commands()
    {
        AddAsync("Ui.DataValidation", "Xác thực dữ liệu", ShowDataValidation009Async, state: DialogState);
        AddAsync("Ui.AdvancedFilter", "Lọc nâng cao", ShowAdvancedFilter009Async, state: DialogState);
        AddAsync("Ui.Consolidate", "Hợp nhất", ShowConsolidate009Async, state: DialogState);
        AddAsync("Ui.ProtectSheet", "Bảo vệ trang tính", ShowProtectSheet009Async, state: DialogState);
        AddAsync("Ui.ProtectWorkbook", "Bảo vệ sổ làm việc", ShowProtectWorkbook009Async, state: DialogState);
    }

    private async Task ShowDataValidation009Async()
    {
        if (_settingsOpen) return;
        var pane = _split.ActiveSpreadsheet;
        using var dialog = new NeraDataValidationDialog(Session, _runtime.Localization, _ribbon.IconTheme);
        await ShowSettingsAsync(dialog, pane);
    }

    private async Task ShowAdvancedFilter009Async()
    {
        if (_settingsOpen) return;
        var pane = _split.ActiveSpreadsheet;
        var dialog = new NeraAdvancedFilterDialog(Session, _runtime.Localization, _ribbon.IconTheme);
        await ShowSettingsAsync(dialog, pane);
    }

    private async Task ShowConsolidate009Async()
    {
        if (_settingsOpen) return;
        var pane = _split.ActiveSpreadsheet;
        var dialog = new NeraConsolidateDialog(Session, _runtime.Localization, _ribbon.IconTheme);
        await ShowSettingsAsync(dialog, pane);
    }

    private async Task ShowProtectSheet009Async()
    {
        if (_settingsOpen) return;
        var pane = _split.ActiveSpreadsheet;
        var dialog = new NeraProtectSheetDialog(Session, _runtime.Localization, _ribbon.IconTheme);
        await ShowSettingsAsync(dialog, pane);
    }

    private async Task ShowProtectWorkbook009Async()
    {
        if (_settingsOpen) return;
        var pane = _split.ActiveSpreadsheet;
        var dialog = new NeraProtectWorkbookDialog(Session, _runtime.Localization, _ribbon.IconTheme);
        await ShowSettingsAsync(dialog, pane);
    }
}
