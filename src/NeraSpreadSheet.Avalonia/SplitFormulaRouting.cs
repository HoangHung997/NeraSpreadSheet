using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Routes reference gestures from every pane to the original draft owner.
/// The split host calls this before pane activation. Native text editing keeps its
/// own pointer handling; worksheet reference drags are captured by the split host.</summary>
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
        if (_editor is { IsEditing: false }) Cancel();
        foreach (var pane in _panes) pane.InvalidateVisual();
    }

    public bool TryPressed(PointerPressedEventArgs e)
    {
        if (_disposed || e.Handled || !e.GetCurrentPoint(_owner).Properties.IsLeftButtonPressed ||
            _owner.EditingSpreadsheet is not { IsFormulaDraft: true } editor) return false;
        if (e.Source is global::Avalonia.Visual source &&
            (source is TextBox || source.GetVisualAncestors().Any(static parent => parent is TextBox))) return false;
        if (!TryTarget(e, out _, out var address)) return false;
        e.Handled = true;
        Cancel();
        // Avalonia initially captures the hit child. Transfer capture BEFORE
        // creating the new point anchor: the child's PointerCaptureLost handler
        // must not cancel the point gesture that we are about to start.
        e.Pointer.Capture(_owner);
        try
        {
            editor.BeginPointReference(address);
            if (editor.IsFormulaPointMode)
            {
                _editor = editor;
                _pointer = e.Pointer;
            }
        }
        finally
        {
            if (_pointer != e.Pointer) e.Pointer.Capture(null);
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
        editor?.EndPointReference();
        pointer?.Capture(null);
        return true;
    }

    private bool TryTarget(PointerEventArgs e, out NeraSpreadsheetControl? target, out CellAddress address)
    {
        target = null;
        address = default;
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
        var pointer = _pointer;
        _pointer = null;
        var editor = _editor;
        _editor = null;
        editor?.EndPointReference(false);
        pointer?.Capture(null);
    }

    public void Dispose()
    {
        if (_disposed) return;
        Cancel();
        _disposed = true;
        foreach (var pane in _panes)
        {
            pane.FormulaAssistanceChanged -= OnFormulaChanged;
            pane.FormulaProjectionOwner = null;
        }
    }
}
