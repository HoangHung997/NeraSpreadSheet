using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Wpf;

internal sealed partial class NeraSpreadsheetSplitAdorner : Adorner
{
    private bool IsEditing => _cellEditor?.IsEditing == true;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        SynchronizeSession();
        if (_disposed || _session is null || IsEditing)
        {
            return;
        }

        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            switch (e.Key)
            {
                case Key.Z:
                    e.Handled = _session.Undo();
                    break;
                case Key.Y:
                    e.Handled = _session.Redo();
                    break;
                case Key.C:
                    _session.Clipboard.CopyPrimarySelection();
                    e.Handled = true;
                    break;
                case Key.X:
                    e.Handled = _session.Clipboard.CutPrimarySelection();
                    break;
                case Key.V:
                    e.Handled = _session.Clipboard.PasteAtActiveCell();
                    break;
                case Key.B:
                    _session.Styles.ToggleBold();
                    e.Handled = true;
                    break;
                case Key.I:
                    _session.Styles.ToggleItalic();
                    e.Handled = true;
                    break;
            }

            if (e.Handled)
            {
                return;
            }
        }

        if (e.Key == Key.Delete)
        {
            e.Handled = _session.ClearSelection();
            return;
        }
        if (e.Key == Key.F2)
        {
            BeginEdit();
            e.Handled = true;
            return;
        }
        if (e.Key is Key.Enter or Key.Return)
        {
            MoveActiveCell(1, 0, false);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Tab)
        {
            MoveActiveCell(
                0,
                (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1,
                false);
            e.Handled = true;
            return;
        }

        var delta = e.Key switch
        {
            Key.Left => (Row: 0, Column: -1),
            Key.Right => (Row: 0, Column: 1),
            Key.Up => (Row: -1, Column: 0),
            Key.Down => (Row: 1, Column: 0),
            _ => (Row: 0, Column: 0),
        };
        if (delta == default)
        {
            return;
        }

        MoveActiveCell(
            delta.Row,
            delta.Column,
            (Keyboard.Modifiers & ModifierKeys.Shift) != 0);
        e.Handled = true;
    }

    protected override void OnTextInput(TextCompositionEventArgs e)
    {
        base.OnTextInput(e);
        SynchronizeSession();
        if (_disposed ||
            _session is null ||
            IsEditing ||
            string.IsNullOrEmpty(e.Text) ||
            e.Text.Any(char.IsControl))
        {
            return;
        }

        BeginEdit(e.Text);
        e.Handled = true;
    }

    private void BeginEdit(string? replacementText = null)
    {
        if (_cellEditor is null || EnsureFrame() is null)
        {
            return;
        }

        var state = _cellEditor.BeginEdit();
        _editor.Text = replacementText ?? state.InitialText;
        _editor.Visibility = Visibility.Visible;
        UpdateEditorBounds();
        _editor.Focus();
        if (replacementText is null)
        {
            _editor.SelectAll();
        }
        else
        {
            _editor.CaretIndex = _editor.Text.Length;
        }
    }

    private bool CommitEditor()
    {
        if (_cellEditor is null || !_cellEditor.Commit(_editor.Text))
        {
            return false;
        }

        HideEditor();
        Focus();
        return true;
    }

    private bool CancelEditor()
    {
        if (_cellEditor is null || !_cellEditor.Cancel())
        {
            return false;
        }

        HideEditor();
        Focus();
        return true;
    }

    private void UpdateEditorBounds()
    {
        if (_cellEditor?.State is not { } state ||
            _engine is null ||
            _session is null ||
            EnsureFrame() is not { } frame ||
            !frame.TryGetPane(frame.ActivePane, out var paneFrame) ||
            !_engine.TryGetCellBounds(frame.ActivePane, state.Address, out var bodyBounds))
        {
            _editor.Visibility = Visibility.Collapsed;
            _editorBounds = Rect.Empty;
            InvalidateArrange();
            return;
        }

        var chrome = GetChromeMetrics();
        var localBounds = bodyBounds.Translate(
            -paneFrame.Pane.Bounds.X,
            -paneFrame.Pane.Bounds.Y);
        var layout = paneFrame.ViewportFrame.Layout;
        var frozenColumn = state.Address.ColumnIndex < _session.View.FrozenColumns;
        var frozenRow = state.Address.RowIndex < _session.View.FrozenRows;
        var paneClip = new RectD(
            paneFrame.Pane.Bounds.X + (frozenColumn ? 0d : layout.FrozenWidth),
            paneFrame.Pane.Bounds.Y + (frozenRow ? 0d : layout.FrozenHeight),
            frozenColumn
                ? layout.FrozenWidth
                : Math.Max(0d, paneFrame.Pane.Bounds.Width - layout.FrozenWidth),
            frozenRow
                ? layout.FrozenHeight
                : Math.Max(0d, paneFrame.Pane.Bounds.Height - layout.FrozenHeight));
        var commonBounds = localBounds.Translate(
            paneFrame.Pane.Bounds.X,
            paneFrame.Pane.Bounds.Y);
        var visibleBody = commonBounds.Intersect(paneClip);
        if (visibleBody.IsEmpty)
        {
            _editor.Visibility = Visibility.Collapsed;
            _editorBounds = Rect.Empty;
            InvalidateArrange();
            return;
        }

        var candidate = new Rect(
            chrome.RowHeaderWidth + visibleBody.Left,
            chrome.ColumnHeaderHeight + visibleBody.Top,
            Math.Max(20d, visibleBody.Width),
            Math.Max(18d, visibleBody.Height));
        var viewport = new Rect(0d, 0d, Math.Max(0d, ActualWidth), Math.Max(0d, ActualHeight));
        var visible = Rect.Intersect(candidate, viewport);
        if (visible.IsEmpty || visible.Width <= 0d || visible.Height <= 0d)
        {
            _editor.Visibility = Visibility.Collapsed;
            _editorBounds = Rect.Empty;
            InvalidateArrange();
            return;
        }

        _editor.Visibility = Visibility.Visible;
        _editorBounds = visible;
        InvalidateArrange();
    }

    private void HideEditor()
    {
        _editor.Visibility = Visibility.Collapsed;
        _editorBounds = Rect.Empty;
        _editor.Text = string.Empty;
        InvalidateArrange();
    }

    private void MoveActiveCell(int rowDelta, int columnDelta, bool extend)
    {
        if (_session is null)
        {
            return;
        }

        var active = _session.Selection.ActiveCell;
        var row = Math.Clamp(active.RowIndex + rowDelta, 0, SpreadsheetLimits.MaxRows - 1);
        var column = Math.Clamp(
            active.ColumnIndex + columnDelta,
            0,
            SpreadsheetLimits.MaxColumns - 1);
        var next = new CellAddress(row, column);
        if (extend)
        {
            _session.Selection.ExtendTo(next);
        }
        else
        {
            _session.Selection.SetActiveCell(next);
        }
    }

    private void OnEditorKeyDown(object sender, KeyEventArgs e)
    {
        if (_disposed)
        {
            return;
        }

        if (e.Key is Key.Enter or Key.Return)
        {
            if (CommitEditor())
            {
                MoveActiveCell(1, 0, false);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            CancelEditor();
            e.Handled = true;
        }
        else if (e.Key == Key.Tab)
        {
            if (CommitEditor())
            {
                MoveActiveCell(
                    0,
                    (Keyboard.Modifiers & ModifierKeys.Shift) != 0 ? -1 : 1,
                    false);
            }
            e.Handled = true;
        }
    }
}
