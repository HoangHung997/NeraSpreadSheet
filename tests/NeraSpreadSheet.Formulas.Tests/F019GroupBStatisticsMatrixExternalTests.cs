using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class F019GroupBStatisticsMatrixExternalTests
{
    private readonly NeraFormulaEngine _scalar = new();
    private readonly NeraDynamicArrayFormulaEngine _dynamic = new();

    [TestMethod] public void ForecastEts_Contract_IsValidated()
    {
        var c = Context((0,0,5),(1,0,5),(2,0,5),(0,1,1),(1,1,2),(2,1,3));
        AssertNumber("=FORECAST.ETS(4,A1:A3,B1:B3)", 5, c);
    }
    [TestMethod] public void ForecastEtsConfint_Contract_IsValidated()
    {
        var c = Context((0,0,5),(1,0,5),(2,0,5),(0,1,1),(1,1,2),(2,1,3));
        AssertNumber("=FORECAST.ETS.CONFINT(4,A1:A3,B1:B3)", 0, c);
    }
    [TestMethod] public void ForecastEtsSeasonality_Contract_IsValidated()
    {
        var c = Context((0,0,1),(1,0,2),(2,0,1),(3,0,2),(0,1,1),(1,1,2),(2,1,3),(3,1,4));
        AssertNumber("=FORECAST.ETS.SEASONALITY(A1:A4,B1:B4)", 2, c);
    }
    [TestMethod] public void ForecastEtsStat_Contract_IsValidated()
    {
        var c = Context((0,0,5),(1,0,5),(2,0,5),(0,1,1),(1,1,2),(2,1,3));
        AssertNumber("=FORECAST.ETS.STAT(A1:A3,B1:B3,6)", 0, c);
    }
    [TestMethod] public void Growth_Contract_IsValidated()
    {
        var c = Context((0,0,6),(1,0,18),(2,0,54),(0,1,1),(1,1,2),(2,1,3),(3,1,4));
        var result = EvaluateArray("=GROWTH(A1:A3,B1:B3,B4)", c);
        Assert.AreEqual(162d, Number(result[0,0]), 1e-8);
    }
    [TestMethod] public void Linest_Contract_IsValidated()
    {
        var c = Context((0,0,3),(1,0,5),(2,0,7),(0,1,1),(1,1,2),(2,1,3));
        var result = EvaluateArray("=LINEST(A1:A3,B1:B3)", c);
        Assert.AreEqual(1, result.RowCount); Assert.AreEqual(2, result.ColumnCount);
        Assert.AreEqual(2d, Number(result[0,0]), 1e-10); Assert.AreEqual(1d, Number(result[0,1]), 1e-10);
    }
    [TestMethod] public void Logest_Contract_IsValidated()
    {
        var c = Context((0,0,6),(1,0,18),(2,0,54),(0,1,1),(1,1,2),(2,1,3));
        var result = EvaluateArray("=LOGEST(A1:A3,B1:B3)", c);
        Assert.AreEqual(3d, Number(result[0,0]), 1e-8); Assert.AreEqual(2d, Number(result[0,1]), 1e-8);
    }
    [TestMethod] public void Maxifs_Contract_IsValidated()
    {
        var c = Context((0,0,4),(1,0,9),(2,0,7),(0,1,1),(1,1,2),(2,1,3));
        AssertNumber("=MAXIFS(A1:A3,B1:B3,\">1\")", 9, c);
    }
    [TestMethod] public void Minifs_Contract_IsValidated()
    {
        var c = Context((0,0,4),(1,0,9),(2,0,7),(0,1,1),(1,1,2),(2,1,3));
        AssertNumber("=MINIFS(A1:A3,B1:B3,\">1\")", 7, c);
    }
    [TestMethod] public void Minverse_Contract_IsValidated()
    {
        var c = Context((0,0,4),(0,1,7),(1,0,2),(1,1,6));
        var r = EvaluateArray("=MINVERSE(A1:B2)", c);
        Assert.AreEqual(.6, Number(r[0,0]), 1e-10); Assert.AreEqual(-.7, Number(r[0,1]), 1e-10);
        Assert.AreEqual(-.2, Number(r[1,0]), 1e-10); Assert.AreEqual(.4, Number(r[1,1]), 1e-10);
    }
    [TestMethod] public void Mmult_Contract_IsValidated()
    {
        var c = Context((0,0,1),(0,1,2),(1,0,3),(1,1,4),(0,2,5),(1,2,6));
        var r = EvaluateArray("=MMULT(A1:B2,C1:C2)", c);
        Assert.AreEqual(17d, Number(r[0,0]), 1e-10); Assert.AreEqual(39d, Number(r[1,0]), 1e-10);
    }
    [TestMethod] public void ModeMult_Contract_IsValidated()
    {
        var c = Context((0,0,1),(1,0,1),(2,0,2),(3,0,2),(4,0,3));
        var r = EvaluateArray("=MODE.MULT(A1:A5)", c);
        Assert.AreEqual(2, r.RowCount); Assert.AreEqual(1d, Number(r[0,0])); Assert.AreEqual(2d, Number(r[1,0]));
    }
    [TestMethod] public void Randarray_Contract_IsValidated()
    {
        var r = EvaluateArray("=RANDARRAY(2,3,5,6,TRUE)", TestContext.Empty);
        Assert.AreEqual(2, r.RowCount); Assert.AreEqual(3, r.ColumnCount);
        foreach (var v in r.ToArray()) Assert.IsTrue(Number(v) is 5d or 6d);
    }
    [TestMethod] public void Textsplit_Contract_IsValidated()
    {
        var r = EvaluateArray("=TEXTSPLIT(\"a,b;c,d\",\",\",\";\")", TestContext.Empty);
        Assert.AreEqual(2, r.RowCount); Assert.AreEqual(2, r.ColumnCount);
        Assert.AreEqual("a", r[0,0].RawValue); Assert.AreEqual("d", r[1,1].RawValue);
    }
    [TestMethod] public void Trend_Contract_IsValidated()
    {
        var c = Context((0,0,3),(1,0,5),(2,0,7),(0,1,1),(1,1,2),(2,1,3),(3,1,4));
        var r = EvaluateArray("=TREND(A1:A3,B1:B3,B4)", c);
        Assert.AreEqual(9d, Number(r[0,0]), 1e-10);
    }
    [TestMethod] public void Image_Contract_IsValidated()=>AssertText("=IMAGE(\"https://example.invalid/x.png\")","image-ok",ExternalContext());
    [TestMethod] public void Detectlanguage_Contract_IsValidated()=>AssertText("=DETECTLANGUAGE(\"bonjour\")","fr",ExternalContext());
    [TestMethod] public void Translate_Contract_IsValidated()=>AssertText("=TRANSLATE(\"hello\",\"fr\")","bonjour",ExternalContext());
    [TestMethod] public void Webservice_Contract_IsValidated()=>AssertText("=WEBSERVICE(\"https://example.invalid/data\")","payload",ExternalContext());
    [TestMethod] public void Stockhistory_Contract_IsValidated()
    {
        var r = EvaluateArray("=STOCKHISTORY(\"MSFT\",1)", ExternalContext());
        Assert.AreEqual(2, r.RowCount); Assert.AreEqual("Date", r[0,0].RawValue); Assert.AreEqual(100d, Number(r[1,1]));
    }
    [TestMethod] public void ExternalProviderExceptions_FailClosed()
    {
        var scalar = _scalar.Evaluate("=WEBSERVICE(\"https://example.invalid/data\")", ThrowingExternalContext.Instance);
        Assert.IsFalse(scalar.IsSuccess);
        Assert.AreEqual("#N/A", scalar.Value.RawValue);

        Assert.IsTrue(_dynamic.TryEvaluate("=STOCKHISTORY(\"MSFT\",1)", ThrowingExternalContext.Instance, out var array));
        Assert.IsFalse(array.IsSuccess);
        Assert.AreEqual("#N/A", array.ErrorValue.RawValue);
    }

    private FormulaArrayValue EvaluateArray(string formula, IFormulaEvaluationContext context)
    {
        Assert.IsTrue(_dynamic.TryEvaluate(formula, context, out var result), formula);
        Assert.IsTrue(result.IsSuccess, formula + " => " + result.ErrorValue.RawValue);
        return result.Value!;
    }
    private void AssertNumber(string formula,double expected,IFormulaEvaluationContext context,double tolerance=1e-10)
    {var r=_scalar.Evaluate(formula,context);Assert.IsTrue(r.IsSuccess,formula+" => "+r.Value.RawValue);Assert.AreEqual(expected,Number(r.Value),tolerance,formula);}
    private void AssertText(string formula,string expected,IFormulaEvaluationContext context)
    {var r=_scalar.Evaluate(formula,context);Assert.IsTrue(r.IsSuccess,formula+" => "+r.Value.RawValue);Assert.AreEqual(expected,r.Value.RawValue,formula);}
    private static double Number(CellValue value){Assert.AreEqual(CellValueKind.Number,value.Kind);return (double)value.RawValue!;}
    private static TestContext Context(params (int Row,int Column,double Value)[] cells)=>new(cells.ToDictionary(x=>new CellAddress(x.Row,x.Column),x=>CellValue.FromNumber(x.Value)));
    private static TestContext ExternalContext()=>new(new Dictionary<CellAddress,CellValue>(),external:true);

    private sealed class TestContext : IFormulaExternalFunctionContext
    {
        private readonly IReadOnlyDictionary<CellAddress,CellValue> _values;
        private readonly bool _external;
        public static TestContext Empty { get; } = new(new Dictionary<CellAddress,CellValue>());
        public TestContext(IReadOnlyDictionary<CellAddress,CellValue> values,bool external=false){_values=values;_external=external;}
        public CellValue GetCellValue(string? worksheetName,CellAddress address)=>_values.GetValueOrDefault(address,CellValue.Blank);
        public bool TryEvaluateExternalFunction(string functionName,IReadOnlyList<CellValue> arguments,out CellValue value)
        {
            if(!_external){value=CellValue.Blank;return false;}
            value=functionName switch
            {
                "IMAGE"=>CellValue.FromText("image-ok"),
                "DETECTLANGUAGE"=>CellValue.FromText("fr"),
                "TRANSLATE"=>CellValue.FromText("bonjour"),
                "WEBSERVICE"=>CellValue.FromText("payload"),
                _=>CellValue.Blank,
            };
            return functionName is "IMAGE" or "DETECTLANGUAGE" or "TRANSLATE" or "WEBSERVICE";
        }
        public bool TryEvaluateExternalArrayFunction(string functionName,IReadOnlyList<CellValue> arguments,out FormulaArrayValue value)
        {
            if(_external&&functionName=="STOCKHISTORY")
            {
                value=FormulaArrayValue.FromRows([[CellValue.FromText("Date"),CellValue.FromText("Close")],[CellValue.FromNumber(1),CellValue.FromNumber(100)]]);
                return true;
            }
            value=null!;return false;
        }
    }
    private sealed class ThrowingExternalContext : IFormulaExternalFunctionContext
    {
        public static ThrowingExternalContext Instance { get; } = new();
        public CellValue GetCellValue(string? worksheetName,CellAddress address)=>CellValue.Blank;
        public bool TryEvaluateExternalFunction(string functionName,IReadOnlyList<CellValue> arguments,out CellValue value)
        {
            value=CellValue.Blank;
            throw new InvalidOperationException("External provider failed.");
        }
        public bool TryEvaluateExternalArrayFunction(string functionName,IReadOnlyList<CellValue> arguments,out FormulaArrayValue value)
        {
            value=null!;
            throw new InvalidOperationException("External array provider failed.");
        }
    }
}
