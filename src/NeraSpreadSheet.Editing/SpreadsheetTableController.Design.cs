using NeraSpreadSheet.Core;
using NeraSpreadSheet.Formulas;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Editing;

public sealed partial class SpreadsheetTableController
{
    /// <summary>Maximum number of Table rows scanned by one remove-duplicates command.</summary>
    public const int MaximumRemoveDuplicateRows = 100_000;

    /// <summary>Maximum number of key cells inspected by one remove-duplicates command.</summary>
    public const int MaximumRemoveDuplicateKeyCells = 1_000_000;

    /// <summary>Creates a Table over a range while retaining sparse worksheet storage.</summary>
    public SpreadsheetTable Create(
        CellRange range,
        string? name = null,
        bool hasHeaders = true)
    {
        EnsureRangeCanOwnTable(range, ignoredTableId: null);
        var tableName = string.IsNullOrWhiteSpace(name)
            ? CreateUniqueTableName()
            : name.Trim();
        var columns = CreateColumns(range, hasHeaders);
        var table = new SpreadsheetTable(
            Guid.NewGuid(),
            tableName,
            range,
            columns,
            hasHeaders);
        ExecuteIncremental(new CreateTableOperation(
            _session,
            table));
        return GetTable(table.Id);
    }

    /// <summary>Resizes a Table from its fixed top-left corner.</summary>
    public void Resize(Guid tableId, CellRange range)
    {
        var table = GetTable(tableId);
        if (range.TopLeft != table.Range.TopLeft)
        {
            throw new InvalidOperationException(
                "Table resize must retain the current top-left cell.");
        }
        if (range == table.Range)
        {
            return;
        }
        EnsureRangeCanOwnTable(range, table.Id);
        EnsureMetadataRowsFit(table, range);
        if (range.ColumnCount < table.Columns.Count)
        {
            foreach (var removed in table.Columns.Skip(range.ColumnCount))
            {
                EnsureColumnIsNotReferenced(table, removed);
            }
        }
        var columns = TranslateColumnsForLayout(
            table,
            range,
            ResizeColumns(table, range.ColumnCount),
            table.HasHeaders,
            table.HasTotalsRow);
        var filter = ResizeFilter(table, columns);
        var replacement = CopyTable(
            table,
            range: range,
            columns: columns,
            autoFilter: filter,
            replaceAutoFilter: true);
        ExecuteIncremental(new UpdateTableMetadataOperation(
            _session.ActiveWorksheet,
            table,
            replacement,
            "Resize table"));
    }

    /// <summary>Shows or hides the Table header row without changing Table identity.</summary>
    public void SetHeaderRow(Guid tableId, bool visible)
    {
        var table = GetTable(tableId);
        if (table.HasHeaders == visible)
        {
            return;
        }
        if (!visible && table.Range.RowCount == 1)
        {
            throw new InvalidOperationException(
                "A Table without a header must retain at least one worksheet row.");
        }
        if (visible && table.Range.Bottom == SpreadsheetLimits.MaxRows - 1)
        {
            throw new InvalidOperationException(
                "The Table header row cannot grow beyond the worksheet row limit.");
        }
        var nextBottom = checked(table.Range.Bottom + (visible ? 1 : -1));
        var nextRange = new CellRange(
            table.Range.TopLeft,
            new CellAddress(nextBottom, table.Range.Right));
        EnsureRangeCanOwnTable(nextRange, table.Id);
        EnsureMetadataRowsFit(table, nextRange, hasHeaders: visible);
        if (visible)
        {
            EnsureDestinationIsEmpty(new CellRange(
                new CellAddress(nextBottom, table.Range.Left),
                new CellAddress(nextBottom, table.Range.Right)));
        }
        EnsureMoveRangeIsNotDirectlyReferenced(table.Range);
        EnsureTableMetadataCanMoveWithoutA1References(table);
        var replacement = CopyTable(
            table,
            range: nextRange,
            hasHeaders: visible);
        ExecuteIncremental(new SetTableHeaderOperation(
            _session,
            table,
            replacement,
            visible));
    }

    /// <summary>Shows or hides a dedicated totals row at the bottom of the Table.</summary>
    public void SetTotalsRow(Guid tableId, bool visible)
    {
        var table = GetTable(tableId);
        if (table.HasTotalsRow == visible)
        {
            return;
        }

        var nextBottom = checked(table.Range.Bottom + (visible ? 1 : -1));
        if (nextBottom < table.Range.Top ||
            nextBottom >= SpreadsheetLimits.MaxRows)
        {
            throw new InvalidOperationException(
                "The Table totals row cannot be changed at this worksheet boundary.");
        }
        var nextRange = new CellRange(
            table.Range.TopLeft,
            new CellAddress(nextBottom, table.Range.Right));
        EnsureRangeCanOwnTable(nextRange, table.Id);
        if (visible)
        {
            EnsureDestinationIsEmpty(new CellRange(
                new CellAddress(nextBottom, table.Range.Left),
                new CellAddress(nextBottom, table.Range.Right)));
        }
        var replacement = CopyTable(
            table,
            range: nextRange,
            hasTotalsRow: visible);
        ExecuteIncremental(new SetTableTotalsRowOperation(
            _session,
            table,
            replacement,
            visible));
    }

