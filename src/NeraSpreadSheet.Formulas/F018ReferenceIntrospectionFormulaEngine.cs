using NeraSpreadSheet.Core;
namespace NeraSpreadSheet.Formulas;
public sealed partial class NeraFormulaEngine
{
    private CellValue EvaluateF018IsRef(FunctionNode f,IFormulaEvaluationContext c,List<FormulaDependency>d)
    {
        if(f.Arguments.Count!=1)return CellValue.FromError("#VALUE!");var a=f.Arguments[0];if(a is ReferenceUnionNode)return CellValue.FromBoolean(true);if(ReferenceIntrospectionFormulaEvaluation.IsReferenceCandidate(a)||AdvancedReferenceFormulaEvaluation.IsReferenceFunction(a is FunctionNode n?n.Name:string.Empty))return CellValue.FromBoolean(TryResolveReference(a,c,d,out _,out _,out _));return CellValue.FromBoolean(false);
    }
    private CellValue EvaluateF018IsFormula(FunctionNode f,IFormulaEvaluationContext c,List<FormulaDependency>d)
    {
        if(f.Arguments.Count!=1)return CellValue.FromError("#VALUE!");if(!TryResolveReference(f.Arguments[0],c,d,out var ws,out var range,out _))return CellValue.FromBoolean(false);if(c is not IFormulaReferenceIntrospectionContext x)return CellValue.FromBoolean(false);var a=range.TopLeft;d.Add(new FormulaDependency(ws,new CellRange(a,a)));return CellValue.FromBoolean(x.TryGetCellFormula(ws,a,out var formula)&&!string.IsNullOrWhiteSpace(formula));
    }
    private CellValue EvaluateF018Cell(FunctionNode f,IFormulaEvaluationContext c,List<FormulaDependency>d)
    {
        if(f.Arguments.Count is <1 or >2)return CellValue.FromError("#VALUE!");var info=EvaluateNode(f.Arguments[0],c,d);if(info.Kind==CellValueKind.Error)return info;var key=FormulaValueCoercion.ToText(info).Trim().ToLowerInvariant();string? ws;CellRange range;
        if(f.Arguments.Count==2){if(!TryResolveReference(f.Arguments[1],c,d,out ws,out range,out var err))return err;}
        else if(c is IFormulaReferenceIntrospectionContext x){ws=x.CurrentWorksheetName;range=new CellRange(x.CurrentCellAddress,x.CurrentCellAddress);}else return CellValue.FromError("#VALUE!");
        var a=range.TopLeft;d.Add(new FormulaDependency(ws,new CellRange(a,a)));var v=c.GetCellValue(ws,a);return key switch{"address"=>CellValue.FromText(ToAbsoluteA1(a)),"row"=>CellValue.FromNumber(a.RowIndex+1d),"col"=>CellValue.FromNumber(a.ColumnIndex+1d),"contents"=>v,"type"=>CellValue.FromText(v.Kind switch{CellValueKind.Blank=>"b",CellValueKind.Text=>"l",_=>"v"}),_=>CellValue.FromError("#VALUE!")};
    }
    private static string ToAbsoluteA1(CellAddress a){var col=a.ColumnIndex+1;Span<char>b=stackalloc char[16];var p=b.Length;while(col>0){col--;b[--p]=(char)('A'+col%26);col/=26;}return string.Concat("$",b[p..],"$",(a.RowIndex+1).ToString(System.Globalization.CultureInfo.InvariantCulture));}
}
