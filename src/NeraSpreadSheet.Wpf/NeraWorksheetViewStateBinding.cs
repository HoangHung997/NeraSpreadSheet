using System.Windows;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// Binds the standalone WPF viewport's continuous scroll/zoom to the shared per-worksheet
/// session state. Selection/freeze/split remain owned by <see cref="SpreadsheetViewController"/>.
/// </summary>
public sealed class NeraWorksheetViewStateBinding : IDisposable
{
    private readonly NeraSpreadsheetControl _control;
    private SpreadsheetSession? _session;
    private bool _synchronizing;
    private bool _disposed;

    public NeraWorksheetViewStateBinding(NeraSpreadsheetControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _control.ScrollChanged += OnViewportChanged;
        _control.ZoomChanged += OnViewportChanged;
        _control.Loaded += OnControlLoaded;
        _control.LayoutUpdated += OnLayoutUpdated;
        EnsureSession();
        RestoreActiveWorksheet();
    }

    private void OnControlLoaded(object sender, RoutedEventArgs e)
    {
        EnsureSession();
        RestoreActiveWorksheet();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => EnsureSession();

    private void EnsureSession()
    {
        var next = _control.Session;
        if (ReferenceEquals(next, _session))
        {
            return;
        }
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.View.WorksheetViewChanged -= OnWorksheetViewChanged;
        }
        _session = next;
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
            !ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            return;
        }
        RestoreActiveWorksheet();
    }

    private void OnViewportChanged(object? sender, EventArgs e)
    {
        if (_disposed || _synchronizing)
        {
            return;
        }
        EnsureSession();
        if (_session is null)
        {
            return;
        }
        var scroll = _control.ScrollSnapshot;
        _session.View.SetWorksheetViewport(
            _session.ActiveWorksheet,
            scroll.OffsetX,
            scroll.OffsetY,
            _control.Zoom,
            this);
    }

    private void RestoreActiveWorksheet()
    {
        if (_disposed || _synchronizing)
        {
            return;
        }
        EnsureSession();
        if (_session is null)
        {
            return;
        }
        var state = _session.View.GetWorksheetState(_session.ActiveWorksheet);
        _synchronizing = true;
        try
        {
            if (Math.Abs(_control.Zoom - state.Zoom) > 1e-9)
            {
                _control.Zoom = state.Zoom;
            }
            _control.ScrollTo(state.OffsetX, state.OffsetY, animated: false);
        }
        finally
        {
            _synchronizing = false;
        }
        if (!_session.View.IsRestoringWorksheetView)
        {
            var actual = _control.ScrollSnapshot;
            _session.View.SetWorksheetViewport(
                _session.ActiveWorksheet,
                actual.OffsetX,
                actual.OffsetY,
                _control.Zoom,
                this);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _control.ScrollChanged -= OnViewportChanged;
        _control.ZoomChanged -= OnViewportChanged;
        _control.Loaded -= OnControlLoaded;
        _control.LayoutUpdated -= OnLayoutUpdated;
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.View.WorksheetViewChanged -= OnWorksheetViewChanged;
        }
        _session = null;
        GC.SuppressFinalize(this);
    }
}
