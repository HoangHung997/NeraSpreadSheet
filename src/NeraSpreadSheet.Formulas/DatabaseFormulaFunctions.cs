using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

/// <summary>
/// Database aggregate functions over a rectangular header/data range and a
/// rectangular criteria table. Criteria rows are OR-ed; populated criteria
/// cells within one row are AND-ed.
/// </summary>
internal static class DatabaseFormulaFunctions
{
    public const int MaximumDatabaseCells = 2_000_000;
    public const int MaximumCriteriaCells = 100_000;
    public const long MaximumCriteriaComparisons = 10_000_000L;

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return CreateDefinition("DSUM", DatabaseAggregateKind.Sum);
        yield return CreateDefinition("DCOUNT", DatabaseAggregateKind.Count);
        yield return CreateDefinition("DCOUNTA", DatabaseAggregateKind.CountA);
        yield return CreateDefinition("DAVERAGE", DatabaseAggregateKind.Average);
        yield return CreateDefinition("DMAX", DatabaseAggregateKind.Maximum);
        yield return CreateDefinition("DMIN", DatabaseAggregateKind.Minimum);
        yield return CreateDefinition("DPRODUCT", DatabaseAggregateKind.Product);
        yield return CreateDefinition("DGET", DatabaseAggregateKind.Get);
        yield return CreateDefinition("DSTDEV", DatabaseAggregateKind.StandardDeviationSample);
        yield return CreateDefinition("DSTDEVP", DatabaseAggregateKind.StandardDeviationPopulation);
        yield return CreateDefinition("DVAR", DatabaseAggregateKind.VarianceSample);
        yield return CreateDefinition("DVARP", DatabaseAggregateKind.VariancePopulation);
    }

    private static FormulaFunctionDefinition CreateDefinition(
        string name,
        DatabaseAggregateKind kind) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimumArguments: 3,
                maximumArguments: 3,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                propagateArgumentErrors: false,
                argumentCountPolicy:
                    FormulaFunctionArgumentCountPolicy.LogicalArguments),
            invocation => Evaluate(invocation, kind));

    private static FormulaEvaluationResult Evaluate(
        FormulaFunctionInvocation invocation,
        DatabaseAggregateKind kind)
    {
        if (!RangeMatrix.TryCreate(
                invocation.Arguments[0],
                MaximumDatabaseCells,
                out var database,
                out var error) ||
            !RangeMatrix.TryCreate(
                invocation.Arguments[2],
                MaximumCriteriaCells,
                out var criteria,
                out error))
        {
            return error;
        }
        if (database.RowCount < 1 ||
            database.ColumnCount < 1 ||
            criteria.RowCount < 2 ||
            criteria.ColumnCount < 1)
        {
            return InvalidValue();
        }

        if (!TryBuildDatabaseHeaders(
                database,
                out var headers,
                out error) ||
            !TryResolveField(
                invocation.Arguments[1],
                headers,
                database.ColumnCount,
                out var fieldIndex,
                out error) ||
            !TryCompileCriteria(
                criteria,
                headers,
                out var criteriaRows,
                out error))
        {
            return error;
        }

        var recordCount = Math.Max(0, database.RowCount - 1);
        var conditionCount = criteriaRows.Sum(static row => row.Length);
        var comparisonBudget = checked(
            (long)recordCount * Math.Max(1, conditionCount));
        if (comparisonBudget > MaximumCriteriaComparisons)
        {
            return NumericError();
        }

        var matchingRows = new List<int>();
        for (var rowIndex = 1; rowIndex < database.RowCount; rowIndex++)
        {
            if (MatchesAnyCriteriaRow(
                    database,
                    rowIndex,
                    criteriaRows))
            {
                matchingRows.Add(rowIndex);
            }
        }

        return kind switch
        {
            DatabaseAggregateKind.Count => Count(database, matchingRows, fieldIndex, nonBlank: false),
            DatabaseAggregateKind.CountA => Count(database, matchingRows, fieldIndex, nonBlank: true),
            DatabaseAggregateKind.Get => Get(database, matchingRows, fieldIndex),
            _ => AggregateNumeric(database, matchingRows, fieldIndex, kind),
        };
    }

    private static FormulaEvaluationResult Count(
        RangeMatrix database,
        List<int> matchingRows,
        int fieldIndex,
        bool nonBlank)
    {
        long count = 0L;
        foreach (var rowIndex in matchingRows)
        {
            var value = database[rowIndex, fieldIndex];
            if (nonBlank
                    ? !value.IsBlank
                    : value.Kind is CellValueKind.Number or CellValueKind.DateTime)
            {
                count++;
            }
        }
        return Number(count);
    }

    private static FormulaEvaluationResult Get(
        RangeMatrix database,
        List<int> matchingRows,
        int fieldIndex)
    {
        if (matchingRows.Count == 0)
        {
            return InvalidValue();
        }
        if (matchingRows.Count > 1)
        {
            return NumericError();
        }

        var value = database[matchingRows[0], fieldIndex];
        return value.Kind == CellValueKind.Error
            ? new FormulaEvaluationResult(
                value,
                FormulaErrorMapping.ToErrorCode(value),
                Array.Empty<FormulaDependency>())
            : FormulaEvaluationResult.Success(value);
    }

    private static FormulaEvaluationResult AggregateNumeric(
        RangeMatrix database,
        List<int> matchingRows,
        int fieldIndex,
        DatabaseAggregateKind kind)
    {
        var numbers = new List<double>(matchingRows.Count);
        foreach (var rowIndex in matchingRows)
        {
            var value = database[rowIndex, fieldIndex];
            if (value.Kind == CellValueKind.Error)
            {
                return new FormulaEvaluationResult(
                    value,
                    FormulaErrorMapping.ToErrorCode(value),
                    Array.Empty<FormulaDependency>());
            }
            if (value.Kind is not
                (CellValueKind.Number or CellValueKind.DateTime))
            {
                continue;
            }
            if (!FormulaValueCoercion.TryNumber(value, out var number) ||
                !double.IsFinite(number))
            {
                return NumericError();
            }
            numbers.Add(number);
        }

        return kind switch
        {
            DatabaseAggregateKind.Sum => Number(CompensatedSum(numbers)),
            DatabaseAggregateKind.Product => Product(numbers),
            DatabaseAggregateKind.Average => numbers.Count == 0
                ? DivisionByZero()
                : Number(CompensatedSum(numbers) / numbers.Count),
            DatabaseAggregateKind.Maximum => numbers.Count == 0
                ? Number(0d)
                : Number(numbers.Max()),
            DatabaseAggregateKind.Minimum => numbers.Count == 0
                ? Number(0d)
                : Number(numbers.Min()),
            DatabaseAggregateKind.VarianceSample => Variance(numbers, sample: true, squareRoot: false),
            DatabaseAggregateKind.VariancePopulation => Variance(numbers, sample: false, squareRoot: false),
            DatabaseAggregateKind.StandardDeviationSample => Variance(numbers, sample: true, squareRoot: true),
            DatabaseAggregateKind.StandardDeviationPopulation => Variance(numbers, sample: false, squareRoot: true),
            _ => InvalidValue(),
        };
    }

    private static FormulaEvaluationResult Product(List<double> numbers)
    {
        if (numbers.Count == 0)
        {
            return Number(0d);
        }
        var product = 1d;
        foreach (var number in numbers)
        {
            product *= number;
            if (!double.IsFinite(product))
            {
                return NumericError();
            }
        }
        return Number(product);
    }

    private static FormulaEvaluationResult Variance(
        List<double> numbers,
        bool sample,
        bool squareRoot)
    {
        var minimumCount = sample ? 2 : 1;
        if (numbers.Count < minimumCount)
        {
            return DivisionByZero();
        }

        var count = 0L;
        var mean = 0d;
        var sumOfSquares = 0d;
        foreach (var number in numbers)
        {
            count++;
            var delta = number - mean;
            mean += delta / count;
            sumOfSquares += delta * (number - mean);
            if (!double.IsFinite(mean) ||
                !double.IsFinite(sumOfSquares))
            {
                return NumericError();
            }
        }

        var denominator = sample ? count - 1d : count;
        var variance = sumOfSquares / denominator;
        if (variance < 0d && variance > -1e-12d)
        {
            variance = 0d;
        }
        if (variance < 0d || !double.IsFinite(variance))
        {
            return NumericError();
        }
        return Number(squareRoot ? Math.Sqrt(variance) : variance);
    }

    private static double CompensatedSum(IEnumerable<double> values)
    {
        var sum = 0d;
        var compensation = 0d;
        foreach (var value in values)
        {
            var adjusted = value - compensation;
            var next = sum + adjusted;
            compensation = (next - sum) - adjusted;
            sum = next;
        }
        return sum;
    }

    private static bool MatchesAnyCriteriaRow(
        RangeMatrix database,
        int recordRowIndex,
        IReadOnlyList<DatabaseCondition[]> criteriaRows)
    {
        foreach (var criteriaRow in criteriaRows)
        {
            var matches = true;
            foreach (var condition in criteriaRow)
            {
                if (!condition.Criterion.Matches(
                        database[recordRowIndex, condition.FieldIndex]))
                {
                    matches = false;
                    break;
                }
            }
            if (matches)
            {
                return true;
            }
        }
        return false;
    }

    private static bool TryBuildDatabaseHeaders(
        RangeMatrix database,
        out Dictionary<string, int> headers,
        out FormulaEvaluationResult error)
    {
        headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var column = 0; column < database.ColumnCount; column++)
        {
            var headerValue = database[0, column];
            if (headerValue.Kind == CellValueKind.Error)
            {
                error = new FormulaEvaluationResult(
                    headerValue,
                    FormulaErrorMapping.ToErrorCode(headerValue),
                    Array.Empty<FormulaDependency>());
                return false;
            }
            var header = FormulaValueCoercion.ToText(headerValue).Trim();
            if (header.Length == 0 || !headers.TryAdd(header, column))
            {
                error = InvalidValue();
                return false;
            }
        }
        error = default!;
        return true;
    }

    private static bool TryResolveField(
        FormulaFunctionArgument argument,
        Dictionary<string, int> headers,
        int columnCount,
        out int fieldIndex,
        out FormulaEvaluationResult error)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar)
        {
            fieldIndex = default;
            error = InvalidValue();
            return false;
        }
        var field = argument.ScalarValue;
        if (field.Kind == CellValueKind.Error)
        {
            fieldIndex = default;
            error = new FormulaEvaluationResult(
                field,
                FormulaErrorMapping.ToErrorCode(field),
                Array.Empty<FormulaDependency>());
            return false;
        }

        if (field.Kind == CellValueKind.Text)
        {
            var header = ((string)field.RawValue!).Trim();
            if (headers.TryGetValue(header, out fieldIndex))
            {
                error = default!;
                return true;
            }
            if (!int.TryParse(
                    header,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var textualIndex))
            {
                fieldIndex = default;
                error = InvalidValue();
                return false;
            }
            fieldIndex = textualIndex - 1;
        }
        else if (FormulaValueCoercion.TryInteger(
                     field,
                     out var numericIndex))
        {
            fieldIndex = numericIndex - 1;
        }
        else
        {
            fieldIndex = default;
            error = InvalidValue();
            return false;
        }

        if (fieldIndex < 0 || fieldIndex >= columnCount)
        {
            fieldIndex = default;
            error = InvalidValue();
            return false;
        }
        error = default!;
        return true;
    }

    private static bool TryCompileCriteria(
        RangeMatrix criteria,
        Dictionary<string, int> databaseHeaders,
        out DatabaseCondition[][] criteriaRows,
        out FormulaEvaluationResult error)
    {
        var fieldIndexes = new int[criteria.ColumnCount];
        for (var column = 0; column < criteria.ColumnCount; column++)
        {
            var headerValue = criteria[0, column];
            if (headerValue.Kind == CellValueKind.Error)
            {
                criteriaRows = [];
                error = new FormulaEvaluationResult(
                    headerValue,
                    FormulaErrorMapping.ToErrorCode(headerValue),
                    Array.Empty<FormulaDependency>());
                return false;
            }
            var header = FormulaValueCoercion.ToText(headerValue).Trim();
            if (header.Length == 0 ||
                !databaseHeaders.TryGetValue(header, out fieldIndexes[column]))
            {
                criteriaRows = [];
                error = InvalidValue();
                return false;
            }
        }

        var rows = new List<DatabaseCondition[]>(criteria.RowCount - 1);
        for (var row = 1; row < criteria.RowCount; row++)
        {
            var conditions = new List<DatabaseCondition>();
            for (var column = 0; column < criteria.ColumnCount; column++)
            {
                var criterionValue = criteria[row, column];
                if (criterionValue.IsBlank)
                {
                    continue;
                }
                if (criterionValue.Kind == CellValueKind.Error)
                {
                    criteriaRows = [];
                    error = new FormulaEvaluationResult(
                        criterionValue,
                        FormulaErrorMapping.ToErrorCode(criterionValue),
                        Array.Empty<FormulaDependency>());
                    return false;
                }
                conditions.Add(new DatabaseCondition(
                    fieldIndexes[column],
                    FormulaCriterion.Parse(criterionValue)));
            }
            rows.Add(conditions.ToArray());
        }
        criteriaRows = rows.ToArray();
        error = default!;
        return true;
    }

    private static FormulaEvaluationResult Number(double value) =>
        double.IsFinite(value)
            ? FormulaEvaluationResult.Success(CellValue.FromNumber(value))
            : NumericError();

    private static FormulaEvaluationResult Number(long value) =>
        FormulaEvaluationResult.Success(CellValue.FromNumber(value));

    private static FormulaEvaluationResult InvalidValue() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.InvalidValue);

    private static FormulaEvaluationResult DivisionByZero() =>
        FormulaEvaluationResult.Failure(FormulaErrorCode.DivisionByZero);

    private static FormulaEvaluationResult NumericError() =>
        new(
            CellValue.FromError("#NUM!"),
            FormulaErrorCode.InvalidValue,
            Array.Empty<FormulaDependency>());

    private enum DatabaseAggregateKind
    {
        Sum,
        Count,
        CountA,
        Average,
        Maximum,
        Minimum,
        Product,
        Get,
        StandardDeviationSample,
        StandardDeviationPopulation,
        VarianceSample,
        VariancePopulation,
    }

    private readonly record struct DatabaseCondition(
        int FieldIndex,
        FormulaCriterion Criterion);

    private sealed class RangeMatrix
    {
        private readonly CellValue[] _values;

        private RangeMatrix(int rowCount, int columnCount, CellValue[] values)
        {
            RowCount = rowCount;
            ColumnCount = columnCount;
            _values = values;
        }

        public int RowCount { get; }

        public int ColumnCount { get; }

        public CellValue this[int row, int column] =>
            _values[checked((row * ColumnCount) + column)];

        public static bool TryCreate(
            FormulaFunctionArgument argument,
            int maximumCells,
            out RangeMatrix matrix,
            out FormulaEvaluationResult error)
        {
            if (argument.Kind != FormulaFunctionArgumentKind.Range ||
                argument.SourceDependency is not { } dependency)
            {
                matrix = null!;
                error = InvalidValue();
                return false;
            }
            var rows = dependency.Range.RowCount;
            var columns = dependency.Range.ColumnCount;
            var cellCount = checked((long)rows * columns);
            if (cellCount <= 0L ||
                cellCount > maximumCells ||
                argument.Values.Count != cellCount)
            {
                matrix = null!;
                error = cellCount > maximumCells
                    ? NumericError()
                    : InvalidValue();
                return false;
            }
            matrix = new RangeMatrix(
                rows,
                columns,
                argument.Values.ToArray());
            error = default!;
            return true;
        }
    }
}
