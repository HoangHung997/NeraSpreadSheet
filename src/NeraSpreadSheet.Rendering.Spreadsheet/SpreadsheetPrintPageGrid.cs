using System.Globalization;
using System.Text;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public readonly record struct SpreadsheetPrintAxisSlot(
    int WorksheetIndex,
    double StartDips,
    double SizeDips,
    bool IsRepeated)
{
    public double EndDips => StartDips + SizeDips;
}

public sealed class SpreadsheetPrintPageGrid
{
    private readonly Dictionary<int, SpreadsheetPrintAxisSlot> _rows;
    private readonly Dictionary<int, SpreadsheetPrintAxisSlot> _columns;

    internal SpreadsheetPrintPageGrid(
        SpreadsheetPrintPage page,
        IReadOnlyList<SpreadsheetPrintAxisSlot> rows,
        IReadOnlyList<SpreadsheetPrintAxisSlot> columns)
    {
        Page = page ?? throw new ArgumentNullException(nameof(page));
        Rows = rows ?? throw new ArgumentNullException(nameof(rows));
        Columns = columns ?? throw new ArgumentNullException(nameof(columns));
        _rows = rows.ToDictionary(static slot => slot.WorksheetIndex);
        _columns = columns.ToDictionary(static slot => slot.WorksheetIndex);
    }

    public SpreadsheetPrintPage Page { get; }

    public IReadOnlyList<SpreadsheetPrintAxisSlot> Rows { get; }

    public IReadOnlyList<SpreadsheetPrintAxisSlot> Columns { get; }

    public bool TryGetCellBounds(
        CellAddress address,
        out RectD bounds)
    {
        if (!_rows.TryGetValue(address.RowIndex, out var row) ||
            !_columns.TryGetValue(address.ColumnIndex, out var column))
        {
            bounds = default;
            return false;
        }

        bounds = new RectD(
            column.StartDips,
            row.StartDips,
            column.SizeDips,
            row.SizeDips);
        return true;
    }
}

public static class SpreadsheetPrintPageGridBuilder
{
    public const int MaximumAxisSlotsPerPage = 2_000_000;

    public static SpreadsheetPrintPageGrid Create(
        WorksheetSnapshot worksheet,
        SpreadsheetPrintPage page)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(page);

        var rowIndexes = BuildIndexes(
            page.RepeatedRows?.Top,
            page.RepeatedRows?.Bottom,
            page.DataRange.Top,
            page.DataRange.Bottom);
        var columnIndexes = BuildIndexes(
            page.RepeatedColumns?.Left,
            page.RepeatedColumns?.Right,
            page.DataRange.Left,
            page.DataRange.Right);
        if ((long)rowIndexes.Count + columnIndexes.Count >
            MaximumAxisSlotsPerPage)
        {
            throw new InvalidOperationException(
                $"The print page exceeds the axis-slot limit of " +
                $"{MaximumAxisSlotsPerPage:N0}.");
        }

