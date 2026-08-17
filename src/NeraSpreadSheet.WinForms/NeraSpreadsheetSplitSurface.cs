using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Direct2D;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

internal sealed class NeraSpreadsheetSplitSurface : Control
{
    private const double GeometryEpsilon = 1e-9;
    private readonly NeraSpreadsheetControl _owner;
    private readonly WinFormsDisplayListRenderer _displayListRenderer = new();
    private readonly System.Windows.Forms.Timer _frameTimer;
    private readonly TextBox _editor;
    private SpreadsheetSession? _session;
    private Worksheet? _subscribedWorksheet;
    private SpreadsheetSplitViewportEngine? _engine;
    private SpreadsheetCellEditorController? _cellEditor;
    private SpreadsheetSplitViewportFrame? _lastFrame;
    private Direct2DHwndDisplayListRenderer? _direct2DRenderer;
    private Direct2DSwapChainDisplayListRenderer? _swapChainRenderer;
    private SplitDragState? _splitDrag;
    private DateTime _lastFrameUtc = DateTime.UtcNow;
    private WinFormsRenderingBackend _activeBackend;
    private SpreadsheetSplitPaneMode _mode;
    private double? _splitX;
    private double? _splitY;
    private double _separatorThickness = 6d;
    private double _minimumPaneExtent = 64d;

