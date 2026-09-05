using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>One bounded style-gallery entry used by every Table Design host.</summary>
public sealed record SpreadsheetTableStyleGalleryItem(
    string Id,
    string Name,
    string Group,
    IReadOnlyList<TableStylePreviewCell> Preview);

/// <summary>Immutable Table Design state projected from the active session selection.</summary>
public sealed record SpreadsheetTableDesignSnapshot(
    bool HasSelection,
    bool IsInTable,
    Guid? TableId,
    Guid? ColumnId,
    string? TableName,
    CellRange? Range,
    bool HasHeaders,
    bool HasTotalsRow,
    bool ShowFirstColumn,
    bool ShowLastColumn,
    bool ShowRowStripes,
    bool ShowColumnStripes,
    bool ShowFilterButtons,
    string? StyleName,
    string? CalculatedColumnFormula,
    SpreadsheetTableTotalsFunction TotalsFunction,
    IReadOnlyList<SpreadsheetTableStyleGalleryItem> Styles);

/// <summary>
/// Projects contextual Table Design state and delegates every mutation to
/// <see cref="SpreadsheetSession.Tables"/>.
/// </summary>
public sealed class SpreadsheetTableDesignController : IDisposable
{
    /// <summary>Maximum number of preview entries exposed by one gallery snapshot.</summary>
    public const int MaximumStyleGalleryEntries = 256;

    private readonly SpreadsheetSession _session;
    private SpreadsheetTableDesignSnapshot _snapshot;
    private Worksheet _observedWorksheet;
    private IReadOnlyList<SpreadsheetTableStyleGalleryItem>? _styles;
    private bool _disposed;

    public SpreadsheetTableDesignController(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _observedWorksheet = session.ActiveWorksheet;
        _snapshot = CreateSnapshot();
        _session.Selection.Changed += OnContextSourceChanged;
        _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
        _observedWorksheet.CellsChanged += OnContextSourceChanged;
    }

    /// <summary>Raised only after the effective contextual state changes.</summary>
    public event EventHandler? ContextChanged;

    /// <summary>Gets the latest immutable active-selection snapshot.</summary>
    public SpreadsheetTableDesignSnapshot Snapshot => _snapshot;

