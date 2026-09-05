using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

internal static partial class DescriptiveCompatibilityStatisticalFormulaFunctions
{
    private const int MaximumValues = 2_000_000;
    private const int MaximumSignificance = 15;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition("AVEDEV", 1, int.MaxValue, EvaluateAveDev);
        yield return CreateDefinition("AVERAGEA", 1, int.MaxValue, EvaluateAverageA);
        yield return CreateDefinition("DEVSQ", 1, int.MaxValue, EvaluateDevSq);
        yield return CreateDefinition("GEOMEAN", 1, int.MaxValue, EvaluateGeoMean);
        yield return CreateDefinition("HARMEAN", 1, int.MaxValue, EvaluateHarMean);
        yield return CreateDefinition("KURT", 1, int.MaxValue, EvaluateKurt);
        yield return CreateDefinition("MAXA", 1, int.MaxValue, static invocation =>
            EvaluateMinMaxA(invocation, maximum: true));
        yield return CreateDefinition("MINA", 1, int.MaxValue, static invocation =>
            EvaluateMinMaxA(invocation, maximum: false));
        yield return CreateDefinition("SKEW", 1, int.MaxValue, static invocation =>
            EvaluateSkew(invocation, population: false));
        yield return CreateDefinition("SKEW.P", 1, int.MaxValue, static invocation =>
            EvaluateSkew(invocation, population: true));
        yield return CreateDefinition("STDEVA", 1, int.MaxValue, static invocation =>
            EvaluateVarianceA(invocation, population: false, squareRoot: true));
        yield return CreateDefinition("STDEVPA", 1, int.MaxValue, static invocation =>
            EvaluateVarianceA(invocation, population: true, squareRoot: true));
        yield return CreateDefinition("VARA", 1, int.MaxValue, static invocation =>
            EvaluateVarianceA(invocation, population: false, squareRoot: false));
        yield return CreateDefinition("VARPA", 1, int.MaxValue, static invocation =>
            EvaluateVarianceA(invocation, population: true, squareRoot: false));
        yield return CreateDefinition("TRIMMEAN", 2, 2, EvaluateTrimMean);
        yield return CreateDefinition("PERCENTILE.EXC", 2, 2, EvaluatePercentileExclusive);
        yield return CreateDefinition("QUARTILE.EXC", 2, 2, EvaluateQuartileExclusive);
        yield return CreateDefinition("RANK.AVG", 2, 3, EvaluateRankAverage);
        yield return CreateDefinition("PERCENTRANK.INC", 2, 3, static invocation =>
            EvaluatePercentRank(invocation, exclusive: false));
        yield return CreateDefinition("PERCENTRANK.EXC", 2, 3, static invocation =>
            EvaluatePercentRank(invocation, exclusive: true));
    }

    private static FormulaFunctionDefinition CreateDefinition(
        string name,
        int minimumArguments,
        int maximumArguments,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments,
                maximumArguments,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaEvaluationResult EvaluateAveDev(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                invocation.Arguments,
                CollectionMode.Standard,
                out var values,
                out var error))
        {
            return error;
        }
        if (values.Length == 0)
        {
            return DivisionByZero();
        }
        var mean = Mean(values);
        var deviations = 0d;
        var compensation = 0d;
        foreach (var value in values)
        {
            AddCompensated(
                Math.Abs(value - mean),
                ref deviations,
                ref compensation);
        }
        return Number(deviations / values.Length);
    }

    private static FormulaEvaluationResult EvaluateAverageA(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                invocation.Arguments,
                CollectionMode.ACompatible,
                out var values,
                out var error))
        {
            return error;
        }
        return values.Length == 0
            ? DivisionByZero()
            : Number(Mean(values));
    }

    private static FormulaEvaluationResult EvaluateDevSq(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                invocation.Arguments,
                CollectionMode.Standard,
                out var values,
                out var error))
        {
            return error;
        }
        return values.Length == 0
            ? DivisionByZero()
            : Number(SumSquaredDeviations(values));
    }

    private static FormulaEvaluationResult EvaluateGeoMean(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                invocation.Arguments,
                CollectionMode.Standard,
                out var values,
                out var error))
        {
            return error;
        }
        if (values.Length == 0 || ContainsNonPositive(values))
        {
            return NumericError();
        }
        var logarithms = 0d;
        var compensation = 0d;
        foreach (var value in values)
        {
            AddCompensated(
                Math.Log(value),
                ref logarithms,
                ref compensation);
        }
        return Number(Math.Exp(logarithms / values.Length));
    }

    private static FormulaEvaluationResult EvaluateHarMean(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                invocation.Arguments,
                CollectionMode.Standard,
                out var values,
                out var error))
        {
            return error;
        }
        if (values.Length == 0 || ContainsNonPositive(values))
        {
            return NumericError();
        }
        var reciprocals = 0d;
        var compensation = 0d;
        foreach (var value in values)
        {
            AddCompensated(
                1d / value,
                ref reciprocals,
                ref compensation);
        }
        return reciprocals <= 0d || !double.IsFinite(reciprocals)
            ? NumericError()
            : Number(values.Length / reciprocals);
    }

    private static FormulaEvaluationResult EvaluateKurt(
        FormulaFunctionInvocation invocation)
    {
        if (!TryCollectNumbers(
                invocation.Arguments,
                CollectionMode.Standard,
                out var values,
                out var error))
        {
            return error;
        }
        var count = values.Length;
        if (count < 4)
        {
            return DivisionByZero();
        }
        var mean = Mean(values);
        var sum2 = 0d;
        var sum4 = 0d;
        var compensation2 = 0d;
        var compensation4 = 0d;
        foreach (var value in values)
        {
            var deviation = value - mean;
            var squared = deviation * deviation;
            AddCompensated(squared, ref sum2, ref compensation2);
            AddCompensated(squared * squared, ref sum4, ref compensation4);
        }
        if (sum2 <= 0d)
        {
            return DivisionByZero();
        }
        var n = (double)count;
        var first = n * (n + 1d) * (n - 1d) * sum4 /
                    ((n - 2d) * (n - 3d) * sum2 * sum2);
        var second = 3d * (n - 1d) * (n - 1d) /
                     ((n - 2d) * (n - 3d));
        return Number(first - second);
    }

    private static FormulaEvaluationResult EvaluateMinMaxA(
        FormulaFunctionInvocation invocation,
        bool maximum)
    {
        if (!TryCollectNumbers(
                invocation.Arguments,
                CollectionMode.ACompatible,
                out var values,
                out var error))
        {
            return error;
        }
        if (values.Length == 0)
        {
            return Number(0d);
        }
        var result = values[0];
        for (var index = 1; index < values.Length; index++)
        {
            result = maximum
                ? Math.Max(result, values[index])
                : Math.Min(result, values[index]);
        }
        return Number(result);
    }

    private static FormulaEvaluationResult EvaluateSkew(
        FormulaFunctionInvocation invocation,
        bool population)
    {
        if (!TryCollectNumbers(
                invocation.Arguments,
                CollectionMode.Standard,
                out var values,
                out var error))
        {
            return error;
        }
        var count = values.Length;
        if (count < 3)
        {
            return DivisionByZero();
        }
        var mean = Mean(values);
        var sum2 = 0d;
        var sum3 = 0d;
        var compensation2 = 0d;
        var compensation3 = 0d;
        foreach (var value in values)
        {
            var deviation = value - mean;
            var squared = deviation * deviation;
            AddCompensated(squared, ref sum2, ref compensation2);
            AddCompensated(squared * deviation, ref sum3, ref compensation3);
        }
        if (sum2 <= 0d)
        {
            return DivisionByZero();
        }
        var n = (double)count;
        if (population)
        {
            var standardDeviation = Math.Sqrt(sum2 / n);
            return standardDeviation == 0d
                ? DivisionByZero()
                : Number((sum3 / n) /
                         (standardDeviation * standardDeviation * standardDeviation));
        }
        var sampleDeviation = Math.Sqrt(sum2 / (n - 1d));
        return sampleDeviation == 0d
            ? DivisionByZero()
            : Number(
                n * sum3 /
                ((n - 1d) * (n - 2d) *
                 sampleDeviation * sampleDeviation * sampleDeviation));
    }

    private static FormulaEvaluationResult EvaluateVarianceA(
        FormulaFunctionInvocation invocation,
        bool population,
        bool squareRoot)
    {
        if (!TryCollectNumbers(
                invocation.Arguments,
                CollectionMode.ACompatible,
                out var values,
                out var error))
        {
            return error;
        }
        var minimum = population ? 1 : 2;
        if (values.Length < minimum)
        {
            return DivisionByZero();
        }
        var denominator = population
            ? values.Length
            : values.Length - 1d;
        var variance = SumSquaredDeviations(values) / denominator;
        return Number(squareRoot ? Math.Sqrt(variance) : variance);
    }
}
