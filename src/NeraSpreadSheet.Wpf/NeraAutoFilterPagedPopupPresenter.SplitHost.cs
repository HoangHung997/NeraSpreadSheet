using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

public sealed partial class NeraAutoFilterPagedPopupPresenter
{
    // Presentation identity only; filter data and mutations remain in the shared binding.
    private readonly record struct NativeFilterButton(SpreadsheetAutoFilterButtonHit Hit, SpreadsheetPaneId? Pane);
    private sealed record FilterOpenContext(long Generation, SpreadsheetSession Session, Worksheet Worksheet,
        UIElement Surface, NeraSpreadsheetSplitController? Split, NativeFilterButton Button);

    private UIElement? _inputSurface;
    private NeraSpreadsheetSplitController? _splitHost;
    private SpreadsheetSession? _hostSession;
    private Worksheet? _hostWorksheet;
    private SpreadsheetSplitViewportFrame? _obsoleteFrame;
    private NativeFilterButton[] _nativeButtons = [];
    private FilterOpenContext? _openContext;
    private long _openGeneration;
    private bool _refreshQueued;
    private bool _refreshingHost;
    private bool _splitWasAttached;
    private (long Generation, NativeFilterButton Button)? _pendingPointer;

    private void SynchronizeHost()
    {
        if (_disposed) return;
        var split = _control.TryGetSplitPaneController(out var candidate) ? candidate : null;
        UIElement surface = split?.InputSurface ?? _control;
        var session = _control.Session;
        var worksheet = session?.ActiveWorksheet;
        if (ReferenceEquals(_inputSurface, surface) && ReferenceEquals(_hostSession, session) &&
            ReferenceEquals(_hostWorksheet, worksheet)) return;

        var worksheetChanged = _hostWorksheet is not null && !ReferenceEquals(_hostWorksheet, worksheet);
        CloseForHostChange();
        DetachHost();
        _splitHost = split;
        _inputSurface = surface;
        _hostSession = session;
        _hostWorksheet = worksheet;
        _obsoleteFrame = worksheetChanged ? split?.PresentedFrame : null;
        _splitWasAttached = split?.IsAttached == true;
        _nativeButtons = [];
        surface.PreviewKeyDown += OnControlPreviewKeyDown;
        surface.PreviewMouseMove += OnPreviewMouseMove;
        surface.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        surface.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
        surface.LostMouseCapture += OnFilterLostMouseCapture;
        if (split is not null) split.PresentationChanged += OnHostPresentationChanged;
        if (session is not null) session.ActiveWorksheetChanged += OnHostIdentityChanged;
        if (worksheet is not null)
        {
            worksheet.CellsChanged += OnHostDataChanged;
            worksheet.Dimensions.Changed += OnHostDataChanged;
        }
        // The filter glyph overlay must remain above a newly attached split adorner.
        DetachAdorner();
        if (_control.IsLoaded) AttachAdorner();
    }

    private void DetachHost()
    {
        CancelPointerOpen();
        if (_inputSurface is { } surface)
        {
            surface.PreviewKeyDown -= OnControlPreviewKeyDown;
            surface.PreviewMouseMove -= OnPreviewMouseMove;
            surface.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            surface.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            surface.LostMouseCapture -= OnFilterLostMouseCapture;
        }
        if (_splitHost is { } split) split.PresentationChanged -= OnHostPresentationChanged;
        if (_hostSession is { } session) session.ActiveWorksheetChanged -= OnHostIdentityChanged;
        if (_hostWorksheet is { } worksheet)
        {
            worksheet.CellsChanged -= OnHostDataChanged;
            worksheet.Dimensions.Changed -= OnHostDataChanged;
        }
        _inputSurface = null;
        _splitHost = null;
        _hostSession = null;
        _hostWorksheet = null;
    }

    private void OnControllerChanged(object? sender, EventArgs e)
    {
        if (!ReferenceEquals(sender, _control)) return;
        SynchronizeHost();
        QueueHostRefresh();
    }

    private void OnHostIdentityChanged(object? sender, EventArgs e)
    {
        SynchronizeHost();
        QueueHostRefresh();
    }

    private void OnHostDataChanged(object? sender, EventArgs e) => QueueHostRefresh();

    private void OnHostPresentationChanged(object? sender, EventArgs e)
    {
        SynchronizeHost();
        var attached = _splitHost?.IsAttached == true;
        if (attached != _splitWasAttached)
        {
            _splitWasAttached = attached;
            CloseForHostChange();
            DetachAdorner();
            if (_control.IsLoaded) AttachAdorner();
        }
        if (_openContext is { } context && !IsCurrentContext(context)) CloseForHostChange();
        QueueHostRefresh();
    }

