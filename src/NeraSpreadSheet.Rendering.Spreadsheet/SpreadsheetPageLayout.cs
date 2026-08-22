using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public sealed record SpreadsheetPrintPage(
    int PageNumber,
    int RowPageIndex,
    int ColumnPageIndex,
    CellRange DataRange,
    CellRange? RepeatedRows,
    CellRange? RepeatedColumns,
    double Scale,
    SizeD PaperSizeDips,
    RectD PrintableBoundsDips,
    SizeD UnscaledContentSizeDips,
    PointD ContentOffsetDips);

public sealed record SpreadsheetPageLayoutPlan(
    CellRange PrintArea,
    SpreadsheetPageSetup Setup,
    double EffectiveScale,
    SizeD PaperSizeDips,
    RectD PrintableBoundsDips,
    IReadOnlyList<SpreadsheetPrintPage> Pages)
{
    public int HorizontalPageCount =>
        Pages.Count == 0
            ? 0
            : Pages.Max(static page => page.ColumnPageIndex) + 1;

    public int VerticalPageCount =>
        Pages.Count == 0
            ? 0
            : Pages.Max(static page => page.RowPageIndex) + 1;
}

public static class SpreadsheetPageLayoutPlanner
{
    public const double DipsPerInch = 96d;
    public const int MaximumPages = 100_000;

    public static SpreadsheetPageLayoutPlan CreatePlan(
        WorksheetSnapshot worksheet,
        CellRange printArea,
        SpreadsheetPageSetup? setup = null)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        setup ??= new SpreadsheetPageSetup();
        ValidateSetup(setup, printArea);
        ValidateManualBreaksAgainstMerges(
            worksheet,
            setup,
            printArea);

        var paper = GetOrientedPaperSize(setup);
        var paperDips = new SizeD(
            paper.WidthInches * DipsPerInch,
            paper.HeightInches * DipsPerInch);
        var margins = setup.Margins;
        var printableLeft = margins.LeftInches * DipsPerInch;
        var printableTop =
            (margins.TopInches + margins.HeaderInches) * DipsPerInch;
        var printableWidth = paperDips.Width -
            ((margins.LeftInches + margins.RightInches) * DipsPerInch);
        var printableHeight = paperDips.Height -
            ((margins.TopInches + margins.BottomInches +
              margins.HeaderInches + margins.FooterInches) * DipsPerInch);
        if (printableWidth <= 0d || printableHeight <= 0d)
        {
            throw new InvalidOperationException(
                "The selected paper and margins leave no printable area.");
        }

        var printableBounds = new RectD(
            printableLeft,
            printableTop,
            printableWidth,
            printableHeight);
        var repeatedRows = NormalizeRepeatedRows(
            setup.RepeatTitles.Rows,
            printArea);
        var repeatedColumns = NormalizeRepeatedColumns(
            setup.RepeatTitles.Columns,
            printArea);
        var repeatHeight = repeatedRows is { } rowTitles
            ? SumRows(worksheet, rowTitles.Top, rowTitles.Bottom)
            : 0d;
        var repeatWidth = repeatedColumns is { } columnTitles
            ? SumColumns(worksheet, columnTitles.Left, columnTitles.Right)
            : 0d;
        var dataTop = repeatedRows is { } rows
            ? checked(rows.Bottom + 1)
            : printArea.Top;
        var dataLeft = repeatedColumns is { } columns
            ? checked(columns.Right + 1)
            : printArea.Left;
        if (dataTop > printArea.Bottom || dataLeft > printArea.Right)
        {
            throw new InvalidOperationException(
                "Repeated title rows or columns consume the complete print area.");
        }

        var dataHeight = SumRows(worksheet, dataTop, printArea.Bottom);
        var dataWidth = SumColumns(worksheet, dataLeft, printArea.Right);
        var scale = CalculateEffectiveScale(
            setup,
            printableWidth,
            printableHeight,
            repeatWidth,
            repeatHeight,
            dataWidth,
            dataHeight);
        var horizontalCapacity = (printableWidth / scale) - repeatWidth;
        var verticalCapacity = (printableHeight / scale) - repeatHeight;
        if (horizontalCapacity <= 0d || verticalCapacity <= 0d)
        {
            throw new InvalidOperationException(
                "Repeated titles leave no room for printable worksheet data.");
        }

