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

    /// <summary>Starts one canonical draft. Another view's active editor is never stolen.
    /// Re-entering an owned draft without replacement preserves its text and selection.</summary>
    public bool BeginEdit(string? replacementText = null, bool focusEditor = true)
    {
        VerifyUsable();
        if (_session is null || (_session.Editor.IsEditing && !IsEditing)) return false;
        var wasEditing = IsEditing;
        if (!wasEditing) _ownedEdit = _session.Editor.BeginEdit();
        if (!wasEditing || replacementText is not null)
            _editor.Text = replacementText ?? _ownedEdit!.InitialText;
        ApplyEditorStyle();
        ScrollCellIntoView(_ownedEdit!.Address);
        UpdateEditorBounds();
        if (focusEditor) _editor.Focus();
        if (replacementText is not null) _editor.CaretIndex = _editor.Text?.Length ?? 0;
        else if (!wasEditing) _editor.SelectAll();
        EditorDraftChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>Updates the existing native draft, without creating an undo operation.</summary>
    public void SetEditorText(string text)
    {
        VerifyUsable();
        ArgumentNullException.ThrowIfNull(text);
        if (!IsEditing) throw new InvalidOperationException("This control does not own an active draft.");
        if (!string.Equals(_editor.Text, text, StringComparison.Ordinal)) _editor.Text = text;
    }

    /// <summary>Validation failure leaves the draft intact; only Session.Editor commits history.</summary>
    public bool CommitEditor()
    {
        VerifyUsable();
        if (!IsEditing || _session is null) { HideLocalEditor(); return false; }
        if (!_session.Editor.Commit(_editor.Text ?? string.Empty)) return false;
        HideLocalEditor();
        Focus();
        return true;
    }

    public bool CancelEditor()
    {
        VerifyUsable();
        return EndOwnedEdit(focus: true);
    }

    private bool EndOwnedEdit(bool focus)
    {
        var cancelled = IsEditing && _session!.Editor.Cancel();
        HideLocalEditor();
        // Never restore the old address after session-level sheet activation/cancellation.
        if (cancelled && focus && _attached) Focus();
        return cancelled;
    }

    private void OnCanonicalEditorChanged(object? sender, CellEditStateChangedEventArgs e)
    {
        if (_ownedEdit is not null && !ReferenceEquals(e.State, _ownedEdit)) HideLocalEditor();
    }

    private void OnEditorTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (IsEditing) EditorDraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void HideLocalEditor()
    {
        var hadDraft = _ownedEdit is not null;
        _ownedEdit = null;
        _editor.IsVisible = false;
        _editorBounds = default;
        _editor.Clip = null;
        InvalidateMeasure();
        if (hadDraft) EditorDraftChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyEditorStyle()
    {
        if (_session is null || _ownedEdit is null) return;
        var style = _session.ActiveWorksheet.GetEffectiveStyle(_ownedEdit.Address, _session.Workbook.Styles);
        _editor.FontFamily = new FontFamily(style.Font.Family);
        _editor.FontSize = style.Font.Size * _zoom;
        _editor.FontWeight = (FontWeight)Math.Clamp(style.Font.Weight, 1, 999);
        _editor.FontStyle = style.Font.Italic ? FontStyle.Italic : FontStyle.Normal;
        _editor.Foreground = new SolidColorBrush(Color.FromArgb(style.Font.Color.Alpha,
            style.Font.Color.Red, style.Font.Color.Green, style.Font.Color.Blue));
        _editor.TextWrapping = style.Alignment.WrapText ? TextWrapping.Wrap : TextWrapping.NoWrap;
        _editor.TextAlignment = style.Alignment.Horizontal switch
        {
            CellHorizontalAlignment.Center or CellHorizontalAlignment.CenterContinuous => TextAlignment.Center,
            CellHorizontalAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };
        _editor.VerticalContentAlignment = style.Alignment.Vertical switch
        {
            CellVerticalAlignment.Top => VerticalAlignment.Top,
            CellVerticalAlignment.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center,
        };
    }

    private void UpdateEditorBounds()
    {
        if (!IsEditing || _viewport is null || _session is null) return;
        var snapshot = _scroll.Snapshot;
        if (!_viewport.TryGetCellBounds(_ownedEdit!.Address, snapshot.OffsetX, snapshot.OffsetY, out var cell))
        {
            _editor.IsVisible = false;
            return;
        }
        var chrome = Chrome;
        var frozen = _viewport.GetFrozenPaneExtent();
        var fw = Math.Min(frozen.Width, chrome.BodyWidth);
        var fh = Math.Min(frozen.Height, chrome.BodyHeight);
        var frozenColumn = _ownedEdit.Address.ColumnIndex < _session.View.FrozenColumns;
        var frozenRow = _ownedEdit.Address.RowIndex < _session.View.FrozenRows;
        var left = chrome.RowHeaderWidth + (frozenColumn ? 0 : fw);
        var top = chrome.ColumnHeaderHeight + (frozenRow ? 0 : fh);
        var right = chrome.RowHeaderWidth + (frozenColumn ? fw : chrome.BodyWidth);
        var bottom = chrome.ColumnHeaderHeight + (frozenRow ? fh : chrome.BodyHeight);
        var full = new Rect((chrome.RowHeaderWidth + cell.X) * _zoom,
            (chrome.ColumnHeaderHeight + cell.Y) * _zoom, cell.Width * _zoom, cell.Height * _zoom);
        var pane = new Rect(left * _zoom, top * _zoom,
            Math.Max(0, right - left) * _zoom, Math.Max(0, bottom - top) * _zoom);
        var visible = full.Intersect(pane).Intersect(new Rect(Bounds.Size));
        _editor.IsVisible = visible.Width > 0 && visible.Height > 0;
        _editorBounds = full;
        _editor.Clip = new RectangleGeometry(new Rect(Math.Max(0, visible.X - full.X),
            Math.Max(0, visible.Y - full.Y), Math.Max(0, visible.Width), Math.Max(0, visible.Height)));
        ApplyEditorStyle();
        // Measure the full cell, clip to its pane. Clipping must not reflow a multiline draft.
        InvalidateMeasure();
        InvalidateArrange();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || !IsEditing) return;
        if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Alt) != 0) return;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CancelEditor();
        }
        else if (e.Key is Key.Enter or Key.Tab)
        {
            e.Handled = true;
            RunInput(() =>
            {
                if (CommitEditor()) MoveActiveCell(e.Key == Key.Enter ? 1 : 0,
                    e.Key == Key.Tab ? ((e.KeyModifiers & KeyModifiers.Shift) != 0 ? -1 : 1) : 0, false);
            });
        }
    }
}
