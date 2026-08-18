using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Direct2D;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

public sealed class SplitPaneScrollChangedEventArgs : EventArgs
{
    public SplitPaneScrollChangedEventArgs(SpreadsheetPaneId paneId, ScrollSnapshot snapshot)
    {
        PaneId = paneId;
        Snapshot = snapshot;
    }

    public SpreadsheetPaneId PaneId { get; }

    public ScrollSnapshot Snapshot { get; }
}

/// <summary>
/// Public WinForms spreadsheet host whose single-pane and split-pane modes share
/// the same platform-neutral viewport, display-list and continuous-scroll engines.
/// </summary>
public sealed class NeraSplitSpreadsheetControl : Control
{
    private const double GeometryEpsilon = 1e-9;
    private readonly WinFormsDisplayListRenderer _displayListRenderer = new();
    private readonly System.Windows.Forms.Timer _frameTimer;
    private readonly TextBox _editor;
    private SpreadsheetSession? _session;
    private SpreadsheetSplitViewportEngine? _viewport;
    private SpreadsheetCellEditorController? _cellEditor;
    private Worksheet? _subscribedWorksheet;
    private Direct2DHwndDisplayListRenderer? _direct2DRenderer;
    private Direct2DSwapChainDisplayListRenderer? _swapChainRenderer;
    private SpreadsheetSplitViewportFrame? _lastFrame;
    private SpreadsheetHeaderResizeHandle? _headerResize;
    private SpreadsheetSplitHitRegionKind _splitDragKind;
    private DateTime _lastFrameUtc = DateTime.UtcNow;
    private WinFormsRenderingBackend _renderingBackend;
    private bool _swapChainVSync = true;
    private double? _splitX;
    private double? _splitY;
    private double _splitSeparatorThickness = 6d;
    private double _minimumSplitPaneExtent = 64d;

    public NeraSplitSpreadsheetControl()
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
    public bool HasSplitPanes => _splitX is not null || _splitY is not null;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double? SplitX => _splitX;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public double? SplitY => _splitY;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SpreadsheetPaneId ActivePane => _viewport?.ActivePane ?? SpreadsheetPaneId.TopLeft;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public SpreadsheetSplitLayout? LastSplitLayout => _lastFrame?.Layout;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ScrollSnapshot ScrollSnapshot => _viewport?.GetPaneScrollSnapshot(ActivePane) ?? default;

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

