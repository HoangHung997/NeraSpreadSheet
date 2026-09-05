using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class FormulaSurfaceTextDateTests
{
    [TestMethod]
    public void TextFunctionsSupportSearchReplacementAndJoining()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual("Hello World", Text(
            engine,
            context,
            "=PROPER(TRIM(\"  hello   WORLD  \"))"));
        Assert.AreEqual("abc", Text(
            engine,
            context,
            "=LEFT(\"abcdef\",3)"));
        Assert.AreEqual("ef", Text(
            engine,
            context,
            "=RIGHT(\"abcdef\",2)"));
        Assert.AreEqual("bcd", Text(
            engine,
            context,
            "=MID(\"abcdef\",2,3)"));
        Assert.AreEqual(4d, Number(
            engine,
            context,
            "=SEARCH(\"LO\",\"hello\")"));
        Assert.AreEqual("one-2-one", Text(
            engine,
            context,
            "=SUBSTITUTE(\"one-1-one\",\"1\",\"2\")"));
        Assert.AreEqual("A|B", Text(
            engine,
            context,
            "=TEXTJOIN(\"|\",TRUE,\"A\",\"\",\"B\")"));
    }

    [TestMethod]
    public void TextFunctionsHandleUnicodeAndLimits()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual("😀", Text(
            engine,
            context,
            "=UNICHAR(128512)"));
        Assert.AreEqual(128512d, Number(
            engine,
            context,
            "=UNICODE(\"😀\")"));
        Assert.AreEqual(65d, Number(
            engine,
            context,
            "=CODE(\"ABC\")"));
        Assert.AreEqual("AAA", Text(
            engine,
            context,
            "=REPT(\"A\",3)"));
        Assert.AreEqual(
            "#VALUE!",
            engine.Evaluate(
                "=REPT(\"1234567890\",4000)",
                context).Value.RawValue);
    }

    [TestMethod]
    public void DateAndTimeFunctionsUseDateValuesAndSerialFractions()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        var date = engine.Evaluate("=DATE(2026,2,28)", context).Value;
        Assert.AreEqual(
            new DateTime(2026, 2, 28),
            date.RawValue);
        Assert.AreEqual(2026d, Number(
            engine,
            context,
            "=YEAR(DATE(2026,2,28))"));
        Assert.AreEqual(2d, Number(
            engine,
            context,
            "=MONTH(DATE(2026,2,28))"));
        Assert.AreEqual(28d, Number(
            engine,
            context,
            "=DAY(DATE(2026,2,28))"));
        Assert.AreEqual(
            0.5d,
            Number(engine, context, "=TIME(12,0,0)"),
            1e-12d);
        Assert.AreEqual(12d, Number(
            engine,
            context,
            "=HOUR(TIME(12,30,45))"));
        Assert.AreEqual(30d, Number(
            engine,
            context,
            "=MINUTE(TIME(12,30,45))"));
        Assert.AreEqual(45d, Number(
            engine,
            context,
            "=SECOND(TIME(12,30,45))"));
    }

    [TestMethod]
    public void DateFunctionsSupportMonthShiftAndWeekday()
    {
        var engine = new NeraFormulaEngine();
        var context = new FormulaSurfaceTestContext();

        Assert.AreEqual(
            new DateTime(2026, 3, 28),
            engine.Evaluate(
                "=EDATE(DATE(2026,2,28),1)",
                context).Value.RawValue);
        Assert.AreEqual(
            new DateTime(2026, 2, 28),
            engine.Evaluate(
                "=EOMONTH(DATE(2026,1,15),1)",
                context).Value.RawValue);
        Assert.AreEqual(2d, Number(
            engine,
            context,
            "=DAYS(DATE(2026,1,3),DATE(2026,1,1))"));
        Assert.AreEqual(1d, Number(
            engine,
            context,
            "=WEEKDAY(DATE(2026,8,24),2)"));
    }

    [TestMethod]
    public void TodayAndNowCanUseADeterministicClockContext()
    {
        var current = new DateTime(2030, 4, 5, 6, 7, 8);
        var context = new FormulaSurfaceTestContext(
            currentDateTime: current);
        var engine = new NeraFormulaEngine();

        Assert.AreEqual(
            current.Date,
            engine.Evaluate("=TODAY()", context).Value.RawValue);
        Assert.AreEqual(
            current,
            engine.Evaluate("=NOW()", context).Value.RawValue);
    }

    private static string Text(
        NeraFormulaEngine engine,
        IFormulaEvaluationContext context,
        string formula)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(result.IsSuccess);
        return result.Value.ToString();
    }

    private static double Number(
        NeraFormulaEngine engine,
        IFormulaEvaluationContext context,
        string formula)
    {
        var result = engine.Evaluate(formula, context);
        Assert.IsTrue(result.IsSuccess);
        return (double)result.Value.RawValue!;
    }
}
