using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Direct2D;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

internal sealed partial class NeraSpreadsheetSplitSurface : Control
{
    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Tab ||
        base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        SynchronizeSession();
        if (_session is null || IsEditing)
        {
            return;
        }

        if (e.Control)
        {
            switch (e.KeyCode)
            {
                case Keys.Z:
                    e.Handled = _session.Undo();
                    break;
                case Keys.Y:
                    e.Handled = _session.Redo();
                    break;
                case Keys.C:
                    _session.Clipboard.CopyPrimarySelection();
                    e.Handled = true;
                    break;
                case Keys.X:
                    e.Handled = _session.Clipboard.CutPrimarySelection();
                    break;
                case Keys.V:
                    e.Handled = _session.Clipboard.PasteAtActiveCell();
                    break;
                case Keys.B:
                    _session.Styles.ToggleBold();
                    e.Handled = true;
                    break;
                case Keys.I:
                    _session.Styles.ToggleItalic();
                    e.Handled = true;
                    break;
            }

            if (e.Handled)
            {
                e.SuppressKeyPress = true;
                return;
            }
        }

        if (e.KeyCode == Keys.Delete)
        {
            e.Handled = _session.ClearSelection();
            e.SuppressKeyPress = e.Handled;
            return;
        }
        if (e.KeyCode == Keys.F2)
        {
            BeginEdit();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode == Keys.Enter)
        {
            MoveActiveCell(1, 0, false);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode == Keys.Tab)
        {
            MoveActiveCell(0, e.Shift ? -1 : 1, false);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }

        var delta = e.KeyCode switch
        {
            Keys.Left => (Row: 0, Column: -1),
            Keys.Right => (Row: 0, Column: 1),
            Keys.Up => (Row: -1, Column: 0),
            Keys.Down => (Row: 1, Column: 0),
            _ => (Row: 0, Column: 0),
        };
        if (delta == default)
        {
            return;
        }

        MoveActiveCell(delta.Row, delta.Column, e.Shift);
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        SynchronizeSession();
        if (_session is null || IsEditing || char.IsControl(e.KeyChar))
        {
            return;
        }

        BeginEdit(e.KeyChar.ToString());
        e.Handled = true;
    }

    private void BeginEdit(string? replacementText = null)
    {
        if (_cellEditor is null || EnsureFrame() is null)
        {
            return;
        }

        var state = _cellEditor.BeginEdit();
        ResetFormulaEditingUi();
        _editor.WordWrap = _session!.ActiveWorksheet.GetEffectiveStyle(state.Address, _session.Workbook.Styles).Alignment.WrapText;
        _editor.Text = replacementText ?? state.InitialText;
        _editor.Visible = true;
        UpdateEditorBounds();
        _editor.Focus();
        if (replacementText is null)
        {
            _editor.SelectAll();
        }
        else
        {
            _editor.SelectionStart = _editor.TextLength;
            _editor.SelectionLength = 0;
        }
        UpdateFormulaSuggestions();
    }

    private bool CommitEditor()
    {
        var address = _cellEditor?.State?.Address;
        if (_cellEditor is null || !_cellEditor.Commit(_editor.Text))
        {
            return false;
        }

        HideEditor();
        if (address is { } target) _session!.Selection.SetActiveCell(target);
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
            _editor.Visible = false;
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
            _editor.Visible = false;
            return;
        }

        var raw = Rectangle.FromLTRB(
            (int)Math.Floor(chrome.RowHeaderWidth + commonBounds.Left),
            (int)Math.Floor(chrome.ColumnHeaderHeight + commonBounds.Top),
            (int)Math.Ceiling(chrome.RowHeaderWidth + commonBounds.Right),
            (int)Math.Ceiling(chrome.ColumnHeaderHeight + commonBounds.Bottom));
        var clip = Rectangle.FromLTRB(
            (int)Math.Ceiling(chrome.RowHeaderWidth + visibleBody.Left),
            (int)Math.Ceiling(chrome.ColumnHeaderHeight + visibleBody.Top),
            (int)Math.Floor(chrome.RowHeaderWidth + visibleBody.Right),
            (int)Math.Floor(chrome.ColumnHeaderHeight + visibleBody.Bottom));
        var visible = Rectangle.Intersect(clip, ClientRectangle);
        if (visible.Width <= 0 || visible.Height <= 0)
        {
            _editor.Visible = false;
            return;
        }

        _editor.Bounds = raw;
        var oldRegion = _editor.Region;
        _editor.Region = new Region(new Rectangle(visible.X - raw.X, visible.Y - raw.Y, visible.Width, visible.Height));
        oldRegion?.Dispose();
        _editor.Visible = true;
        _editor.BringToFront();
        UpdateFormulaSuggestionBounds();
    }

    private void HideEditor()
    {
        ResetFormulaEditingUi();
        _editor.Visible = false;
        _editor.Text = string.Empty;
    }

    private void MoveActiveCell(int rowDelta, int columnDelta, bool extend)
    {
        if (_session is null)
        {
            return;
        }

        var active = _session.Selection.ActiveCell;
        var next = SpreadsheetVisibleCellNavigation.GetNextVisibleCell(
            _session.ActiveWorksheet,
            active,
            rowDelta,
            columnDelta);
        if (extend)
        {
            _session.Selection.ExtendTo(next);
        }
        else
        {
            _session.Selection.SetActiveCell(next);
        }
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (TryHandleFormulaSuggestionKey(e))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode == Keys.Enter)
        {
            if (e.Alt)
            {
                _editor.SelectedText = Environment.NewLine;
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            if (CommitEditor())
            {
                MoveActiveCell(1, 0, false);
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            CancelEditor();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Tab)
        {
            if (CommitEditor())
            {
                MoveActiveCell(0, e.Shift ? -1 : 1, false);
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

}
