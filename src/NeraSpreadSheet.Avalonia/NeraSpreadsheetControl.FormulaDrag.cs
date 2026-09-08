using System.Diagnostics;
using global::Avalonia;
using global::Avalonia.Threading;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Scrolling;

namespace NeraSpreadSheet.Avalonia;

public sealed partial class NeraSpreadsheetControl
{
    private readonly Stopwatch _formulaDragClock = new();
    private DispatcherTimer? _formulaDragTimer;
    private NeraSpreadsheetControl? _formulaDragTarget;
    private Point _formulaDragPosition;
    private CellAddress? _formulaDragLastCell;

    /// <summary>Coalesces raw positions. A frame tick or release flush updates the
    /// provisional span. The source editor and destination viewport stay separate.</summary>
    internal void TrackFormulaPointer(NeraSpreadsheetControl target, Point point)
    {
        if (!IsFormulaPointMode || !ReferenceEquals(_session, target._session)) return;
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y)) return;
        _formulaDragTarget = target;
        _formulaDragPosition = point;
        if (_formulaDragTimer is null)
        {
            _formulaDragLastCell = _pointAnchor;
            _formulaDragTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1d / 60d) };
            _formulaDragTimer.Tick += OnFormulaDragFrame;
            _formulaDragClock.Restart();
            _formulaDragTimer.Start();
        }
    }
    private void OnFormulaDragFrame(object? sender, EventArgs e)
    {
        if (_disposed || _formulaDragTimer is null) return;
        var elapsed = _formulaDragClock.Elapsed;
        _formulaDragClock.Restart();
        RunInput(() =>
        {
            try { AdvanceFormulaPointerFrame(elapsed); }
            catch { StopFormulaPointerTracking(); throw; }
        });
    }
    internal bool AdvanceFormulaPointerFrame(TimeSpan elapsed)
    {
        VerifyUsable();
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        var target = _formulaDragTarget;
        if (!IsFormulaPointMode || target is null || target._disposed || !target._attached ||
            !target.IsEffectivelyVisible || !ReferenceEquals(_session, target._session))
        {
            StopFormulaPointerTracking();
            return false;
        }
        var bounds = target.GetFormulaBodyBounds();
        if (bounds.Width <= 1 || bounds.Height <= 1) return false;
        var seconds = Math.Min(elapsed.TotalSeconds, 0.05);
        if (seconds > 0)
        {
            var frozen = target._viewport!.GetFrozenPaneExtent();
            var x = EdgeVelocity(_formulaDragPosition.X, bounds.Left, bounds.Right, frozen.Width * target._zoom);
            var y = EdgeVelocity(_formulaDragPosition.Y, bounds.Top, bounds.Bottom, frozen.Height * target._zoom);
            if (x != 0 || y != 0)
            {
                target._scroll.QueueDelta(new ScrollDelta(x * seconds / target._zoom, y * seconds / target._zoom, ScrollInputKind.Precision));
                target.AdvanceFrame(elapsed);
            }
        }
        var point = new Point(Math.Clamp(_formulaDragPosition.X, bounds.Left + 0.01, bounds.Right - 0.01),
            Math.Clamp(_formulaDragPosition.Y, bounds.Top + 0.01, bounds.Bottom - 0.01));
        if (target.TryHitFormulaCell(point, out var address) && address != _formulaDragLastCell)
        {
            _formulaDragLastCell = address;
            return UpdatePointReference(address);
        }
        return true;
    }
    internal void StopFormulaPointerTracking()
    {
        var timer = _formulaDragTimer;
        _formulaDragTimer = null;
        if (timer is not null) { timer.Stop(); timer.Tick -= OnFormulaDragFrame; }
        _formulaDragClock.Reset();
        _formulaDragTarget = null;
        _formulaDragLastCell = null;
    }
    private Rect GetFormulaBodyBounds()
    {
        var chrome = Chrome;
        return new Rect(chrome.RowHeaderWidth * _zoom, chrome.ColumnHeaderHeight * _zoom, chrome.BodyWidth * _zoom, chrome.BodyHeight * _zoom);
    }
    private static double EdgeVelocity(double coordinate, double start, double end, double frozen)
    {
        if (end - start <= 1 || frozen >= end - start) return 0;
        var scrollStart = start + Math.Max(0, frozen);
        if (coordinate >= start && coordinate < scrollStart) return 0;
        var band = Math.Min(24d, (end - scrollStart) / 3d);
        if (band <= 0) return 0;
        const double maximumDipsPerSecond = 960d;
        if (coordinate < scrollStart + band) return -maximumDipsPerSecond * Math.Clamp((scrollStart + band - coordinate) / band, 0, 1);
        if (coordinate > end - band) return maximumDipsPerSecond * Math.Clamp((coordinate - end + band) / band, 0, 1);
        return 0;
    }
}
