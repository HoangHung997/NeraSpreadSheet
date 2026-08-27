using NeraSpreadSheet.Core;
namespace NeraSpreadSheet.Formulas;
internal static class F018EngineeringLookupMathFormulaFunctions
{
    private const FormulaFunctionCapabilities ScalarRange=FormulaFunctionCapabilities.ScalarArguments|FormulaFunctionCapabilities.RangeArguments;
    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return Def("CONVERT",3,3,EvalConvert);
        yield return Def("ERF",1,2,EvalErf);yield return Def("ERF.PRECISE",1,1,EvalErf);
        yield return Def("ERFC",1,1,EvalErfc);yield return Def("ERFC.PRECISE",1,1,EvalErfc);
        yield return Def("BESSELI",2,2,i=>EvalBessel(i,'I'));yield return Def("BESSELJ",2,2,i=>EvalBessel(i,'J'));yield return Def("BESSELK",2,2,i=>EvalBessel(i,'K'));yield return Def("BESSELY",2,2,i=>EvalBessel(i,'Y'));
        yield return Def("HLOOKUP",3,4,i=>EvalLookup(i,true));yield return Def("VLOOKUP",3,4,i=>EvalLookup(i,false));
        yield return Def("INDEX",2,3,EvalIndex);yield return Def("MATCH",2,3,EvalMatch);yield return Def("XLOOKUP",3,6,EvalXLookup);
        yield return Def("AGGREGATE",3,255,EvalAggregate);
        yield return Def("RAND",0,0,i=>Num(Random.Shared.NextDouble()),FormulaFunctionVolatility.Volatile);
        yield return Def("RANDBETWEEN",2,2,EvalRandBetween,FormulaFunctionVolatility.Volatile);
        yield return Def("MDETERM",1,1,EvalMDeterm);
    }
    private static FormulaFunctionDefinition Def(string name,int min,int max,Func<FormulaFunctionInvocation,FormulaEvaluationResult> eval,FormulaFunctionVolatility vol=FormulaFunctionVolatility.Deterministic)=>new(new FormulaFunctionDescriptor(new FormulaFunctionIdentity("NERA.BUILTIN",name),new FormulaFunctionVersion(1,0,0),FormulaFunctionApiVersion.Current,min,max,ScalarRange|FormulaFunctionCapabilities.ReturnsScalar,vol,argumentCountPolicy:FormulaFunctionArgumentCountPolicy.LogicalArguments),eval);
    private static FormulaEvaluationResult EvalConvert(FormulaFunctionInvocation i){if(!Scalar(i.Arguments[0],out var v)||!Txt(i.Arguments[1],out var f)||!Txt(i.Arguments[2],out var t))return Val();if(!Unit(f,out var fd,out var ff,out var fo)||!Unit(t,out var td,out var tf,out var to)||fd!=td)return NA();return Num(((v+fo)*ff)/tf-to);}
    private static bool Unit(string s,out string d,out double f,out double o){o=0;switch(s){case"m":d="L";f=1;return true;case"km":d="L";f=1000;return true;case"cm":d="L";f=.01;return true;case"mm":d="L";f=.001;return true;case"in":d="L";f=.0254;return true;case"ft":d="L";f=.3048;return true;case"yd":d="L";f=.9144;return true;case"mi":d="L";f=1609.344;return true;case"g":d="M";f=1;return true;case"kg":d="M";f=1000;return true;case"lbm":d="M";f=453.59237;return true;case"sec":d="T";f=1;return true;case"min":d="T";f=60;return true;case"hr":d="T";f=3600;return true;case"day":d="T";f=86400;return true;case"C":d="TEMP";f=1;o=273.15;return true;case"K":d="TEMP";f=1;return true;case"F":d="TEMP";f=5d/9d;o=459.67;return true;default:d="";f=0;return false;}}
    private static FormulaEvaluationResult EvalErf(FormulaFunctionInvocation i){if(!Scalar(i.Arguments[0],out var a))return Val();var r=Erf(a);if(i.Arguments.Count==2){if(!Scalar(i.Arguments[1],out var b))return Val();r=Erf(b)-r;}return Num(r);}
    private static FormulaEvaluationResult EvalErfc(FormulaFunctionInvocation i)=>Scalar(i.Arguments[0],out var x)?Num(1-Erf(x)):Val();
    private static double Erf(double x){var s=Math.Sign(x);x=Math.Abs(x);var t=1/(1+.3275911*x);var y=1-(((((1.061405429*t-1.453152027)*t+1.421413741)*t-.284496736)*t+.254829592)*t)*Math.Exp(-x*x);return s*y;}
    private static FormulaEvaluationResult EvalBessel(FormulaFunctionInvocation i,char k){if(!Scalar(i.Arguments[0],out var x)||!Int(i.Arguments[1],out var n)||n<0)return NumErr();double r;if(k=='J'||k=='I')r=BesselSeries(x,n,k=='I');else{if(x<=0)return NumErr();r=k=='Y'?BesselY(x,n):BesselK(x,n);}return Num(r);}
    private static double BesselSeries(double x,int n,bool modified){double term=Math.Pow(x/2,n)/Fact(n),sum=term;for(int m=1;m<256;m++){term*=modified?x*x/(4d*m*(m+n)):-x*x/(4d*m*(m+n));sum+=term;if(Math.Abs(term)<Math.Max(1e-300,Math.Abs(sum))*1e-15)break;}return sum;}
    private static double Fact(int n){double r=1;for(int j=2;j<=n;j++)r*=j;return r;}
    private static double BesselY(double x,int n){double y0=Math.Sqrt(2/(Math.PI*x))*Math.Sin(x-Math.PI/4);if(n==0)return y0;double y1=Math.Sqrt(2/(Math.PI*x))*Math.Sin(x-3*Math.PI/4);for(int j=1;j<n;j++){var z=2d*j/x*y1-y0;y0=y1;y1=z;}return y1;}
    private static double BesselK(double x,int n){double k0=Math.Sqrt(Math.PI/(2*x))*Math.Exp(-x);if(n==0)return k0;double k1=k0*(1+1/(2*x));for(int j=1;j<n;j++){var z=k0+2d*j/x*k1;k0=k1;k1=z;}return k1;}
    private static FormulaEvaluationResult EvalLookup(FormulaFunctionInvocation i,bool horizontal){if(!ScalarVal(i.Arguments[0],out var key)||!Shape(i.Arguments[1],out var rows,out var cols,out var vals)||!Int(i.Arguments[2],out var idx))return Val();var limit=horizontal?rows:cols;if(idx<1||idx>limit)return Ref();bool approx=true;if(i.Arguments.Count==4&&!Bool(i.Arguments[3],out approx))return Val();int count=horizontal?cols:rows,found=-1;for(int p=0;p<count;p++){var v=vals[horizontal?p:p*cols];var c=Cmp(v,key);if(c==0){found=p;break;}if(approx&&c<=0)found=p;}if(found<0)return NA();return FormulaEvaluationResult.Success(vals[horizontal?(idx-1)*cols+found:found*cols+(idx-1)]);}
    private static FormulaEvaluationResult EvalIndex(FormulaFunctionInvocation i){if(!Shape(i.Arguments[0],out var r,out var c,out var v)||!Int(i.Arguments[1],out var row))return Val();int col=1;if(i.Arguments.Count==3&&!Int(i.Arguments[2],out col))return Val();if(row<1||row>r||col<1||col>c)return Ref();return FormulaEvaluationResult.Success(v[(row-1)*c+col-1]);}
    private static FormulaEvaluationResult EvalMatch(FormulaFunctionInvocation i){if(!ScalarVal(i.Arguments[0],out var key)||!Shape(i.Arguments[1],out var r,out var c,out var vals)||(r>1&&c>1))return NA();int mode=1;if(i.Arguments.Count==3&&!Int(i.Arguments[2],out mode))return Val();int found=-1;for(int p=0;p<vals.Length;p++){int q=Cmp(vals[p],key);if(q==0){found=p;break;}if(mode==1&&q<=0)found=p;if(mode==-1&&q>=0)found=p;}return found<0?NA():Num(found+1);}
    private static FormulaEvaluationResult EvalXLookup(FormulaFunctionInvocation i){if(!ScalarVal(i.Arguments[0],out var key)||!Shape(i.Arguments[1],out _,out _,out var la)||!Shape(i.Arguments[2],out _,out _,out var ra)||la.Length!=ra.Length)return Val();for(int p=0;p<la.Length;p++)if(Cmp(la[p],key)==0)return FormulaEvaluationResult.Success(ra[p]);if(i.Arguments.Count>=4&&i.Arguments[3].Kind==FormulaFunctionArgumentKind.Scalar)return FormulaEvaluationResult.Success(i.Arguments[3].ScalarValue);return NA();}
    private static FormulaEvaluationResult EvalAggregate(FormulaFunctionInvocation i){if(!Int(i.Arguments[0],out var fn)||!Int(i.Arguments[1],out _))return Val();var nums=new List<double>();for(int a=2;a<i.Arguments.Count;a++)foreach(var cv in i.Arguments[a].Values)if(FormulaValueCoercion.TryNumber(cv,out var n)&&double.IsFinite(n))nums.Add(n);if(nums.Count==0)return fn==9?Num(0):Div0();nums.Sort();double mean=nums.Average(),sse=nums.Sum(x=>(x-mean)*(x-mean));return fn switch{1=>Num(mean),2=>Num(nums.Count),4=>Num(nums[^1]),5=>Num(nums[0]),6=>Num(nums.Aggregate(1d,(a,b)=>a*b)),7=>nums.Count>1?Num(Math.Sqrt(sse/(nums.Count-1))):Div0(),8=>Num(Math.Sqrt(sse/nums.Count)),9=>Num(nums.Sum()),10=>nums.Count>1?Num(sse/(nums.Count-1)):Div0(),11=>Num(sse/nums.Count),12=>Num(nums.Count%2==1?nums[nums.Count/2]:(nums[nums.Count/2-1]+nums[nums.Count/2])/2),_=>NumErr()};}
    private static FormulaEvaluationResult EvalRandBetween(FormulaFunctionInvocation i){if(!Int(i.Arguments[0],out var lo)||!Int(i.Arguments[1],out var hi)||lo>hi)return NumErr();return Num(Random.Shared.NextInt64(lo,(long)hi+1));}
    private static FormulaEvaluationResult EvalMDeterm(FormulaFunctionInvocation i){if(!Shape(i.Arguments[0],out var r,out var c,out var vals)||r!=c||r<1||r>256)return Val();var a=new double[r,r];for(int y=0;y<r;y++)for(int x=0;x<c;x++){if(!FormulaValueCoercion.TryNumber(vals[y*c+x],out a[y,x]))return Val();}double det=1;for(int k=0;k<r;k++){int p=k;for(int y=k+1;y<r;y++)if(Math.Abs(a[y,k])>Math.Abs(a[p,k]))p=y;if(Math.Abs(a[p,k])<1e-15)return Num(0);if(p!=k){for(int x=k;x<r;x++)(a[p,x],a[k,x])=(a[k,x],a[p,x]);det=-det;}var q=a[k,k];det*=q;for(int y=k+1;y<r;y++){var f=a[y,k]/q;for(int x=k+1;x<r;x++)a[y,x]-=f*a[k,x];}}return Num(det);}
    private static bool Shape(FormulaFunctionArgument a,out int r,out int c,out CellValue[] vals){vals=a.Values.ToArray();if(a.Kind==FormulaFunctionArgumentKind.Array&&a.ArrayValue is not null){r=a.ArrayValue.RowCount;c=a.ArrayValue.ColumnCount;return true;}if(a.Kind==FormulaFunctionArgumentKind.Range&&a.SourceDependency is FormulaDependency d){r=d.Range.RowCount;c=d.Range.ColumnCount;return true;}r=1;c=vals.Length;return true;}
    private static bool Scalar(FormulaFunctionArgument a,out double v)
    {
        v=default;
        return a.Kind==FormulaFunctionArgumentKind.Scalar &&
               FormulaValueCoercion.TryNumber(a.ScalarValue,out v,allowText:true) &&
               double.IsFinite(v);
    }
    private static bool ScalarVal(FormulaFunctionArgument a,out CellValue v){if(a.Kind==FormulaFunctionArgumentKind.Scalar){v=a.ScalarValue;return true;}v=CellValue.Blank;return false;}
    private static bool Int(FormulaFunctionArgument a,out int v){v=0;if(!Scalar(a,out var d)||d<int.MinValue||d>int.MaxValue)return false;v=(int)Math.Truncate(d);return true;}
    private static bool Bool(FormulaFunctionArgument a,out bool v)
    {
        v=default;
        return a.Kind==FormulaFunctionArgumentKind.Scalar &&
               FormulaValueCoercion.TryBoolean(a.ScalarValue,out v);
    }
    private static bool Txt(FormulaFunctionArgument a,out string s){if(a.Kind==FormulaFunctionArgumentKind.Scalar){s=FormulaValueCoercion.ToText(a.ScalarValue);return true;}s="";return false;}
    private static int Cmp(CellValue a,CellValue b){if(FormulaValueCoercion.TryNumber(a,out var x,true)&&FormulaValueCoercion.TryNumber(b,out var y,true))return x.CompareTo(y);return string.Compare(FormulaValueCoercion.ToText(a),FormulaValueCoercion.ToText(b),StringComparison.OrdinalIgnoreCase);}
    private static FormulaEvaluationResult Num(double x)=>double.IsFinite(x)?FormulaEvaluationResult.Success(CellValue.FromNumber(x)):NumErr();
    private static FormulaEvaluationResult Val()=>FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);private static FormulaEvaluationResult Ref()=>FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidReference);private static FormulaEvaluationResult NA()=>FormulaEvaluationResult.Failure(FormulaErrorCode.NotAvailable);private static FormulaEvaluationResult Div0()=>FormulaEvaluationResult.Failure(FormulaErrorCode.DivisionByZero);private static FormulaEvaluationResult NumErr()=>new(CellValue.FromError("#NUM!"),FormulaErrorCode.InvalidValue,Array.Empty<FormulaDependency>());
}
