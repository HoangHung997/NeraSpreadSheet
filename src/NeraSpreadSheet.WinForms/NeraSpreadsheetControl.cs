using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Direct2D;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

public enum WinFormsRenderingBackend
{
    GdiPlus,
    Direct2D,
    Direct2DSwapChain,
}

public sealed class ScrollChangedEventArgs : EventArgs
{
    public ScrollChangedEventArgs(ScrollSnapshot snapshot) { Snapshot = snapshot; }
    public ScrollSnapshot Snapshot { get; }
}

public sealed class NeraSpreadsheetControl : Control
{
    private const double DirtyRegionPadding = 3d;
    private readonly ContinuousScrollController _scrollController = new();
    private readonly WinFormsDisplayListRenderer _displayListRenderer = new();
    private readonly System.Windows.Forms.Timer _frameTimer;
    private readonly TextBox _editor;
    private SpreadsheetSession? _session;
    private SpreadsheetViewportEngine? _viewport;
    private SpreadsheetCellEditorController? _cellEditor;
    private Worksheet? _subscribedWorksheet;
    private Direct2DHwndDisplayListRenderer? _direct2DRenderer;
    private Direct2DSwapChainDisplayListRenderer? _swapChainRenderer;
    private DateTime _lastFrameUtc = DateTime.UtcNow;
    private WinFormsRenderingBackend _renderingBackend;
    private bool _swapChainVSync = true;

    public NeraSpreadsheetControl()
    {
        SetGdiPaintingStyles(enabled: true);
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
    public SpreadsheetRenderTheme RenderTheme { get; set; } = new() { ShowHeaders = true };

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

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Direct2DRendererDiagnostics? Direct2DDiagnostics => _direct2DRenderer?.Diagnostics;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Direct2DSwapChainRendererDiagnostics? SwapChainDiagnostics => _swapChainRenderer?.Diagnostics;

    [DefaultValue(WinFormsRenderingBackend.GdiPlus)]
    public WinFormsRenderingBackend RenderingBackend
    {
        get => _renderingBackend;
        set
        {
            if (_renderingBackend == value)
            {
                return;
            }

            _renderingBackend = value;
            DisposeGpuRenderers();
            SetGdiPaintingStyles(value == WinFormsRenderingBackend.GdiPlus);
            if (IsHandleCreated)
            {
                EnsureSelectedGpuRenderer();
            }
            Invalidate();
        }
    }

    [DefaultValue(true)]
    public bool SwapChainVSync
    {
        get => _swapChainVSync;
        set
        {
            _swapChainVSync = value;
            if (_swapChainRenderer is not null)
            {
                _swapChainRenderer.VSync = value;
            }
        }
    }

    [DefaultValue(96d)]
    public double WheelPixelsPerNotch { get; set; } = 96d;

    [DefaultValue(128d)]
    public double OverscanPixels { get; set; } = 128d;

    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        EnsureSelectedGpuRenderer();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DisposeGpuRenderers();
        base.OnHandleDestroyed(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        if (_direct2DRenderer is null && _swapChainRenderer is null)
        {
            base.OnPaintBackground(pevent);
        }
    }

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

        var hit = SpreadsheetChromeGeometry.HitTest(
            e.X,
            e.Y,
            ClientSize.Width,
            ClientSize.Height,
            RenderTheme);
        var scroll = _scrollController.Snapshot;
        switch (hit.Region)
        {
            case SpreadsheetChromeRegion.Corner:
                _session.Selection.SelectAll();
                return;
            case SpreadsheetChromeRegion.RowHeader:
                if (EnsureViewport().TryHitTestRow(hit.BodyY, scroll.OffsetY, out var rowIndex))
                {
                    if ((ModifierKeys & Keys.Shift) != 0)
                    {
                        _session.Selection.ExtendRowsTo(rowIndex);
                    }
                    else
                    {
                        _session.Selection.SelectRow(rowIndex, additive: (ModifierKeys & Keys.Control) != 0);
                    }
                }
                return;
            case SpreadsheetChromeRegion.ColumnHeader:
                if (EnsureViewport().TryHitTestColumn(hit.BodyX, scroll.OffsetX, out var columnIndex))
                {
                    if ((ModifierKeys & Keys.Shift) != 0)
                    {
                        _session.Selection.ExtendColumnsTo(columnIndex);
                    }
                    else
                    {
                        _session.Selection.SelectColumn(columnIndex, additive: (ModifierKeys & Keys.Control) != 0);
                    }
                }
                return;
            case SpreadsheetChromeRegion.Body:
                break;
            default:
                return;
        }

        if (!EnsureViewport().TryHitTest(hit.BodyX, hit.BodyY, scroll.OffsetX, scroll.OffsetY, out var address))
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

        if (e.Control)
        {
            switch (e.KeyCode)
            {
                case Keys.Z:
                    e.Handled = _session.Undo();
                    break;
                case Keys.Y:
                    e.Handled = _session.Redo();
                    break;
                case Keys.C:
                    _session.Clipboard.CopyPrimarySelection();
                    e.Handled = true;
                    break;
                case Keys.X:
                    e.Handled = _session.Clipboard.CutPrimarySelection();
                    break;
                case Keys.V:
                    e.Handled = _session.Clipboard.PasteAtActiveCell();
                    break;
                case Keys.B:
                    _session.Styles.ToggleBold();
                    e.Handled = true;
                    break;
                case Keys.I:
                    _session.Styles.ToggleItalic();
                    e.Handled = true;
                    break;
            }

            if (e.Handled)
            {
                e.SuppressKeyPress = true;
                return;
            }
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
        if (_session is not null && ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            EnsureWorksheetSubscription();
            var chrome = GetChromeMetrics();
            if (chrome.BodyWidth <= 0d || chrome.BodyHeight <= 0d)
            {
                RenderEmptyBackground(e.Graphics);
                base.OnPaint(e);
                return;
            }

            var scroll = _scrollController.Snapshot;
            var frame = EnsureViewport().Compose(
                scroll.OffsetX,
                scroll.OffsetY,
                chrome.BodyWidth,
                chrome.BodyHeight,
                OverscanPixels,
                RenderTheme);
            ContentWidth = frame.Layout.ContentWidth;
            ContentHeight = frame.Layout.ContentHeight;
            var displayList = SpreadsheetChromeDisplayListComposer.Compose(
                frame.DisplayList,
                frame.Layout,
                _session.Selection.Capture(),
                RenderTheme);

            switch (_renderingBackend)
            {
                case WinFormsRenderingBackend.Direct2D:
                {
                    var clipped = e.ClipRectangle == ClientRectangle
                        ? displayList
                        : CreateDirtyClippedDisplayList(displayList, e.ClipRectangle);
                    EnsureDirect2DRenderer().Render(clipped);
                    break;
                }
                case WinFormsRenderingBackend.Direct2DSwapChain:
                    EnsureSwapChainRenderer().Render(displayList);
                    break;
                default:
                    _displayListRenderer.Render(e.Graphics, displayList);
                    break;
            }
        }
        else
        {
            RenderEmptyBackground(e.Graphics);
        }

        base.OnPaint(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            _direct2DRenderer?.Resize(ClientSize.Width, ClientSize.Height);
            _swapChainRenderer?.Resize(ClientSize.Width, ClientSize.Height);
            if (_direct2DRenderer is not null || _swapChainRenderer is not null)
            {
                Invalidate();
            }
        }
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
            DisposeGpuRenderers();
            _displayListRenderer.Dispose();
        }
        base.Dispose(disposing);
    }

