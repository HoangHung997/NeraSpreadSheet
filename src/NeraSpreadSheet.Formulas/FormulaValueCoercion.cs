using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Shared, deterministic coercion helpers for built-in and extension formula
/// functions. Extension functions should use this surface instead of inventing
/// incompatible blank/Boolean/number/date conversion rules.
/// </summary>
public static class FormulaValueCoercion
{
    public static bool TryGetFirstError(
        IReadOnlyList<CellValue> values,
        out CellValue error)
    {
        ArgumentNullException.ThrowIfNull(values);
        foreach (var value in values)
        {
            if (value.Kind == CellValueKind.Error)
            {
                error = value;
                return true;
            }
        }

        error = default;
        return false;
    }

    public static bool TryNumber(
        CellValue value,
        out double number,
        bool allowText = false)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                number = (double)value.RawValue!;
                return true;
            case CellValueKind.Boolean:
                number = (bool)value.RawValue! ? 1d : 0d;
                return true;
            case CellValueKind.Blank:
                number = 0d;
                return true;
            case CellValueKind.DateTime:
                try
                {
                    number = ((DateTime)value.RawValue!).ToOADate();
                    return double.IsFinite(number);
                }
                catch (OverflowException)
                {
                    number = 0d;
                    return false;
                }
            case CellValueKind.Text when allowText:
                return double.TryParse(
                    (string)value.RawValue!,
                    NumberStyles.Float | NumberStyles.AllowThousands,
                    CultureInfo.InvariantCulture,
                    out number) &&
                    double.IsFinite(number);
            default:
                number = 0d;
                return false;
        }
    }

    public static bool TryInteger(
        CellValue value,
        out int result,
        bool allowText = false)
    {
        if (!TryNumber(value, out var number, allowText) ||
            number < int.MinValue ||
            number > int.MaxValue)
        {
            result = default;
            return false;
        }

        var rounded = Math.Round(number);
        if (Math.Abs(number - rounded) > 1e-10d)
        {
            result = default;
            return false;
        }

        result = checked((int)rounded);
        return true;
    }

    public static bool TryBoolean(
        CellValue value,
        out bool result,
        bool allowText = true)
    {
        switch (value.Kind)
        {
            case CellValueKind.Boolean:
                result = (bool)value.RawValue!;
                return true;
            case CellValueKind.Number:
                result = Math.Abs((double)value.RawValue!) > double.Epsilon;
                return true;
            case CellValueKind.Blank:
                result = false;
                return true;
            case CellValueKind.Text when allowText:
                if (bool.TryParse((string)value.RawValue!, out result))
                {
                    return true;
                }
                break;
        }

        result = false;
        return false;
    }

    public static bool TryDateTime(
        CellValue value,
        out DateTime dateTime,
        bool allowText = true)
    {
        switch (value.Kind)
        {
            case CellValueKind.DateTime:
                dateTime = (DateTime)value.RawValue!;
                return true;
            case CellValueKind.Number:
                try
                {
                    dateTime = DateTime.FromOADate((double)value.RawValue!);
                    return true;
                }
                catch (ArgumentException)
                {
                    break;
                }
            case CellValueKind.Text when allowText:
                if (DateTime.TryParse(
                        (string)value.RawValue!,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AllowWhiteSpaces |
                        DateTimeStyles.RoundtripKind,
                        out dateTime))
                {
                    return true;
                }
                break;
        }

        dateTime = default;
        return false;
    }

    public static string ToText(CellValue value) =>
        value.Kind switch
        {
            CellValueKind.Blank => string.Empty,
            CellValueKind.Boolean =>
                (bool)value.RawValue! ? "TRUE" : "FALSE",
            CellValueKind.DateTime =>
                ((DateTime)value.RawValue!).ToString(
                    "yyyy-MM-dd HH:mm:ss",
                    CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

    public static CellValue SafeNumber(double value) =>
        double.IsFinite(value)
            ? CellValue.FromNumber(value)
            : CellValue.FromError("#NUM!");

    public static CellValue Error(string code) =>
        CellValue.FromError(code);
}
