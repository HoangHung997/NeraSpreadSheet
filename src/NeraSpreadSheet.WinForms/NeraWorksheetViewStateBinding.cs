using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.WinForms;

/// <summary>
/// Binds the standalone WinForms viewport's continuous scroll to the shared
/// per-worksheet session view state without inventing a WinForms zoom API that the
/// host does not expose. The existing session zoom value is preserved untouched.
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
        _control.ScrollChanged += OnScrollChanged;
        _control.Layout += OnLayout;
        EnsureSession();
        RestoreActiveWorksheet();
    }

    private void OnLayout(object? sender, EventArgs e) => EnsureSession();

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

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => PersistViewport();

    private void PersistViewport()
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
        var current = _session.View.GetWorksheetState(_session.ActiveWorksheet);
        _session.View.SetWorksheetViewport(
            _session.ActiveWorksheet,
            scroll.OffsetX,
            scroll.OffsetY,
            current.Zoom,
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
                state.Zoom,
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
        _control.ScrollChanged -= OnScrollChanged;
        _control.Layout -= OnLayout;
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.View.WorksheetViewChanged -= OnWorksheetViewChanged;
        }
        _session = null;
        GC.SuppressFinalize(this);
    }
}
