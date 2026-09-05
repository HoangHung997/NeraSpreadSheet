using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class F019GroupADateTextCompatibilityTests
{
    private readonly NeraFormulaEngine _engine = new();

    [TestMethod] public void Daysinmonth_Contract_IsValidated() => AssertNumber("=DAYSINMONTH(DATE(1968,2,17))", 29d);
    [TestMethod] public void Daysinyear_Contract_IsValidated() => AssertNumber("=DAYSINYEAR(DATE(1968,2,29))", 366d);
    [TestMethod] public void Eastersunday_Contract_IsValidated() => AssertDate("=EASTERSUNDAY(2024)", new DateTime(2024, 3, 31));
    [TestMethod] public void Isleapyear_Contract_IsValidated() { AssertBoolean("=ISLEAPYEAR(DATE(2000,1,1))", true); AssertBoolean("=ISLEAPYEAR(DATE(1900,3,1))", false); }
    [TestMethod] public void Months_Contract_IsValidated() { AssertNumber("=MONTHS(DATE(2024,1,31),DATE(2024,3,30),0)", 1d); AssertNumber("=MONTHS(DATE(2024,1,31),DATE(2024,3,30),1)", 2d); }
    [TestMethod] public void WeeknumExcel2003_Contract_IsValidated() => AssertNumber("=WEEKNUM_EXCEL2003(DATE(2024,1,1))", 1d);
    [TestMethod] public void WeeknumOoo_Contract_IsValidated() => AssertNumber("=WEEKNUM_OOO(DATE(2021,1,1),2)", 53d);
    [TestMethod] public void WeeknumAdd_Contract_IsValidated() => AssertNumber("=WEEKNUM_ADD(DATE(2024,1,8),1)", 2d);
    [TestMethod] public void Weeks_Contract_IsValidated() { AssertNumber("=WEEKS(DATE(2022,1,12),DATE(2022,1,19),0)", 1d); AssertNumber("=WEEKS(DATE(2022,1,12),DATE(2022,1,17),1)", 1d); }
    [TestMethod] public void Weeksinyear_Contract_IsValidated() => AssertNumber("=WEEKSINYEAR(DATE(2020,2,1))", 53d);
    [TestMethod] public void Years_Contract_IsValidated() { AssertNumber("=YEARS(DATE(2020,5,20),DATE(2024,5,19),0)", 3d); AssertNumber("=YEARS(DATE(2020,5,20),DATE(2024,5,19),1)", 4d); }
    [TestMethod] public void Rot13_Contract_IsValidated() => AssertText("=ROT13(\"Gur Qbphzrag\")", "The Document");
    [TestMethod] public void Rawsubtract_Contract_IsValidated() => AssertNumber("=RAWSUBTRACT(10,1,2,3)", 4d);
    [TestMethod] public void Current_Contract_IsValidated() => AssertNumber("=CURRENT()", 42d, TestContext.WithCurrent(CellValue.FromNumber(42d)));
    [TestMethod] public void Formula_Contract_IsValidated() { var context = TestContext.WithFormula(new CellAddress(0, 0), "=1+2"); AssertText("=FORMULA(A1)", "=1+2", context); }
    [TestMethod] public void BinomDistRange_Contract_IsValidated() => AssertNumber("=BINOM.DIST.RANGE(4,0.5,1,2)", 0.625d);
    [TestMethod] public void Euroconvert_Contract_IsValidated() => AssertNumber("=EUROCONVERT(1,\"EUR\",\"DEM\",TRUE())", 1.95583d);
    [TestMethod] public void Info_Contract_IsValidated() => AssertText("=INFO(\"system\")", "TEST-HOST", TestContext.WithInfo("system", CellValue.FromText("TEST-HOST")));
    [TestMethod] public void Phonetic_Contract_IsValidated() => AssertText("=PHONETIC(\"東京\")", "東京");
    [TestMethod] public void Filterxml_Contract_IsValidated() { AssertText("=FILTERXML(\"<root><x>42</x></root>\",\"/root/x\")", "42"); AssertError("=FILTERXML(\"<root>\",\"/root\")", "#VALUE!"); }

    private void AssertNumber(string formula, double expected, TestContext? context = null)
    {
        var result = _engine.Evaluate(formula, context ?? TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula + " => " + result.Value.RawValue);
        Assert.AreEqual(CellValueKind.Number, result.Value.Kind, formula);
        Assert.AreEqual(expected, (double)result.Value.RawValue!, 1e-9, formula);
    }

    private void AssertDate(string formula, DateTime expected)
    {
        var result = _engine.Evaluate(formula, TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula + " => " + result.Value.RawValue);
        Assert.AreEqual(CellValueKind.DateTime, result.Value.Kind, formula);
        Assert.AreEqual(expected, (DateTime)result.Value.RawValue!, formula);
    }

    private void AssertText(string formula, string expected, TestContext? context = null)
    {
        var result = _engine.Evaluate(formula, context ?? TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula + " => " + result.Value.RawValue);
        Assert.AreEqual(CellValueKind.Text, result.Value.Kind, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }

    private void AssertBoolean(string formula, bool expected)
    {
        var result = _engine.Evaluate(formula, TestContext.Empty);
        Assert.IsTrue(result.IsSuccess, formula);
        Assert.AreEqual(CellValueKind.Boolean, result.Value.Kind, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }

    private void AssertError(string formula, string expected)
    {
        var result = _engine.Evaluate(formula, TestContext.Empty);
        Assert.IsFalse(result.IsSuccess, formula);
        Assert.AreEqual(expected, result.Value.RawValue, formula);
    }

    private sealed class TestContext :
        IFormulaEvaluationContext,
        IFormulaReferenceIntrospectionContext,
        IFormulaCurrentValueContext,
        IFormulaHostInfoContext
    {
        private readonly IReadOnlyDictionary<CellAddress, CellValue> _values;
        private readonly IReadOnlyDictionary<CellAddress, string> _formulas;
        private readonly IReadOnlyDictionary<string, CellValue> _info;

        public static TestContext Empty { get; } = new(
            new Dictionary<CellAddress, CellValue>(),
            new Dictionary<CellAddress, string>(),
            new Dictionary<string, CellValue>(),
            CellValue.Blank);

        private TestContext(
            IReadOnlyDictionary<CellAddress, CellValue> values,
            IReadOnlyDictionary<CellAddress, string> formulas,
            IReadOnlyDictionary<string, CellValue> info,
            CellValue current)
        {
            _values = values;
            _formulas = formulas;
            _info = info;
            CurrentFormulaCellValue = current;
        }

        public string CurrentWorksheetName => "Sheet1";
        public CellAddress CurrentCellAddress => new(0, 1);
        public CellValue CurrentFormulaCellValue { get; }

        public static TestContext WithCurrent(CellValue value) => new(
            new Dictionary<CellAddress, CellValue>(),
            new Dictionary<CellAddress, string>(),
            new Dictionary<string, CellValue>(),
            value);

        public static TestContext WithFormula(CellAddress address, string formula) => new(
            new Dictionary<CellAddress, CellValue>(),
            new Dictionary<CellAddress, string> { [address] = formula },
            new Dictionary<string, CellValue>(),
            CellValue.Blank);

        public static TestContext WithInfo(string key, CellValue value) => new(
            new Dictionary<CellAddress, CellValue>(),
            new Dictionary<CellAddress, string>(),
            new Dictionary<string, CellValue>(StringComparer.OrdinalIgnoreCase) { [key] = value },
            CellValue.Blank);

        public CellValue GetCellValue(string? worksheetName, CellAddress address) =>
            _values.GetValueOrDefault(address, CellValue.Blank);

        public bool TryGetCellFormula(string? worksheetName, CellAddress address, out string? formula)
        {
            if (_formulas.TryGetValue(address, out var found))
            {
                formula = found;
                return true;
            }
            formula = null;
            return false;
        }

        public bool TryGetFormulaInfo(string typeText, out CellValue value) =>
            _info.TryGetValue(typeText, out value);
    }
}
