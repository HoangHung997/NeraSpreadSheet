using NeraSpreadSheet.Core;
namespace NeraSpreadSheet.Formulas;
public sealed partial class NeraDynamicArrayFormulaEngine
{
    private FormulaArrayEvaluationResult EvaluateF018MUnit(FunctionNode f,IFormulaEvaluationContext c,List<FormulaDependency>d)
    {
        if(f.Arguments.Count!=1)return Failure("#VALUE!",FormulaErrorCode.InvalidValue,d);var v=EvaluateScalarNode(f.Arguments[0],c,d);if(!FormulaValueCoercion.TryNumber(v,out var n)||n<1||n>1000)return Failure("#VALUE!",FormulaErrorCode.InvalidValue,d);int z=(int)Math.Truncate(n);if((long)z*z>FormulaArrayValue.MaximumCellCount)return Failure("#NUM!",FormulaErrorCode.InvalidValue,d);return FormulaArrayEvaluationResult.Success(FormulaArrayValue.Create(z,z,(r,col)=>CellValue.FromNumber(r==col?1:0)),DistinctDependencies(d));
    }
    private FormulaArrayEvaluationResult EvaluateF018Frequency(FunctionNode f,IFormulaEvaluationContext c,List<FormulaDependency>d)
    {
        if(f.Arguments.Count!=2)return Failure("#VALUE!",FormulaErrorCode.InvalidValue,d);var data=EvaluateNodeAsArray(f.Arguments[0],c,d);var bins=EvaluateNodeAsArray(f.Arguments[1],c,d);if(!data.IsSuccess)return data;if(!bins.IsSuccess)return bins;var ds=data.Value!.ToArray().Where(x=>FormulaValueCoercion.TryNumber(x,out _)).Select(x=>{FormulaValueCoercion.TryNumber(x,out var q);return q;}).ToArray();var bs=bins.Value!.ToArray().Where(x=>FormulaValueCoercion.TryNumber(x,out _)).Select(x=>{FormulaValueCoercion.TryNumber(x,out var q);return q;}).OrderBy(x=>x).ToArray();var outv=new CellValue[bs.Length+1];double prev=double.NegativeInfinity;for(int k=0;k<bs.Length;k++){double b=bs[k];outv[k]=CellValue.FromNumber(ds.Count(x=>x>prev&&x<=b));prev=b;}outv[^1]=CellValue.FromNumber(ds.Count(x=>x>prev));return FormulaArrayEvaluationResult.Success(new FormulaArrayValue(outv.Length,1,outv),DistinctDependencies(d));
    }
}
