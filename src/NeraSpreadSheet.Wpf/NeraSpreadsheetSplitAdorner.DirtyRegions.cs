using System.Windows;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

internal sealed partial class NeraSpreadsheetSplitAdorner
{
    private long PartialDirtyRenderCount { get; set; }

    private long FullDirtyRenderCount { get; set; }

    private int LastDirtyRegionCount => LastDirtyBounds.Count;

    private IReadOnlyList<RectD> LastDirtyBounds { get; set; } =
        Array.Empty<RectD>();

    private IReadOnlyList<Int32Rect> LastPresentedDirtyRectangles =>
        _gpuSurface.LastPresentedDirtyRectangles;

    private void HandleCellsChanged(CellsChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (_disposed)
        {
            return;
        }

        var engine = _engine;
        if (_activeBackend != WpfRenderingBackend.Direct2DD3DImage ||
            engine is null ||
            _lastFrame is null)
        {
            InvalidateFullDirtyFrame();
            return;
        }

        var projection = engine.ProjectDirtyRange(e.Range);
        if (projection.RequiresFullInvalidation)
        {
            InvalidateFullDirtyFrame();
            return;
        }
        if (projection.Regions.Length == 0)
        {
            LastDirtyBounds = Array.Empty<RectD>();
            _lastFrame = null;
            return;
        }

        _lastFrame = null;
        var displayList = ComposeDisplayList();
        if (displayList is null)
        {
            InvalidateFullDirtyFrame();
            return;
        }

        var chrome = GetChromeMetrics();
        var dirtyBounds = new RectD[projection.Regions.Length];
        for (var index = 0; index < projection.Regions.Length; index++)
        {
            dirtyBounds[index] = projection.Regions[index].Bounds.Translate(
                chrome.RowHeaderWidth,
                chrome.ColumnHeaderHeight);
        }

        LastDirtyBounds = dirtyBounds;
        PartialDirtyRenderCount++;
        _gpuSurface.SetDisplayList(displayList, dirtyBounds);
    }

    private void InvalidateFullDirtyFrame()
    {
        FullDirtyRenderCount++;
        LastDirtyBounds = Array.Empty<RectD>();
        _lastFrame = null;
        InvalidateVisual();
    }
}
