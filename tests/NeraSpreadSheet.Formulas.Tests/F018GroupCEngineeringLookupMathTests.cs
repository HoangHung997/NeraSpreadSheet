using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
namespace NeraSpreadSheet.Formulas.Tests;
[TestClass] public sealed class F018GroupCEngineeringLookupMathTests
{
    private readonly NeraFormulaEngine _engine=new();
    private readonly NeraDynamicArrayFormulaEngine _arrays=new();
    [TestMethod] public void F018_CONVERT_Compatibility(){AssertNumber("=CONVERT(1,\"km\",\"m\")",1000);AssertNumber("=CONVERT(0,\"C\",\"K\")",273.15,1e-10);AssertError("=CONVERT(1,\"m\",\"kg\")","#N/A");}
    [TestMethod] public void F018_ERF_Compatibility(){AssertNumber("=ERF(0)",0,1e-7);AssertNumber("=ERF(0,1)",0.8427006897475899,5e-7);}
    [TestMethod] public void F018_ERF_PRECISE_Compatibility()=>AssertNumber("=ERF.PRECISE(1)",0.8427006897475899,5e-7);
    [TestMethod] public void F018_ERFC_Compatibility()=>AssertNumber("=ERFC(0)",1,1e-7);
    [TestMethod] public void F018_ERFC_PRECISE_Compatibility()=>AssertNumber("=ERFC.PRECISE(1)",0.1572993102524101,5e-7);
    [TestMethod] public void F018_BESSELI_Compatibility()=>AssertNumber("=BESSELI(0,0)",1,1e-12);
    [TestMethod] public void F018_BESSELJ_Compatibility()=>AssertNumber("=BESSELJ(0,0)",1,1e-12);
    [TestMethod] public void F018_BESSELK_Compatibility()=>AssertNumber("=BESSELK(100,0)",4.6624238126346715e-45,1e-55);
    [TestMethod] public void F018_BESSELY_Compatibility()=>AssertNumber("=BESSELY(100,0)",-0.07721975456219661,1e-12);
    [TestMethod] public void F018_HLOOKUP_Compatibility(){var c=Grid(new double[,]{{1,2,3},{10,20,30}});AssertNumber("=HLOOKUP(2,A1:C2,2,FALSE())",20,context:c);}
    [TestMethod] public void F018_VLOOKUP_Compatibility(){var c=Grid(new double[,]{{1,10},{2,20},{3,30}});AssertNumber("=VLOOKUP(2,A1:B3,2,FALSE())",20,context:c);}
    [TestMethod] public void F018_INDEX_Compatibility(){var c=Grid(new double[,]{{1,2},{3,4}});AssertNumber("=INDEX(A1:B2,2,2)",4,context:c);}
    [TestMethod] public void F018_MATCH_Compatibility(){var c=Grid(new double[,]{{1},{2},{3}});AssertNumber("=MATCH(2,A1:A3,0)",2,context:c);}
    [TestMethod] public void F018_XLOOKUP_Compatibility(){var c=Grid(new double[,]{{1,10},{2,20},{3,30}});AssertNumber("=XLOOKUP(2,A1:A3,B1:B3,\"missing\")",20,context:c);AssertText("=XLOOKUP(9,A1:A3,B1:B3,\"missing\")","missing",c);}
    [TestMethod] public void F018_AGGREGATE_Compatibility(){var c=Grid(new double[,]{{1},{2},{3}});AssertNumber("=AGGREGATE(9,0,A1:A3)",6,context:c);}
    [TestMethod] public void F018_RAND_Compatibility(){var r=_engine.Evaluate("=RAND()",TestContext.Empty);Assert.IsTrue(r.IsSuccess);var v=(double)r.Value.RawValue!;Assert.IsTrue(v>=0&&v<1);}
    [TestMethod] public void F018_RANDBETWEEN_Compatibility(){var r=_engine.Evaluate("=RANDBETWEEN(2,4)",TestContext.Empty);Assert.IsTrue(r.IsSuccess);var v=(double)r.Value.RawValue!;Assert.IsTrue(v>=2&&v<=4&&v==Math.Truncate(v));}
    [TestMethod] public void F018_MDETERM_Compatibility(){var c=Grid(new double[,]{{1,2},{3,4}});AssertNumber("=MDETERM(A1:B2)",-2,1e-10,c);}
    [TestMethod] public void F018_MUNIT_Compatibility(){Assert.IsTrue(_arrays.TryEvaluate("=MUNIT(3)",TestContext.Empty,out var r));Assert.IsTrue(r.IsSuccess);Assert.AreEqual(3,r.Value!.RowCount);Assert.AreEqual(3,r.Value.ColumnCount);for(int y=0;y<3;y++)for(int x=0;x<3;x++)Assert.AreEqual(y==x?1d:0d,(double)r.Value[y,x].RawValue!);}
    [TestMethod] public void F018_FREQUENCY_Compatibility(){var c=Grid(new double[,]{{1,2},{2,4},{3,0},{4,0},{5,0}});Assert.IsTrue(_arrays.TryEvaluate("=FREQUENCY(A1:A5,B1:B2)",c,out var r));Assert.IsTrue(r.IsSuccess);Assert.AreEqual(3,r.Value!.RowCount);Assert.AreEqual(1,r.Value.ColumnCount);Assert.AreEqual(2d,(double)r.Value[0,0].RawValue!);Assert.AreEqual(2d,(double)r.Value[1,0].RawValue!);Assert.AreEqual(1d,(double)r.Value[2,0].RawValue!);}
    private static TestContext Grid(double[,] values){var d=new Dictionary<CellAddress,CellValue>();for(int r=0;r<values.GetLength(0);r++)for(int c=0;c<values.GetLength(1);c++)d[new CellAddress(r,c)]=CellValue.FromNumber(values[r,c]);return new TestContext(d);}
    private void AssertNumber(string formula,double expected,double tolerance=1e-10,TestContext? context=null){var r=_engine.Evaluate(formula,context??TestContext.Empty);Assert.IsTrue(r.IsSuccess,formula+" => "+r.Value.RawValue);Assert.AreEqual(CellValueKind.Number,r.Value.Kind,formula);Assert.AreEqual(expected,(double)r.Value.RawValue!,tolerance,formula);}
    private void AssertText(string formula,string expected,TestContext c){var r=_engine.Evaluate(formula,c);Assert.IsTrue(r.IsSuccess,formula+" => "+r.Value.RawValue);Assert.AreEqual(expected,r.Value.RawValue,formula);}
    private void AssertError(string formula,string expected){var r=_engine.Evaluate(formula,TestContext.Empty);Assert.IsFalse(r.IsSuccess,formula);Assert.AreEqual(expected,r.Value.RawValue,formula);}
    private sealed class TestContext:IFormulaEvaluationContext{private readonly IReadOnlyDictionary<CellAddress,CellValue> _values;public static TestContext Empty{get;}=new(new Dictionary<CellAddress,CellValue>());public TestContext(IReadOnlyDictionary<CellAddress,CellValue> values)=>_values=values;public CellValue GetCellValue(string? worksheetName,CellAddress address)=>_values.GetValueOrDefault(address,CellValue.Blank);}
}
