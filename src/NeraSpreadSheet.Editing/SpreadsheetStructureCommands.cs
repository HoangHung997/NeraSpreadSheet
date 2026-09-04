using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public static class SpreadsheetStructureCommandIds
{
    public static CommandId InsertRows { get; } = new("Structure.Row.Insert");
    public static CommandId DeleteRows { get; } = new("Structure.Row.Delete");
    public static CommandId InsertColumns { get; } = new("Structure.Column.Insert");
    public static CommandId DeleteColumns { get; } = new("Structure.Column.Delete");
    public static CommandId HideRows { get; } = new("Structure.Row.Hide");
    public static CommandId UnhideRows { get; } = new("Structure.Row.Unhide");
    public static CommandId HideColumns { get; } = new("Structure.Column.Hide");
    public static CommandId UnhideColumns { get; } = new("Structure.Column.Unhide");
}

public static class SpreadsheetStructureCommandCatalog
{
    /// <summary>
    /// Registers structural commands while preserving the legacy registration
    /// entry point for SDK consumers.
    /// </summary>
    public static void Register(
        CommandRegistry registry,
        SpreadsheetSession session,
        SpreadsheetStructureController structure) =>
        Register(registry, session, structure, session.AxisVisibility);

    /// <summary>Registers structural and axis-visibility commands.</summary>
    public static void Register(
        CommandRegistry registry,
        SpreadsheetSession session,
        SpreadsheetStructureController structure,
        SpreadsheetAxisVisibilityController visibility)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(structure);
        ArgumentNullException.ThrowIfNull(visibility);

        registry.Register(
            new CommandDescriptor(SpreadsheetStructureCommandIds.InsertRows, "Insert rows"),
            new StructureCommandHandler(
                () => GetAxisState(session, WorksheetAxis.Row),
                () =>
                {
                    var operation = GetAxisOperation(session, WorksheetAxis.Row);
                    structure.InsertRows(operation.Index, operation.Count);
                }));
        registry.Register(
            new CommandDescriptor(SpreadsheetStructureCommandIds.DeleteRows, "Delete rows"),
            new StructureCommandHandler(
                () => GetAxisState(session, WorksheetAxis.Row),
                () =>
                {
                    var operation = GetAxisOperation(session, WorksheetAxis.Row);
                    structure.DeleteRows(operation.Index, operation.Count);
                }));
        registry.Register(
            new CommandDescriptor(SpreadsheetStructureCommandIds.InsertColumns, "Insert columns"),
            new StructureCommandHandler(
                () => GetAxisState(session, WorksheetAxis.Column),
                () =>
                {
                    var operation = GetAxisOperation(session, WorksheetAxis.Column);
                    structure.InsertColumns(operation.Index, operation.Count);
                }));
        registry.Register(
            new CommandDescriptor(SpreadsheetStructureCommandIds.DeleteColumns, "Delete columns"),
            new StructureCommandHandler(
                () => GetAxisState(session, WorksheetAxis.Column),
                () =>
                {
                    var operation = GetAxisOperation(session, WorksheetAxis.Column);
                    structure.DeleteColumns(operation.Index, operation.Count);
                }));
        registry.Register(
            new CommandDescriptor(SpreadsheetStructureCommandIds.HideRows, "Ẩn hàng"),
            new StructureCommandHandler(
                () => GetAxisState(session, WorksheetAxis.Row),
                () =>
                {
                    var operation = GetAxisOperation(session, WorksheetAxis.Row);
                    visibility.HideRows(operation.Index, operation.Count);
                }));
        registry.Register(
            new CommandDescriptor(SpreadsheetStructureCommandIds.UnhideRows, "Hiện hàng"),
            new StructureCommandHandler(
                () => GetUnhideState(session, WorksheetAxis.Row),
                () =>
                {
                    var operation = GetAxisOperation(session, WorksheetAxis.Row);
                    visibility.UnhideRows(operation.Index, operation.Count);
                }));
        registry.Register(
            new CommandDescriptor(SpreadsheetStructureCommandIds.HideColumns, "Ẩn cột"),
            new StructureCommandHandler(
                () => GetAxisState(session, WorksheetAxis.Column),
                () =>
                {
                    var operation = GetAxisOperation(session, WorksheetAxis.Column);
                    visibility.HideColumns(operation.Index, operation.Count);
                }));
        registry.Register(
            new CommandDescriptor(SpreadsheetStructureCommandIds.UnhideColumns, "Hiện cột"),
            new StructureCommandHandler(
                () => GetUnhideState(session, WorksheetAxis.Column),
                () =>
                {
                    var operation = GetAxisOperation(session, WorksheetAxis.Column);
                    visibility.UnhideColumns(operation.Index, operation.Count);
                }));
    }

    private static CommandState GetUnhideState(
        SpreadsheetSession session,
        WorksheetAxis axis)
    {
        var operation = GetAxisOperation(session, axis);
        var dimensions = session.ActiveWorksheet.Dimensions;
        var enabled = axis == WorksheetAxis.Row
            ? dimensions.HasHiddenRows(operation.Index, operation.Count)
            : dimensions.HasHiddenColumns(operation.Index, operation.Count);
        return new CommandState(enabled);
    }

    private static CommandState GetAxisState(SpreadsheetSession session, WorksheetAxis axis)
    {
        var operation = GetAxisOperation(session, axis);
        var axisLength = axis == WorksheetAxis.Row
            ? SpreadsheetLimits.MaxRows
            : SpreadsheetLimits.MaxColumns;
        return new CommandState(
            operation.Index >= 0 &&
            operation.Count > 0 &&
            operation.Count <= axisLength - operation.Index);
    }

    private static AxisOperation GetAxisOperation(SpreadsheetSession session, WorksheetAxis axis)
    {
        if (session.Selection.Ranges.Count == 1)
        {
            var range = session.Selection.Ranges[0];
            if (axis == WorksheetAxis.Row && IsWholeRowRange(range))
            {
                return new AxisOperation(range.Top, range.RowCount);
            }
            if (axis == WorksheetAxis.Column && IsWholeColumnRange(range))
            {
                return new AxisOperation(range.Left, range.ColumnCount);
            }
        }

        var active = session.Selection.ActiveCell;
        return axis == WorksheetAxis.Row
            ? new AxisOperation(active.RowIndex, 1)
            : new AxisOperation(active.ColumnIndex, 1);
    }

    private static bool IsWholeRowRange(CellRange range) =>
        range.Left == 0 && range.Right == SpreadsheetLimits.MaxColumns - 1;

    private static bool IsWholeColumnRange(CellRange range) =>
        range.Top == 0 && range.Bottom == SpreadsheetLimits.MaxRows - 1;

    private readonly record struct AxisOperation(int Index, int Count);

    private sealed class StructureCommandHandler : IStatefulCommandHandler
    {
        private readonly Func<CommandState> _state;
        private readonly Action _execute;

        public StructureCommandHandler(Func<CommandState> state, Action execute)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public bool CanExecute(CommandContext context) => _state().IsEnabled;

        public CommandState GetState(CommandContext context) => _state();

        public ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            _execute();
            return ValueTask.CompletedTask;
        }
    }
}
