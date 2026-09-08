using global::Avalonia.Input;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Avalonia;

public sealed partial class NeraSpreadsheetControl
{
    private IPointer? _capturedPointer;
    private bool _draggingSelection;
    private SpreadsheetHeaderResizeHandle? _resize;
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Handled || _disposed || _session is null) return;
        e.Handled = true;
        if (PrimaryModifier(e.KeyModifiers)) { Zoom = Math.Clamp(_zoom + Math.Sign(e.Delta.Y) * 0.1, 0.25, 4); return; }
        var dx = -e.Delta.X * 48 / _zoom; var dy = -e.Delta.Y * 48 / _zoom;
        if ((e.KeyModifiers & KeyModifiers.Shift) != 0 && dx == 0) (dx, dy) = (dy, 0);
        QueuePrecisionScroll(dx, dy);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || _disposed || _session is null || _viewport is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var p = e.GetPosition(this);
        if (IsEditing && _editorBounds.Contains(p)) return;
        e.Handled = true;
        RunInput(() =>
        {
            if (IsEditing && !CommitEditor()) return;
            Focus(); var x = p.X / _zoom; var y = p.Y / _zoom;
            if (_layout is not null && SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(x, y, DocumentWidth, DocumentHeight, _renderTheme, _layout, out var resize))
            { _resize = resize; CapturePointer(e.Pointer); return; }
            var hit = SpreadsheetChromeGeometry.HitTest(x, y, DocumentWidth, DocumentHeight, _renderTheme);
            var shift = (e.KeyModifiers & KeyModifiers.Shift) != 0; var additive = PrimaryModifier(e.KeyModifiers); var snapshot = _scroll.Snapshot;
            switch (hit.Region)
            {
                case SpreadsheetChromeRegion.Corner: _session.Selection.SelectAll(); break;
                case SpreadsheetChromeRegion.RowHeader:
                    if (_viewport.TryHitTestRow(hit.BodyY, snapshot.OffsetY, out var row))
                    { if (shift) _session.Selection.ExtendRowsTo(row); else _session.Selection.SelectRow(row, additive); }
                    break;
                case SpreadsheetChromeRegion.ColumnHeader:
                    if (_viewport.TryHitTestColumn(hit.BodyX, snapshot.OffsetX, out var column))
                    { if (shift) _session.Selection.ExtendColumnsTo(column); else _session.Selection.SelectColumn(column, additive); }
                    break;
                case SpreadsheetChromeRegion.Body:
                    if (!_viewport.TryHitTest(hit.BodyX, hit.BodyY, snapshot.OffsetX, snapshot.OffsetY, out var address)) return;
                    if (shift) _session.Selection.ExtendTo(address);
                    else if (additive) _session.Selection.AddRange(new CellRange(address, address));
                    else _session.Selection.SetActiveCell(address);
                    if (e.ClickCount >= 2) BeginEdit(); else { _draggingSelection = true; CapturePointer(e.Pointer); }
                    break;
            }
        });
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_disposed || _session is null || _viewport is null || _capturedPointer is null) return;
        var p = e.GetPosition(this);
        if (_resize is { } resize)
        {
            var size = SpreadsheetHeaderResizeGeometry.CalculateSize(resize, p.X / _zoom, p.Y / _zoom);
            if (resize.Axis == WorksheetAxis.Row) _session.ActiveWorksheet.Dimensions.SetRowHeight(resize.Index, size);
            else _session.ActiveWorksheet.Dimensions.SetColumnWidth(resize.Index, size);
            e.Handled = true;
        }
        else if (_draggingSelection)
        {
            var chrome = Chrome; var snapshot = _scroll.Snapshot;
            if (_viewport.TryHitTest(p.X / _zoom - chrome.RowHeaderWidth, p.Y / _zoom - chrome.ColumnHeaderHeight, snapshot.OffsetX, snapshot.OffsetY, out var address)) _session.Selection.ExtendTo(address);
            e.Handled = true;
        }
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); if (_capturedPointer is null) return; ReleasePointer(); e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _capturedPointer = null; _draggingSelection = false; _resize = null; base.OnPointerCaptureLost(e);
    }
    private void CapturePointer(IPointer pointer) { _capturedPointer = pointer; pointer.Capture(this); }
    private void ReleasePointer()
    {
        var pointer = _capturedPointer; _capturedPointer = null; _draggingSelection = false; _resize = null; pointer?.Capture(null);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _disposed || _session is null || IsEditing) return;
        if (PrimaryModifier(e.KeyModifiers))
        {
            if (e.Key is Key.C or Key.X or Key.V) { e.Handled = true; ExecuteClipboardInput(e.Key); return; }
            Action? command = e.Key switch
            {
                Key.Z => () => { if ((e.KeyModifiers & KeyModifiers.Shift) != 0) _session.Redo(); else _session.Undo(); },
                Key.Y => () => { _session.Redo(); }, Key.B => () => { _session.Styles.ToggleBold(); }, Key.I => () => { _session.Styles.ToggleItalic(); }, _ => null,
            };
            if (command is not null) { e.Handled = true; RunInput(command); return; }
        }
        if (e.Key is Key.F2 or Key.Delete or Key.Enter or Key.Tab)
        {
            e.Handled = true;
            RunInput(() =>
            {
                switch (e.Key)
                {
                    case Key.F2: BeginEdit(); break; case Key.Delete: _session.ClearSelection(); break;
                    case Key.Enter: MoveActiveCell(1, 0, false); break;
                    case Key.Tab: MoveActiveCell(0, (e.KeyModifiers & KeyModifiers.Shift) != 0 ? -1 : 1, false); break;
                }
            });
            return;
        }
        var delta = e.Key switch
        {
            Key.Left => (Row: 0, Column: -1), Key.Right => (Row: 0, Column: 1), Key.Up => (Row: -1, Column: 0), Key.Down => (Row: 1, Column: 0), _ => (Row: 0, Column: 0),
        };
        if (delta == default) return;
        e.Handled = true; MoveActiveCell(delta.Row, delta.Column, (e.KeyModifiers & KeyModifiers.Shift) != 0);
    }
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (e.Handled || _disposed || _session is null || IsEditing || string.IsNullOrEmpty(e.Text) || e.Text.Any(char.IsControl)) return;
        e.Handled = true; RunInput(() => BeginEdit(e.Text));
    }
    private static bool PrimaryModifier(KeyModifiers modifiers) => (modifiers & (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control)) != 0;
    private void MoveActiveCell(int rowDelta, int columnDelta, bool extend)
    {
        if (_session is null) return;
        var next = SpreadsheetVisibleCellNavigation.GetNextVisibleCell(_session.ActiveWorksheet, _session.Selection.ActiveCell, rowDelta, columnDelta);
        if (extend) _session.Selection.ExtendTo(next); else _session.Selection.SetActiveCell(next);
        ScrollCellIntoView(next);
    }
    private void RunInput(Action action)
    {
        try { action(); }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            if (InteractionFailed is not { } handler) throw;
            handler(this, new SpreadsheetInteractionFailedEventArgs(exception));
        }
    }
}
