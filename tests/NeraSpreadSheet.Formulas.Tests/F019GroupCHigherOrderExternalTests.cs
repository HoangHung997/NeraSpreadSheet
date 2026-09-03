using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

[TestClass]
public sealed class F019GroupCHigherOrderExternalTests
{
    private readonly NeraFormulaEngine _scalar = new();
    private readonly NeraDynamicArrayFormulaEngine _dynamic = new();

    [TestMethod] public void Bycol_Contract_IsValidated()
    {
        var r=Array("=BYCOL(A1:B2,LAMBDA(x,SUM(x)))",Context((0,0,1),(0,1,2),(1,0,3),(1,1,4)));
        Assert.AreEqual(1,r.RowCount);Assert.AreEqual(2,r.ColumnCount);Assert.AreEqual(4d,N(r[0,0]));Assert.AreEqual(6d,N(r[0,1]));
    }
    [TestMethod] public void Byrow_Contract_IsValidated()
    {
        var r=Array("=BYROW(A1:B2,LAMBDA(x,SUM(x)))",Context((0,0,1),(0,1,2),(1,0,3),(1,1,4)));
        Assert.AreEqual(2,r.RowCount);Assert.AreEqual(3d,N(r[0,0]));Assert.AreEqual(7d,N(r[1,0]));
    }
    [TestMethod] public void Makearray_Contract_IsValidated()
    {
        var r=Array("=MAKEARRAY(2,2,LAMBDA(r,c,r+c))",TestContext.Empty);
        Assert.AreEqual(2d,N(r[0,0]));Assert.AreEqual(3d,N(r[0,1]));Assert.AreEqual(4d,N(r[1,1]));
    }
    [TestMethod] public void Map_Contract_IsValidated()
    {
        var r=Array("=MAP(A1:B2,LAMBDA(x,x*2))",Context((0,0,1),(0,1,2),(1,0,3),(1,1,4)));
        Assert.AreEqual(2d,N(r[0,0]));Assert.AreEqual(8d,N(r[1,1]));
    }
    [TestMethod] public void Reduce_Contract_IsValidated()
    {
        var r=Array("=REDUCE(0,A1:A3,LAMBDA(a,v,a+v))",Context((0,0,1),(1,0,2),(2,0,3)));
        Assert.AreEqual(6d,N(r[0,0]));
    }
    [TestMethod] public void Scan_Contract_IsValidated()
    {
        var r=Array("=SCAN(0,A1:A3,LAMBDA(a,v,a+v))",Context((0,0,1),(1,0,2),(2,0,3)));
        Assert.AreEqual(3,r.RowCount);Assert.AreEqual(1d,N(r[0,0]));Assert.AreEqual(3d,N(r[1,0]));Assert.AreEqual(6d,N(r[2,0]));
    }
    [TestMethod] public void Lambda_Contract_IsValidated()
    {
        var r=_scalar.Evaluate("=LAMBDA(x,x+1)",TestContext.Empty);Assert.IsFalse(r.IsSuccess);Assert.AreEqual("#CALC!",r.Value.RawValue);
    }
    [TestMethod] public void Let_Contract_IsValidated()=>AssertNumber("=LET(x,2,y,3,x*y+1)",7,TestContext.Empty);
    [TestMethod] public void Isomitted_Contract_IsValidated()=>AssertBoolean("=ISOMITTED()",true,TestContext.Empty);
    [TestMethod] public void Call_Contract_IsValidated()=>AssertText("=CALL(\"lib\",\"proc\",\"J\")","call",ExternalContext());
    [TestMethod] public void RegisterId_Contract_IsValidated()=>AssertText("=REGISTER.ID(\"lib\",\"proc\")","register",ExternalContext());
    [TestMethod] public void Cubekpimember_Contract_IsValidated()=>AssertText("=CUBEKPIMEMBER(\"conn\",\"kpi\",1)","cube-kpi",ExternalContext());
    [TestMethod] public void Cubemember_Contract_IsValidated()=>AssertText("=CUBEMEMBER(\"conn\",\"member\")","cube-member",ExternalContext());
    [TestMethod] public void Cubememberproperty_Contract_IsValidated()=>AssertText("=CUBEMEMBERPROPERTY(\"conn\",\"member\",\"caption\")","cube-property",ExternalContext());
    [TestMethod] public void Cuberankedmember_Contract_IsValidated()=>AssertText("=CUBERANKEDMEMBER(\"conn\",\"set\",1)","cube-ranked",ExternalContext());
    [TestMethod] public void Cubeset_Contract_IsValidated()=>AssertText("=CUBESET(\"conn\",\"set\")","cube-set",ExternalContext());
    [TestMethod] public void Cubesetcount_Contract_IsValidated()=>AssertNumber("=CUBESETCOUNT(\"handle\")",3,ExternalContext());
    [TestMethod] public void Cubevalue_Contract_IsValidated()=>AssertNumber("=CUBEVALUE(\"conn\",\"member\")",42,ExternalContext());
    [TestMethod] public void Rtd_Contract_IsValidated()=>AssertNumber("=RTD(\"prog\",\"server\",\"topic\")",7,ExternalContext());
    [TestMethod] public void Copilot_Contract_IsValidated()=>AssertText("=COPILOT(\"prompt\")","answer",ExternalContext());
    [TestMethod] public void ExternalProviderExceptions_FailClosed()
    {
        var r=_scalar.Evaluate("=CALL(\"lib\",\"proc\",\"J\")",ThrowingExternalContext.Instance);
        Assert.IsFalse(r.IsSuccess);
        Assert.AreEqual("#N/A",r.Value.RawValue);
    }

