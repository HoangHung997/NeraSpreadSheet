using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

internal sealed partial class NeraSpreadsheetSplitSurface : Control
{
    private const double SplitDirtyRegionPadding = 3d;

    internal long PartialDirtyInvalidationCount { get; private set; }

    internal long FullDirtyInvalidationCount { get; private set; }

    internal int LastDirtyRegionCount { get; private set; }

    private void InvalidateDirtyRange(CellRange range)
    {
        if (_engine is null ||
            _lastFrame is null ||
            ClientSize.Width <= 0 ||
            ClientSize.Height <= 0 ||
            _owner.RenderingBackend == WinFormsRenderingBackend.Direct2DSwapChain)
        {
            InvalidateDirtyRangeFully();
            return;
        }

        var projection = _engine.ProjectDirtyRange(range);
        _engine.InvalidateSnapshot();
        _lastFrame = null;
        LastDirtyRegionCount = projection.Regions.Length;
        if (projection.RequiresFullInvalidation)
        {
            FullDirtyInvalidationCount++;
            Invalidate();
            return;
        }
        if (projection.Regions.Length == 0)
        {
            return;
        }

        PartialDirtyInvalidationCount++;
        var chrome = GetChromeMetrics();
        foreach (var region in projection.Regions)
        {
            var invalidRectangle = ToClientInvalidationRectangle(
                region.Bounds,
                chrome);
            if (!invalidRectangle.IsEmpty)
            {
                Invalidate(invalidRectangle);
            }
        }
    }

    private void InvalidateDirtyRangeFully()
    {
        _engine?.InvalidateSnapshot();
        _lastFrame = null;
        LastDirtyRegionCount = 0;
        FullDirtyInvalidationCount++;
        Invalidate();
    }

    private Rectangle ToClientInvalidationRectangle(
        RectD bodyBounds,
        SpreadsheetChromeMetrics chrome)
    {
        var left = Math.Max(
            0d,
            chrome.RowHeaderWidth + bodyBounds.Left - SplitDirtyRegionPadding);
        var top = Math.Max(
            0d,
            chrome.ColumnHeaderHeight + bodyBounds.Top - SplitDirtyRegionPadding);
        var right = Math.Min(
            ClientSize.Width,
            chrome.RowHeaderWidth + bodyBounds.Right + SplitDirtyRegionPadding);
        var bottom = Math.Min(
            ClientSize.Height,
            chrome.ColumnHeaderHeight + bodyBounds.Bottom + SplitDirtyRegionPadding);
        return right <= left || bottom <= top
            ? Rectangle.Empty
            : Rectangle.FromLTRB(
                (int)Math.Floor(left),
                (int)Math.Floor(top),
                (int)Math.Ceiling(right),
                (int)Math.Ceiling(bottom));
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
        builder.DrawDisplayList(displayList);
        builder.PopClip();
        return builder.Build();
    }
}
