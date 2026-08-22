using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.WinForms;

/// <summary>
/// Native WinForms viewport for the platform-neutral print-preview session.
/// Only visible and overscan pages are composed; no control is created for
/// every worksheet cell or every document page.
/// </summary>
public sealed class NeraPrintPreviewControl : Control
{
    private static readonly Color WorkspaceColor =
        Color.FromArgb(48, 49, 52);
    private static readonly Color PaperBorderColor =
        Color.FromArgb(148, 148, 148);
    private static readonly Color ShadowColor =
        Color.FromArgb(90, 0, 0, 0);

    private readonly WinFormsDisplayListRenderer _renderer = new();
    private SpreadsheetPrintPreviewSession? _session;
    private SpreadsheetPrintPreviewFrame? _lastFrame;
    private Point _lastPanPoint;
    private bool _isPanning;

    public NeraPrintPreviewControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.Selectable,
            true);
        TabStop = true;
        AccessibleRole = AccessibleRole.Graphic;
        AccessibleName = "Nera print preview";
        AccessibleDescription =
            "Use the mouse wheel to scroll, Ctrl+wheel to zoom, and drag to pan.";
        BackColor = WorkspaceColor;
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
            Invalidate();
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
        RequireSession().SetZoom(
            zoom,
            anchorViewportX,
            anchorViewportY);
        Invalidate();
    }

    public void SetColumns(int columns)
    {
        RequireSession().SetColumns(columns);
        Invalidate();
    }

    public void ScrollTo(double offsetXDips, double offsetYDips)
    {
        RequireSession().ScrollTo(offsetXDips, offsetYDips);
        Invalidate();
    }

    public void ScrollBy(double deltaXDips, double deltaYDips)
    {
        RequireSession().ScrollBy(deltaXDips, deltaYDips);
        Invalidate();
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

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(WorkspaceColor);
        if (_session is null || ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            _lastFrame = null;
            return;
        }

        UpdateViewportSize();
        var frame = _session.Compose();
        _lastFrame = frame;
        using var paperBrush = new SolidBrush(Color.White);
        using var shadowBrush = new SolidBrush(ShadowColor);
        using var borderPen = new Pen(PaperBorderColor, 1f);
        foreach (var page in frame.Pages)
        {
            var slot = page.Slot;
            var left = slot.BoundsDips.X - frame.Layout.OffsetXDips;
            var top = slot.BoundsDips.Y - frame.Layout.OffsetYDips;
            var bounds = new RectangleF(
                (float)left,
                (float)top,
                (float)slot.BoundsDips.Width,
                (float)slot.BoundsDips.Height);
            if (bounds.Right <= 0f || bounds.Bottom <= 0f ||
                bounds.Left >= ClientSize.Width ||
                bounds.Top >= ClientSize.Height)
            {
                continue;
            }

            var shadow = bounds;
            shadow.Offset(4f, 5f);
            e.Graphics.FillRectangle(shadowBrush, shadow);
            e.Graphics.FillRectangle(paperBrush, bounds);
            var state = e.Graphics.Save();
            try
            {
                var printPage = _session.PagePlan.Pages[slot.PageIndex];
                using var transform = new Matrix(
                    (float)frame.Layout.Zoom,
                    0f,
                    0f,
                    (float)frame.Layout.Zoom,
                    bounds.X,
                    bounds.Y);
                e.Graphics.Transform = transform;
                e.Graphics.SetClip(new RectangleF(
                    0f,
                    0f,
                    (float)printPage.PaperSizeDips.Width,
                    (float)printPage.PaperSizeDips.Height));
                _renderer.Render(e.Graphics, page.DisplayList);
            }
            finally
            {
                e.Graphics.Restore(state);
            }
            e.Graphics.DrawRectangle(
                borderPen,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height);
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateViewportSize();
        Invalidate();
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        base.OnMouseWheel(e);
        if (_session is null)
        {
            return;
        }

        if ((ModifierKeys & Keys.Control) != 0)
        {
            var factor = Math.Pow(1.1d, e.Delta / 120d);
            var zoom = Math.Clamp(
                _session.Zoom * factor,
                0.05d,
                8d);
            _session.SetZoom(zoom, e.X, e.Y);
        }
        else
        {
            _session.ScrollBy(0d, -e.Delta / 3d);
        }
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_session is null || e.Button != MouseButtons.Left)
        {
            return;
        }
        Focus();
        _isPanning = true;
        _lastPanPoint = e.Location;
        Capture = true;
        Cursor = Cursors.SizeAll;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_isPanning || _session is null)
        {
            return;
        }
        _session.ScrollBy(
            _lastPanPoint.X - e.X,
            _lastPanPoint.Y - e.Y);
        _lastPanPoint = e.Location;
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.Button == MouseButtons.Left)
        {
            EndPan();
        }
    }

    protected override void OnMouseCaptureChanged(EventArgs e)
    {
        base.OnMouseCaptureChanged(e);
        if (!Capture)
        {
            _isPanning = false;
            Cursor = Cursors.Default;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderer.Dispose();
        }
        base.Dispose(disposing);
    }

    private void EndPan()
    {
        if (!_isPanning)
        {
            return;
        }
        _isPanning = false;
        Capture = false;
        Cursor = Cursors.Default;
    }

    private void UpdateViewportSize()
    {
        _session?.SetViewportSize(
            Math.Max(0d, ClientSize.Width),
            Math.Max(0d, ClientSize.Height));
    }

    private SpreadsheetPrintPreviewSession RequireSession() =>
        _session ?? throw new InvalidOperationException(
            "Assign a print-preview session before changing the viewport.");
}