    private FormulaArrayValue Array(string f,IFormulaEvaluationContext c){Assert.IsTrue(_dynamic.TryEvaluate(f,c,out var r),f);Assert.IsTrue(r.IsSuccess,f+" => "+r.ErrorValue.RawValue);return r.Value!;}
    private void AssertNumber(string f,double e,IFormulaEvaluationContext c){var r=_scalar.Evaluate(f,c);Assert.IsTrue(r.IsSuccess,f+" => "+r.Value.RawValue);Assert.AreEqual(e,N(r.Value),1e-10,f);}
    private void AssertText(string f,string e,IFormulaEvaluationContext c){var r=_scalar.Evaluate(f,c);Assert.IsTrue(r.IsSuccess,f+" => "+r.Value.RawValue);Assert.AreEqual(e,r.Value.RawValue,f);}
    private void AssertBoolean(string f,bool e,IFormulaEvaluationContext c){var r=_scalar.Evaluate(f,c);Assert.IsTrue(r.IsSuccess,f);Assert.AreEqual(e,r.Value.RawValue,f);}
    private static double N(CellValue v){Assert.AreEqual(CellValueKind.Number,v.Kind);return (double)v.RawValue!;}
    private static TestContext Context(params (int Row,int Column,double Value)[] cells)=>new(cells.ToDictionary(x=>new CellAddress(x.Row,x.Column),x=>CellValue.FromNumber(x.Value)));
    private static TestContext ExternalContext()=>new(new Dictionary<CellAddress,CellValue>(),true);

    private sealed class TestContext:IFormulaExternalFunctionContext
    {
        private readonly IReadOnlyDictionary<CellAddress,CellValue> _values;private readonly bool _external;
        public static TestContext Empty{get;}=new(new Dictionary<CellAddress,CellValue>());
        public TestContext(IReadOnlyDictionary<CellAddress,CellValue> values,bool external=false){_values=values;_external=external;}
        public CellValue GetCellValue(string? worksheetName,CellAddress address)=>_values.GetValueOrDefault(address,CellValue.Blank);
        public bool TryEvaluateExternalFunction(string name,IReadOnlyList<CellValue> args,out CellValue value)
        {
            if(!_external){value=CellValue.Blank;return false;}
            value=name switch
            {
                "CALL"=>CellValue.FromText("call"),"REGISTER.ID"=>CellValue.FromText("register"),"CUBEKPIMEMBER"=>CellValue.FromText("cube-kpi"),
                "CUBEMEMBER"=>CellValue.FromText("cube-member"),"CUBEMEMBERPROPERTY"=>CellValue.FromText("cube-property"),"CUBERANKEDMEMBER"=>CellValue.FromText("cube-ranked"),
                "CUBESET"=>CellValue.FromText("cube-set"),"CUBESETCOUNT"=>CellValue.FromNumber(3),"CUBEVALUE"=>CellValue.FromNumber(42),"RTD"=>CellValue.FromNumber(7),"COPILOT"=>CellValue.FromText("answer"),
                _=>CellValue.Blank,
            };
            return name is "CALL" or "REGISTER.ID" or "CUBEKPIMEMBER" or "CUBEMEMBER" or "CUBEMEMBERPROPERTY" or "CUBERANKEDMEMBER" or "CUBESET" or "CUBESETCOUNT" or "CUBEVALUE" or "RTD" or "COPILOT";
        }
        public bool TryEvaluateExternalArrayFunction(string functionName,IReadOnlyList<CellValue> arguments,out FormulaArrayValue value){value=null!;return false;}
    }
    private sealed class ThrowingExternalContext:IFormulaExternalFunctionContext
    {
        public static ThrowingExternalContext Instance{get;}=new();
        public CellValue GetCellValue(string? worksheetName,CellAddress address)=>CellValue.Blank;
        public bool TryEvaluateExternalFunction(string name,IReadOnlyList<CellValue> args,out CellValue value){value=CellValue.Blank;throw new InvalidOperationException("External provider failed.");}
        public bool TryEvaluateExternalArrayFunction(string functionName,IReadOnlyList<CellValue> arguments,out FormulaArrayValue value){value=null!;throw new InvalidOperationException("External array provider failed.");}
    }
}
