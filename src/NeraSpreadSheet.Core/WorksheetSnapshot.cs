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
    private readonly DataValidationRule[] _dataValidationRules;
    private readonly SpreadsheetTable[] _tables;
    private readonly Dictionary<string, TableStyleDefinition>
        _tableStyles;
    private readonly FormulaSpillRange[] _formulaSpills;
    private readonly WorksheetAutoFilter? _autoFilter;
    private readonly WorksheetAxisInterval[] _hiddenRows;
    private readonly WorksheetAxisInterval[] _hiddenColumns;
    private readonly ConcurrentDictionary<AxisStyleCacheKey, CellStyle>
        _axisStyleCache = new();
    private readonly ConcurrentDictionary<FilterPredicateCacheKey, Lazy<Func<int, bool>>>
        _filterPredicateCache = new();
    private readonly ConcurrentDictionary<string, ResolvedTableStyle>
        _resolvedTableStyles = new(StringComparer.OrdinalIgnoreCase);
    private readonly CellStyle[] _styles;

    private WorksheetSnapshot(
        string name,
        long version,
        IReadOnlyDictionary<CellAddress, CellData> cells,
        CellStyle[] styles,
        ExcelDateSystem dateSystem,
        double defaultRowHeight,
        double defaultColumnWidth,
        IReadOnlyDictionary<int, double> rowHeights,
        IReadOnlyDictionary<int, double> columnWidths,
        WorksheetAxisInterval[] hiddenRows,
        WorksheetAxisInterval[] hiddenColumns,
        CellRange[] mergedCells,
        WorksheetAxisStyleSpan[] rowStyleSpans,
        WorksheetAxisStyleSpan[] columnStyleSpans,
        ConditionalFormattingRule[] conditionalFormattingRules,
        CellStylePatch[] differentialStyles,
        DataValidationRule[] dataValidationRules,
        SpreadsheetTable[] tables,
        TableStyleDefinition[] tableStyles,
        WorkbookTheme theme,
        FormulaSpillRange[] formulaSpills,
        WorksheetAutoFilter? autoFilter)
    {
        Name = name;
        Version = version;
        _cells = cells;
        _styles = [.. styles];
        DateSystem = dateSystem;
        DefaultRowHeight = defaultRowHeight;
        DefaultColumnWidth = defaultColumnWidth;
        RowHeights = rowHeights;
        ColumnWidths = columnWidths;
        _hiddenRows = [.. hiddenRows];
        _hiddenColumns = [.. hiddenColumns];
        _mergedCells = mergedCells;
        _rowStyleSpans = rowStyleSpans
            .Select(static span => span.Clone())
            .ToArray();
        _columnStyleSpans = columnStyleSpans
            .Select(static span => span.Clone())
            .ToArray();
        _conditionalFormattingRules = conditionalFormattingRules
            .Select(static rule => rule.Copy())
            .OrderBy(static rule => rule.Priority)
            .ToArray();
        _differentialStyles = [.. differentialStyles];
        _dataValidationRules = dataValidationRules
            .Select(static rule => rule.Copy())
            .ToArray();
        _tables = tables
            .Select(static table => table.Copy())
            .ToArray();
        _tableStyles = tableStyles.ToDictionary(
            static style => style.Name,
            static style => style.Copy(),
            StringComparer.OrdinalIgnoreCase);
        Theme = theme;
        _formulaSpills = formulaSpills
            .Select(static spill => spill.Copy())
            .OrderBy(static spill => spill.Owner.RowIndex)
            .ThenBy(static spill => spill.Owner.ColumnIndex)
            .ToArray();
        _autoFilter = autoFilter?.Copy();
    }

    public string Name { get; }

    public long Version { get; }

    public int UsedCellCount => _cells.Count;

    /// <summary>Gets the workbook date system captured with this immutable worksheet snapshot.</summary>
    public ExcelDateSystem DateSystem { get; }

    public int RowStyleSpanCount => _rowStyleSpans.Length;

    public int ColumnStyleSpanCount => _columnStyleSpans.Length;

    public int ConditionalFormattingRuleCount =>
        _conditionalFormattingRules.Length;

    public int DifferentialStyleCount =>
        _differentialStyles.Length;

    public int DataValidationRuleCount =>
        _dataValidationRules.Length;

    public int TableCount => _tables.Length;

    public int FormulaSpillCount => _formulaSpills.Length;

    public double DefaultRowHeight { get; }

    public double DefaultColumnWidth { get; }

    public IReadOnlyDictionary<int, double> RowHeights { get; }

    public IReadOnlyDictionary<int, double> ColumnWidths { get; }

    public IReadOnlyList<WorksheetAxisInterval> HiddenRowRanges => _hiddenRows;

    public IReadOnlyList<WorksheetAxisInterval> HiddenColumnRanges => _hiddenColumns;

    public IReadOnlyList<CellRange> MergedCells => _mergedCells;

    public IReadOnlyList<ConditionalFormattingRule>
        ConditionalFormattingRules =>
        _conditionalFormattingRules;

    public IReadOnlyList<DataValidationRule> DataValidationRules =>
        _dataValidationRules;

    public IReadOnlyList<SpreadsheetTable> Tables => _tables;

    public WorkbookTheme Theme { get; }

    public IReadOnlyList<FormulaSpillRange> FormulaSpills =>
        _formulaSpills;

    public WorksheetAutoFilter? AutoFilter => _autoFilter;

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

    /// <summary>Gets the effective cell style from the catalog captured with this snapshot.</summary>
    public CellStyle GetEffectiveStyle(CellAddress address)
    {
        address = ResolveMergedAnchor(address);
        var cell = GetCell(address);
        if (cell.StyleId != CellStyleCatalog.DefaultStyleId)
        {
            if ((uint)cell.StyleId >= (uint)_styles.Length)
            {
                throw new InvalidOperationException("The captured cell references an unavailable style.");
            }
            return _styles[cell.StyleId];
        }

        var key = new AxisStyleCacheKey(
            FindOperations(_rowStyleSpans, address.RowIndex),
            FindOperations(_columnStyleSpans, address.ColumnIndex));
        return _axisStyleCache.GetOrAdd(
            key,
            static cacheKey => ComposeAxisStyle(cacheKey.RowOperations, cacheKey.ColumnOperations));
    }

    /// <summary>
    /// Applies direct and sparse axis formatting over a supplied base style.
    /// Direct cell formatting remains a complete override.
    /// </summary>
    public CellStyle GetEffectiveStyle(
        CellAddress address,
        CellStyle baseStyle)
    {
        ArgumentNullException.ThrowIfNull(baseStyle);
        address = ResolveMergedAnchor(address);
        var cell = GetCell(address);
        if (cell.StyleId != CellStyleCatalog.DefaultStyleId)
        {
            if ((uint)cell.StyleId >= (uint)_styles.Length)
            {
                throw new InvalidOperationException(
                    "The captured cell references an unavailable style.");
            }
            return _styles[cell.StyleId];
        }

        var key = new AxisStyleCacheKey(
            FindOperations(_rowStyleSpans, address.RowIndex),
            FindOperations(_columnStyleSpans, address.ColumnIndex),
            baseStyle);
        return _axisStyleCache.GetOrAdd(
            key,
            static cacheKey => ComposeAxisStyle(
                cacheKey.RowOperations,
                cacheKey.ColumnOperations,
                cacheKey.BaseStyle));
    }

    public bool TryGetResolvedTableStyle(
        string name,
        out ResolvedTableStyle? style)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_tableStyles.TryGetValue(name, out var definition))
        {
            style = null;
            return false;
        }
        style = _resolvedTableStyles.GetOrAdd(
            definition.Name,
            static (_, state) => TableStyleResolver.Resolve(
                state.Definition,
                state.Theme),
            (Definition: definition, Theme));
        return true;
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

    public bool TryGetDataValidationRule(
        CellAddress address,
        out DataValidationRule? rule)
    {
        foreach (var candidate in _dataValidationRules)
        {
            if (candidate.AppliesTo(address))
            {
                rule = candidate;
                return true;
            }
        }

        rule = null;
        return false;
    }

    public bool TryGetTable(
        string name,
        out SpreadsheetTable? table)
    {
        table = _tables.FirstOrDefault(candidate => string.Equals(
            candidate.Name,
            name,
            StringComparison.OrdinalIgnoreCase));
        return table is not null;
    }

    public bool TryGetTable(
        CellAddress address,
        out SpreadsheetTable? table)
    {
        foreach (var candidate in _tables)
        {
            if (candidate.Range.Contains(address))
            {
                table = candidate;
                return true;
            }
        }

        table = null;
        return false;
    }

    public bool TryGetFormulaSpill(
        CellAddress owner,
        out FormulaSpillRange? spill)
    {
        spill = _formulaSpills.FirstOrDefault(candidate =>
            candidate.Owner == owner);
        return spill is not null;
    }

    public bool TryGetFormulaSpillOwner(
        CellAddress address,
        out CellAddress owner)
    {
        foreach (var spill in _formulaSpills)
        {
            if (spill.Range.Contains(address))
            {
                owner = spill.Owner;
                return true;
            }
        }
        owner = default;
        return false;
    }

    public bool IsFormulaSpillChild(CellAddress address) =>
        TryGetFormulaSpillOwner(address, out var owner) &&
        owner != address;

    public bool IsRowHidden(int rowIndex) =>
        IsHidden(_hiddenRows, rowIndex);

    public bool IsColumnHidden(int columnIndex) =>
        IsHidden(_hiddenColumns, columnIndex);

    public bool IsRowVisible(int rowIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(rowIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            rowIndex,
            SpreadsheetLimits.MaxRows);
        if (_autoFilter is not null &&
            !_autoFilter.IsRowVisible(this, rowIndex))
        {
            return false;
        }

        foreach (var table in _tables)
        {
            if (!table.IsRowVisible(this, rowIndex))
            {
                return false;
            }
        }

        return true;
    }

    internal bool MatchesFilter(
        CellRange dataRange,
        int columnIndex,
        TableFilterColumn filter,
        int rowIndex)
    {
        var key = new FilterPredicateCacheKey(dataRange, columnIndex, filter);
        var predicate = _filterPredicateCache.GetOrAdd(
            key,
            cacheKey => new Lazy<Func<int, bool>>(
                () => SpreadsheetFilterEvaluator.CreateRowPredicate(
                    this,
                    cacheKey.DataRange,
                    cacheKey.ColumnIndex,
                    cacheKey.Filter),
                LazyThreadSafetyMode.ExecutionAndPublication));
        return predicate.Value(rowIndex);
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
            [.. worksheet.Workbook.Styles.Snapshot()],
            worksheet.Workbook.DateSystem,
            worksheet.Dimensions.DefaultRowHeight,
            worksheet.Dimensions.DefaultColumnWidth,
            rows,
            columns,
            [.. worksheet.Dimensions.GetHiddenRowRanges()],
            [.. worksheet.Dimensions.GetHiddenColumnRanges()],
            [.. worksheet.MergedCells.Ranges],
            axisStyles.RowSpans,
            axisStyles.ColumnSpans,
            [.. worksheet.ConditionalFormattingRules],
            [.. worksheet.DifferentialStyles.Snapshot()],
            [.. worksheet.DataValidationRules],
            [.. worksheet.Tables],
            worksheet.Workbook.TableStyles.Snapshot(),
            worksheet.Workbook.Theme,
            [.. worksheet.GetFormulaSpills()],
            worksheet.AutoFilter);
    }

    private static bool IsHidden(
        WorksheetAxisInterval[] ranges,
        int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        var low = 0;
        var high = ranges.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var range = ranges[middle];
            if (index < range.Start)
            {
                high = middle - 1;
            }
            else if (index > range.End)
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }
        return false;
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
        WorksheetAxisStyleOperation[] columnOperations,
        CellStyle? baseStyle = null)
    {
        var style = baseStyle ?? CellStyle.Default;
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
        WorksheetAxisStyleOperation[] ColumnOperations,
        CellStyle? BaseStyle = null);

    private readonly record struct FilterPredicateCacheKey(
        CellRange DataRange,
        int ColumnIndex,
        TableFilterColumn Filter);
}
