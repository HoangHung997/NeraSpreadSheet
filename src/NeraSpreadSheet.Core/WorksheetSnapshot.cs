using System.Collections.Concurrent;
using System.Collections.ObjectModel;

namespace NeraSpreadSheet.Core;

public sealed class WorksheetSnapshot
{
    private static readonly WorksheetAxisStyleOperation[]
        EmptyAxisStyleOperations = [];
    private readonly IReadOnlyDictionary<CellAddress, CellData> _cells;
    private readonly CellRange[] _mergedCells;
    private readonly WorksheetAxisStyleSpan[] _rowStyleSpans;
    private readonly WorksheetAxisStyleSpan[] _columnStyleSpans;
    private readonly ConditionalFormattingRule[]
        _conditionalFormattingRules;
    private readonly CellStylePatch[] _differentialStyles;
    private readonly ConcurrentDictionary<AxisStyleCacheKey, CellStyle>
        _axisStyleCache = new();

    private WorksheetSnapshot(
        string name,
        long version,
        IReadOnlyDictionary<CellAddress, CellData> cells,
        double defaultRowHeight,
        double defaultColumnWidth,
        IReadOnlyDictionary<int, double> rowHeights,
        IReadOnlyDictionary<int, double> columnWidths,
        CellRange[] mergedCells,
        WorksheetAxisStyleSpan[] rowStyleSpans,
        WorksheetAxisStyleSpan[] columnStyleSpans,
        ConditionalFormattingRule[] conditionalFormattingRules,
        CellStylePatch[] differentialStyles)
    {
        Name = name;
        Version = version;
        _cells = cells;
        DefaultRowHeight = defaultRowHeight;
        DefaultColumnWidth = defaultColumnWidth;
        RowHeights = rowHeights;
        ColumnWidths = columnWidths;
        _mergedCells = mergedCells;
        _rowStyleSpans = rowStyleSpans
            .Select(static span => span.Clone())
            .ToArray();
        _columnStyleSpans = columnStyleSpans
            .Select(static span => span.Clone())
            .ToArray();
        _conditionalFormattingRules = conditionalFormattingRules
            .Select(static rule => rule.Clone())
            .OrderBy(static rule => rule.Priority)
            .ToArray();
        _differentialStyles = [.. differentialStyles];
    }

    public string Name { get; }

    public long Version { get; }

    public int UsedCellCount => _cells.Count;

    public int RowStyleSpanCount => _rowStyleSpans.Length;

    public int ColumnStyleSpanCount => _columnStyleSpans.Length;

    public int ConditionalFormattingRuleCount =>
        _conditionalFormattingRules.Length;

    public int DifferentialStyleCount =>
        _differentialStyles.Length;

    public double DefaultRowHeight { get; }

    public double DefaultColumnWidth { get; }

    public IReadOnlyDictionary<int, double> RowHeights { get; }

    public IReadOnlyDictionary<int, double> ColumnWidths { get; }

    public IReadOnlyList<CellRange> MergedCells => _mergedCells;

    public IReadOnlyList<ConditionalFormattingRule>
        ConditionalFormattingRules =>
        _conditionalFormattingRules;

    internal int AxisStyleCacheEntryCount =>
        _axisStyleCache.Count;

    public CellData GetCell(CellAddress address) =>
        _cells.GetValueOrDefault(address, CellData.Empty);

    public CellStyle GetEffectiveStyle(
        CellAddress address,
        CellStyleCatalog styles)
    {
        ArgumentNullException.ThrowIfNull(styles);
        address = ResolveMergedAnchor(address);
        var cell = GetCell(address);
        if (cell.StyleId != CellStyleCatalog.DefaultStyleId)
        {
            return styles.Get(cell.StyleId);
        }

        var key = new AxisStyleCacheKey(
            FindOperations(
                _rowStyleSpans,
                address.RowIndex),
            FindOperations(
                _columnStyleSpans,
                address.ColumnIndex));
        return _axisStyleCache.GetOrAdd(
            key,
            static cacheKey => ComposeAxisStyle(
                cacheKey.RowOperations,
                cacheKey.ColumnOperations));
    }

