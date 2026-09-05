using System.Windows;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Wpf;

public sealed partial class NeraSpreadsheetControl
{
    private bool _hasEditorDraft;
    private bool _changingEditorDraft;
    private SpreadsheetEditorDraft? _lastNotifiedEditorDraft;

    /// <summary>
    /// Gets the current native draft, including when split panes are enabled,
    /// or null when no native edit is active. Access this API on the UI thread.
    /// </summary>
    public SpreadsheetEditorDraft? CurrentEditorDraft =>
        this.TryGetSplitPaneController(out var split) ? split.CurrentEditorDraft :
        _hasEditorDraft && _cellEditor?.State is { } state
            ? new(state.Address, _editor.Text, _editor.SelectionStart,
                _editor.SelectionLength, _editor.CaretIndex)
            : null;

    /// <summary>
    /// Raised on the UI thread after the native draft text, selection or edit
    /// lifecycle changes. Read CurrentEditorDraft for the current snapshot.
    /// </summary>
    public event EventHandler? EditorDraftChanged;

    /// <summary>
    /// Replaces the active native draft and selection without taking focus,
    /// restarting the canonical edit, validating, or adding workbook history.
    /// Returns false when no native edit is active. Invalid UTF-16 selection
    /// bounds throw before changing the draft. Call BeginEdit once to start.
    /// </summary>
    public bool UpdateEditorDraft(string text, int selectionStart, int selectionLength)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ValidateEditorDraftSelection(text, selectionStart, selectionLength);
        if (this.TryGetSplitPaneController(out var split))
            return split.UpdateEditorDraft(text, selectionStart, selectionLength);
        if (!_hasEditorDraft || _cellEditor?.IsEditing != true) return false;
        _changingEditorDraft = true;
        try
        {
            ResetFormulaEditingUi();
            if (_editor.Text != text) _editor.Text = text;
            _editor.Select(selectionStart, selectionLength);
            UpdateFormulaSuggestions();
        }
        finally { _changingEditorDraft = false; }
        NotifyEditorDraftChanged();
        return true;
    }

    /// <summary>
    /// Transfers keyboard focus to the active native editor, retaining its
    /// draft and selection. Returns false when no native edit can take focus.
    /// </summary>
    public bool FocusEditor()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (this.TryGetSplitPaneController(out var split)) return split.FocusEditor();
        return _hasEditorDraft && _cellEditor?.IsEditing == true && _editor.Focus();
    }

    internal static void ValidateEditorDraftSelection(string text, int selectionStart, int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(selectionStart);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(selectionStart, text.Length);
        ArgumentOutOfRangeException.ThrowIfNegative(selectionLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(selectionLength, text.Length - selectionStart);
    }

    internal void NotifyEditorDraftChanged()
    {
        if (_changingEditorDraft) return;
        var draft = CurrentEditorDraft;
        if (draft == _lastNotifiedEditorDraft) return;
        _lastNotifiedEditorDraft = draft;
        EditorDraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnNativeEditorDraftChanged(object sender, RoutedEventArgs e) => NotifyEditorDraftChanged();

    private void OnCanonicalEditorStateChanged(object? sender, CellEditStateChangedEventArgs e)
    {
        if (e.State is not null) return;
        if (this.TryGetSplitPaneController(out var split)) split.CancelEditor();
        HideEditor();
        ResetFormulaEditingUi();
    }
}
