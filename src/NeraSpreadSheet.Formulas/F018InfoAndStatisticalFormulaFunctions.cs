using System.Globalization;
using System.Text;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static class F018InfoAndStatisticalFormulaFunctions
{
    private const int MaximumValues = 2_000_000;
    private const int MaximumTextLength = 32_767;
    private const FormulaFunctionCapabilities ScalarRange = FormulaFunctionCapabilities.ScalarArguments | FormulaFunctionCapabilities.RangeArguments;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return Definition("TEXT",2,2,EvaluateText);
        yield return Definition("VALUETOTEXT",1,2,EvaluateValueToText);
        yield return Definition("ENCODEURL",1,1,EvaluateEncodeUrl);
        yield return Definition("ERROR.TYPE",1,1,EvaluateErrorType, propagateErrors:false);
        yield return Definition("TYPE",1,1,EvaluateType, propagateErrors:false);
        yield return Definition("GAMMA",1,1,EvaluateGamma);
        yield return Definition("GAMMALN",1,1,EvaluateGammaLn);
        yield return Definition("GAMMALN.PRECISE",1,1,EvaluateGammaLn);
        yield return Definition("GAUSS",1,1,EvaluateGauss);
        yield return Definition("PHI",1,1,EvaluatePhi);
        yield return Definition("PERMUT",2,2,i=>EvaluatePermutation(i,false));
        yield return Definition("PERMUTATIONA",2,2,i=>EvaluatePermutation(i,true));
        yield return Definition("CHISQ.TEST",2,2,EvaluateChiSquareTest);
        yield return Definition("T.TEST",4,4,EvaluateTTest);
        yield return Definition("PERCENTRANK",2,3,EvaluatePercentRankLegacy);
        yield return Definition("CHITEST",2,2,EvaluateChiSquareTest);
        yield return Definition("TTEST",4,4,EvaluateTTest);
    }

    private static FormulaFunctionDefinition Definition(string name,int min,int max,Func<FormulaFunctionInvocation,FormulaEvaluationResult> eval,bool propagateErrors=true) =>
        new(new FormulaFunctionDescriptor(new FormulaFunctionIdentity("NERA.BUILTIN",name),new FormulaFunctionVersion(1,0,0),FormulaFunctionApiVersion.Current,min,max,ScalarRange|FormulaFunctionCapabilities.ReturnsScalar,propagateArgumentErrors:propagateErrors,argumentCountPolicy:FormulaFunctionArgumentCountPolicy.LogicalArguments),eval);

    private static FormulaEvaluationResult EvaluateText(FormulaFunctionInvocation i)
    {
        if(!TryScalarText(i.Arguments[1],out var format,out var error)) return error;
        var a=i.Arguments[0]; if(a.Kind!=FormulaFunctionArgumentKind.Scalar) return InvalidValue(); var v=a.ScalarValue;
        if(v.Kind==CellValueKind.Error) return FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);
        if(v.Kind==CellValueKind.Text) return Text((string)v.RawValue!);
        if(v.Kind==CellValueKind.Boolean) return Text((bool)v.RawValue!?"TRUE":"FALSE");
        if(!FormulaValueCoercion.TryNumber(v,out var n,allowText:true)||!double.IsFinite(n)) return InvalidValue();
        try
        {
            if(LooksLikeDateFormat(format))
            {
                var d=v.Kind==CellValueKind.DateTime?(DateTime)v.RawValue!:DateTime.FromOADate(n);
                var f=format.Replace("mmmm","MMMM",StringComparison.OrdinalIgnoreCase).Replace("mmm","MMM",StringComparison.OrdinalIgnoreCase);
                if(!format.Contains('h',StringComparison.OrdinalIgnoreCase)) f=ReplaceMonthTokens(f);
                return Text(d.ToString(f,CultureInfo.InvariantCulture));
            }
            var sections=format.Split(';'); var selected=n>0?sections[0]:n<0&&sections.Length>1?sections[1]:n==0&&sections.Length>2?sections[2]:sections[0];
            if(selected.Contains('%')) n*=100; var dot=selected.IndexOf('.'); var decimals=dot<0?0:selected[(dot+1)..].Count(c=>c is '0' or '#'); decimals=Math.Clamp(decimals,0,15);
            var grouping=selected.Contains(','); var s=Math.Round(n,decimals,MidpointRounding.AwayFromZero).ToString((grouping?"N":"F")+decimals,CultureInfo.InvariantCulture); if(selected.Contains('%'))s+="%"; return Text(s);
        }
        catch(Exception e) when(e is FormatException or ArgumentOutOfRangeException){return InvalidValue();}
    }
    private static bool LooksLikeDateFormat(string f)=>f.Contains('y',StringComparison.OrdinalIgnoreCase)||f.Contains('d',StringComparison.OrdinalIgnoreCase)||f.Contains("hh",StringComparison.OrdinalIgnoreCase)||f.Contains("ss",StringComparison.OrdinalIgnoreCase);
    private static string ReplaceMonthTokens(string f){var b=new StringBuilder(f.Length);for(int x=0;x<f.Length;){if(f[x] is 'm' or 'M'){int c=1;while(x+c<f.Length&&f[x+c] is 'm' or 'M')c++;if(c<=2)b.Append('M',c);else b.Append(f.AsSpan(x,c));x+=c;}else b.Append(f[x++]);}return b.ToString();}

    private static FormulaEvaluationResult EvaluateValueToText(FormulaFunctionInvocation i)
    {
        int format=0; if(i.Arguments.Count==2&&!TryScalarInteger(i.Arguments[1],out format,out var e))return e; if(format is not(0 or 1))return NumericError(); var a=i.Arguments[0];if(a.Kind!=FormulaFunctionArgumentKind.Scalar)return InvalidValue();var v=a.ScalarValue;
        var s=v.Kind switch{CellValueKind.Text when format==1=>string.Concat("\"",((string)v.RawValue!).Replace("\"","\"\"",StringComparison.Ordinal),"\""),CellValueKind.Text=>(string)v.RawValue!,CellValueKind.Boolean=>(bool)v.RawValue!?"TRUE":"FALSE",CellValueKind.Blank=>format==1?"\"\"":string.Empty,CellValueKind.Error=>v.RawValue?.ToString()??"#VALUE!",_=>FormulaValueCoercion.ToText(v)};return Text(s);
    }
    private static FormulaEvaluationResult EvaluateEncodeUrl(FormulaFunctionInvocation i){if(!TryScalarText(i.Arguments[0],out var s,out var e))return e;try{return Text(Uri.EscapeDataString(s));}catch(UriFormatException){return InvalidValue();}}
    private static FormulaEvaluationResult EvaluateErrorType(FormulaFunctionInvocation i){var a=i.Arguments[0];if(a.Kind!=FormulaFunctionArgumentKind.Scalar||a.ScalarValue.Kind!=CellValueKind.Error)return NotAvailable();return Number(a.ScalarValue.RawValue?.ToString() switch{"#NULL!"=>1,"#DIV/0!"=>2,"#VALUE!"=>3,"#REF!"=>4,"#NAME?"=>5,"#NUM!"=>6,"#N/A"=>7,"#SPILL!"=>9,"#CIRC!"=>14,_=>3});}
    private static FormulaEvaluationResult EvaluateType(FormulaFunctionInvocation i){var a=i.Arguments[0];if(a.Kind is FormulaFunctionArgumentKind.Range or FormulaFunctionArgumentKind.Array)return Number(64);var v=a.ScalarValue;return Number(v.Kind switch{CellValueKind.Text=>2,CellValueKind.Boolean=>4,CellValueKind.Error=>16,_=>1});}
    private static FormulaEvaluationResult EvaluateGamma(FormulaFunctionInvocation i){if(!TryScalarNumber(i.Arguments[0],out var v,out var e))return e;if(v<=0&&Math.Abs(v-Math.Round(v))<1e-14)return NumericError();double g;if(v>0)g=Math.Exp(StatisticalNumerics.LogGamma(v));else g=Math.PI/(Math.Sin(Math.PI*v)*Math.Exp(StatisticalNumerics.LogGamma(1-v)));return Number(g);}
    private static FormulaEvaluationResult EvaluateGammaLn(FormulaFunctionInvocation i){if(!TryScalarNumber(i.Arguments[0],out var v,out var e))return e;return v<=0?NumericError():Number(StatisticalNumerics.LogGamma(v));}
    private static FormulaEvaluationResult EvaluateGauss(FormulaFunctionInvocation i)=>TryScalarNumber(i.Arguments[0],out var v,out var e)?Number(StatisticalNumerics.NormalCumulative(v)-.5):e;
    private static FormulaEvaluationResult EvaluatePhi(FormulaFunctionInvocation i)=>TryScalarNumber(i.Arguments[0],out var v,out var e)?Number(StatisticalNumerics.NormalDensity(v)):e;
    private static FormulaEvaluationResult EvaluatePermutation(FormulaFunctionInvocation i,bool rep){if(!TryScalarInteger(i.Arguments[0],out var n,out var e)||!TryScalarInteger(i.Arguments[1],out var k,out e))return e;if(n<0||k<0||(!rep&&k>n))return NumericError();if(k==0)return Number(1);if(rep)return Number(Math.Pow(n,k));double r=1;for(int x=0;x<k;x++){r*=n-x;if(!double.IsFinite(r))return NumericError();}return Number(r);}

    private static FormulaEvaluationResult EvaluateChiSquareTest(FormulaFunctionInvocation i)
    {
        if(!TryCollectNumbers(i.Arguments[0],out var obs,out var e)||!TryCollectNumbers(i.Arguments[1],out var exp,out e))return e;if(obs.Length!=exp.Length||obs.Length<2)return NotAvailable();double q=0;for(int x=0;x<obs.Length;x++){if(exp[x]<=0)return DivisionByZero();var d=obs[x]-exp[x];q+=d*d/exp[x];}return StatisticalNumerics.TryRegularizedGammaQ((obs.Length-1d)/2d,q/2d,out var p)?Number(p):NumericError();
    }
    private static FormulaEvaluationResult EvaluateTTest(FormulaFunctionInvocation i)
    {
        if(!TryCollectNumbers(i.Arguments[0],out var a,out var e)||!TryCollectNumbers(i.Arguments[1],out var b,out e)||!TryScalarInteger(i.Arguments[2],out var tails,out e)||!TryScalarInteger(i.Arguments[3],out var type,out e))return e;if(tails is not(1 or 2)||type<1||type>3)return NumericError();if(a.Length<2||b.Length<2)return DivisionByZero();double t,df;
        if(type==1){if(a.Length!=b.Length)return NotAvailable();var d=new double[a.Length];for(int x=0;x<a.Length;x++)d[x]=a[x]-b[x];var v=SampleVariance(d);if(v<=0)return DivisionByZero();t=Mean(d)/Math.Sqrt(v/d.Length);df=d.Length-1;}
        else{var md=Mean(a)-Mean(b);var va=SampleVariance(a);var vb=SampleVariance(b);if(type==2){df=a.Length+b.Length-2d;var pooled=(((a.Length-1d)*va)+((b.Length-1d)*vb))/df;var den=Math.Sqrt(pooled*((1d/a.Length)+(1d/b.Length)));if(den<=0)return DivisionByZero();t=md/den;}else{var x=va/a.Length;var y=vb/b.Length;var den=Math.Sqrt(x+y);if(den<=0)return DivisionByZero();t=md/den;var dd=(x*x/(a.Length-1d))+(y*y/(b.Length-1d));if(dd<=0)return DivisionByZero();df=(x+y)*(x+y)/dd;}}
        if(!AdvancedDistributionNumerics.TryStudentTCumulative(Math.Abs(t),df,out var c))return NumericError();var upper=1-c;return Number(Math.Clamp(tails==2?2*upper:upper,0,1));
    }
    private static FormulaEvaluationResult EvaluatePercentRankLegacy(FormulaFunctionInvocation i)
    {
        if(!TryCollectNumbers(i.Arguments[0],out var v,out var e)||!TryScalarNumber(i.Arguments[1],out var target,out e))return e;int sig=3;if(i.Arguments.Count==3&&!TryScalarInteger(i.Arguments[2],out sig,out e))return e;if(v.Length<2||sig<1||sig>15)return NumericError();Array.Sort(v);if(target<v[0]||target>v[^1])return NotAvailable();var found=Array.BinarySearch(v,target);double pos;if(found>=0){int f=found,l=found;while(f>0&&v[f-1]==target)f--;while(l+1<v.Length&&v[l+1]==target)l++;pos=(f+l)/2d;}else{var up=~found;var lo=up-1;pos=lo+(target-v[lo])/(v[up]-v[lo]);}return Number(Math.Round(pos/(v.Length-1d),sig,MidpointRounding.AwayFromZero));
    }

    private static bool TryCollectNumbers(FormulaFunctionArgument a,out double[] values,out FormulaEvaluationResult error){var list=new List<double>();var direct=a.Kind==FormulaFunctionArgumentKind.Scalar;foreach(var v in a.Values){if(v.Kind is CellValueKind.Number or CellValueKind.DateTime){if(!FormulaValueCoercion.TryNumber(v,out var n)||!double.IsFinite(n)){values=[];error=NumericError();return false;}list.Add(n);}else if(direct&&v.Kind==CellValueKind.Boolean)list.Add((bool)v.RawValue!?1:0);else if(direct&&v.Kind==CellValueKind.Text){if(!FormulaValueCoercion.TryNumber(v,out var n,true)){values=[];error=InvalidValue();return false;}list.Add(n);}if(list.Count>MaximumValues){values=[];error=NumericError();return false;}}values=list.ToArray();error=default!;return true;}
    private static double Mean(double[] v){double s=0,c=0;foreach(var x in v){var y=x-c;var t=s+y;c=(t-s)-y;s=t;}return s/v.Length;}
    private static double SampleVariance(double[] v){var m=Mean(v);double s=0;foreach(var x in v){var d=x-m;s+=d*d;}return s/(v.Length-1d);}
    private static bool TryScalarText(FormulaFunctionArgument a,out string s,out FormulaEvaluationResult e){if(a.Kind!=FormulaFunctionArgumentKind.Scalar){s="";e=InvalidValue();return false;}s=FormulaValueCoercion.ToText(a.ScalarValue);if(s.Length>MaximumTextLength){e=InvalidValue();return false;}e=default!;return true;}
    private static bool TryScalarNumber(FormulaFunctionArgument a,out double v,out FormulaEvaluationResult e){if(a.Kind!=FormulaFunctionArgumentKind.Scalar||!FormulaValueCoercion.TryNumber(a.ScalarValue,out v,true)||!double.IsFinite(v)){v=0;e=InvalidValue();return false;}e=default!;return true;}
    private static bool TryScalarInteger(FormulaFunctionArgument a,out int v,out FormulaEvaluationResult e){if(!TryScalarNumber(a,out var n,out e)||n<int.MinValue||n>int.MaxValue){v=0;e=NumericError();return false;}v=(int)Math.Truncate(n);return true;}
    private static FormulaEvaluationResult Text(string v)=>v.Length<=MaximumTextLength?FormulaEvaluationResult.Success(CellValue.FromText(v)):InvalidValue();
    private static FormulaEvaluationResult Number(double v)=>double.IsFinite(v)?FormulaEvaluationResult.Success(CellValue.FromNumber(v)):NumericError();
    private static FormulaEvaluationResult InvalidValue()=>FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);
    private static FormulaEvaluationResult NotAvailable()=>FormulaEvaluationResult.Failure(FormulaErrorCode.NotAvailable);
    private static FormulaEvaluationResult DivisionByZero()=>FormulaEvaluationResult.Failure(FormulaErrorCode.DivisionByZero);
    private static FormulaEvaluationResult NumericError()=>new(CellValue.FromError("#NUM!"),FormulaErrorCode.InvalidValue,Array.Empty<FormulaDependency>());
}
