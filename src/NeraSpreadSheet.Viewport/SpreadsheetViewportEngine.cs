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
    /// <summary>Default blank rows retained by adaptive navigation.</summary>
    public const int DefaultAdaptiveTrailingRowCount = 100;

    /// <summary>Default blank columns retained by adaptive navigation.</summary>
    public const int DefaultAdaptiveTrailingColumnCount = 20;

    private readonly SpreadsheetSession _session;
    private readonly SpreadsheetViewportCacheOptions _cacheOptions;
    private readonly Dictionary<DisplayListCacheKey, CachedDisplayListEntry> _displayListCache = [];
    private SparseAxisMetricIndex? _rows;
    private SparseAxisMetricIndex? _columns;
    private Worksheet? _metricsWorksheet;
    private long _dimensionsVersion = -1;
    private long _rowVisibilityVersion = -1;
    private WorksheetSnapshot? _worksheetSnapshot;
    private Worksheet? _snapshotWorksheet;
    private long _snapshotWorksheetVersion = -1;
    private long _snapshotDimensionsVersion = -1;
    private long _cacheClock;
    private Worksheet? _navigationExtentWorksheet;
    private long _navigationExtentWorksheetVersion = -1;
    private long _navigationExtentDimensionsVersion = -1;
    private CellAddress _navigationUsedBottomRight;

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
        var cellDisplayList = _cacheOptions.Enabled
            ? ComposeCachedDisplayList(layoutEngine, layout, worksheet, selection, theme, overscan)
            : ComposeFreshDisplayList(worksheet, layout, selection, theme);
        var displayList = ComposeAnalyticsOverlay(
            worksheet,
            layout,
            cellDisplayList);

        return new SpreadsheetViewportFrame(layout, displayList, worksheet.Version, selection.Version);
    }

    public IReadOnlyList<SpreadsheetAnalyticsInteractionTarget>
        GetAnalyticsInteractionTargets(ViewportLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var placements = SpreadsheetAnalyticsInteractionProjection.ApplyPreview(
            _session.AnalyticsPlacements.GetPlacements(_session.ActiveWorksheet),
            _session.AnalyticsInteraction.Snapshot);
        return SpreadsheetAnalyticsInteractionTargetMapper.Map(
            placements,
            layout);
    }

    public bool TryHitTest(double viewportX, double viewportY, double scrollX, double scrollY, out CellAddress address)
    {
        if (!TryHitTestColumn(viewportX, scrollX, out var columnIndex) ||
            !TryHitTestRow(viewportY, scrollY, out var rowIndex))
        {
            address = default;
            return false;
        }

        address = _session.ActiveWorksheet.ResolveMergedAnchor(new CellAddress(rowIndex, columnIndex));
        return true;
    }

    public bool TryHitTestRow(double viewportY, double scrollY, out int rowIndex)
    {
        if (!double.IsFinite(viewportY) || !double.IsFinite(scrollY) || viewportY < 0d || scrollY < 0d)
        {
            rowIndex = default;
            return false;
        }

        EnsureMetrics();
        return TryHitTestAxis(_rows!, viewportY, scrollY, _session.View.FrozenRows, out rowIndex);
    }

    public bool TryHitTestColumn(double viewportX, double scrollX, out int columnIndex)
    {
        if (!double.IsFinite(viewportX) || !double.IsFinite(scrollX) || viewportX < 0d || scrollX < 0d)
        {
            columnIndex = default;
            return false;
        }

        EnsureMetrics();
        return TryHitTestAxis(_columns!, viewportX, scrollX, _session.View.FrozenColumns, out columnIndex);
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

    /// <summary>
    /// Gets a compact scroll extent containing the sparse used range, the
    /// current navigation cell and at least the visible viewport. Physical
    /// worksheet limits are not changed.
    /// </summary>
    public SizeD GetAdaptiveNavigationExtent(
        CellAddress navigationCell,
        SizeD minimumViewportExtent) =>
        GetAdaptiveNavigationExtent(
            navigationCell,
            minimumViewportExtent,
            default,
            DefaultAdaptiveTrailingRowCount,
            DefaultAdaptiveTrailingColumnCount);

    /// <summary>
    /// Gets a compact scroll extent containing the sparse used range, the
    /// current navigation cell, a configurable trailing workspace and the
    /// current viewport. Physical worksheet limits are not changed.
    /// </summary>
    /// <param name="navigationCell">The cell that keyboard navigation must reach.</param>
    /// <param name="minimumViewportExtent">The visible worksheet body size.</param>
    /// <param name="currentScrollOffset">
    /// The current continuous scroll offset. The returned extent retains the
    /// current viewport without adding another trailing workspace after it.
    /// </param>
    /// <param name="trailingRowCount">Minimum blank rows retained after used or navigated content.</param>
    /// <param name="trailingColumnCount">Minimum blank columns retained after used or navigated content.</param>
    public SizeD GetAdaptiveNavigationExtent(
        CellAddress navigationCell,
        SizeD minimumViewportExtent,
        PointD currentScrollOffset,
        int trailingRowCount = DefaultAdaptiveTrailingRowCount,
        int trailingColumnCount = DefaultAdaptiveTrailingColumnCount)
    {
        if (!double.IsFinite(minimumViewportExtent.Width) ||
            minimumViewportExtent.Width < 0d ||
            !double.IsFinite(minimumViewportExtent.Height) ||
            minimumViewportExtent.Height < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minimumViewportExtent),
                "Viewport extents must be finite and non-negative.");
        }
        if (!double.IsFinite(currentScrollOffset.X) ||
            currentScrollOffset.X < 0d ||
            !double.IsFinite(currentScrollOffset.Y) ||
            currentScrollOffset.Y < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(currentScrollOffset),
                "Scroll offsets must be finite and non-negative.");
        }
        ArgumentOutOfRangeException.ThrowIfNegative(trailingRowCount);
        ArgumentOutOfRangeException.ThrowIfNegative(trailingColumnCount);

        EnsureMetrics();
        var used = GetNavigationUsedBottomRight();
        var lastRow = Math.Max(used.RowIndex, navigationCell.RowIndex);
        var lastColumn = Math.Max(
            used.ColumnIndex,
            navigationCell.ColumnIndex);

        var lastRowExclusive = lastRow + 1;
        var lastColumnExclusive = lastColumn + 1;
        var trailingRowExclusive = Math.Min(
            SpreadsheetLimits.MaxRows,
            lastRowExclusive + trailingRowCount);
        var trailingColumnExclusive = Math.Min(
            SpreadsheetLimits.MaxColumns,
            lastColumnExclusive + trailingColumnCount);
        var rowBoundary = _rows!.GetOffset(lastRowExclusive);
        var columnBoundary = _columns!.GetOffset(lastColumnExclusive);
        var rowTail = Math.Max(
            minimumViewportExtent.Height,
            _rows.GetOffset(trailingRowExclusive) - rowBoundary);
        var columnTail = Math.Max(
            minimumViewportExtent.Width,
            _columns.GetOffset(trailingColumnExclusive) - columnBoundary);

        return new SizeD(
            Math.Min(
                _columns.TotalExtent,
                Math.Max(
                    columnBoundary + columnTail,
                    currentScrollOffset.X + minimumViewportExtent.Width)),
            Math.Min(
                _rows.TotalExtent,
                Math.Max(
                    rowBoundary + rowTail,
                    currentScrollOffset.Y + minimumViewportExtent.Height)));
    }

    public void InvalidateMetrics()
    {
        _metricsWorksheet = null;
        _dimensionsVersion = -1;
        _rowVisibilityVersion = -1;
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

    private CellAddress GetNavigationUsedBottomRight()
    {
        var worksheet = _session.ActiveWorksheet;
        if (ReferenceEquals(_navigationExtentWorksheet, worksheet) &&
            _navigationExtentWorksheetVersion == worksheet.Version &&
            _navigationExtentDimensionsVersion == worksheet.Dimensions.Version)
        {
            return _navigationUsedBottomRight;
        }

        var bottom = 0;
        var right = 0;
        foreach (var (address, _) in worksheet.EnumerateUsedCells())
        {
            bottom = Math.Max(bottom, address.RowIndex);
            right = Math.Max(right, address.ColumnIndex);
        }
        foreach (var range in worksheet.MergedCells.Ranges)
        {
            bottom = Math.Max(bottom, range.Bottom);
            right = Math.Max(right, range.Right);
        }
        foreach (var table in worksheet.Tables)
        {
            bottom = Math.Max(bottom, table.Range.Bottom);
            right = Math.Max(right, table.Range.Right);
        }
        foreach (var rowIndex in worksheet.Dimensions.GetRowOverrides().Keys)
        {
            bottom = Math.Max(bottom, rowIndex);
        }
        foreach (var columnIndex in worksheet.Dimensions.GetColumnOverrides().Keys)
        {
            right = Math.Max(right, columnIndex);
        }

        _navigationExtentWorksheet = worksheet;
        _navigationExtentWorksheetVersion = worksheet.Version;
        _navigationExtentDimensionsVersion = worksheet.Dimensions.Version;
        _navigationUsedBottomRight = new CellAddress(bottom, right);
        return _navigationUsedBottomRight;
    }

    public void ClearDisplayListCache() => _displayListCache.Clear();

    private DisplayList ComposeAnalyticsOverlay(
        Worksheet worksheet,
        ViewportLayout layout,
        DisplayList cellDisplayList)
    {
        var placements = _session.AnalyticsPlacements.GetPlacements(worksheet);
        if (placements.Count == 0)
        {
            return cellDisplayList;
        }

        var projectedPlacements = SpreadsheetAnalyticsInteractionProjection.ApplyPreview(
            placements,
            _session.AnalyticsInteraction.Snapshot);
        var overlay = SpreadsheetAnalyticsOverlayDisplayListComposer.Compose(
            worksheet,
            _session.Analytics.GetCharts(worksheet),
            _session.Analytics.GetPivots(worksheet),
            projectedPlacements,
            layout,
            _session.AnalyticsInteraction.SelectedItem);
        if (overlay.Commands.Count == 0)
        {
            return cellDisplayList;
        }

        var builder = new DisplayListBuilder();
        builder.Append(cellDisplayList);
        builder.Append(overlay);
        return builder.Build();
    }

    private static bool TryHitTestAxis(
        SparseAxisMetricIndex axis,
        double viewportCoordinate,
        double scrollOffset,
        int frozenCount,
        out int index)
    {
        var frozenExtent = axis.GetOffset(frozenCount);
        var documentCoordinate = viewportCoordinate < frozenExtent
            ? viewportCoordinate
            : viewportCoordinate + scrollOffset;
        if (documentCoordinate >= axis.TotalExtent)
        {
            index = default;
            return false;
        }

        var candidate = axis.FindIndexAtOffset(documentCoordinate);
        if (axis.GetSize(candidate) <= 0d)
        {
            index = default;
            return false;
        }

        index = candidate;
        return true;
    }

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
        var frozenRows = _session.View.FrozenRows;
        var frozenColumns = _session.View.FrozenColumns;
        var key = new DisplayListCacheKey(
            worksheet,
            worksheet.Version,
            worksheet.Dimensions.Version,
            selection.Version,
            frozenRows,
            frozenColumns,
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
                overscan,
                frozenRows,
                frozenColumns));
            cachedDisplayList = SpreadsheetDisplayListComposer.Compose(
                GetWorksheetSnapshot(worksheet),
                cachedLayout,
                selection,
                theme,
                _session.Workbook.Styles,
                includeFreezeSeparators: false);
            AddDisplayListCacheEntry(key, cachedDisplayList);
        }

        return actualLayout.HasFrozenPanes
            ? ReprojectFrozenCachedDisplayList(cachedDisplayList, actualLayout, cacheScrollX, cacheScrollY, theme)
            : ReprojectCachedDisplayList(cachedDisplayList, actualLayout, cacheScrollX, cacheScrollY);
    }

    private static DisplayList ReprojectCachedDisplayList(
        DisplayList cachedDisplayList,
        ViewportLayout actualLayout,
        double cacheScrollX,
        double cacheScrollY)
    {
        var builder = new DisplayListBuilder();
        builder.PushClip(new RectD(0d, 0d, actualLayout.ViewportSize.Width, actualLayout.ViewportSize.Height));
        builder.PushTranslation(cacheScrollX - actualLayout.ScrollX, cacheScrollY - actualLayout.ScrollY);
        builder.Append(cachedDisplayList);
        builder.PopTranslation();
        builder.PopClip();
        return builder.Build();
    }

    private static DisplayList ReprojectFrozenCachedDisplayList(
        DisplayList cachedDisplayList,
        ViewportLayout actualLayout,
        double cacheScrollX,
        double cacheScrollY,
        SpreadsheetRenderTheme theme)
    {
        var width = actualLayout.ViewportSize.Width;
        var height = actualLayout.ViewportSize.Height;
        var frozenWidth = Math.Clamp(actualLayout.FrozenWidth, 0d, width);
        var frozenHeight = Math.Clamp(actualLayout.FrozenHeight, 0d, height);
        var deltaX = cacheScrollX - actualLayout.ScrollX;
        var deltaY = cacheScrollY - actualLayout.ScrollY;
        var builder = new DisplayListBuilder();
        builder.PushClip(new RectD(0d, 0d, width, height));

        AppendCachedPane(builder, cachedDisplayList, new RectD(0d, 0d, frozenWidth, frozenHeight), 0d, 0d);
        AppendCachedPane(
            builder,
            cachedDisplayList,
            new RectD(frozenWidth, 0d, Math.Max(0d, width - frozenWidth), frozenHeight),
            deltaX,
            0d);
        AppendCachedPane(
            builder,
            cachedDisplayList,
            new RectD(0d, frozenHeight, frozenWidth, Math.Max(0d, height - frozenHeight)),
            0d,
            deltaY);
        AppendCachedPane(
            builder,
            cachedDisplayList,
            new RectD(
                frozenWidth,
                frozenHeight,
                Math.Max(0d, width - frozenWidth),
                Math.Max(0d, height - frozenHeight)),
            deltaX,
            deltaY);

        SpreadsheetDisplayListComposer.AppendFreezeSeparators(builder, actualLayout, theme);
        builder.PopClip();
        return builder.Build();
    }

    private static void AppendCachedPane(
        DisplayListBuilder builder,
        DisplayList cachedDisplayList,
        RectD clip,
        double deltaX,
        double deltaY)
    {
        if (clip.Width <= 0d || clip.Height <= 0d)
        {
            return;
        }

        builder.PushClip(clip);
        if (deltaX != 0d || deltaY != 0d)
        {
            builder.PushTranslation(deltaX, deltaY);
        }
        builder.Append(cachedDisplayList);
        if (deltaX != 0d || deltaY != 0d)
        {
            builder.PopTranslation();
        }
        builder.PopClip();
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
        var hasActiveFilters = worksheet.Tables.Any(static table =>
            table.AutoFilter is { Columns.Count: > 0 } &&
            table.DataRange is not null);
        var rowVisibilityVersion = hasActiveFilters
            ? worksheet.Version
            : 0L;
        if (ReferenceEquals(_metricsWorksheet, worksheet) &&
            _dimensionsVersion == worksheet.Dimensions.Version &&
            _rowVisibilityVersion == rowVisibilityVersion &&
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

        var hiddenRows = worksheet.Dimensions
            .GetHiddenRowRanges()
            .Select(static range => new AxisIndexRange(
                range.Start,
                range.End));
        if (hasActiveFilters)
        {
            var snapshot = GetWorksheetSnapshot(worksheet);
            hiddenRows = hiddenRows.Concat(
                snapshot.GetFilteredOutRowSpans()
                    .Select(static span => new AxisIndexRange(
                        span.StartRowIndex,
                        span.EndRowIndex)));
        }
        rows.SetHiddenRanges(hiddenRows);

        var columns = new SparseAxisMetricIndex(SpreadsheetLimits.MaxColumns, worksheet.Dimensions.DefaultColumnWidth);
        foreach (var (index, size) in worksheet.Dimensions.GetColumnOverrides())
        {
            columns.SetSize(index, size);
        }
        columns.SetHiddenRanges(
            worksheet.Dimensions
                .GetHiddenColumnRanges()
                .Select(static range => new AxisIndexRange(
                    range.Start,
                    range.End)));

        _rows = rows;
        _columns = columns;
        _metricsWorksheet = worksheet;
        _dimensionsVersion = worksheet.Dimensions.Version;
        _rowVisibilityVersion = rowVisibilityVersion;
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
        int FrozenRows,
        int FrozenColumns,
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
