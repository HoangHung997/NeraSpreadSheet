using System.Diagnostics;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;

namespace NeraSpreadSheet.Avalonia;

/// <summary>A read-only snapshot projection, not a second workbook/session/editor.
/// Reuses the shared sparse axes, layout, selection snapshot and display-list composer.</summary>
internal sealed class FormulaReferenceSelectionSurface : Control, IDisposable
{
    private readonly Workbook _workbook;
    private readonly SelectionModel _selection = new();
    private readonly AvaloniaDisplayListRenderer _renderer = new();
    private readonly ContinuousScrollController _scroll = new();
    private readonly Stopwatch _clock = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1d / 60d) };
    private readonly SpreadsheetRenderTheme _theme = new() { ShowHeaders = true };
    private WorksheetSnapshot _snapshot = null!;
    private SparseAxisMetricIndex _rows = null!;
    private SparseAxisMetricIndex _columns = null!;
    private IPointer? _pointer;
    private CellAddress _anchor;
    private Point _point;
    private double _zoom = 1;
    private bool _disposed;

    public FormulaReferenceSelectionSurface(Workbook workbook, Worksheet worksheet)
    {
        _workbook = workbook; Worksheet = worksheet;
        ClipToBounds = true; Focusable = true;
        _timer.Tick += OnFrame; SetWorksheet(worksheet);
    }
    public Worksheet Worksheet { get; private set; }
    public CellRange SelectedRange => _selection.Ranges[0];
    public long RenderCount { get; private set; }
    public ScrollSnapshot ScrollSnapshot => _scroll.Snapshot;
    public double Zoom
    {
        get => _zoom;
        set
        {
            VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
            if (!double.IsFinite(value) || value < 0.25 || value > 4) throw new ArgumentOutOfRangeException(nameof(value), "Zoom must be 25–400%.");
            _zoom = value; InvalidateVisual();
        }
    }
    public event EventHandler? RangeChanged;
    private SpreadsheetChromeMetrics Chrome => SpreadsheetChromeGeometry.Calculate(Math.Max(0, Bounds.Width / _zoom), Math.Max(0, Bounds.Height / _zoom), _theme);

    public void SetWorksheet(Worksheet worksheet)
    {
        VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        EndDrag(); _timer.Stop(); _scroll.Reset();
        Worksheet = worksheet; _snapshot = WorksheetSnapshot.Capture(worksheet);
        _rows = new SparseAxisMetricIndex(SpreadsheetLimits.MaxRows, worksheet.Dimensions.DefaultRowHeight);
        _columns = new SparseAxisMetricIndex(SpreadsheetLimits.MaxColumns, worksheet.Dimensions.DefaultColumnWidth);
        foreach (var (index, value) in worksheet.Dimensions.GetRowOverrides()) _rows.SetSize(index, value);
        foreach (var (index, value) in worksheet.Dimensions.GetColumnOverrides()) _columns.SetSize(index, value);
        _rows.SetHiddenRanges(worksheet.Dimensions.GetHiddenRowRanges().Select(static range => new AxisIndexRange(range.Start, range.End))
            .Concat(_snapshot.GetFilteredOutRowSpans().Select(static range => new AxisIndexRange(range.StartRowIndex, range.EndRowIndex))));
        _columns.SetHiddenRanges(worksheet.Dimensions.GetHiddenColumnRanges().Select(static range => new AxisIndexRange(range.Start, range.End)));
        _renderer.ClearCaches(); SelectRange(new CellRange(default, default)); InvalidateVisual();
    }
    public void SelectRange(CellRange range)
    {
        VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this);
        _selection.Select(range); InvalidateVisual(); RangeChanged?.Invoke(this, EventArgs.Empty);
    }
    public override void Render(DrawingContext context)
    {
        base.Render(context); if (_disposed || Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var chrome = Chrome; if (chrome.BodyWidth <= 0 || chrome.BodyHeight <= 0) return;
        var offset = _scroll.Snapshot;
        var layout = new ViewportLayoutEngine(_rows, _columns).Compute(new ViewportRequest(offset.OffsetX, offset.OffsetY, new SizeD(chrome.BodyWidth, chrome.BodyHeight), 64));
        var selection = _selection.Capture();
        var body = SpreadsheetDisplayListComposer.Compose(_snapshot, layout, selection, _theme, _workbook.Styles, dateSystem: _workbook.DateSystem);
        var list = SpreadsheetChromeDisplayListComposer.Compose(body, layout, selection, _theme);
        using (context.PushClip(new Rect(Bounds.Size)))
        using (context.PushTransform(Matrix.CreateScale(_zoom, _zoom))) _renderer.Render(context, list);
        RenderCount++;
    }
    protected override Size MeasureOverride(Size availableSize) => new(double.IsFinite(availableSize.Width) ? availableSize.Width : 800, double.IsFinite(availableSize.Height) ? availableSize.Height : 420);
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e); if (e.Handled || _disposed) return;
        e.Handled = true; _scroll.QueueDelta(new ScrollDelta(-e.Delta.X * 48 / _zoom, -e.Delta.Y * 48 / _zoom, ScrollInputKind.Precision)); StartFrames();
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e); if (e.Handled || _disposed || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (!TryHit(e.GetPosition(this), out _anchor)) return;
        e.Handled = true; Focus(); _point = e.GetPosition(this); _pointer = e.Pointer; e.Pointer.Capture(this);
        SelectRange(new CellRange(_anchor, _anchor)); StartFrames();
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e); if (_disposed || _pointer != e.Pointer) return;
        e.Handled = true; _point = e.GetPosition(this);
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e); if (_disposed || _pointer != e.Pointer) return;
        e.Handled = true; _point = e.GetPosition(this); AdvanceFrame(TimeSpan.Zero); EndDrag();
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { _pointer = null; base.OnPointerCaptureLost(e); }
    private bool TryHit(Point point, out CellAddress address)
    {
        var chrome = Chrome; address = default;
        var x = point.X / _zoom - chrome.RowHeaderWidth; var y = point.Y / _zoom - chrome.ColumnHeaderHeight;
        if (x < 0 || y < 0 || x >= chrome.BodyWidth || y >= chrome.BodyHeight) return false;
        x += _scroll.Snapshot.OffsetX; y += _scroll.Snapshot.OffsetY;
        if (x >= _columns.TotalExtent || y >= _rows.TotalExtent) return false;
        var column = _columns.FindIndexAtOffset(x); var row = _rows.FindIndexAtOffset(y);
        if (_columns.GetSize(column) <= 0 || _rows.GetSize(row) <= 0) return false;
        address = Worksheet.ResolveMergedAnchor(new CellAddress(row, column)); return true;
    }
    private void StartFrames() { if (_timer.IsEnabled) return; _clock.Restart(); _timer.Start(); }
    private void OnFrame(object? sender, EventArgs e) { var elapsed = _clock.Elapsed; _clock.Restart(); AdvanceFrame(elapsed); }
    internal void AdvanceFrame(TimeSpan elapsed)
    {
        if (_disposed) return;
        var chrome = Chrome; var width = chrome.BodyWidth; var height = chrome.BodyHeight;
        if (_pointer is not null && elapsed > TimeSpan.Zero)
        {
            var x = _point.X - chrome.RowHeaderWidth * _zoom; var y = _point.Y - chrome.ColumnHeaderHeight * _zoom;
            var seconds = Math.Min(0.05, elapsed.TotalSeconds);
            static double Velocity(double position, double extent) => position < 20 ? -600 * Math.Clamp((20 - position) / 20, 0, 1)
                : position > extent - 20 ? 600 * Math.Clamp((position - extent + 20) / 20, 0, 1) : 0;
            _scroll.QueueDelta(new ScrollDelta(Velocity(x, width * _zoom) * seconds / _zoom, Velocity(y, height * _zoom) * seconds / _zoom, ScrollInputKind.Precision));
        }
        var result = _scroll.AdvanceFrame(elapsed, new ScrollBounds(Math.Max(0, _columns.TotalExtent - width), Math.Max(0, _rows.TotalExtent - height)));
        if (_pointer is not null && width > 1 && height > 1)
        {
            var clamped = new Point(Math.Clamp(_point.X, chrome.RowHeaderWidth * _zoom + 0.01, Bounds.Width - 0.01),
                Math.Clamp(_point.Y, chrome.ColumnHeaderHeight * _zoom + 0.01, Bounds.Height - 0.01));
            if (TryHit(clamped, out var cell) && SelectedRange != new CellRange(_anchor, cell)) SelectRange(new CellRange(_anchor, cell));
        }
        if (result.Changed) InvalidateVisual();
        if (_pointer is null && !_scroll.HasPendingMotion) _timer.Stop();
    }
    private void EndDrag() { var pointer = _pointer; _pointer = null; pointer?.Capture(null); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) { EndDrag(); _timer.Stop(); base.OnDetachedFromVisualTree(e); }
    public void Dispose()
    {
        VerifyAccess(); if (_disposed) return; EndDrag(); _disposed = true; _timer.Stop(); _timer.Tick -= OnFrame; _renderer.ClearCaches();
    }
}
