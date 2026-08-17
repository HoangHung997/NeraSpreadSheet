using System.Windows.Forms;
using System.Runtime.CompilerServices;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Direct2D;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

public enum SpreadsheetSplitPaneMode
{
    None,
    Vertical,
    Horizontal,
    Both,
}

public sealed class SpreadsheetSplitChangedEventArgs : EventArgs
{
    public SpreadsheetSplitChangedEventArgs(
        SpreadsheetSplitPaneMode mode,
        double? splitX,
        double? splitY,
        SpreadsheetSplitLayout? layout)
    {
        Mode = mode;
        SplitX = splitX;
        SplitY = splitY;
        Layout = layout;
    }

    public SpreadsheetSplitPaneMode Mode { get; }

    public double? SplitX { get; }

    public double? SplitY { get; }

    public SpreadsheetSplitLayout? Layout { get; }
}

public sealed class SpreadsheetPaneScrollChangedEventArgs : EventArgs
{
    public SpreadsheetPaneScrollChangedEventArgs(
        SpreadsheetPaneId paneId,
        ScrollSnapshot snapshot)
    {
        PaneId = paneId;
        Snapshot = snapshot;
    }

    public SpreadsheetPaneId PaneId { get; }

    public ScrollSnapshot Snapshot { get; }
}

public sealed class NeraSpreadsheetSplitController : IDisposable
{
    private NeraSpreadsheetControl? _owner;
    private NeraSpreadsheetSplitSurface? _surface;

    internal NeraSpreadsheetSplitController(NeraSpreadsheetControl owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        if (owner.IsEditing)
        {
            owner.CancelEditor();
        }
        _surface = new NeraSpreadsheetSplitSurface(owner);
        _surface.SplitChanged += OnSurfaceSplitChanged;
        _surface.PaneScrollChanged += OnSurfacePaneScrollChanged;
        owner.Invalidated += OnOwnerInvalidated;
        owner.BackColorChanged += OnOwnerVisualChanged;
        owner.FontChanged += OnOwnerVisualChanged;
        owner.Disposed += OnOwnerDisposed;
        owner.Controls.Add(_surface);
        _surface.BringToFront();
    }

    public bool IsDisposed => _surface is null;

    public SpreadsheetSplitPaneMode Mode => GetSurface().Mode;

    public double? SplitX => GetSurface().SplitX;

    public double? SplitY => GetSurface().SplitY;

    public double SeparatorThickness
    {
        get => GetSurface().SeparatorThickness;
        set => GetSurface().SeparatorThickness = value;
    }

    public double MinimumPaneExtent
    {
        get => GetSurface().MinimumPaneExtent;
        set => GetSurface().MinimumPaneExtent = value;
    }

    public SpreadsheetPaneId ActivePane => GetSurface().ActivePane;

    public SpreadsheetSplitViewportFrame? LastFrame => GetSurface().LastFrame;

    public Direct2DRendererDiagnostics? Direct2DDiagnostics => GetSurface().Direct2DDiagnostics;

    public Direct2DSwapChainRendererDiagnostics? SwapChainDiagnostics => GetSurface().SwapChainDiagnostics;

    public event EventHandler<SpreadsheetSplitChangedEventArgs>? SplitChanged;

    public event EventHandler<SpreadsheetPaneScrollChangedEventArgs>? PaneScrollChanged;

    public void SetMode(SpreadsheetSplitPaneMode mode) => GetSurface().SetMode(mode);

    public void SetSplit(double? splitX, double? splitY) => GetSurface().SetSplit(splitX, splitY);

    public void ClearSplit() => SetMode(SpreadsheetSplitPaneMode.None);

    public void SetActivePane(SpreadsheetPaneId paneId) => GetSurface().SetActivePane(paneId);

    public PointD GetPaneScroll(SpreadsheetPaneId paneId) => GetSurface().GetPaneScroll(paneId);

    public ScrollSnapshot GetPaneScrollSnapshot(SpreadsheetPaneId paneId) =>
        GetSurface().GetPaneScrollSnapshot(paneId);

    public void ScrollPaneTo(
        SpreadsheetPaneId paneId,
        double offsetX,
        double offsetY,
        bool animated = false) =>
        GetSurface().ScrollPaneTo(paneId, offsetX, offsetY, animated);

