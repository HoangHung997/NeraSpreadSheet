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

    private void Execute(ISpreadsheetEditOperation operation)
    {
        _session.History.Execute(operation);
        _session.Calculation.Recalculate(_session.Workbook);
    }

    private abstract class TableOperationBase
        : ISpreadsheetEditOperation
    {
        private SpreadsheetTable[]? _tablesBefore;

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
        }

        protected virtual void RestoreBeforeState()
        {
            if (_tablesBefore is null)
            {
                throw new InvalidOperationException(
                    "The table operation has not been executed yet.");
            }

            Worksheet.RestoreTables(
                _tablesBefore,
                AffectedRange);
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
        }

        protected override void RestoreBeforeState()
        {
            base.RestoreBeforeState();
            if (_formulaCellsBefore is null)
            {
                throw new InvalidOperationException(
                    "Formula state was not captured.");
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

        protected override void Apply() =>
            Worksheet.AddTable(_table);
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
        }
    }
}
