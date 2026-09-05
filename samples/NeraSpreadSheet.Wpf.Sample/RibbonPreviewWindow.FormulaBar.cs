using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Wpf.Sample;

public sealed partial class RibbonPreviewWindow
{
    private readonly TextBox _formula = new()
    {
        Margin = new Thickness(6, 4, 8, 4),
        Padding = new Thickness(6, 3, 6, 3),
        MinHeight = 28,
        MaxHeight = 92,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.Wrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        UndoLimit = 100,
    };
    private readonly Button _formulaAccept = FormulaButton("✓");
    private readonly Button _formulaCancel = FormulaButton("×");
    private readonly Button _formulaInCell = FormulaButton("F2");
    private readonly Button _formulaHelpButton = FormulaButton("ƒx");
    private readonly TextBlock _formulaBarHelpText = new()
    {
        TextWrapping = TextWrapping.Wrap, MaxWidth = 460, Margin = new Thickness(10), Foreground = Brushes.Black,
    };
    private Popup? _formulaBarHelpPopup;
    private Worksheet? _formulaWorksheet;
    private PresentationLocalization? _formulaLocalization;
    private bool _synchronizingFormulaBar;
    private bool _hasFormulaBarDraft;

    private static Button FormulaButton(string content) => new()
    {
        Content = content, MinWidth = 30, Padding = new Thickness(5, 2, 5, 2),
        Margin = new Thickness(2, 4, 2, 4), VerticalAlignment = VerticalAlignment.Top, Focusable = false,
    };

