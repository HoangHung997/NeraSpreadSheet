using System.Runtime.CompilerServices;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

public sealed class NeraSpreadsheetHeaderReorderController : IDisposable
{
    private const double GeometryEpsilon = 1e-9;
    private readonly System.Windows.Forms.Timer _autoScrollTimer;
    private readonly HeaderReorderPreviewControl _preview;
    private NeraSpreadsheetControl? _owner;
    private SpreadsheetSession? _observedSession;
    private SpreadsheetViewportEngine? _viewport;
    private HeaderReorderState? _state;
    private SpreadsheetSplitHeaderReorderDropTarget? _dropTarget;
    private DateTime _lastAutoScrollUtc;
    private double _pointerX;
    private double _pointerY;
    private bool _completing;

    internal NeraSpreadsheetHeaderReorderController(
        NeraSpreadsheetControl owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        ObjectDisposedException.ThrowIf(owner.IsDisposed, owner);

        _preview = new HeaderReorderPreviewControl
        {
            Visible = false,
            TabStop = false,
        };
        owner.Controls.Add(_preview);
        _preview.BringToFront();

        _autoScrollTimer = new System.Windows.Forms.Timer { Interval = 16 };
        _autoScrollTimer.Tick += OnAutoScrollTick;
        owner.MouseDown += OnOwnerMouseDown;
        owner.MouseMove += OnOwnerMouseMove;
        owner.MouseUp += OnOwnerMouseUp;
        owner.MouseCaptureChanged += OnOwnerMouseCaptureChanged;
        owner.Resize += OnOwnerResize;
        owner.Disposed += OnOwnerDisposed;
    }

    public bool IsDisposed => _owner is null;

    public bool IsDragging => _state is { IsActive: true };

    public bool IsAutoScrolling =>
        IsDragging &&
        !SpreadsheetHeaderReorderAutoScroll.IsZero(AutoScrollVelocity);

    public SpreadsheetSplitHeaderReorderDropTarget? DropTarget => _dropTarget;

    public PointD AutoScrollVelocity
    {
        get
        {
            if (_state is not { IsActive: true } state || _owner is null)
            {
                return default;
            }

            return SpreadsheetHeaderReorderAutoScroll.CalculateVelocity(
                state.Axis,
                _pointerX,
                _pointerY,
                _owner.ClientSize.Width,
                _owner.ClientSize.Height,
                _owner.RenderTheme);
        }
    }

    public void Dispose()
    {
        var owner = _owner;
        if (owner is null)
        {
            return;
        }

        Cancel();
        _owner = null;
        owner.MouseDown -= OnOwnerMouseDown;
        owner.MouseMove -= OnOwnerMouseMove;
        owner.MouseUp -= OnOwnerMouseUp;
        owner.MouseCaptureChanged -= OnOwnerMouseCaptureChanged;
        owner.Resize -= OnOwnerResize;
        owner.Disposed -= OnOwnerDisposed;
        _autoScrollTimer.Tick -= OnAutoScrollTick;
        _autoScrollTimer.Dispose();
        if (!owner.IsDisposed)
        {
            owner.Controls.Remove(_preview);
        }
        _preview.Dispose();
        NeraSpreadsheetHeaderReorderExtensions.Remove(owner, this);
        GC.SuppressFinalize(this);
    }

