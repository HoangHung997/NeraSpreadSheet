using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.CompilerServices;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

public sealed class NeraSpreadsheetSplitScrollBarController : IDisposable
{
    private readonly NeraSpreadsheetControl _owner;
    private readonly NeraSpreadsheetSplitController _split;
    private NeraSpreadsheetSplitScrollBarOverlay? _overlay;
    private SpreadsheetSplitScrollBarStyle _style;

    internal NeraSpreadsheetSplitScrollBarController(
        NeraSpreadsheetControl owner,
        NeraSpreadsheetSplitController split,
        SpreadsheetSplitScrollBarStyle? style)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _split = split ?? throw new ArgumentNullException(nameof(split));
        _style = style ?? new SpreadsheetSplitScrollBarStyle();
        var surface = owner.Controls
            .OfType<NeraSpreadsheetSplitSurface>()
            .SingleOrDefault() ??
            throw new InvalidOperationException(
                "The WinForms split surface is not available.");
        _overlay = new NeraSpreadsheetSplitScrollBarOverlay(
            owner,
            split,
            surface,
            _style);
        _overlay.MouseDown += OnOverlayMouseDown;
        _overlay.MouseUp += OnOverlayMouseUp;
        _overlay.MouseCaptureChanged += OnOverlayMouseCaptureChanged;
        surface.Controls.Add(_overlay);
        _overlay.BringToFront();
        split.RenderNow();
        _overlay.RefreshFromSplitFrame();
    }

    public bool IsDisposed => _overlay is null;

    public bool IsVisible
    {
        get => GetOverlay().Visible;
        set
        {
            GetOverlay().Visible = value;
            GetOverlay().RefreshFromSplitFrame();
        }
    }

    public SpreadsheetSplitScrollBarStyle Style
    {
        get => _style;
        set
        {
            _style = value ?? throw new ArgumentNullException(nameof(value));
            GetOverlay().ScrollBarStyle = value;
        }
    }

    public SpreadsheetSplitScrollBarLayout? Layout =>
        GetOverlay().ScrollBarLayout;

    public int ScrollBarCount => Layout?.Count ?? 0;

    public SpreadsheetSplitScrollBarHit HitTest(double bodyX, double bodyY) =>
        Layout?.HitTest(new PointD(bodyX, bodyY)) ?? default;

    public void Refresh()
    {
        _split.RenderNow();
        GetOverlay().RefreshFromSplitFrame();
    }

    public void Dispose()
    {
        var overlay = _overlay;
        if (overlay is null)
        {
            return;
        }

        _overlay = null;
        overlay.MouseDown -= OnOverlayMouseDown;
        overlay.MouseUp -= OnOverlayMouseUp;
        overlay.MouseCaptureChanged -= OnOverlayMouseCaptureChanged;
        _split.CommitViewHistory();
        if (!overlay.Parent?.IsDisposed == true)
        {
            overlay.Parent?.Controls.Remove(overlay);
        }
        overlay.Dispose();
        NeraSpreadsheetSplitScrollBarExtensions.Remove(_owner, this);
        GC.SuppressFinalize(this);
    }

    private NeraSpreadsheetSplitScrollBarOverlay GetOverlay()
    {
        ObjectDisposedException.ThrowIf(_overlay is null, this);
        return _overlay!;
    }

    private void OnOverlayMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _split.BeginViewHistory(
                "Use pane scrollbar",
                SpreadsheetSplitViewChangeKind.PaneScroll);
        }
    }

    private void OnOverlayMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _split.CommitViewHistory();
        }
    }

    private void OnOverlayMouseCaptureChanged(object? sender, EventArgs e)
    {
        if (sender is Control { Capture: false })
        {
            _split.CommitViewHistory();
        }
    }
}

public static class NeraSpreadsheetSplitScrollBarExtensions
{
    private static readonly ConditionalWeakTable<
        NeraSpreadsheetControl,
        NeraSpreadsheetSplitScrollBarController> Controllers = new();
    private static readonly object SyncRoot = new();

    public static NeraSpreadsheetSplitScrollBarController EnableSplitPaneScrollBars(
        this NeraSpreadsheetControl control,
        SpreadsheetSplitScrollBarStyle? style = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        ObjectDisposedException.ThrowIf(control.IsDisposed, control);
        lock (SyncRoot)
        {
            if (Controllers.TryGetValue(control, out var existing))
            {
                if (!existing.IsDisposed)
                {
                    if (style is not null)
                    {
                        existing.Style = style;
                    }
                    existing.Refresh();
                    return existing;
                }
                Controllers.Remove(control);
            }

            var split = control.TryGetSplitPaneController(out var existingSplit)
                ? existingSplit
                : control.EnableSplitPanes();
            var controller = new NeraSpreadsheetSplitScrollBarController(
                control,
                split,
                style);
            Controllers.Add(control, controller);
            return controller;
        }
    }