        var columnSegments = PartitionColumns(
            worksheet,
            dataLeft,
            printArea.Right,
            horizontalCapacity,
            setup.ManualColumnBreaks,
            printArea);
        var rowSegments = PartitionRows(
            worksheet,
            dataTop,
            printArea.Bottom,
            verticalCapacity,
            setup.ManualRowBreaks,
            printArea);
        if ((long)columnSegments.Count * rowSegments.Count > MaximumPages)
        {
            throw new InvalidOperationException(
                $"The print plan exceeds the page limit of {MaximumPages:N0}.");
        }

        var pages = new List<SpreadsheetPrintPage>(
            checked(columnSegments.Count * rowSegments.Count));
        var pageNumber = 1;
        for (var rowPageIndex = 0;
             rowPageIndex < rowSegments.Count;
             rowPageIndex++)
        {
            var rowSegment = rowSegments[rowPageIndex];
            for (var columnPageIndex = 0;
                 columnPageIndex < columnSegments.Count;
                 columnPageIndex++)
            {
                var columnSegment = columnSegments[columnPageIndex];
                var dataRange = new CellRange(
                    new CellAddress(rowSegment.Start, columnSegment.Start),
                    new CellAddress(rowSegment.End, columnSegment.End));
                var pageContentWidth = repeatWidth +
                    SumColumns(
                        worksheet,
                        columnSegment.Start,
                        columnSegment.End);
                var pageContentHeight = repeatHeight +
                    SumRows(
                        worksheet,
                        rowSegment.Start,
                        rowSegment.End);
                var scaledContent = new SizeD(
                    pageContentWidth * scale,
                    pageContentHeight * scale);
                var offsetX = setup.CenterHorizontally
                    ? Math.Max(
                        0d,
                        (printableWidth - scaledContent.Width) / 2d)
                    : 0d;
                var offsetY = setup.CenterVertically
                    ? Math.Max(
                        0d,
                        (printableHeight - scaledContent.Height) / 2d)
                    : 0d;
                pages.Add(new SpreadsheetPrintPage(
                    pageNumber++,
                    rowPageIndex,
                    columnPageIndex,
                    dataRange,
                    repeatedRows,
                    repeatedColumns,
                    scale,
                    paperDips,
                    printableBounds,
                    new SizeD(pageContentWidth, pageContentHeight),
                    new PointD(offsetX, offsetY)));
            }
        }