    private void OnOwnerMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left ||
            _owner is null ||
            _owner.Session is null ||
            _owner.TryGetSplitPaneController(out _))
        {
            return;
        }

        if (!TryCreatePaneLayout(out var paneLayout, out var layout) ||
            SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(
                e.X,
                e.Y,
                _owner.ClientSize.Width,
                _owner.ClientSize.Height,
                _owner.RenderTheme,
                layout,
                out _) ||
            !SpreadsheetSplitHeaderReorderGeometry.TryHitSource(
                e.X,
                e.Y,
                _owner.ClientSize.Width,
                _owner.ClientSize.Height,
                _owner.RenderTheme,
                paneLayout,
                out var source))
        {
            return;
        }

        var (sourceIndex, count) = ResolveSourceRange(
            _owner.Session,
            source.Axis,
            source.Index);
        _state = new HeaderReorderState(
            source.Axis,
            sourceIndex,
            count,
            new PointD(e.X, e.Y),
            IsActive: false);
        _pointerX = e.X;
        _pointerY = e.Y;
        _dropTarget = null;
        HidePreview();
    }

    private void OnOwnerMouseMove(object? sender, MouseEventArgs e)
    {
        if (_state is null)
        {
            return;
        }

        _pointerX = e.X;
        _pointerY = e.Y;
        var leftPressed =
            (Control.MouseButtons & MouseButtons.Left) != MouseButtons.None;
        if (!UpdateDrag(leftPressed))
        {
            return;
        }

        ScheduleDragCursor();
    }

    private void OnOwnerMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || _state is null)
        {
            return;
        }

        _pointerX = e.X;
        _pointerY = e.Y;
        Complete();
    }

    private void OnOwnerMouseCaptureChanged(object? sender, EventArgs e)
    {
        if (_completing ||
            _owner is null ||
            _owner.Capture ||
            _state is not { IsActive: true })
        {
            return;
        }

        Cancel();
    }

    private void OnOwnerResize(object? sender, EventArgs e)
    {
        if (_state is { IsActive: true })
        {
            UpdateDropTarget();
        }
    }

    private void OnOwnerDisposed(object? sender, EventArgs e) => Dispose();

    private bool UpdateDrag(bool leftButtonPressed)
    {
        if (_state is not { } state)
        {
            return false;
        }
        if (!leftButtonPressed)
        {
            Cancel();
            return false;
        }

        if (!state.IsActive)
        {
            if (!SpreadsheetSplitHeaderReorderGeometry.HasExceededDragThreshold(
                    state.StartPoint,
                    new PointD(_pointerX, _pointerY)))
            {
                return false;
            }

            state = state with { IsActive = true };
            _state = state;
            if (_owner is not null)
            {
                _owner.Capture = true;
            }
        }

        UpdateDropTarget();
        UpdateAutoScrollTimer();
        return true;
    }

    private void UpdateDropTarget()
    {
        if (_state is not { IsActive: true } state ||
            _owner is null ||
            !TryCreatePaneLayout(out var paneLayout, out _))
        {
            _dropTarget = null;
            HidePreview();
            return;
        }

        if (SpreadsheetSplitHeaderReorderGeometry.TryGetDropTarget(
                state.Axis,
                state.SourceIndex,
                state.Count,
                _pointerX,
                _pointerY,
                _owner.ClientSize.Width,
                _owner.ClientSize.Height,
                _owner.RenderTheme,
                paneLayout,
                out var target))
        {
            _dropTarget = target;
            ShowPreview(target);
        }
        else
        {
            _dropTarget = null;
            HidePreview();
        }
    }

    private void Complete()
    {
        if (_state is not { } state)
        {
            return;
        }

        var wasActive = state.IsActive;
        if (wasActive)
        {
            UpdateDropTarget();
        }
        var target = _dropTarget;
        var owner = _owner;
        var session = owner?.Session;

        _completing = true;
        try
        {
            ClearState(releaseCapture: true);
        }
        finally
        {
            _completing = false;
        }

        if (!wasActive || target is not { IsNoOp: false } || session is null)
        {
            return;
        }

        try
        {
            session.Reorder.Move(target.Value.Move);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ArgumentException)
        {
            System.Media.SystemSounds.Beep.Play();
        }
    }

    private void Cancel() => ClearState(releaseCapture: true);

    private void ClearState(bool releaseCapture)
    {
        var owner = _owner;
        _state = null;
        _dropTarget = null;
        _autoScrollTimer.Stop();
        HidePreview();
        if (releaseCapture && owner is { Capture: true })
        {
            owner.Capture = false;
        }
        if (owner is not null && !owner.IsDisposed)
        {
            owner.Cursor = Cursors.Default;
        }
    }

    private void UpdateAutoScrollTimer()
    {
        if (!IsAutoScrolling)
        {
            _autoScrollTimer.Stop();
            return;
        }

        if (!_autoScrollTimer.Enabled)
        {
            _lastAutoScrollUtc = DateTime.UtcNow;
            _autoScrollTimer.Start();
        }
    }

    private void OnAutoScrollTick(object? sender, EventArgs e)
    {
        if (_owner is null || _state is not { IsActive: true })
        {
            _autoScrollTimer.Stop();
            return;
        }

        var velocity = AutoScrollVelocity;
        if (SpreadsheetHeaderReorderAutoScroll.IsZero(velocity))
        {
            _autoScrollTimer.Stop();
            return;
        }

        var now = DateTime.UtcNow;
        var elapsed = now - _lastAutoScrollUtc;
        _lastAutoScrollUtc = now;
        if (elapsed > TimeSpan.FromMilliseconds(100d))
        {
            elapsed = TimeSpan.FromMilliseconds(100d);
        }

        var delta = SpreadsheetHeaderReorderAutoScroll.CalculateDelta(
            velocity,
            elapsed);
        var chrome = SpreadsheetChromeGeometry.Calculate(
            _owner.ClientSize.Width,
            _owner.ClientSize.Height,
            _owner.RenderTheme);
        var snapshot = _owner.ScrollSnapshot;
        var maximumX = Math.Max(0d, _owner.ContentWidth - chrome.BodyWidth);
        var maximumY = Math.Max(0d, _owner.ContentHeight - chrome.BodyHeight);
        var nextX = Math.Clamp(snapshot.OffsetX + delta.X, 0d, maximumX);
        var nextY = Math.Clamp(snapshot.OffsetY + delta.Y, 0d, maximumY);
        if (Math.Abs(nextX - snapshot.OffsetX) <= GeometryEpsilon &&
            Math.Abs(nextY - snapshot.OffsetY) <= GeometryEpsilon)
        {
            return;
        }

        _owner.ScrollTo(nextX, nextY, animated: false);
        UpdateDropTarget();
    }

    private bool TryCreatePaneLayout(
        out SpreadsheetSplitPaneChromeLayout[] paneLayout,
        out NeraSpreadSheet.Layout.ViewportLayout layout)
    {
        paneLayout = [];
        layout = null!;
        var owner = _owner;
        var session = owner?.Session;
        if (owner is null ||
            session is null ||
            owner.ClientSize.Width <= 0 ||
            owner.ClientSize.Height <= 0)
        {
            return false;
        }

        var chrome = SpreadsheetChromeGeometry.Calculate(
            owner.ClientSize.Width,
            owner.ClientSize.Height,
            owner.RenderTheme);
        if (chrome.BodyWidth <= 0d || chrome.BodyHeight <= 0d)
        {
            return false;
        }

        if (!ReferenceEquals(_observedSession, session))
        {
            _observedSession = session;
            _viewport = new SpreadsheetViewportEngine(session);
        }
        _viewport!.InvalidateMetrics();
        var scroll = owner.ScrollSnapshot;
        var frame = _viewport.Compose(
            scroll.OffsetX,
            scroll.OffsetY,
            chrome.BodyWidth,
            chrome.BodyHeight,
            owner.OverscanPixels,
            owner.RenderTheme);
        layout = frame.Layout;
        paneLayout =
        [
            new SpreadsheetSplitPaneChromeLayout(
                SpreadsheetPaneId.TopLeft,
                new RectD(0d, 0d, chrome.BodyWidth, chrome.BodyHeight),
                layout),
        ];
        return true;
    }

    private static (int SourceIndex, int Count) ResolveSourceRange(
        SpreadsheetSession session,
        WorksheetAxis axis,
        int hitIndex)
    {
        if (session.Selection.Ranges.Count == 1)
        {
            var range = session.Selection.Ranges[0];
            if (axis == WorksheetAxis.Row &&
                range.Left == 0 &&
                range.Right == SpreadsheetLimits.MaxColumns - 1 &&
                hitIndex >= range.Top &&
                hitIndex <= range.Bottom)
            {
                return (range.Top, range.RowCount);
            }
            if (axis == WorksheetAxis.Column &&
                range.Top == 0 &&
                range.Bottom == SpreadsheetLimits.MaxRows - 1 &&
                hitIndex >= range.Left &&
                hitIndex <= range.Right)
            {
                return (range.Left, range.ColumnCount);
            }
        }

        return (hitIndex, 1);
    }

    private void ShowPreview(SpreadsheetSplitHeaderReorderDropTarget target)
    {
        var owner = _owner;
        if (owner is null)
        {
            return;
        }

        var bounds = Rectangle.FromLTRB(
            (int)Math.Floor(target.PreviewBounds.Left),
            (int)Math.Floor(target.PreviewBounds.Top),
            (int)Math.Ceiling(target.PreviewBounds.Right),
            (int)Math.Ceiling(target.PreviewBounds.Bottom));
        bounds = Rectangle.Intersect(bounds, owner.ClientRectangle);
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            HidePreview();
            return;
        }

        var sourceColor = target.IsNoOp
            ? owner.RenderTheme.HeaderBorder
            : owner.RenderTheme.ActivePaneBorder;
        _preview.BackColor = System.Drawing.Color.FromArgb(
            sourceColor.Alpha,
            sourceColor.Red,
            sourceColor.Green,
            sourceColor.Blue);
        _preview.Bounds = bounds;
        _preview.Visible = true;
        _preview.BringToFront();
    }

    private void HidePreview() => _preview.Visible = false;

    private void ScheduleDragCursor()
    {
        var owner = _owner;
        if (owner is null || owner.IsDisposed || !owner.IsHandleCreated)
        {
            return;
        }

        owner.BeginInvoke((Action)(() =>
        {
            if (_owner is not null && IsDragging && !_owner.IsDisposed)
            {
                _owner.Cursor = Cursors.SizeAll;
            }
        }));
    }

    private readonly record struct HeaderReorderState(
        WorksheetAxis Axis,
        int SourceIndex,
        int Count,
        PointD StartPoint,
        bool IsActive);

    private sealed class HeaderReorderPreviewControl : Control
    {
        private const int WindowMessageNonClientHitTest = 0x0084;
        private static readonly IntPtr HitTestTransparent = new(-1);

        internal HeaderReorderPreviewControl()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.Opaque,
                true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            using var brush = new System.Drawing.SolidBrush(BackColor);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == WindowMessageNonClientHitTest)
            {
                message.Result = HitTestTransparent;
                return;
            }
            base.WndProc(ref message);
        }
    }
}