        var originX = page.PrintableBoundsDips.X +
                      page.ContentOffsetDips.X;
        var originY = page.PrintableBoundsDips.Y +
                      page.ContentOffsetDips.Y;
        var rows = BuildSlots(
            rowIndexes,
            originY,
            page.Scale,
            index => page.RepeatedRows is { } repeated &&
                     index >= repeated.Top &&
                     index <= repeated.Bottom,
            index => worksheet.IsRowHidden(index)
                ? 0d
                : worksheet.RowHeights.TryGetValue(index, out var size)
                    ? size
                    : worksheet.DefaultRowHeight);
        var columns = BuildSlots(
            columnIndexes,
            originX,
            page.Scale,
            index => page.RepeatedColumns is { } repeated &&
                     index >= repeated.Left &&
                     index <= repeated.Right,
            index => worksheet.IsColumnHidden(index)
                ? 0d
                : worksheet.ColumnWidths.TryGetValue(index, out var size)
                    ? size
                    : worksheet.DefaultColumnWidth);
        return new SpreadsheetPrintPageGrid(page, rows, columns);
    }

    private static List<int> BuildIndexes(
        int? repeatedStart,
        int? repeatedEnd,
        int dataStart,
        int dataEnd)
    {
        var result = new List<int>();
        if (repeatedStart is { } start && repeatedEnd is { } end)
        {
            for (var index = start; index <= end; index++)
            {
                result.Add(index);
            }
        }
        for (var index = dataStart; index <= dataEnd; index++)
        {
            if (!result.Contains(index))
            {
                result.Add(index);
            }
        }
        return result;
    }

    private static SpreadsheetPrintAxisSlot[] BuildSlots(
        List<int> indexes,
        double origin,
        double scale,
        Func<int, bool> isRepeated,
        Func<int, double> getSize)
    {
        var result = new SpreadsheetPrintAxisSlot[indexes.Count];
        var cursor = origin;
        for (var index = 0; index < indexes.Count; index++)
        {
            var worksheetIndex = indexes[index];
            var size = getSize(worksheetIndex) * scale;
            result[index] = new SpreadsheetPrintAxisSlot(
                worksheetIndex,
                cursor,
                size,
                isRepeated(worksheetIndex));
            cursor += size;
        }
        return result;
    }
}

public sealed record SpreadsheetHeaderFooterContext(
    int PageNumber,
    int TotalPages,
    string WorksheetName,
    string? WorkbookName = null,
    DateTime? Timestamp = null)
{
    public DateTime EffectiveTimestamp =>
        Timestamp ?? DateTime.Now;
}

public static class SpreadsheetHeaderFooterFormatter
{
    public const int MaximumTemplateLength = 32_767;

    public static string Format(
        string? template,
        SpreadsheetHeaderFooterContext context,
        CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (context.PageNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(context),
                "PageNumber must be greater than zero.");
        }
        if (context.TotalPages <= 0 ||
            context.PageNumber > context.TotalPages)
        {
            throw new ArgumentOutOfRangeException(
                nameof(context),
                "TotalPages must be positive and no smaller than PageNumber.");
        }
        if (string.IsNullOrWhiteSpace(context.WorksheetName))
        {
            throw new ArgumentException(
                "WorksheetName must not be blank.",
                nameof(context));
        }
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }
        if (template.Length > MaximumTemplateLength)
        {
            throw new ArgumentException(
                $"Header/footer templates may contain at most " +
                $"{MaximumTemplateLength:N0} characters.",
                nameof(template));
        }

        culture ??= CultureInfo.CurrentCulture;
        var timestamp = context.EffectiveTimestamp;
        var result = new StringBuilder(template.Length + 32);
        for (var index = 0; index < template.Length; index++)
        {
            var current = template[index];
            if (current != '&' || index == template.Length - 1)
            {
                result.Append(current);
                continue;
            }

            var token = char.ToUpperInvariant(template[++index]);
            switch (token)
            {
                case '&':
                    result.Append('&');
                    break;
                case 'P':
                    result.Append(context.PageNumber.ToString(culture));
                    break;
                case 'N':
                    result.Append(context.TotalPages.ToString(culture));
                    break;
                case 'A':
                    result.Append(context.WorksheetName);
                    break;
                case 'F':
                    result.Append(context.WorkbookName ?? string.Empty);
                    break;
                case 'D':
                    result.Append(NormalizeCultureWhitespace(
                        timestamp.ToString("d", culture)));
                    break;
                case 'T':
                    result.Append(NormalizeCultureWhitespace(
                        timestamp.ToString("t", culture)));
                    break;
                default:
                    result.Append('&');
                    result.Append(template[index]);
                    break;
            }
        }
        return result.ToString();
    }

    private static string NormalizeCultureWhitespace(string value) =>
        value
            .Replace('\u00A0', ' ')
            .Replace('\u202F', ' ');
}
