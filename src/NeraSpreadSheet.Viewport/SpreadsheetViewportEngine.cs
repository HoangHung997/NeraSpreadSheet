using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Viewport;

public sealed class SpreadsheetViewportEngine
{
    private readonly SpreadsheetSession _session;
    private SparseAxisMetricIndex? _rows;
    private SparseAxisMetricIndex? _columns;
    private Worksheet? _metricsWorksheet;
    private long _dimensionsVersion = -1;
    private WorksheetSnapshot? _worksheetSnapshot;
    private Worksheet? _snapshotWorksheet;
    private long _snapshotWorksheetVersion = -1;
    private long _snapshotDimensionsVersion = -1;

    public SpreadsheetViewportEngine(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public SpreadsheetSession Session => _session;

    public long SnapshotRefreshCount { get; private set; }

    public SpreadsheetViewportFrame Compose(
        double scrollX,
        double scrollY,
        double viewportWidth,
        double viewportHeight,
        double overscan = 128d,
        SpreadsheetRenderTheme? theme = null)
    {
        EnsureMetrics();
        var layout = new ViewportLayoutEngine(_rows!, _columns!).Compute(
            new ViewportRequest(scrollX, scrollY, new SizeD(viewportWidth, viewportHeight), overscan));
        var worksheet = _session.ActiveWorksheet;
        var selection = _session.Selection.Capture();
        var worksheetSnapshot = GetWorksheetSnapshot(worksheet);
        var displayList = SpreadsheetDisplayListComposer.Compose(
            worksheetSnapshot,
            layout,
            selection,
            theme,
            _session.Workbook.Styles);

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
        var documentX = viewportX + scrollX;
        var documentY = viewportY + scrollY;
        if (documentX >= _columns!.TotalExtent || documentY >= _rows!.TotalExtent)
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
            var left = _columns!.GetOffset(mergedRange.Left);
            var right = _columns.GetOffset(mergedRange.Right + 1);
            var top = _rows!.GetOffset(mergedRange.Top);
            var bottom = _rows.GetOffset(mergedRange.Bottom + 1);
            var width = right - left;
            var height = bottom - top;
            if (width <= 0d || height <= 0d)
            {
                bounds = RectD.Empty;
                return false;
            }

            bounds = new RectD(left - scrollX, top - scrollY, width, height);
            return true;
        }

        var cellWidth = _columns!.GetSize(address.ColumnIndex);
        var cellHeight = _rows!.GetSize(address.RowIndex);
        if (cellWidth <= 0d || cellHeight <= 0d)
        {
            bounds = RectD.Empty;
            return false;
        }

        bounds = new RectD(
            _columns.GetOffset(address.ColumnIndex) - scrollX,
            _rows.GetOffset(address.RowIndex) - scrollY,
            cellWidth,
            cellHeight);
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
    }
}
