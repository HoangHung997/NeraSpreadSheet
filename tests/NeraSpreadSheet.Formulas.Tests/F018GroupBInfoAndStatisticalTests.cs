using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
namespace NeraSpreadSheet.Formulas.Tests;
[TestClass] public sealed class F018GroupBInfoAndStatisticalTests
{
    private readonly NeraFormulaEngine _engine=new();
    [TestMethod] public void F018_TEXT_Compatibility()=>AssertText("=TEXT(1234.5,\"0.00\")","1234.50");
    [TestMethod] public void F018_VALUETOTEXT_Compatibility()=>AssertText("=VALUETOTEXT(\"x\",1)","\"x\"");
    [TestMethod] public void F018_ENCODEURL_Compatibility()=>AssertText("=ENCODEURL(\"a b\")","a%20b");
    [TestMethod] public void F018_CELL_Compatibility(){var c=Context((0,0,7d));AssertText("=CELL(\"address\",A1)","$A$1",c);AssertNumber("=CELL(\"contents\",A1)",7,c);}
    [TestMethod] public void F018_ERROR_TYPE_Compatibility(){AssertNumber("=ERROR.TYPE(1/0)",2);AssertError("=ERROR.TYPE(1)","#N/A");}
    [TestMethod] public void F018_ISFORMULA_Compatibility(){var c=Context((0,0,2));c.Formulas[new CellAddress(0,0)]="=1+1";AssertBoolean("=ISFORMULA(A1)",true,c);AssertBoolean("=ISFORMULA(B1)",false,c);}
    [TestMethod] public void F018_ISREF_Compatibility(){AssertBoolean("=ISREF(A1)",true,TestContext.Empty);AssertBoolean("=ISREF(1)",false,TestContext.Empty);}
    [TestMethod] public void F018_TYPE_Compatibility(){AssertNumber("=TYPE(\"x\")",2);AssertNumber("=TYPE(TRUE())",4);}
    [TestMethod] public void F018_GAMMA_Compatibility(){AssertNumber("=GAMMA(5)",24,tolerance:1e-8);AssertError("=GAMMA(0)","#NUM!");}
    [TestMethod] public void F018_GAMMALN_Compatibility()=>AssertNumber("=GAMMALN(5)",Math.Log(24),tolerance:1e-8);
    [TestMethod] public void F018_GAMMALN_PRECISE_Compatibility()=>AssertNumber("=GAMMALN.PRECISE(5)",Math.Log(24),tolerance:1e-8);
    [TestMethod] public void F018_GAUSS_Compatibility()=>AssertNumber("=GAUSS(0)",0,tolerance:1e-7);
    [TestMethod] public void F018_PHI_Compatibility()=>AssertNumber("=PHI(0)",1/Math.Sqrt(2*Math.PI),tolerance:1e-10);
    [TestMethod] public void F018_PERMUT_Compatibility()=>AssertNumber("=PERMUT(5,2)",20);
    [TestMethod] public void F018_PERMUTATIONA_Compatibility()=>AssertNumber("=PERMUTATIONA(5,2)",25);
    [TestMethod] public void F018_CHISQ_TEST_Compatibility(){var c=Context((0,0,10),(1,0,20),(0,1,15),(1,1,15));AssertNumber("=CHISQ.TEST(A1:A2,B1:B2)",0.06788915486182903,c,1e-8);}
    [TestMethod] public void F018_T_TEST_Compatibility(){var c=Context((0,0,1),(1,0,2),(2,0,4),(0,1,1),(1,1,3),(2,1,5));AssertNumber("=T.TEST(A1:A3,B1:B3,2,1)",0.18350341907227397,c,1e-7);}
    [TestMethod] public void F018_PERCENTRANK_Compatibility(){var c=Context((0,0,1),(1,0,2),(2,0,3),(3,0,4),(4,0,5));AssertNumber("=PERCENTRANK(A1:A5,3)",.5,c);}
    [TestMethod] public void F018_CHITEST_Compatibility(){var c=Context((0,0,10),(1,0,20),(0,1,15),(1,1,15));AssertNumber("=CHITEST(A1:A2,B1:B2)",0.06788915486182903,c,1e-8);}
    [TestMethod] public void F018_TTEST_Compatibility(){var c=Context((0,0,1),(1,0,2),(2,0,4),(0,1,1),(1,1,3),(2,1,5));AssertNumber("=TTEST(A1:A3,B1:B3,2,1)",0.18350341907227397,c,1e-7);}
    private static TestContext Context(params (int Row,int Column,double Value)[] cells)=>new(cells.ToDictionary(x=>new CellAddress(x.Row,x.Column),x=>CellValue.FromNumber(x.Value)));
    private void AssertNumber(string f,double expected,TestContext? c=null,double tolerance=1e-10){var r=_engine.Evaluate(f,c??TestContext.Empty);Assert.IsTrue(r.IsSuccess,f+" => "+r.Value.RawValue);Assert.AreEqual(CellValueKind.Number,r.Value.Kind,f);Assert.AreEqual(expected,(double)r.Value.RawValue!,tolerance,f);}
    private void AssertText(string f,string expected,TestContext? c=null){var r=_engine.Evaluate(f,c??TestContext.Empty);Assert.IsTrue(r.IsSuccess,f+" => "+r.Value.RawValue);Assert.AreEqual(expected,r.Value.RawValue,f);}
    private void AssertBoolean(string f,bool expected,TestContext c){var r=_engine.Evaluate(f,c);Assert.IsTrue(r.IsSuccess,f);Assert.AreEqual(expected,r.Value.RawValue,f);}
    private void AssertError(string f,string expected){var r=_engine.Evaluate(f,TestContext.Empty);Assert.IsFalse(r.IsSuccess,f);Assert.AreEqual(expected,r.Value.RawValue,f);}
    private sealed class TestContext:IFormulaEvaluationContext,IFormulaReferenceIntrospectionContext{private readonly IReadOnlyDictionary<CellAddress,CellValue> _values;public static TestContext Empty{get;}=new(new Dictionary<CellAddress,CellValue>());public Dictionary<CellAddress,string> Formulas{get;}=new();public string CurrentWorksheetName=>"Sheet1";public CellAddress CurrentCellAddress=>new(0,0);public TestContext(IReadOnlyDictionary<CellAddress,CellValue> values)=>_values=values;public CellValue GetCellValue(string? worksheetName,CellAddress address)=>_values.GetValueOrDefault(address,CellValue.Blank);public bool TryGetCellFormula(string? worksheetName,CellAddress address,out string? formula)=>Formulas.TryGetValue(address,out formula);}
}