    public NeraSpreadsheetSplitSurface(NeraSpreadsheetControl owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _activeBackend = owner.RenderingBackend;
        SetGdiPaintingStyles(_activeBackend == WinFormsRenderingBackend.GdiPlus);
        Dock = DockStyle.Fill;
        TabStop = true;
        BackColor = owner.BackColor;
        _frameTimer = new System.Windows.Forms.Timer { Interval = 8 };
        _frameTimer.Tick += OnFrameTick;
        _editor = new TextBox
        {
            Visible = false,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _editor.KeyDown += OnEditorKeyDown;
        Controls.Add(_editor);
    }

    public SpreadsheetSplitPaneMode Mode => _mode;

    public double? SplitX => _splitX;

    public double? SplitY => _splitY;

    public double SeparatorThickness
    {
        get => _separatorThickness;
        set
        {
            var validated = Guard.PositiveFinite(value, nameof(value));
            if (Math.Abs(_separatorThickness - validated) <= GeometryEpsilon)
            {
                return;
            }

            _separatorThickness = validated;
            InvalidateSplitLayout();
        }
    }

    public double MinimumPaneExtent
    {
        get => _minimumPaneExtent;
        set
        {
            var validated = Guard.PositiveFinite(value, nameof(value));
            if (Math.Abs(_minimumPaneExtent - validated) <= GeometryEpsilon)
            {
                return;
            }

            _minimumPaneExtent = validated;
            InvalidateSplitLayout();
        }
    }

    public SpreadsheetPaneId ActivePane => _engine?.ActivePane ?? SpreadsheetPaneId.TopLeft;

    public SpreadsheetSplitViewportFrame? LastFrame => _lastFrame;

    public Direct2DRendererDiagnostics? Direct2DDiagnostics => _direct2DRenderer?.Diagnostics;

    public Direct2DSwapChainRendererDiagnostics? SwapChainDiagnostics => _swapChainRenderer?.Diagnostics;

    public event EventHandler<SpreadsheetSplitChangedEventArgs>? SplitChanged;

    public event EventHandler<SpreadsheetPaneScrollChangedEventArgs>? PaneScrollChanged;

    public void SetMode(SpreadsheetSplitPaneMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        var metrics = GetChromeMetrics();
        var centeredX = Math.Max(0d, (metrics.BodyWidth - _separatorThickness) / 2d);
        var centeredY = Math.Max(0d, (metrics.BodyHeight - _separatorThickness) / 2d);
        SetSplitCore(
            mode,
            mode is SpreadsheetSplitPaneMode.Vertical or SpreadsheetSplitPaneMode.Both
                ? _splitX ?? centeredX
                : null,
            mode is SpreadsheetSplitPaneMode.Horizontal or SpreadsheetSplitPaneMode.Both
                ? _splitY ?? centeredY
                : null);
    }

    public void SetSplit(double? splitX, double? splitY)
    {
        ValidateSplitCoordinate(splitX, nameof(splitX));
        ValidateSplitCoordinate(splitY, nameof(splitY));
        var mode = (splitX, splitY) switch
        {
            (not null, not null) => SpreadsheetSplitPaneMode.Both,
            (not null, null) => SpreadsheetSplitPaneMode.Vertical,
            (null, not null) => SpreadsheetSplitPaneMode.Horizontal,
            _ => SpreadsheetSplitPaneMode.None,
        };
        SetSplitCore(mode, splitX, splitY);
    }

    public void SetActivePane(SpreadsheetPaneId paneId)
    {
        var engine = GetEngine();
        engine.SetActivePane(paneId);
        _lastFrame = null;
        Invalidate();
    }

    public PointD GetPaneScroll(SpreadsheetPaneId paneId) =>
        _engine?.GetPaneScroll(paneId) ?? default;

    public ScrollSnapshot GetPaneScrollSnapshot(SpreadsheetPaneId paneId) =>
        _engine?.GetPaneScrollSnapshot(paneId) ?? default;

    public void ScrollPaneTo(
        SpreadsheetPaneId paneId,
        double offsetX,
        double offsetY,
        bool animated)
    {
        var engine = GetEngine();
        engine.ScrollPaneTo(paneId, offsetX, offsetY, animated);
        _lastFrame = null;
        UpdateEditorBounds();
        Invalidate();
        if (animated)
        {
            StartFrameLoop();
        }
    }

    public void QueuePaneScroll(SpreadsheetPaneId paneId, ScrollDelta delta)
    {
        GetEngine().QueuePaneScroll(paneId, delta);
        StartFrameLoop();
    }

    public void QueueActivePaneScroll(ScrollDelta delta)
    {
        GetEngine().QueueActivePaneScroll(delta);
        StartFrameLoop();
    }

    public bool TryHitTest(
        double clientX,
        double clientY,
        out SpreadsheetPaneId paneId,
        out CellAddress address)
    {
        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null)
        {
            paneId = default;
            address = default;
            return false;
        }

        var hit = SpreadsheetChromeGeometry.HitTest(
            clientX,
            clientY,
            ClientSize.Width,
            ClientSize.Height,
            _owner.RenderTheme);
        return hit.Region == SpreadsheetChromeRegion.Body &&
            GetEngine().TryHitTest(hit.BodyX, hit.BodyY, out paneId, out address);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SynchronizeBackend();
        EnsureSelectedGpuRenderer();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DisposeGpuRenderers();
        base.OnHandleDestroyed(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        SynchronizeBackend();
        if (_activeBackend == WinFormsRenderingBackend.GdiPlus)
        {
            base.OnPaintBackground(pevent);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        SynchronizeSession();
        SynchronizeBackend();

        var frame = EnsureFrame();
        if (frame is null)
        {
            RenderEmptyBackground(e.Graphics);
            base.OnPaint(e);
            return;
        }

        var paneLayouts = new List<SpreadsheetSplitPaneChromeLayout>(frame.Panes.Count);
        foreach (var pane in frame.Panes)
        {
            paneLayouts.Add(new SpreadsheetSplitPaneChromeLayout(
                pane.Pane.PaneId,
                pane.Pane.Bounds,
                pane.ViewportFrame.Layout));
        }

        var displayList = SpreadsheetSplitChromeDisplayListComposer.Compose(
            frame.DisplayList,
            frame.Layout,
            paneLayouts,
            _session!.Selection.Capture(),
            _owner.RenderTheme);
        switch (_activeBackend)
        {
            case WinFormsRenderingBackend.Direct2D:
                EnsureDirect2DRenderer().Render(displayList);
                break;
            case WinFormsRenderingBackend.Direct2DSwapChain:
                EnsureSwapChainRenderer().Render(displayList);
                break;
            default:
                _displayListRenderer.Render(e.Graphics, displayList);
                break;
        }

        base.OnPaint(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _lastFrame = null;
        if (ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            _direct2DRenderer?.Resize(ClientSize.Width, ClientSize.Height);
            _swapChainRenderer?.Resize(ClientSize.Width, ClientSize.Height);
        }
        UpdateEditorBounds();
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        SynchronizeSession();
        var frame = EnsureFrame();
        if (frame is null)
        {
            return;
        }

        var paneId = ResolvePaneAtClientPoint(e.X, e.Y, frame) ?? frame.ActivePane;
        GetEngine().SetActivePane(paneId);
        var notches = e.Delta / 120d;
        var delta = -notches * _owner.WheelPixelsPerNotch;
        GetEngine().QueuePaneScroll(
            paneId,
            (ModifierKeys & Keys.Shift) != 0
                ? new ScrollDelta(delta, 0d, ScrollInputKind.Wheel)
                : new ScrollDelta(0d, delta, ScrollInputKind.Wheel));
        StartFrameLoop();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        SynchronizeSession();
        if (e.Button != MouseButtons.Left || _session is null)
        {
            return;
        }

        if (IsEditing)
        {
            CommitEditor();
        }
        Focus();

        if (TryBeginSeparatorDrag(e.X, e.Y))
        {
            return;
        }

        var frame = EnsureFrame();
        if (frame is null)
        {
            return;
        }
        var chromeHit = SpreadsheetChromeGeometry.HitTest(
            e.X,
            e.Y,
            ClientSize.Width,
            ClientSize.Height,
            _owner.RenderTheme);
        switch (chromeHit.Region)
        {
            case SpreadsheetChromeRegion.Corner:
                _session.Selection.SelectAll();
                return;
            case SpreadsheetChromeRegion.RowHeader:
                if (TryHitTestRowHeader(frame, chromeHit.BodyY, out var rowPane, out var rowIndex))
                {
                    GetEngine().SetActivePane(rowPane);
                    if ((ModifierKeys & Keys.Shift) != 0)
                    {
                        _session.Selection.ExtendRowsTo(rowIndex);
                    }
                    else
                    {
                        _session.Selection.SelectRow(
                            rowIndex,
                            additive: (ModifierKeys & Keys.Control) != 0);
                    }
                }
                return;
            case SpreadsheetChromeRegion.ColumnHeader:
                if (TryHitTestColumnHeader(
                    frame,
                    chromeHit.BodyX,
                    out var columnPane,
                    out var columnIndex))
                {
                    GetEngine().SetActivePane(columnPane);
                    if ((ModifierKeys & Keys.Shift) != 0)
                    {
                        _session.Selection.ExtendColumnsTo(columnIndex);
                    }
                    else
                    {
                        _session.Selection.SelectColumn(
                            columnIndex,
                            additive: (ModifierKeys & Keys.Control) != 0);
                    }
                }
                return;
            case SpreadsheetChromeRegion.Body:
                break;
            default:
                return;
        }

        GetEngine().TryActivatePaneAt(chromeHit.BodyX, chromeHit.BodyY);
        if (!GetEngine().TryHitTest(
            chromeHit.BodyX,
            chromeHit.BodyY,
            out _,
            out var address))
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

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_splitDrag is { } drag)
        {
            ApplySeparatorDrag(drag, e.X, e.Y);
            Cursor = GetSeparatorCursor(drag.Vertical, drag.Horizontal);
            return;
        }

        var separator = HitTestSeparator(e.X, e.Y);
        Cursor = separator.HasValue
            ? GetSeparatorCursor(separator.Value.Vertical, separator.Value.Horizontal)
            : Cursors.Default;
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button != MouseButtons.Left || _splitDrag is not { } drag)
        {
            return;
        }

        ApplySeparatorDrag(drag, e.X, e.Y);
        _splitDrag = null;
        Capture = false;
        var separator = HitTestSeparator(e.X, e.Y);
        Cursor = separator.HasValue
            ? GetSeparatorCursor(separator.Value.Vertical, separator.Value.Horizontal)
            : Cursors.Default;
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (Capture || _splitDrag is null)
        {
            return;
        }

        _splitDrag = null;
        Cursor = Cursors.Default;
    }

    protected override bool IsInputKey(Keys keyData) =>
        (keyData & Keys.KeyCode) is Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Tab ||
        base.IsInputKey(keyData);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        SynchronizeSession();
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
        SynchronizeSession();
        if (_session is null || IsEditing || char.IsControl(e.KeyChar))
        {
            return;
        }

        BeginEdit(e.KeyChar.ToString());
        e.Handled = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cellEditor?.Cancel();
            HideEditor();
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

    private bool IsEditing => _cellEditor?.IsEditing == true;

    private SpreadsheetSplitViewportEngine GetEngine()
    {
        SynchronizeSession();
        return _engine ?? throw new InvalidOperationException(
            "A spreadsheet session is required before split-pane operations can run.");
    }

    private SpreadsheetSplitViewportFrame? EnsureFrame()
    {
        SynchronizeSession();
        if (_engine is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            _lastFrame = null;
            return null;
        }

        var chrome = GetChromeMetrics();
        if (chrome.BodyWidth <= 0d || chrome.BodyHeight <= 0d)
        {
            _lastFrame = null;
            return null;
        }

        _lastFrame = _engine.Compose(
            new SpreadsheetSplitRequest(
                new SizeD(chrome.BodyWidth, chrome.BodyHeight),
                _splitX,
                _splitY,
                _separatorThickness,
                _minimumPaneExtent),
            _owner.OverscanPixels,
            _owner.RenderTheme);
        return _lastFrame;
    }

    private SpreadsheetChromeMetrics GetChromeMetrics() =>
        SpreadsheetChromeGeometry.Calculate(
            ClientSize.Width,
            ClientSize.Height,
            _owner.RenderTheme);

    private void SetSplitCore(
        SpreadsheetSplitPaneMode mode,
        double? splitX,
        double? splitY)
    {
        if (_mode == mode && _splitX == splitX && _splitY == splitY)
        {
            return;
        }

        _mode = mode;
        _splitX = splitX;
        _splitY = splitY;
        InvalidateSplitLayout();
        SplitChanged?.Invoke(
            this,
            new SpreadsheetSplitChangedEventArgs(mode, splitX, splitY, _lastFrame?.Layout));
    }

    private void InvalidateSplitLayout()
    {
        _lastFrame = null;
        UpdateEditorBounds();
        Invalidate();
    }

    private bool TryBeginSeparatorDrag(double clientX, double clientY)
    {
        var hit = HitTestSeparator(clientX, clientY);
        if (hit is null)
        {
            return false;
        }

        var frame = EnsureFrame();
        if (frame is null)
        {
            return false;
        }

        var chrome = GetChromeMetrics();
        var bodyX = clientX - chrome.RowHeaderWidth;
        var bodyY = clientY - chrome.ColumnHeaderHeight;
        _splitDrag = new SplitDragState(
            hit.Value.Vertical,
            hit.Value.Horizontal,
            hit.Value.Vertical && frame.Layout.SplitX is { } splitX ? bodyX - splitX : 0d,
            hit.Value.Horizontal && frame.Layout.SplitY is { } splitY ? bodyY - splitY : 0d);
        Capture = true;
        Cursor = GetSeparatorCursor(hit.Value.Vertical, hit.Value.Horizontal);
        return true;
    }

    private SeparatorHit? HitTestSeparator(double clientX, double clientY)
    {
        var frame = _lastFrame ?? EnsureFrame();
        if (frame is null)
        {
            return null;
        }

        var chrome = GetChromeMetrics();
        var bodyX = clientX - chrome.RowHeaderWidth;
        var bodyY = clientY - chrome.ColumnHeaderHeight;
        var vertical = frame.Layout.HasVerticalSplit &&
            clientX >= chrome.RowHeaderWidth &&
            clientY >= 0d &&
            clientY < chrome.FullHeight &&
            bodyX >= frame.Layout.VerticalSeparator.Left &&
            bodyX < frame.Layout.VerticalSeparator.Right;
        var horizontal = frame.Layout.HasHorizontalSplit &&
            clientY >= chrome.ColumnHeaderHeight &&
            clientX >= 0d &&
            clientX < chrome.FullWidth &&
            bodyY >= frame.Layout.HorizontalSeparator.Top &&
            bodyY < frame.Layout.HorizontalSeparator.Bottom;
        return vertical || horizontal ? new SeparatorHit(vertical, horizontal) : null;
    }

    private void ApplySeparatorDrag(
        SplitDragState drag,
        double clientX,
        double clientY)
    {
        var chrome = GetChromeMetrics();
        var nextX = drag.Vertical
            ? clientX - chrome.RowHeaderWidth - drag.GrabOffsetX
            : _splitX;
        var nextY = drag.Horizontal
            ? clientY - chrome.ColumnHeaderHeight - drag.GrabOffsetY
            : _splitY;
        SetSplit(nextX, nextY);
    }

    private SpreadsheetPaneId? ResolvePaneAtClientPoint(
        double clientX,
        double clientY,
        SpreadsheetSplitViewportFrame frame)
    {
        var chromeHit = SpreadsheetChromeGeometry.HitTest(
            clientX,
            clientY,
            ClientSize.Width,
            ClientSize.Height,
            _owner.RenderTheme);
        switch (chromeHit.Region)
        {
            case SpreadsheetChromeRegion.Body:
            {
                var hit = frame.Layout.HitTest(new PointD(chromeHit.BodyX, chromeHit.BodyY));
                return hit.RegionKind == SpreadsheetSplitHitRegionKind.Pane ? hit.PaneId : null;
            }
            case SpreadsheetChromeRegion.RowHeader:
                return TryResolveLeftPane(frame, chromeHit.BodyY, out var rowPane) ? rowPane.Pane.PaneId : null;
            case SpreadsheetChromeRegion.ColumnHeader:
                return TryResolveTopPane(frame, chromeHit.BodyX, out var columnPane) ? columnPane.Pane.PaneId : null;
            default:
                return frame.ActivePane;
        }
    }

    private static bool TryHitTestRowHeader(
        SpreadsheetSplitViewportFrame frame,
        double bodyY,
        out SpreadsheetPaneId paneId,
        out int rowIndex)
    {
        if (TryResolveLeftPane(frame, bodyY, out var pane) &&
            TryHitAxisSlot(
                pane.ViewportFrame.Layout.Rows,
                bodyY - pane.Pane.Bounds.Y,
                out rowIndex))
        {
            paneId = pane.Pane.PaneId;
            return true;
        }

        paneId = default;
        rowIndex = default;
        return false;
    }

    private static bool TryHitTestColumnHeader(
        SpreadsheetSplitViewportFrame frame,
        double bodyX,
        out SpreadsheetPaneId paneId,
        out int columnIndex)
    {
        if (TryResolveTopPane(frame, bodyX, out var pane) &&
            TryHitAxisSlot(
                pane.ViewportFrame.Layout.Columns,
                bodyX - pane.Pane.Bounds.X,
                out columnIndex))
        {
            paneId = pane.Pane.PaneId;
            return true;
        }

        paneId = default;
        columnIndex = default;
        return false;
    }

    private static bool TryResolveLeftPane(
        SpreadsheetSplitViewportFrame frame,
        double bodyY,
        out SpreadsheetSplitPaneFrame pane)
    {
        foreach (var candidate in frame.Panes)
        {
            if (Math.Abs(candidate.Pane.Bounds.Left) <= GeometryEpsilon &&
                bodyY >= candidate.Pane.Bounds.Top &&
                bodyY < candidate.Pane.Bounds.Bottom)
            {
                pane = candidate;
                return true;
            }
        }

        pane = null!;
        return false;
    }

    private static bool TryResolveTopPane(
        SpreadsheetSplitViewportFrame frame,
        double bodyX,
        out SpreadsheetSplitPaneFrame pane)
    {
        foreach (var candidate in frame.Panes)
        {
            if (Math.Abs(candidate.Pane.Bounds.Top) <= GeometryEpsilon &&
                bodyX >= candidate.Pane.Bounds.Left &&
                bodyX < candidate.Pane.Bounds.Right)
            {
                pane = candidate;
                return true;
            }
        }

        pane = null!;
        return false;
    }

    private static bool TryHitAxisSlot(
        IReadOnlyList<AxisSlot> slots,
        double coordinate,
        out int index)
    {
        foreach (var slot in slots)
        {
            if (coordinate >= slot.Start && coordinate < slot.End)
            {
                index = slot.Index;
                return true;
            }
        }

        index = default;
        return false;
    }

    private void BeginEdit(string? replacementText = null)
    {
        if (_cellEditor is null || EnsureFrame() is null)
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

    private bool CommitEditor()
    {
        if (_cellEditor is null || !_cellEditor.Commit(_editor.Text))
        {
            return false;
        }

        HideEditor();
        Focus();
        return true;
    }

    private bool CancelEditor()
    {
        if (_cellEditor is null || !_cellEditor.Cancel())
        {
            return false;
        }

        HideEditor();
        Focus();
        return true;
    }

    private void UpdateEditorBounds()
    {
        if (_cellEditor?.State is not { } state ||
            _engine is null ||
            _session is null ||
            EnsureFrame() is not { } frame ||
            !frame.TryGetPane(frame.ActivePane, out var paneFrame) ||
            !_engine.TryGetCellBounds(frame.ActivePane, state.Address, out var bodyBounds))
        {
            _editor.Visible = false;
            return;
        }

        var chrome = GetChromeMetrics();
        var localBounds = bodyBounds.Translate(
            -paneFrame.Pane.Bounds.X,
            -paneFrame.Pane.Bounds.Y);
        var layout = paneFrame.ViewportFrame.Layout;
        var frozenColumn = state.Address.ColumnIndex < _session.View.FrozenColumns;
        var frozenRow = state.Address.RowIndex < _session.View.FrozenRows;
        var paneClip = new RectD(
            paneFrame.Pane.Bounds.X + (frozenColumn ? 0d : layout.FrozenWidth),
            paneFrame.Pane.Bounds.Y + (frozenRow ? 0d : layout.FrozenHeight),
            frozenColumn
                ? layout.FrozenWidth
                : Math.Max(0d, paneFrame.Pane.Bounds.Width - layout.FrozenWidth),
            frozenRow
                ? layout.FrozenHeight
                : Math.Max(0d, paneFrame.Pane.Bounds.Height - layout.FrozenHeight));
        var commonBounds = localBounds.Translate(
            paneFrame.Pane.Bounds.X,
            paneFrame.Pane.Bounds.Y);
        var visibleBody = commonBounds.Intersect(paneClip);
        if (visibleBody.IsEmpty)
        {
            _editor.Visible = false;
            return;
        }

        var raw = Rectangle.FromLTRB(
            (int)Math.Floor(chrome.RowHeaderWidth + visibleBody.Left),
            (int)Math.Floor(chrome.ColumnHeaderHeight + visibleBody.Top),
            (int)Math.Ceiling(chrome.RowHeaderWidth + visibleBody.Right),
            (int)Math.Ceiling(chrome.ColumnHeaderHeight + visibleBody.Bottom));
        var visible = Rectangle.Intersect(raw, ClientRectangle);
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

    private void SynchronizeSession()
    {
        var next = _owner.Session;
        if (ReferenceEquals(_session, next))
        {
            EnsureWorksheetSubscription();
            return;
        }

        DetachSessionEvents();
        _cellEditor?.Cancel();
        _session = next;
        _engine = next is null ? null : new SpreadsheetSplitViewportEngine(next);
        _cellEditor = next?.Editor;
        _lastFrame = null;
        HideEditor();
        AttachSessionEvents();
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
        _engine?.InvalidateMetrics();
        _engine?.ResetPaneScrolls();
        _lastFrame = null;
        Invalidate();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateEditorBounds();
        Invalidate();
    }

    private void OnViewChanged(object? sender, SpreadsheetViewChangedEventArgs e)
    {
        if (_session is null || !ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            return;
        }

        _engine?.ClearDisplayListCache();
        _lastFrame = null;
        UpdateEditorBounds();
        Invalidate();
    }

    private void OnCellsChanged(object? sender, CellsChangedEventArgs e)
    {
        _lastFrame = null;
        Invalidate();
    }

    private void OnDimensionsChanged(object? sender, DimensionChangedEventArgs e)
    {
        _engine?.InvalidateMetrics();
        _lastFrame = null;
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
        if (_engine is null)
        {
            _frameTimer.Stop();
            return;
        }

        var before = CaptureVisiblePaneScrolls();
        var now = DateTime.UtcNow;
        var changed = _engine.AdvanceScrollFrame(now - _lastFrameUtc);
        _lastFrameUtc = now;
        if (changed)
        {
            PublishChangedPaneScrolls(before);
            _lastFrame = null;
            UpdateEditorBounds();
            Invalidate();
        }

        if (!_engine.HasPendingScroll)
        {
            _frameTimer.Stop();
        }
    }

    private Dictionary<SpreadsheetPaneId, ScrollSnapshot> CaptureVisiblePaneScrolls()
    {
        var snapshots = new Dictionary<SpreadsheetPaneId, ScrollSnapshot>();
        if (_lastFrame is null || _engine is null)
        {
            return snapshots;
        }

        foreach (var pane in _lastFrame.Panes)
        {
            snapshots[pane.Pane.PaneId] = _engine.GetPaneScrollSnapshot(pane.Pane.PaneId);
        }
        return snapshots;
    }

    private void PublishChangedPaneScrolls(
        IReadOnlyDictionary<SpreadsheetPaneId, ScrollSnapshot> before)
    {
        if (_engine is null || _lastFrame is null)
        {
            return;
        }

        foreach (var pane in _lastFrame.Panes)
        {
            var paneId = pane.Pane.PaneId;
            var current = _engine.GetPaneScrollSnapshot(paneId);
            if (!before.TryGetValue(paneId, out var previous) || previous != current)
            {
                PaneScrollChanged?.Invoke(
                    this,
                    new SpreadsheetPaneScrollChangedEventArgs(paneId, current));
            }
        }
    }

    private void SynchronizeBackend()
    {
        var requested = _owner.RenderingBackend;
        if (_activeBackend != requested)
        {
            _activeBackend = requested;
            DisposeGpuRenderers();
            SetGdiPaintingStyles(requested == WinFormsRenderingBackend.GdiPlus);
        }

        if (_swapChainRenderer is not null)
        {
            _swapChainRenderer.VSync = _owner.SwapChainVSync;
        }
    }

    private void EnsureSelectedGpuRenderer()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        switch (_activeBackend)
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
        if (_activeBackend != WinFormsRenderingBackend.Direct2D)
        {
            throw new InvalidOperationException(
                "The HWND Direct2D backend is not selected for this split surface.");
        }

        _direct2DRenderer ??= new Direct2DHwndDisplayListRenderer(
            Handle,
            Math.Max(1, ClientSize.Width),
            Math.Max(1, ClientSize.Height));
        return _direct2DRenderer;
    }

    private Direct2DSwapChainDisplayListRenderer EnsureSwapChainRenderer()
    {
        EnsureGpuPlatformAndHandle();
        if (_activeBackend != WinFormsRenderingBackend.Direct2DSwapChain)
        {
            throw new InvalidOperationException(
                "The D3D11/DXGI backend is not selected for this split surface.");
        }

        if (_swapChainRenderer is null)
        {
            _swapChainRenderer = new Direct2DSwapChainDisplayListRenderer(
                Handle,
                Math.Max(1, ClientSize.Width),
                Math.Max(1, ClientSize.Height))
            {
                VSync = _owner.SwapChainVSync,
            };
        }
        return _swapChainRenderer;
    }

    private void EnsureGpuPlatformAndHandle()
    {
        if (!Direct2DBackendDescriptor.IsPlatformSupported)
        {
            throw new PlatformNotSupportedException(
                "The Direct2D backends require Windows 10 version 2004 or later.");
        }
        if (!IsHandleCreated)
        {
            throw new InvalidOperationException(
                "The split surface handle must exist before GPU initialization.");
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

        if (_activeBackend == WinFormsRenderingBackend.GdiPlus)
        {
            graphics.Clear(_owner.BackColor);
            return;
        }

        var builder = new DisplayListBuilder();
        builder.FillRectangle(
            new RectD(0d, 0d, ClientSize.Width, ClientSize.Height),
            new ColorRgba(
                _owner.BackColor.R,
                _owner.BackColor.G,
                _owner.BackColor.B,
                _owner.BackColor.A));
        var displayList = builder.Build();
        if (_activeBackend == WinFormsRenderingBackend.Direct2D)
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

    private static Cursor GetSeparatorCursor(bool vertical, bool horizontal) =>
        (vertical, horizontal) switch
        {
            (true, true) => Cursors.SizeAll,
            (true, false) => Cursors.VSplit,
            (false, true) => Cursors.HSplit,
            _ => Cursors.Default,
        };

    private static void ValidateSplitCoordinate(double? value, string parameterName)
    {
        if (value is { } coordinate && !double.IsFinite(coordinate))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                coordinate,
                "Split coordinates must be finite.");
        }
    }

    private readonly record struct SeparatorHit(bool Vertical, bool Horizontal);

    private readonly record struct SplitDragState(
        bool Vertical,
        bool Horizontal,
        double GrabOffsetX,
        double GrabOffsetY);
}