    /// <summary>Reprojects current selection, Table metadata, and the bounded style gallery.</summary>
    public SpreadsheetTableDesignSnapshot Refresh()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var next = CreateSnapshot();
        if (!EquivalentContext(_snapshot, next))
        {
            _snapshot = next;
            ContextChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            _snapshot = next;
        }
        return _snapshot;
    }

    /// <summary>
    /// Invalidates bounded style previews after an application changes the
    /// workbook TableStyleCatalog or theme.
    /// </summary>
    public SpreadsheetTableDesignSnapshot RefreshStyleGallery()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _styles = null;
        _snapshot = CreateSnapshot();
        ContextChanged?.Invoke(this, EventArgs.Empty);
        return _snapshot;
    }

    /// <summary>Creates a Table from the primary selection.</summary>
    public SpreadsheetTable CreateTable(string? name = null, bool hasHeaders = true)
    {
        var range = _session.Selection.Ranges[0];
        var table = _session.Tables.Create(range, name, hasHeaders);
        _session.Selection.SetActiveCell(table.Range.TopLeft);
        Refresh();
        return table;
    }

    /// <summary>Renames the active Table.</summary>
    public void RenameTable(string name) =>
        _session.Tables.RenameTable(RequireTable().Id, name);

    /// <summary>Resizes the active Table.</summary>
    public void ResizeTable(CellRange range) =>
        _session.Tables.Resize(RequireTable().Id, range);

    /// <summary>Sets a calculated-column formula on the active Table column.</summary>
    public void SetCalculatedColumnFormula(string? formula)
    {
        var (table, column) = RequireColumn();
        _session.Tables.SetCalculatedColumnFormula(table.Id, column.Id, formula);
    }

    /// <summary>Sets a totals function on the active Table column.</summary>
    public void SetTotalsFunction(
        SpreadsheetTableTotalsFunction function,
        string? customFormula = null)
    {
        var (table, column) = RequireColumn();
        _session.Tables.SetTotalsRowFunction(
            table.Id,
            column.Id,
            function,
            customFormula);
    }

    /// <summary>Applies a style selected from the workbook TableStyleCatalog.</summary>
    public void SetStyle(string styleName) =>
        _session.Tables.SetStyle(RequireTable().Id, styleName);

    /// <summary>Inserts a data row at the active row.</summary>
    public void InsertRow()
    {
        var table = RequireTable();
        var row = table.DataRange is { } dataRange
            ? _session.Selection.ActiveCell.RowIndex >= dataRange.Top &&
              _session.Selection.ActiveCell.RowIndex <= dataRange.Bottom
                ? _session.Selection.ActiveCell.RowIndex
                : dataRange.Bottom + 1
            : table.Range.Top + (table.HasHeaders ? 1 : 0);
        _session.Tables.InsertRow(table.Id, row);
    }

    /// <summary>Deletes the active data row.</summary>
    public void DeleteRow() =>
        _session.Tables.DeleteRow(
            RequireTable().Id,
            _session.Selection.ActiveCell.RowIndex);

    /// <summary>Inserts a Table column before the active column.</summary>
    public SpreadsheetTableColumn InsertColumn(string? name = null) =>
        _session.Tables.InsertColumn(
            RequireTable().Id,
            _session.Selection.ActiveCell.ColumnIndex,
            name);

    /// <summary>Deletes the active Table column after reference validation.</summary>
    public void DeleteColumn()
    {
        var (table, column) = RequireColumn();
        _session.Tables.DeleteColumn(table.Id, column.Id);
    }

    /// <summary>Removes duplicate rows using all columns unless stable IDs are supplied.</summary>
    public int RemoveDuplicates(IEnumerable<Guid>? columnIds = null) =>
        _session.Tables.RemoveDuplicates(RequireTable().Id, columnIds);

    /// <summary>Converts the active Table to a normal range.</summary>
    public bool ConvertToRange() =>
        _session.Tables.ConvertToRange(RequireTable().Id);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _session.Selection.Changed -= OnContextSourceChanged;
        _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
        _observedWorksheet.CellsChanged -= OnContextSourceChanged;
        GC.SuppressFinalize(this);
    }

    private SpreadsheetTableDesignSnapshot CreateSnapshot()
    {
        var active = _session.Selection.ActiveCell;
        var hasSelection = _session.Selection.Ranges.Count > 0;
        if (!hasSelection ||
            !_session.ActiveWorksheet.TryGetTable(active, out var table) ||
            table is null)
        {
            return new SpreadsheetTableDesignSnapshot(
                hasSelection,
                false,
                null,
                null,
                null,
                null,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                null,
                null,
                SpreadsheetTableTotalsFunction.None,
                []);
        }

        var columnOffset = active.ColumnIndex - table.Range.Left;
        var column = columnOffset >= 0 && columnOffset < table.Columns.Count
            ? table.Columns[columnOffset]
            : null;
        return new SpreadsheetTableDesignSnapshot(
            true,
            true,
            table.Id,
            column?.Id,
            table.Name,
            table.Range,
            table.HasHeaders,
            table.HasTotalsRow,
            table.ShowFirstColumn,
            table.ShowLastColumn,
            table.ShowRowStripes,
            table.ShowColumnStripes,
            table.ShowFilterButtons,
            table.StyleName,
            column?.CalculatedColumnFormula,
            ResolveTotalsFunction(column?.TotalsRowFormula),
            CreateStyleGallery());
    }

    private IReadOnlyList<SpreadsheetTableStyleGalleryItem> CreateStyleGallery()
    {
        if (_styles is not null)
        {
            return _styles;
        }
        var catalog = _session.Workbook.TableStyles;
        var entries = new List<SpreadsheetTableStyleGalleryItem>();
        foreach (var entry in catalog.BuiltInGallery)
        {
            if (entries.Count == MaximumStyleGalleryEntries)
            {
                break;
            }
            var definition = catalog.Get(entry.Name);
            entries.Add(new SpreadsheetTableStyleGalleryItem(
                entry.Id,
                entry.Name,
                entry.Group,
                Array.AsReadOnly(TableStylePreview.Create(
                    definition,
                    _session.Workbook.Theme).ToArray())));
        }
        foreach (var definition in catalog.CustomStyles)
        {
            if (entries.Count == MaximumStyleGalleryEntries)
            {
                break;
            }
            entries.Add(new SpreadsheetTableStyleGalleryItem(
                definition.Id,
                definition.Name,
                "Custom",
                Array.AsReadOnly(TableStylePreview.Create(
                    definition,
                    _session.Workbook.Theme).ToArray())));
        }
        _styles = Array.AsReadOnly(entries.ToArray());
        return _styles;
    }

    private SpreadsheetTable RequireTable()
    {
        Refresh();
        if (_snapshot.TableId is not { } id ||
            !_session.ActiveWorksheet.TryGetTable(id, out var table) ||
            table is null)
        {
            throw new InvalidOperationException(
                "The active selection does not belong to a Table.");
        }
        return table;
    }

    private (SpreadsheetTable Table, SpreadsheetTableColumn Column) RequireColumn()
    {
        var table = RequireTable();
        if (_snapshot.ColumnId is not { } id ||
            !table.TryGetColumn(id, out var column) ||
            column is null)
        {
            throw new InvalidOperationException(
                "The active selection does not identify a Table column.");
        }
        return (table, column);
    }

    private void OnActiveWorksheetChanged(object? sender, EventArgs e)
    {
        _observedWorksheet.CellsChanged -= OnContextSourceChanged;
        _observedWorksheet = _session.ActiveWorksheet;
        _observedWorksheet.CellsChanged += OnContextSourceChanged;
        Refresh();
    }

    private void OnContextSourceChanged(object? sender, EventArgs e) => Refresh();

    private static SpreadsheetTableTotalsFunction ResolveTotalsFunction(string? formula)
    {
        if (formula is null)
        {
            return SpreadsheetTableTotalsFunction.None;
        }
        var normalized = formula.Replace(" ", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();
        return normalized.StartsWith("=SUBTOTAL(101,", StringComparison.Ordinal)
            ? SpreadsheetTableTotalsFunction.Average
            : normalized.StartsWith("=SUBTOTAL(102,", StringComparison.Ordinal)
                ? SpreadsheetTableTotalsFunction.CountNumbers
                : normalized.StartsWith("=SUBTOTAL(103,", StringComparison.Ordinal)
                    ? SpreadsheetTableTotalsFunction.Count
                    : normalized.StartsWith("=SUBTOTAL(104,", StringComparison.Ordinal)
                        ? SpreadsheetTableTotalsFunction.Maximum
                        : normalized.StartsWith("=SUBTOTAL(105,", StringComparison.Ordinal)
                            ? SpreadsheetTableTotalsFunction.Minimum
                            : normalized.StartsWith("=SUBTOTAL(109,", StringComparison.Ordinal)
                                ? SpreadsheetTableTotalsFunction.Sum
                                : SpreadsheetTableTotalsFunction.Custom;
    }

    private static bool EquivalentContext(
        SpreadsheetTableDesignSnapshot left,
        SpreadsheetTableDesignSnapshot right) =>
        left.HasSelection == right.HasSelection &&
        left.IsInTable == right.IsInTable &&
        left.TableId == right.TableId &&
        left.ColumnId == right.ColumnId &&
        left.TableName == right.TableName &&
        left.Range == right.Range &&
        left.HasHeaders == right.HasHeaders &&
        left.HasTotalsRow == right.HasTotalsRow &&
        left.ShowFirstColumn == right.ShowFirstColumn &&
        left.ShowLastColumn == right.ShowLastColumn &&
        left.ShowRowStripes == right.ShowRowStripes &&
        left.ShowColumnStripes == right.ShowColumnStripes &&
        left.ShowFilterButtons == right.ShowFilterButtons &&
        left.StyleName == right.StyleName &&
        left.CalculatedColumnFormula == right.CalculatedColumnFormula &&
        left.TotalsFunction == right.TotalsFunction;
}

