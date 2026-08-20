using System.Buffers;

namespace NeraSpreadSheet.Core;

public sealed class CellsChangedEventArgs : EventArgs
{
    public CellsChangedEventArgs(
        CellRange range,
        long worksheetVersion)
    {
        Range = range;
        WorksheetVersion = worksheetVersion;
    }

    public CellRange Range { get; }

    public long WorksheetVersion { get; }
}

public sealed class Worksheet
{
    private static readonly SearchValues<char> InvalidNameCharacters =
        SearchValues.Create("[]:*?/\\");
    private readonly Dictionary<CellAddress, CellData> _cells = [];
    private readonly WorksheetAxisStyleMap _rowStyles = new(
        SpreadsheetLimits.MaxRows);
    private readonly WorksheetAxisStyleMap _columnStyles = new(
        SpreadsheetLimits.MaxColumns);
    private readonly WorksheetConditionalFormattingCollection
        _conditionalFormatting = new();
    private readonly WorksheetDataValidationCollection
        _dataValidations = new();
    private readonly WorksheetTableCollection _tables = new();
    private long _nextAxisStyleSequence = 1L;

    internal Worksheet(string name, Workbook workbook)
    {
        Workbook = workbook ?? throw new ArgumentNullException(nameof(workbook));
        Name = name;
        Dimensions = new WorksheetDimensions();
        MergedCells = new MergedCellRanges();
        DifferentialStyles = new DifferentialStyleCatalog();
    }

    internal Workbook Workbook { get; }

    public string Name { get; internal set; }

    public WorksheetDimensions Dimensions { get; }

    public MergedCellRanges MergedCells { get; }

    public DifferentialStyleCatalog DifferentialStyles { get; }

    public long Version { get; private set; }

    public int UsedCellCount => _cells.Count;

    public int RowStyleSpanCount => _rowStyles.SpanCount;

    public int ColumnStyleSpanCount => _columnStyles.SpanCount;

    public int ConditionalFormattingRuleCount =>
        _conditionalFormatting.Count;

    public IReadOnlyList<ConditionalFormattingRule>
        ConditionalFormattingRules =>
        _conditionalFormatting.Rules;

    public int DataValidationRuleCount =>
        _dataValidations.Count;

    public IReadOnlyList<DataValidationRule> DataValidationRules =>
        _dataValidations.Rules;

    public int TableCount => _tables.Count;

    public IReadOnlyList<SpreadsheetTable> Tables => _tables.Tables;

    public event EventHandler<CellsChangedEventArgs>? CellsChanged;

    public CellData GetCell(CellAddress address) =>
        _cells.GetValueOrDefault(address, CellData.Empty);

    public object? GetValue(CellAddress address) =>
        GetCell(ResolveMergedAnchor(address)).Value.RawValue;

    public string? GetFormula(CellAddress address) =>
        GetCell(ResolveMergedAnchor(address)).Formula;

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

