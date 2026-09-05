using System.Windows;

namespace NeraSpreadSheet.Wpf;

internal sealed partial class NeraSpreadsheetSplitAdorner
{
    private bool _hasEditorDraft;
    private bool _changingEditorDraft;

    internal SpreadsheetEditorDraft? CurrentEditorDraft =>
        _hasEditorDraft && ReferenceEquals(_session, _owner.Session) && _cellEditor?.State is { } state
            ? new(state.Address, _editor.Text, _editor.SelectionStart,
                _editor.SelectionLength, _editor.CaretIndex)
            : null;

    internal bool UpdateEditorDraft(string text, int selectionStart, int selectionLength)
    {
        NeraSpreadsheetControl.ValidateEditorDraftSelection(text, selectionStart, selectionLength);
        if (CurrentEditorDraft is null) return false;
        _changingEditorDraft = true;
        try
        {
            ResetFormulaEditingUi();
            if (_editor.Text != text) _editor.Text = text;
            if (_editor.SelectionStart != selectionStart || _editor.SelectionLength != selectionLength)
                _editor.Select(selectionStart, selectionLength);
            UpdateFormulaSuggestions();
        }
        finally { _changingEditorDraft = false; }
        NotifyEditorDraftChanged();
        return true;
    }

    internal bool FocusEditor() => CurrentEditorDraft is not null && _editor.Focus();

    private void NotifyEditorDraftChanged()
    {
        if (!_changingEditorDraft) _owner.NotifyEditorDraftChanged();
    }

    private void OnNativeEditorDraftChanged(object sender, RoutedEventArgs e) => NotifyEditorDraftChanged();
}