    public void QueuePaneScroll(SpreadsheetPaneId paneId, ScrollDelta delta) =>
        GetSurface().QueuePaneScroll(paneId, delta);

    public void QueueActivePaneScroll(ScrollDelta delta) =>
        GetSurface().QueueActivePaneScroll(delta);

    public bool TryHitTest(
        double clientX,
        double clientY,
        out SpreadsheetPaneId paneId,
        out CellAddress address) =>
        GetSurface().TryHitTest(clientX, clientY, out paneId, out address);

    public void RenderNow() => GetSurface().Refresh();

    public void Focus() => GetSurface().Focus();

    public void Dispose()
    {
        var owner = _owner;
        var surface = _surface;
        if (owner is null || surface is null)
        {
            return;
        }

        _owner = null;
        _surface = null;
        surface.SplitChanged -= OnSurfaceSplitChanged;
        surface.PaneScrollChanged -= OnSurfacePaneScrollChanged;
        owner.Invalidated -= OnOwnerInvalidated;
        owner.BackColorChanged -= OnOwnerVisualChanged;
        owner.FontChanged -= OnOwnerVisualChanged;
        owner.Disposed -= OnOwnerDisposed;
        if (!owner.IsDisposed)
        {
            owner.Controls.Remove(surface);
            owner.Invalidate();
        }
        surface.Dispose();
        NeraSpreadsheetSplitExtensions.Remove(owner, this);
        GC.SuppressFinalize(this);
    }

    private NeraSpreadsheetSplitSurface GetSurface()
    {
        ObjectDisposedException.ThrowIf(_surface is null, this);
        return _surface!;
    }

    private void OnSurfaceSplitChanged(object? sender, SpreadsheetSplitChangedEventArgs e) =>
        SplitChanged?.Invoke(this, e);

    private void OnSurfacePaneScrollChanged(object? sender, SpreadsheetPaneScrollChangedEventArgs e) =>
        PaneScrollChanged?.Invoke(this, e);

    private void OnOwnerInvalidated(object? sender, InvalidateEventArgs e) => _surface?.Invalidate();

    private void OnOwnerVisualChanged(object? sender, EventArgs e) => _surface?.Invalidate();

    private void OnOwnerDisposed(object? sender, EventArgs e) => Dispose();
}

public static class NeraSpreadsheetSplitExtensions
{
    private static readonly ConditionalWeakTable<NeraSpreadsheetControl, NeraSpreadsheetSplitController> Controllers = new();
    private static readonly object SyncRoot = new();

    public static NeraSpreadsheetSplitController EnableSplitPanes(
        this NeraSpreadsheetControl control,
        SpreadsheetSplitPaneMode mode = SpreadsheetSplitPaneMode.Vertical)
    {
        ArgumentNullException.ThrowIfNull(control);
        ObjectDisposedException.ThrowIf(control.IsDisposed, control);

        lock (SyncRoot)
        {
            if (Controllers.TryGetValue(control, out var existing))
            {
                if (existing.IsDisposed)
                {
                    Controllers.Remove(control);
                }
                else
                {
                    existing.SetMode(mode);
                    existing.Focus();
                    return existing;
                }
            }

            var controller = new NeraSpreadsheetSplitController(control);
            Controllers.Add(control, controller);
            controller.SetMode(mode);
            controller.Focus();
            return controller;
        }
    }

    public static bool TryGetSplitPaneController(
        this NeraSpreadsheetControl control,
        out NeraSpreadsheetSplitController controller)
    {
        ArgumentNullException.ThrowIfNull(control);
        lock (SyncRoot)
        {
            if (Controllers.TryGetValue(control, out var existing) && !existing.IsDisposed)
            {
                controller = existing;
                return true;
            }
        }

        controller = null!;
        return false;
    }

    public static bool DisableSplitPanes(this NeraSpreadsheetControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        NeraSpreadsheetSplitController controller;
        lock (SyncRoot)
        {
            if (!Controllers.TryGetValue(control, out var existing))
            {
                return false;
            }
            controller = existing;
            Controllers.Remove(control);
        }

        controller.Dispose();
        return true;
    }

    internal static void Remove(
        NeraSpreadsheetControl control,
        NeraSpreadsheetSplitController controller)
    {
        lock (SyncRoot)
        {
            if (Controllers.TryGetValue(control, out var current) &&
                ReferenceEquals(current, controller))
            {
                Controllers.Remove(control);
            }
        }
    }
}
