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

internal sealed partial class NeraSpreadsheetSplitSurface : Control
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

    internal NeraSpreadsheetSplitSurface(NeraSpreadsheetControl owner)
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

    internal SpreadsheetSplitPaneMode Mode => _mode;

    internal double? SplitX => _splitX;

    internal double? SplitY => _splitY;

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal double SeparatorThickness
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

    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    internal double MinimumPaneExtent
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

    internal SpreadsheetPaneId ActivePane => _engine?.ActivePane ?? SpreadsheetPaneId.TopLeft;

    internal SpreadsheetSplitViewportFrame? LastFrame => _lastFrame;

    internal Direct2DRendererDiagnostics? Direct2DDiagnostics => _direct2DRenderer?.Diagnostics;

    internal Direct2DSwapChainRendererDiagnostics? SwapChainDiagnostics => _swapChainRenderer?.Diagnostics;

    internal event EventHandler<SpreadsheetSplitChangedEventArgs>? SplitChanged;

    internal event EventHandler<SpreadsheetPaneScrollChangedEventArgs>? PaneScrollChanged;

    internal void SetMode(SpreadsheetSplitPaneMode mode)
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

    internal void SetSplit(double? splitX, double? splitY)
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

    internal void SetActivePane(SpreadsheetPaneId paneId)
    {
        var engine = GetEngine();
        engine.SetActivePane(paneId);
        _lastFrame = null;
        Invalidate();
    }

    internal PointD GetPaneScroll(SpreadsheetPaneId paneId) =>
        _engine?.GetPaneScroll(paneId) ?? default;

    internal ScrollSnapshot GetPaneScrollSnapshot(SpreadsheetPaneId paneId) =>
        _engine?.GetPaneScrollSnapshot(paneId) ?? default;

    internal void ScrollPaneTo(
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

    internal void QueuePaneScroll(SpreadsheetPaneId paneId, ScrollDelta delta)
    {
        GetEngine().QueuePaneScroll(paneId, delta);
        StartFrameLoop();
    }

    internal void QueueActivePaneScroll(ScrollDelta delta)
    {
        GetEngine().QueueActivePaneScroll(delta);
        StartFrameLoop();
    }

    internal bool TryHitTest(
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
        if (hit.Region != SpreadsheetChromeRegion.Body)
        {
            paneId = default;
            address = default;
            return false;
        }

        return GetEngine().TryHitTest(
            hit.BodyX,
            hit.BodyY,
            out paneId,
            out address);
    }
}