    private void InitializeFormulaBar()
    {
        // This handler must precede the Ribbon's Window preview handler.
        PreviewKeyDown += OnFormulaBarWindowKeyDown;
        _formula.GotKeyboardFocus += OnFormulaBarGotKeyboardFocus;
        _formula.TextChanged += OnFormulaBarTextChanged;
        _formula.SelectionChanged += OnFormulaBarSelectionChanged;
        _sheet.EditorDraftChanged += OnFormulaBarDraftChanged;
        _session.ActiveWorksheetChanged += OnFormulaBarWorksheetChanged;
        _formulaAccept.Click += OnFormulaAcceptClick;
        _formulaCancel.Click += OnFormulaCancelClick;
        _formulaInCell.Click += OnFormulaInCellClick;
        _formulaHelpButton.Click += OnFormulaHelpClick;
        _formulaBarHelpPopup = new Popup
        {
            PlacementTarget = _formula, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true,
            Child = new Border { Background = Brushes.White, BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1), Child = _formulaBarHelpText },
        };
        AutomationProperties.SetAutomationId(_formula, "Nera.FormulaBar.Editor");
        AutomationProperties.SetAutomationId(_formulaAccept, "Nera.FormulaBar.Accept");
        AutomationProperties.SetAutomationId(_formulaCancel, "Nera.FormulaBar.Cancel");
        AutomationProperties.SetAutomationId(_formulaInCell, "Nera.FormulaBar.InCell");
        AutomationProperties.SetAutomationId(_formulaHelpButton, "Nera.FormulaBar.Help");
        SubscribeFormulaWorksheet();
    }

    private DockPanel CreateFormulaBar()
    {
        var row = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(247, 249, 250)) };
        row.Children.Add(_address);
        row.Children.Add(_formulaHelpButton);
        row.Children.Add(_formulaCancel);
        row.Children.Add(_formulaAccept);
        row.Children.Add(_formulaInCell);
        row.Children.Add(_formula);
        return row;
    }

    private void DisposeFormulaBar()
    {
        PreviewKeyDown -= OnFormulaBarWindowKeyDown;
        _formula.GotKeyboardFocus -= OnFormulaBarGotKeyboardFocus;
        _formula.TextChanged -= OnFormulaBarTextChanged;
        _formula.SelectionChanged -= OnFormulaBarSelectionChanged;
        _sheet.EditorDraftChanged -= OnFormulaBarDraftChanged;
        _session.ActiveWorksheetChanged -= OnFormulaBarWorksheetChanged;
        if (_formulaWorksheet is { } worksheet) worksheet.CellsChanged -= OnFormulaWorksheetCellsChanged;
        _formulaWorksheet = null;
        _formulaAccept.Click -= OnFormulaAcceptClick;
        _formulaCancel.Click -= OnFormulaCancelClick;
        _formulaInCell.Click -= OnFormulaInCellClick;
        _formulaHelpButton.Click -= OnFormulaHelpClick;
        if (_formulaBarHelpPopup is { } popup) { popup.IsOpen = false; popup.Child = null; }
        _formulaBarHelpPopup = null;
    }

    private void SubscribeFormulaWorksheet()
    {
        if (ReferenceEquals(_formulaWorksheet, _session.ActiveWorksheet)) return;
        if (_formulaWorksheet is { } previous) previous.CellsChanged -= OnFormulaWorksheetCellsChanged;
        _formulaWorksheet = _session.ActiveWorksheet;
        _formulaWorksheet.CellsChanged += OnFormulaWorksheetCellsChanged;
    }

    private void OnFormulaBarWorksheetChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        SubscribeFormulaWorksheet();
        if (_formulaBarHelpPopup is { } popup) popup.IsOpen = false;
        RefreshFormulaBar();
    }

    private void OnFormulaWorksheetCellsChanged(object? sender, CellsChangedEventArgs e)
    {
        if (ReferenceEquals(sender, _formulaWorksheet)) RefreshFormulaBar();
    }

    private void OnFormulaBarDraftChanged(object? sender, EventArgs e)
    {
        if (_sheet.CurrentEditorDraft is null && _formulaBarHelpPopup is { } popup) popup.IsOpen = false;
        RefreshFormulaBar();
    }

    private void RefreshFormulaBar()
    {
        if (_disposed || _synchronizingFormulaBar) return;
        _synchronizingFormulaBar = true;
        try
        {
            // Queued shell refreshes read the current snapshot, never an old captured draft.
            var draft = _sheet.CurrentEditorDraft;
            var address = draft?.Address ?? _session.Selection.ActiveCell;
            _address.Text = address.ToString();
            var cell = _session.ActiveWorksheet.GetCell(address);
            var text = draft?.Text ?? cell.Formula ?? cell.Value.ToString();
            if (_hasFormulaBarDraft != (draft is not null))
            {
                if (draft is null || !_formula.IsKeyboardFocusWithin)
                {
                    _formula.IsUndoEnabled = false;
                    _formula.IsUndoEnabled = true;
                }
                _hasFormulaBarDraft = draft is not null;
            }
            if (_formula.Text != text) _formula.Text = text;
            if (draft is not null && (_formula.SelectionStart != draft.SelectionStart || _formula.SelectionLength != draft.SelectionLength))
                _formula.Select(draft.SelectionStart, draft.SelectionLength);
            _formulaAccept.IsEnabled = draft is not null;
            _formulaCancel.IsEnabled = draft is not null;
            RefreshFormulaBarLabels();
            if (_formulaBarHelpPopup?.IsOpen == true) RefreshFormulaBarHelp();
        }
        finally { _synchronizingFormulaBar = false; }
    }

    private void RefreshFormulaBarLabels()
    {
        if (ReferenceEquals(_formulaLocalization, Localization)) return;
        _formulaLocalization = Localization;
        AutomationProperties.SetName(_formula, Localization.Get("Thanh công thức"));
        _formula.ToolTip = Localization.Get("Enter: xác nhận · Alt+Enter: xuống dòng · Esc: hủy · Tab/F2: sửa trong ô");
        SetFormulaButtonLabel(_formulaAccept, "Xác nhận công thức");
        SetFormulaButtonLabel(_formulaCancel, "Hủy chỉnh sửa công thức");
        SetFormulaButtonLabel(_formulaInCell, "Sửa trong ô và chọn gợi ý (F2)");
        SetFormulaButtonLabel(_formulaHelpButton, "Trợ giúp công thức đang nhập");
    }

    private void SetFormulaButtonLabel(Button button, string key)
    {
        var text = Localization.Get(key);
        button.ToolTip = text;
        AutomationProperties.SetName(button, text);
    }

    private void OnFormulaBarGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_disposed || _synchronizingFormulaBar) return;
        if (_sheet.CurrentEditorDraft is null)
        {
            _synchronizingFormulaBar = true;
            try { _sheet.BeginEdit(); }
            finally { _synchronizingFormulaBar = false; }
            RefreshFormulaBar();
            _synchronizingFormulaBar = true;
            try { _formula.Focus(); }
            finally { _synchronizingFormulaBar = false; }
        }
        else RefreshFormulaBar();
    }

    private void OnFormulaBarTextChanged(object sender, TextChangedEventArgs e) => FlushFormulaBarDraft();
    private void OnFormulaBarSelectionChanged(object sender, RoutedEventArgs e) => FlushFormulaBarDraft();

    private bool FlushFormulaBarDraft()
    {
        if (_disposed || _synchronizingFormulaBar || !_formula.IsKeyboardFocusWithin) return false;
        var text = _formula.Text;
        var start = _formula.SelectionStart;
        var length = _formula.SelectionLength;
        _synchronizingFormulaBar = true;
        try
        {
            if (_sheet.CurrentEditorDraft is null)
            {
                _sheet.BeginEdit();
                _formula.Focus();
            }
            var draft = _sheet.CurrentEditorDraft;
            if (draft is null) return false;
            if (draft.Text != text || draft.SelectionStart != start || draft.SelectionLength != length)
                _sheet.UpdateEditorDraft(text, start, length);
        }
        finally { _synchronizingFormulaBar = false; }
        RefreshFormulaBar();
        return true;
    }

    private void OnFormulaBarWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (_disposed || e.Handled || !_formula.IsKeyboardFocusWithin) return;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.Control && key is Key.Z or Key.Y or Key.C or Key.X or Key.V)
        {
            // Preserve the TextBox's own edit history/clipboard, including when Undo is empty.
            e.Handled = true;
            var command = key switch
            {
                Key.Z => ApplicationCommands.Undo, Key.Y => ApplicationCommands.Redo,
                Key.C => ApplicationCommands.Copy, Key.X => ApplicationCommands.Cut, _ => ApplicationCommands.Paste,
            };
            if (command.CanExecute(null, _formula)) command.Execute(null, _formula);
            return;
        }
        if (key is Key.Enter or Key.Return)
        {
            e.Handled = true;
            if ((modifiers & ModifierKeys.Alt) != 0)
            {
                if (_ribbon.KeyTipScope != RibbonKeyTipScope.Inactive) _ribbon.EscapeKeyTipMode();
                var start = _formula.SelectionStart;
                _formula.SelectedText = Environment.NewLine;
                _formula.Select(start + Environment.NewLine.Length, 0);
                FlushFormulaBarDraft();
            }
            else if ((modifiers & (ModifierKeys.Control | ModifierKeys.Windows)) == 0) CommitFormulaBar();
        }
        else if (key == Key.Escape)
        {
            e.Handled = true;
            if (_ribbon.KeyTipScope != RibbonKeyTipScope.Inactive) _ribbon.EscapeKeyTipMode();
            else CancelFormulaBar();
        }
        else if (key is Key.Tab or Key.F2 && (modifiers & (ModifierKeys.Control | ModifierKeys.Alt | ModifierKeys.Windows)) == 0)
        {
            e.Handled = true;
            FocusFormulaCellEditor();
        }
    }

    private void CommitFormulaBar()
    {
        if (_disposed || _sheet.CurrentEditorDraft is null) return;
        FlushFormulaBarDraft();
        if (!_sheet.CommitEditor())
        {
            SetStatus(_session.Editor.LastValidationResult?.ErrorMessage ?? Localization.Get("Nội dung chưa hợp lệ. Kiểm tra lại ô đang sửa."));
            _formula.Focus();
            return;
        }
        var next = SpreadsheetVisibleCellNavigation.GetNextVisibleCell(_session.ActiveWorksheet, _session.Selection.ActiveCell, 1, 0);
        _session.Selection.SetActiveCell(next);
        _sheet.ScrollCellIntoView(next);
        RefreshFormulaBar();
    }

    private void CancelFormulaBar()
    {
        if (_disposed) return;
        _sheet.CancelEditor();
        RefreshFormulaBar();
    }

    private void FocusFormulaCellEditor()
    {
        if (_disposed) return;
        FlushFormulaBarDraft();
        if (_sheet.CurrentEditorDraft is null) _sheet.BeginEdit();
        _sheet.FocusEditor();
    }

    private void StartFormulaTemplate(string text)
    {
        if (_disposed) return;
        if (_sheet.CurrentEditorDraft is null) _sheet.BeginEdit(text);
        else _sheet.UpdateEditorDraft(text, text.Length, 0);
        _sheet.FocusEditor();
        RefreshFormulaBar();
    }

    private void OnFormulaAcceptClick(object sender, RoutedEventArgs e) => CommitFormulaBar();
    private void OnFormulaCancelClick(object sender, RoutedEventArgs e) => CancelFormulaBar();
    private void OnFormulaInCellClick(object sender, RoutedEventArgs e) => FocusFormulaCellEditor();
    private void OnFormulaHelpClick(object sender, RoutedEventArgs e) => ShowFormulaBarHelp();

    private void ShowFormulaBarHelp()
    {
        if (_disposed || _formulaBarHelpPopup is null) return;
        RefreshFormulaBarHelp();
        _formulaBarHelpPopup.IsOpen = true;
    }

    private void RefreshFormulaBarHelp()
    {
        var draft = _sheet.CurrentEditorDraft;
        var context = _sheet.CurrentFormulaHelp ?? (draft is null ? null : _session.FormulaEditing.GetFunctionHelp(draft.Text, draft.CaretIndex));
        _formulaBarHelpText.Text = context is null
            ? Localization.Get("Đặt con trỏ trong lời gọi hàm đang nhập để xem trợ giúp đối số.")
            : $"{context.Function.Signature}\n{context.Function.Description}" + (context.ActiveArgument is { } argument
                ? "\n" + Localization.Format("Đối số {0}: {1} — {2}", context.ActiveArgumentIndex + 1, argument.Name, argument.Description)
                : string.Empty);
    }
}
