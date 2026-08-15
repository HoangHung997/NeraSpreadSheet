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
    private readonly TextBox _editor;
    private SpreadsheetSession? _session;
    private SpreadsheetViewportEngine? _viewport;
    private SpreadsheetCellEditorController? _cellEditor;
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
        _editor = new TextBox { Visible = false, BorderStyle = BorderStyle.FixedSingle };
        _editor.KeyDown += OnEditorKeyDown;
        Controls.Add(_editor);
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

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ScrollSnapshot ScrollSnapshot => _scrollController.Snapshot;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsEditing => _cellEditor?.IsEditing == true;

    [DefaultValue(96d)]
    public double WheelPixelsPerNotch { get; set; } = 96d;

    [DefaultValue(128d)]
    public double OverscanPixels { get; set; } = 128d;

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
        if (IsEditing)
        {
            CommitEditor();
        }
        Focus();
        var scroll = _scrollController.Snapshot;
        if (!EnsureViewport().TryHitTest(e.X, e.Y, scroll.OffsetX, scroll.OffsetY, out var address))
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
        if (e.Clicks >= 2)
        {
            BeginEdit();
        }
    }

    protected override bool IsInputKey(Keys keyData)
    {
        return (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Tab
            || base.IsInputKey(keyData);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_session is null || IsEditing)
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
        if (e.KeyCode == Keys.F2)
        {
            BeginEdit();
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode == Keys.Enter)
        {
            MoveActiveCell(1, 0, false);
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode == Keys.Tab)
        {
            MoveActiveCell(0, e.Shift ? -1 : 1, false);
            e.Handled = true;
            e.SuppressKeyPress = true;
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

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (_session is null || IsEditing || char.IsControl(e.KeyChar))
        {
            return;
        }
        BeginEdit(e.KeyChar.ToString());
        e.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        if (_session is not null && ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            EnsureWorksheetSubscription();
            var scroll = _scrollController.Snapshot;
            var frame = EnsureViewport().Compose(scroll.OffsetX, scroll.OffsetY, ClientSize.Width, ClientSize.Height, OverscanPixels, RenderTheme);
            ContentWidth = frame.Layout.ContentWidth;
            ContentHeight = frame.Layout.ContentHeight;
            _displayListRenderer.Render(e.Graphics, frame.DisplayList);
        }
        base.OnPaint(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateEditorBounds();
    }

    public void BeginEdit(string? replacementText = null)
    {
        if (_cellEditor is null)
        {
            return;
        }
        var state = _cellEditor.BeginEdit();
        _editor.Text = replacementText ?? state.InitialText;
        _editor.Visible = true;
        UpdateEditorBounds();
        _editor.Focus();
        if (replacementText is null)
        {
            _editor.SelectAll();
        }
        else
        {
            _editor.SelectionStart = _editor.TextLength;
            _editor.SelectionLength = 0;
        }
    }

    public bool CommitEditor()
    {
        if (_cellEditor is null || !_cellEditor.Commit(_editor.Text))
        {
            return false;
        }
        HideEditor();
        Focus();
        return true;
    }

    public bool CancelEditor()
    {
        if (_cellEditor is null || !_cellEditor.Cancel())
        {
            return false;
        }
        HideEditor();
        Focus();
        return true;
    }

    public void QueuePrecisionScroll(double deltaX, double deltaY)
    {
        _scrollController.QueueDelta(new ScrollDelta(deltaX, deltaY, ScrollInputKind.Precision));
        StartFrameLoop();
    }

    public void ScrollTo(double offsetX, double offsetY, bool animated = false)
    {
        _scrollController.ScrollTo(offsetX, offsetY, animated);
        UpdateEditorBounds();
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
            _editor.KeyDown -= OnEditorKeyDown;
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
        _cellEditor = value is null ? null : new SpreadsheetCellEditorController(value);
        _scrollController.Reset();
        HideEditor();
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
        CancelEditor();
        EnsureWorksheetSubscription();
        _viewport?.InvalidateMetrics();
        _scrollController.Reset();
        UpdateContentExtent();
        Invalidate();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateEditorBounds();
        Invalidate();
    }

    private void OnCellsChanged(object? sender, CellsChangedEventArgs e) => Invalidate();

    private void OnDimensionsChanged(object? sender, DimensionChangedEventArgs e)
    {
        _viewport?.InvalidateMetrics();
        UpdateContentExtent();
        UpdateEditorBounds();
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

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            if (CommitEditor()) MoveActiveCell(1, 0, false);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            CancelEditor();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
        else if (e.KeyCode == Keys.Tab)
        {
            if (CommitEditor()) MoveActiveCell(0, e.Shift ? -1 : 1, false);
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void UpdateEditorBounds()
    {
        if (_cellEditor?.State is not { } state || _viewport is null)
        {
            return;
        }
        var scroll = _scrollController.Snapshot;
        if (!_viewport.TryGetCellBounds(state.Address, scroll.OffsetX, scroll.OffsetY, out var bounds))
        {
            _editor.Visible = false;
            return;
        }
        var rectangle = Rectangle.FromLTRB(
            (int)Math.Floor(bounds.Left),
            (int)Math.Floor(bounds.Top),
            (int)Math.Ceiling(bounds.Right),
            (int)Math.Ceiling(bounds.Bottom));
        _editor.Bounds = rectangle;
        _editor.Visible = rectangle.IntersectsWith(ClientRectangle);
        if (_editor.Visible) _editor.BringToFront();
    }

    private void HideEditor()
    {
        _editor.Visible = false;
        _editor.Text = string.Empty;
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
            UpdateEditorBounds();
            Invalidate();
        }
        if (!_scrollController.HasPendingMotion) _frameTimer.Stop();
    }
}