/// <summary>Stable command identities for Table lifecycle and Table Design.</summary>
public static class SpreadsheetTableCommandIds
{
    public static CommandId Create { get; } = new("Table.Create");
    public static CommandId Rename { get; } = new("Table.Rename");
    public static CommandId Resize { get; } = new("Table.Resize");
    public static CommandId HeaderRow { get; } = new("Table.HeaderRow");
    public static CommandId TotalsRow { get; } = new("Table.TotalsRow");
    public static CommandId FirstColumn { get; } = new("Table.FirstColumn");
    public static CommandId LastColumn { get; } = new("Table.LastColumn");
    public static CommandId BandedRows { get; } = new("Table.BandedRows");
    public static CommandId BandedColumns { get; } = new("Table.BandedColumns");
    public static CommandId FilterButtons { get; } = new("Table.FilterButtons");
    public static CommandId Style { get; } = new("Table.Style");
    public static CommandId CalculatedColumn { get; } = new("Table.CalculatedColumn");
    public static CommandId TotalsFunction { get; } = new("Table.TotalsFunction");
    public static CommandId InsertRow { get; } = new("Table.Row.Insert");
    public static CommandId DeleteRow { get; } = new("Table.Row.Delete");
    public static CommandId InsertColumn { get; } = new("Table.Column.Insert");
    public static CommandId DeleteColumn { get; } = new("Table.Column.Delete");
    public static CommandId RemoveDuplicates { get; } = new("Table.RemoveDuplicates");
    public static CommandId ConvertToRange { get; } = new("Table.ConvertToRange");
}