    private SpreadsheetChromeMetrics GetChromeMetrics() =>
        SpreadsheetChromeGeometry.Calculate(ClientSize.Width, ClientSize.Height, RenderTheme);

    private void EnsureSelectedGpuRenderer()
    {
        switch (_renderingBackend)
        {
            case WinFormsRenderingBackend.Direct2D:
                EnsureDirect2DRenderer();
                break;
            case WinFormsRenderingBackend.Direct2DSwapChain:
                EnsureSwapChainRenderer();
                break;
        }
    }

    private Direct2DHwndDisplayListRenderer EnsureDirect2DRenderer()
    {
        EnsureGpuPlatformAndHandle();
        if (_renderingBackend != WinFormsRenderingBackend.Direct2D)
        {
            throw new InvalidOperationException("The HWND Direct2D backend is not selected for this control.");
        }

        var width = Math.Max(1, ClientSize.Width);
        var height = Math.Max(1, ClientSize.Height);
        _direct2DRenderer ??= new Direct2DHwndDisplayListRenderer(Handle, width, height);
        return _direct2DRenderer;
    }

    private Direct2DSwapChainDisplayListRenderer EnsureSwapChainRenderer()
    {
        EnsureGpuPlatformAndHandle();
        if (_renderingBackend != WinFormsRenderingBackend.Direct2DSwapChain)
        {
            throw new InvalidOperationException("The D3D11/DXGI swap-chain backend is not selected for this control.");
        }

        var width = Math.Max(1, ClientSize.Width);
        var height = Math.Max(1, ClientSize.Height);
        if (_swapChainRenderer is null)
        {
            _swapChainRenderer = new Direct2DSwapChainDisplayListRenderer(Handle, width, height)
            {
                VSync = _swapChainVSync,
            };
        }
        return _swapChainRenderer;
    }

