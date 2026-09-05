using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public sealed record SpreadsheetPrintPreviewOptions
{
    public double Zoom { get; init; } = 1d;

    public double PageGapDips { get; init; } = 24d;

    public int Columns { get; init; } = 1;

    public double OverscanDips { get; init; } = 200d;
}

public readonly record struct SpreadsheetPrintPreviewPageSlot(
    int PageIndex,
    int PageNumber,
    int RowIndex,
    int ColumnIndex,
    RectD BoundsDips);

public sealed record SpreadsheetPrintPreviewLayout(
    SizeD ViewportSizeDips,
    SizeD ContentSizeDips,
    double OffsetXDips,
    double OffsetYDips,
    double Zoom,
    int Columns,
    IReadOnlyList<SpreadsheetPrintPreviewPageSlot> VisiblePages)
{
    public bool TryHitTest(
        double viewportX,
        double viewportY,
        out SpreadsheetPrintPreviewPageSlot page,
        out PointD pagePoint)
    {
        if (!double.IsFinite(viewportX) ||
            !double.IsFinite(viewportY))
        {
            page = default;
            pagePoint = default;
            return false;
        }

        var contentPoint = new PointD(
            viewportX + OffsetXDips,
            viewportY + OffsetYDips);
        foreach (var candidate in VisiblePages)
        {
            if (!candidate.BoundsDips.Contains(contentPoint))
            {
                continue;
            }

            page = candidate;
            pagePoint = new PointD(
                contentPoint.X - candidate.BoundsDips.X,
                contentPoint.Y - candidate.BoundsDips.Y);
            return true;
        }

        page = default;
        pagePoint = default;
        return false;
    }
}

public static class SpreadsheetPrintPreviewLayoutEngine
{
    public static SpreadsheetPrintPreviewLayout Create(
        SpreadsheetPageLayoutPlan plan,
        SizeD viewportSizeDips,
        double offsetXDips,
        double offsetYDips,
        SpreadsheetPrintPreviewOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        options ??= new SpreadsheetPrintPreviewOptions();
        Validate(viewportSizeDips, offsetXDips, offsetYDips, options);

        var pageCount = plan.Pages.Count;
        var effectiveColumns = pageCount == 0
            ? 0
            : Math.Min(options.Columns, pageCount);
        var pageWidth = plan.PaperSizeDips.Width * options.Zoom;
        var pageHeight = plan.PaperSizeDips.Height * options.Zoom;
        var strideX = pageWidth + options.PageGapDips;
        var strideY = pageHeight + options.PageGapDips;
        var rowCount = pageCount == 0
            ? 0
            : checked((pageCount + effectiveColumns - 1) / effectiveColumns);
        var contentWidth = pageCount == 0
            ? 0d
            : options.PageGapDips + (effectiveColumns * strideX);
        var contentHeight = rowCount == 0
            ? 0d
            : options.PageGapDips + (rowCount * strideY);
        var contentSize = new SizeD(contentWidth, contentHeight);
        var visiblePages = pageCount == 0
            ? Array.Empty<SpreadsheetPrintPreviewPageSlot>()
            : GetVisiblePages(
                plan,
                viewportSizeDips,
                offsetXDips,
                offsetYDips,
                options,
                effectiveColumns,
                pageWidth,
                pageHeight,
                strideX,
                strideY,
                rowCount);

        return new SpreadsheetPrintPreviewLayout(
            viewportSizeDips,
            contentSize,
            offsetXDips,
            offsetYDips,
            options.Zoom,
            effectiveColumns,
            visiblePages);
    }

    private static SpreadsheetPrintPreviewPageSlot[] GetVisiblePages(
        SpreadsheetPageLayoutPlan plan,
        SizeD viewportSize,
        double offsetX,
        double offsetY,
        SpreadsheetPrintPreviewOptions options,
        int columns,
        double pageWidth,
        double pageHeight,
        double strideX,
        double strideY,
        int rowCount)
    {
        var visibleLeft = Math.Max(
            0d,
            offsetX - options.OverscanDips);
        var visibleTop = Math.Max(
            0d,
            offsetY - options.OverscanDips);
        var visibleRight = offsetX + viewportSize.Width +
                           options.OverscanDips;
        var visibleBottom = offsetY + viewportSize.Height +
                            options.OverscanDips;
        var firstRow = Math.Clamp(
            (int)Math.Floor(
                (visibleTop - options.PageGapDips) / strideY),
            0,
            Math.Max(0, rowCount - 1));
        var lastRow = Math.Clamp(
            (int)Math.Floor(
                (visibleBottom - options.PageGapDips) / strideY),
            0,
            Math.Max(0, rowCount - 1));
        var firstColumn = Math.Clamp(
            (int)Math.Floor(
                (visibleLeft - options.PageGapDips) / strideX),
            0,
            columns - 1);
        var lastColumn = Math.Clamp(
            (int)Math.Floor(
                (visibleRight - options.PageGapDips) / strideX),
            0,
            columns - 1);
        var result = new List<SpreadsheetPrintPreviewPageSlot>();
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = firstColumn;
                 column <= lastColumn;
                 column++)
            {
                var pageIndex = checked((row * columns) + column);
                if (pageIndex >= plan.Pages.Count)
                {
                    break;
                }
                var bounds = new RectD(
                    options.PageGapDips + (column * strideX),
                    options.PageGapDips + (row * strideY),
                    pageWidth,
                    pageHeight);
                if (bounds.Right < visibleLeft ||
                    bounds.Left > visibleRight ||
                    bounds.Bottom < visibleTop ||
                    bounds.Top > visibleBottom)
                {
                    continue;
                }
                result.Add(new SpreadsheetPrintPreviewPageSlot(
                    pageIndex,
                    plan.Pages[pageIndex].PageNumber,
                    row,
                    column,
                    bounds));
            }
        }
        return result.ToArray();
    }

    private static void Validate(
        SizeD viewportSize,
        double offsetX,
        double offsetY,
        SpreadsheetPrintPreviewOptions options)
    {
        if (!double.IsFinite(viewportSize.Width) ||
            viewportSize.Width < 0d ||
            !double.IsFinite(viewportSize.Height) ||
            viewportSize.Height < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportSize));
        }
        if (!double.IsFinite(offsetX) || offsetX < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(offsetX));
        }
        if (!double.IsFinite(offsetY) || offsetY < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(offsetY));
        }
        if (!double.IsFinite(options.Zoom) ||
            options.Zoom < 0.05d ||
            options.Zoom > 8d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Zoom must be between 0.05 and 8.");
        }
        if (!double.IsFinite(options.PageGapDips) ||
            options.PageGapDips < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "PageGapDips must be finite and nonnegative.");
        }
        if (options.Columns <= 0 || options.Columns > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Columns must be between 1 and 100.");
        }
        if (!double.IsFinite(options.OverscanDips) ||
            options.OverscanDips < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "OverscanDips must be finite and nonnegative.");
        }
    }
}