    public CellStylePatch GetDifferentialStyle(
        int styleId)
    {
        if ((uint)styleId >=
            (uint)_differentialStyles.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(styleId));
        }

        return _differentialStyles[styleId];
    }

    public IEnumerable<ConditionalFormattingRule>
        EnumerateConditionalFormattingRules(
            CellAddress address)
    {
        foreach (var rule in _conditionalFormattingRules)
        {
            if (rule.AppliesTo(address))
            {
                yield return rule;
            }
        }
    }

    public IEnumerable<KeyValuePair<CellAddress, CellData>>
        EnumerateUsedCells() =>
        _cells;

    public bool TryGetMergedRange(
        CellAddress address,
        out CellRange range)
    {
        foreach (var candidate in _mergedCells)
        {
            if (candidate.Contains(address))
            {
                range = candidate;
                return true;
            }
        }

        range = default;
        return false;
    }

    public static WorksheetSnapshot Capture(
        Worksheet worksheet)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        var cells =
            new ReadOnlyDictionary<CellAddress, CellData>(
                worksheet
                    .EnumerateUsedCells()
                    .ToDictionary(
                        static pair => pair.Key,
                        static pair => pair.Value));
        var rows =
            new ReadOnlyDictionary<int, double>(
                worksheet.Dimensions
                    .GetRowOverrides()
                    .ToDictionary(
                        static pair => pair.Key,
                        static pair => pair.Value));
        var columns =
            new ReadOnlyDictionary<int, double>(
                worksheet.Dimensions
                    .GetColumnOverrides()
                    .ToDictionary(
                        static pair => pair.Key,
                        static pair => pair.Value));
        var axisStyles =
            worksheet.CaptureAxisStyleState();

        return new WorksheetSnapshot(
            worksheet.Name,
            worksheet.Version,
            cells,
            worksheet.Dimensions.DefaultRowHeight,
            worksheet.Dimensions.DefaultColumnWidth,
            rows,
            columns,
            [.. worksheet.MergedCells.Ranges],
            axisStyles.RowSpans,
            axisStyles.ColumnSpans,
            [.. worksheet.ConditionalFormattingRules],
            [.. worksheet.DifferentialStyles.Snapshot()]);
    }

    private CellAddress ResolveMergedAnchor(
        CellAddress address) =>
        TryGetMergedRange(address, out var range)
            ? range.TopLeft
            : address;

    private static WorksheetAxisStyleOperation[]
        FindOperations(
            WorksheetAxisStyleSpan[] spans,
            int index)
    {
        var low = 0;
        var high = spans.Length - 1;
        while (low <= high)
        {
            var middle =
                low + ((high - low) / 2);
            var span = spans[middle];
            if (index < span.StartIndex)
            {
                high = middle - 1;
            }
            else if (index > span.EndIndex)
            {
                low = middle + 1;
            }
            else
            {
                return span.Operations;
            }
        }

        return EmptyAxisStyleOperations;
    }

    private static CellStyle ComposeAxisStyle(
        WorksheetAxisStyleOperation[] rowOperations,
        WorksheetAxisStyleOperation[] columnOperations)
    {
        var style = CellStyle.Default;
        var rowIndex = 0;
        var columnIndex = 0;
        while (rowIndex < rowOperations.Length ||
               columnIndex < columnOperations.Length)
        {
            WorksheetAxisStyleOperation operation;
            if (columnIndex >= columnOperations.Length ||
                (rowIndex < rowOperations.Length &&
                 rowOperations[rowIndex].Sequence <
                 columnOperations[columnIndex].Sequence))
            {
                operation = rowOperations[rowIndex++];
            }
            else
            {
                operation = columnOperations[columnIndex++];
            }

            style = operation.Patch.Apply(style);
        }

        return style;
    }

    private readonly record struct AxisStyleCacheKey(
        WorksheetAxisStyleOperation[] RowOperations,
        WorksheetAxisStyleOperation[] ColumnOperations);
}
