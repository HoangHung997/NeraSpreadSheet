using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class F018GroupATextCompatibilityTests
{
    private readonly NeraFormulaEngine _engine = new();
    [TestMethod] public void F018_ASC_Compatibility() { AssertText("=ASC(\"ＡＢＣ１２３\")", "ABC123"); AssertText("=ASC(\"ガ\")", "ｶﾞ"); }
    [TestMethod] public void F018_ARRAYTOTEXT_Compatibility() { var c=ColumnContext(CellValue.FromNumber(1),CellValue.FromText("x")); AssertText("=ARRAYTOTEXT(A1:A2,1)", "{1;\"x\"}",c); }
    [TestMethod] public void F018_BAHTTEXT_Compatibility() { AssertText("=BAHTTEXT(21)", "ยี่สิบเอ็ดบาทถ้วน"); AssertText("=BAHTTEXT(1.25)", "หนึ่งบาทยี่สิบห้าสตางค์"); }
    [TestMethod] public void F018_CONCATENATE_Compatibility() { var c=ColumnContext(CellValue.FromText("A"),CellValue.FromText("B")); AssertText("=CONCATENATE(A1:A2,\"C\")", "ABC",c); }
    [TestMethod] public void F018_DBCS_Compatibility() { AssertText("=DBCS(\"ABC 123\")", "ＡＢＣ　１２３"); AssertText("=DBCS(\"ｶﾞ\")", "ガ"); }
    [TestMethod] public void F018_DOLLAR_Compatibility() { AssertText("=DOLLAR(1234.567,2)", "$1,234.57"); AssertText("=DOLLAR(-12.5,0)", "($13)"); }
    [TestMethod] public void F018_FINDB_Compatibility() { AssertNumber("=FINDB(\"B\",\"AＢB\")",4); AssertError("=FINDB(\"x\",\"abc\")","#VALUE!"); }
    [TestMethod] public void F018_FIXED_Compatibility() { AssertText("=FIXED(1234.567,1,FALSE())","1,234.6"); AssertText("=FIXED(1234.567,1,TRUE())","1234.6"); }
    [TestMethod] public void F018_JIS_Compatibility() { AssertText("=JIS(\"ABC\")","ＡＢＣ"); }
    [TestMethod] public void F018_LEFTB_Compatibility() { AssertText("=LEFTB(\"AＢC\",3)","AＢ"); }
    [TestMethod] public void F018_LENB_Compatibility() { AssertNumber("=LENB(\"AＢC\")",4); }
    [TestMethod] public void F018_MIDB_Compatibility() { AssertText("=MIDB(\"AＢCD\",2,2)","Ｂ"); }
    [TestMethod] public void F018_REGEXEXTRACT_Compatibility() { AssertText("=REGEXEXTRACT(\"abc123\",\"[0-9]+\")","123"); AssertError("=REGEXEXTRACT(\"abc\",\"[0-9]+\")","#N/A"); }
    [TestMethod] public void F018_REGEXREPLACE_Compatibility() { AssertText("=REGEXREPLACE(\"a1b2\",\"[0-9]\",\"X\")","aXbX"); AssertText("=REGEXREPLACE(\"a1b2\",\"[0-9]\",\"X\",2)","a1bX"); }
    [TestMethod] public void F018_REGEXTEST_Compatibility() { AssertBoolean("=REGEXTEST(\"Abc\",\"abc\",TRUE())",true); AssertBoolean("=REGEXTEST(\"Abc\",\"^z\")",false); }
    [TestMethod] public void F018_REPLACEB_Compatibility() { AssertText("=REPLACEB(\"AＢC\",2,2,\"X\")","AXC"); }
    [TestMethod] public void F018_RIGHTB_Compatibility() { AssertText("=RIGHTB(\"AＢC\",3)","ＢC"); }
    [TestMethod] public void F018_SEARCHB_Compatibility() { AssertNumber("=SEARCHB(\"c*\",\"AＢcd\")",4); }
    [TestMethod] public void F018_TEXTAFTER_Compatibility() { AssertText("=TEXTAFTER(\"a-b-c\",\"-\",2)","c"); AssertText("=TEXTAFTER(\"a-b-c\",\"-\",-1)","c"); }
    [TestMethod] public void F018_TEXTBEFORE_Compatibility() { AssertText("=TEXTBEFORE(\"a-b-c\",\"-\",2)","a-b"); AssertText("=TEXTBEFORE(\"a-b-c\",\"-\",-1)","a-b"); }
    private static TestContext ColumnContext(params CellValue[] values){var cells=new Dictionary<CellAddress,CellValue>();for(var i=0;i<values.Length;i++)cells[new CellAddress(i,0)]=values[i];return new TestContext(cells);}
    private void AssertText(string formula,string expected,TestContext? context=null){var r=_engine.Evaluate(formula,context??TestContext.Empty);Assert.IsTrue(r.IsSuccess,formula+" => "+r.Value.RawValue);Assert.AreEqual(CellValueKind.Text,r.Value.Kind,formula);Assert.AreEqual(expected,r.Value.RawValue,formula);}
    private void AssertNumber(string formula,double expected,TestContext? context=null){var r=_engine.Evaluate(formula,context??TestContext.Empty);Assert.IsTrue(r.IsSuccess,formula+" => "+r.Value.RawValue);Assert.AreEqual(CellValueKind.Number,r.Value.Kind,formula);Assert.AreEqual(expected,(double)r.Value.RawValue!,1e-10,formula);}
    private void AssertBoolean(string formula,bool expected){var r=_engine.Evaluate(formula,TestContext.Empty);Assert.IsTrue(r.IsSuccess,formula);Assert.AreEqual(expected,r.Value.RawValue,formula);}
    private void AssertError(string formula,string expected){var r=_engine.Evaluate(formula,TestContext.Empty);Assert.IsFalse(r.IsSuccess,formula);Assert.AreEqual(expected,r.Value.RawValue,formula);}
    private sealed class TestContext:IFormulaEvaluationContext{private readonly IReadOnlyDictionary<CellAddress,CellValue> _values;public static TestContext Empty{get;}=new(new Dictionary<CellAddress,CellValue>());public TestContext(IReadOnlyDictionary<CellAddress,CellValue> values)=>_values=values;public CellValue GetCellValue(string? worksheetName,CellAddress address)=>_values.GetValueOrDefault(address,CellValue.Blank);}
}
