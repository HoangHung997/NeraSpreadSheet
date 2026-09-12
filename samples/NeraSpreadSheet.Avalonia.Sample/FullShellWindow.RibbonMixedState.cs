using System.Globalization;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    private const long RibbonMixedStateCellScanLimit = 100_000;

    private bool TryGetCommonSelectionValue<T>(Func<CellStyle, T> selector, out T value)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var ranges = Session.Selection.Ranges;
        if (ranges.Count == 0)
        {
            value = selector(Session.Styles.ActiveCellStyle);
            return true;
        }

        var worksheet = Session.ActiveWorksheet;
        var catalog = Session.Workbook.Styles;
        var comparer = EqualityComparer<T>.Default;
        var hasValue = false;
        var common = default(T)!;
        long scanned = 0;

        foreach (var range in ranges)
        {
            var cellCount = checked((long)range.RowCount * range.ColumnCount);
            if (cellCount > RibbonMixedStateCellScanLimit || scanned + cellCount > RibbonMixedStateCellScanLimit)
            {
                value = default!;
                return false;
            }

            for (var row = range.Top; row <= range.Bottom; row++)
            {
                for (var column = range.Left; column <= range.Right; column++)
                {
                    var style = worksheet.GetEffectiveStyle(new CellAddress(row, column), catalog);
                    var current = selector(style);
                    if (!hasValue)
                    {
                        common = current;
                        hasValue = true;
                    }
                    else if (!comparer.Equals(common, current))
                    {
                        value = default!;
                        return false;
                    }
                }
            }

            scanned += cellCount;
        }

        value = common;
        return hasValue;
    }

    private CommandState RibbonChoiceState(Func<CellStyle, string?> selector, IEnumerable<CommandItem> choices)
    {
        return TryGetCommonSelectionValue(selector, out var selected)
            ? ChoiceState(selected, choices)
            : ChoiceState(null, choices);
    }

    private CommandState RibbonToggleState(Func<CellStyle, bool> selector)
    {
        return TryGetCommonSelectionValue(selector, out var selected)
            ? new CommandState(true, selected)
            : new CommandState(true, null);
    }

    private CommandState RibbonAlignmentState(CellHorizontalAlignment alignment)
    {
        return TryGetCommonSelectionValue(style => style.Alignment.Horizontal, out var selected)
            ? new CommandState(true, selected == alignment)
            : new CommandState(true, null);
    }

    private CommandState RibbonNumberFormatMatchState(string formatCode)
    {
        return TryGetCommonSelectionValue(style => style.NumberFormat.FormatCode, out var selected)
            ? new CommandState(true, string.Equals(selected, formatCode, StringComparison.Ordinal))
            : new CommandState(true, null);
    }

    private CommandState RibbonFontSizeState() =>
        RibbonChoiceState(
            style => style.Font.Size.ToString(CultureInfo.InvariantCulture),
            SizeChoices);
}
