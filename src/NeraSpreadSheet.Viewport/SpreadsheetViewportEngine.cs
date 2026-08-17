using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Viewport;

public sealed class SpreadsheetViewportEngine
{
    private readonly SpreadsheetSession _session;
    private readonly SpreadsheetViewportCacheOptions _cacheOptions;
    private readonly Dictionary<DisplayListCacheKey, CachedDisplayListEntry> _displayListCache = [];
    private SparseAxisMetricIndex? _rows;
    private SparseAxisMetricIndex? _columns;
    private Worksheet? _metricsWorksheet;
    private long _dimensionsVersion = -1;
    private WorksheetSnapshot? _worksheetSnapshot;
    private Worksheet? _snapshotWorksheet;
    private long _snapshotWorksheetVersion = -1;
    private long _snapshotDimensionsVersion = -1;
    private long _cacheClock;

    public SpreadsheetViewportEngine(
        SpreadsheetSession session,
        SpreadsheetViewportCacheOptions? cacheOptions = null)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _cacheOptions = cacheOptions ?? new SpreadsheetViewportCacheOptions();
        ValidateCacheOptions(_cacheOptions);
    }

    public SpreadsheetSession Session => _session;
    public long SnapshotRefreshCount { get; private set; }
    public long DisplayListCacheHitCount { get; private set; }
    public long DisplayListCacheMissCount { get; private set; }
    public int DisplayListCacheEntryCount => _displayListCache.Count;

    public SpreadsheetViewportFrame Compose(
        double scrollX,
        double scrollY,
        double viewportWidth,
        double viewportHeight,
        double overscan = 128d,
        SpreadsheetRenderTheme? theme = null)
    {
        EnsureMetrics();
        theme ??= new SpreadsheetRenderTheme();
        var layoutEngine = new ViewportLayoutEngine(_rows!, _columns!);
        var layout = layoutEngine.Compute(new ViewportRequest(
            scrollX,
            scrollY,
            new SizeD(viewportWidth, viewportHeight),
            overscan,
            _session.View.FrozenRows,
            _session.View.FrozenColumns));
        var worksheet = _session.ActiveWorksheet;
        var selection = _session.Selection.Capture();
        var canUseTranslatedTileCache = _cacheOptions.Enabled && !_session.View.HasFrozenPanes;
        var displayList = canUseTranslatedTileCache
            ? ComposeCachedDisplayList(layoutEngine, layout, worksheet, selection, theme, overscan)
            : ComposeFreshDisplayList(worksheet, layout, selection, theme);

        return new SpreadsheetViewportFrame(layout, displayList, worksheet.Version, selection.Version);
    }

    public bool TryHitTest(double viewportX, double viewportY, double scrollX, double scrollY, out CellAddress address)
    {
        if (!double.IsFinite(viewportX) || !double.IsFinite(viewportY) ||
            !double.IsFinite(scrollX) || !double.IsFinite(scrollY) ||
            viewportX < 0d || viewportY < 0d || scrollX < 0d || scrollY < 0d)
        {
            address = default;
            return false;
        }

        EnsureMetrics();
        var frozenWidth = _columns!.GetOffset(_session.View.FrozenColumns);
        var frozenHeight = _rows!.GetOffset(_session.View.FrozenRows);
        var documentX = viewportX < frozenWidth ? viewportX : viewportX + scrollX;
        var documentY = viewportY < frozenHeight ? viewportY : viewportY + scrollY;
        if (documentX >= _columns.TotalExtent || documentY >= _rows.TotalExtent)
        {
            address = default;
            return false;
        }

        var columnIndex = _columns.FindIndexAtOffset(documentX);
        var rowIndex = _rows.FindIndexAtOffset(documentY);
        if (_columns.GetSize(columnIndex) <= 0d || _rows.GetSize(rowIndex) <= 0d)
        {
            address = default;
            return false;
        }

        address = _session.ActiveWorksheet.ResolveMergedAnchor(new CellAddress(rowIndex, columnIndex));
        return true;
    }

    public bool TryGetCellBounds(CellAddress address, double scrollX, double scrollY, out RectD bounds)
    {
        if (!double.IsFinite(scrollX) || !double.IsFinite(scrollY) || scrollX < 0d || scrollY < 0d)
        {
            bounds = RectD.Empty;
            return false;
        }

        EnsureMetrics();
        var worksheet = _session.ActiveWorksheet;
        if (worksheet.MergedCells.TryGetContaining(address, out var mergedRange))
        {
            return TryGetRangeViewportBounds(mergedRange, scrollX, scrollY, out bounds);
        }

        var width = _columns!.GetSize(address.ColumnIndex);
        var height = _rows!.GetSize(address.RowIndex);
        if (width <= 0d || height <= 0d)
        {
            bounds = RectD.Empty;
            return false;
        }

        bounds = new RectD(
            GetViewportCoordinate(_columns.GetOffset(address.ColumnIndex), address.ColumnIndex, _session.View.FrozenColumns, scrollX),
            GetViewportCoordinate(_rows.GetOffset(address.RowIndex), address.RowIndex, _session.View.FrozenRows, scrollY),
            width,
            height);
        return true;
    }

    public SizeD GetContentExtent()
    {
        EnsureMetrics();
        return new SizeD(_columns!.TotalExtent, _rows!.TotalExtent);
    }

    public void InvalidateMetrics()
    {
        _metricsWorksheet = null;
        _dimensionsVersion = -1;
        _rows = null;
        _columns = null;
        InvalidateSnapshot();
    }

    public void InvalidateSnapshot()
    {
        _worksheetSnapshot = null;
        _snapshotWorksheet = null;
        _snapshotWorksheetVersion = -1;
        _snapshotDimensionsVersion = -1;
        ClearDisplayListCache();
    }

    public void ClearDisplayListCache() => _displayListCache.Clear();

    private bool TryGetRangeViewportBounds(CellRange range, double scrollX, double scrollY, out RectD bounds)
    {
        var left = _columns!.GetOffset(range.Left);
        var right = _columns.GetOffset(range.Right + 1);
        var top = _rows!.GetOffset(range.Top);
        var bottom = _rows.GetOffset(range.Bottom + 1);
        var width = right - left;
        var height = bottom - top;
        if (width <= 0d || height <= 0d)
        {
            bounds = RectD.Empty;
            return false;
        }

        var frozenRows = _session.View.FrozenRows;
        var frozenColumns = _session.View.FrozenColumns;
        if ((range.Top < frozenRows && range.Bottom >= frozenRows) ||
            (range.Left < frozenColumns && range.Right >= frozenColumns))
        {
            bounds = RectD.Empty;
            return false;
        }

        bounds = new RectD(
            GetViewportCoordinate(left, range.Left, frozenColumns, scrollX),
            GetViewportCoordinate(top, range.Top, frozenRows, scrollY),
            width,
            height);
        return true;
    }

    private static double GetViewportCoordinate(double absolute, int index, int frozenCount, double scrollOffset) =>
        index < frozenCount ? absolute : absolute - scrollOffset;

    private DisplayList ComposeFreshDisplayList(
        Worksheet worksheet,
        ViewportLayout layout,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme) =>
        SpreadsheetDisplayListComposer.Compose(
            GetWorksheetSnapshot(worksheet),
            layout,
            selection,
            theme,
            _session.Workbook.Styles);

    private DisplayList ComposeCachedDisplayList(
        ViewportLayoutEngine layoutEngine,
        ViewportLayout actualLayout,
        Worksheet worksheet,
        SelectionSnapshot selection,
        SpreadsheetRenderTheme theme,
        double overscan)
    {
        var tileSize = _cacheOptions.ScrollTileSize;
        var expandedWidth = actualLayout.ViewportSize.Width + tileSize;
        var expandedHeight = actualLayout.ViewportSize.Height + tileSize;
        var requestedTileX = Math.Floor(actualLayout.ScrollX / tileSize) * tileSize;
        var requestedTileY = Math.Floor(actualLayout.ScrollY / tileSize) * tileSize;
        var maximumCacheX = Math.Max(0d, _columns!.TotalExtent - expandedWidth);
        var maximumCacheY = Math.Max(0d, _rows!.TotalExtent - expandedHeight);
        var cacheScrollX = Math.Min(requestedTileX, maximumCacheX);
        var cacheScrollY = Math.Min(requestedTileY, maximumCacheY);
        var key = new DisplayListCacheKey(
            worksheet,
            worksheet.Version,
            worksheet.Dimensions.Version,
            selection.Version,
            cacheScrollX,
            cacheScrollY,
            actualLayout.ViewportSize.Width,
            actualLayout.ViewportSize.Height,
            overscan,
            theme);

        DisplayList cachedDisplayList;
        if (_displayListCache.TryGetValue(key, out var entry))
        {
            DisplayListCacheHitCount++;
            entry.LastAccess = ++_cacheClock;
            cachedDisplayList = entry.DisplayList;
        }
        else
        {
            DisplayListCacheMissCount++;
            var cachedLayout = layoutEngine.Compute(new ViewportRequest(
                cacheScrollX,
                cacheScrollY,
                new SizeD(expandedWidth, expandedHeight),
                overscan));
            cachedDisplayList = SpreadsheetDisplayListComposer.Compose(
                GetWorksheetSnapshot(worksheet),
                cachedLayout,
                selection,
                theme,
                _session.Workbook.Styles);
            AddDisplayListCacheEntry(key, cachedDisplayList);
        }

        var builder = new DisplayListBuilder();
        builder.PushClip(new RectD(0d, 0d, actualLayout.ViewportSize.Width, actualLayout.ViewportSize.Height));
        builder.PushTranslation(cacheScrollX - actualLayout.ScrollX, cacheScrollY - actualLayout.ScrollY);
        builder.Append(cachedDisplayList);
        builder.PopTranslation();
        builder.PopClip();
        return builder.Build();
    }

    private void AddDisplayListCacheEntry(DisplayListCacheKey key, DisplayList displayList)
    {
        if (_displayListCache.Count >= _cacheOptions.MaxEntries)
        {
            DisplayListCacheKey? leastRecentlyUsedKey = null;
            var leastRecentlyUsed = long.MaxValue;
            foreach (var pair in _displayListCache)
            {
                if (pair.Value.LastAccess >= leastRecentlyUsed)
                {
                    continue;
                }
                leastRecentlyUsed = pair.Value.LastAccess;
                leastRecentlyUsedKey = pair.Key;
            }

            if (leastRecentlyUsedKey is { } keyToRemove)
            {
                _displayListCache.Remove(keyToRemove);
            }
        }

        _displayListCache[key] = new CachedDisplayListEntry(displayList, ++_cacheClock);
    }

    private WorksheetSnapshot GetWorksheetSnapshot(Worksheet worksheet)
    {
        if (_worksheetSnapshot is not null &&
            ReferenceEquals(_snapshotWorksheet, worksheet) &&
            _snapshotWorksheetVersion == worksheet.Version &&
            _snapshotDimensionsVersion == worksheet.Dimensions.Version)
        {
            return _worksheetSnapshot;
        }

        _worksheetSnapshot = WorksheetSnapshot.Capture(worksheet);
        _snapshotWorksheet = worksheet;
        _snapshotWorksheetVersion = worksheet.Version;
        _snapshotDimensionsVersion = worksheet.Dimensions.Version;
        SnapshotRefreshCount++;
        return _worksheetSnapshot;
    }

    private void EnsureMetrics()
    {
        var worksheet = _session.ActiveWorksheet;
        if (ReferenceEquals(_metricsWorksheet, worksheet) &&
            _dimensionsVersion == worksheet.Dimensions.Version &&
            _rows is not null &&
            _columns is not null)
        {
            return;
        }

        var rows = new SparseAxisMetricIndex(SpreadsheetLimits.MaxRows, worksheet.Dimensions.DefaultRowHeight);
        foreach (var (index, size) in worksheet.Dimensions.GetRowOverrides())
        {
            rows.SetSize(index, size);
        }

        var columns = new SparseAxisMetricIndex(SpreadsheetLimits.MaxColumns, worksheet.Dimensions.DefaultColumnWidth);
        foreach (var (index, size) in worksheet.Dimensions.GetColumnOverrides())
        {
            columns.SetSize(index, size);
        }

        _rows = rows;
        _columns = columns;
        _metricsWorksheet = worksheet;
        _dimensionsVersion = worksheet.Dimensions.Version;
        ClearDisplayListCache();
    }

    private static void ValidateCacheOptions(SpreadsheetViewportCacheOptions options)
    {
        if (!double.IsFinite(options.ScrollTileSize) || options.ScrollTileSize <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "ScrollTileSize must be finite and positive.");
        }
        if (options.MaxEntries <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxEntries must be positive.");
        }
    }

    private readonly record struct DisplayListCacheKey(
        Worksheet Worksheet,
        long WorksheetVersion,
        long DimensionsVersion,
        long SelectionVersion,
        double CacheScrollX,
        double CacheScrollY,
        double ViewportWidth,
        double ViewportHeight,
        double Overscan,
        SpreadsheetRenderTheme Theme);

    private sealed class CachedDisplayListEntry
    {
        public CachedDisplayListEntry(DisplayList displayList, long lastAccess)
        {
            DisplayList = displayList;
            LastAccess = lastAccess;
        }

        public DisplayList DisplayList { get; }
        public long LastAccess { get; set; }
    }
}
