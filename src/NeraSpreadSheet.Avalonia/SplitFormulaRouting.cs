using global::Avalonia.Input;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Routes references to the draft owner. The split host calls these methods
/// directly before activation, rather than relying on sibling tunnel-handler order.</summary>
internal sealed class SplitFormulaRouting : IDisposable
{
    private readonly NeraSpreadsheetSplitControl _owner;
    private readonly NeraSpreadsheetControl[] _panes;
    private IPointer? _pointer;
    private NeraSpreadsheetControl? _editor;
    private bool _disposed;

    public SplitFormulaRouting(NeraSpreadsheetSplitControl owner)
    {
        _owner = owner;
        _panes = Enum.GetValues<SpreadsheetSplitViewPane>().Select(owner.GetPane).ToArray();
        foreach (var pane in _panes)
        {
            pane.FormulaProjectionOwner = () => owner.EditingSpreadsheet;
            pane.FormulaAssistanceChanged += OnFormulaChanged;
        }
    }
    private void OnFormulaChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        foreach (var pane in _panes) pane.InvalidateVisual();
    }
    public bool TryPressed(PointerPressedEventArgs e)
    {
        if (_disposed || e.Handled || !e.GetCurrentPoint(_owner).Properties.IsLeftButtonPressed ||
            _owner.EditingSpreadsheet is not { IsFormulaDraft: true } editor) return false;
        if (!TryTarget(e, out var target, out var address) || ReferenceEquals(target, editor)) return false;
        e.Handled = true;
        editor.BeginPointReference(address);
        if (editor.IsFormulaPointMode)
        {
            _editor = editor; _pointer = e.Pointer; e.Pointer.Capture(_owner);
        }
        return true;
    }
    public bool TryMoved(PointerEventArgs e)
    {
        if (_disposed || _pointer != e.Pointer) return false;
        e.Handled = true;
        if (_editor is not { IsEditing: true }) { Cancel(); return true; }
        if (TryTarget(e, out _, out var address)) _editor.UpdatePointReference(address);
        return true;
    }
    public bool TryReleased(PointerReleasedEventArgs e)
    {
        if (_disposed || _pointer != e.Pointer) return false;
        e.Handled = true;
        var editor = _editor;
        _editor = null;
        var pointer = _pointer;
        _pointer = null;
        // EndPointReference restores the original formula-bar/editor focus.
        editor?.EndPointReference();
        pointer?.Capture(null);
        return true;
    }
    private bool TryTarget(PointerEventArgs e, out NeraSpreadsheetControl? target, out CellAddress address)
    {
        target = null; address = default;
        var point = e.GetPosition(_owner);
        var layout = _owner.LastLayout;
        if (layout is null) return false;
        var hit = layout.HitTest(new PointD(point.X, point.Y));
        if (hit.RegionKind != SpreadsheetSplitHitRegionKind.Pane || hit.PaneId is not { } id) return false;
        target = _owner.GetPane((SpreadsheetSplitViewPane)id);
        return target.TryHitFormulaCell(e.GetPosition(target), out address);
    }
    public void Cancel()
    {
        var pointer = _pointer; _pointer = null;
        var editor = _editor; _editor = null;
        editor?.EndPointReference(false);
        pointer?.Capture(null);
    }
    public void Dispose()
    {
        if (_disposed) return; Cancel(); _disposed = true;
        foreach (var pane in _panes) { pane.FormulaAssistanceChanged -= OnFormulaChanged; pane.FormulaProjectionOwner = null; }
    }
}
