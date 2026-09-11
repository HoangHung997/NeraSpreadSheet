using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Avalonia;

public sealed partial class NeraSpreadsheetControl
{
    /// <summary>Fits the current finite selection inside the viewport and aligns its
    /// upper-left cell into view. Returns false when no session/selection is available.</summary>
    public bool ZoomToSelection()
    {
        VerifyUsable();
        if (_session is null || _viewport is null || Bounds.Width <= 0 || Bounds.Height <= 0)
            return false;

        var ranges = _session.Selection.Ranges.ToArray();
        if (ranges.Length == 0) return false;
        var range = new CellRange(
            new CellAddress(ranges.Min(static item => item.Top), ranges.Min(static item => item.Left)),
            new CellAddress(ranges.Max(static item => item.Bottom), ranges.Max(static item => item.Right)));
        if (!_viewport.TryGetCellBounds(range.TopLeft, 0, 0, out var first) ||
            !_viewport.TryGetCellBounds(range.BottomRight, 0, 0, out var last))
            return false;

        var selectionWidth = Math.Max(1d, last.Right - first.Left);
        var selectionHeight = Math.Max(1d, last.Bottom - first.Top);
        var chrome = Chrome;
        var rowHeaderWidth = Math.Max(0d, DocumentWidth - chrome.BodyWidth);
        var columnHeaderHeight = Math.Max(0d, DocumentHeight - chrome.BodyHeight);
        var target = Math.Min(
            Bounds.Width / (selectionWidth + rowHeaderWidth),
            Bounds.Height / (selectionHeight + columnHeaderHeight));
        if (!double.IsFinite(target) || target <= 0d) return false;

        Zoom = Math.Clamp(target, 0.1d, 4d);
        ScrollTo(Math.Max(0d, first.Left), Math.Max(0d, first.Top));
        return true;
    }
}
