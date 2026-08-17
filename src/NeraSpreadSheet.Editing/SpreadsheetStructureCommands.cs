using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public static class SpreadsheetStructureCommandIds
{
    public static CommandId InsertRows { get; } = new("Structure.Row.Insert");
    public static CommandId DeleteRows { get; } = new("Structure.Row.Delete");
    public static CommandId InsertColumns { get; } = new("Structure.Column.Insert");
    public static CommandId DeleteColumns { get; } = new("Structure.Column.Delete");
}

public static class SpreadsheetStructureCommandCatalog
{
    public static void Register(
        CommandRegistry registry,
        SpreadsheetSession session,
        SpreadsheetStructureController structure)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(structure);

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
