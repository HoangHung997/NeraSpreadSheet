namespace NeraSpreadSheet.Core;

/// <summary>
/// Converts between CLR dates and the numeric serial representation used by Excel.
/// Number formats are presentation only; arithmetic should operate on these serials.
/// </summary>
public static class ExcelDateSerial
{
    private static readonly DateTime Epoch1900 = new(1899, 12, 31);
    private static readonly DateTime LeapBugBoundary = new(1900, 3, 1);
    private static readonly DateTime Epoch1904 = new(1904, 1, 1);

    public static double ToSerial(DateTime value, ExcelDateSystem dateSystem = ExcelDateSystem.Date1900)
    {
        var date = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        var serial = dateSystem == ExcelDateSystem.Date1904
            ? (date - Epoch1904).TotalDays
            : (date - Epoch1900).TotalDays;

        // Excel intentionally preserves Lotus 1-2-3 compatibility and treats
        // 1900 as a leap year. Real dates on/after 1900-03-01 therefore have
        // one extra serial day in the 1900 system.
        if (dateSystem == ExcelDateSystem.Date1900 && date >= LeapBugBoundary)
        {
            serial += 1d;
        }

        if (!double.IsFinite(serial))
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        return serial;
    }

    public static bool TryFromSerial(
        double serial,
        ExcelDateSystem dateSystem,
        out DateTime value)
    {
        value = default;
        if (!double.IsFinite(serial))
        {
            return false;
        }

        // Serial 60 in the 1900 date system is Excel's fictitious 1900-02-29
        // and cannot be represented by System.DateTime. Keep the serial numeric
        // instead of silently mapping it to a different real date.
        if (dateSystem == ExcelDateSystem.Date1900 &&
            serial >= 60d && serial < 61d)
        {
            return false;
        }

        try
        {
            var adjusted = dateSystem == ExcelDateSystem.Date1900 && serial >= 61d
                ? serial - 1d
                : serial;
            value = (dateSystem == ExcelDateSystem.Date1904 ? Epoch1904 : Epoch1900)
                .AddDays(adjusted);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public static DateTime FromSerial(
        double serial,
        ExcelDateSystem dateSystem = ExcelDateSystem.Date1900) =>
        TryFromSerial(serial, dateSystem, out var value)
            ? value
            : throw new ArgumentOutOfRangeException(
                nameof(serial),
                serial,
                "The serial is outside the supported Excel date range or is Excel's fictitious 1900-02-29.");
}
