using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public sealed partial class NeraFormulaEngine : IFormulaEngine
{
    private readonly IFormulaFunctionRegistry _functions;

    public NeraFormulaEngine(
        IFormulaFunctionRegistry? functions = null)
    {
        _functions = functions ??
            new BuiltInFormulaFunctionRegistry();
    }

    public FormulaEvaluationResult Evaluate(
        string formula,
        IFormulaEvaluationContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(formula);
        ArgumentNullException.ThrowIfNull(context);
        try
        {
            var effectiveFormula = context is
                IStructuredReferenceEvaluationContext structuredContext
                ? structuredContext.ExpandStructuredReferences(formula)
                : formula;
            var node = new FormulaParser(effectiveFormula).Parse();
            var dependencies = new List<FormulaDependency>();
            var value = EvaluateNode(node, context, dependencies);
            var error = FormulaErrorMapping.ToErrorCode(value);
            return error == FormulaErrorCode.None
                ? FormulaEvaluationResult.Success(value, dependencies)
                : new FormulaEvaluationResult(
                    value,
                    error,
                    dependencies);
        }
        catch (FormatException)
        {
            return FormulaEvaluationResult.Failure(
                FormulaErrorCode.InvalidValue);
        }
        catch (OverflowException)
        {
            return FormulaEvaluationResult.Failure(
                FormulaErrorCode.InvalidValue);
        }
    }

    private CellValue EvaluateNode(
        FormulaNode node,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies) =>
        node switch
        {
            ConstantNode constant => constant.Value,
            MissingArgumentNode => CellValue.Blank,
            NameNode => CellValue.FromError("#NAME?"),
            CellNode cell => EvaluateCell(cell, context, dependencies),
            RangeNode => CellValue.FromError("#VALUE!"),
            ReferenceUnionNode => CellValue.FromError("#VALUE!"),
            UnaryNode unary => EvaluateUnary(unary, context, dependencies),
            BinaryNode binary => EvaluateBinary(binary, context, dependencies),
            FunctionNode function => EvaluateFunction(
                function,
                context,
                dependencies),
            _ => CellValue.FromError("#VALUE!"),
        };

    private static CellValue EvaluateCell(
        CellNode cell,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        dependencies.Add(new FormulaDependency(
            cell.WorksheetName,
            new CellRange(cell.Address, cell.Address)));
        return context.GetCellValue(
            cell.WorksheetName,
            cell.Address);
    }

    private CellValue EvaluateUnary(
        UnaryNode unary,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        var value = EvaluateNode(unary.Operand, context, dependencies);
        if (!TryNumber(value, out var number))
        {
            return CellValue.FromError("#VALUE!");
        }

        return unary.Operator == FormulaTokenKind.Minus
            ? SafeNumber(-number)
            : SafeNumber(number);
    }

    private CellValue EvaluateBinary(
        BinaryNode binary,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        var left = EvaluateNode(binary.Left, context, dependencies);
        var right = EvaluateNode(binary.Right, context, dependencies);
        if (left.Kind == CellValueKind.Error)
        {
            return left;
        }
        if (right.Kind == CellValueKind.Error)
        {
            return right;
        }
        if (binary.Operator == FormulaTokenKind.Concat)
        {
            return CellValue.FromText(left.ToString() + right.ToString());
        }
        if (binary.Operator is FormulaTokenKind.Equal or
            FormulaTokenKind.NotEqual or
            FormulaTokenKind.Less or
            FormulaTokenKind.LessOrEqual or
            FormulaTokenKind.Greater or
            FormulaTokenKind.GreaterOrEqual)
        {
            var comparison = Compare(left, right);
            return CellValue.FromBoolean(binary.Operator switch
            {
                FormulaTokenKind.Equal => comparison == 0,
                FormulaTokenKind.NotEqual => comparison != 0,
                FormulaTokenKind.Less => comparison < 0,
                FormulaTokenKind.LessOrEqual => comparison <= 0,
                FormulaTokenKind.Greater => comparison > 0,
                FormulaTokenKind.GreaterOrEqual => comparison >= 0,
                _ => false,
            });
        }
        if (!TryNumber(left, out var leftNumber) ||
            !TryNumber(right, out var rightNumber))
        {
            return CellValue.FromError("#VALUE!");
        }
        if (binary.Operator == FormulaTokenKind.Divide &&
            Math.Abs(rightNumber) <= double.Epsilon)
        {
            return CellValue.FromError("#DIV/0!");
        }

        var value = binary.Operator switch
        {
            FormulaTokenKind.Plus => leftNumber + rightNumber,
            FormulaTokenKind.Minus => leftNumber - rightNumber,
            FormulaTokenKind.Multiply => leftNumber * rightNumber,
            FormulaTokenKind.Divide => leftNumber / rightNumber,
            FormulaTokenKind.Power => Math.Pow(leftNumber, rightNumber),
            _ => double.NaN,
        };
        return SafeNumber(value);
    }

    private CellValue EvaluateFunction(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (TryEvaluateWholeColumnVlookup(
                function,
                context,
                dependencies,
                out var wholeColumnLookup))
        {
            return wholeColumnLookup;
        }
        if (string.Equals(function.Name, "LET", StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateF019Let(function, context, dependencies);
        }
        if (string.Equals(function.Name, "LAMBDA", StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateF019Lambda(function);
        }
        if (string.Equals(function.Name, "ISOMITTED", StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateF019IsOmitted(function);
        }
        if (string.Equals(
                function.Name,
                "IFERROR",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateIfError(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "IFNA",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateIfNa(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "SWITCH",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateSwitch(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "XMATCH",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateXMatch(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "GETPIVOTDATA",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateGetPivotData(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "INDIRECT",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateIndirect(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "OFFSET",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateOffset(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "ROW",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateRow(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "ROWS",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateRows(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "SHEET",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateSheet(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "SHEETS",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateSheets(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "AREAS",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateAreas(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "CHOOSE",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateChoose(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "COLUMN",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateColumn(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "COLUMNS",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateColumns(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "FORMULATEXT",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateFormulaText(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "FORMULA",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateF019Formula(function, context, dependencies);
        }
        if (string.Equals(function.Name, "CELL", StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateF018Cell(function, context, dependencies);
        }
        if (string.Equals(function.Name, "ISFORMULA", StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateF018IsFormula(function, context, dependencies);
        }
        if (string.Equals(function.Name, "ISREF", StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateF018IsRef(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "IF",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateIf(function, context, dependencies);
        }
        if (string.Equals(
                function.Name,
                "SUBTOTAL",
                StringComparison.OrdinalIgnoreCase))
        {
            return EvaluateSubtotal(function, context, dependencies);
        }
        if (TryEvaluateConditionalAggregate(
                function,
                context,
                dependencies,
                out var aggregateValue))
        {
            return aggregateValue;
        }
        if (!_functions.TryResolve(
                function.Name,
                out var formulaFunction))
        {
            return CellValue.FromError("#NAME?");
        }

        var invocationArguments =
            new List<FormulaFunctionArgument>(function.Arguments.Count);
        foreach (var argumentNode in function.Arguments)
        {
            if (TryEvaluateIndirectInvocationArgument(
                    argumentNode,
                    context,
                    dependencies,
                    out var indirectArgument))
            {
                invocationArguments.Add(indirectArgument);
                continue;
            }
            if (TryEvaluateAdvancedReferenceInvocationArgument(
                    argumentNode,
                    context,
                    dependencies,
                    out var advancedReferenceArgument))
            {
                invocationArguments.Add(advancedReferenceArgument);
                continue;
            }
            if (TryEvaluateChooseInvocationArgument(
                    argumentNode,
                    context,
                    dependencies,
                    out var chooseArgument))
            {
                invocationArguments.Add(chooseArgument);
                continue;
            }

            if (argumentNode is RangeNode range)
            {
                var dependency = new FormulaDependency(
                    range.WorksheetName,
                    range.Range);
                dependencies.Add(dependency);
                if (range.ExtentKind != FormulaRangeExtentKind.Cells)
                {
                    invocationArguments.Add(FormulaFunctionArgument.Scalar(
                        CellValue.FromError("#VALUE!")));
                    continue;
                }
                var rangeValues = new List<CellValue>(
                    checked(range.Range.RowCount * range.Range.ColumnCount));
                AppendRange(rangeValues, range, context);
                invocationArguments.Add(
                    FormulaFunctionArgument.Range(
                        dependency,
                        rangeValues));
            }
            else
            {
                invocationArguments.Add(
                    FormulaFunctionArgument.Scalar(
                        EvaluateNode(
                            argumentNode,
                            context,
                            dependencies)));
            }
        }

        FormulaEvaluationResult result;
        if (formulaFunction is IVersionedFormulaFunction versioned)
        {
            result = versioned.Invoke(
                new FormulaFunctionInvocation(
                    invocationArguments,
                    context));
        }
        else
        {
            result = formulaFunction.Invoke(
                invocationArguments
                    .SelectMany(static argument => argument.Values)
                    .ToArray(),
                context);
        }
        dependencies.AddRange(result.Dependencies);
        return result.Value;
    }

    private CellValue EvaluateIf(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count is < 2 or > 3)
        {
            return CellValue.FromError("#VALUE!");
        }

        var condition = EvaluateNode(
            function.Arguments[0],
            context,
            dependencies);
        if (!TryBoolean(condition, out var isTrue))
        {
            return CellValue.FromError("#VALUE!");
        }
        if (isTrue)
        {
            return EvaluateNode(
                function.Arguments[1],
                context,
                dependencies);
        }

        return function.Arguments.Count == 3
            ? EvaluateNode(
                function.Arguments[2],
                context,
                dependencies)
            : CellValue.FromBoolean(false);
    }

    private CellValue EvaluateSubtotal(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies)
    {
        if (function.Arguments.Count < 2)
        {
            return CellValue.FromError("#VALUE!");
        }

        var functionValue = EvaluateNode(
            function.Arguments[0],
            context,
            dependencies);
        if (!TryFunctionNumber(functionValue, out var functionNumber) ||
            !TryGetSubtotalKind(functionNumber, out var kind))
        {
            return CellValue.FromError("#VALUE!");
        }

        var values = new List<CellValue>();
        for (var index = 1;
             index < function.Arguments.Count;
             index++)
        {
            var argument = function.Arguments[index];
            switch (argument)
            {
                case RangeNode range:
                    dependencies.Add(new FormulaDependency(
                        range.WorksheetName,
                        range.Range));
                    AppendRowVisibilityDependencies(
                        dependencies,
                        range.WorksheetName,
                        range.Range,
                        context);
                    AppendVisibleRange(values, range, context);
                    break;
                case CellNode cell:
                    var cellRange = new CellRange(
                        cell.Address,
                        cell.Address);
                    dependencies.Add(new FormulaDependency(
                        cell.WorksheetName,
                        cellRange));
                    AppendRowVisibilityDependencies(
                        dependencies,
                        cell.WorksheetName,
                        cellRange,
                        context);
                    if (IsRowVisible(
                            context,
                            cell.WorksheetName,
                            cell.Address.RowIndex))
                    {
                        values.Add(context.GetCellValue(
                            cell.WorksheetName,
                            cell.Address));
                    }
                    break;
                default:
                    values.Add(EvaluateNode(
                        argument,
                        context,
                        dependencies));
                    break;
            }
        }

        return AggregateSubtotal(kind, values);
    }

    private static CellValue AggregateSubtotal(
        SubtotalKind kind,
        List<CellValue> values)
    {
        if (kind == SubtotalKind.CountNumbers)
        {
            return CellValue.FromNumber(values.Count(static value =>
                value.Kind == CellValueKind.Number));
        }
        if (kind == SubtotalKind.CountNonBlank)
        {
            return CellValue.FromNumber(values.Count(static value =>
                !value.IsBlank));
        }

        foreach (var value in values)
        {
            if (value.Kind == CellValueKind.Error)
            {
                return value;
            }
        }

        var numbers = values
            .Where(static value => value.Kind == CellValueKind.Number)
            .Select(static value => (double)value.RawValue!)
            .ToArray();
        return kind switch
        {
            SubtotalKind.Sum => SafeNumber(numbers.Sum()),
            SubtotalKind.Average => numbers.Length == 0
                ? CellValue.FromError("#DIV/0!")
                : SafeNumber(numbers.Average()),
            SubtotalKind.Minimum => numbers.Length == 0
                ? CellValue.FromNumber(0d)
                : SafeNumber(numbers.Min()),
            SubtotalKind.Maximum => numbers.Length == 0
                ? CellValue.FromNumber(0d)
                : SafeNumber(numbers.Max()),
            _ => CellValue.FromError("#VALUE!"),
        };
    }

    private bool TryEvaluateWholeColumnVlookup(
        FunctionNode function,
        IFormulaEvaluationContext context,
        List<FormulaDependency> dependencies,
        out CellValue value)
    {
        if (!string.Equals(
                function.Name,
                "VLOOKUP",
                StringComparison.OrdinalIgnoreCase) ||
            function.Arguments.Count is < 3 or > 4 ||
            function.Arguments[1] is not RangeNode
            {
                ExtentKind: FormulaRangeExtentKind.WholeColumns,
            } range)
        {
            value = default;
            return false;
        }

        var lookupValue = EvaluateNode(
            function.Arguments[0],
            context,
            dependencies);
        if (lookupValue.Kind == CellValueKind.Error)
        {
            value = lookupValue;
            return true;
        }
        var indexValue = EvaluateNode(
            function.Arguments[2],
            context,
            dependencies);
        if (indexValue.Kind == CellValueKind.Error)
        {
            value = indexValue;
            return true;
        }
        if (!TryNumber(indexValue, out var indexNumber) ||
            !double.IsFinite(indexNumber))
        {
            value = CellValue.FromError("#VALUE!");
            return true;
        }
        var columnOffset = (int)Math.Truncate(indexNumber) - 1;
        if (columnOffset < 0 ||
            range.Range.Left + columnOffset > range.Range.Right)
        {
            value = CellValue.FromError("#REF!");
            return true;
        }

        var approximate = true;
        if (function.Arguments.Count == 4)
        {
            var approximateValue = EvaluateNode(
                function.Arguments[3],
                context,
                dependencies);
            if (approximateValue.Kind == CellValueKind.Error)
            {
                value = approximateValue;
                return true;
            }
            if (!TryBoolean(approximateValue, out approximate))
            {
                value = CellValue.FromError("#VALUE!");
                return true;
            }
        }

        dependencies.Add(new FormulaDependency(
            range.WorksheetName,
            range.Range));
        if (context is not IFormulaSparseRangeContext sparseContext ||
            !sparseContext.TryGetUsedRowIndexes(
                range.WorksheetName,
                range.Range,
                out var usedRows))
        {
            value = CellValue.FromError("#VALUE!");
            return true;
        }

        var foundRow = -1;
        foreach (var row in usedRows)
        {
            var candidate = context.GetCellValue(
                range.WorksheetName,
                new CellAddress(row, range.Range.Left));
            if (candidate.Kind == CellValueKind.Error)
            {
                value = candidate;
                return true;
            }
            var comparison = Compare(candidate, lookupValue);
            if (comparison == 0)
            {
                foundRow = row;
                break;
            }
            if (approximate && comparison <= 0)
            {
                foundRow = row;
            }
        }

        value = foundRow < 0
            ? CellValue.FromError("#N/A")
            : context.GetCellValue(
                range.WorksheetName,
                new CellAddress(
                    foundRow,
                    range.Range.Left + columnOffset));
        return true;
    }

    private static void AppendRange(
        List<CellValue> values,
        RangeNode range,
        IFormulaEvaluationContext context)
    {
        for (var row = range.Range.Top;
             row <= range.Range.Bottom;
             row++)
        {
            for (var column = range.Range.Left;
                 column <= range.Range.Right;
                 column++)
            {
                values.Add(context.GetCellValue(
                    range.WorksheetName,
                    new CellAddress(row, column)));
            }
        }
    }

    private static void AppendVisibleRange(
        List<CellValue> values,
        RangeNode range,
        IFormulaEvaluationContext context)
    {
        for (var row = range.Range.Top;
             row <= range.Range.Bottom;
             row++)
        {
            if (!IsRowVisible(context, range.WorksheetName, row))
            {
                continue;
            }

            for (var column = range.Range.Left;
                 column <= range.Range.Right;
                 column++)
            {
                values.Add(context.GetCellValue(
                    range.WorksheetName,
                    new CellAddress(row, column)));
            }
        }
    }

    private static void AppendRowVisibilityDependencies(
        List<FormulaDependency> dependencies,
        string? worksheetName,
        CellRange range,
        IFormulaEvaluationContext context)
    {
        if (context is not IFilterAwareFormulaEvaluationContext
            filterAwareContext)
        {
            return;
        }

        dependencies.AddRange(
            filterAwareContext.GetRowVisibilityDependencies(
                worksheetName,
                range));
    }

    private static bool IsRowVisible(
        IFormulaEvaluationContext context,
        string? worksheetName,
        int rowIndex) =>
        context is not IFilterAwareFormulaEvaluationContext filterAware ||
        filterAware.IsRowVisible(worksheetName, rowIndex);

    private static bool TryFunctionNumber(
        CellValue value,
        out int functionNumber)
    {
        if (!TryNumber(value, out var number) ||
            !double.IsFinite(number) ||
            number < int.MinValue ||
            number > int.MaxValue)
        {
            functionNumber = default;
            return false;
        }

        var rounded = Math.Round(number);
        if (Math.Abs(number - rounded) > double.Epsilon)
        {
            functionNumber = default;
            return false;
        }

        functionNumber = (int)rounded;
        return true;
    }

    private static bool TryGetSubtotalKind(
        int functionNumber,
        out SubtotalKind kind)
    {
        kind = functionNumber switch
        {
            1 or 101 => SubtotalKind.Average,
            2 or 102 => SubtotalKind.CountNumbers,
            3 or 103 => SubtotalKind.CountNonBlank,
            4 or 104 => SubtotalKind.Maximum,
            5 or 105 => SubtotalKind.Minimum,
            9 or 109 => SubtotalKind.Sum,
            _ => default,
        };
        return functionNumber is
            1 or 101 or
            2 or 102 or
            3 or 103 or
            4 or 104 or
            5 or 105 or
            9 or 109;
    }

    private static bool TryNumber(
        CellValue value,
        out double number)
    {
        switch (value.Kind)
        {
            case CellValueKind.Number:
                number = (double)value.RawValue!;
                return true;
            case CellValueKind.Boolean:
                number = (bool)value.RawValue! ? 1d : 0d;
                return true;
            case CellValueKind.Blank:
                number = 0d;
                return true;
            default:
                number = 0d;
                return false;
        }
    }

    private static bool TryBoolean(
        CellValue value,
        out bool result)
    {
        switch (value.Kind)
        {
            case CellValueKind.Boolean:
                result = (bool)value.RawValue!;
                return true;
            case CellValueKind.Number:
                result = Math.Abs((double)value.RawValue!) >
                         double.Epsilon;
                return true;
            case CellValueKind.Blank:
                result = false;
                return true;
            default:
                result = false;
                return false;
        }
    }

    private static int Compare(CellValue left, CellValue right)
    {
        if (TryNumber(left, out var leftNumber) &&
            TryNumber(right, out var rightNumber))
        {
            return leftNumber.CompareTo(rightNumber);
        }

        return string.Compare(
            left.ToString(),
            right.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static CellValue SafeNumber(double value) =>
        double.IsFinite(value)
            ? CellValue.FromNumber(value)
            : CellValue.FromError("#NUM!");

    private enum SubtotalKind
    {
        Average = 0,
        CountNumbers,
        CountNonBlank,
        Maximum,
        Minimum,
        Sum,
    }
}
