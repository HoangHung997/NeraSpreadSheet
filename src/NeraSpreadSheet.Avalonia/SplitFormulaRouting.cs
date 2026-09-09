using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Avalonia;

/// <summary>One capture owner routes every reference drag across split panes.
/// The original native editor owns the draft; the hovered pane owns its viewport.</summary>
internal sealed class SplitFormulaRouting : IDisposable
{
    private readonly NeraSpreadsheetSplitControl _owner;
    private readonly NeraSpreadsheetControl[] _panes;
    private IPointer? _pointer;
    private NeraSpreadsheetControl? _editor;
    private NeraSpreadsheetControl? _lastTarget;
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
        if (_editor is { IsEditing: false }) Cancel();
        foreach (var pane in _panes) pane.InvalidateVisual();
    }
    public bool TryPressed(PointerPressedEventArgs e)
    {
        if (_disposed || e.Handled || !e.GetCurrentPoint(_owner).Properties.IsLeftButtonPressed ||
            _owner.EditingSpreadsheet is not { IsFormulaDraft: true } editor) return false;
        if (e.Source is Visual source &&
            (source is TextBox || source.GetVisualAncestors().Any(static parent => parent is TextBox))) return false;
        if (!TryTarget(e, out var target, out var address)) return false;
        e.Handled = true;
        Cancel();
        e.Pointer.Capture(_owner);
        try
        {
            editor.BeginReferenceGesture(target!, e.GetPosition(target), address);
            if (editor.IsFormulaPointMode)
            {
                _editor = editor; _pointer = e.Pointer; _lastTarget = target;
                editor.TrackFormulaPointer(target!, e.GetPosition(target));
            }
        }
        finally { if (_pointer != e.Pointer) e.Pointer.Capture(null); }
        return true;
    }
    public bool TryMoved(PointerEventArgs e)
    {
        if (_disposed || _pointer != e.Pointer) return false;
        e.Handled = true;
        if (_editor is not { IsEditing: true }) { Cancel(); return true; }
        TrackTarget(e); return true;
    }
    public bool TryReleased(PointerReleasedEventArgs e)
    {
        if (_disposed || _pointer != e.Pointer) return false;
        e.Handled = true;
        var editor = _editor;
        if (editor is { IsEditing: true }) { TrackTarget(e); editor.AdvanceFormulaPointerFrame(TimeSpan.Zero); }
        _editor = null; _lastTarget = null;
        var pointer = _pointer; _pointer = null;
        editor?.EndPointReference(); pointer?.Capture(null); return true;
    }
    private void TrackTarget(PointerEventArgs e)
    {
        if (_editor is null) return;
        if (TryTarget(e, out var target, out _))
        {
            _lastTarget = target; _editor.TrackFormulaPointer(target!, e.GetPosition(target));
        }
        else if (_lastTarget is not null && !new Rect(_owner.Bounds.Size).Contains(e.GetPosition(_owner)))
            _editor.TrackFormulaPointer(_lastTarget, e.GetPosition(_lastTarget));
        else _editor.StopFormulaPointerTracking();
    }
    private bool TryTarget(PointerEventArgs e, out NeraSpreadsheetControl? target, out CellAddress address)
    {
        target = null; address = default;
        var point = e.GetPosition(_owner); var layout = _owner.LastLayout;
        if (layout is null) return false;
        var hit = layout.HitTest(new PointD(point.X, point.Y));
        if (hit.RegionKind != SpreadsheetSplitHitRegionKind.Pane || hit.PaneId is not { } id) return false;
        target = _owner.GetPane((SpreadsheetSplitViewPane)id);
        return target.TryHitFormulaCell(e.GetPosition(target), out address);
    }
    public void Cancel()
    {
        var pointer = _pointer; _pointer = null;
        var editor = _editor; _editor = null; _lastTarget = null;
        editor?.EndPointReference(false); pointer?.Capture(null);
    }
    public void Dispose()
    {
        if (_disposed) return;
        Cancel(); _disposed = true;
        foreach (var pane in _panes) { pane.FormulaAssistanceChanged -= OnFormulaChanged; pane.FormulaProjectionOwner = null; }
    }
}