    /// <summary>Changes the Table style by catalog name.</summary>
    public void SetStyle(Guid tableId, string? styleName)
    {
        var table = GetTable(tableId);
        if (styleName is not null)
        {
            _session.Workbook.TableStyles.Get(styleName);
        }
        if (string.Equals(table.StyleName, styleName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        ReplaceVisualMetadata(table, CopyTable(table, styleName: styleName, replaceStyle: true),
            "Set table style");
    }

    /// <summary>Sets emphasis for the first Table column.</summary>
    public void SetFirstColumn(Guid tableId, bool visible) =>
        SetVisualOptions(
            tableId,
            showFirstColumn: visible,
            description: "Set first table column");

    /// <summary>Sets emphasis for the last Table column.</summary>
    public void SetLastColumn(Guid tableId, bool visible) =>
        SetVisualOptions(
            tableId,
            showLastColumn: visible,
            description: "Set last table column");

    /// <summary>Enables or disables alternating row bands.</summary>
    public void SetBandedRows(Guid tableId, bool visible) =>
        SetVisualOptions(
            tableId,
            showRowStripes: visible,
            description: "Set table row bands");

    /// <summary>Enables or disables alternating column bands.</summary>
    public void SetBandedColumns(Guid tableId, bool visible) =>
        SetVisualOptions(
            tableId,
            showColumnStripes: visible,
            description: "Set table column bands");

    /// <summary>Shows or hides filter buttons while retaining filter criteria.</summary>
    public void SetFilterButtons(Guid tableId, bool visible) =>
        SetVisualOptions(
            tableId,
            showFilterButtons: visible,
            description: "Set table filter buttons");

    /// <summary>Inserts a data row at a worksheet row inside the Table.</summary>
    public void InsertRow(Guid tableId, int worksheetRowIndex)
    {
        var table = GetTable(tableId);
        var dataTop = table.Range.Top + (table.HasHeaders ? 1 : 0);
        var dataBottom = table.Range.Bottom - (table.HasTotalsRow ? 1 : 0);
        var minimum = dataTop;
        var maximum = Math.Max(dataTop, dataBottom + 1);
        if (worksheetRowIndex < minimum || worksheetRowIndex > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(worksheetRowIndex));
        }
        if (table.Range.Bottom == SpreadsheetLimits.MaxRows - 1)
        {
            throw new InvalidOperationException("The Table cannot grow beyond the worksheet row limit.");
        }
        var expanded = new CellRange(
            table.Range.TopLeft,
            new CellAddress(table.Range.Bottom + 1, table.Range.Right));
        EnsureRangeCanOwnTable(expanded, table.Id);
        EnsureDestinationIsEmpty(new CellRange(
            new CellAddress(table.Range.Bottom + 1, table.Range.Left),
            new CellAddress(table.Range.Bottom + 1, table.Range.Right)));
        if (worksheetRowIndex <= table.Range.Bottom)
        {
            EnsureMoveRangeIsNotDirectlyReferenced(
                new CellRange(
                    new CellAddress(worksheetRowIndex, table.Range.Left),
                    new CellAddress(table.Range.Bottom, table.Range.Right)));
        }
        EnsureTableMetadataCanMoveWithoutA1References(table);
        ExecuteIncremental(new InsertTableRowOperation(
            _session,
            table,
            CopyTable(table, range: expanded),
            worksheetRowIndex));
    }

    /// <summary>Deletes one data row and compacts the remaining Table data.</summary>
    public void DeleteRow(Guid tableId, int worksheetRowIndex)
    {
        var table = GetTable(tableId);
        if (table.DataRange is not { } dataRange ||
            worksheetRowIndex < dataRange.Top ||
            worksheetRowIndex > dataRange.Bottom)
        {
            throw new ArgumentOutOfRangeException(nameof(worksheetRowIndex));
        }
        if (table.Range.RowCount == 1)
        {
            throw new InvalidOperationException(
                "A Table must retain at least one worksheet row.");
        }
        EnsureRangeCanOwnTable(table.Range, table.Id);
        EnsureMoveRangeIsNotDirectlyReferenced(
            new CellRange(
                new CellAddress(worksheetRowIndex, table.Range.Left),
                new CellAddress(table.Range.Bottom, table.Range.Right)));
        var reduced = new CellRange(
            table.Range.TopLeft,
            new CellAddress(table.Range.Bottom - 1, table.Range.Right));
        EnsureMetadataRowsFit(table, reduced);
        EnsureTableMetadataCanMoveWithoutA1References(table);
        ExecuteIncremental(new DeleteTableRowOperation(
            _session,
            table,
            CopyTable(table, range: reduced),
            worksheetRowIndex));
    }

    /// <summary>Inserts a new stable Table column before the supplied worksheet column.</summary>
    public SpreadsheetTableColumn InsertColumn(
        Guid tableId,
        int worksheetColumnIndex,
        string? name = null)
    {
        var table = GetTable(tableId);
        if (worksheetColumnIndex < table.Range.Left ||
            worksheetColumnIndex > table.Range.Right + 1)
        {
            throw new ArgumentOutOfRangeException(nameof(worksheetColumnIndex));
        }
        if (table.Range.Right == SpreadsheetLimits.MaxColumns - 1)
        {
            throw new InvalidOperationException("The Table cannot grow beyond the worksheet column limit.");
        }
        var offset = worksheetColumnIndex - table.Range.Left;
        var columnName = CreateUniqueColumnName(
            table.Columns.Select(static column => column.Name),
            name);
        var inserted = new SpreadsheetTableColumn(Guid.NewGuid(), columnName);
        var columns = table.Columns.Select(static column => column.Copy()).ToList();
        columns.Insert(offset, inserted);
        var expanded = new CellRange(
            table.Range.TopLeft,
            new CellAddress(table.Range.Bottom, table.Range.Right + 1));
        EnsureRangeCanOwnTable(expanded, table.Id);
        EnsureDestinationIsEmpty(new CellRange(
            new CellAddress(table.Range.Top, table.Range.Right + 1),
            new CellAddress(table.Range.Bottom, table.Range.Right + 1)));
        if (worksheetColumnIndex <= table.Range.Right)
        {
            EnsureMoveRangeIsNotDirectlyReferenced(new CellRange(
                new CellAddress(table.Range.Top, worksheetColumnIndex),
                table.Range.BottomRight));
        }
        EnsureTableMetadataCanMoveWithoutA1References(table);
        var filter = MapFilterForColumnInsert(table.AutoFilter, offset);
        ExecuteIncremental(new InsertTableColumnOperation(
            _session,
            table,
            CopyTable(
                table,
                range: expanded,
                columns: columns,
                autoFilter: filter,
                replaceAutoFilter: true),
            worksheetColumnIndex));
        return inserted.Copy();
    }

    /// <summary>Deletes a Table column after validating structured-reference safety.</summary>
    public void DeleteColumn(Guid tableId, Guid columnId)
    {
        var table = GetTable(tableId);
        if (table.Columns.Count == 1)
        {
            throw new InvalidOperationException("A Table must retain at least one column.");
        }
        EnsureRangeCanOwnTable(table.Range, table.Id);
        var offset = table.GetColumnIndex(columnId);
        var removed = table.Columns[offset];
        EnsureColumnIsNotReferenced(table, removed);
        EnsureMoveRangeIsNotDirectlyReferenced(new CellRange(
            new CellAddress(table.Range.Top, table.Range.Left + offset),
            table.Range.BottomRight));
        var columns = table.Columns
            .Where(column => column.Id != columnId)
            .Select(static column => column.Copy())
            .ToArray();
        var reduced = new CellRange(
            table.Range.TopLeft,
            new CellAddress(table.Range.Bottom, table.Range.Right - 1));
        EnsureTableMetadataCanMoveWithoutA1References(table);
        var filter = MapFilterForColumnDelete(table.AutoFilter, columnId, offset);
        ExecuteIncremental(new DeleteTableColumnOperation(
            _session,
            table,
            CopyTable(
                table,
                range: reduced,
                columns: columns,
                autoFilter: filter,
                replaceAutoFilter: true),
            table.Range.Left + offset));
    }

    /// <summary>Removes duplicate data rows using stable Table column identities.</summary>
    public int RemoveDuplicates(Guid tableId, IEnumerable<Guid>? columnIds = null)
    {
        var table = GetTable(tableId);
        if (table.DataRange is not { } dataRange)
        {
            return 0;
        }
        EnsureRangeCanOwnTable(table.Range, table.Id);
        if (dataRange.RowCount > MaximumRemoveDuplicateRows)
        {
            throw new InvalidOperationException(
                $"Remove duplicates is limited to {MaximumRemoveDuplicateRows} Table rows.");
        }
        var requested = columnIds?.ToArray() ?? table.Columns
            .Select(static column => column.Id)
            .ToArray();
        if (requested.Length == 0 || requested.Distinct().Count() != requested.Length)
        {
            throw new ArgumentException(
                "Remove duplicates requires one or more unique Table columns.",
                nameof(columnIds));
        }
        var offsets = requested.Select(table.GetColumnIndex).ToArray();
        if ((long)dataRange.RowCount * offsets.Length > MaximumRemoveDuplicateKeyCells)
        {
            throw new InvalidOperationException(
                $"Remove duplicates is limited to {MaximumRemoveDuplicateKeyCells} key cells.");
        }
        var retainedRows = GetDistinctRows(table, offsets);
        var removedCount = dataRange.RowCount - retainedRows.Length;
        if (removedCount == 0)
        {
            return 0;
        }
        EnsureMoveRangeIsNotDirectlyReferenced(dataRange);
        var reduced = new CellRange(
            table.Range.TopLeft,
            new CellAddress(table.Range.Bottom - removedCount, table.Range.Right));
        EnsureTableMetadataCanMoveWithoutA1References(table);
        ExecuteIncremental(new RemoveDuplicateTableRowsOperation(
            _session,
            table,
            CopyTable(table, range: reduced),
            retainedRows));
        return removedCount;
    }

    /// <summary>Converts this Table's structured references to A1 before removing its metadata.</summary>
    public bool ConvertToRange(Guid tableId)
    {
        if (!_session.ActiveWorksheet.TryGetTable(tableId, out var table) ||
            table is null)
        {
            return false;
        }
        ExecuteIncremental(new ConvertTableToRangeOperation(
            _session.Workbook,
            _session.ActiveWorksheet,
            table));
        return true;
    }

    private sealed class ConvertTableToRangeOperation
        : FormulaRewritingTableOperationBase, IDependencyGraphRebuildOperation
    {
        private readonly Workbook _workbook;
        private readonly SpreadsheetTable _table;

        public ConvertTableToRangeOperation(Workbook workbook, Worksheet worksheet, SpreadsheetTable table)
            : base(workbook, worksheet, table.Range)
        {
            _workbook = workbook;
            _table = table;
        }

        public override string Description => "Convert table to range";

        protected override void Apply()
        {
            RewriteWorkbookFormulas((owner, address, formula) =>
                StructuredReferenceFormulaTranslator.ConvertTableReferencesToA1(formula, _workbook, owner, address, _table.Id));
            RewriteWorkbookTableMetadata((owner, candidate) =>
            {
                if (candidate.Id == _table.Id) return candidate;
                var columns = candidate.Columns.Select((column, index) => new SpreadsheetTableColumn(
                    column.Id, column.Name,
                    ConvertFormula(column.CalculatedColumnFormula, owner,
                        GetDataAnchor(candidate.Range, candidate.HasHeaders, candidate.HasTotalsRow, index) ??
                        new CellAddress(candidate.Range.Top, candidate.Range.Left + index)),
                    ConvertFormula(column.TotalsRowFormula, owner,
                        new CellAddress(candidate.Range.Bottom, candidate.Range.Left + index)),
                    column.TotalsRowLabel)).ToArray();
                return CopyTable(candidate, columns: columns);
            });
            if (!Worksheet.RemoveTable(_table.Id)) throw new InvalidOperationException("The Table no longer exists.");
        }

        private string? ConvertFormula(string? formula, Worksheet owner, CellAddress address) => formula is null
            ? null
            : StructuredReferenceFormulaTranslator.ConvertTableReferencesToA1(formula, _workbook, owner, address, _table.Id);
    }

    private void SetVisualOptions(
        Guid tableId,
        bool? showFirstColumn = null,
        bool? showLastColumn = null,
        bool? showRowStripes = null,
        bool? showColumnStripes = null,
        bool? showFilterButtons = null,
        string description = "Set table visual options")
    {
        var table = GetTable(tableId);
        var replacement = CopyTable(
            table,
            showFirstColumn: showFirstColumn,
            showLastColumn: showLastColumn,
            showRowStripes: showRowStripes,
            showColumnStripes: showColumnStripes,
            showFilterButtons: showFilterButtons);
        if (TableMetadataEquals(table, replacement))
        {
            return;
        }
        ReplaceVisualMetadata(table, replacement, description);
    }

    private void ReplaceVisualMetadata(
        SpreadsheetTable previous,
        SpreadsheetTable next,
        string description) =>
        _session.Execute(new UpdateTableVisualMetadataOperation(
            _session.ActiveWorksheet,
            previous,
            next,
            description));

    private void ExecuteIncremental(ISpreadsheetEditOperation operation)
    {
        _session.History.Execute(operation);
        _session.Calculation.PrepareDependencyGraph(_session.Workbook);
        _session.Calculation.RecalculateAffected(
            _session.Workbook,
            operation.Worksheet,
            operation.AffectedRange);
    }

    private sealed class UpdateTableVisualMetadataOperation(
        Worksheet worksheet, SpreadsheetTable previous, SpreadsheetTable next, string description)
        : TableOperationBase(worksheet, previous.Range)
    {
        public override string Description => description;

        public override bool AffectsCalculation => false;

        protected override void CaptureBeforeState() { }

        protected override void RestoreBeforeState() => Replace(previous);

        protected override void Apply() => Replace(next);

        private void Replace(SpreadsheetTable table) => Worksheet.RestoreTables(
            Worksheet.Tables.Select(candidate => candidate.Id == table.Id ? table : candidate),
            table.Range);
    }

    private SpreadsheetTableColumn[] CreateColumns(CellRange range, bool hasHeaders)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var columns = new SpreadsheetTableColumn[range.ColumnCount];
        for (var index = 0; index < columns.Length; index++)
        {
            var header = hasHeaders
                ? _session.ActiveWorksheet.GetValue(
                    new CellAddress(range.Top, range.Left + index))?.ToString()
                : null;
            var name = CreateUniqueColumnName(names, header);
            names.Add(name);
            columns[index] = new SpreadsheetTableColumn(Guid.NewGuid(), name);
        }
        return columns;
    }

