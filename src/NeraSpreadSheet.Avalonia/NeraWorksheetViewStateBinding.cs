using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia;

/// <summary>
/// Binds one Avalonia viewport to the per-worksheet session view-state model.
/// The binding stores document-pixel scroll offsets and zoom only; selection,
/// freeze and split state remain owned by <see cref="SpreadsheetViewController"/>.
/// It never writes workbook cells or history.
/// </summary>
public sealed class NeraWorksheetViewStateBinding : IDisposable
{
    private readonly NeraSpreadsheetControl? _single;
    private readonly NeraSpreadsheetSplitControl? _split;
    private readonly NeraSpreadsheetControl[] _panes;
    private SpreadsheetSession? _session;
    private bool _synchronizing;
    private bool _disposed;

    public NeraWorksheetViewStateBinding(NeraSpreadsheetControl control)
    {
        _single = control ?? throw new ArgumentNullException(nameof(control));
        _panes = [control];
        control.SessionChanged += OnSessionChanged;
        control.ViewportChanged += OnSingleViewportChanged;
        control.ZoomChanged += OnSingleViewportChanged;
        SubscribeSession(control.Session);
        RestoreActiveWorksheet();
    }

    public NeraWorksheetViewStateBinding(NeraSpreadsheetSplitControl control)
    {
        _split = control ?? throw new ArgumentNullException(nameof(control));
        _panes = Enum.GetValues<SpreadsheetSplitViewPane>().Select(control.GetPane).ToArray();
        control.SessionChanged += OnSessionChanged;
        foreach (var pane in _panes) pane.ZoomChanged += OnSplitZoomChanged;
        SubscribeSession(control.Session);
        RestoreActiveWorksheet();
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        SubscribeSession(_split?.Session ?? _single?.Session);
        RestoreActiveWorksheet();
    }

    private void SubscribeSession(SpreadsheetSession? session)
    {
        if (ReferenceEquals(_session, session)) return;
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.View.WorksheetViewChanged -= OnWorksheetViewChanged;
        }
        _session = session;
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
            _session.View.WorksheetViewChanged += OnWorksheetViewChanged;
        }
    }

    private void OnActiveWorksheetChanged(object? sender, EventArgs e) => RestoreActiveWorksheet();

    private void OnWorksheetViewChanged(object? sender, SpreadsheetWorksheetViewChangedEventArgs e)
    {
        if (_disposed || _session is null || ReferenceEquals(e.Source, this) ||
            !ReferenceEquals(e.Worksheet, _session.ActiveWorksheet)) return;
        RestoreActiveWorksheet();
    }

    private void OnSingleViewportChanged(object? sender, EventArgs e)
    {
        if (_synchronizing || _disposed || _session is null || _single is null) return;
        var scroll = _single.ScrollSnapshot;
        _session.View.SetWorksheetViewport(_session.ActiveWorksheet, scroll.OffsetX, scroll.OffsetY, _single.Zoom, this);
    }

    private void OnSplitZoomChanged(object? sender, EventArgs e)
    {
        if (_synchronizing || _disposed || _session is null || sender is not NeraSpreadsheetControl source) return;
        _synchronizing = true;
        try
        {
            foreach (var pane in _panes)
                if (!ReferenceEquals(pane, source) && pane.Zoom != source.Zoom) pane.Zoom = source.Zoom;
        }
        finally { _synchronizing = false; }
        var state = _session.View.GetWorksheetState(_session.ActiveWorksheet);
        _session.View.SetWorksheetViewport(_session.ActiveWorksheet, state.OffsetX, state.OffsetY, source.Zoom, this);
    }

    private void RestoreActiveWorksheet()
    {
        if (_synchronizing || _disposed || _session is null) return;
        var state = _session.View.GetWorksheetState(_session.ActiveWorksheet);
        _synchronizing = true;
        try
        {
            if (_single is not null)
            {
                if (_single.Zoom != state.Zoom) _single.Zoom = state.Zoom;
                _single.ScrollTo(state.OffsetX, state.OffsetY);
            }
            else
            {
                foreach (var pane in _panes)
                    if (pane.Zoom != state.Zoom) pane.Zoom = state.Zoom;
                // NeraSpreadsheetSplitControl already restores all four pane
                // offsets/topology from Session.View.SplitState on worksheet change.
            }
        }
        finally { _synchronizing = false; }

        if (_single is not null && !_session.View.IsRestoringWorksheetView)
        {
            var actual = _single.ScrollSnapshot;
            _session.View.SetWorksheetViewport(_session.ActiveWorksheet, actual.OffsetX, actual.OffsetY, _single.Zoom, this);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_single is not null)
        {
            _single.SessionChanged -= OnSessionChanged;
            _single.ViewportChanged -= OnSingleViewportChanged;
            _single.ZoomChanged -= OnSingleViewportChanged;
        }
        if (_split is not null)
        {
            _split.SessionChanged -= OnSessionChanged;
            foreach (var pane in _panes) pane.ZoomChanged -= OnSplitZoomChanged;
        }
        SubscribeSession(null);
        GC.SuppressFinalize(this);
    }
}