        return ComposeAxisStyle(
            _rowStyles.GetOperations(address.RowIndex),
            _columnStyles.GetOperations(address.ColumnIndex));
    }

    public bool TryGetCell(
        CellAddress address,
        out CellData cellData)
    {
        if (_cells.TryGetValue(address, out var stored))
        {
            cellData = stored;
            return true;
        }

        cellData = CellData.Empty;
        return false;
    }

    public IEnumerable<KeyValuePair<CellAddress, CellData>>
        EnumerateUsedCells() =>
        _cells;

    public CellAddress ResolveMergedAnchor(
        CellAddress address) =>
        MergedCells.TryGetContaining(address, out var range)
            ? range.TopLeft
            : address;

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var normalized = name.Trim();
        if (normalized.Length >
            SpreadsheetLimits.MaxWorksheetNameLength)
        {
            throw new ArgumentException(
                $"Worksheet names cannot exceed " +
                $"{SpreadsheetLimits.MaxWorksheetNameLength} characters.",
                nameof(name));
        }

        if (normalized.AsSpan()
            .IndexOfAny(InvalidNameCharacters) >= 0)
        {
            throw new ArgumentException(
                "Worksheet name contains an invalid character.",
                nameof(name));
        }

        if (normalized.StartsWith('\'') ||
            normalized.EndsWith('\''))
        {
            throw new ArgumentException(
                "Worksheet name cannot start or end with an apostrophe.",
                nameof(name));
        }

        Name = normalized;
    }

    public void SetValue(
        CellAddress address,
        object? value)
    {
        address = ResolveMergedAnchor(address);
        var current = GetCell(address);
        SetCell(
            address,
            new CellData(
                CellValue.FromObject(value),
                styleId: current.StyleId));
    }

    public void SetFormula(
        CellAddress address,
        string formula)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        address = ResolveMergedAnchor(address);
        var normalized = formula.StartsWith('=')
            ? formula
            : $"={formula}";
        var current = GetCell(address);
        SetCell(
            address,
            new CellData(
                current.Value,
                normalized,
                current.StyleId));
    }

    public void SetStyle(
        CellAddress address,
        int styleId)
    {
        address = ResolveMergedAnchor(address);
        var current = GetCell(address);
        SetCell(
            address,
            new CellData(
                current.Value,
                current.Formula,
                styleId));
    }

    public void Clear(CellAddress address) =>
        SetCell(
            ResolveMergedAnchor(address),
            CellData.Empty);

    public void AddConditionalFormattingRule(
        ConditionalFormattingRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _conditionalFormatting.Add(
            rule,
            DifferentialStyles);
        PublishConditionalFormattingChange(rule.Ranges);
    }

    public bool RemoveConditionalFormattingRule(Guid ruleId)
    {
        if (!_conditionalFormatting.Remove(
                ruleId,
                out var removed) ||
            removed is null)
        {
            return false;
        }

        PublishConditionalFormattingChange(removed.Ranges);
        return true;
    }

    public void ClearConditionalFormattingRules()
    {
        var existing = _conditionalFormatting.Capture();
        if (existing.Length == 0)
        {
            return;
        }

        _conditionalFormatting.Restore(
            [],
            DifferentialStyles);
        PublishConditionalFormattingChange(
            existing.SelectMany(static rule => rule.Ranges));
    }

    public void AddDataValidationRule(DataValidationRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        _dataValidations.Add(rule);
        PublishDataValidationChange(rule.Ranges);
    }

    public bool RemoveDataValidationRule(Guid ruleId)
    {
        if (!_dataValidations.Remove(ruleId, out var removed) ||
            removed is null)
        {
            return false;
        }

        PublishDataValidationChange(removed.Ranges);
        return true;
    }

    public void ClearDataValidationRules()
    {
        var existing = _dataValidations.Capture();
        if (existing.Length == 0)
        {
            return;
        }

        _dataValidations.Restore([]);
        PublishDataValidationChange(
            existing.SelectMany(static rule => rule.Ranges));
    }

    public bool TryGetDataValidationRule(
        CellAddress address,
        out DataValidationRule? rule) =>
        _dataValidations.TryGetRule(
            ResolveMergedAnchor(address),
            out rule);

    public void AddTable(SpreadsheetTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        Workbook.EnsureTableNameAvailable(table.Name, table.Id);
        if (MergedCells.Ranges.Any(range =>
                range.Intersects(table.Range)))
        {
            throw new InvalidOperationException(
                "A table cannot overlap a merged range.");
        }

        _tables.Add(table);
        Workbook.NotifyTableCollectionChanged();
        PublishTableChange(table.Range);
    }

    public bool RemoveTable(Guid tableId)
    {
        if (!_tables.Remove(tableId, out var removed) ||
            removed is null)
        {
            return false;
        }

        Workbook.NotifyTableCollectionChanged();
        PublishTableChange(removed.Range);
        return true;
    }

    public bool TryGetTable(
        string name,
        out SpreadsheetTable? table) =>
        _tables.TryGet(name, out table);

    public bool TryGetTable(
        Guid id,
        out SpreadsheetTable? table) =>
        _tables.TryGet(id, out table);

    public bool TryGetTable(
        CellAddress address,
        out SpreadsheetTable? table) =>
        _tables.TryGet(address, out table);

    public void RenameTable(Guid tableId, string name)
    {
        if (!_tables.TryGet(tableId, out var table) ||
            table is null)
        {
            throw new KeyNotFoundException(
                $"Table '{tableId}' was not found.");
        }

        Workbook.EnsureTableNameAvailable(name, tableId);
        var replacement = table.Rename(name);
        ReplaceTable(tableId, replacement);
    }

    public void RenameTableColumn(
        Guid tableId,
        Guid columnId,
        string name)
    {
        if (!_tables.TryGet(tableId, out var table) ||
            table is null)
        {
            throw new KeyNotFoundException(
                $"Table '{tableId}' was not found.");
        }

        ReplaceTable(tableId, table.RenameColumn(columnId, name));
    }

    public void SetTableAutoFilter(
        Guid tableId,
        TableAutoFilter? autoFilter)
    {
        if (!_tables.TryGet(tableId, out var table) ||
            table is null)
        {
            throw new KeyNotFoundException(
                $"Table '{tableId}' was not found.");
        }

        ReplaceTable(tableId, table.WithAutoFilter(autoFilter));
    }

    public void MergeCells(
        CellRange range,
        bool clearNonTopLeftCells = true)
    {
        if (_tables.Tables.Any(table =>
                table.Range.Intersects(range)))
        {
            throw new InvalidOperationException(
                "Merged cells cannot overlap a table.");
        }

        MergedCells.Add(range);

        if (clearNonTopLeftCells)
        {
            var addressesToRemove = _cells.Keys
                .Where(address =>
                    address != range.TopLeft &&
                    range.Contains(address))
                .ToArray();
            foreach (var address in addressesToRemove)
            {
                _cells.Remove(address);
            }
        }

        PublishChange(range);
    }

    public bool UnmergeCells(CellRange range)
    {
        if (!MergedCells.Remove(range))
        {
            return false;
        }

        PublishChange(range);
        return true;
    }

    public bool UnmergeCell(CellAddress address)
    {
        if (!MergedCells.TryGetContaining(
                address,
                out var range))
        {
            return false;
        }

        return UnmergeCells(range);
    }

    public void SetCell(
        CellAddress address,
        CellData cellData)
    {
        ArgumentNullException.ThrowIfNull(cellData);
        address = ResolveMergedAnchor(address);
        SetCells([
            new KeyValuePair<CellAddress, CellData>(
                address,
                cellData),
        ]);
    }

    public void SetCells(
        IEnumerable<KeyValuePair<CellAddress, CellData>> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);
        var requested =
            new Dictionary<CellAddress, CellData>();
        foreach (var pair in changes)
        {
            ArgumentNullException.ThrowIfNull(pair.Value);
            requested[ResolveMergedAnchor(pair.Key)] =
                pair.Value;
        }

        if (requested.Count == 0)
        {
            return;
        }

        var changed = false;
        var top = int.MaxValue;
        var left = int.MaxValue;
        var bottom = int.MinValue;
        var right = int.MinValue;

        foreach (var (address, cellData) in requested)
        {
            if (GetCell(address) == cellData)
            {
                continue;
            }

            if (cellData.IsEmpty)
            {
                _cells.Remove(address);
            }
            else
            {
                _cells[address] = cellData;
            }

            changed = true;
            top = Math.Min(top, address.RowIndex);
            left = Math.Min(left, address.ColumnIndex);
            bottom = Math.Max(bottom, address.RowIndex);
            right = Math.Max(right, address.ColumnIndex);
        }

        if (!changed)
        {
            return;
        }

        PublishChange(new CellRange(
            new CellAddress(top, left),
            new CellAddress(bottom, right)));
    }

    internal WorksheetAxisStyleState
        CaptureAxisStyleState() => new(
            _rowStyles.Capture(),
            _columnStyles.Capture(),
            _nextAxisStyleSequence);

    internal void ApplyAxisStyle(
        WorksheetAxis axis,
        int startIndex,
        int endIndex,
        CellStylePatch patch)
    {
        ArgumentNullException.ThrowIfNull(patch);
        if (!Enum.IsDefined(axis))
        {
            throw new ArgumentOutOfRangeException(
                nameof(axis));
        }

        if (patch.IsEmpty)
        {
            return;
        }

        if (_nextAxisStyleSequence == long.MaxValue)
        {
            throw new InvalidOperationException(
                "The worksheet axis-style sequence is exhausted.");
        }

        var operation = new WorksheetAxisStyleOperation(
            _nextAxisStyleSequence++,
            patch);
        if (axis == WorksheetAxis.Row)
        {
            _rowStyles.Apply(
                startIndex,
                endIndex,
                operation);
        }
        else
        {
            _columnStyles.Apply(
                startIndex,
                endIndex,
                operation);
        }

        PublishAxisStyle(
            axis,
            startIndex,
            endIndex);
    }

    internal void RestoreAxisStyleState(
        WorksheetAxisStyleState state,
        CellRange signalRange)
    {
        ArgumentNullException.ThrowIfNull(state);
        _rowStyles.Restore(state.RowSpans);
        _columnStyles.Restore(state.ColumnSpans);
        _nextAxisStyleSequence = state.NextSequence;
        PublishChange(signalRange);
    }

    internal WorksheetStructuralState
        CaptureStructuralState() => new(
            _cells.ToArray(),
            Dimensions.GetRowOverrides().ToArray(),
            Dimensions.GetColumnOverrides().ToArray(),
            MergedCells.Ranges.ToArray(),
            _rowStyles.Capture(),
            _columnStyles.Capture(),
            _nextAxisStyleSequence,
            _conditionalFormatting.Capture(),
            _dataValidations.Capture(),
            _tables.Capture());

    internal void ApplyStructuralChange(
        WorksheetStructuralChange change)
    {
        var transformedCells =
            CreateStructuralCells(change);
        var transformedDimensions =
            Dimensions.CreateStructuralOverrides(change);
        var transformedMergedCells =
            MergedCells.CreateStructuralRanges(change);
        var transformedStyles =
            CreateStructuralStyles(change);
        var transformedConditionalFormatting =
            _conditionalFormatting.CreateStructuralRules(change);
        var transformedDataValidations =
            _dataValidations.CreateStructuralRules(change);
        var transformedTables =
            _tables.CreateStructuralTables(change, Name);
        ValidateTablesAgainstMergedRanges(
            transformedTables,
            transformedMergedCells);

        ReplaceCells(transformedCells);
        Dimensions.ReplaceStructuralOverrides(
            change,
            transformedDimensions);
        MergedCells.ReplaceAll(transformedMergedCells);
        RestoreAxisStylesWithoutPublish(transformedStyles);
        _conditionalFormatting.Restore(
            transformedConditionalFormatting,
            DifferentialStyles);
        _dataValidations.Restore(transformedDataValidations);
        _tables.Restore(transformedTables);
        PublishStructuralChange(change);
    }

    internal void ApplyAxisMove(
        WorksheetAxisMove move)
    {
        if (move.IsNoOp)
        {
            return;
        }

        var transformedCells =
            CreateAxisMoveCells(move);
        var transformedDimensions =
            Dimensions.CreateAxisMoveOverrides(move);
        var transformedMergedCells =
            MergedCells.CreateAxisMoveRanges(move);
        var transformedStyles =
            CreateAxisMoveStyles(move);
        var transformedConditionalFormatting =
            _conditionalFormatting.CreateAxisMoveRules(move);
        var transformedDataValidations =
            _dataValidations.CreateAxisMoveRules(move);
        var transformedTables =
            _tables.CreateAxisMoveTables(move);
        ValidateTablesAgainstMergedRanges(
            transformedTables,
            transformedMergedCells);

        ReplaceCells(transformedCells);
        Dimensions.ReplaceAxisMoveOverrides(
            move,
            transformedDimensions);
        MergedCells.ReplaceAll(transformedMergedCells);
        RestoreAxisStylesWithoutPublish(transformedStyles);
        _conditionalFormatting.Restore(
            transformedConditionalFormatting,
            DifferentialStyles);
        _dataValidations.Restore(transformedDataValidations);
        _tables.Restore(transformedTables);
        PublishAxisMove(move);
    }

    internal void RestoreStructuralState(
        WorksheetStructuralState state,
        WorksheetStructuralChange signalChange)
    {
        ArgumentNullException.ThrowIfNull(state);

        ReplaceCells(state.Cells);
        Dimensions.RestoreOverrides(
            state.RowHeights,
            state.ColumnWidths,
            signalChange);
        MergedCells.ReplaceAll(state.MergedCells);
        RestoreAxisStylesWithoutPublish(
            new WorksheetAxisStyleState(
                state.RowStyleSpans,
                state.ColumnStyleSpans,
                state.NextAxisStyleSequence));
        _conditionalFormatting.Restore(
            state.ConditionalFormattingRules,
            DifferentialStyles);
        _dataValidations.Restore(state.DataValidationRules);
        _tables.Restore(state.Tables);
        PublishStructuralChange(signalChange);
    }

    internal void RestoreAxisMoveState(
        WorksheetStructuralState state,
        WorksheetAxisMove signalMove)
    {
        ArgumentNullException.ThrowIfNull(state);

        ReplaceCells(state.Cells);
        Dimensions.RestoreOverrides(
            state.RowHeights,
            state.ColumnWidths,
            signalMove);
        MergedCells.ReplaceAll(state.MergedCells);
        RestoreAxisStylesWithoutPublish(
            new WorksheetAxisStyleState(
                state.RowStyleSpans,
                state.ColumnStyleSpans,
                state.NextAxisStyleSequence));
        _conditionalFormatting.Restore(
            state.ConditionalFormattingRules,
            DifferentialStyles);
        _dataValidations.Restore(state.DataValidationRules);
        _tables.Restore(state.Tables);
        PublishAxisMove(signalMove);
    }

    internal void RestoreConditionalFormatting(
        IEnumerable<ConditionalFormattingRule> rules,
        CellRange signalRange)
    {
        _conditionalFormatting.Restore(
            rules,
            DifferentialStyles);
        PublishChange(signalRange);
    }

    internal void RestoreDataValidations(
        IEnumerable<DataValidationRule> rules,
        CellRange signalRange)
    {
        _dataValidations.Restore(rules);
        PublishChange(signalRange);
    }

    internal void RestoreTables(
        IEnumerable<SpreadsheetTable> tables,
        CellRange signalRange)
    {
        _tables.Restore(tables);
        Workbook.NotifyTableCollectionChanged();
        PublishChange(signalRange);
    }

    private Dictionary<CellAddress, CellData>
        CreateStructuralCells(
            WorksheetStructuralChange change)
    {
        var transformed =
            new Dictionary<CellAddress, CellData>(
                _cells.Count);
        foreach (var (address, cell) in _cells)
        {
            if (!change.TryMapAddress(
                    address,
                    out var mappedAddress))
            {
                if (change.Kind ==
                    WorksheetStructuralChangeKind.Insert)
                {
                    throw new InvalidOperationException(
                        "Cannot insert because a used cell would move " +
                        "outside the worksheet bounds.");
                }

                continue;
            }

            transformed.Add(mappedAddress, cell);
        }

        return transformed;
    }

    private Dictionary<CellAddress, CellData>
        CreateAxisMoveCells(
            WorksheetAxisMove move)
    {
        var transformed =
            new Dictionary<CellAddress, CellData>(
                _cells.Count);
        foreach (var (address, cell) in _cells)
        {
            transformed.Add(
                move.MapAddress(address),
                cell);
        }

        return transformed;
    }

    private WorksheetAxisStyleState
        CreateStructuralStyles(
            WorksheetStructuralChange change)
    {
        var rowStyles = new WorksheetAxisStyleMap(
            SpreadsheetLimits.MaxRows);
        var columnStyles = new WorksheetAxisStyleMap(
            SpreadsheetLimits.MaxColumns);
        rowStyles.Restore(_rowStyles.Capture());
        columnStyles.Restore(_columnStyles.Capture());
        if (change.Axis == WorksheetAxis.Row)
        {
            rowStyles.ApplyStructuralChange(change);
        }
        else
        {
            columnStyles.ApplyStructuralChange(change);
        }

        return new WorksheetAxisStyleState(
            rowStyles.Capture(),
            columnStyles.Capture(),
            _nextAxisStyleSequence);
    }

    private WorksheetAxisStyleState CreateAxisMoveStyles(
        WorksheetAxisMove move)
    {
        var rowStyles = new WorksheetAxisStyleMap(
            SpreadsheetLimits.MaxRows);
        var columnStyles = new WorksheetAxisStyleMap(
            SpreadsheetLimits.MaxColumns);
        rowStyles.Restore(_rowStyles.Capture());
        columnStyles.Restore(_columnStyles.Capture());
        if (move.Axis == WorksheetAxis.Row)
        {
            rowStyles.ApplyAxisMove(move);
        }
        else
        {
            columnStyles.ApplyAxisMove(move);
        }

        return new WorksheetAxisStyleState(
            rowStyles.Capture(),
            columnStyles.Capture(),
            _nextAxisStyleSequence);
    }

    private void RestoreAxisStylesWithoutPublish(
        WorksheetAxisStyleState state)
    {
        _rowStyles.Restore(state.RowSpans);
        _columnStyles.Restore(state.ColumnSpans);
        _nextAxisStyleSequence = state.NextSequence;
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

    private void ReplaceTable(
        Guid tableId,
        SpreadsheetTable replacement)
    {
        var tables = _tables.Capture();
        var index = Array.FindIndex(
            tables,
            table => table.Id == tableId);
        if (index < 0)
        {
            throw new KeyNotFoundException(
                $"Table '{tableId}' was not found.");
        }

        var oldRange = tables[index].Range;
        tables[index] = replacement;
        _tables.Restore(tables);
        Workbook.NotifyTableCollectionChanged();
        PublishTableChange(new CellRange(
            new CellAddress(
                Math.Min(oldRange.Top, replacement.Range.Top),
                Math.Min(oldRange.Left, replacement.Range.Left)),
            new CellAddress(
                Math.Max(oldRange.Bottom, replacement.Range.Bottom),
                Math.Max(oldRange.Right, replacement.Range.Right))));
    }

    private static void ValidateTablesAgainstMergedRanges(
        IEnumerable<SpreadsheetTable> tables,
        IEnumerable<CellRange> mergedRanges)
    {
        var materializedMerges = mergedRanges.ToArray();
        foreach (var table in tables)
        {
            if (materializedMerges.Any(range =>
                    range.Intersects(table.Range)))
            {
                throw new InvalidOperationException(
                    $"Table '{table.Name}' would overlap a merged range.");
            }
        }
    }

    private void ReplaceCells(
        IEnumerable<KeyValuePair<CellAddress, CellData>> cells)
    {
        _cells.Clear();
        foreach (var (address, cell) in cells)
        {
            _cells.Add(address, cell);
        }
    }

    private void PublishStructuralChange(
        WorksheetStructuralChange change)
    {
        var range = change.Axis == WorksheetAxis.Row
            ? new CellRange(
                new CellAddress(change.Index, 0),
                new CellAddress(
                    SpreadsheetLimits.MaxRows - 1,
                    SpreadsheetLimits.MaxColumns - 1))
            : new CellRange(
                new CellAddress(0, change.Index),
                new CellAddress(
                    SpreadsheetLimits.MaxRows - 1,
                    SpreadsheetLimits.MaxColumns - 1));
        PublishChange(range);
    }

    private void PublishAxisMove(
        WorksheetAxisMove move)
    {
        var range = move.Axis == WorksheetAxis.Row
            ? new CellRange(
                new CellAddress(
                    move.AffectedStartIndex,
                    0),
                new CellAddress(
                    move.AffectedEndIndex,
                    SpreadsheetLimits.MaxColumns - 1))
            : new CellRange(
                new CellAddress(
                    0,
                    move.AffectedStartIndex),
                new CellAddress(
                    SpreadsheetLimits.MaxRows - 1,
                    move.AffectedEndIndex));
        PublishChange(range);
    }

    private void PublishAxisStyle(
        WorksheetAxis axis,
        int startIndex,
        int endIndex)
    {
        var range = axis == WorksheetAxis.Row
            ? new CellRange(
                new CellAddress(startIndex, 0),
                new CellAddress(
                    endIndex,
                    SpreadsheetLimits.MaxColumns - 1))
            : new CellRange(
                new CellAddress(0, startIndex),
                new CellAddress(
                    SpreadsheetLimits.MaxRows - 1,
                    endIndex));
        PublishChange(range);
    }

    private void PublishConditionalFormattingChange(
        IEnumerable<CellRange> ranges)
    {
        PublishRuleChange(ranges);
    }

    private void PublishDataValidationChange(
        IEnumerable<CellRange> ranges)
    {
        PublishRuleChange(ranges);
    }

    private void PublishTableChange(CellRange range)
    {
        PublishChange(range);
    }

    private void PublishRuleChange(IEnumerable<CellRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        var materialized = ranges.ToArray();
        if (materialized.Length == 0)
        {
            return;
        }

        var top = materialized.Min(
            static range => range.Top);
        var left = materialized.Min(
            static range => range.Left);
        var bottom = materialized.Max(
            static range => range.Bottom);
        var right = materialized.Max(
            static range => range.Right);
        PublishChange(new CellRange(
            new CellAddress(top, left),
            new CellAddress(bottom, right)));
    }

    private void PublishChange(CellRange range)
    {
        range = _conditionalFormatting.ExpandSignalRange(range);
        range = _dataValidations.ExpandSignalRange(range);
        range = _tables.ExpandSignalRange(range);
        Version++;
        CellsChanged?.Invoke(
            this,
            new CellsChangedEventArgs(range, Version));
    }
}
