using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

public sealed class ScrollChangedEventArgs : EventArgs
{
    public ScrollChangedEventArgs(ScrollSnapshot snapshot) { Snapshot = snapshot; }
    public ScrollSnapshot Snapshot { get; }
}

public sealed class NeraSpreadsheetControl : Control
{
    private readonly ContinuousScrollController _scrollController = new();
    private readonly WinFormsDisplayListRenderer _displayListRenderer = new();
    private readonly System.Windows.Forms.Timer _frameTimer;
    private SpreadsheetSession? _session;
    private SpreadsheetViewportEngine? _viewport;
    private Worksheet? _subscribedWorksheet;
    private DateTime _lastFrameUtc = DateTime.UtcNow;

    public NeraSpreadsheetControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        BackColor = Color.White;
        TabStop = true;
        _frameTimer = new System.Windows.Forms.Timer { Interval = 8 };
        _frameTimer.Tick += OnFrameTick;
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SpreadsheetSession? Session
    {
        get => _session;
        set => SetSession(value);
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Workbook? Workbook
    {
        get => _session?.Workbook;
        set => SetSession(value is null ? null : new SpreadsheetSession(value));
    }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SpreadsheetRenderTheme RenderTheme { get; set; } = new();

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ContentWidth { get; private set; }

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double ContentHeight { get; private set; }

    [DefaultValue(96d)]
    public double WheelPixelsPerNotch { get; set; } = 96d;

    [DefaultValue(128d)]
    public double OverscanPixels { get; set; } = 128d;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ScrollSnapshot ScrollSnapshot => _scrollController.Snapshot;

    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        var notches = e.Delta / 120d;
        var delta = -notches * WheelPixelsPerNotch;
        if ((ModifierKeys & Keys.Shift) != 0)
        {
            _scrollController.QueueDelta(new ScrollDelta(delta, 0d, ScrollInputKind.Wheel));
        }
        else
        {
            _scrollController.QueueDelta(new ScrollDelta(0d, delta, ScrollInputKind.Wheel));
        }
        StartFrameLoop();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || _session is null)
        {
            return;
        }
        Focus();
        var scroll = _scrollController.Snapshot;
        if (!EnsureViewport().TryHitTest(e.X, e.Y, scroll.X, scroll.Y, out var address))
        {
            return;
        }
        if ((ModifierKeys & Keys.Shift) != 0)
        {
            _session.Selection.ExtendTo(address);
        }
        else if ((ModifierKeys & Keys.Control) != 0)
        {
            _session.Selection.AddRange(new CellRange(address, address));
        }
        else
        {
            _session.Selection.SetActiveCell(address);
        }
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down
            || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_session is null)
        {
            return;
        }
        if (e.Control && e.KeyCode == Keys.Z)
        {
            e.Handled = _session.Undo();
            e.SuppressKeyPress = e.Handled;
            return;
        }
        if (e.Control && e.KeyCode == Keys.Y)
        {
            e.Handled = _session.Redo();
            e.SuppressKeyPress = e.Handled;
            return;
        }
        if (e.KeyCode == Keys.Delete)
        {
            e.Handled = _session.ClearSelection();
            e.SuppressKeyPress = e.Handled;
            return;
        }

        var delta = e.KeyCode switch
        {
            Keys.Left => (Row: 0, Column: -1),
            Keys.Right => (Row: 0, Column: 1),
            Keys.Up => (Row: -1, Column: 0),
            Keys.Down => (Row: 1, Column: 0),
            _ => (Row: 0, Column: 0),
        };
        if (delta == default)
        {
            return;
        }
        MoveActiveCell(delta.Row, delta.Column, e.Shift);
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (_session is not null && ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            EnsureWorksheetSubscription();
            var scroll = _scrollController.Snapshot;
            var frame = EnsureViewport().Compose(scroll.X, scroll.Y, ClientSize.Width, ClientSize.Height, OverscanPixels, RenderTheme);
            ContentWidth = frame.Layout.ContentWidth;
            ContentHeight = frame.Layout.ContentHeight;
            _displayListRenderer.Render(e.Graphics, frame.DisplayList);
        }
        base.OnPaint(e);
    }

    public void QueuePrecisionScroll(double deltaX, double deltaY)
    {
        _scrollController.QueueDelta(new ScrollDelta(deltaX, deltaY, ScrollInputKind.Precision));
        StartFrameLoop();
    }

    public void ScrollTo(double offsetX, double offsetY, bool animated = false)
    {
        _scrollController.ScrollTo(offsetX, offsetY, animated);
        Invalidate();
        if (animated)
        {
            StartFrameLoop();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DetachSessionEvents();
            _frameTimer.Stop();
            _frameTimer.Tick -= OnFrameTick;
            _frameTimer.Dispose();
            _displayListRenderer.Dispose();
        }
        base.Dispose(disposing);
    }

    private SpreadsheetViewportEngine EnsureViewport() => _viewport ??= new SpreadsheetViewportEngine(
        _session ?? throw new InvalidOperationException("A spreadsheet session is required."));

    private void SetSession(SpreadsheetSession? value)
    {
        if (ReferenceEquals(_session, value)) return;
        DetachSessionEvents();
        _session = value;
        _viewport = value is null ? null : new SpreadsheetViewportEngine(value);
        _scrollController.Reset();
        AttachSessionEvents();
        UpdateContentExtent();
        Invalidate();
    }

    private void AttachSessionEvents()
    {
        if (_session is null) return;
        _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
        _session.Selection.Changed += OnSelectionChanged;
        EnsureWorksheetSubscription();
    }

    private void DetachSessionEvents()
    {
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.Selection.Changed -= OnSelectionChanged;
        }
        DetachWorksheetSubscription();
    }

    private void EnsureWorksheetSubscription()
    {
        var worksheet = _session?.ActiveWorksheet;
        if (ReferenceEquals(_subscribedWorksheet, worksheet)) return;
        DetachWorksheetSubscription();
        _subscribedWorksheet = worksheet;
        if (worksheet is not null)
        {
            worksheet.CellsChanged += OnCellsChanged;
            worksheet.Dimensions.Changed += OnDimensionsChanged;
        }
    }

    private void DetachWorksheetSubscription()
    {
        if (_subscribedWorksheet is null) return;
        _subscribedWorksheet.CellsChanged -= OnCellsChanged;
        _subscribedWorksheet.Dimensions.Changed -= OnDimensionsChanged;
        _subscribedWorksheet = null;
    }

    private void OnActiveWorksheetChanged(object? sender, EventArgs e)
    {
        EnsureWorksheetSubscription();
        _viewport?.InvalidateMetrics();
        _scrollController.Reset();
        UpdateContentExtent();
        Invalidate();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e) => Invalidate();
    private void OnCellsChanged(object? sender, CellsChangedEventArgs e) => Invalidate();

    private void OnDimensionsChanged(object? sender, DimensionChangedEventArgs e)
    {
        _viewport?.InvalidateMetrics();
        UpdateContentExtent();
        Invalidate();
    }

    private void UpdateContentExtent()
    {
        if (_viewport is null)
        {
            ContentWidth = 0d;
            ContentHeight = 0d;
            return;
        }
        var extent = _viewport.GetContentExtent();
        ContentWidth = extent.Width;
        ContentHeight = extent.Height;
    }

    private void MoveActiveCell(int rowDelta, int columnDelta, bool extend)
    {
        if (_session is null) return;
        var active = _session.Selection.ActiveCell;
        var row = Math.Clamp(active.RowIndex + rowDelta, 0, SpreadsheetLimits.MaxRows - 1);
        var column = Math.Clamp(active.ColumnIndex + columnDelta, 0, SpreadsheetLimits.MaxColumns - 1);
        var next = new CellAddress(row, column);
        if (extend) _session.Selection.ExtendTo(next); else _session.Selection.SetActiveCell(next);
    }

    private void StartFrameLoop()
    {
        _lastFrameUtc = DateTime.UtcNow;
        _frameTimer.Start();
    }

    private void OnFrameTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsed = now - _lastFrameUtc;
        _lastFrameUtc = now;
        var bounds = new ScrollBounds(
            Math.Max(0d, ContentWidth - ClientSize.Width),
            Math.Max(0d, ContentHeight - ClientSize.Height));
        var result = _scrollController.AdvanceFrame(elapsed, bounds);
        if (result.Changed)
        {
            ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(result.Snapshot));
            Invalidate();
        }
        if (!_scrollController.HasPendingMotion) _frameTimer.Stop();
    }
}
