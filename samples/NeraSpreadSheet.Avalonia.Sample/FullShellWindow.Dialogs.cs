using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    private bool _settingsOpen;
    private void RegisterDialogCommands()
    {
        AddAsync("Ui.Dialog.Number", "Định dạng ô: Số", () => ShowFormatCellsAsync(NeraFormatCellsTab.Number), "Ctrl+1", state: DialogState);
        AddAsync("Ui.Dialog.Font", "Định dạng ô: Phông chữ", () => ShowFormatCellsAsync(NeraFormatCellsTab.Font), state: DialogState);
        AddAsync("Ui.Dialog.Alignment", "Định dạng ô: Căn chỉnh", () => ShowFormatCellsAsync(NeraFormatCellsTab.Alignment), state: DialogState);
        AddAsync("Ui.Dialog.PageSetup", "Thiết lập trang nâng cao", () => ShowPageSetupAsync(NeraPageSetupTab.Page), state: DialogState);
        AddAsync("Ui.Dialog.PrintOptions", "Tùy chọn trang tính khi in", () => ShowPageSetupAsync(NeraPageSetupTab.Sheet), state: DialogState);
        AddAsync("Ui.Dialog.Zoom", "Thiết lập thu phóng", ShowZoomAsync, state: DialogState);
        RegisterQaGapCommands();
    }
    private CommandState DialogState() => new(!_settingsOpen);
    private async Task ShowFormatCellsAsync(NeraFormatCellsTab tab)
    {
        if (_settingsOpen) return;
        var session = Session; var pane = _split.ActiveSpreadsheet;
        using var dialog = new NeraFormatCellsDialog(session, tab, _runtime.Localization, _ribbon.IconTheme,
            () => !_closed && ReferenceEquals(Session, session) && ReferenceEquals(_split.ActiveSpreadsheet, pane));
        await ShowSettingsAsync(dialog, pane);
    }
    private async Task ShowPageSetupAsync(NeraPageSetupTab tab)
    {
        if (_settingsOpen) return;
        var session = Session; var pane = _split.ActiveSpreadsheet;
        using var dialog = new NeraPageSetupDialog(session, tab, _runtime.Localization, _ribbon.IconTheme,
            () => !_closed && ReferenceEquals(Session, session));
        await ShowSettingsAsync(dialog, pane);
    }
    private async Task ShowZoomAsync()
    {
        if (_settingsOpen) return;
        var session = Session; var sheet = session.ActiveWorksheet; var pane = _split.ActiveSpreadsheet;
        var dialog = new NeraZoomDialog(pane.Zoom, _runtime.Localization, _ribbon.IconTheme);
        var accepted = await ShowSettingsAsync(dialog, pane);
        if (accepted && !_closed && ReferenceEquals(Session, session) && ReferenceEquals(session.ActiveWorksheet, sheet) && ReferenceEquals(pane, _split.ActiveSpreadsheet))
            _split.SetZoom(dialog.Zoom);
    }
    private async Task<bool> ShowSettingsAsync(NeraSettingsDialog dialog, NeraSpreadsheetControl pane)
    {
        _settingsOpen = true;
        try
        {
            TrackWindow(dialog);
            _runtime.Refresh();
            return await dialog.ShowDialog<bool>(this);
        }
        finally
        {
            // ShowDialog can fail before a Closed event. Do not retain a dead
            // window or leave commands disabled after that startup failure.
            _dialogs.Remove(dialog);
            _settingsOpen = false;
            if (!_closed) { _runtime.Refresh(); if (ReferenceEquals(_split.ActiveSpreadsheet, pane)) pane.Focus(); }
        }
    }
}