internal static class SpreadsheetTableCommandCatalog
{
    public static void Register(CommandRegistry registry, SpreadsheetSession session)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(session);
        Register(registry, session, SpreadsheetTableCommandIds.Create, "Tạo Bảng",
            "insert.table", state => new CommandState(!state.IsInTable),
            (_, parameter) => session.TableDesign.CreateTable(parameter as string));
        Register(registry, session, SpreadsheetTableCommandIds.Rename, "Đổi tên Bảng",
            "table.properties", InTable, (_, parameter) =>
                session.TableDesign.RenameTable(RequireString(parameter, "Table name")));
        Register(registry, session, SpreadsheetTableCommandIds.Resize, "Đổi kích thước Bảng",
            "table.resize", InTable, (_, parameter) =>
                session.TableDesign.ResizeTable(parameter is CellRange range
                    ? range
                    : throw new ArgumentException("A Table range is required.")));
        RegisterToggle(registry, session, SpreadsheetTableCommandIds.HeaderRow,
            "Hàng tiêu đề", "table.header-row", static state => state.HasHeaders,
            static (tables, id, value) => tables.SetHeaderRow(id, value));
        RegisterToggle(registry, session, SpreadsheetTableCommandIds.TotalsRow,
            "Hàng tổng", "table.total-row", static state => state.HasTotalsRow,
            static (tables, id, value) => tables.SetTotalsRow(id, value));
        RegisterToggle(registry, session, SpreadsheetTableCommandIds.FirstColumn,
            "Cột đầu tiên", "table.first-column", static state => state.ShowFirstColumn,
            static (tables, id, value) => tables.SetFirstColumn(id, value));
        RegisterToggle(registry, session, SpreadsheetTableCommandIds.LastColumn,
            "Cột cuối cùng", "table.last-column", static state => state.ShowLastColumn,
            static (tables, id, value) => tables.SetLastColumn(id, value));
        RegisterToggle(registry, session, SpreadsheetTableCommandIds.BandedRows,
            "Hàng xen kẽ", "table.banded-rows", static state => state.ShowRowStripes,
            static (tables, id, value) => tables.SetBandedRows(id, value));
        RegisterToggle(registry, session, SpreadsheetTableCommandIds.BandedColumns,
            "Cột xen kẽ", "table.banded-columns", static state => state.ShowColumnStripes,
            static (tables, id, value) => tables.SetBandedColumns(id, value));
        RegisterToggle(registry, session, SpreadsheetTableCommandIds.FilterButtons,
            "Nút lọc", "table.filter-buttons", static state => state.ShowFilterButtons,
            static (tables, id, value) => tables.SetFilterButtons(id, value),
            static state => state.HasHeaders);
        Register(registry, session, SpreadsheetTableCommandIds.Style, "Kiểu Bảng",
            "table.styles", state => new CommandState(
                state.IsInTable,
                IsChecked: null,
                DisplayText: state.StyleName,
                SelectedValue: state.StyleName,
                ItemsSource: state.Styles.Select(item => new CommandItem(
                    item.Name,
                    item.Name,
                    tooltip: item.Group))),
            (selected, parameter) => session.TableDesign.SetStyle(
                RequireString(selected ?? parameter, "Table style")));
        Register(registry, session, SpreadsheetTableCommandIds.CalculatedColumn,
            "Cột được tính", "formula.autosum", state => new CommandState(
                state.IsInTable && state.ColumnId is not null,
                DisplayText: state.CalculatedColumnFormula),
            (_, parameter) => session.TableDesign.SetCalculatedColumnFormula(parameter as string));
        Register(registry, session, SpreadsheetTableCommandIds.TotalsFunction,
            "Hàm tổng", "formula.autosum", state => new CommandState(
                state.IsInTable && state.HasTotalsRow && state.ColumnId is not null,
                IsChecked: null,
                DisplayText: null,
                SelectedValue: state.TotalsFunction.ToString(),
                ItemsSource: CreateTotalsItems()),
            (selected, parameter) =>
            {
                var value = RequireString(selected ?? parameter, "Totals function");
                if (!Enum.TryParse<SpreadsheetTableTotalsFunction>(value, out var function))
                {
                    throw new ArgumentException("The totals function is invalid.");
                }
                session.TableDesign.SetTotalsFunction(
                    function,
                    function == SpreadsheetTableTotalsFunction.Custom
                        ? parameter as string
                        : null);
            });
        Register(registry, session, SpreadsheetTableCommandIds.InsertRow, "Chèn hàng Bảng",
            "structure.row.insert", state => new CommandState(state.IsInTable),
            (_, _) => session.TableDesign.InsertRow());
        Register(registry, session, SpreadsheetTableCommandIds.DeleteRow, "Xóa hàng Bảng",
            "structure.row.delete", state => new CommandState(
                state.IsInTable && state.Range is { } range &&
                session.Selection.ActiveCell.RowIndex >=
                    range.Top + (state.HasHeaders ? 1 : 0) &&
                (!state.HasTotalsRow || session.Selection.ActiveCell.RowIndex < range.Bottom)),
            (_, _) => session.TableDesign.DeleteRow());
        Register(registry, session, SpreadsheetTableCommandIds.InsertColumn, "Chèn cột Bảng",
            "structure.column.insert", state => new CommandState(state.IsInTable),
            (_, parameter) => session.TableDesign.InsertColumn(parameter as string));
        Register(registry, session, SpreadsheetTableCommandIds.DeleteColumn, "Xóa cột Bảng",
            "structure.column.delete", state => new CommandState(
                state.IsInTable && state.ColumnId is not null &&
                state.Range is { ColumnCount: > 1 }),
            (_, _) => session.TableDesign.DeleteColumn());
        Register(registry, session, SpreadsheetTableCommandIds.RemoveDuplicates,
            "Loại bỏ trùng lặp", "table.remove-duplicates", InTable,
            (_, parameter) => session.TableDesign.RemoveDuplicates(parameter as IEnumerable<Guid>));
        Register(registry, session, SpreadsheetTableCommandIds.ConvertToRange,
            "Chuyển thành phạm vi", "table.convert-range", InTable,
            (_, _) => session.TableDesign.ConvertToRange());
    }

    private static CommandState InTable(SpreadsheetTableDesignSnapshot state) =>
        new(state.IsInTable);

    private static void RegisterToggle(
        CommandRegistry registry,
        SpreadsheetSession session,
        CommandId id,
        string caption,
        string icon,
        Func<SpreadsheetTableDesignSnapshot, bool> getValue,
        Action<SpreadsheetTableController, Guid, bool> setValue,
        Func<SpreadsheetTableDesignSnapshot, bool>? canExecute = null) =>
        Register(registry, session, id, caption, icon,
            state => new CommandState(
                state.IsInTable && (canExecute?.Invoke(state) ?? true),
                getValue(state)),
            (_, _) =>
            {
                var state = session.TableDesign.Refresh();
                setValue(
                    session.Tables,
                    state.TableId ?? throw new InvalidOperationException(
                        "The active selection does not belong to a Table."),
                    !getValue(state));
            });

    private static void Register(
        CommandRegistry registry,
        SpreadsheetSession session,
        CommandId id,
        string caption,
        string icon,
        Func<SpreadsheetTableDesignSnapshot, CommandState> getState,
        Action<string?, object?> execute)
    {
        registry.Register(
            new CommandDescriptor(id, caption, iconKey: icon),
            new TableCommandHandler(
                () => getState(session.TableDesign.Refresh()),
                execute));
    }

    private static IReadOnlyList<CommandItem> CreateTotalsItems() =>
    [
        new(SpreadsheetTableTotalsFunction.None.ToString(), "Không có"),
        new(SpreadsheetTableTotalsFunction.Average.ToString(), "Trung bình"),
        new(SpreadsheetTableTotalsFunction.CountNumbers.ToString(), "Đếm số"),
        new(SpreadsheetTableTotalsFunction.Count.ToString(), "Đếm không trống"),
        new(SpreadsheetTableTotalsFunction.Maximum.ToString(), "Lớn nhất"),
        new(SpreadsheetTableTotalsFunction.Minimum.ToString(), "Nhỏ nhất"),
        new(SpreadsheetTableTotalsFunction.Sum.ToString(), "Tổng"),
        new(SpreadsheetTableTotalsFunction.Custom.ToString(), "Tùy chỉnh"),
    ];

    private static string RequireString(object? value, string label) =>
        value is string { Length: > 0 } text
            ? text
            : throw new ArgumentException($"{label} is required.");

    private sealed class TableCommandHandler : IStatefulCommandHandler
    {
        private readonly Func<CommandState> _getState;
        private readonly Action<string?, object?> _execute;

        public TableCommandHandler(
            Func<CommandState> getState,
            Action<string?, object?> execute)
        {
            _getState = getState;
            _execute = execute;
        }

        public bool CanExecute(CommandContext context) => _getState().IsEnabled;

        public CommandState GetState(CommandContext context) => _getState();

        public ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var selected = (context.Parameter as ICommandItemActivation)?.SelectedValue;
            var parameter = (context.Parameter as ICommandItemActivation)?.OriginalParameter ??
                            context.Parameter;
            _execute(selected, parameter);
            return ValueTask.CompletedTask;
        }
    }
}