        return new SpreadsheetPageLayoutPlan(
            printArea,
            setup.Copy(),
            scale,
            paperDips,
            printableBounds,
            pages);
    }

    private static void ValidateSetup(
        SpreadsheetPageSetup setup,
        CellRange printArea)
    {
        if (!Enum.IsDefined(setup.Orientation))
        {
            throw new ArgumentOutOfRangeException(
                nameof(setup),
                "Page orientation is not defined.");
        }
        if (!double.IsFinite(setup.ScalePercent) ||
            setup.ScalePercent <= 0d ||
            setup.ScalePercent > 400d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(setup),
                "ScalePercent must be finite and between 0 and 400.");
        }
        ValidateFit(setup.FitToPagesWide);
        ValidateFit(setup.FitToPagesTall);
        ValidateBreaks(
            setup.ManualRowBreaks,
            printArea.Top,
            printArea.Bottom);
        ValidateBreaks(
            setup.ManualColumnBreaks,
            printArea.Left,
            printArea.Right);
    }

    private static void ValidateManualBreaksAgainstMerges(
        WorksheetSnapshot worksheet,
        SpreadsheetPageSetup setup,
        CellRange printArea)
    {
        foreach (var merge in worksheet.MergedCells)
        {
            if (!merge.Intersects(printArea))
            {
                continue;
            }
            if (setup.ManualRowBreaks.Any(value =>
                    merge.Top < value && merge.Bottom >= value))
            {
                throw new InvalidOperationException(
                    "A manual row page break cannot split a merged cell.");
            }
            if (setup.ManualColumnBreaks.Any(value =>
                    merge.Left < value && merge.Right >= value))
            {
                throw new InvalidOperationException(
                    "A manual column page break cannot split a merged cell.");
            }
        }
    }

    private static void ValidateFit(int? value)
    {
        if (value is <= 0 or > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                "Fit-to-page values must be between 1 and 1000.");
        }
    }

    private static void ValidateBreaks(
        IReadOnlyList<int> breaks,
        int areaStart,
        int areaEnd)
    {
        ArgumentNullException.ThrowIfNull(breaks);
        var seen = new HashSet<int>();
        foreach (var value in breaks)
        {
            if (value <= areaStart || value > areaEnd || !seen.Add(value))
            {
                throw new ArgumentException(
                    "Manual page breaks must be unique and inside the print area.",
                    nameof(breaks));
            }
        }
    }

    private static SpreadsheetPaperSize GetOrientedPaperSize(
        SpreadsheetPageSetup setup) =>
        setup.Orientation == SpreadsheetPageOrientation.Portrait
            ? setup.PaperSize
            : new SpreadsheetPaperSize(
                setup.PaperSize.HeightInches,
                setup.PaperSize.WidthInches,
                setup.PaperSize.Name);

    private static CellRange? NormalizeRepeatedRows(
        CellRange? repeated,
        CellRange printArea)
    {
        if (repeated is null)
        {
            return null;
        }
        var range = repeated.Value;
        if (range.Top != printArea.Top ||
            range.Bottom >= printArea.Bottom ||
            range.Left > printArea.Left ||
            range.Right < printArea.Right)
        {
            throw new ArgumentException(
                "Repeated rows must begin at the print-area top and span its columns.",
                nameof(repeated));
        }
        return new CellRange(
            new CellAddress(range.Top, printArea.Left),
            new CellAddress(range.Bottom, printArea.Right));
    }

    private static CellRange? NormalizeRepeatedColumns(
        CellRange? repeated,
        CellRange printArea)
    {
        if (repeated is null)
        {
            return null;
        }
        var range = repeated.Value;
        if (range.Left != printArea.Left ||
            range.Right >= printArea.Right ||
            range.Top > printArea.Top ||
            range.Bottom < printArea.Bottom)
        {
            throw new ArgumentException(
                "Repeated columns must begin at the print-area left and span its rows.",
                nameof(repeated));
        }
        return new CellRange(
            new CellAddress(printArea.Top, range.Left),
            new CellAddress(printArea.Bottom, range.Right));
    }

    private static double CalculateEffectiveScale(
        SpreadsheetPageSetup setup,
        double printableWidth,
        double printableHeight,
        double repeatWidth,
        double repeatHeight,
        double dataWidth,
        double dataHeight)
    {
        var scale = setup.ScalePercent / 100d;
        if (setup.FitToPagesWide is { } pagesWide)
        {
            var denominator = dataWidth + (pagesWide * repeatWidth);
            if (denominator > 0d)
            {
                scale = Math.Min(
                    scale,
                    (pagesWide * printableWidth) / denominator);
            }
        }
        if (setup.FitToPagesTall is { } pagesTall)
        {
            var denominator = dataHeight + (pagesTall * repeatHeight);
            if (denominator > 0d)
            {
                scale = Math.Min(
                    scale,
                    (pagesTall * printableHeight) / denominator);
            }
        }
        if (!double.IsFinite(scale) || scale <= 0d)
        {
            throw new InvalidOperationException(
                "The page setup produced an invalid print scale.");
        }
        return scale;
    }

    private static List<AxisSegment> PartitionRows(
        WorksheetSnapshot worksheet,
        int start,
        int end,
        double capacity,
        IReadOnlyList<int> manualBreaks,
        CellRange printArea) =>
        PartitionAxis(
            start,
            end,
            capacity,
            manualBreaks,
            index => GetRowSize(worksheet, index),
            (segmentStart, next) => AdjustRowBreakForMerges(
                worksheet,
                printArea,
                segmentStart,
                next));

    private static List<AxisSegment> PartitionColumns(
        WorksheetSnapshot worksheet,
        int start,
        int end,
        double capacity,
        IReadOnlyList<int> manualBreaks,
        CellRange printArea) =>
        PartitionAxis(
            start,
            end,
            capacity,
            manualBreaks,
            index => GetColumnSize(worksheet, index),
            (segmentStart, next) => AdjustColumnBreakForMerges(
                worksheet,
                printArea,
                segmentStart,
                next));

    private static List<AxisSegment> PartitionAxis(
        int start,
        int end,
        double capacity,
        IReadOnlyList<int> manualBreaks,
        Func<int, double> getSize,
        Func<int, int, int> adjustForMerges)
    {
        var orderedBreaks = manualBreaks
            .Where(value => value > start && value <= end)
            .OrderBy(static value => value)
            .ToArray();
        var result = new List<AxisSegment>();
        var current = start;
        while (current <= end)
        {
            var nextManualBreak = orderedBreaks.FirstOrDefault(
                value => value > current);
            var limit = nextManualBreak == 0
                ? end + 1
                : nextManualBreak;
            var next = current;
            var consumed = 0d;
            while (next < limit)
            {
                var size = getSize(next);
                if (next > current && consumed + size > capacity)
                {
                    break;
                }
                consumed += size;
                next++;
                if (consumed >= capacity)
                {
                    break;
                }
            }
            if (next == current)
            {
                next++;
            }
            next = Math.Min(
                end + 1,
                adjustForMerges(current, next));
            if (next > limit && nextManualBreak != 0)
            {
                throw new InvalidOperationException(
                    "A manual page break cannot split a merged cell.");
            }
            if (next <= current)
            {
                next = current + 1;
            }
            result.Add(new AxisSegment(current, next - 1));
            current = next;
        }
        return result;
    }

    private static int AdjustRowBreakForMerges(
        WorksheetSnapshot worksheet,
        CellRange printArea,
        int segmentStart,
        int next)
    {
        foreach (var merge in worksheet.MergedCells)
        {
            if (!merge.Intersects(printArea) ||
                merge.Top >= next ||
                merge.Bottom < next)
            {
                continue;
            }
            return merge.Top > segmentStart
                ? merge.Top
                : checked(merge.Bottom + 1);
        }
        return next;
    }

    private static int AdjustColumnBreakForMerges(
        WorksheetSnapshot worksheet,
        CellRange printArea,
        int segmentStart,
        int next)
    {
        foreach (var merge in worksheet.MergedCells)
        {
            if (!merge.Intersects(printArea) ||
                merge.Left >= next ||
                merge.Right < next)
            {
                continue;
            }
            return merge.Left > segmentStart
                ? merge.Left
                : checked(merge.Right + 1);
        }
        return next;
    }

    private static double SumRows(
        WorksheetSnapshot worksheet,
        int start,
        int end)
    {
        var result = 0d;
        for (var index = start; index <= end; index++)
        {
            result += GetRowSize(worksheet, index);
        }
        return result;
    }

    private static double SumColumns(
        WorksheetSnapshot worksheet,
        int start,
        int end)
    {
        var result = 0d;
        for (var index = start; index <= end; index++)
        {
            result += GetColumnSize(worksheet, index);
        }
        return result;
    }

    private static double GetRowSize(
        WorksheetSnapshot worksheet,
        int index) =>
        worksheet.RowHeights.TryGetValue(index, out var size)
            ? size
            : worksheet.DefaultRowHeight;

    private static double GetColumnSize(
        WorksheetSnapshot worksheet,
        int index) =>
        worksheet.ColumnWidths.TryGetValue(index, out var size)
            ? size
            : worksheet.DefaultColumnWidth;

    private readonly record struct AxisSegment(int Start, int End);
}
