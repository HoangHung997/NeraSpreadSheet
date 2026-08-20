namespace NeraSpreadSheet.Core;

internal static class SpreadsheetTableStringCompatibilityExtensions
{
    public static bool Contains(
        this string value,
        char character,
        StringComparison comparisonType)
    {
        ArgumentNullException.ThrowIfNull(value);
        _ = comparisonType;
        return value.Contains(character);
    }
}