    private void EnsureGpuPlatformAndHandle()
    {
        if (!Direct2DBackendDescriptor.IsPlatformSupported)
        {
            throw new PlatformNotSupportedException("The Direct2D backends require Windows 10 version 2004 or later.");
        }
        if (!IsHandleCreated)
        {
            throw new InvalidOperationException("The control handle must be created before a GPU renderer can be initialized.");
        }
    }

    private void DisposeGpuRenderers()
    {
        _direct2DRenderer?.Dispose();
        _direct2DRenderer = null;
        _swapChainRenderer?.Dispose();
        _swapChainRenderer = null;
    }

    private void RenderEmptyBackground(Graphics graphics)
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        if (_renderingBackend == WinFormsRenderingBackend.GdiPlus)
        {
            graphics.Clear(BackColor);
            return;
        }

        var builder = new DisplayListBuilder();
        builder.FillRectangle(
            new RectD(0d, 0d, ClientSize.Width, ClientSize.Height),
            new ColorRgba(BackColor.R, BackColor.G, BackColor.B, BackColor.A));
        var displayList = builder.Build();
        if (_renderingBackend == WinFormsRenderingBackend.Direct2D)
        {
            EnsureDirect2DRenderer().Render(displayList);
        }
        else
        {
            EnsureSwapChainRenderer().Render(displayList);
        }
    }

    private void SetGdiPaintingStyles(bool enabled)
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, enabled);
        if (IsHandleCreated)
        {
            UpdateStyles();
        }
    }

    private SpreadsheetViewportEngine EnsureViewport() => _viewport ??= new SpreadsheetViewportEngine(
        _session ?? throw new InvalidOperationException("A spreadsheet session is required."));

    private void SetSession(SpreadsheetSession? value)
    {
        if (ReferenceEquals(_session, value))
        {
            return;
        }
        DetachSessionEvents();
        _session = value;
        _viewport = value is null ? null : new SpreadsheetViewportEngine(value);
        _cellEditor = value?.Editor;
        _scrollController.Reset();
        HideEditor();
        AttachSessionEvents();
        UpdateContentExtent();
        Invalidate();
    }

    private void AttachSessionEvents()
    {
        if (_session is null)
        {
            return;
        }
        _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
        _session.Selection.Changed += OnSelectionChanged;
        _session.View.Changed += OnViewChanged;
        EnsureWorksheetSubscription();
    }

    private void DetachSessionEvents()
    {
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.Selection.Changed -= OnSelectionChanged;
            _session.View.Changed -= OnViewChanged;
        }
        DetachWorksheetSubscription();
    }

    private void EnsureWorksheetSubscription()
    {
        var worksheet = _session?.ActiveWorksheet;
        if (ReferenceEquals(_subscribedWorksheet, worksheet))
        {
            return;
        }
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
        if (_subscribedWorksheet is null)
        {
            return;
        }
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

    private void OnViewChanged(object? sender, SpreadsheetViewChangedEventArgs e)
    {
        if (_session is null || !ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            return;
        }

        _viewport?.ClearDisplayListCache();
        UpdateEditorBounds();
        Invalidate();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateEditorBounds();
        Invalidate();
    }

    private void OnCellsChanged(object? sender, CellsChangedEventArgs e)
    {
        if (_renderingBackend == WinFormsRenderingBackend.Direct2DSwapChain)
        {
            Invalidate();
            return;
        }
        InvalidateCellRange(e.Range);
    }

    private void OnDimensionsChanged(object? sender, DimensionChangedEventArgs e)
    {
        _viewport?.InvalidateMetrics();
        UpdateContentExtent();
        UpdateEditorBounds();
        Invalidate();
    }

    private void InvalidateCellRange(CellRange range)
    {
        if (_viewport is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            Invalidate();
            return;
        }

        var scroll = _scrollController.Snapshot;
        if (!_viewport.TryGetRangeBounds(range, scroll.OffsetX, scroll.OffsetY, out var bounds))
        {
            Invalidate();
            return;
        }

        var chrome = GetChromeMetrics();
        var left = Math.Max(0d, chrome.RowHeaderWidth + bounds.Left - DirtyRegionPadding);
        var top = Math.Max(0d, chrome.ColumnHeaderHeight + bounds.Top - DirtyRegionPadding);
        var right = Math.Min(ClientSize.Width, chrome.RowHeaderWidth + bounds.Right + DirtyRegionPadding);
        var bottom = Math.Min(ClientSize.Height, chrome.ColumnHeaderHeight + bounds.Bottom + DirtyRegionPadding);
        if (right <= left || bottom <= top)
        {
            return;
        }

        Invalidate(Rectangle.FromLTRB(
            (int)Math.Floor(left),
            (int)Math.Floor(top),
            (int)Math.Ceiling(right),
            (int)Math.Ceiling(bottom)));
    }

    private static DisplayList CreateDirtyClippedDisplayList(DisplayList displayList, Rectangle clipRectangle)
    {
        var builder = new DisplayListBuilder();
        builder.PushClip(new RectD(
            clipRectangle.X,
            clipRectangle.Y,
            clipRectangle.Width,
            clipRectangle.Height));
        builder.Append(displayList);
        builder.PopClip();
        return builder.Build();
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
        if (_session is null)
        {
            return;
        }
        var active = _session.Selection.ActiveCell;
        var row = Math.Clamp(active.RowIndex + rowDelta, 0, SpreadsheetLimits.MaxRows - 1);
        var column = Math.Clamp(active.ColumnIndex + columnDelta, 0, SpreadsheetLimits.MaxColumns - 1);
        var next = new CellAddress(row, column);
        if (extend)
        {
            _session.Selection.ExtendTo(next);
        }
        else
        {
            _session.Selection.SetActiveCell(next);
        }
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            if (CommitEditor())
            {
                MoveActiveCell(1, 0, false);
            }
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
            if (CommitEditor())
            {
                MoveActiveCell(0, e.Shift ? -1 : 1, false);
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }
    }

    private void UpdateEditorBounds()
    {
        if (_cellEditor?.State is not { } state || _viewport is null || _session is null)
        {
            return;
        }
        var scroll = _scrollController.Snapshot;
        if (!_viewport.TryGetCellBounds(state.Address, scroll.OffsetX, scroll.OffsetY, out var bounds))
        {
            _editor.Visible = false;
            return;
        }

        var chrome = GetChromeMetrics();
        var raw = Rectangle.FromLTRB(
            (int)Math.Floor(chrome.RowHeaderWidth + bounds.Left),
            (int)Math.Floor(chrome.ColumnHeaderHeight + bounds.Top),
            (int)Math.Ceiling(chrome.RowHeaderWidth + bounds.Right),
            (int)Math.Ceiling(chrome.ColumnHeaderHeight + bounds.Bottom));
        var frozen = _viewport.GetFrozenPaneExtent();
        var frozenWidth = Math.Clamp((int)Math.Ceiling(frozen.Width), 0, (int)Math.Ceiling(chrome.BodyWidth));
        var frozenHeight = Math.Clamp((int)Math.Ceiling(frozen.Height), 0, (int)Math.Ceiling(chrome.BodyHeight));
        var originX = (int)Math.Ceiling(chrome.RowHeaderWidth);
        var originY = (int)Math.Ceiling(chrome.ColumnHeaderHeight);
        var frozenColumn = state.Address.ColumnIndex < _session.View.FrozenColumns;
        var frozenRow = state.Address.RowIndex < _session.View.FrozenRows;
        var pane = Rectangle.FromLTRB(
            originX + (frozenColumn ? 0 : frozenWidth),
            originY + (frozenRow ? 0 : frozenHeight),
            originX + (frozenColumn ? frozenWidth : (int)Math.Ceiling(chrome.BodyWidth)),
            originY + (frozenRow ? frozenHeight : (int)Math.Ceiling(chrome.BodyHeight)));
        var visible = Rectangle.Intersect(Rectangle.Intersect(raw, pane), ClientRectangle);
        if (visible.Width <= 0 || visible.Height <= 0)
        {
            _editor.Visible = false;
            return;
        }

        _editor.Bounds = visible;
        _editor.Visible = true;
        _editor.BringToFront();
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
        var chrome = GetChromeMetrics();
        var bounds = new ScrollBounds(
            Math.Max(0d, ContentWidth - chrome.BodyWidth),
            Math.Max(0d, ContentHeight - chrome.BodyHeight));
        var result = _scrollController.AdvanceFrame(elapsed, bounds);
        if (result.Changed)
        {
            ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(result.Snapshot));
            UpdateEditorBounds();
            Invalidate();
        }
        if (!_scrollController.HasPendingMotion)
        {
            _frameTimer.Stop();
        }
    }
}
