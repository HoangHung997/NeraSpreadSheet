using System.ComponentModel;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Binds one MAUI viewport to the shared per-worksheet scroll/zoom state. The binding
/// observes Workbook rebinding through BindableObject.PropertyChanged and never stores
/// a second selection/freeze/split model.
/// </summary>
public sealed class NeraWorksheetViewStateBinding : IDisposable
{
    private readonly NeraSpreadsheetView _view;
    private SpreadsheetSession? _session;
    private bool _synchronizing;
    private bool _disposed;

    public NeraWorksheetViewStateBinding(NeraSpreadsheetView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _view.ScrollChanged += OnViewportChanged;
        _view.ZoomChanged += OnViewportChanged;
        _view.SizeChanged += OnSizeChanged;
        _view.PropertyChanged += OnPropertyChanged;
        EnsureSession();
        RestoreActiveWorksheet();
    }

    private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(NeraSpreadsheetView.Workbook))
        {
            EnsureSession();
            RestoreActiveWorksheet();
        }
    }

    private void OnSizeChanged(object? sender, EventArgs e)
    {
        EnsureSession();
        RestoreActiveWorksheet();
    }

    private void EnsureSession()
    {
        var next = _view.Session;
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
        var scroll = _view.ScrollSnapshot;
        _session.View.SetWorksheetViewport(
            _session.ActiveWorksheet,
            scroll.OffsetX,
            scroll.OffsetY,
            _view.Zoom,
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
            if (Math.Abs(_view.Zoom - state.Zoom) > 1e-9)
            {
                _view.Zoom = state.Zoom;
            }
            _view.ScrollTo(state.OffsetX, state.OffsetY, animated: false);
        }
        finally
        {
            _synchronizing = false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _view.ScrollChanged -= OnViewportChanged;
        _view.ZoomChanged -= OnViewportChanged;
        _view.SizeChanged -= OnSizeChanged;
        _view.PropertyChanged -= OnPropertyChanged;
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.View.WorksheetViewChanged -= OnWorksheetViewChanged;
        }
        _session = null;
        GC.SuppressFinalize(this);
    }
}
