using System.ComponentModel;
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

public sealed partial class NeraSpreadsheetControl : Control
{
    private const double DirtyRegionPadding = 3d;
    private readonly ContinuousScrollController _scrollController = new();
    private readonly WinFormsDisplayListRenderer _displayListRenderer = new();
    private readonly System.Windows.Forms.Timer _frameTimer;
    private readonly TextBox _editor;
    private SpreadsheetSession? _session;
    private SpreadsheetViewportEngine? _viewport;
    private SpreadsheetAnalyticsViewportInteractionController? _analyticsInput;
    private SpreadsheetCellEditorController? _cellEditor;
    private Worksheet? _subscribedWorksheet;
    private Direct2DHwndDisplayListRenderer? _direct2DRenderer;
    private Direct2DSwapChainDisplayListRenderer? _swapChainRenderer;
    private ViewportLayout? _lastLayout;
    private SpreadsheetHeaderResizeHandle? _headerResize;
    private DateTime _lastFrameUtc = DateTime.UtcNow;
    private WinFormsRenderingBackend _renderingBackend;
    private bool _swapChainVSync = true;
    private bool _useAdaptiveNavigationExtent;
    private int _adaptiveNavigationTrailingRowCount =
        SpreadsheetViewportEngine.DefaultAdaptiveTrailingRowCount;
    private int _adaptiveNavigationTrailingColumnCount =
        SpreadsheetViewportEngine.DefaultAdaptiveTrailingColumnCount;

