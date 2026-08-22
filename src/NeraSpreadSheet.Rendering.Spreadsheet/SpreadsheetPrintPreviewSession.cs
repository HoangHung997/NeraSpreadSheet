using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public sealed record SpreadsheetPrintPreviewPageFrame(
    SpreadsheetPrintPreviewPageSlot Slot,
    DisplayList DisplayList);

public sealed record SpreadsheetPrintPreviewFrame(
    SpreadsheetPrintPreviewLayout Layout,
    IReadOnlyList<SpreadsheetPrintPreviewPageFrame> Pages);

/// <summary>
/// Immutable-document, mutable-viewport print preview session. It keeps
/// continuous fractional offsets and composes only visible/overscan pages.
/// </summary>
public sealed class SpreadsheetPrintPreviewSession
{
    public const int DefaultMaximumCachedPages = 64;

    private readonly WorksheetSnapshot _worksheet;
    private readonly SpreadsheetPageLayoutPlan _plan;
    private readonly CellStyleCatalog? _styles;
    private readonly SpreadsheetPrintDisplayListOptions _displayListOptions;
    private readonly int _maximumCachedPages;
    private readonly Dictionary<int, CacheEntry> _cache = [];
    private long _nextCacheSequence;

    private SizeD _viewportSize;
    private double _offsetX;
    private double _offsetY;
    private SpreadsheetPrintPreviewOptions _previewOptions;

    public SpreadsheetPrintPreviewSession(
        WorksheetSnapshot worksheet,
        SpreadsheetPageLayoutPlan plan,
        CellStyleCatalog? styles = null,
        SpreadsheetPrintDisplayListOptions? displayListOptions = null,
        SpreadsheetPrintPreviewOptions? previewOptions = null,
        int maximumCachedPages = DefaultMaximumCachedPages)
    {
        _worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _styles = styles;
        _displayListOptions = displayListOptions ??
            new SpreadsheetPrintDisplayListOptions();
        _previewOptions = previewOptions ??
            new SpreadsheetPrintPreviewOptions();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCachedPages);
        _maximumCachedPages = maximumCachedPages;
    }

    public SpreadsheetPageLayoutPlan PagePlan => _plan;

    public SizeD ViewportSize => _viewportSize;

    public double OffsetX => _offsetX;

    public double OffsetY => _offsetY;

    public double Zoom => _previewOptions.Zoom;

    public int Columns => _previewOptions.Columns;

    public int CachedPageCount => _cache.Count;

    public void SetViewportSize(double widthDips, double heightDips)
    {
        if (!double.IsFinite(widthDips) || widthDips < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(widthDips));
        }
        if (!double.IsFinite(heightDips) || heightDips < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(heightDips));
        }

        _viewportSize = new SizeD(widthDips, heightDips);
        ClampOffsets();
    }

    public void SetColumns(int columns)
    {
        if (columns <= 0 || columns > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }
        _previewOptions = _previewOptions with
        {
            Columns = columns,
        };
        ClampOffsets();
    }

    public void SetZoom(
        double zoom,
        double anchorViewportX = 0d,
        double anchorViewportY = 0d)
    {
        if (!double.IsFinite(zoom) || zoom < 0.05d || zoom > 8d)
        {
            throw new ArgumentOutOfRangeException(nameof(zoom));
        }
        if (!double.IsFinite(anchorViewportX) ||
            !double.IsFinite(anchorViewportY))
        {
            throw new ArgumentOutOfRangeException(
                nameof(anchorViewportX),
                "Zoom anchors must be finite.");
        }

        var previousZoom = _previewOptions.Zoom;
        var contentAnchorX = _offsetX + anchorViewportX;
        var contentAnchorY = _offsetY + anchorViewportY;
        _previewOptions = _previewOptions with
        {
            Zoom = zoom,
        };
        var ratio = zoom / previousZoom;
        _offsetX = (contentAnchorX * ratio) - anchorViewportX;
        _offsetY = (contentAnchorY * ratio) - anchorViewportY;
        ClampOffsets();
    }

    public void ScrollTo(double offsetXDips, double offsetYDips)
    {
        if (!double.IsFinite(offsetXDips) ||
            !double.IsFinite(offsetYDips))
        {
            throw new ArgumentOutOfRangeException(
                nameof(offsetXDips),
                "Preview offsets must be finite.");
        }
        _offsetX = offsetXDips;
        _offsetY = offsetYDips;
        ClampOffsets();
    }

    public void ScrollBy(double deltaXDips, double deltaYDips)
    {
        if (!double.IsFinite(deltaXDips) ||
            !double.IsFinite(deltaYDips))
        {
            throw new ArgumentOutOfRangeException(
                nameof(deltaXDips),
                "Preview scroll deltas must be finite.");
        }
        ScrollTo(
            _offsetX + deltaXDips,
            _offsetY + deltaYDips);
    }

    public SpreadsheetPrintPreviewFrame Compose()
    {
        var layout = CreateLayout();
        var pages = new SpreadsheetPrintPreviewPageFrame[
            layout.VisiblePages.Count];
        for (var index = 0; index < layout.VisiblePages.Count; index++)
        {
            var slot = layout.VisiblePages[index];
            pages[index] = new SpreadsheetPrintPreviewPageFrame(
                slot,
                GetOrComposePage(slot.PageIndex));
        }
        TrimCache();
        return new SpreadsheetPrintPreviewFrame(layout, pages);
    }

    public bool TryHitTest(
        double viewportX,
        double viewportY,
        out SpreadsheetPrintPreviewPageSlot page,
        out PointD pagePoint) =>
        CreateLayout().TryHitTest(
            viewportX,
            viewportY,
            out page,
            out pagePoint);

    public void ClearPageCache() => _cache.Clear();

    private DisplayList GetOrComposePage(int pageIndex)
    {
        if (_cache.TryGetValue(pageIndex, out var cached))
        {
            _cache[pageIndex] = cached with
            {
                Sequence = checked(++_nextCacheSequence),
            };
            return cached.DisplayList;
        }

        var composed = SpreadsheetPrintDisplayListComposer.Compose(
            _worksheet,
            _plan,
            pageIndex,
            _styles,
            _displayListOptions);
        _cache[pageIndex] = new CacheEntry(
            composed.DisplayList,
            checked(++_nextCacheSequence));
        return composed.DisplayList;
    }

    private SpreadsheetPrintPreviewLayout CreateLayout() =>
        SpreadsheetPrintPreviewLayoutEngine.Create(
            _plan,
            _viewportSize,
            _offsetX,
            _offsetY,
            _previewOptions);

    private void ClampOffsets()
    {
        var layout = SpreadsheetPrintPreviewLayoutEngine.Create(
            _plan,
            _viewportSize,
            0d,
            0d,
            _previewOptions);
        _offsetX = Math.Clamp(
            _offsetX,
            0d,
            Math.Max(0d, layout.ContentSizeDips.Width -
                          _viewportSize.Width));
        _offsetY = Math.Clamp(
            _offsetY,
            0d,
            Math.Max(0d, layout.ContentSizeDips.Height -
                          _viewportSize.Height));
    }

    private void TrimCache()
    {
        if (_cache.Count <= _maximumCachedPages)
        {
            return;
        }

        foreach (var pageIndex in _cache
                     .OrderBy(static pair => pair.Value.Sequence)
                     .Take(_cache.Count - _maximumCachedPages)
                     .Select(static pair => pair.Key)
                     .ToArray())
        {
            _cache.Remove(pageIndex);
        }
    }

    private sealed record CacheEntry(
        DisplayList DisplayList,
        long Sequence);
}
