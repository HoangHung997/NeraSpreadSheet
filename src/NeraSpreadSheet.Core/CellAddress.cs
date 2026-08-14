using System.Globalization;

namespace NeraSpreadSheet.Core;

public readonly record struct CellAddress
{
    public CellAddress(int rowIndex, int columnIndex)
    {
        if (rowIndex < 0 || rowIndex >= SpreadsheetLimits.MaxRows)
        {
            throw new ArgumentOutOfRangeException(nameof(rowIndex));
        }

        if (columnIndex < 0 || columnIndex >= SpreadsheetLimits.MaxColumns)
        {
            throw new ArgumentOutOfRangeException(nameof(columnIndex));
        }

        RowIndex = rowIndex;
        ColumnIndex = columnIndex;
    }

    public int RowIndex { get; }

    public int ColumnIndex { get; }

    public string ToA1()
    {
        Span<char> buffer = stackalloc char[8];
        var position = buffer.Length;
        var column = ColumnIndex + 1;

        while (column > 0)
        {
            column--;
            buffer[--position] = (char)('A' + (column % 26));
            column /= 26;
        }

        return new string(buffer[position..]) + (RowIndex + 1).ToString(CultureInfo.InvariantCulture);
    }

    public static CellAddress ParseA1(string text)
    {
        if (!TryParseA1(text, out var address))
        {
            throw new FormatException($"'{text}' is not a valid A1 cell address.");
        }

        return address;
    }

    public static bool TryParseA1(string? text, out CellAddress address)
    {
        address = default;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var value = text.Trim();
        var index = 0;
        var columnNumber = 0;

        if (index < value.Length && value[index] == '$')
        {
            index++;
        }

        var letterStart = index;
        while (index < value.Length && char.IsAsciiLetter(value[index]))
        {
            var letter = char.ToUpperInvariant(value[index]);
            var digit = letter - 'A' + 1;

            if (columnNumber > (SpreadsheetLimits.MaxColumns - digit) / 26)
            {
                return false;
            }

            columnNumber = (columnNumber * 26) + digit;
            index++;
        }

        if (index == letterStart)
        {
            return false;
        }

        if (index < value.Length && value[index] == '$')
        {
            index++;
        }

        var digitStart = index;
        var rowNumber = 0;
        while (index < value.Length && char.IsAsciiDigit(value[index]))
        {
            var digit = value[index] - '0';

            if (rowNumber > (SpreadsheetLimits.MaxRows - digit) / 10)
            {
                return false;
            }

            rowNumber = (rowNumber * 10) + digit;
            index++;
        }

        if (index == digitStart || index != value.Length)
        {
            return false;
        }

        if (rowNumber < 1 || rowNumber > SpreadsheetLimits.MaxRows ||
            columnNumber < 1 || columnNumber > SpreadsheetLimits.MaxColumns)
        {
            return false;
        }

        address = new CellAddress(rowNumber - 1, columnNumber - 1);
        return true;
    }

    public override string ToString() => ToA1();
}
