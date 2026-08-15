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

    public SpreadsheetViewportEngine(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public SpreadsheetSession Session => _session;

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
        var displayList = SpreadsheetDisplayListComposer.Compose(
            WorksheetSnapshot.Capture(worksheet),
            layout,
            selection,
            theme);

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

        address = new CellAddress(rowIndex, columnIndex);
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
