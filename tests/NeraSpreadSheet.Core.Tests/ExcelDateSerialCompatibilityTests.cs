using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class ExcelDateSerialCompatibilityTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [TestMethod]
    public void Date1900PreservesExcelLeapYearCompatibility()
    {
        Assert.AreEqual(1d, ExcelDateSerial.ToSerial(new DateTime(1900, 1, 1)), 1e-12);
        Assert.AreEqual(59d, ExcelDateSerial.ToSerial(new DateTime(1900, 2, 28)), 1e-12);
        Assert.AreEqual(61d, ExcelDateSerial.ToSerial(new DateTime(1900, 3, 1)), 1e-12);
        Assert.IsFalse(ExcelDateSerial.TryFromSerial(60d, ExcelDateSystem.Date1900, out _));
        Assert.AreEqual(new DateTime(1900, 3, 1), ExcelDateSerial.FromSerial(61d));
    }

    [TestMethod]
    public void Date1904StartsAtZeroAndRoundTripsFractions()
    {
        Assert.AreEqual(0d, ExcelDateSerial.ToSerial(new DateTime(1904, 1, 1), ExcelDateSystem.Date1904), 1e-12);
        var expected = new DateTime(2026, 8, 21, 14, 30, 15);
        var serial = ExcelDateSerial.ToSerial(expected, ExcelDateSystem.Date1904);
        Assert.AreEqual(expected, ExcelDateSerial.FromSerial(serial, ExcelDateSystem.Date1904));
    }

    [TestMethod]
    public void NumberFormatControlsPresentationWithoutChangingUnderlyingSerial()
    {
        var date = new DateTime(2026, 8, 21);
        var value = CellValue.FromDateTime(date);
        var serial = ExcelDateSerial.ToSerial(date);

        Assert.AreEqual(serial.ToString("G15", Invariant),
            ExcelCellValueFormatter.Format(value, "General", ExcelDateSystem.Date1900, Invariant));
        Assert.AreEqual(serial.ToString("0.00", Invariant),
            ExcelCellValueFormatter.Format(value, "0.00", ExcelDateSystem.Date1900, Invariant));
        Assert.AreEqual("2026-08-21",
            ExcelCellValueFormatter.Format(value, "yyyy-mm-dd", ExcelDateSystem.Date1900, Invariant));
        Assert.AreEqual("2026-08-21",
            ExcelCellValueFormatter.Format(CellValue.FromNumber(serial), "yyyy-mm-dd", ExcelDateSystem.Date1900, Invariant));
    }

    [TestMethod]
    public void SameCalendarDateUsesWorkbookDateSystemBasis()
    {
        var date = new DateTime(2026, 8, 21);
        var serial1900 = ExcelDateSerial.ToSerial(date, ExcelDateSystem.Date1900);
        var serial1904 = ExcelDateSerial.ToSerial(date, ExcelDateSystem.Date1904);
        Assert.AreEqual(1462d, serial1900 - serial1904, 1e-12);
        Assert.AreEqual(date, ExcelDateSerial.FromSerial(serial1900, ExcelDateSystem.Date1900));
        Assert.AreEqual(date, ExcelDateSerial.FromSerial(serial1904, ExcelDateSystem.Date1904));
    }
}