    public NeraSpreadsheetControl()
    {
        SetGdiPaintingStyles(enabled: true);
        BackColor = Color.White;
        TabStop = true;
        _frameTimer = new System.Windows.Forms.Timer { Interval = 8 };
        _frameTimer.Tick += OnFrameTick;
        _editor = new TextBox { Visible = false, BorderStyle = BorderStyle.FixedSingle, Multiline = true, AcceptsReturn = true, AcceptsTab = true };
        _editor.KeyDown += OnEditorKeyDown;
        Controls.Add(_editor);
        InitializeFormulaEditingUi();
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

    /// <summary>
    /// Gets or sets whether the scroll range follows the sparse used range and
    /// active cell instead of exposing the full physical worksheet extent.
    /// </summary>
    [DefaultValue(false)]
    public bool UseAdaptiveNavigationExtent
    {
        get => _useAdaptiveNavigationExtent;
        set
        {
            if (_useAdaptiveNavigationExtent == value)
            {
                return;
            }
            _useAdaptiveNavigationExtent = value;
            UpdateContentExtent();
            ClampScrollToContentBounds();
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the minimum blank rows kept after used or navigated content
    /// when <see cref="UseAdaptiveNavigationExtent"/> is enabled.
    /// </summary>
    [DefaultValue(SpreadsheetViewportEngine.DefaultAdaptiveTrailingRowCount)]
    public int AdaptiveNavigationTrailingRowCount
    {
        get => _adaptiveNavigationTrailingRowCount;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (_adaptiveNavigationTrailingRowCount == value)
            {
                return;
            }
            _adaptiveNavigationTrailingRowCount = value;
            UpdateContentExtent();
            ClampScrollToContentBounds();
            Invalidate();
        }
    }

    /// <summary>
    /// Gets or sets the minimum blank columns kept after used or navigated
    /// content when <see cref="UseAdaptiveNavigationExtent"/> is enabled.
    /// </summary>
    [DefaultValue(SpreadsheetViewportEngine.DefaultAdaptiveTrailingColumnCount)]
    public int AdaptiveNavigationTrailingColumnCount
    {
        get => _adaptiveNavigationTrailingColumnCount;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            if (_adaptiveNavigationTrailingColumnCount == value)
            {
                return;
            }
            _adaptiveNavigationTrailingColumnCount = value;
            UpdateContentExtent();
            ClampScrollToContentBounds();
            Invalidate();
        }
    }

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
        if (_analyticsInput?.IsTransforming == true)
        {
            return;
        }
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
        if (TryBeginFormulaReferencePointer(e.X, e.Y)) return;
        if (IsEditing)
        {
            CommitEditor();
        }
        Focus();

        if (TryBeginHeaderResize(e.X, e.Y))
        {
            return;
        }

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

        var viewport = EnsureViewport();
        if (_lastLayout is not null &&
            EnsureAnalyticsInput().PointerPressed(
                new PointD(hit.BodyX, hit.BodyY),
                _lastLayout))
        {
            Capture = true;
            Cursor = GetAnalyticsCursor(
                _session.AnalyticsInteraction.Snapshot.ActiveHandle);
            Invalidate();
            return;
        }

        if (!viewport.TryHitTest(hit.BodyX, hit.BodyY, scroll.OffsetX, scroll.OffsetY, out var address))
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
        if (UpdateFormulaReferencePointer(e.X, e.Y)) return;
        if (_headerResize is { } resize)
        {
            ApplyHeaderResize(resize, e.X, e.Y);
            Cursor = GetResizeCursor(resize.Axis);
            return;
        }
        if (_analyticsInput?.IsTransforming == true)
        {
            _analyticsInput.PointerMoved(ToBodyPoint(e.X, e.Y));
            Cursor = _session is null
                ? Cursors.Default
                : GetAnalyticsCursor(
                    _session.AnalyticsInteraction.Snapshot.ActiveHandle);
            Invalidate();
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
        if (UpdateFormulaReferencePointer(e.X, e.Y, release: true)) return;

        if (_headerResize is { } resize)
        {
            ApplyHeaderResize(resize, e.X, e.Y);
            _headerResize = null;
            Capture = false;
            UpdatePointerCursor(e.X, e.Y);
            return;
        }
        if (_analyticsInput?.IsTransforming == true)
        {
            _analyticsInput.PointerReleased(ToBodyPoint(e.X, e.Y));
            Capture = false;
            UpdatePointerCursor(e.X, e.Y);
            Invalidate();
        }
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (Capture)
        {
            return;
        }

        var changed = false;
        if (_headerResize is not null)
        {
            _headerResize = null;
            changed = true;
        }
        if (_analyticsInput?.IsTransforming == true)
        {
            _analyticsInput.Cancel();
            changed = true;
        }
        if (changed)
        {
            Cursor = Cursors.Default;
            Invalidate();
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

        if (_session.AnalyticsInteraction.SelectedItem.HasValue)
        {
            var analyticsKey = e.KeyCode switch
            {
                Keys.Left => SpreadsheetAnalyticsKeyboardKey.Left,
                Keys.Right => SpreadsheetAnalyticsKeyboardKey.Right,
                Keys.Up => SpreadsheetAnalyticsKeyboardKey.Up,
                Keys.Down => SpreadsheetAnalyticsKeyboardKey.Down,
                Keys.Delete => SpreadsheetAnalyticsKeyboardKey.Delete,
                Keys.Escape => SpreadsheetAnalyticsKeyboardKey.Escape,
                _ => (SpreadsheetAnalyticsKeyboardKey?)null,
            };
            if (analyticsKey.HasValue)
            {
                var modifiers = SpreadsheetAnalyticsKeyboardModifiers.None;
                if (e.Shift)
                {
                    modifiers |= SpreadsheetAnalyticsKeyboardModifiers.Shift;
                }
                if (e.Control)
                {
                    modifiers |= SpreadsheetAnalyticsKeyboardModifiers.Control;
                }

                e.Handled = EnsureAnalyticsInput().Keyboard(
                    analyticsKey.Value,
                    modifiers);
                if (e.Handled)
                {
                    e.SuppressKeyPress = true;
                    return;
                }
            }
            if (e.KeyCode == Keys.F2)
            {
                e.Handled = true;
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
        if (_session is null ||
            IsEditing ||
            _session.AnalyticsInteraction.SelectedItem.HasValue ||
            char.IsControl(e.KeyChar))
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
                _lastLayout = null;
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
            _lastLayout = frame.Layout;
            UpdateContentExtent(chrome);
            var bodyDisplayList = SpreadsheetFormulaReferenceDisplayListComposer.Compose(
                frame.DisplayList, frame.Layout, GetFormulaReferenceHighlights(), RenderTheme.FormulaReferenceStrokeWidth);
            var displayList = SpreadsheetChromeDisplayListComposer.Compose(
                bodyDisplayList,
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
            _lastLayout = null;
            RenderEmptyBackground(e.Graphics);
        }

        base.OnPaint(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _lastLayout = null;
        if (ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            _direct2DRenderer?.Resize(ClientSize.Width, ClientSize.Height);
            _swapChainRenderer?.Resize(ClientSize.Width, ClientSize.Height);
            if (_direct2DRenderer is not null || _swapChainRenderer is not null)
            {
                Invalidate();
            }
        }
        UpdateContentExtent();
        ClampScrollToContentBounds();
        UpdateEditorBounds();
    }

    public void BeginEdit(string? replacementText = null)
    {
        if (_cellEditor is null)
        {
            return;
        }
        var state = _cellEditor.BeginEdit();
        _editor.WordWrap = _session!.ActiveWorksheet.GetEffectiveStyle(state.Address, _session.Workbook.Styles).Alignment.WrapText;
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
        UpdateFormulaSuggestions();
    }

    public bool CommitEditor()
    {
        if (_cellEditor?.State is { } target) _session!.Selection.SetActiveCell(target.Address);
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
        if (_cellEditor?.State is { } target) _session!.Selection.SetActiveCell(target.Address);
        var canceled = _cellEditor?.Cancel() == true;
        // Session activation may already have canceled the draft. The native
        // overlay still needs cleanup without selecting the old cell again.
        HideEditor();
        if (canceled) Focus();
        return canceled;
    }

    public void QueuePrecisionScroll(double deltaX, double deltaY)
    {
        _scrollController.QueueDelta(new ScrollDelta(deltaX, deltaY, ScrollInputKind.Precision));
        StartFrameLoop();
    }

    public void ScrollTo(double offsetX, double offsetY, bool animated = false)
    {
        var before = _scrollController.Snapshot;
        _scrollController.ScrollTo(offsetX, offsetY, animated);
        UpdateContentExtent();
        UpdateEditorBounds();
        Invalidate();
        if (!animated && before != _scrollController.Snapshot)
        {
            ScrollChanged?.Invoke(
                this,
                new ScrollChangedEventArgs(_scrollController.Snapshot));
        }
        if (animated)
        {
            StartFrameLoop();
        }
    }

    /// <summary>
    /// Scrolls only as far as needed to make a worksheet cell visible while
    /// preserving continuous pixel offsets and frozen panes.
    /// </summary>
    public bool ScrollCellIntoView(CellAddress address)
    {
        if (_session is null || _viewport is null)
        {
            return false;
        }

        UpdateContentExtent();
        var chrome = GetChromeMetrics();
        var scroll = _scrollController.Snapshot;
        if (!_viewport.TryGetCellBounds(
                address,
                scroll.OffsetX,
                scroll.OffsetY,
                out var bounds))
        {
            return false;
        }

        var frozen = _viewport.GetFrozenPaneExtent();
        var nextX = scroll.OffsetX;
        var nextY = scroll.OffsetY;
        if (address.ColumnIndex >= _session.View.FrozenColumns)
        {
            var visibleLeft = Math.Clamp(frozen.Width, 0d, chrome.BodyWidth);
            if (bounds.Left < visibleLeft)
            {
                nextX -= visibleLeft - bounds.Left;
            }
            else if (bounds.Right > chrome.BodyWidth)
            {
                nextX += bounds.Right - chrome.BodyWidth;
            }
        }
        if (address.RowIndex >= _session.View.FrozenRows)
        {
            var visibleTop = Math.Clamp(frozen.Height, 0d, chrome.BodyHeight);
            if (bounds.Top < visibleTop)
            {
                nextY -= visibleTop - bounds.Top;
            }
            else if (bounds.Bottom > chrome.BodyHeight)
            {
                nextY += bounds.Bottom - chrome.BodyHeight;
            }
        }

        nextX = Math.Clamp(
            nextX,
            0d,
            Math.Max(0d, ContentWidth - chrome.BodyWidth));
        nextY = Math.Clamp(
            nextY,
            0d,
            Math.Max(0d, ContentHeight - chrome.BodyHeight));
        if (Math.Abs(nextX - scroll.OffsetX) <= 1e-9 &&
            Math.Abs(nextY - scroll.OffsetY) <= 1e-9)
        {
            return false;
        }

        ScrollTo(nextX, nextY, animated: false);
        return true;
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
            DisposeFormulaEditingUi();
            DisposeGpuRenderers();
            _displayListRenderer.Dispose();
        }
        base.Dispose(disposing);
    }

    private SpreadsheetChromeMetrics GetChromeMetrics() =>
        SpreadsheetChromeGeometry.Calculate(ClientSize.Width, ClientSize.Height, RenderTheme);

    private PointD ToBodyPoint(double x, double y)
    {
        var chrome = GetChromeMetrics();
        return new PointD(
            x - chrome.RowHeaderWidth,
            y - chrome.ColumnHeaderHeight);
    }

    private bool TryBeginHeaderResize(double x, double y)
    {
        if (_lastLayout is null ||
            !SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(
                x,
                y,
                ClientSize.Width,
                ClientSize.Height,
                RenderTheme,
                _lastLayout,
                out var resize))
        {
            return false;
        }

        _headerResize = resize;
        Capture = true;
        Cursor = GetResizeCursor(resize.Axis);
        return true;
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
        if (_lastLayout is not null &&
            SpreadsheetHeaderResizeGeometry.TryHitResizeHandle(
                x,
                y,
                ClientSize.Width,
                ClientSize.Height,
                RenderTheme,
                _lastLayout,
                out var resize))
        {
            Cursor = GetResizeCursor(resize.Axis);
            return;
        }
        if (_session is null || _lastLayout is null || _viewport is null)
        {
            Cursor = Cursors.Default;
            return;
        }

        var hit = SpreadsheetChromeGeometry.HitTest(
            x,
            y,
            ClientSize.Width,
            ClientSize.Height,
            RenderTheme);
        if (hit.Region != SpreadsheetChromeRegion.Body)
        {
            Cursor = Cursors.Default;
            return;
        }

        var analyticsHit = SpreadsheetAnalyticsHitTester.HitTest(
            _viewport.GetAnalyticsInteractionTargets(_lastLayout),
            new PointD(hit.BodyX, hit.BodyY),
            _session.AnalyticsInteraction.SelectedItem);
        Cursor = analyticsHit.HasValue
            ? GetAnalyticsCursor(analyticsHit.Value.Handle)
            : Cursors.Default;
    }

    private static Cursor GetResizeCursor(WorksheetAxis axis) =>
        axis == WorksheetAxis.Row ? Cursors.SizeNS : Cursors.SizeWE;

    private static Cursor GetAnalyticsCursor(
        SpreadsheetAnalyticsResizeHandle handle) =>
        handle switch
        {
            SpreadsheetAnalyticsResizeHandle.Move => Cursors.SizeAll,
            SpreadsheetAnalyticsResizeHandle.North or
                SpreadsheetAnalyticsResizeHandle.South => Cursors.SizeNS,
            SpreadsheetAnalyticsResizeHandle.East or
                SpreadsheetAnalyticsResizeHandle.West => Cursors.SizeWE,
            SpreadsheetAnalyticsResizeHandle.NorthWest or
                SpreadsheetAnalyticsResizeHandle.SouthEast => Cursors.SizeNWSE,
            SpreadsheetAnalyticsResizeHandle.NorthEast or
                SpreadsheetAnalyticsResizeHandle.SouthWest => Cursors.SizeNESW,
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

    private SpreadsheetViewportEngine EnsureViewport() => _viewport ??= new SpreadsheetViewportEngine(
        _session ?? throw new InvalidOperationException("A spreadsheet session is required."));

    private SpreadsheetAnalyticsViewportInteractionController EnsureAnalyticsInput() =>
        _analyticsInput ??= new SpreadsheetAnalyticsViewportInteractionController(
            EnsureViewport());

    private void SetSession(SpreadsheetSession? value)
    {
        if (ReferenceEquals(_session, value))
        {
            return;
        }
        DetachSessionEvents();
        _session = value;
        _viewport = value is null ? null : new SpreadsheetViewportEngine(value);
        _analyticsInput = _viewport is null
            ? null
            : new SpreadsheetAnalyticsViewportInteractionController(_viewport);
        _cellEditor = value?.Editor;
        _lastLayout = null;
        _headerResize = null;
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
        _session.Analytics.Changed += OnAnalyticsChanged;
        _session.AnalyticsPlacements.Changed += OnAnalyticsPlacementChanged;
        _session.AnalyticsInteraction.Changed += OnAnalyticsInteractionChanged;
        EnsureWorksheetSubscription();
    }

    private void DetachSessionEvents()
    {
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
            _session.Selection.Changed -= OnSelectionChanged;
            _session.View.Changed -= OnViewChanged;
            _session.Analytics.Changed -= OnAnalyticsChanged;
            _session.AnalyticsPlacements.Changed -= OnAnalyticsPlacementChanged;
            _session.AnalyticsInteraction.Changed -= OnAnalyticsInteractionChanged;
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
        _lastLayout = null;
        _headerResize = null;
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
        _lastLayout = null;
        UpdateEditorBounds();
        Invalidate();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_useAdaptiveNavigationExtent)
        {
            UpdateContentExtent();
            ClampScrollToContentBounds();
        }
        UpdateEditorBounds();
        Invalidate();
    }

    private void OnAnalyticsChanged(
        object? sender,
        SpreadsheetAnalyticsChangedEventArgs e)
    {
        if (_session is not null &&
            ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            Invalidate();
        }
    }

    private void OnAnalyticsPlacementChanged(
        object? sender,
        SpreadsheetAnalyticsPlacementChangedEventArgs e)
    {
        if (_session is not null &&
            ReferenceEquals(e.Worksheet, _session.ActiveWorksheet))
        {
            Invalidate();
        }
    }

    private void OnAnalyticsInteractionChanged(object? sender, EventArgs e) =>
        Invalidate();

    private void OnCellsChanged(object? sender, CellsChangedEventArgs e)
    {
        if (_useAdaptiveNavigationExtent)
        {
            UpdateContentExtent();
            ClampScrollToContentBounds();
        }
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
        _lastLayout = null;
        UpdateContentExtent();
        ClampScrollToContentBounds();
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
        UpdateContentExtent(GetChromeMetrics());
    }

    private void UpdateContentExtent(SpreadsheetChromeMetrics chrome)
    {
        if (_viewport is null)
        {
            ContentWidth = 0d;
            ContentHeight = 0d;
            return;
        }
        var extent = _useAdaptiveNavigationExtent && _session is not null
            ? _viewport.GetAdaptiveNavigationExtent(
                _session.Selection.ActiveCell,
                new SizeD(chrome.BodyWidth, chrome.BodyHeight),
                new PointD(
                    _scrollController.Snapshot.OffsetX,
                    _scrollController.Snapshot.OffsetY),
                _adaptiveNavigationTrailingRowCount,
                _adaptiveNavigationTrailingColumnCount)
            : _viewport.GetContentExtent();
        ContentWidth = extent.Width;
        ContentHeight = extent.Height;
    }

    private void ClampScrollToContentBounds()
    {
        if (_viewport is null)
        {
            return;
        }
        var chrome = GetChromeMetrics();
        var snapshot = _scrollController.Snapshot;
        var nextX = Math.Clamp(
            snapshot.OffsetX,
            0d,
            Math.Max(0d, ContentWidth - chrome.BodyWidth));
        var nextY = Math.Clamp(
            snapshot.OffsetY,
            0d,
            Math.Max(0d, ContentHeight - chrome.BodyHeight));
        if (Math.Abs(nextX - snapshot.OffsetX) > 1e-9 ||
            Math.Abs(nextY - snapshot.OffsetY) > 1e-9)
        {
            ScrollTo(nextX, nextY, animated: false);
        }
    }

    private void MoveActiveCell(int rowDelta, int columnDelta, bool extend)
    {
        if (_session is null)
        {
            return;
        }
        var active = _session.Selection.ActiveCell;
        var next = SpreadsheetVisibleCellNavigation.GetNextVisibleCell(
            _session.ActiveWorksheet,
            active,
            rowDelta,
            columnDelta);
        if (extend)
        {
            _session.Selection.ExtendTo(next);
        }
        else
        {
            _session.Selection.SetActiveCell(next);
        }
        ScrollCellIntoView(next);
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (TryHandleFormulaSuggestionKey(e))
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            return;
        }
        if (e.KeyCode == Keys.Enter)
        {
            if (e.Alt)
            {
                _editor.SelectedText = Environment.NewLine;
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
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
            _formulaSuggestionList.Visible = false;
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
            _formulaSuggestionList.Visible = false;
            return;
        }

        _editor.Bounds = raw;
        var oldRegion = _editor.Region;
        _editor.Region = new Region(new Rectangle(visible.X - raw.X, visible.Y - raw.Y, visible.Width, visible.Height));
        oldRegion?.Dispose();
        _editor.Visible = true;
        _editor.BringToFront();
        UpdateFormulaSuggestionBounds();
    }

    private void HideEditor()
    {
        ResetFormulaEditingUi();
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
            UpdateContentExtent(chrome);
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
