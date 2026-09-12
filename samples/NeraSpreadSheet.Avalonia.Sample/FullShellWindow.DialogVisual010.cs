using System.Globalization;
using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    private static readonly double[] DialogVisual010RasterScales = [1.25, 1.5, 2];

    private async Task CaptureDialogVisual010Async(
        string directory,
        List<RibbonCapture> captures,
        List<string> checks,
        Worksheet sheet,
        long version,
        int history)
    {
        void Check(string id, bool condition)
        {
            if (!condition) throw new InvalidOperationException("Dialog visual 010: " + id);
            if (checks.Contains(id)) throw new InvalidOperationException("Duplicate dialog visual 010 assertion: " + id);
            checks.Add(id);
        }

        async Task CloseAsync(NeraSettingsDialog dialog, ValueTask<bool> activation, string key)
        {
            DialogControl<Button>(dialog, "dialog-cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await activation;
            Check(key + "-cancel", !dialog.IsVisible && !_settingsOpen &&
                Session.History.UndoCount == history && sheet.Version == version);
        }

        foreach (var theme in Enum.GetValues<NeraIconTheme>())
        {
            SetRibbonTheme(theme);
            await SettleRibbonAsync();

            var formatActivation = _ribbon.ActivateCommandAsync("Ui.Dialog.Number");
            await SettleRibbonAsync();
            var format = _dialogs.OfType<NeraFormatCellsDialog>().Single();
            await SettleDialog(format);
            var formatTabs = format.GetVisualDescendants().OfType<TabControl>().Single();
            formatTabs.SelectedIndex = (int)NeraFormatCellsTab.Protection;
            await SettleDialog(format);
            Check($"{theme}-protection-tab", format.SelectedFormatTab == NeraFormatCellsTab.Protection);
            VerifyDialogVisual010(format, $"{theme}-protection", checks);
            CaptureRibbonScene(format, $"{theme}-protection", 1, directory, captures);
            await CloseAsync(format, formatActivation, $"{theme}-protection");

            var pageActivation = _ribbon.ActivateCommandAsync("Ui.Dialog.PageSetup");
            await SettleRibbonAsync();
            var page = _dialogs.OfType<NeraPageSetupDialog>().Single();
            await SettleDialog(page);
            var pageTabs = page.GetVisualDescendants().OfType<TabControl>().Single();
            pageTabs.SelectedIndex = (int)NeraPageSetupTab.HeaderFooter;
            await SettleDialog(page);
            Check($"{theme}-header-footer-tab", page.SelectedPageTab == NeraPageSetupTab.HeaderFooter);
            VerifyDialogVisual010(page, $"{theme}-header-footer", checks);
            CaptureRibbonScene(page, $"{theme}-header-footer", 1, directory, captures);
            await CloseAsync(page, pageActivation, $"{theme}-header-footer");

            var validationActivation = _ribbon.ActivateCommandAsync("Ui.DataValidation");
            await SettleRibbonAsync();
            var validation = _dialogs.OfType<NeraDataValidationDialog>().Single();
            await SettleDialog(validation);
            VerifyDialogVisual010(validation, $"{theme}-data-validation", checks);
            CaptureRibbonScene(validation, $"{theme}-data-validation-settings", 1, directory, captures);
            var validationTabs = validation.GetVisualDescendants().OfType<TabControl>().Single();
            validationTabs.SelectedIndex = 1;
            await SettleDialog(validation);
            Check($"{theme}-data-validation-input-tab", validationTabs.SelectedIndex == 1);
            VerifyDialogVisual010(validation, $"{theme}-data-validation-input-focus", checks);
            CaptureRibbonScene(validation, $"{theme}-data-validation-input", 1, directory, captures);
            validationTabs.SelectedIndex = 2;
            await SettleDialog(validation);
            Check($"{theme}-data-validation-error-tab", validationTabs.SelectedIndex == 2);
            VerifyDialogVisual010(validation, $"{theme}-data-validation-error-focus", checks);
            CaptureRibbonScene(validation, $"{theme}-data-validation-error", 1, directory, captures);
            await CloseAsync(validation, validationActivation, $"{theme}-data-validation");

            var advancedActivation = _ribbon.ActivateCommandAsync("Ui.AdvancedFilter");
            await SettleRibbonAsync();
            var advanced = _dialogs.OfType<NeraAdvancedFilterDialog>().Single();
            await SettleDialog(advanced);
            VerifyDialogVisual010(advanced, $"{theme}-advanced-filter", checks);
            CaptureRibbonScene(advanced, $"{theme}-advanced-filter", 1, directory, captures);
            await CloseAsync(advanced, advancedActivation, $"{theme}-advanced-filter");

            var consolidateActivation = _ribbon.ActivateCommandAsync("Ui.Consolidate");
            await SettleRibbonAsync();
            var consolidate = _dialogs.OfType<NeraConsolidateDialog>().Single();
            await SettleDialog(consolidate);
            VerifyDialogVisual010(consolidate, $"{theme}-consolidate", checks);
            CaptureRibbonScene(consolidate, $"{theme}-consolidate", 1, directory, captures);
            await CloseAsync(consolidate, consolidateActivation, $"{theme}-consolidate");

            var sheetActivation = _ribbon.ActivateCommandAsync("Ui.ProtectSheet");
            await SettleRibbonAsync();
            var protectSheet = _dialogs.OfType<NeraProtectSheetDialog>().Single();
            await SettleDialog(protectSheet);
            VerifyDialogVisual010(protectSheet, $"{theme}-protect-sheet", checks);
            CaptureRibbonScene(protectSheet, $"{theme}-protect-sheet", 1, directory, captures);
            await CloseAsync(protectSheet, sheetActivation, $"{theme}-protect-sheet");

            var workbookActivation = _ribbon.ActivateCommandAsync("Ui.ProtectWorkbook");
            await SettleRibbonAsync();
            var protectWorkbook = _dialogs.OfType<NeraProtectWorkbookDialog>().Single();
            await SettleDialog(protectWorkbook);
            VerifyDialogVisual010(protectWorkbook, $"{theme}-protect-workbook", checks);
            CaptureRibbonScene(protectWorkbook, $"{theme}-protect-workbook", 1, directory, captures);
            await CloseAsync(protectWorkbook, workbookActivation, $"{theme}-protect-workbook");
        }

        SetRibbonTheme(NeraIconTheme.Light);
        await SettleRibbonAsync();
        await CaptureRasterDialog010Async("Ui.Dialog.Number", () => _dialogs.OfType<NeraFormatCellsDialog>().Single(),
            "number", directory, captures, checks, sheet, version, history);
        await CaptureRasterDialog010Async("Ui.Dialog.PageSetup", () => _dialogs.OfType<NeraPageSetupDialog>().Single(),
            "page", directory, captures, checks, sheet, version, history);
        await CaptureRasterDialog010Async("Ui.DataValidation", () => _dialogs.OfType<NeraDataValidationDialog>().Single(),
            "data-validation", directory, captures, checks, sheet, version, history);

        await CaptureCompactDialog010Async("Ui.Dialog.Number", () => _dialogs.OfType<NeraFormatCellsDialog>().Single(),
            "format", directory, captures, checks, sheet, version, history);
        await CaptureCompactDialog010Async("Ui.Dialog.PageSetup", () => _dialogs.OfType<NeraPageSetupDialog>().Single(),
            "page", directory, captures, checks, sheet, version, history);
        await CaptureCompactDialog010Async("Ui.DataValidation", () => _dialogs.OfType<NeraDataValidationDialog>().Single(),
            "data-validation", directory, captures, checks, sheet, version, history);
        await CaptureCompactDialog010Async("Ui.ProtectSheet", () => _dialogs.OfType<NeraProtectSheetDialog>().Single(),
            "protect-sheet", directory, captures, checks, sheet, version, history);
    }

    private async Task CaptureRasterDialog010Async<TDialog>(
        string commandId,
        Func<TDialog> resolve,
        string name,
        string directory,
        List<RibbonCapture> captures,
        List<string> checks,
        Worksheet sheet,
        long version,
        int history)
        where TDialog : NeraSettingsDialog
    {
        var activation = _ribbon.ActivateCommandAsync(commandId);
        await SettleRibbonAsync();
        var dialog = resolve();
        await SettleDialog(dialog);
        VerifyDialogVisual010(dialog, "Light-" + name + "-raster", checks);
        foreach (var scale in DialogVisual010RasterScales)
        {
            var suffix = scale.ToString("0.##", CultureInfo.InvariantCulture);
            CaptureRibbonScene(dialog, $"Light-{name}-raster-{suffix}", scale, directory, captures);
        }
        DialogControl<Button>(dialog, "dialog-cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await activation;
        AddDialogVisual010Check(checks, $"Light-{name}-raster-cancel", !dialog.IsVisible && !_settingsOpen &&
            Session.History.UndoCount == history && sheet.Version == version);
    }

    private async Task CaptureCompactDialog010Async<TDialog>(
        string commandId,
        Func<TDialog> resolve,
        string name,
        string directory,
        List<RibbonCapture> captures,
        List<string> checks,
        Worksheet sheet,
        long version,
        int history)
        where TDialog : NeraSettingsDialog
    {
        var activation = _ribbon.ActivateCommandAsync(commandId);
        await SettleRibbonAsync();
        var dialog = resolve();
        await SettleDialog(dialog);
        dialog.Width = dialog.MinWidth;
        dialog.Height = dialog.MinHeight;
        await SettleDialog(dialog);
        VerifyDialogVisual010(dialog, "Light-" + name + "-compact", checks);
        CaptureRibbonScene(dialog, $"Light-{name}-compact", 1, directory, captures);
        DialogControl<Button>(dialog, "dialog-cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        await activation;
        AddDialogVisual010Check(checks, $"Light-{name}-compact-cancel", !dialog.IsVisible && !_settingsOpen &&
            Session.History.UndoCount == history && sheet.Version == version);
    }

    private void VerifyDialogVisual010(NeraSettingsDialog dialog, string key, List<string> checks)
    {
        var client = dialog.Content as Control ?? throw new InvalidOperationException("Dialog has no client: " + key);
        AddDialogVisual010Check(checks, key + "-owner", dialog.IsVisible && dialog.Owner == this);
        AddDialogVisual010Check(checks, key + "-font", Math.Abs(dialog.FontSize - 13) < 0.01 && !string.IsNullOrWhiteSpace(dialog.Title));
        AddDialogVisual010Check(checks, key + "-tab-cycle", KeyboardNavigation.GetTabNavigation(client) == KeyboardNavigationMode.Cycle);
        var ok = DialogControl<Button>(dialog, "dialog-ok");
        var cancel = DialogControl<Button>(dialog, "dialog-cancel");
        AddDialogVisual010Check(checks, key + "-default-cancel", ok.IsDefault && cancel.IsCancel);
        AddDialogVisual010Check(checks, key + "-button-targets", ok.Bounds.Width >= 80 && ok.Bounds.Height >= 28 &&
            cancel.Bounds.Width >= 80 && cancel.Bounds.Height >= 28);
        AddDialogVisual010Check(checks, key + "-bounded", dialog.Bounds.Width <= 760 && dialog.Bounds.Height <= 720 &&
            client.Bounds.Width > 0 && client.Bounds.Height > 0);

        var focused = TopLevel.GetTopLevel(dialog)?.FocusManager?.GetFocusedElement() as Control;
        if (focused is null || !dialog.GetVisualDescendants().Contains(focused) ||
            AutomationProperties.GetAutomationId(focused) is "dialog-ok" or "dialog-cancel")
        {
            var input = dialog.GetVisualDescendants().OfType<Control>().FirstOrDefault(control =>
                control.IsVisible && control.IsEnabled && control.Focusable &&
                control is TextBox or ComboBox or CheckBox or RadioButton or ListBox);
            input?.Focus(NavigationMethod.Tab);
            focused = TopLevel.GetTopLevel(dialog)?.FocusManager?.GetFocusedElement() as Control;
        }
        AddDialogVisual010Check(checks, key + "-focus", focused is not null &&
            dialog.GetVisualDescendants().Contains(focused) &&
            AutomationProperties.GetAutomationId(focused) is not "dialog-ok" and not "dialog-cancel");
    }

    private static void AddDialogVisual010Check(List<string> checks, string id, bool condition)
    {
        if (!condition) throw new InvalidOperationException("Dialog visual 010: " + id);
        if (checks.Contains(id)) throw new InvalidOperationException("Duplicate dialog visual 010 assertion: " + id);
        checks.Add(id);
    }
}