    public static bool TryGetSplitPaneScrollBarController(
        this NeraSpreadsheetControl control,
        out NeraSpreadsheetSplitScrollBarController controller)
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

    public static bool DisableSplitPaneScrollBars(
        this NeraSpreadsheetControl control)
    {
        ArgumentNullException.ThrowIfNull(control);
        NeraSpreadsheetSplitScrollBarController controller;
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
        NeraSpreadsheetSplitScrollBarController controller)
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

internal sealed class NeraSpreadsheetSplitScrollBarOverlay : Control
{
    private readonly NeraSpreadsheetControl _owner;
    private readonly NeraSpreadsheetSplitController _split;
    private readonly NeraSpreadsheetSplitSurface _surface;
    private readonly SpreadsheetSplitScrollBarInteractionController _interaction = new();
    private SpreadsheetSplitScrollBarStyle _style;
    private SpreadsheetSplitScrollBarLayout? _layout;
    private Region? _scrollBarRegion;

    internal NeraSpreadsheetSplitScrollBarOverlay(
        NeraSpreadsheetControl owner,
        NeraSpreadsheetSplitController split,
        NeraSpreadsheetSplitSurface surface,
        SpreadsheetSplitScrollBarStyle style)
    {
        _owner = owner;
        _split = split;
        _surface = surface;
        _style = style;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        TabStop = false;
        _surface.Paint += OnSurfacePaint;
        _surface.Resize += OnSurfaceResize;
        _split.SplitChanged += OnSplitChanged;
        _split.PaneScrollChanged += OnPaneScrollChanged;
        _owner.Disposed += OnOwnerDisposed;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal SpreadsheetSplitScrollBarStyle ScrollBarStyle
    {
        get => _style;
        set
        {
            _style = value;
            RefreshFromSplitFrame();
        }
    }

    internal SpreadsheetSplitScrollBarLayout? ScrollBarLayout => _layout;

    internal void RefreshFromSplitFrame()
    {
        if (IsDisposed || !Visible)
        {
            return;
        }

        var chrome = SpreadsheetChromeGeometry.Calculate(
            _surface.ClientSize.Width,
            _surface.ClientSize.Height,
            _owner.RenderTheme);
        SetBounds(
            (int)Math.Ceiling(chrome.RowHeaderWidth),
            (int)Math.Ceiling(chrome.ColumnHeaderHeight),
            Math.Max(0, (int)Math.Floor(chrome.BodyWidth)),
            Math.Max(0, (int)Math.Floor(chrome.BodyHeight)));

        var frame = _split.LastFrame;
        if (frame is null || Width <= 0 || Height <= 0)
        {
            _layout = null;
            ReplaceRegion(null);
            Invalidate();
            return;
        }

        var contentExtent = GetContentExtent(frame);
        _layout = frame.CreateScrollBarLayout(contentExtent, _style);
        ReplaceRegion(CreateRegion(_layout));
        Invalidate();
        BringToFront();
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_layout is null)
        {
            return;
        }

        e.Graphics.SmoothingMode = SmoothingMode.None;
        foreach (var scrollBar in _layout.ScrollBars)
        {
            using var trackBrush = new SolidBrush(ToColor(_style.TrackColor));
            using var thumbBrush = new SolidBrush(ToColor(
                scrollBar.PaneId == _split.ActivePane
                    ? _style.ActiveThumbColor
                    : _style.ThumbColor));
            using var borderPen = new Pen(ToColor(_style.BorderColor), 1f);
            var track = ToRectangleF(scrollBar.TrackBounds);
            var thumb = ToRectangleF(scrollBar.ThumbBounds);
            e.Graphics.FillRectangle(trackBrush, track);
            e.Graphics.FillRectangle(thumbBrush, thumb);
            e.Graphics.DrawRectangle(
                borderPen,
                thumb.X,
                thumb.Y,
                Math.Max(0f, thumb.Width - 1f),
                Math.Max(0f, thumb.Height - 1f));
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _layout is null)
        {
            return;
        }

        var result = _interaction.BeginPointer(
            _layout,
            new PointD(e.X, e.Y));
        if (!result.Handled)
        {
            return;
        }

        if (result.ScrollRequest is { } request)
        {
            ApplyRequest(request);
        }
        else if (_interaction.DragPaneId is { } paneId)
        {
            _split.SetActivePane(paneId);
        }
        Capture = result.IsDragging;
        Cursor = result.IsDragging ? Cursors.Hand : Cursors.Default;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_layout is null)
        {
            return;
        }