public static class NeraSpreadsheetHeaderReorderExtensions
{
    private static readonly ConditionalWeakTable<
        NeraSpreadsheetControl,
        NeraSpreadsheetHeaderReorderController> Controllers = new();
    private static readonly object SyncRoot = new();

    public static NeraSpreadsheetHeaderReorderController EnableHeaderReordering(
        this NeraSpreadsheetControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        ObjectDisposedException.ThrowIf(control.IsDisposed, control);

        lock (SyncRoot)
        {
            if (Controllers.TryGetValue(control, out var existing))
            {
                if (!existing.IsDisposed)
                {
                    return existing;
                }
                Controllers.Remove(control);
            }

            var controller = new NeraSpreadsheetHeaderReorderController(control);
            Controllers.Add(control, controller);
            return controller;
        }
    }

    public static bool TryGetHeaderReorderController(
        this NeraSpreadsheetControl control,
        out NeraSpreadsheetHeaderReorderController controller)
    {
        ArgumentNullException.ThrowIfNull(control);
        lock (SyncRoot)
        {
            if (Controllers.TryGetValue(control, out var existing) &&
                !existing.IsDisposed)
            {
                controller = existing;
                return true;
            }
        }

        controller = null!;
        return false;
    }

    public static bool DisableHeaderReordering(
        this NeraSpreadsheetControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        NeraSpreadsheetHeaderReorderController controller;
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
        NeraSpreadsheetHeaderReorderController controller)
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
