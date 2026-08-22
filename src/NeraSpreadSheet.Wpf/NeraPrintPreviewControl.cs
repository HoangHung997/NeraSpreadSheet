using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// Native WPF viewport for the platform-neutral print-preview session. Only
/// visible and overscan pages are composed and rendered; the control never
/// creates one WPF element per worksheet cell or per document page.
/// </summary>
public sealed class NeraPrintPreviewControl : FrameworkElement
{
    private static readonly SolidColorBrush WorkspaceBrush =
        CreateFrozenBrush(Color.FromRgb(48, 49, 52));
    private static readonly SolidColorBrush PaperBrush =
        CreateFrozenBrush(Colors.White);
    private static readonly SolidColorBrush ShadowBrush =
        CreateFrozenBrush(Color.FromArgb(90, 0, 0, 0));
    private static readonly Pen PaperBorderPen =
        CreateFrozenPen(Color.FromRgb(148, 148, 148), 1d);

    private readonly WpfDisplayListRenderer _renderer = new();
    private SpreadsheetPrintPreviewSession? _session;
    private SpreadsheetPrintPreviewFrame? _lastFrame;
    private Point _lastPanPoint;
    private bool _isPanning;

    public NeraPrintPreviewControl()
    {
        Focusable = true;
        ClipToBounds = true;
        AutomationProperties.SetName(this, "Nera print preview");
        AutomationProperties.SetHelpText(
            this,
            "Use the mouse wheel to scroll, Ctrl+wheel to zoom, and drag to pan.");
    }

    public SpreadsheetPrintPreviewSession? Session
    {
        get => _session;
        set
        {
            if (ReferenceEquals(_session, value))
            {
                return;
            }
            _session = value;
            _lastFrame = null;
            UpdateViewportSize();
            InvalidateVisual();
        }
    }

    public SpreadsheetPrintPreviewFrame? LastFrame => _lastFrame;

    public double Zoom => _session?.Zoom ?? 1d;

    public double OffsetX => _session?.OffsetX ?? 0d;

    public double OffsetY => _session?.OffsetY ?? 0d;

    public void SetZoom(
        double zoom,
        double anchorViewportX = 0d,
        double anchorViewportY = 0d)
    {
        var session = RequireSession();
        session.SetZoom(
            zoom,
            anchorViewportX,
            anchorViewportY);
        InvalidateVisual();
    }

    public void SetColumns(int columns)
    {
        RequireSession().SetColumns(columns);
        InvalidateVisual();
    }

    public void ScrollTo(double offsetXDips, double offsetYDips)
    {
        RequireSession().ScrollTo(offsetXDips, offsetYDips);
        InvalidateVisual();
    }

    public void ScrollBy(double deltaXDips, double deltaYDips)
    {
        RequireSession().ScrollBy(deltaXDips, deltaYDips);
        InvalidateVisual();
    }

    public bool TryHitTestPage(
        double viewportX,
        double viewportY,
        out SpreadsheetPrintPreviewPageSlot page,
        out PointD pagePoint)
    {
        if (_session is null)
        {
            page = default;
            pagePoint = default;
            return false;
        }
        return _session.TryHitTest(
            viewportX,
            viewportY,
            out page,
            out pagePoint);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(
            WorkspaceBrush,
            null,
            new Rect(0d, 0d, ActualWidth, ActualHeight));
        if (_session is null || ActualWidth <= 0d || ActualHeight <= 0d)
        {
            _lastFrame = null;
            return;
        }

        UpdateViewportSize();
        var frame = _session.Compose();
        _lastFrame = frame;
        var pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        foreach (var page in frame.Pages)
        {
            var slot = page.Slot;
            var left = slot.BoundsDips.X - frame.Layout.OffsetXDips;
            var top = slot.BoundsDips.Y - frame.Layout.OffsetYDips;
            var bounds = new Rect(
                left,
                top,
                slot.BoundsDips.Width,
                slot.BoundsDips.Height);
            if (bounds.Right <= 0d || bounds.Bottom <= 0d ||
                bounds.Left >= ActualWidth || bounds.Top >= ActualHeight)
            {
                continue;
            }

            var shadow = new Rect(
                bounds.X + 4d,
                bounds.Y + 5d,
                bounds.Width,
                bounds.Height);
            drawingContext.DrawRectangle(ShadowBrush, null, shadow);
            drawingContext.DrawRectangle(PaperBrush, null, bounds);
            drawingContext.PushClip(new RectangleGeometry(bounds));
            var transform = new MatrixTransform(new Matrix(
                frame.Layout.Zoom,
                0d,
                0d,
                frame.Layout.Zoom,
                bounds.X,
                bounds.Y));
            transform.Freeze();
            drawingContext.PushTransform(transform);
            _renderer.Render(
                drawingContext,
                page.DisplayList,
                pixelsPerDip);
            drawingContext.Pop();
            drawingContext.Pop();
            drawingContext.DrawRectangle(
                null,
                PaperBorderPen,
                bounds);
        }
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateViewportSize();
        InvalidateVisual();
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_session is null)
        {
            return;
        }

        var pointer = e.GetPosition(this);
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            var factor = Math.Pow(1.1d, e.Delta / 120d);
            var zoom = Math.Clamp(
                _session.Zoom * factor,
                0.05d,
                8d);
            _session.SetZoom(zoom, pointer.X, pointer.Y);
        }
        else
        {
            _session.ScrollBy(0d, -e.Delta / 3d);
        }
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_session is null)
        {
            return;
        }
        Focus();
        _isPanning = CaptureMouse();
        _lastPanPoint = e.GetPosition(this);
        if (_isPanning)
        {
            Cursor = Cursors.SizeAll;
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isPanning || _session is null)
        {
            return;
        }
        var current = e.GetPosition(this);
        _session.ScrollBy(
            _lastPanPoint.X - current.X,
            _lastPanPoint.Y - current.Y);
        _lastPanPoint = current;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        EndPan();
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        _isPanning = false;
        Cursor = null;
    }

    private void EndPan()
    {
        if (!_isPanning)
        {
            return;
        }
        _isPanning = false;
        ReleaseMouseCapture();
        Cursor = null;
    }

    private void UpdateViewportSize()
    {
        _session?.SetViewportSize(
            Math.Max(0d, ActualWidth),
            Math.Max(0d, ActualHeight));
    }

    private SpreadsheetPrintPreviewSession RequireSession() =>
        _session ?? throw new InvalidOperationException(
            "Assign a print-preview session before changing the viewport.");

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen CreateFrozenPen(Color color, double width)
    {
        var pen = new Pen(CreateFrozenBrush(color), width);
        pen.Freeze();
        return pen;
    }
}
