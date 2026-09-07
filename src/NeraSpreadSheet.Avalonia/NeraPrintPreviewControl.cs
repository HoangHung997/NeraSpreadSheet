using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Media;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Virtualized multi-page preview backed by the existing Nera print-preview
/// session. Only visible/overscan pages are composed; scrolling never paginates a workbook.</summary>
public sealed class NeraPrintPreviewControl : Control, IDisposable
{
    public static readonly DirectProperty<NeraPrintPreviewControl, SpreadsheetPrintPreviewSession?> SessionProperty =
        AvaloniaProperty.RegisterDirect<NeraPrintPreviewControl, SpreadsheetPrintPreviewSession?>(
            nameof(Session), owner => owner.Session, (owner, value) => owner.Session = value);
    private static readonly IBrush WorkspaceBrush = new SolidColorBrush(Color.FromRgb(48, 49, 52));
    private static readonly IBrush ShadowBrush = new SolidColorBrush(Color.FromArgb(90, 0, 0, 0));
    private static readonly Pen PaperBorder = new(Brushes.Gray, 1);
    private readonly AvaloniaDisplayListRenderer _renderer = new();
    private SpreadsheetPrintPreviewSession? _session;
    private IPointer? _panPointer;
    private Point _lastPan;
    private bool _disposed;

    public NeraPrintPreviewControl()
    {
        Focusable = true;
        ClipToBounds = true;
        AutomationProperties.SetName(this, "Xem trước bản in NeraSpreadSheet");
        AutomationProperties.SetAutomationId(this, "nera-print-preview");
    }

    public SpreadsheetPrintPreviewSession? Session
    {
        get => _session;
        set
        {
            VerifyUsable();
            if (ReferenceEquals(_session, value)) return;
            EndPan();
            SetAndRaise(SessionProperty, ref _session, value);
            LastFrame = null;
            _renderer.ClearCaches();
            RefreshViewport();
        }
    }
    public SpreadsheetPrintPreviewFrame? LastFrame { get; private set; }
    public double Zoom => _session?.Zoom ?? 1;
    public double OffsetX => _session?.OffsetX ?? 0;
    public double OffsetY => _session?.OffsetY ?? 0;
    public event EventHandler? ViewportChanged;

    public void SetZoom(double zoom, double anchorViewportX = 0, double anchorViewportY = 0)
    {
        VerifyUsable();
        RequireSession().SetZoom(zoom, anchorViewportX, anchorViewportY);
        RefreshViewport();
    }
    public void SetColumns(int columns) { VerifyUsable(); RequireSession().SetColumns(columns); RefreshViewport(); }
    public void ScrollTo(double offsetX, double offsetY) { VerifyUsable(); RequireSession().ScrollTo(offsetX, offsetY); RefreshViewport(); }
    public void ScrollBy(double deltaX, double deltaY) { VerifyUsable(); RequireSession().ScrollBy(deltaX, deltaY); RefreshViewport(); }
    public bool TryHitTestPage(double x, double y, out SpreadsheetPrintPreviewPageSlot page, out PointD pagePoint)
    {
        VerifyUsable();
        if (_session is not null) return _session.TryHitTest(x, y, out page, out pagePoint);
        page = default;
        pagePoint = default;
        return false;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_disposed || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        context.DrawRectangle(WorkspaceBrush, null, new Rect(Bounds.Size));
        if (_session is null) { LastFrame = null; return; }
        _session.SetViewportSize(Bounds.Width, Bounds.Height);
        var frame = _session.Compose();
        LastFrame = frame;
        using var viewportClip = context.PushClip(new Rect(Bounds.Size));
        foreach (var page in frame.Pages)
        {
            var slot = page.Slot.BoundsDips;
            var bounds = new Rect(slot.X - frame.Layout.OffsetXDips, slot.Y - frame.Layout.OffsetYDips, slot.Width, slot.Height);
            if (bounds.Right <= 0 || bounds.Bottom <= 0 || bounds.Left >= Bounds.Width || bounds.Top >= Bounds.Height) continue;
            context.DrawRectangle(ShadowBrush, null, new Rect(bounds.X + 4, bounds.Y + 5, bounds.Width, bounds.Height));
            context.DrawRectangle(Brushes.White, null, bounds);
            using (context.PushClip(bounds))
            using (context.PushTransform(Matrix.CreateScale(frame.Layout.Zoom, frame.Layout.Zoom) * Matrix.CreateTranslation(bounds.X, bounds.Y)))
                _renderer.Render(context, page.DisplayList);
            context.DrawRectangle(null, PaperBorder, bounds);
        }
    }
    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsFinite(availableSize.Width) ? availableSize.Width : 800,
        double.IsFinite(availableSize.Height) ? availableSize.Height : 600);
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && !_disposed) RefreshViewport();
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (e.Handled || _disposed || _session is null) return;
        e.Handled = true;
        var point = e.GetPosition(this);
        var primary = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if ((e.KeyModifiers & primary) != 0) SetZoom(Math.Clamp(Zoom * Math.Pow(1.1, e.Delta.Y), 0.05, 8), point.X, point.Y);
        else ScrollBy(-e.Delta.X * 40, -e.Delta.Y * 40);
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.Handled || _disposed || _session is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        e.Handled = true;
        Focus();
        _lastPan = e.GetPosition(this);
        _panPointer = e.Pointer;
        e.Pointer.Capture(this);
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_panPointer != e.Pointer || _disposed || _session is null) return;
        var point = e.GetPosition(this);
        ScrollBy(_lastPan.X - point.X, _lastPan.Y - point.Y);
        _lastPan = point;
        e.Handled = true;
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_panPointer != e.Pointer) return;
        EndPan();
        e.Handled = true;
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { _panPointer = null; base.OnPointerCaptureLost(e); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        EndPan();
        _renderer.ClearCaches();
        base.OnDetachedFromVisualTree(e);
    }
    private void EndPan() { var pointer = _panPointer; _panPointer = null; pointer?.Capture(null); }
    private void RefreshViewport()
    {
        _session?.SetViewportSize(Math.Max(0, Bounds.Width), Math.Max(0, Bounds.Height));
        InvalidateVisual();
        ViewportChanged?.Invoke(this, EventArgs.Empty);
    }
    private SpreadsheetPrintPreviewSession RequireSession() => _session ?? throw new InvalidOperationException("Assign a print-preview session first.");
    private void VerifyUsable() { VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); }
    public void Dispose()
    {
        VerifyAccess();
        if (_disposed) return;
        EndPan();
        _renderer.ClearCaches();
        _session = null;
        LastFrame = null;
        _disposed = true;
    }
}