        if (_interaction.IsDragging)
        {
            var result = _interaction.MovePointer(new PointD(e.X, e.Y));
            if (result.ScrollRequest is { } request)
            {
                ApplyRequest(request);
            }
            Cursor = Cursors.Hand;
            return;
        }

        Cursor = _layout.HitTest(new PointD(e.X, e.Y)).IsHit
            ? Cursors.Hand
            : Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        _interaction.EndPointer();
        Capture = false;
        Cursor = _layout?.HitTest(new PointD(e.X, e.Y)).IsHit == true
            ? Cursors.Hand
            : Cursors.Default;
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture)
        {
            _interaction.Cancel();
        }
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_layout is null)
        {
            return;
        }

        var hit = _layout.HitTest(new PointD(e.X, e.Y));
        var paneId = hit.IsHit ? hit.PaneId : _split.ActivePane;
        _split.SetActivePane(paneId);
        var delta = -(e.Delta / 120d) * _owner.WheelPixelsPerNotch;
        _split.QueuePaneScroll(
            paneId,
            (ModifierKeys & Keys.Shift) != 0
                ? new ScrollDelta(delta, 0d, ScrollInputKind.Wheel)
                : new ScrollDelta(0d, delta, ScrollInputKind.Wheel));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _surface.Paint -= OnSurfacePaint;
            _surface.Resize -= OnSurfaceResize;
            _split.SplitChanged -= OnSplitChanged;
            _split.PaneScrollChanged -= OnPaneScrollChanged;
            _owner.Disposed -= OnOwnerDisposed;
            _scrollBarRegion?.Dispose();
            _scrollBarRegion = null;
        }
        base.Dispose(disposing);
    }

    private void ApplyRequest(SpreadsheetSplitScrollRequest request)
    {
        var current = _split.GetPaneScroll(request.PaneId);
        _split.SetActivePane(request.PaneId);
        _split.ScrollPaneTo(
            request.PaneId,
            request.Axis == SpreadsheetScrollBarAxis.Horizontal
                ? request.Offset
                : current.X,
            request.Axis == SpreadsheetScrollBarAxis.Vertical
                ? request.Offset
                : current.Y,
            animated: false);
        _surface.Invalidate();
    }

    private void OnSurfacePaint(object? sender, PaintEventArgs e) =>
        RefreshFromSplitFrame();

    private void OnSurfaceResize(object? sender, EventArgs e) =>
        RefreshFromSplitFrame();

    private void OnSplitChanged(
        object? sender,
        SpreadsheetSplitChangedEventArgs e) =>
        RefreshFromSplitFrame();

    private void OnPaneScrollChanged(
        object? sender,
        SpreadsheetPaneScrollChangedEventArgs e) =>
        RefreshFromSplitFrame();

    private void OnOwnerDisposed(object? sender, EventArgs e) => Dispose();

    private void ReplaceRegion(Region? next)
    {
        var previous = _scrollBarRegion;
        _scrollBarRegion = next;
        Region = next;
        previous?.Dispose();
    }

    private static Region? CreateRegion(
        SpreadsheetSplitScrollBarLayout layout)
    {
        Region? region = null;
        foreach (var scrollBar in layout.ScrollBars)
        {
            var rectangle = Rectangle.Ceiling(ToRectangleF(
                Inflate(scrollBar.TrackBounds, layout.Style.HitSlop)));
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                continue;
            }

            if (region is null)
            {
                region = new Region(rectangle);
            }
            else
            {
                region.Union(rectangle);
            }
        }
        return region;
    }

    private static SizeD GetContentExtent(
        SpreadsheetSplitViewportFrame frame)
    {
        if (frame.Panes.Count == 0)
        {
            return default;
        }

        var layout = frame.Panes[0].ViewportFrame.Layout;
        return new SizeD(layout.ContentWidth, layout.ContentHeight);
    }

    private static RectD Inflate(RectD bounds, double amount) =>
        amount <= 0d
            ? bounds
            : new RectD(
                bounds.X - amount,
                bounds.Y - amount,
                bounds.Width + (amount * 2d),
                bounds.Height + (amount * 2d));

    private static RectangleF ToRectangleF(RectD bounds) =>
        new(
            (float)bounds.X,
            (float)bounds.Y,
            (float)bounds.Width,
            (float)bounds.Height);

    private static Color ToColor(ColorRgba color) =>
        Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);
}
