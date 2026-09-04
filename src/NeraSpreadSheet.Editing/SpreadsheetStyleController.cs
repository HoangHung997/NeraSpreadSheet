using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Editing;

public sealed class SpreadsheetStyleController
{
    public const long DefaultMaximumMaterializedCells = 1_000_000;
    private readonly SpreadsheetSession _session;
    private readonly long _maximumMaterializedCells;

    public SpreadsheetStyleController(
        SpreadsheetSession session,
        long maximumMaterializedCells = DefaultMaximumMaterializedCells)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            maximumMaterializedCells);
        _maximumMaterializedCells = maximumMaterializedCells;
    }

    public CellStyle ActiveCellStyle =>
        _session.ActiveWorksheet.GetEffectiveStyle(
            _session.Selection.ActiveCell,
            _session.Workbook.Styles);

    public void ToggleBold() => ApplyToSelection(
        style => style with
        {
            Font = style.Font with
            {
                Weight = style.Font.Weight >= 600 ? 400 : 700,
            },
        },
        "Toggle bold");

    public void ToggleItalic() => ApplyToSelection(
        style => style with
        {
            Font = style.Font with
            {
                Italic = !style.Font.Italic,
            },
        },
        "Toggle italic");

    public void SetFontColor(ColorRgba color) => ApplyToSelection(
        style => style with
        {
            Font = style.Font with
            {
                Color = color,
            },
        },
        "Set font color");

    public void SetFill(ColorRgba color) => ApplyToSelection(
        style => style with
        {
            Fill = new CellFillStyle
            {
                IsVisible = true,
                Color = color,
            },
        },
        "Set cell fill");

    public void ClearFill() => ApplyToSelection(
        style => style with
        {
            Fill = new CellFillStyle(),
        },
        "Clear cell fill");

    public void SetNumberFormat(string formatCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formatCode);
        ApplyToSelection(
            style => style with
            {
                NumberFormat = new CellNumberFormatStyle
                {
                    FormatCode = formatCode.Trim(),
                },
            },
            "Set number format");
    }

    public void SetAllBorders(
        CellBorderLineStyle lineStyle,
        ColorRgba color,
        double width = 1d)
    {
        var side = new CellBorderSide
        {
            Style = lineStyle,
            Color = color,
            Width = width,
        };
        ApplyToSelection(
            style => style with
            {
                Border = new CellBorderStyle
                {
                    Left = side,
                    Top = side,
                    Right = side,
                    Bottom = side,
                },
            },
            "Set cell borders");
    }

    public void ApplyToSelection(
        Func<CellStyle, CellStyle> transform,
        string description)
    {
        ArgumentNullException.ThrowIfNull(transform);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        var affectedRanges = _session.Selection.Ranges.ToArray();
        if (affectedRanges.Length == 0)
        {
            return;
        }

        var activeStyle = ActiveCellStyle;
        var transformedActiveStyle = transform(activeStyle) ??
            throw new InvalidOperationException(
                "Style transform returned null.");
        var axisPatch = CellStylePatch.FromDifference(
            activeStyle,
            transformedActiveStyle);
        var axisMutations = new List<WorksheetAxisStyleMutation>();
        var finiteRanges = new List<CellRange>();

        foreach (var range in affectedRanges)
        {
            if (IsWholeSheet(range))
            {
                AddAxisMutation(
                    axisMutations,
                    WorksheetAxis.Row,
                    0,
                    SpreadsheetLimits.MaxRows - 1,
                    axisPatch);
            }
            else if (IsWholeRows(range))
            {
                AddAxisMutation(
                    axisMutations,
                    WorksheetAxis.Row,
                    range.Top,
                    range.Bottom,
                    axisPatch);
            }
            else if (IsWholeColumns(range))
            {
                AddAxisMutation(
                    axisMutations,
                    WorksheetAxis.Column,
                    range.Left,
                    range.Right,
                    axisPatch);
            }
            else
            {
                finiteRanges.Add(range);
            }
        }

        EnsureMaterializationLimit(finiteRanges);
        if (axisMutations.Count == 0 && finiteRanges.Count == 0)
        {
            return;
        }

        _session.Execute(new SetWorksheetStylesOperation(
            _session.ActiveWorksheet,
            _session.Workbook.Styles,
            axisMutations,
            finiteRanges,
            transform,
            affectedRanges,
            description));
    }

    private static void AddAxisMutation(
        List<WorksheetAxisStyleMutation> mutations,
        WorksheetAxis axis,
        int startIndex,
        int endIndex,
        CellStylePatch patch)
    {
        if (!patch.IsEmpty)
        {
            mutations.Add(new WorksheetAxisStyleMutation(
                axis,
                startIndex,
                endIndex,
                patch));
        }
    }

    private void EnsureMaterializationLimit(List<CellRange> finiteRanges)
    {
        long total = 0L;
        foreach (var range in finiteRanges)
        {
            total = checked(
                total + ((long)range.RowCount * range.ColumnCount));
            if (total > _maximumMaterializedCells)
            {
                throw new InvalidOperationException(
                    "The finite selected range is too large to materialize " +
                    "for a style operation. Whole-row, whole-column and " +
                    "whole-sheet selections remain sparse.");
            }
        }
    }

    private static bool IsWholeSheet(CellRange range) =>
        range.Top == 0 &&
        range.Left == 0 &&
        range.Bottom == SpreadsheetLimits.MaxRows - 1 &&
        range.Right == SpreadsheetLimits.MaxColumns - 1;

    private static bool IsWholeRows(CellRange range) =>
        range.Left == 0 &&
        range.Right == SpreadsheetLimits.MaxColumns - 1;

    private static bool IsWholeColumns(CellRange range) =>
        range.Top == 0 &&
        range.Bottom == SpreadsheetLimits.MaxRows - 1;
}

public static class SpreadsheetFormattingCommandIds
{
    public static CommandId Bold { get; } = new("Cell.Format.Bold");

    public static CommandId Italic { get; } = new("Cell.Format.Italic");
}

public static class SpreadsheetFormattingCommandCatalog
{
    public static void Register(
        CommandRegistry registry,
        SpreadsheetStyleController styles)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(styles);
        registry.Register(
            new CommandDescriptor(
                SpreadsheetFormattingCommandIds.Bold,
                "Bold",
                iconKey: "font.bold",
                shortcut: "Ctrl+B"),
            new StyleCommandHandler(
                () => new CommandState(
                    true,
                    styles.ActiveCellStyle.Font.Weight >= 600),
                styles.ToggleBold));
        registry.Register(
            new CommandDescriptor(
                SpreadsheetFormattingCommandIds.Italic,
                "Italic",
                iconKey: "font.italic",
                shortcut: "Ctrl+I"),
            new StyleCommandHandler(
                () => new CommandState(
                    true,
                    styles.ActiveCellStyle.Font.Italic),
                styles.ToggleItalic));
    }

    private sealed class StyleCommandHandler : IStatefulCommandHandler
    {
        private readonly Func<CommandState> _getState;
        private readonly Action _execute;

        public StyleCommandHandler(
            Func<CommandState> getState,
            Action execute)
        {
            _getState = getState ??
                throw new ArgumentNullException(nameof(getState));
            _execute = execute ??
                throw new ArgumentNullException(nameof(execute));
        }

        public bool CanExecute(CommandContext context) =>
            _getState().IsEnabled;

        public CommandState GetState(CommandContext context) =>
            _getState();

        public ValueTask ExecuteAsync(CommandContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            _execute();
            return ValueTask.CompletedTask;
        }
    }
}
