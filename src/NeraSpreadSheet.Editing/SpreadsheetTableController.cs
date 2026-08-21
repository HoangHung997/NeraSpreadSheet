using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetTableController
{
    private readonly SpreadsheetSession _session;

    public SpreadsheetTableController(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public void Add(SpreadsheetTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        Execute(new AddTableOperation(
            _session.ActiveWorksheet,
            table));
    }

    public bool Remove(Guid tableId)
    {
        if (!_session.ActiveWorksheet.TryGetTable(
                tableId,
                out var table) ||
            table is null)
        {
            return false;
        }

        Execute(new RemoveTableOperation(
            _session.ActiveWorksheet,
            table));
        return true;
    }

    public void RenameTable(Guid tableId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var table = GetTable(tableId);
        Execute(new RenameTableOperation(
            _session.Workbook,
            _session.ActiveWorksheet,
            table,
            name));
    }

    public void RenameColumn(
        Guid tableId,
        Guid columnId,
        string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var table = GetTable(tableId);
        var column = table.Columns.FirstOrDefault(candidate =>
            candidate.Id == columnId)
            ?? throw new KeyNotFoundException(
                $"Table column '{columnId}' was not found.");
        Execute(new RenameTableColumnOperation(
            _session.Workbook,
            _session.ActiveWorksheet,
            table,
            column,
            name));
    }

    public void SetCalculatedColumnFormula(
        Guid tableId,
        Guid columnId,
        string? formula)
    {
        var table = GetTable(tableId);
        var replacement =
            SpreadsheetTableFormulaProjection
                .WithCalculatedColumnFormula(
                    table,
                    columnId,
                    formula);
        Execute(new UpdateTableMetadataOperation(
            _session.ActiveWorksheet,
            table,
            replacement,
            formula is null
                ? "Clear calculated column formula"
                : "Set calculated column formula"));
    }

    public void SetTotalsRowFormula(
        Guid tableId,
        Guid columnId,
        string? formula)
    {
        var table = GetTable(tableId);
        EnsureTotalsRow(table);
        var replacement =
            SpreadsheetTableFormulaProjection
                .WithTotalsRowFormula(
                    table,
                    columnId,
                    formula);
        Execute(new UpdateTableMetadataOperation(
            _session.ActiveWorksheet,
            table,
            replacement,
            formula is null
                ? "Clear totals row formula"
                : "Set totals row formula"));
    }

    public void SetTotalsRowLabel(
        Guid tableId,
        Guid columnId,
        string? label)
    {
        var table = GetTable(tableId);
        EnsureTotalsRow(table);
        var replacement =
            SpreadsheetTableFormulaProjection
                .WithTotalsRowLabel(
                    table,
                    columnId,
                    label);
        Execute(new UpdateTableMetadataOperation(
            _session.ActiveWorksheet,
            table,
            replacement,
            label is null
                ? "Clear totals row label"
                : "Set totals row label"));
    }

    public void SetTotalsRowFunction(
        Guid tableId,
        Guid columnId,
        SpreadsheetTableTotalsFunction function,
        string? customFormula = null)
    {
        var table = GetTable(tableId);
        EnsureTotalsRow(table);
        var replacement =
            SpreadsheetTableFormulaProjection
                .WithTotalsRowFunction(
                    table,
                    columnId,
                    function,
                    customFormula);
        Execute(new UpdateTableMetadataOperation(
            _session.ActiveWorksheet,
            table,
            replacement,
            function == SpreadsheetTableTotalsFunction.None
                ? "Clear totals row function"
                : "Set totals row function"));
    }

    public void SetAutoFilter(
        Guid tableId,
        TableAutoFilter? autoFilter)
    {
        var table = GetTable(tableId);
        Execute(new SetTableAutoFilterOperation(
            _session.ActiveWorksheet,
            table,
            autoFilter));
    }

    public void ClearAutoFilter(Guid tableId) =>
        SetAutoFilter(tableId, null);

    private SpreadsheetTable GetTable(Guid tableId)
    {
        if (_session.ActiveWorksheet.TryGetTable(
                tableId,
                out var table) &&
            table is not null)
        {
            return table;
        }

        throw new KeyNotFoundException(
            $"Table '{tableId}' was not found.");
    }

    private static void EnsureTotalsRow(SpreadsheetTable table)
    {
        if (!table.HasTotalsRow)
        {
            throw new InvalidOperationException(
                $"Table '{table.Name}' does not have a totals row.");
        }
    }

    private void Execute(ISpreadsheetEditOperation operation)
    {
        _session.History.Execute(operation);
        _session.Calculation.Recalculate(_session.Workbook);
    }

    private abstract class TableOperationBase
        : ISpreadsheetEditOperation
    {
        private SpreadsheetTable[]? _tablesBefore;
        private KeyValuePair<CellAddress, CellData>[]? _cellsBefore;

        protected TableOperationBase(
            Worksheet worksheet,
            CellRange affectedRange)
        {
            Worksheet = worksheet ??
                throw new ArgumentNullException(nameof(worksheet));
            AffectedRange = affectedRange;
        }

        public abstract string Description { get; }

        public Worksheet Worksheet { get; }

        public CellRange AffectedRange { get; }

        public void Execute()
        {
            CaptureBeforeState();
            try
            {
                Apply();
            }
            catch
            {
                RestoreBeforeState();
                throw;
            }
        }

        public void Undo() => RestoreBeforeState();

        protected abstract void Apply();

        protected virtual void CaptureBeforeState()
        {
            _tablesBefore ??= Worksheet.Tables
                .Select(static table => table.Copy())
                .ToArray();
            _cellsBefore ??= Worksheet.EnumerateUsedCells()
                .Where(pair => AffectedRange.Contains(pair.Key))
                .ToArray();
        }

        protected virtual void RestoreBeforeState()
        {
            if (_tablesBefore is null ||
                _cellsBefore is null)
            {
                throw new InvalidOperationException(
                    "The table operation has not been executed yet.");
            }

            Worksheet.RestoreTables(
                _tablesBefore,
                AffectedRange);
            var updates = Worksheet.EnumerateUsedCells()
                .Where(pair => AffectedRange.Contains(pair.Key))
                .ToDictionary(
                    static pair => pair.Key,
                    static _ => CellData.Empty);
            foreach (var pair in _cellsBefore)
            {
                updates[pair.Key] = pair.Value;
            }
            if (updates.Count > 0)
            {
                Worksheet.SetCells(updates);
            }
        }
    }

    private abstract class FormulaRewritingTableOperationBase
        : TableOperationBase
    {
        private readonly Workbook _workbook;
        private Dictionary<
            Worksheet,
            KeyValuePair<CellAddress, CellData>[]>?
            _formulaCellsBefore;
        private Dictionary<Worksheet, SpreadsheetTable[]>?
            _externalTablesBefore;

        protected FormulaRewritingTableOperationBase(
            Workbook workbook,
            Worksheet worksheet,
            CellRange affectedRange)
            : base(worksheet, affectedRange)
        {
            _workbook = workbook ??
                throw new ArgumentNullException(nameof(workbook));
        }

        protected void RewriteWorkbookFormulas(
            Func<Worksheet, CellAddress, string, string> rewrite)
        {
            ArgumentNullException.ThrowIfNull(rewrite);
            foreach (var worksheet in _workbook.Worksheets)
            {
                var updates = worksheet.EnumerateUsedCells()
                    .Where(static pair =>
                        pair.Value.Formula is not null)
                    .Select(pair =>
                    {
                        var formula = pair.Value.Formula!;
                        var rewritten = rewrite(
                            worksheet,
                            pair.Key,
                            formula);
                        return new
                        {
                            pair.Key,
                            Current = pair.Value,
                            Rewritten = rewritten,
                        };
                    })
                    .Where(static item =>
                        !string.Equals(
                            item.Current.Formula,
                            item.Rewritten,
                            StringComparison.Ordinal))
                    .Select(static item =>
                        new KeyValuePair<CellAddress, CellData>(
                            item.Key,
                            new CellData(
                                item.Current.Value,
                                item.Rewritten,
                                item.Current.StyleId)))
                    .ToArray();
                if (updates.Length > 0)
                {
                    worksheet.SetCells(updates);
                }
            }
        }

        protected void RewriteWorkbookTableMetadata(
            Func<Worksheet, SpreadsheetTable, SpreadsheetTable> rewrite)
        {
            ArgumentNullException.ThrowIfNull(rewrite);
            foreach (var worksheet in _workbook.Worksheets)
            {
                var changedRanges = new List<CellRange>();
                var tables = worksheet.Tables
                    .Select(table =>
                    {
                        var rewritten = rewrite(worksheet, table);
                        if (!ReferenceEquals(rewritten, table))
                        {
                            changedRanges.Add(Union(
                                table.Range,
                                rewritten.Range));
                        }
                        return rewritten;
                    })
                    .ToArray();
                if (changedRanges.Count > 0)
                {
                    worksheet.RestoreTables(
                        tables,
                        Union(changedRanges));
                }
            }
        }

        protected override void CaptureBeforeState()
        {
            base.CaptureBeforeState();
            _formulaCellsBefore ??= _workbook.Worksheets
                .ToDictionary(
                    static worksheet => worksheet,
                    static worksheet => worksheet.EnumerateUsedCells()
                        .Where(static pair =>
                            pair.Value.Formula is not null)
                        .ToArray());
            _externalTablesBefore ??= _workbook.Worksheets
                .Where(worksheet =>
                    !ReferenceEquals(worksheet, Worksheet))
                .ToDictionary(
                    static worksheet => worksheet,
                    static worksheet => worksheet.Tables
                        .Select(static table => table.Copy())
                        .ToArray());
        }

        protected override void RestoreBeforeState()
        {
            base.RestoreBeforeState();
            if (_formulaCellsBefore is null ||
                _externalTablesBefore is null)
            {
                throw new InvalidOperationException(
                    "Formula and external Table state was not captured.");
            }

            foreach (var (worksheet, tables) in
                     _externalTablesBefore)
            {
                worksheet.RestoreTables(
                    tables,
                    CalculateTableSignalRange(tables));
            }
            foreach (var (worksheet, formulas) in
                     _formulaCellsBefore)
            {
                if (formulas.Length > 0)
                {
                    worksheet.SetCells(formulas);
                }
            }
        }

        private static CellRange CalculateTableSignalRange(
            SpreadsheetTable[] tables)
        {
            if (tables.Length == 0)
            {
                return new CellRange(default, default);
            }
            return Union(tables.Select(static table => table.Range));
        }
    }

    private sealed class AddTableOperation
        : TableOperationBase
    {
        private readonly SpreadsheetTable _table;

        public AddTableOperation(
            Worksheet worksheet,
            SpreadsheetTable table)
            : base(worksheet, table.Range)
        {
            _table = table.Copy();
        }

        public override string Description => "Add table";

        protected override void Apply()
        {
            Worksheet.AddTable(_table);
            SpreadsheetTableFormulaProjection.Project(
                Worksheet,
                _table);
        }
    }

    private sealed class RemoveTableOperation
        : TableOperationBase
    {
        private readonly Guid _tableId;

        public RemoveTableOperation(
            Worksheet worksheet,
            SpreadsheetTable table)
            : base(worksheet, table.Range)
        {
            _tableId = table.Id;
        }

        public override string Description => "Remove table";

        protected override void Apply()
        {
            if (!Worksheet.RemoveTable(_tableId))
            {
                throw new InvalidOperationException(
                    "The table does not exist.");
            }
        }
    }

    private sealed class UpdateTableMetadataOperation
        : TableOperationBase
    {
        private readonly SpreadsheetTable _previous;
        private readonly SpreadsheetTable _next;
        private readonly string _description;

        public UpdateTableMetadataOperation(
            Worksheet worksheet,
            SpreadsheetTable previous,
            SpreadsheetTable next,
            string description)
            : base(
                worksheet,
                Union(previous.Range, next.Range))
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(description);
            _previous = previous.Copy();
            _next = next.Copy();
            _description = description.Trim();
        }

        public override string Description => _description;

        protected override void Apply() =>
            SpreadsheetTableFormulaProjection.ApplyReplacement(
                Worksheet,
                _previous,
                _next);
    }

    private sealed class SetTableAutoFilterOperation
        : TableOperationBase
    {
        private readonly Guid _tableId;
        private readonly TableAutoFilter? _autoFilter;

        public SetTableAutoFilterOperation(
            Worksheet worksheet,
            SpreadsheetTable table,
            TableAutoFilter? autoFilter)
            : base(worksheet, table.Range)
        {
            _tableId = table.Id;
            _autoFilter = autoFilter?.Copy();
        }

        public override string Description =>
            _autoFilter is null
                ? "Clear table filter"
                : "Set table filter";

        protected override void Apply() =>
            Worksheet.SetTableAutoFilter(
                _tableId,
                _autoFilter);
    }

    private sealed class RenameTableOperation
        : FormulaRewritingTableOperationBase
    {
        private readonly Guid _tableId;
        private readonly string _oldName;
        private readonly string _newName;

        public RenameTableOperation(
            Workbook workbook,
            Worksheet worksheet,
            SpreadsheetTable table,
            string newName)
            : base(workbook, worksheet, table.Range)
        {
            _tableId = table.Id;
            _oldName = table.Name;
            _newName = newName;
        }

        public override string Description => "Rename table";

        protected override void Apply()
        {
            Worksheet.RenameTable(_tableId, _newName);
            RewriteWorkbookFormulas(
                (_, _, formula) =>
                    StructuredReferenceFormulaRewriter.RenameTable(
                        formula,
                        _oldName,
                        _newName));
            RewriteWorkbookTableMetadata(
                (_, table) =>
                    SpreadsheetTableFormulaProjection
                        .RewriteStructuredReferences(
                            table,
                            formula =>
                                StructuredReferenceFormulaRewriter
                                    .RenameTable(
                                        formula,
                                        _oldName,
                                        _newName)));
        }
    }

    private sealed class RenameTableColumnOperation
        : FormulaRewritingTableOperationBase
    {
        private readonly Guid _tableId;
        private readonly Guid _columnId;
        private readonly string _tableName;
        private readonly string _oldName;
        private readonly string _newName;
        private readonly CellRange _tableRange;

        public RenameTableColumnOperation(
            Workbook workbook,
            Worksheet worksheet,
            SpreadsheetTable table,
            SpreadsheetTableColumn column,
            string newName)
            : base(workbook, worksheet, table.Range)
        {
            _tableId = table.Id;
            _columnId = column.Id;
            _tableName = table.Name;
            _oldName = column.Name;
            _newName = newName;
            _tableRange = table.Range;
        }

        public override string Description => "Rename table column";

        protected override void Apply()
        {
            Worksheet.RenameTableColumn(
                _tableId,
                _columnId,
                _newName);
            RewriteWorkbookFormulas(
                (worksheet, address, formula) =>
                    StructuredReferenceFormulaRewriter.RenameColumn(
                        formula,
                        _tableName,
                        _oldName,
                        _newName,
                        rewriteImplicitReferences:
                            ReferenceEquals(worksheet, Worksheet) &&
                            _tableRange.Contains(address)));
            RewriteWorkbookTableMetadata(
                (worksheet, table) =>
                    SpreadsheetTableFormulaProjection
                        .RewriteStructuredReferences(
                            table,
                            formula =>
                                StructuredReferenceFormulaRewriter
                                    .RenameColumn(
                                        formula,
                                        _tableName,
                                        _oldName,
                                        _newName,
                                        rewriteImplicitReferences:
                                            ReferenceEquals(
                                                worksheet,
                                                Worksheet) &&
                                            table.Id == _tableId)));
        }
    }

    private static CellRange Union(
        IEnumerable<CellRange> ranges)
    {
        var materialized = ranges.ToArray();
        if (materialized.Length == 0)
        {
            return new CellRange(default, default);
        }
        return new CellRange(
            new CellAddress(
                materialized.Min(static range => range.Top),
                materialized.Min(static range => range.Left)),
            new CellAddress(
                materialized.Max(static range => range.Bottom),
                materialized.Max(static range => range.Right)));
    }

    private static CellRange Union(
        CellRange left,
        CellRange right) =>
        new(
            new CellAddress(
                Math.Min(left.Top, right.Top),
                Math.Min(left.Left, right.Left)),
            new CellAddress(
                Math.Max(left.Bottom, right.Bottom),
                Math.Max(left.Right, right.Right)));
}
