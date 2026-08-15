using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetStyleController
{
    public const long DefaultMaximumMaterializedCells = 1_000_000;
    private readonly SpreadsheetSession _session;
    private readonly long _maximumMaterializedCells;

    public SpreadsheetStyleController(SpreadsheetSession session, long maximumMaterializedCells = DefaultMaximumMaterializedCells)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumMaterializedCells);
        _maximumMaterializedCells = maximumMaterializedCells;
    }

    public CellStyle ActiveCellStyle => _session.Workbook.Styles.Get(_session.ActiveWorksheet.GetCell(_session.Selection.ActiveCell).StyleId);

    public void ToggleBold() => ApplyToSelection(
        style => style with { Font = style.Font with { Weight = style.Font.Weight >= 600 ? 400 : 700 } },
        "Toggle bold");

    public void ToggleItalic() => ApplyToSelection(
        style => style with { Font = style.Font with { Italic = !style.Font.Italic } },
        "Toggle italic");

    public void SetFontColor(ColorRgba color) => ApplyToSelection(
        style => style with { Font = style.Font with { Color = color } },
        "Set font color");

    public void SetFill(ColorRgba color) => ApplyToSelection(
        style => style with { Fill = new CellFillStyle { IsVisible = true, Color = color } },
        "Set cell fill");

    public void ClearFill() => ApplyToSelection(
        style => style with { Fill = new CellFillStyle() },
        "Clear cell fill");

    public void SetNumberFormat(string formatCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formatCode);
        ApplyToSelection(
            style => style with { NumberFormat = new CellNumberFormatStyle { FormatCode = formatCode.Trim() } },
            "Set number format");
    }

    public void SetAllBorders(CellBorderLineStyle lineStyle, ColorRgba color, double width = 1d)
    {
        var side = new CellBorderSide { Style = lineStyle, Color = color, Width = width };
        ApplyToSelection(
            style => style with { Border = new CellBorderStyle { Left = side, Top = side, Right = side, Bottom = side } },
            "Set cell borders");
    }

    public void ApplyToSelection(Func<CellStyle, CellStyle> transform, string description)
    {
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        EnsureMaterializationLimit();

        var updates = new Dictionary<CellAddress, CellData>();
        foreach (var range in _session.Selection.Ranges)
        {
            for (var row = range.Top; row <= range.Bottom; row++)
            {
                for (var column = range.Left; column <= range.Right; column++)
                {
                    var address = new CellAddress(row, column);
                    var cell = _session.ActiveWorksheet.GetCell(address);
                    var current = _session.Workbook.Styles.Get(cell.StyleId);
                    var nextStyle = transform(current) ?? throw new InvalidOperationException("Style transform returned null.");
                    var nextStyleId = _session.Workbook.Styles.Intern(nextStyle);
                    updates[address] = new CellData(cell.Value, cell.Formula, nextStyleId);
                }
            }
        }

        if (updates.Count == 0)
        {
            return;
        }
        _session.Execute(new SetCellsOperation(_session.ActiveWorksheet, updates, description));
    }

    private void EnsureMaterializationLimit()
    {
        long total = 0;
        foreach (var range in _session.Selection.Ranges)
        {
            total = checked(total + ((long)range.RowCount * range.ColumnCount));
            if (total > _maximumMaterializedCells)
            {
                throw new InvalidOperationException("The selected range is too large to materialize for a style operation.");
            }
        }
    }
}

public static class SpreadsheetFormattingCommandIds
{
    public static CommandId Bold { get; } = new("Cell.Format.Bold");
    public static CommandId Italic { get; } = new("Cell.Format.Italic");
}

public static class SpreadsheetFormattingCommandCatalog
{
    public static void Register(CommandRegistry registry, SpreadsheetStyleController styles)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(styles);
        registry.Register(
            new CommandDescriptor(SpreadsheetFormattingCommandIds.Bold, "Bold", shortcut: "Ctrl+B"),
            new StyleCommandHandler(
                () => new CommandState(true, styles.ActiveCellStyle.Font.Weight >= 600),
                styles.ToggleBold));
        registry.Register(
            new CommandDescriptor(SpreadsheetFormattingCommandIds.Italic, "Italic", shortcut: "Ctrl+I"),
            new StyleCommandHandler(
                () => new CommandState(true, styles.ActiveCellStyle.Font.Italic),
                styles.ToggleItalic));
    }

    private sealed class StyleCommandHandler : IStatefulCommandHandler
    {
        private readonly Func<CommandState> _getState;
        private readonly Action _execute;
        public StyleCommandHandler(Func<CommandState> getState, Action execute) { _getState = getState; _execute = execute; }
        public bool CanExecute(CommandContext context) => _getState().IsEnabled;
        public CommandState GetState(CommandContext context) => _getState();
        public ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            _execute();
            return ValueTask.CompletedTask;
        }
    }
}
