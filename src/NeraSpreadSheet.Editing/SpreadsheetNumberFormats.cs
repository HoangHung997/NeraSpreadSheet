namespace NeraSpreadSheet.Editing;

/// <summary>Number-format choices for the formatting UI. Codes remain culture-independent;
/// the existing ExcelCellValueFormatter supplies the culture-aware display preview.</summary>
public enum SpreadsheetNumberFormatCategory
{
    General, Number, Currency, Accounting, Date, Time, Percentage, Fraction, Scientific, Text, Custom,
}

public static class SpreadsheetNumberFormats
{
    /// <summary>Creates a format code, not a converted cell value.</summary>
    public static string Create(SpreadsheetNumberFormatCategory category, int decimals = 2,
        bool groupThousands = true, string currencySymbol = "₫", bool negativeParentheses = false)
    {
        if (!Enum.IsDefined(category)) throw new ArgumentOutOfRangeException(nameof(category));
        if (decimals is < 0 or > 15) throw new ArgumentOutOfRangeException(nameof(decimals));
        ArgumentNullException.ThrowIfNull(currencySymbol);
        if (currencySymbol.Length > 16 || currencySymbol.Any(char.IsControl))
            throw new ArgumentException("The currency symbol must contain at most 16 printable characters.", nameof(currencySymbol));
        var fraction = decimals == 0 ? string.Empty : "." + new string('0', decimals);
        var number = (groupThousands ? "#,##0" : "0") + fraction;
        var quoted = "\"" + currencySymbol.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        var positive = category switch
        {
            SpreadsheetNumberFormatCategory.General => "General",
            SpreadsheetNumberFormatCategory.Number => number,
            SpreadsheetNumberFormatCategory.Currency => number + " " + quoted,
            SpreadsheetNumberFormatCategory.Accounting => "_(* " + number + " " + quoted + "_)",
            SpreadsheetNumberFormatCategory.Date => "dd/mm/yyyy",
            SpreadsheetNumberFormatCategory.Time => "hh:mm:ss",
            SpreadsheetNumberFormatCategory.Percentage => "0" + fraction + "%",
            SpreadsheetNumberFormatCategory.Fraction => "# ?/?",
            SpreadsheetNumberFormatCategory.Scientific => "0" + fraction + "E+00",
            SpreadsheetNumberFormatCategory.Text => "@",
            _ => throw new ArgumentException("Custom formats must be supplied explicitly.", nameof(category)),
        };
        return negativeParentheses && category is SpreadsheetNumberFormatCategory.Number or SpreadsheetNumberFormatCategory.Currency
            ? positive + ";(" + positive + ")" : positive;
    }

    /// <summary>Checks bounded structural syntax. This is not a full Excel format grammar
    /// or a promise that every directive can be rendered by the current engine.</summary>
    public static void Validate(string code)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        if (code.Length > 255) throw new ArgumentException("A format in this dialog is limited to 255 characters.", nameof(code));
        var quoted = false;
        var bracket = false;
        var sections = 1;
        for (var index = 0; index < code.Length; index++)
        {
            var character = code[index];
            if (char.IsControl(character)) throw new ArgumentException("Control characters are not allowed in a number format.", nameof(code));
            if (character == '\\' || (!quoted && character is '_' or '*'))
            {
                if (++index == code.Length || char.IsControl(code[index]))
                    throw new ArgumentException("A format escape requires a following printable character.", nameof(code));
                continue;
            }
            if (character == '"') { quoted = !quoted; continue; }
            if (quoted) continue;
            if (character == '[')
            {
                if (bracket) throw new ArgumentException("Nested format brackets are not supported.", nameof(code));
                bracket = true;
            }
            if (character == ']')
            {
                if (!bracket) throw new ArgumentException("Unbalanced format brackets.", nameof(code));
                bracket = false;
            }
            if (character == ';' && !bracket && ++sections > 4)
                throw new ArgumentException("A number format has at most four sections.", nameof(code));
        }
        if (quoted || bracket) throw new ArgumentException("Close the quote or bracket in the number format.", nameof(code));
    }
}
