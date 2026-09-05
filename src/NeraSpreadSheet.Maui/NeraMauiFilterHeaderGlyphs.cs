using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Maui;

internal static class NeraMauiFilterHeaderGlyphs
{
    internal static string Get(
        SpreadsheetFilterHeaderState state,
        bool? sortDescending) => state switch
        {
            SpreadsheetFilterHeaderState.None => "▼",
            SpreadsheetFilterHeaderState.Filtered => "▽",
            SpreadsheetFilterHeaderState.Sorted => sortDescending == true
                ? "↓"
                : "↑",
            SpreadsheetFilterHeaderState.FilteredAndSorted => sortDescending == true
                ? "⇊"
                : "⇈",
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
}
