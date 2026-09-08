using global::Avalonia;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Routes cross-pane reference pointing to the one canonical draft owner.</summary>
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
        owner.AddHandler(InputElement.PointerPressedEvent, OnPressed, RoutingStrategies.Tunnel);
        owner.AddHandler(InputElement.PointerMovedEvent, OnMoved, RoutingStrategies.Tunnel);
        owner.AddHandler(InputElement.PointerReleasedEvent, OnReleased, RoutingStrategies.Tunnel);
    }
    private void OnFormulaChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;
        foreach (var pane in _panes) pane.InvalidateVisual();
    }
    private void OnPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_disposed || e.Handled || !e.GetCurrentPoint(_owner).Properties.IsLeftButtonPressed ||
            _owner.EditingSpreadsheet is not { IsFormulaDraft: true } editor) return;
        if (!TryTarget(e, out var target, out var address) || ReferenceEquals(target, editor)) return;
        e.Handled = true;
        editor.BeginPointReference(address);
        if (!editor.IsFormulaPointMode) return;
        _editor = editor; _pointer = e.Pointer; e.Pointer.Capture(_owner);
    }
    private void OnMoved(object? sender, PointerEventArgs e)
    {
        if (_disposed || _pointer != e.Pointer) return;
        e.Handled = true;
        if (_editor is not { IsEditing: true }) { Cancel(); return; }
        if (TryTarget(e, out _, out var address)) _editor.UpdatePointReference(address);
    }
    private void OnReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_pointer != e.Pointer) return;
        e.Handled = true;
        var editor = _editor;
        Cancel();
        if (editor?.IsEditing == true) editor.FocusEditor();
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
        _owner.RemoveHandler(InputElement.PointerPressedEvent, OnPressed);
        _owner.RemoveHandler(InputElement.PointerMovedEvent, OnMoved);
        _owner.RemoveHandler(InputElement.PointerReleasedEvent, OnReleased);
        foreach (var pane in _panes) { pane.FormulaAssistanceChanged -= OnFormulaChanged; pane.FormulaProjectionOwner = null; }
    }
}