    private SpreadsheetTableColumn[] ResizeColumns(
        SpreadsheetTable table,
        int requestedCount)
    {
        if (requestedCount < table.Columns.Count)
        {
            foreach (var column in table.Columns.Skip(requestedCount))
            {
                EnsureColumnIsNotReferenced(table, column);
            }
        }
        var columns = table.Columns
            .Take(requestedCount)
            .Select(static column => column.Copy())
            .ToList();
        var names = columns.Select(static column => column.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        while (columns.Count < requestedCount)
        {
            var name = CreateUniqueColumnName(names, null);
            names.Add(name);
            columns.Add(new SpreadsheetTableColumn(Guid.NewGuid(), name));
        }
        return columns.ToArray();
    }

    private int[] GetDistinctRows(SpreadsheetTable table, int[] offsets)
    {
        var dataRange = table.DataRange!.Value;
        var seen = new HashSet<CellValue[]>(CellValueArrayComparer.Instance);
        var rows = new List<int>(dataRange.RowCount);
        for (var row = dataRange.Top; row <= dataRange.Bottom; row++)
        {
            var key = offsets.Select(offset => _session.ActiveWorksheet.GetCell(
                    new CellAddress(row, table.Range.Left + offset)).Value)
                .ToArray();
            if (seen.Add(key))
            {
                rows.Add(row);
            }
        }
        return rows.ToArray();
    }

    private void EnsureColumnIsNotReferenced(
        SpreadsheetTable table,
        SpreadsheetTableColumn column)
    {
        var sentinel = $"NeraRemoved{column.Id:N}";
        foreach (var worksheet in _session.Workbook.Worksheets)
        {
            foreach (var (address, cell) in worksheet.EnumerateUsedCells())
            {
                if (cell.Formula is not { } formula)
                {
                    continue;
                }
                var rewritten = StructuredReferenceFormulaRewriter.RenameColumn(
                    formula,
                    table.Name,
                    column.Name,
                    sentinel,
                    ReferenceEquals(worksheet, _session.ActiveWorksheet) &&
                    table.Range.Contains(address));
                if (!string.Equals(formula, rewritten, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Table column '{column.Name}' is referenced by a workbook formula and cannot be deleted.");
                }
            }
            foreach (var candidate in worksheet.Tables)
            {
                foreach (var metadataFormula in candidate.Columns.SelectMany(static item =>
                             new[] { item.CalculatedColumnFormula, item.TotalsRowFormula }))
                {
                    if (metadataFormula is null)
                    {
                        continue;
                    }
                    var rewritten = StructuredReferenceFormulaRewriter.RenameColumn(
                        metadataFormula,
                        table.Name,
                        column.Name,
                        sentinel,
                        ReferenceEquals(worksheet, _session.ActiveWorksheet) &&
                        candidate.Id == table.Id);
                    if (!string.Equals(metadataFormula, rewritten, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Table column '{column.Name}' is referenced by Table formula metadata and cannot be deleted.");
                    }
                }
            }
        }
    }

    private void EnsureMoveRangeIsNotDirectlyReferenced(CellRange moveRange)
    {
        foreach (var worksheet in _session.Workbook.Worksheets)
        {
            foreach (var (address, cell) in worksheet.EnumerateUsedCells())
            {
                if (cell.Formula is not { } formula ||
                    !FormulaReferenceAnalyzer.TryGetReferences(
                        formula,
                        out var references))
                {
                    continue;
                }
                var formulaMoves =
                    ReferenceEquals(worksheet, _session.ActiveWorksheet) &&
                    moveRange.Contains(address);
                if (formulaMoves && references.Count > 0 ||
                    references.Any(reference =>
                        (reference.WorksheetName is null &&
                         ReferenceEquals(worksheet, _session.ActiveWorksheet) ||
                         string.Equals(
                             reference.WorksheetName,
                             _session.ActiveWorksheet.Name,
                             StringComparison.OrdinalIgnoreCase)) &&
                        reference.Range.Intersects(moveRange)))
                {
                    throw new InvalidOperationException(
                        "A Table-local compact operation cannot move cells referenced by an external A1 formula.");
                }
            }
        }
    }

    private static void EnsureTableMetadataCanMoveWithoutA1References(
        SpreadsheetTable table)
    {
        foreach (var formula in table.Columns.SelectMany(static column =>
                     new[]
                     {
                         column.CalculatedColumnFormula,
                         column.TotalsRowFormula,
                     }))
        {
            if (formula is not null &&
                FormulaReferenceAnalyzer.TryGetReferences(formula, out var references) &&
                references.Count > 0)
            {
                throw new InvalidOperationException(
                    "A Table-local compact operation cannot safely move A1 Table formula metadata.");
            }
        }
    }

    private static SpreadsheetTableColumn[] TranslateColumnsForLayout(
        SpreadsheetTable source,
        CellRange nextRange,
        IEnumerable<SpreadsheetTableColumn> columns,
        bool hasHeaders,
        bool hasTotalsRow)
    {
        var result = columns.Select(static column => column.Copy()).ToArray();
        for (var nextIndex = 0; nextIndex < result.Length; nextIndex++)
        {
            var column = result[nextIndex];
            if (!source.TryGetColumn(column.Id, out var previous) ||
                previous is null)
            {
                continue;
            }
            var previousIndex = source.GetColumnIndex(column.Id);
            var calculated = TranslateLayoutFormula(
                previous.CalculatedColumnFormula,
                GetDataAnchor(source.Range, source.HasHeaders, source.HasTotalsRow, previousIndex),
                GetDataAnchor(nextRange, hasHeaders, hasTotalsRow, nextIndex));
            var totals = TranslateLayoutFormula(
                previous.TotalsRowFormula,
                source.HasTotalsRow
                    ? new CellAddress(source.Range.Bottom, source.Range.Left + previousIndex)
                    : null,
                hasTotalsRow
                    ? new CellAddress(nextRange.Bottom, nextRange.Left + nextIndex)
                    : null);
            result[nextIndex] = new SpreadsheetTableColumn(
                column.Id,
                column.Name,
                calculated,
                totals,
                column.TotalsRowLabel);
        }
        return result;
    }

    private static CellAddress? GetDataAnchor(
        CellRange range,
        bool hasHeaders,
        bool hasTotalsRow,
        int columnIndex)
    {
        var row = range.Top + (hasHeaders ? 1 : 0);
        var bottom = range.Bottom - (hasTotalsRow ? 1 : 0);
        return row <= bottom
            ? new CellAddress(row, range.Left + columnIndex)
            : null;
    }

    private static string? TranslateLayoutFormula(
        string? formula,
        CellAddress? previousAnchor,
        CellAddress? nextAnchor) =>
        formula is not null &&
        previousAnchor is { } source &&
        nextAnchor is { } destination &&
        source != destination
            ? A1FormulaReferenceTranslator.Translate(
                formula,
                source,
                destination)
            : formula;

    private void EnsureRangeCanOwnTable(CellRange range, Guid? ignoredTableId)
    {
        if (_session.ActiveWorksheet.MergedCells.Ranges.Any(range.Intersects))
        {
            throw new InvalidOperationException("A Table cannot overlap merged cells.");
        }
        if (_session.ActiveWorksheet.GetFormulaSpills().Any(spill =>
                spill.Range.Intersects(range)))
        {
            throw new InvalidOperationException("A Table mutation cannot intersect a dynamic-array spill.");
        }
        if (_session.ActiveWorksheet.Tables.Any(table =>
                table.Id != ignoredTableId && table.Range.Intersects(range)))
        {
            throw new InvalidOperationException("Tables on one worksheet cannot overlap.");
        }
    }

    private void EnsureDestinationIsEmpty(CellRange range)
    {
        if (EnumerateTableCells(_session.ActiveWorksheet, range).Any())
        {
            throw new InvalidOperationException(
                "The Table cannot grow because destination cells contain data.");
        }
    }

    private static IEnumerable<KeyValuePair<CellAddress, CellData>> EnumerateTableCells(Worksheet worksheet, CellRange range)
    {
        var area = (long)range.RowCount * range.ColumnCount;
        if (area <= Math.Min(worksheet.UsedCellCount, 100_000))
        {
            for (var row = range.Top; row <= range.Bottom; row++)
            {
                for (var column = range.Left; column <= range.Right; column++)
                {
                    var address = new CellAddress(row, column);
                    if (worksheet.TryGetCell(address, out var cell)) yield return new(address, cell);
                }
            }
        }
        else
        {
            foreach (var pair in worksheet.EnumerateUsedCells())
            {
                if (range.Contains(pair.Key)) yield return pair;
            }
        }
    }

    private static void EnsureMetadataRowsFit(
        SpreadsheetTable table,
        CellRange range,
        bool? hasHeaders = null)
    {
        var required = ((hasHeaders ?? table.HasHeaders) ? 1 : 0) +
                       (table.HasTotalsRow ? 1 : 0);
        if (range.RowCount < Math.Max(1, required))
        {
            throw new InvalidOperationException(
                "The resized Table is too small for its header and totals rows.");
        }
    }

    private string CreateUniqueTableName()
    {
        var names = _session.Workbook.Tables
            .Select(static table => table.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var index = 1; ; index++)
        {
            var candidate = $"Table{index}";
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static string CreateUniqueColumnName(
        IEnumerable<string> currentNames,
        string? preferred)
    {
        var names = currentNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var root = string.IsNullOrWhiteSpace(preferred)
            ? "Column"
            : preferred.Trim();
        if (!names.Contains(root))
        {
            return root;
        }
        for (var index = 2; ; index++)
        {
            var candidate = $"{root}{index}";
            if (!names.Contains(candidate))
            {
                return candidate;
            }
        }
    }

    private static TableAutoFilter? ResizeFilter(
        SpreadsheetTable table,
        SpreadsheetTableColumn[] columns)
    {
        if (table.AutoFilter is null)
        {
            return null;
        }
        var retained = columns.Select(static column => column.Id).ToHashSet();
        var filterColumns = table.AutoFilter.Columns
            .Where(column => retained.Contains(column.ColumnId))
            .Select(static column => column.Copy())
            .ToArray();
        var conditions = table.AutoFilter.SortState?.Conditions
            .Where(condition => condition.ColumnOffset < columns.Length)
            .ToArray();
        var sort = conditions is { Length: > 0 }
            ? new SpreadsheetFilterSortState(
                conditions,
                table.AutoFilter.SortState!.CaseSensitive)
            : null;
        return filterColumns.Length == 0 && sort is null
            ? null
            : new TableAutoFilter(filterColumns, sort);
    }

    private static TableAutoFilter? MapFilterForColumnInsert(
        TableAutoFilter? filter,
        int insertedOffset)
    {
        if (filter is null)
        {
            return null;
        }
        var sort = filter.SortState is null
            ? null
            : new SpreadsheetFilterSortState(
                filter.SortState.Conditions.Select(condition =>
                    CopySortCondition(
                        condition,
                        condition.ColumnOffset >= insertedOffset
                            ? condition.ColumnOffset + 1
                            : condition.ColumnOffset)),
                filter.SortState.CaseSensitive);
        return new TableAutoFilter(filter.Columns, sort);
    }

    private static TableAutoFilter? MapFilterForColumnDelete(
        TableAutoFilter? filter,
        Guid removedColumnId,
        int removedOffset)
    {
        if (filter is null)
        {
            return null;
        }
        var columns = filter.Columns
            .Where(column => column.ColumnId != removedColumnId)
            .ToArray();
        var conditions = filter.SortState?.Conditions
            .Where(condition => condition.ColumnOffset != removedOffset)
            .Select(condition => CopySortCondition(
                condition,
                condition.ColumnOffset > removedOffset
                    ? condition.ColumnOffset - 1
                    : condition.ColumnOffset))
            .ToArray();
        var sort = conditions is { Length: > 0 }
            ? new SpreadsheetFilterSortState(
                conditions,
                filter.SortState!.CaseSensitive)
            : null;
        return columns.Length == 0 && sort is null
            ? null
            : new TableAutoFilter(columns, sort);
    }

    private static SpreadsheetFilterSortCondition CopySortCondition(
        SpreadsheetFilterSortCondition source,
        int offset) =>
        new(
            offset,
            source.Descending,
            source.SortBy,
            source.CustomList,
            source.Color,
            source.Icon);

    private static SpreadsheetTable CopyTable(
        SpreadsheetTable source,
        CellRange? range = null,
        IEnumerable<SpreadsheetTableColumn>? columns = null,
        bool? hasHeaders = null,
        bool? hasTotalsRow = null,
        string? styleName = null,
        bool replaceStyle = false,
        bool? showFirstColumn = null,
        bool? showLastColumn = null,
        bool? showRowStripes = null,
        bool? showColumnStripes = null,
        TableAutoFilter? autoFilter = null,
        bool replaceAutoFilter = false,
        bool? showFilterButtons = null) =>
        new(
            source.Id,
            source.Name,
            range ?? source.Range,
            columns ?? source.Columns,
            hasHeaders ?? source.HasHeaders,
            hasTotalsRow ?? source.HasTotalsRow,
            replaceStyle ? styleName : source.StyleName,
            showFirstColumn ?? source.ShowFirstColumn,
            showLastColumn ?? source.ShowLastColumn,
            showRowStripes ?? source.ShowRowStripes,
            showColumnStripes ?? source.ShowColumnStripes,
            replaceAutoFilter ? autoFilter : source.AutoFilter,
            showFilterButtons ?? source.ShowFilterButtons);

    private static bool TableMetadataEquals(
        SpreadsheetTable left,
        SpreadsheetTable right) =>
        left.Range == right.Range &&
        left.HasHeaders == right.HasHeaders &&
        left.HasTotalsRow == right.HasTotalsRow &&
        string.Equals(left.StyleName, right.StyleName, StringComparison.OrdinalIgnoreCase) &&
        left.ShowFirstColumn == right.ShowFirstColumn &&
        left.ShowLastColumn == right.ShowLastColumn &&
        left.ShowRowStripes == right.ShowRowStripes &&
        left.ShowColumnStripes == right.ShowColumnStripes &&
        left.ShowFilterButtons == right.ShowFilterButtons;

    private abstract class SelectionAwareTableOperationBase : TableOperationBase,
        IDependencyGraphRebuildOperation
    {
        private readonly SelectionModel _selection;
        private SelectionSnapshot? _selectionBefore;

        protected SelectionAwareTableOperationBase(
            SpreadsheetSession session,
            CellRange affectedRange)
            : base(session.ActiveWorksheet, affectedRange)
        {
            _selection = session.Selection;
        }

        protected SelectionModel Selection => _selection;

        protected override void CaptureBeforeState()
        {
            base.CaptureBeforeState();
            _selectionBefore ??= _selection.Capture();
        }

        protected override void RestoreBeforeState()
        {
            base.RestoreBeforeState();
            _selection.Restore(_selectionBefore ?? throw new InvalidOperationException(
                "Selection state was not captured."));
        }

        protected static void ReplaceCells(
            Worksheet worksheet,
            CellRange range,
            Func<CellAddress, CellAddress?> map)
        {
            var source = EnumerateTableCells(worksheet, range)
                .ToArray();
            var updates = source.ToDictionary(
                static pair => pair.Key,
                static _ => CellData.Empty);
            foreach (var (address, cell) in source)
            {
                if (map(address) is not { } destination)
                {
                    continue;
                }
                updates[destination] = new CellData(
                    cell.Value,
                    cell.Formula,
                    cell.StyleId);
            }
            if (updates.Count > 0)
            {
                worksheet.SetCells(updates);
            }
        }
    }

    private sealed class CreateTableOperation : SelectionAwareTableOperationBase,
        IIncrementalCalculationOperation
    {
        private readonly SpreadsheetTable _table;

        public CreateTableOperation(SpreadsheetSession session, SpreadsheetTable table)
            : base(session, table.Range) => _table = table.Copy();

        public override string Description => "Create table";

        protected override void Apply()
        {
            Worksheet.AddTable(_table);
            if (_table.HasHeaders)
            {
                var headers = _table.Columns.Select((column, index) =>
                    new KeyValuePair<CellAddress, CellData>(
                        new CellAddress(_table.Range.Top, _table.Range.Left + index),
                        new CellData(CellValue.FromText(column.Name), styleId:
                            Worksheet.GetCell(new CellAddress(
                                _table.Range.Top,
                                _table.Range.Left + index)).StyleId)));
                Worksheet.SetCells(headers);
            }
            SpreadsheetTableFormulaProjection.Project(Worksheet, _table);
            Selection.SetActiveCell(_table.Range.TopLeft);
        }
    }

    private sealed class SetTableHeaderOperation : SelectionAwareTableOperationBase,
        IIncrementalCalculationOperation
    {
        private readonly SpreadsheetTable _previous;
        private readonly SpreadsheetTable _next;
        private readonly bool _show;

        public SetTableHeaderOperation(
            SpreadsheetSession session,
            SpreadsheetTable previous,
            SpreadsheetTable next,
            bool show)
            : base(session, Union(previous.Range, next.Range))
        {
            _previous = previous.Copy();
            _next = next.Copy();
            _show = show;
        }

        public override string Description => "Set table header row";

        protected override void Apply()
        {
            ReplaceCells(Worksheet, _previous.Range, address =>
            {
                if (_show)
                {
                    return new CellAddress(
                        address.RowIndex + 1,
                        address.ColumnIndex);
                }
                return address.RowIndex == _previous.Range.Top
                    ? null
                    : new CellAddress(
                        address.RowIndex - 1,
                        address.ColumnIndex);
            });
            SpreadsheetTableFormulaProjection.ApplyReplacement(
                Worksheet,
                _previous,
                _next);
            if (_next.HasHeaders)
            {
                Worksheet.SetCells(_next.Columns.Select((column, index) =>
                {
                    var address = new CellAddress(_next.Range.Top, _next.Range.Left + index);
                    return new KeyValuePair<CellAddress, CellData>(
                        address,
                        new CellData(CellValue.FromText(column.Name), styleId:
                            Worksheet.GetCell(address).StyleId));
                }));
            }
            Selection.SetActiveCell(new CellAddress(
                Math.Clamp(
                    Selection.ActiveCell.RowIndex + (_show ? 1 : -1),
                    _next.Range.Top,
                    _next.Range.Bottom),
                Math.Clamp(
                    Selection.ActiveCell.ColumnIndex,
                    _next.Range.Left,
                    _next.Range.Right)));
        }
    }

    private sealed class SetTableTotalsRowOperation : SelectionAwareTableOperationBase,
        IIncrementalCalculationOperation
    {
        private readonly SpreadsheetTable _previous;
        private readonly SpreadsheetTable _next;
        private readonly bool _show;

        public SetTableTotalsRowOperation(
            SpreadsheetSession session,
            SpreadsheetTable previous,
            SpreadsheetTable next,
            bool show)
            : base(session, Union(previous.Range, next.Range))
        {
            _previous = previous.Copy();
            _next = next.Copy();
            _show = show;
        }

        public override string Description => "Set table totals row";

        protected override void Apply()
        {
            if (!_show)
            {
                var oldTotals = new CellRange(
                    new CellAddress(_previous.Range.Bottom, _previous.Range.Left),
                    new CellAddress(_previous.Range.Bottom, _previous.Range.Right));
                var updates = EnumerateTableCells(Worksheet, oldTotals)
                    .Select(static pair => new KeyValuePair<CellAddress, CellData>(
                        pair.Key,
                        CellData.Empty));
                Worksheet.SetCells(updates);
            }
            SpreadsheetTableFormulaProjection.ApplyReplacement(
                Worksheet,
                _previous,
                _next);
            Selection.SetActiveCell(new CellAddress(
                _next.Range.Bottom,
                Math.Clamp(Selection.ActiveCell.ColumnIndex, _next.Range.Left, _next.Range.Right)));
        }
    }

    private sealed class InsertTableRowOperation : SelectionAwareTableOperationBase,
        IIncrementalCalculationOperation
    {
        private readonly SpreadsheetTable _previous;
        private readonly SpreadsheetTable _next;
        private readonly int _row;

        public InsertTableRowOperation(
            SpreadsheetSession session,
            SpreadsheetTable previous,
            SpreadsheetTable next,
            int row)
            : base(session, Union(previous.Range, next.Range))
        {
            _previous = previous.Copy();
            _next = next.Copy();
            _row = row;
        }

        public override string Description => "Insert table row";

        protected override void Apply()
        {
            ReplaceCells(Worksheet, _previous.Range, address =>
                address.RowIndex >= _row
                    ? new CellAddress(address.RowIndex + 1, address.ColumnIndex)
                    : address);
            SpreadsheetTableFormulaProjection.ApplyReplacement(Worksheet, _previous, _next);
            Selection.SetActiveCell(new CellAddress(
                _row,
                Math.Clamp(Selection.ActiveCell.ColumnIndex, _next.Range.Left, _next.Range.Right)));
        }
    }

    private sealed class DeleteTableRowOperation : SelectionAwareTableOperationBase,
        IIncrementalCalculationOperation
    {
        private readonly SpreadsheetTable _previous;
        private readonly SpreadsheetTable _next;
        private readonly int _row;

        public DeleteTableRowOperation(
            SpreadsheetSession session,
            SpreadsheetTable previous,
            SpreadsheetTable next,
            int row)
            : base(session, previous.Range)
        {
            _previous = previous.Copy();
            _next = next.Copy();
            _row = row;
        }

        public override string Description => "Delete table row";

        protected override void Apply()
        {
            ReplaceCells(Worksheet, _previous.Range, address =>
                address.RowIndex == _row
                    ? null
                    : address.RowIndex > _row
                        ? new CellAddress(address.RowIndex - 1, address.ColumnIndex)
                        : address);
            SpreadsheetTableFormulaProjection.ApplyReplacement(Worksheet, _previous, _next);
            Selection.SetActiveCell(new CellAddress(
                Math.Min(_row, _next.Range.Bottom),
                Math.Clamp(Selection.ActiveCell.ColumnIndex, _next.Range.Left, _next.Range.Right)));
        }
    }

    private sealed class InsertTableColumnOperation : SelectionAwareTableOperationBase,
        IIncrementalCalculationOperation
    {
        private readonly SpreadsheetTable _previous;
        private readonly SpreadsheetTable _next;
        private readonly int _column;

        public InsertTableColumnOperation(
            SpreadsheetSession session,
            SpreadsheetTable previous,
            SpreadsheetTable next,
            int column)
            : base(session, Union(previous.Range, next.Range))
        {
            _previous = previous.Copy();
            _next = next.Copy();
            _column = column;
        }

        public override string Description => "Insert table column";

        protected override void Apply()
        {
            ReplaceCells(Worksheet, _previous.Range, address =>
                address.ColumnIndex >= _column
                    ? new CellAddress(address.RowIndex, address.ColumnIndex + 1)
                    : address);
            SpreadsheetTableFormulaProjection.ApplyReplacement(Worksheet, _previous, _next);
            if (_next.HasHeaders)
            {
                var offset = _column - _next.Range.Left;
                var address = new CellAddress(_next.Range.Top, _column);
                Worksheet.SetCell(address, new CellData(
                    CellValue.FromText(_next.Columns[offset].Name),
                    styleId: Worksheet.GetCell(address).StyleId));
            }
            Selection.SetActiveCell(new CellAddress(
                Math.Clamp(Selection.ActiveCell.RowIndex, _next.Range.Top, _next.Range.Bottom),
                _column));
        }
    }

    private sealed class DeleteTableColumnOperation : SelectionAwareTableOperationBase,
        IIncrementalCalculationOperation
    {
        private readonly SpreadsheetTable _previous;
        private readonly SpreadsheetTable _next;
        private readonly int _column;

        public DeleteTableColumnOperation(
            SpreadsheetSession session,
            SpreadsheetTable previous,
            SpreadsheetTable next,
            int column)
            : base(session, previous.Range)
        {
            _previous = previous.Copy();
            _next = next.Copy();
            _column = column;
        }

        public override string Description => "Delete table column";

        protected override void Apply()
        {
            ReplaceCells(Worksheet, _previous.Range, address =>
                address.ColumnIndex == _column
                    ? null
                    : address.ColumnIndex > _column
                        ? new CellAddress(address.RowIndex, address.ColumnIndex - 1)
                        : address);
            SpreadsheetTableFormulaProjection.ApplyReplacement(Worksheet, _previous, _next);
            Selection.SetActiveCell(new CellAddress(
                Math.Clamp(Selection.ActiveCell.RowIndex, _next.Range.Top, _next.Range.Bottom),
                Math.Min(_column, _next.Range.Right)));
        }
    }

    private sealed class RemoveDuplicateTableRowsOperation : SelectionAwareTableOperationBase,
        IIncrementalCalculationOperation
    {
        private readonly SpreadsheetTable _previous;
        private readonly SpreadsheetTable _next;
        private readonly int[] _retainedRows;

        public RemoveDuplicateTableRowsOperation(
            SpreadsheetSession session,
            SpreadsheetTable previous,
            SpreadsheetTable next,
            int[] retainedRows)
            : base(session, previous.Range)
        {
            _previous = previous.Copy();
            _next = next.Copy();
            _retainedRows = retainedRows.ToArray();
        }

        public override string Description => "Remove duplicate table rows";

        protected override void Apply()
        {
            var rowMap = _retainedRows.Select((source, index) => new
                {
                    Source = source,
                    Destination = _previous.DataRange!.Value.Top + index,
                })
                .ToDictionary(static item => item.Source, static item => item.Destination);
            var totalsRow = _previous.HasTotalsRow ? _previous.Range.Bottom : (int?)null;
            ReplaceCells(Worksheet, _previous.Range, address =>
            {
                if (address.RowIndex == _previous.Range.Top && _previous.HasHeaders)
                {
                    return address;
                }
                if (totalsRow == address.RowIndex)
                {
                    return new CellAddress(_next.Range.Bottom, address.ColumnIndex);
                }
                return rowMap.TryGetValue(address.RowIndex, out var destination)
                    ? new CellAddress(destination, address.ColumnIndex)
                    : null;
            });
            SpreadsheetTableFormulaProjection.ApplyReplacement(Worksheet, _previous, _next);
            Selection.SetActiveCell(new CellAddress(
                Math.Min(Selection.ActiveCell.RowIndex, _next.Range.Bottom),
                Math.Clamp(Selection.ActiveCell.ColumnIndex, _next.Range.Left, _next.Range.Right)));
        }
    }

    private sealed class CellValueArrayComparer : IEqualityComparer<CellValue[]>
    {
        public static CellValueArrayComparer Instance { get; } = new();

        public bool Equals(CellValue[]? left, CellValue[]? right) =>
            ReferenceEquals(left, right) ||
            left is not null && right is not null && left.SequenceEqual(right);

        public int GetHashCode(CellValue[] values)
        {
            var hash = new HashCode();
            foreach (var value in values)
            {
                hash.Add(value);
            }
            return hash.ToHashCode();
        }
    }
}