    [DefaultValue(6d)]
    public double SplitSeparatorThickness
    {
        get => _splitSeparatorThickness;
        set
        {
            if (!double.IsFinite(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Split separator thickness must be finite and positive.");
            }
            if (Math.Abs(_splitSeparatorThickness - value) <= GeometryEpsilon)
            {
                return;
            }
            _splitSeparatorThickness = value;
            InvalidateSplitGeometry();
        }
    }

    [DefaultValue(64d)]
    public double MinimumSplitPaneExtent
    {
        get => _minimumSplitPaneExtent;
        set
        {
            if (!double.IsFinite(value) || value <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Minimum split-pane extent must be finite and positive.");
            }
            if (Math.Abs(_minimumSplitPaneExtent - value) <= GeometryEpsilon)
            {
                return;
            }
            _minimumSplitPaneExtent = value;
            InvalidateSplitGeometry();
        }
    }

    public event EventHandler<ScrollChangedEventArgs>? ScrollChanged;

    public event EventHandler<SplitPaneScrollChangedEventArgs>? PaneScrollChanged;

    public event EventHandler? SplitChanged;

    public event EventHandler? ActivePaneChanged;

    public void SetSplit(double? splitX, double? splitY)
    {
        ValidateOptionalSplit(splitX, nameof(splitX));
        ValidateOptionalSplit(splitY, nameof(splitY));
        if (_splitX == splitX && _splitY == splitY)
        {
            return;
        }

        _splitX = splitX;
        _splitY = splitY;
        InvalidateSplitGeometry();
        SplitChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SplitVertically(double splitX) => SetSplit(splitX, _splitY);

    public void SplitHorizontally(double splitY) => SetSplit(_splitX, splitY);

    public void ClearSplit() => SetSplit(null, null);

    public bool ActivatePane(SpreadsheetPaneId paneId)
    {
        if (_viewport is null || !_viewport.SetActivePaneAndReport(paneId))
        {
            return false;
        }

        UpdateEditorBounds();
        Invalidate();
        ActivePaneChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public ScrollSnapshot GetPaneScrollSnapshot(SpreadsheetPaneId paneId) =>
        EnsureViewport().GetPaneScrollSnapshot(paneId);

    public void ScrollPaneTo(
        SpreadsheetPaneId paneId,
        double offsetX,
        double offsetY,
        bool animated = false)
    {
        EnsureViewport().ScrollPaneTo(paneId, offsetX, offsetY, animated);
        if (paneId == ActivePane)
        {
            UpdateEditorBounds();
        }
        Invalidate();
        if (animated)
        {
            StartFrameLoop();
        }
    }

    public void QueuePanePrecisionScroll(SpreadsheetPaneId paneId, double deltaX, double deltaY)
    {
        EnsureViewport().QueuePaneScroll(
            paneId,
            new ScrollDelta(deltaX, deltaY, ScrollInputKind.Precision));
        Invalidate();
        StartFrameLoop();
    }

    public void QueuePrecisionScroll(double deltaX, double deltaY) =>
        QueuePanePrecisionScroll(ActivePane, deltaX, deltaY);

    public void ScrollTo(double offsetX, double offsetY, bool animated = false) =>
        ScrollPaneTo(ActivePane, offsetX, offsetY, animated);

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
        if (_session is null)
        {
            return;
        }

        if (TryResolvePaneAtClientPoint(e.X, e.Y, out var paneId))
        {
            ActivatePane(paneId);
        }

        var notches = e.Delta / 120d;
        var delta = -notches * WheelPixelsPerNotch;
        var scrollDelta = (ModifierKeys & Keys.Shift) != 0
            ? new ScrollDelta(delta, 0d, ScrollInputKind.Wheel)
            : new ScrollDelta(0d, delta, ScrollInputKind.Wheel);
        EnsureViewport().QueuePaneScroll(ActivePane, scrollDelta);
        Invalidate();
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

        if (TryBeginHeaderResize(e.X, e.Y))
        {
            return;
        }

        var chromeHit = SpreadsheetChromeGeometry.HitTest(
            e.X,
            e.Y,
            ClientSize.Width,
            ClientSize.Height,
            RenderTheme);
        switch (chromeHit.Region)
        {
            case SpreadsheetChromeRegion.Corner:
                _session.Selection.SelectAll();
                return;
            case SpreadsheetChromeRegion.RowHeader:
                SelectRowFromHeader(chromeHit.BodyY);
                return;
            case SpreadsheetChromeRegion.ColumnHeader:
                SelectColumnFromHeader(chromeHit.BodyX);
                return;
            case SpreadsheetChromeRegion.Body:
                break;
            default:
                return;
        }

        if (TryBeginSplitDrag(chromeHit.BodyX, chromeHit.BodyY))
        {
            return;
        }

        var viewport = EnsureViewport();
        var oldPane = viewport.ActivePane;
        viewport.TryActivatePaneAt(chromeHit.BodyX, chromeHit.BodyY);
        if (viewport.ActivePane != oldPane)
        {
            ActivePaneChanged?.Invoke(this, EventArgs.Empty);
        }
        if (!viewport.TryHitTest(
            chromeHit.BodyX,
            chromeHit.BodyY,
            out _,
            out var address))
        {
            Invalidate();
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_splitDragKind != SpreadsheetSplitHitRegionKind.None)
        {
            UpdateSplitDrag(e.X, e.Y);
            Cursor = GetSplitCursor(_splitDragKind);
            return;
        }
        if (_headerResize is { } resize)
        {
            ApplyHeaderResize(resize, e.X, e.Y);
            Cursor = GetResizeCursor(resize.Axis);
            return;
        }
        UpdatePointerCursor(e.X, e.Y);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left)
        {
            return;
        }

        if (_splitDragKind != SpreadsheetSplitHitRegionKind.None)
        {
            UpdateSplitDrag(e.X, e.Y);
            _splitDragKind = SpreadsheetSplitHitRegionKind.None;
            Capture = false;
            NormalizeRequestedSplit();
            UpdatePointerCursor(e.X, e.Y);
            return;
        }

        if (_headerResize is not { } resize)
        {
            return;
        }
        ApplyHeaderResize(resize, e.X, e.Y);
        _headerResize = null;
        Capture = false;
        UpdatePointerCursor(e.X, e.Y);
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (Capture)
        {
            return;
        }
        _headerResize = null;
        _splitDragKind = SpreadsheetSplitHitRegionKind.None;
        Cursor = Cursors.Default;
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
                _lastFrame = null;
                RenderEmptyBackground(e.Graphics);
                base.OnPaint(e);
                return;
            }

            var frame = EnsureViewport().Compose(
                CreateSplitRequest(chrome),
                OverscanPixels,
                RenderTheme);
            _lastFrame = frame;
            var extent = EnsureViewport().GetContentExtent();
            ContentWidth = extent.Width;
            ContentHeight = extent.Height;
            var paneLayouts = frame.Panes
                .Select(static pane => new SpreadsheetSplitPaneChromeLayout(
                    pane.Pane.PaneId,
                    pane.Pane.Bounds,
                    pane.ViewportFrame.Layout))
                .ToArray();
            var displayList = SpreadsheetSplitChromeDisplayListComposer.Compose(
                frame.DisplayList,
                frame.Layout,
                paneLayouts,
                _session.Selection.Capture(),
                RenderTheme);

            RenderDisplayList(e, displayList);
        }
        else
        {
            _lastFrame = null;
            RenderEmptyBackground(e.Graphics);
        }

