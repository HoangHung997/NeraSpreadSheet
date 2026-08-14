using System.Globalization;

namespace NeraSpreadSheet.Core;

public enum CellValueKind
{
    Blank = 0,
    Number = 1,
    Text = 2,
    Boolean = 3,
    DateTime = 4,
    Error = 5,
}

public readonly record struct CellValue
{
    private CellValue(CellValueKind kind, object? rawValue)
    {
        Kind = kind;
        RawValue = rawValue;
    }

    public CellValueKind Kind { get; }

    public object? RawValue { get; }

    public bool IsBlank => Kind == CellValueKind.Blank;

    public static CellValue Blank => default;

    public static CellValue FromNumber(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Cell numbers must be finite.");
        }

        return new CellValue(CellValueKind.Number, value);
    }

    public static CellValue FromText(string? value) => string.IsNullOrEmpty(value)
        ? Blank
        : new CellValue(CellValueKind.Text, value);

    public static CellValue FromBoolean(bool value) => new(CellValueKind.Boolean, value);

    public static CellValue FromDateTime(DateTime value) => new(CellValueKind.DateTime, value);

    public static CellValue FromError(string errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        return new CellValue(CellValueKind.Error, errorCode);
    }

    public static CellValue FromObject(object? value) => value switch
    {
        null => Blank,
        CellValue cellValue => cellValue,
        string text => FromText(text),
        bool boolean => FromBoolean(boolean),
        DateTime dateTime => FromDateTime(dateTime),
        byte number => FromNumber(number),
        short number => FromNumber(number),
        int number => FromNumber(number),
        long number => FromNumber(number),
        float number => FromNumber(number),
        double number => FromNumber(number),
        decimal number => FromNumber((double)number),
        _ => FromText(Convert.ToString(value, CultureInfo.InvariantCulture)),
    };

    public override string ToString() => Kind switch
    {
        CellValueKind.Blank => string.Empty,
        CellValueKind.Number => ((double)RawValue!).ToString(CultureInfo.InvariantCulture),
        CellValueKind.Boolean => ((bool)RawValue!).ToString(CultureInfo.InvariantCulture),
        CellValueKind.DateTime => ((DateTime)RawValue!).ToString("O", CultureInfo.InvariantCulture),
        _ => Convert.ToString(RawValue, CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
