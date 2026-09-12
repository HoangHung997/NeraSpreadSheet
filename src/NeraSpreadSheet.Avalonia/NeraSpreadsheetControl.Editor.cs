using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia;

public sealed partial class NeraSpreadsheetControl
{
    private CellEditState? _ownedEdit;
    private Rect _editorBounds;
    public bool IsEditing => _ownedEdit is not null && ReferenceEquals(_session?.Editor.State, _ownedEdit);
    public string EditorText => IsEditing ? _editor.Text ?? string.Empty : string.Empty;

    /// <summary>Starts the canonical draft without stealing another pane's editor.
    /// Re-entering the same draft preserves its text and selection unless replaced.</summary>
    public bool BeginEdit(string? replacementText = null, bool focusEditor = true)
    {
        VerifyUsable(); if (_session is null || (_session.Editor.IsEditing && !IsEditing)) return false;
        var wasEditing = IsEditing; if (!wasEditing) _ownedEdit = _session.Editor.BeginEdit();
        _updatingFormulaDraft = true;
        try
        {
            if (!wasEditing || replacementText is not null)
            {
                _acknowledgedFormulaText = replacementText ?? _ownedEdit!.InitialText;
                _editor.Text = _acknowledgedFormulaText; ResetProvisionalReference();
            }
            ApplyEditorStyle(); ScrollCellIntoView(_ownedEdit!.Address); UpdateEditorBounds();
            if (focusEditor) _editor.Focus();
            if (replacementText is not null)
            {
                var end = _editor.Text?.Length ?? 0; _editor.CaretIndex = end; _editor.SelectionStart = end; _editor.SelectionEnd = end;
            }
            else if (!wasEditing && focusEditor) _editor.SelectAll();
        }
        finally { _updatingFormulaDraft = false; }
        RefreshFormulaAssistance(); RefreshFormulaHighlights(); NotifyDraftChanged(); return true;
    }
    public void SetEditorText(string text)
    {
        VerifyUsable(); ArgumentNullException.ThrowIfNull(text);
        if (!IsEditing) throw new InvalidOperationException("This control does not own an active draft.");
        UpdateEditorDraft(text, Math.Min(_editor.SelectionStart, text.Length), Math.Min(_editor.SelectionEnd, text.Length));
    }
    public bool CommitEditor()
    {
        VerifyUsable(); if (!IsEditing || _session is null) { HideLocalEditor(); return false; }
        if (!_session.Editor.Commit(_editor.Text ?? string.Empty)) return false;
        HideLocalEditor(); Focus(); return true;
    }
    public bool CancelEditor() { VerifyUsable(); return EndOwnedEdit(true); }
    private bool EndOwnedEdit(bool focus)
    {
        var cancelled = IsEditing && _session!.Editor.Cancel(); HideLocalEditor();
        if (cancelled && focus && _attached) Focus(); return cancelled;
    }
    private void OnCanonicalEditorChanged(object? sender, CellEditStateChangedEventArgs e)
    {
        if (_ownedEdit is not null && !ReferenceEquals(e.State, _ownedEdit)) HideLocalEditor();
    }
    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsEditing) { OnFormulaTextChanged(); NotifyDraftChanged(); }
    }
    private void HideLocalEditor()
    {
        _ownedEdit = null; _editor.IsVisible = false; _editorBounds = default; _editor.Clip = null;
        ResetFormulaAssistance(); InvalidateMeasure(); NotifyDraftChanged();
    }
    private void ApplyEditorStyle()
    {
        if (_session is null || _ownedEdit is null) return;
        var style = _session.ActiveWorksheet.GetEffectiveStyle(_ownedEdit.Address, _session.Workbook.Styles);
        _editor.FontFamily = new FontFamily(style.Font.Family); _editor.FontSize = style.Font.Size * _zoom;
        _editor.FontWeight = (FontWeight)Math.Clamp(style.Font.Weight, 1, 999);
        _editor.FontStyle = style.Font.Italic ? FontStyle.Italic : FontStyle.Normal;
        _editor.Foreground = new SolidColorBrush(Color.FromArgb(style.Font.Color.Alpha, style.Font.Color.Red, style.Font.Color.Green, style.Font.Color.Blue));
        var editorBackground = style.Fill.IsVisible
            ? style.Fill.Pattern is CellFillPattern.None or CellFillPattern.Solid
                ? style.Fill.Color
                : style.Fill.BackgroundColor.Alpha == 0 ? _renderTheme.Background : style.Fill.BackgroundColor
            : _renderTheme.Background;
        _editor.Background = new SolidColorBrush(Color.FromArgb(
            editorBackground.Alpha,
            editorBackground.Red,
            editorBackground.Green,
            editorBackground.Blue));
        _editor.BorderBrush = Brushes.Transparent;
        _editor.BorderThickness = new Thickness(0);
        // Mirror the display-list cell text inset (4 DIP horizontal / 1 DIP vertical)
        // after visual zoom. Fixed native padding is disproportionately large below 100%.
        _editor.Padding = new Thickness(4d * _zoom, 1d * _zoom);
        // Formula-bar selection is mirrored into the draft. Preserve those UTF-16 endpoints
        // without painting a second inactive selection band over the cell text.
        _editor.IsInactiveSelectionHighlightEnabled = false;
        _editor.TextWrapping = style.Alignment.WrapText ? TextWrapping.Wrap : TextWrapping.NoWrap;
        _editor.TextAlignment = style.Alignment.Horizontal switch
        {
            CellHorizontalAlignment.Center or CellHorizontalAlignment.CenterContinuous => TextAlignment.Center,
            CellHorizontalAlignment.Right => TextAlignment.Right, _ => TextAlignment.Left,
        };
        _editor.VerticalContentAlignment = style.Alignment.Vertical switch
        {
            CellVerticalAlignment.Top => VerticalAlignment.Top, CellVerticalAlignment.Bottom => VerticalAlignment.Bottom, _ => VerticalAlignment.Center,
        };
    }
    private void UpdateEditorBounds()
    {
        if (!IsEditing || _viewport is null || _session is null) return;
        var snapshot = _scroll.Snapshot;
        if (!_viewport.TryGetCellBounds(_ownedEdit!.Address, snapshot.OffsetX, snapshot.OffsetY, out var cell)) { _editor.IsVisible = false; return; }
        var chrome = Chrome; var frozen = _viewport.GetFrozenPaneExtent();
        var fw = Math.Min(frozen.Width, chrome.BodyWidth); var fh = Math.Min(frozen.Height, chrome.BodyHeight);
        var frozenColumn = _ownedEdit.Address.ColumnIndex < _session.View.FrozenColumns;
        var frozenRow = _ownedEdit.Address.RowIndex < _session.View.FrozenRows;
        var left = chrome.RowHeaderWidth + (frozenColumn ? 0 : fw); var top = chrome.ColumnHeaderHeight + (frozenRow ? 0 : fh);
        var right = chrome.RowHeaderWidth + (frozenColumn ? fw : chrome.BodyWidth); var bottom = chrome.ColumnHeaderHeight + (frozenRow ? fh : chrome.BodyHeight);
        var full = new Rect((chrome.RowHeaderWidth + cell.X) * _zoom, (chrome.ColumnHeaderHeight + cell.Y) * _zoom, cell.Width * _zoom, cell.Height * _zoom);
        var pane = new Rect(left * _zoom, top * _zoom, Math.Max(0, right - left) * _zoom, Math.Max(0, bottom - top) * _zoom);
        var visible = full.Intersect(pane).Intersect(new Rect(Bounds.Size));
        _editor.IsVisible = visible.Width > 0 && visible.Height > 0; _editorBounds = full;
        _editor.Clip = new RectangleGeometry(new Rect(Math.Max(0, visible.X - full.X), Math.Max(0, visible.Y - full.Y), Math.Max(0, visible.Width), Math.Max(0, visible.Height)));
        ApplyEditorStyle(); InvalidateMeasure(); InvalidateArrange();
    }
    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsEditing) return;
        if (TryHandleFormulaAssistanceKey(e.Key, e.KeyModifiers)) { e.Handled = true; return; }
        if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Alt) != 0) return;
        if (e.Key == Key.Escape) { e.Handled = true; CancelEditor(); }
        else if (e.Key is Key.Enter or Key.Tab)
        {
            e.Handled = true;
            RunInput(() =>
            {
                var delta = (e.KeyModifiers & KeyModifiers.Shift) != 0 ? -1 : 1;
                if (CommitEditor()) MoveActiveCell(e.Key == Key.Enter ? delta : 0, e.Key == Key.Tab ? delta : 0, false);
            });
        }
    }
}
