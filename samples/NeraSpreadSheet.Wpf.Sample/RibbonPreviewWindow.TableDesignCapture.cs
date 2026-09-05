using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Wpf.Sample;

public sealed partial class RibbonPreviewWindow
{
    private async Task CaptureTableDialogSmokeAsync(string outputDirectory, List<object> images)
    {
        var table = CurrentTable ?? throw new InvalidOperationException("A Table is required.");
        var history = _session.History.UndoCount;
        Exception? failure = null;
        void InDialog(Action<Window> action)
        {
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () =>
            {
                var dialog = OwnedWindows.Cast<Window>().Single(window =>
                    AutomationProperties.GetAutomationId(window) == "table-parameter-dialog");
                try { action(dialog); }
                catch (Exception exception) { failure = exception; dialog.Close(); }
            });
        }
        static Button Button(Window dialog, string id) => CaptureDescendants<Button>(dialog)
            .Single(button => AutomationProperties.GetAutomationId(button) == id);
        static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        InDialog(dialog => Click(Button(dialog, "table-parameter-cancel")));
        if (await _runtime.TryActivateAsync("Table.Rename") || _session.History.UndoCount != history || CurrentTable?.Name != table.Name)
            throw new InvalidOperationException("Cancel mutated the Table or its history.");
        if (failure is not null) throw new InvalidOperationException("Dialog cancellation smoke failed.", failure);
        InDialog(dialog =>
        {
            var input = CaptureDescendants<TextBox>(dialog).Single();
            input.Text = "";
            Click(Button(dialog, "table-parameter-apply"));
            if (_session.History.UndoCount != history || !dialog.IsVisible ||
                !CaptureDescendants<TextBlock>(dialog).Any(text => AutomationProperties.GetAutomationId(text) == "table-parameter-error" && text.Text.Length > 0))
                throw new InvalidOperationException("Invalid dialog input was not retained with a visible error.");
            dialog.UpdateLayout();
            const string file = "table-rename-validation.png";
            SaveCapture((FrameworkElement)dialog.Content, System.IO.Path.Combine(outputDirectory, file), 1);
            images.Add(new { file, tab = "table-rename-validation", exportScale = 1d });
            input.Text = "BanHangMoi";
            Click(Button(dialog, "table-parameter-apply"));
        });
        if (!await _runtime.TryActivateAsync("Table.Rename") || CurrentTable?.Name != "BanHangMoi" ||
            CurrentTable.Id != table.Id || _session.History.UndoCount != history + 1)
            throw new InvalidOperationException("Rename dialog did not dispatch one stable Table mutation.");
        if (failure is not null) throw new InvalidOperationException("Dialog validation smoke failed.", failure);
        if (!await _runtime.TryActivateAsync("Edit.Undo") || CurrentTable?.Name != table.Name)
            throw new InvalidOperationException("Rename Undo failed.");
        async Task ApplyInputAsync(string id, string? text, Func<bool> verify, string? selected = null)
        {
            InDialog(dialog =>
            {
                if (text is not null) CaptureDescendants<TextBox>(dialog).Single().Text = text;
                if (id == "Table.RemoveDuplicates")
                {
                    foreach (var choice in CaptureDescendants<CheckBox>(dialog))
                        choice.IsChecked = (Guid)choice.Tag == table.Columns[0].Id;
                }
                Click(Button(dialog, "table-parameter-apply"));
            });
            var before = _session.History.UndoCount;
            var executed = selected is null ? await _runtime.TryActivateAsync(id) : await _runtime.TryActivateItemAsync(id, selected);
            if (failure is not null) throw new InvalidOperationException($"{id} dialog smoke failed.", failure);
            if (!executed || !verify() || _session.History.UndoCount != before + 1)
                throw new InvalidOperationException($"{id} did not perform the expected single mutation.");
            if (!await _runtime.TryActivateAsync("Edit.Undo") || _session.History.UndoCount != before)
                throw new InvalidOperationException($"{id} Undo failed.");
        }
        await ApplyInputAsync("Table.Resize", "A1:E35", () => CurrentTable?.Range.Bottom == 34);
        await ApplyInputAsync("Table.CalculatedColumn", "=42", () => CurrentTable?.Columns[0].CalculatedColumnFormula == "=42");
        await ApplyInputAsync("Table.TotalsFunction", "=SUM(1,2)", () => CurrentTable?.Columns[0].TotalsRowFormula == "=SUM(1,2)", "Custom");
        await ApplyInputAsync("Table.RemoveDuplicates", null, () => CurrentTable?.Range.RowCount == 7);
        var formulaCells = _session.ActiveWorksheet.EnumerateUsedCells()
            .Where(pair => pair.Value.Formula is not null).ToArray();
        if (formulaCells.Length == 0 || formulaCells.Any(pair => pair.Value.Value.Kind == CellValueKind.Error))
            throw new InvalidOperationException("Convert smoke requires successfully evaluated Table formulas.");
        await ApplyInputAsync("Table.ConvertToRange", null, () =>
            _session.ActiveWorksheet.TableCount == 0 && formulaCells.All(pair =>
                _session.ActiveWorksheet.GetCell(pair.Key).Value == pair.Value.Value &&
                _session.ActiveWorksheet.GetFormula(pair.Key) is { } formula && formula != pair.Value.Formula));
        if (CurrentTable?.Id != table.Id || formulaCells.Any(pair =>
            _session.ActiveWorksheet.GetCell(pair.Key).Value != pair.Value.Value ||
            _session.ActiveWorksheet.GetFormula(pair.Key) != pair.Value.Formula))
            throw new InvalidOperationException("Convert Undo did not restore formula values and structured references.");
        var selection = _session.Selection.Ranges.ToArray();
        var active = _session.Selection.ActiveCell;
        _session.Selection.Select(new CellRange(new CellAddress(0, 6), new CellAddress(2, 7)));
        await ApplyInputAsync("Table.Create", "NewTable", () => CurrentTable?.Name == "NewTable" && _session.ActiveWorksheet.TableCount == 2);
        _session.Selection.Select(selection[0]);
        _session.Selection.SetActiveCell(active);
        var worksheets = CaptureDescendants<ComboBox>(_root).Single(combo =>
            AutomationProperties.GetAutomationId(combo) == "preview-worksheet");
        worksheets.SelectedIndex = 1;
        if (_runtime.SelectionContext.IsInTable || await _runtime.TryActivateAsync("Table.Rename"))
            throw new InvalidOperationException("Table context survived switching to an empty worksheet.");
        worksheets.SelectedIndex = 0;
        _session.Selection.SetActiveCell(active);
        if (!_runtime.SelectionContext.IsInTable)
            throw new InvalidOperationException("Table context was not restored after worksheet switching.");
        await FlushCaptureAsync();
    }
}