        base.OnPaint(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _lastFrame = null;
        _viewport?.ClearDisplayListCache();
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

    private SpreadsheetSplitRequest CreateSplitRequest(SpreadsheetChromeMetrics chrome) => new(
        new SizeD(chrome.BodyWidth, chrome.BodyHeight),
        _splitX,
        _splitY,
        _splitSeparatorThickness,
        _minimumSplitPaneExtent);

    private void RenderDisplayList(PaintEventArgs e, DisplayList displayList)
    {
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

    private void SelectRowFromHeader(double bodyY)
    {
        if (_session is null || !TryHitTestRowHeader(bodyY, out var paneId, out var rowIndex))
        {
            return;
        }
        ActivatePane(paneId);
        if ((ModifierKeys & Keys.Shift) != 0)
        {
            _session.Selection.ExtendRowsTo(rowIndex);
        }
        else
        {
            _session.Selection.SelectRow(rowIndex, additive: (ModifierKeys & Keys.Control) != 0);
        }
    }

    private void SelectColumnFromHeader(double bodyX)
    {
        if (_session is null || !TryHitTestColumnHeader(bodyX, out var paneId, out var columnIndex))
        {
            return;
        }
        ActivatePane(paneId);
        if ((ModifierKeys & Keys.Shift) != 0)
        {
            _session.Selection.ExtendColumnsTo(columnIndex);
        }
        else
        {
            _session.Selection.SelectColumn(columnIndex, additive: (ModifierKeys & Keys.Control) != 0);
        }
    }

    private bool TryHitTestRowHeader(
        double bodyY,
        out SpreadsheetPaneId paneId,
        out int rowIndex)
    {
        if (_lastFrame is null)
        {
            paneId = default;
            rowIndex = default;
            return false;
        }

        foreach (var pane in _lastFrame.Panes)
        {
            var bounds = pane.Pane.Bounds;
            if (Math.Abs(bounds.Left) > GeometryEpsilon ||
                bodyY < bounds.Top ||
                bodyY >= bounds.Bottom ||
                bounds.Width <= 0d)
            {
                continue;
            }
            var probeX = bounds.Left + Math.Min(1d, bounds.Width / 2d);
            if (EnsureViewport().TryHitTest(probeX, bodyY, out paneId, out var address))
            {
                rowIndex = address.RowIndex;
                return true;
            }
        }

        paneId = default;
        rowIndex = default;
        return false;
    }

    private bool TryHitTestColumnHeader(
        double bodyX,
        out SpreadsheetPaneId paneId,
        out int columnIndex)
    {
        if (_lastFrame is null)
        {
            paneId = default;
            columnIndex = default;
            return false;
        }

        foreach (var pane in _lastFrame.Panes)
        {
            var bounds = pane.Pane.Bounds;
            if (Math.Abs(bounds.Top) > GeometryEpsilon ||
                bodyX < bounds.Left ||
                bodyX >= bounds.Right ||
                bounds.Height <= 0d)
            {
                continue;
            }
            var probeY = bounds.Top + Math.Min(1d, bounds.Height / 2d);
            if (EnsureViewport().TryHitTest(bodyX, probeY, out paneId, out var address))
            {
                columnIndex = address.ColumnIndex;
                return true;
            }
        }

        paneId = default;
        columnIndex = default;
        return false;
    }

    private bool TryResolvePaneAtClientPoint(double x, double y, out SpreadsheetPaneId paneId)
    {
        paneId = ActivePane;
        if (_lastFrame is null)
        {
            return false;
        }

        var hit = SpreadsheetChromeGeometry.HitTest(
            x,
            y,
            ClientSize.Width,
            ClientSize.Height,
            RenderTheme);
        switch (hit.Region)
        {
            case SpreadsheetChromeRegion.Body:
            {
                var paneHit = _lastFrame.Layout.HitTest(new PointD(hit.BodyX, hit.BodyY));
                if (paneHit is
                    {
                        RegionKind: SpreadsheetSplitHitRegionKind.Pane,
                        PaneId: { } resolvedPane,
                    })
                {
                    paneId = resolvedPane;
                    return true;
                }
                return false;
            }
            case SpreadsheetChromeRegion.RowHeader:
                return TryHitTestRowHeader(hit.BodyY, out paneId, out _);
            case SpreadsheetChromeRegion.ColumnHeader:
                return TryHitTestColumnHeader(hit.BodyX, out paneId, out _);
            default:
                return false;
        }
    }

    private bool TryBeginSplitDrag(double bodyX, double bodyY)
    {
        if (_lastFrame is null)
        {
            return false;
        }

        var hit = _lastFrame.Layout.HitTest(new PointD(bodyX, bodyY));
        if (hit.RegionKind is not (
            SpreadsheetSplitHitRegionKind.VerticalSeparator or
            SpreadsheetSplitHitRegionKind.HorizontalSeparator or
            SpreadsheetSplitHitRegionKind.SeparatorIntersection))
        {
            return false;
        }

        _splitDragKind = hit.RegionKind;
        Capture = true;
        Cursor = GetSplitCursor(hit.RegionKind);
        return true;
    }

    private void UpdateSplitDrag(double clientX, double clientY)
    {
        var chrome = GetChromeMetrics();
        var bodyX = clientX - chrome.RowHeaderWidth;
        var bodyY = clientY - chrome.ColumnHeaderHeight;
        var nextX = _splitX;
        var nextY = _splitY;
        if (_splitDragKind is SpreadsheetSplitHitRegionKind.VerticalSeparator or
            SpreadsheetSplitHitRegionKind.SeparatorIntersection)
        {
            nextX = bodyX - (_splitSeparatorThickness / 2d);
        }
        if (_splitDragKind is SpreadsheetSplitHitRegionKind.HorizontalSeparator or
            SpreadsheetSplitHitRegionKind.SeparatorIntersection)
        {
            nextY = bodyY - (_splitSeparatorThickness / 2d);
        }

        _splitX = nextX;
        _splitY = nextY;
        _lastFrame = null;
        Invalidate();
        SplitChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NormalizeRequestedSplit()
    {
        var chrome = GetChromeMetrics();
        if (chrome.BodyWidth <= 0d || chrome.BodyHeight <= 0d)
        {
            return;
        }
        var layout = SpreadsheetSplitLayoutEngine.Compute(CreateSplitRequest(chrome));
        _splitX = _splitX is null ? null : layout.SplitX;
        _splitY = _splitY is null ? null : layout.SplitY;
        _lastFrame = null;
        Invalidate();
        SplitChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool TryBeginHeaderResize(double x, double y)
    {
        if (!TryFindHeaderResizeHandle(x, y, out var resize))
        {
            return false;
        }

        _headerResize = resize;
        Capture = true;
        Cursor = GetResizeCursor(resize.Axis);
        return true;
    }

    private bool TryFindHeaderResizeHandle(
        double x,
        double y,
        out SpreadsheetHeaderResizeHandle handle)
    {
        if (_lastFrame is null || !RenderTheme.ShowHeaders)
        {
            handle = default;
            return false;
        }

        var chromeHit = SpreadsheetChromeGeometry.HitTest(
            x,
            y,
            ClientSize.Width,
            ClientSize.Height,
            RenderTheme);
        if (chromeHit.Region == SpreadsheetChromeRegion.RowHeader)
        {
            foreach (var pane in _lastFrame.Panes)
            {
                var bounds = pane.Pane.Bounds;
                if (Math.Abs(bounds.Left) > GeometryEpsilon ||
                    chromeHit.BodyY < bounds.Top ||
                    chromeHit.BodyY >= bounds.Bottom)
                {
                    continue;
                }
                var localY = chromeHit.BodyY - bounds.Top;
                if (!SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(
                    x,
                    RenderTheme.ColumnHeaderHeight + localY,
                    RenderTheme.RowHeaderWidth + bounds.Width,
                    RenderTheme.ColumnHeaderHeight + bounds.Height,
                    RenderTheme,
                    pane.ViewportFrame.Layout,
                    out var localHandle))
                {
                    continue;
                }
                handle = localHandle with { EdgeCoordinate = localHandle.EdgeCoordinate + bounds.Top };
                return true;
            }
        }
        else if (chromeHit.Region == SpreadsheetChromeRegion.ColumnHeader)
        {
            foreach (var pane in _lastFrame.Panes)
            {
                var bounds = pane.Pane.Bounds;
                if (Math.Abs(bounds.Top) > GeometryEpsilon ||
                    chromeHit.BodyX < bounds.Left ||
                    chromeHit.BodyX >= bounds.Right)
                {
                    continue;
                }
                var localX = chromeHit.BodyX - bounds.Left;
                if (!SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(
                    RenderTheme.RowHeaderWidth + localX,
                    y,
                    RenderTheme.RowHeaderWidth + bounds.Width,
                    RenderTheme.ColumnHeaderHeight + bounds.Height,
                    RenderTheme,
                    pane.ViewportFrame.Layout,
                    out var localHandle))
                {
                    continue;
                }
                handle = localHandle with { EdgeCoordinate = localHandle.EdgeCoordinate + bounds.Left };
                return true;
            }
        }

        handle = default;
        return false;
    }

    private void ApplyHeaderResize(SpreadsheetHeaderResizeHandle resize, double x, double y)
    {
        if (_session is null)
        {
            return;
        }

        var size = SpreadsheetHeaderResizeGeometry.CalculateSize(resize, x, y);
        if (resize.Axis == WorksheetAxis.Row)
        {
            _session.ActiveWorksheet.Dimensions.SetRowHeight(resize.Index, size);
        }
        else
        {
            _session.ActiveWorksheet.Dimensions.SetColumnWidth(resize.Index, size);
        }
    }

    private void UpdatePointerCursor(double x, double y)
    {
        if (TryFindHeaderResizeHandle(x, y, out var resize))
        {
            Cursor = GetResizeCursor(resize.Axis);
            return;
        }

        var chromeHit = SpreadsheetChromeGeometry.HitTest(
            x,
            y,
            ClientSize.Width,
            ClientSize.Height,
            RenderTheme);
        if (chromeHit.Region == SpreadsheetChromeRegion.Body && _lastFrame is not null)
        {
            var splitHit = _lastFrame.Layout.HitTest(new PointD(chromeHit.BodyX, chromeHit.BodyY));
            if (splitHit.RegionKind is
                SpreadsheetSplitHitRegionKind.VerticalSeparator or
                SpreadsheetSplitHitRegionKind.HorizontalSeparator or
                SpreadsheetSplitHitRegionKind.SeparatorIntersection)
            {
                Cursor = GetSplitCursor(splitHit.RegionKind);
                return;
            }
        }
        Cursor = Cursors.Default;
    }

    private static Cursor GetResizeCursor(WorksheetAxis axis) =>
        axis == WorksheetAxis.Row ? Cursors.SizeNS : Cursors.SizeWE;

    private static Cursor GetSplitCursor(SpreadsheetSplitHitRegionKind kind) => kind switch
    {
        SpreadsheetSplitHitRegionKind.VerticalSeparator => Cursors.VSplit,
        SpreadsheetSplitHitRegionKind.HorizontalSeparator => Cursors.HSplit,
        SpreadsheetSplitHitRegionKind.SeparatorIntersection => Cursors.SizeAll,
        _ => Cursors.Default,
    };

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

    private SpreadsheetSplitViewportEngine EnsureViewport() =>
        _viewport ??= new SpreadsheetSplitViewportEngine(
            _session ?? throw new InvalidOperationException("A spreadsheet session is required."));

    private void SetSession(SpreadsheetSession? value)
    {
        if (ReferenceEquals(_session, value))
        {
            return;
        }
        DetachSessionEvents();
        _session = value;
        _viewport = value is null ? null : new SpreadsheetSplitViewportEngine(value);
        _cellEditor = value?.Editor;
        _lastFrame = null;
        _headerResize = null;
        _splitDragKind = SpreadsheetSplitHitRegionKind.None;
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
        _viewport?.ResetPaneScrolls();
        _lastFrame = null;
        _headerResize = null;
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
        _lastFrame = null;
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
        _viewport?.InvalidateSnapshot();
        Invalidate();
    }

    private void OnDimensionsChanged(object? sender, DimensionChangedEventArgs e)
    {
        _viewport?.InvalidateMetrics();
        _lastFrame = null;
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
        if (_cellEditor?.State is not { } state ||
            _viewport is null ||
            _session is null ||
            _lastFrame is null ||
            !_viewport.TryGetCellBounds(ActivePane, state.Address, out var bounds) ||
            !_lastFrame.TryGetPane(ActivePane, out var paneFrame))
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
        var paneBounds = paneFrame.Pane.Bounds;
        var viewportLayout = paneFrame.ViewportFrame.Layout;
        var frozenWidth = Math.Clamp(
            (int)Math.Ceiling(viewportLayout.FrozenWidth),
            0,
            (int)Math.Ceiling(paneBounds.Width));
        var frozenHeight = Math.Clamp(
            (int)Math.Ceiling(viewportLayout.FrozenHeight),
            0,
            (int)Math.Ceiling(paneBounds.Height));
        var paneOriginX = (int)Math.Ceiling(chrome.RowHeaderWidth + paneBounds.Left);
        var paneOriginY = (int)Math.Ceiling(chrome.ColumnHeaderHeight + paneBounds.Top);
        var frozenColumn = state.Address.ColumnIndex < _session.View.FrozenColumns;
        var frozenRow = state.Address.RowIndex < _session.View.FrozenRows;
        var subPane = Rectangle.FromLTRB(
            paneOriginX + (frozenColumn ? 0 : frozenWidth),
            paneOriginY + (frozenRow ? 0 : frozenHeight),
            paneOriginX + (frozenColumn ? frozenWidth : (int)Math.Ceiling(paneBounds.Width)),
            paneOriginY + (frozenRow ? frozenHeight : (int)Math.Ceiling(paneBounds.Height)));
        var visible = Rectangle.Intersect(Rectangle.Intersect(raw, subPane), ClientRectangle);
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

    private void InvalidateSplitGeometry()
    {
        _lastFrame = null;
        _viewport?.ClearDisplayListCache();
        UpdateEditorBounds();
        Invalidate();
    }

    private void StartFrameLoop()
    {
        _lastFrameUtc = DateTime.UtcNow;
        _frameTimer.Start();
    }

    private void OnFrameTick(object? sender, EventArgs e)
    {
        if (_viewport is null || _lastFrame is null)
        {
            _frameTimer.Stop();
            return;
        }

        var before = _lastFrame.Layout.Panes.ToDictionary(
            static pane => pane.PaneId,
            pane => _viewport.GetPaneScrollSnapshot(pane.PaneId));
        var now = DateTime.UtcNow;
        var elapsed = now - _lastFrameUtc;
        _lastFrameUtc = now;
        if (_viewport.AdvanceScrollFrame(elapsed))
        {
            foreach (var pane in _lastFrame.Layout.Panes)
            {
                var after = _viewport.GetPaneScrollSnapshot(pane.PaneId);
                if (before[pane.PaneId] != after)
                {
                    PaneScrollChanged?.Invoke(
                        this,
                        new SplitPaneScrollChangedEventArgs(pane.PaneId, after));
                }
            }
            ScrollChanged?.Invoke(this, new ScrollChangedEventArgs(ScrollSnapshot));
            UpdateEditorBounds();
            Invalidate();
        }
        if (!_viewport.HasPendingScroll)
        {
            _frameTimer.Stop();
        }
    }

    private static DisplayList CreateDirtyClippedDisplayList(
        DisplayList displayList,
        Rectangle clipRectangle)
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

    private static void ValidateOptionalSplit(double? value, string parameterName)
    {
        if (value is { } split && !double.IsFinite(split))
        {
            throw new ArgumentOutOfRangeException(parameterName, split, "Split position must be finite when specified.");
        }
    }
}
