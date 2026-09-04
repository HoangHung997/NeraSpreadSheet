using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Core.Tests;

[TestClass]
public sealed class ExcelCellValueFormatterTests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    [TestMethod]
    public void FormatShouldRenderExcelDateSerialInsteadOfLiteralFormatCode()
    {
        var result = ExcelCellValueFormatter.Format(
            CellValue.FromNumber(45_751d),
            "m/d/yyyy",
            culture: EnUs);

        Assert.AreEqual("4/4/2025", result);
    }

    [TestMethod]
    public void FormatShouldHonorMac1904DateSystem()
    {
        var result = ExcelCellValueFormatter.Format(
            CellValue.FromNumber(0d),
            "yyyy-mm-dd",
            ExcelDateSystem.Date1904,
            EnUs);

        Assert.AreEqual("1904-01-01", result);
    }

    [TestMethod]
    public void FormatShouldHandleSectionsPercentScientificAndText()
    {
        Assert.AreEqual(
            "(1,234.50)",
            ExcelCellValueFormatter.Format(
                CellValue.FromNumber(-1234.5d),
                "#,##0.00;(#,##0.00);-",
                culture: EnUs));
        Assert.AreEqual(
            "12.5%",
            ExcelCellValueFormatter.Format(
                CellValue.FromNumber(0.125d),
                "0.0%",
                culture: EnUs));
        Assert.AreEqual(
            "1.23E+03",
            ExcelCellValueFormatter.Format(
                CellValue.FromNumber(1234d),
                "0.00E+00",
                culture: EnUs));
        Assert.AreEqual(
            "ID: abc",
            ExcelCellValueFormatter.Format(
                CellValue.FromText("abc"),
                "General;General;General;\"ID: \"@",
                culture: EnUs));
    }

    [TestMethod]
    public void FormatShouldHandleFractionsAndElapsedTime()
    {
        Assert.AreEqual(
            "2 1/4",
            ExcelCellValueFormatter.Format(
                CellValue.FromNumber(2.25d),
                "# ?/?",
                culture: EnUs));
        Assert.AreEqual(
            "36:00:00",
            ExcelCellValueFormatter.Format(
                CellValue.FromNumber(1.5d),
                "[h]:mm:ss",
                culture: EnUs));
    }

    [TestMethod]
    public void FormatShouldHandleConditionalSectionsCurrencyLocalesAndFractionPrecision()
    {
        Assert.AreEqual(
            "low 25",
            ExcelCellValueFormatter.Format(
                CellValue.FromNumber(25d),
                "[Red][<50]\"low \"0;[Blue][>=50]\"high \"0;0",
                culture: EnUs));
        Assert.AreEqual(
            "$1,234.50",
            ExcelCellValueFormatter.Format(
                CellValue.FromNumber(1234.5d),
                "[$$-409]#,##0.00",
                culture: EnUs));
        Assert.AreEqual(
            "1 1/16",
            ExcelCellValueFormatter.Format(
                CellValue.FromNumber(1.0625d),
                "# ??/??",
                culture: EnUs));
        Assert.AreEqual(
            "1 2/8",
            ExcelCellValueFormatter.Format(
                CellValue.FromNumber(1.25d),
                "# ?/8",
                culture: EnUs));
    }
}
