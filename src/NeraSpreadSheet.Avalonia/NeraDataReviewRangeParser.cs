using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia;

internal static class NeraDataReviewRangeParser
{
    public static CellRange ParseRange(string? text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var normalized = text.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        var bang = normalized.LastIndexOf('!');
        if (bang >= 0) normalized = normalized[(bang + 1)..];
        var pieces = normalized.Split(':', StringSplitOptions.TrimEntries);
        if (pieces.Length is < 1 or > 2 || !CellAddress.TryParseA1(pieces[0], out var first) ||
            !CellAddress.TryParseA1(pieces.Length == 2 ? pieces[1] : pieces[0], out var second))
            throw new ArgumentException("Use an A1 range such as A1:D100.");
        return new CellRange(first, second);
    }

    public static CellAddress ParseAddress(string? text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var normalized = text.Replace("$", string.Empty, StringComparison.Ordinal).Trim();
        var bang = normalized.LastIndexOf('!');
        if (bang >= 0) normalized = normalized[(bang + 1)..];
        if (!CellAddress.TryParseA1(normalized, out var address))
            throw new ArgumentException("Use an A1 cell address such as H2.");
        return address;
    }

    public static SpreadsheetConsolidationSource ParseSource(Workbook workbook, Worksheet activeWorksheet, string? text)
    {
        ArgumentNullException.ThrowIfNull(workbook);
        ArgumentNullException.ThrowIfNull(activeWorksheet);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var normalized = text.Trim();
        var bang = normalized.LastIndexOf('!');
        var worksheet = activeWorksheet;
        var rangeText = normalized;
        if (bang >= 0)
        {
            var sheetText = normalized[..bang].Trim();
            if (sheetText.Length >= 2 && sheetText[0] == '\'' && sheetText[^1] == '\'')
                sheetText = sheetText[1..^1].Replace("''", "'", StringComparison.Ordinal);
            worksheet = workbook.GetWorksheet(sheetText);
            rangeText = normalized[(bang + 1)..];
        }
        return new SpreadsheetConsolidationSource(worksheet, ParseRange(rangeText));
    }

    public static string Format(CellRange range) => $"{range.TopLeft.ToA1()}:{range.BottomRight.ToA1()}";

    public static string Format(SpreadsheetConsolidationSource source)
    {
        var name = source.Worksheet.Name.Contains('\'', StringComparison.Ordinal)
            ? source.Worksheet.Name.Replace("'", "''", StringComparison.Ordinal)
            : source.Worksheet.Name;
        return $"'{name}'!{Format(source.Range)}";
    }
}