    private void QueueHostRefresh()
    {
        if (_disposed || _refreshQueued) return;
        _refreshQueued = true;
        _control.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(() =>
        {
            _refreshQueued = false;
            if (!_disposed) RefreshHostPresentation();
        }));
    }

    private bool IsHostReady => _inputSurface is FrameworkElement { IsLoaded: true } &&
        (_splitHost is null || (!_splitHost.IsDisposed && _splitHost.IsAttached));

    private void RefreshHostPresentation()
    {
        if (_disposed || _refreshingHost) return;
        _refreshingHost = true;
        try
        {
            SynchronizeHost();
            if (!IsHostReady || _hostSession is null || !_control.RenderTheme.ShowTableFilterButtons)
            {
                _nativeButtons = [];
            }
            else if (_splitHost is { } split)
            {
                // Raw input never composes a frame. Use the exact frame published by the native renderer.
                var frame = split.PresentedFrame;
                if (frame is null || ReferenceEquals(frame, _obsoleteFrame)) return;
                _obsoleteFrame = null;
                var chrome = SpreadsheetChromeGeometry.Calculate(_control.ActualWidth, _control.ActualHeight, _control.RenderTheme);
                var buttons = new List<NativeFilterButton>();
                foreach (var pane in frame.Panes)
                {
                    var right = frame.ScrollBars.TryGetBar(pane.Pane.PaneId, SpreadsheetScrollBarOrientation.Vertical, out var vertical)
                        ? Math.Min(pane.Pane.Bounds.Right, vertical.Bounds.Left) : pane.Pane.Bounds.Right;
                    var bottom = frame.ScrollBars.TryGetBar(pane.Pane.PaneId, SpreadsheetScrollBarOrientation.Horizontal, out var horizontal)
                        ? Math.Min(pane.Pane.Bounds.Bottom, horizontal.Bounds.Top) : pane.Pane.Bounds.Bottom;
                    var clip = new RectD(pane.Pane.Bounds.X, pane.Pane.Bounds.Y,
                        Math.Max(0d, right - pane.Pane.Bounds.X), Math.Max(0d, bottom - pane.Pane.Bounds.Y));
                    foreach (var hit in SpreadsheetAutoFilterButtonGeometry.GetVisibleButtons(
                        _hostSession.ActiveWorksheet.Tables, _hostSession.ActiveWorksheet.AutoFilter,
                        pane.ViewportFrame.Layout, _control.RenderTheme))
                    {
                        var bounds = hit.Bounds.Translate(pane.Pane.Bounds.X, pane.Pane.Bounds.Y).Intersect(clip);
                        if (!bounds.IsEmpty) buttons.Add(new NativeFilterButton(hit with
                        {
                            Bounds = bounds.Translate(chrome.RowHeaderWidth, chrome.ColumnHeaderHeight),
                        }, pane.Pane.PaneId));
                    }
                }
                _nativeButtons = buttons.ToArray();
            }
            else
            {
                _nativeButtons = ComposeStandaloneButtons().Select(static hit => new NativeFilterButton(hit, null)).ToArray();
            }
            _adorner?.Refresh();
            if (_openContext is not { } context || _popup is not { } popup) return;
            var anchor = _nativeButtons.FirstOrDefault(button => SameHeader(button, context.Button));
            if (!IsCurrentContext(context) || anchor.Hit.Bounds.IsEmpty)
            {
                CloseForHostChange();
                return;
            }
            popup.HorizontalOffset = Math.Max(0d, anchor.Hit.Bounds.Left);
            popup.VerticalOffset = Math.Max(0d, anchor.Hit.Bounds.Bottom);
        }
        finally { _refreshingHost = false; }
    }

    private static bool SameHeader(NativeFilterButton left, NativeFilterButton right) =>
        left.Pane == right.Pane && left.Hit.OwnerKind == right.Hit.OwnerKind &&
        left.Hit.TableId == right.Hit.TableId && left.Hit.TableColumnId == right.Hit.TableColumnId &&
        left.Hit.FilterRange == right.Hit.FilterRange && left.Hit.HeaderCell == right.Hit.HeaderCell;

    private bool IsCurrentContext(FilterOpenContext context) =>
        !_disposed && context.Generation == _openGeneration &&
        ReferenceEquals(_control.Session, context.Session) && ReferenceEquals(context.Session.ActiveWorksheet, context.Worksheet) &&
        ReferenceEquals(_inputSurface, context.Surface) && IsHostReady &&
        ReferenceEquals(_splitHost, context.Split) && (context.Split is null || context.Split.ActivePane == context.Button.Pane);

    private bool IsCurrentBinding(NeraWpfAutoFilterPagedBinding? binding) =>
        binding is not null && ReferenceEquals(_binding, binding) && _openContext is { } context && IsCurrentContext(context);

    private void CloseForHostChange()
    {
        CancelPointerOpen();
        _openGeneration++;
        Close();
        CancelOperations();
        DisposeBinding();
    }
}
