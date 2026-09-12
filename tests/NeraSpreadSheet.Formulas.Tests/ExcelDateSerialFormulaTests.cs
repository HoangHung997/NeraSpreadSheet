using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class ExcelDateSerialFormulaTests
{
    private readonly NeraFormulaEngine _engine = new();

    [TestMethod]
    public void DateArithmeticUsesNumericSerialsInsteadOfValueErrors()
    {
        var context = new TestContext(ExcelDateSystem.Date1900);
        AssertNumber("=DATE(2026,8,21)+1", ExcelDateSerial.ToSerial(new DateTime(2026, 8, 22)), context);
        AssertNumber("=DATE(2026,8,21)-DATE(2026,8,20)", 1d, context);
        AssertNumber("=DATE(2026,8,21)*2", ExcelDateSerial.ToSerial(new DateTime(2026, 8, 21)) * 2d, context);
        AssertNumber("=TIME(12,0,0)", 0.5d, context);
    }

    [TestMethod]
    public void CellDateTimeValuesCoerceToWorkbookSerialForArithmeticAndComparison()
    {
        var values = new Dictionary<CellAddress, CellValue>
        {
            [new CellAddress(0, 0)] = CellValue.FromDateTime(new DateTime(2026, 8, 21)),
            [new CellAddress(0, 1)] = CellValue.FromNumber(1d),
        };
        var context = new TestContext(ExcelDateSystem.Date1900, values);
        AssertNumber("=A1+B1", ExcelDateSerial.ToSerial(new DateTime(2026, 8, 22)), context);
        var comparison = _engine.Evaluate("=A1>45000", context);
        Assert.IsTrue(comparison.IsSuccess);
        Assert.AreEqual(CellValueKind.Boolean, comparison.Value.Kind);
        Assert.AreEqual(true, comparison.Value.RawValue);
    }

    [TestMethod]
    public void DateFunctionsReturnNumbersAndDatePartsAcceptNumbers()
    {
        var context = new TestContext(ExcelDateSystem.Date1900);
        var date = _engine.Evaluate("=DATE(2026,8,21)", context);
        Assert.IsTrue(date.IsSuccess);
        Assert.AreEqual(CellValueKind.Number, date.Value.Kind);
        AssertNumber("=YEAR(DATE(2026,8,21))", 2026d, context);
        AssertNumber("=MONTH(DATE(2026,8,21))", 8d, context);
        AssertNumber("=DAY(DATE(2026,8,21))", 21d, context);
        AssertNumber("=DATEVALUE(\"2026-08-21\")", ExcelDateSerial.ToSerial(new DateTime(2026, 8, 21)), context);
        AssertNumber("=DAYS(DATE(2026,8,21),DATE(2026,8,14))", 7d, context);
    }

    [TestMethod]
    public void TodayAndNowUseWorkbookDateSystemAndRemainNumeric()
    {
        var now = new DateTime(2026, 8, 21, 12, 0, 0);
        var context1900 = new TestContext(ExcelDateSystem.Date1900, now: now);
        var context1904 = new TestContext(ExcelDateSystem.Date1904, now: now);
        AssertNumber("=TODAY()", ExcelDateSerial.ToSerial(now.Date, ExcelDateSystem.Date1900), context1900);
        AssertNumber("=NOW()", ExcelDateSerial.ToSerial(now, ExcelDateSystem.Date1900), context1900);
        AssertNumber("=TODAY()", ExcelDateSerial.ToSerial(now.Date, ExcelDateSystem.Date1904), context1904);
        AssertNumber("=NOW()", ExcelDateSerial.ToSerial(now, ExcelDateSystem.Date1904), context1904);
    }

    [TestMethod]
    public void Date1904ArithmeticUses1904Basis()
    {
        var context = new TestContext(ExcelDateSystem.Date1904);
        AssertNumber("=DATE(1904,1,1)", 0d, context);
        AssertNumber("=DATE(2026,8,21)+1", ExcelDateSerial.ToSerial(new DateTime(2026, 8, 22), ExcelDateSystem.Date1904), context);
    }

    private void AssertNumber(string formula, double expected, IFormulaEvaluationContext context)
    {
        var result = _engine.Evaluate(formula, context);
        Assert.IsTrue(result.IsSuccess, $"{formula}: {result.Value}");
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind, formula);
        Assert.AreEqual(expected, (double)result.Value.RawValue!, 1e-9, formula);
    }

    private sealed class TestContext :
        IFormulaDateSystemEvaluationContext,
        IFormulaClockEvaluationContext
    {
        private readonly IReadOnlyDictionary<CellAddress, CellValue> _values;

        public TestContext(
            ExcelDateSystem dateSystem,
            IReadOnlyDictionary<CellAddress, CellValue>? values = null,
            DateTime? now = null)
        {
            DateSystem = dateSystem;
            _values = values ?? new Dictionary<CellAddress, CellValue>();
            CurrentDateTime = now ?? new DateTime(2026, 8, 21, 12, 0, 0);
        }

        public ExcelDateSystem DateSystem { get; }
        public DateTime CurrentDateTime { get; }

        public CellValue GetCellValue(string? worksheetName, CellAddress address) =>
            _values.GetValueOrDefault(address, CellValue.Blank);
    }
}
