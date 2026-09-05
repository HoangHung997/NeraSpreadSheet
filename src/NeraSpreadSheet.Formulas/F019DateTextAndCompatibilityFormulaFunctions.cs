using System.Globalization;
using System.Xml;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas;

public interface IFormulaCurrentValueContext : IFormulaEvaluationContext
{
    CellValue CurrentFormulaCellValue { get; }
}

public interface IFormulaHostInfoContext : IFormulaEvaluationContext
{
    bool TryGetFormulaInfo(string typeText, out CellValue value);
}

internal static class F019DateTextAndCompatibilityFormulaFunctions
{
    private static readonly Dictionary<string, double> EuroRates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["EUR"] = 1d,
            ["ATS"] = 13.7603d,
            ["BEF"] = 40.3399d,
            ["CYP"] = 0.585274d,
            ["DEM"] = 1.95583d,
            ["EEK"] = 15.6466d,
            ["ESP"] = 166.386d,
            ["FIM"] = 5.94573d,
            ["FRF"] = 6.55957d,
            ["GRD"] = 340.750d,
            ["IEP"] = 0.787564d,
            ["ITL"] = 1936.27d,
            ["LTL"] = 3.45280d,
            ["LUF"] = 40.3399d,
            ["LVL"] = 0.702804d,
            ["MTL"] = 0.429300d,
            ["NLG"] = 2.20371d,
            ["PTE"] = 200.482d,
            ["SIT"] = 239.640d,
            ["SKK"] = 30.1260d,
        };

    public static IEnumerable<IFormulaFunction> Create()
    {
        yield return Scalar("DAYSINMONTH", 1, 1, EvaluateDaysInMonth);
        yield return Scalar("DAYSINYEAR", 1, 1, EvaluateDaysInYear);
        yield return Scalar("EASTERSUNDAY", 1, 1, EvaluateEasterSunday);
        yield return Scalar("ISLEAPYEAR", 1, 1, EvaluateIsLeapYear);
        yield return Scalar("MONTHS", 3, 3, EvaluateMonths);
        yield return Scalar("WEEKNUM_EXCEL2003", 1, 1, EvaluateWeeknumExcel2003);
        yield return Scalar("WEEKNUM_OOO", 1, 2, EvaluateWeeknumOoo);
        yield return Scalar("WEEKNUM_ADD", 1, 2, EvaluateWeeknumAdd);
        yield return Scalar("WEEKS", 3, 3, EvaluateWeeks);
        yield return Scalar("WEEKSINYEAR", 1, 1, EvaluateWeeksInYear);
        yield return Scalar("YEARS", 3, 3, EvaluateYears);
        yield return Scalar("ROT13", 1, 1, EvaluateRot13);
        yield return Flattened("RAWSUBTRACT", 2, 254, EvaluateRawSubtract);
        yield return Scalar(
            "CURRENT",
            0,
            0,
            EvaluateCurrent,
            FormulaFunctionVolatility.Volatile,
            FormulaFunctionSecurityClassification.ContextReadOnly);
        yield return Scalar(
            "FORMULA",
            1,
            1,
            static _ => Error("#VALUE!"),
            security: FormulaFunctionSecurityClassification.ContextReadOnly);
        yield return Scalar("BINOM.DIST.RANGE", 3, 4, EvaluateBinomialRange);
        yield return Scalar("EUROCONVERT", 3, 5, EvaluateEuroConvert);
        yield return Scalar(
            "INFO",
            1,
            1,
            EvaluateInfo,
            FormulaFunctionVolatility.Volatile,
            FormulaFunctionSecurityClassification.ContextReadOnly);
        yield return Scalar("PHONETIC", 1, 1, EvaluatePhonetic);
        yield return Scalar("FILTERXML", 2, 2, EvaluateFilterXml);
    }

    private static FormulaFunctionDefinition Scalar(
        string name,
        int minimum,
        int maximum,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator,
        FormulaFunctionVolatility volatility = FormulaFunctionVolatility.Deterministic,
        FormulaFunctionSecurityClassification security = FormulaFunctionSecurityClassification.Pure) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimum,
                maximum,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                volatility,
                security,
                argumentCountPolicy: FormulaFunctionArgumentCountPolicy.LogicalArguments),
            evaluator);

    private static FormulaFunctionDefinition Flattened(
        string name,
        int minimum,
        int maximum,
        Func<FormulaFunctionInvocation, FormulaEvaluationResult> evaluator) =>
        new(
            new FormulaFunctionDescriptor(
                new FormulaFunctionIdentity("NERA.BUILTIN", name),
                new FormulaFunctionVersion(1, 0, 0),
                FormulaFunctionApiVersion.Current,
                minimum,
                maximum,
                FormulaFunctionCapabilities.ScalarArguments |
                FormulaFunctionCapabilities.RangeArguments |
                FormulaFunctionCapabilities.ReturnsScalar,
                argumentCountPolicy: FormulaFunctionArgumentCountPolicy.FlattenedValues),
            evaluator);

    private static FormulaEvaluationResult EvaluateDaysInMonth(FormulaFunctionInvocation invocation) =>
        TryDate(invocation.Arguments[0], out var date)
            ? Number(DateTime.DaysInMonth(date.Year, date.Month))
            : Error("#VALUE!");

    private static FormulaEvaluationResult EvaluateDaysInYear(FormulaFunctionInvocation invocation) =>
        TryDate(invocation.Arguments[0], out var date)
            ? Number(DateTime.IsLeapYear(date.Year) ? 366d : 365d)
            : Error("#VALUE!");

    private static FormulaEvaluationResult EvaluateEasterSunday(FormulaFunctionInvocation invocation)
    {
        if (!TryNumber(invocation.Arguments[0], out var yearValue))
        {
            return Error("#VALUE!");
        }
        var year = checked((int)Math.Truncate(yearValue));
        if (year is < 1583 or > 9999)
        {
            return Error("#NUM!");
        }
        // Meeus/Jones/Butcher Gregorian computus.
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var month = (h + l - (7 * m) + 114) / 31;
        var day = ((h + l - (7 * m) + 114) % 31) + 1;
        return Date(new DateTime(year, month, day));
    }

    private static FormulaEvaluationResult EvaluateIsLeapYear(FormulaFunctionInvocation invocation) =>
        TryDate(invocation.Arguments[0], out var date)
            ? Boolean(DateTime.IsLeapYear(date.Year))
            : Error("#VALUE!");

    private static FormulaEvaluationResult EvaluateMonths(FormulaFunctionInvocation invocation)
    {
        if (!TryDate(invocation.Arguments[0], out var start) ||
            !TryDate(invocation.Arguments[1], out var end) ||
            !TryInteger(invocation.Arguments[2], out var type) ||
            type is < 0 or > 1 || end < start)
        {
            return Error("#VALUE!");
        }
        if (type == 1)
        {
            return Number(((end.Year - start.Year) * 12) + end.Month - start.Month);
        }
        var months = ((end.Year - start.Year) * 12) + end.Month - start.Month;
        if (end.Day < start.Day)
        {
            months--;
        }
        return Number(months);
    }

    private static FormulaEvaluationResult EvaluateWeeknumExcel2003(FormulaFunctionInvocation invocation)
    {
        if (!TryDate(invocation.Arguments[0], out var date))
        {
            return Error("#VALUE!");
        }
        var jan1 = new DateTime(date.Year, 1, 1);
        var offset = (int)jan1.DayOfWeek;
        return Number(((date.DayOfYear - 1 + offset) / 7) + 1);
    }

    private static FormulaEvaluationResult EvaluateWeeknumOoo(FormulaFunctionInvocation invocation)
    {
        if (!TryDate(invocation.Arguments[0], out var date))
        {
            return Error("#VALUE!");
        }
        var mode = 1;
        if (invocation.Arguments.Count == 2 &&
            !TryInteger(invocation.Arguments[1], out mode))
        {
            return Error("#VALUE!");
        }
        return mode switch
        {
            1 => EvaluateWeeknumExcel2003(invocation),
            2 or 21 => Number(System.Globalization.ISOWeek.GetWeekOfYear(date)),
            _ => Error("#NUM!"),
        };
    }

    private static FormulaEvaluationResult EvaluateWeeknumAdd(FormulaFunctionInvocation invocation) =>
        EvaluateWeeknumOoo(invocation);

    private static FormulaEvaluationResult EvaluateWeeks(FormulaFunctionInvocation invocation)
    {
        if (!TryDate(invocation.Arguments[0], out var start) ||
            !TryDate(invocation.Arguments[1], out var end) ||
            !TryInteger(invocation.Arguments[2], out var type) ||
            type is < 0 or > 1 || end < start)
        {
            return Error("#VALUE!");
        }
        if (type == 0)
        {
            return Number(Math.Floor((end.Date - start.Date).TotalDays / 7d));
        }
        var count = 0;
        for (var date = start.Date.AddDays(1); date <= end.Date; date = date.AddDays(1))
        {
            if (date.DayOfWeek == DayOfWeek.Monday)
            {
                count++;
            }
        }
        return Number(count);
    }

    private static FormulaEvaluationResult EvaluateWeeksInYear(FormulaFunctionInvocation invocation)
    {
        if (!TryDate(invocation.Arguments[0], out var date))
        {
            return Error("#VALUE!");
        }
        return Number(System.Globalization.ISOWeek.GetWeeksInYear(date.Year));
    }

    private static FormulaEvaluationResult EvaluateYears(FormulaFunctionInvocation invocation)
    {
        if (!TryDate(invocation.Arguments[0], out var start) ||
            !TryDate(invocation.Arguments[1], out var end) ||
            !TryInteger(invocation.Arguments[2], out var type) ||
            type is < 0 or > 1 || end < start)
        {
            return Error("#VALUE!");
        }
        if (type == 1)
        {
            return Number(end.Year - start.Year);
        }
        var years = end.Year - start.Year;
        if (end.Month < start.Month ||
            (end.Month == start.Month && end.Day < start.Day))
        {
            years--;
        }
        return Number(years);
    }

    private static FormulaEvaluationResult EvaluateRot13(FormulaFunctionInvocation invocation)
    {
        if (!TryText(invocation.Arguments[0], out var text))
        {
            return Error("#VALUE!");
        }
        var chars = text.ToCharArray();
        for (var index = 0; index < chars.Length; index++)
        {
            var c = chars[index];
            if (c is >= 'A' and <= 'Z')
            {
                chars[index] = (char)('A' + ((c - 'A' + 13) % 26));
            }
            else if (c is >= 'a' and <= 'z')
            {
                chars[index] = (char)('a' + ((c - 'a' + 13) % 26));
            }
        }
        return Text(new string(chars));
    }

    private static FormulaEvaluationResult EvaluateRawSubtract(FormulaFunctionInvocation invocation)
    {
        var values = invocation.FlattenValues();
        if (values.Length < 2 || values.Length > 254)
        {
            return Error("#VALUE!");
        }
        if (!FormulaValueCoercion.TryNumber(values[0], out var result, allowText: true))
        {
            return Error("#VALUE!");
        }
        for (var index = 1; index < values.Length; index++)
        {
            if (!FormulaValueCoercion.TryNumber(values[index], out var number, allowText: true))
            {
                return Error("#VALUE!");
            }
            result -= number;
        }
        return Number(result);
    }

    private static FormulaEvaluationResult EvaluateCurrent(FormulaFunctionInvocation invocation) =>
        invocation.Context is IFormulaCurrentValueContext current
            ? FormulaEvaluationResult.Success(current.CurrentFormulaCellValue)
            : FormulaEvaluationResult.Success(CellValue.Blank);

    private static FormulaEvaluationResult EvaluateBinomialRange(FormulaFunctionInvocation invocation)
    {
        if (!TryInteger(invocation.Arguments[0], out var trials) ||
            !TryNumber(invocation.Arguments[1], out var probability) ||
            !TryInteger(invocation.Arguments[2], out var lower) ||
            (invocation.Arguments.Count == 4 &&
             !TryInteger(invocation.Arguments[3], out _)))
        {
            return Error("#VALUE!");
        }
        var upper = lower;
        if (invocation.Arguments.Count == 4)
        {
            _ = TryInteger(invocation.Arguments[3], out upper);
        }
        if (trials < 0 || probability < 0d || probability > 1d ||
            lower < 0 || upper < lower || upper > trials)
        {
            return Error("#NUM!");
        }
        var sum = 0d;
        for (var successes = lower; successes <= upper; successes++)
        {
            var logP = LogCombination(trials, successes) +
                       (successes * LogProbability(probability)) +
                       ((trials - successes) * LogProbability(1d - probability));
            var term = double.IsNegativeInfinity(logP) ? 0d : Math.Exp(logP);
            sum += term;
        }
        return Number(Math.Clamp(sum, 0d, 1d));
    }

    private static double LogProbability(double p) =>
        p == 0d ? double.NegativeInfinity : Math.Log(p);

    private static double LogCombination(int total, int selected) =>
        StatisticalNumerics.LogGamma(total + 1d) -
        StatisticalNumerics.LogGamma(selected + 1d) -
        StatisticalNumerics.LogGamma(total - selected + 1d);

    private static FormulaEvaluationResult EvaluateEuroConvert(FormulaFunctionInvocation invocation)
    {
        if (!TryNumber(invocation.Arguments[0], out var number) ||
            !TryText(invocation.Arguments[1], out var source) ||
            !TryText(invocation.Arguments[2], out var target) ||
            !EuroRates.TryGetValue(source.Trim(), out var sourceRate) ||
            !EuroRates.TryGetValue(target.Trim(), out var targetRate))
        {
            return Error("#VALUE!");
        }
        var fullPrecision = false;
        if (invocation.Arguments.Count >= 4 &&
            !TryBoolean(invocation.Arguments[3], out fullPrecision))
        {
            return Error("#VALUE!");
        }
        var triangulationPrecision = 0;
        if (invocation.Arguments.Count == 5 &&
            (!TryInteger(invocation.Arguments[4], out triangulationPrecision) ||
             triangulationPrecision is < 3 or > 15))
        {
            return Error("#NUM!");
        }
        var euros = number / sourceRate;
        if (triangulationPrecision > 0)
        {
            euros = Math.Round(euros, triangulationPrecision, MidpointRounding.AwayFromZero);
        }
        var result = euros * targetRate;
        if (!fullPrecision)
        {
            result = Math.Round(result, 2, MidpointRounding.AwayFromZero);
        }
        return Number(result);
    }

    private static FormulaEvaluationResult EvaluateInfo(FormulaFunctionInvocation invocation)
    {
        if (!TryText(invocation.Arguments[0], out var type))
        {
            return Error("#VALUE!");
        }
        var normalized = type.Trim().ToLowerInvariant();
        if (invocation.Context is IFormulaHostInfoContext provider &&
            provider.TryGetFormulaInfo(normalized, out var value))
        {
            return FormulaEvaluationResult.Success(value);
        }
        return normalized switch
        {
            "system" => Text("NERA"),
            "osversion" => Text("NeraSpreadSheet deterministic host"),
            "release" => Text("1.0"),
            "recalc" => Text("Automatic"),
            "numfile" => Number(1d),
            "directory" => Text(string.Empty),
            "origin" => Text("$A:$1"),
            _ => Error("#VALUE!"),
        };
    }

    private static FormulaEvaluationResult EvaluatePhonetic(FormulaFunctionInvocation invocation)
    {
        if (!TryText(invocation.Arguments[0], out var text))
        {
            return Error("#VALUE!");
        }
        // Cell phonetic runs are optional metadata. When they are absent Excel
        // effectively has no alternate reading; preserve the source text.
        return Text(text);
    }

    private static FormulaEvaluationResult EvaluateFilterXml(FormulaFunctionInvocation invocation)
    {
        if (!TryText(invocation.Arguments[0], out var xml) ||
            !TryText(invocation.Arguments[1], out var xpath) ||
            xml.Length > 1_000_000 || xpath.Length > 32_768)
        {
            return Error("#VALUE!");
        }
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 1_000_000,
            };
            using var stringReader = new StringReader(xml);
            using var reader = XmlReader.Create(stringReader, settings);
            var document = new XmlDocument { XmlResolver = null };
            document.Load(reader);
            var node = document.SelectSingleNode(xpath);
            return node is null ? Error("#VALUE!") : Text(node.InnerText);
        }
        catch (XmlException)
        {
            return Error("#VALUE!");
        }
        catch (System.Xml.XPath.XPathException)
        {
            return Error("#VALUE!");
        }
    }

    private static bool TryDate(FormulaFunctionArgument argument, out DateTime date)
    {
        date = default;
        return argument.Kind == FormulaFunctionArgumentKind.Scalar &&
               FormulaValueCoercion.TryDateTime(argument.ScalarValue, out date);
    }

    private static bool TryNumber(FormulaFunctionArgument argument, out double number)
    {
        number = default;
        return argument.Kind == FormulaFunctionArgumentKind.Scalar &&
               FormulaValueCoercion.TryNumber(argument.ScalarValue, out number, allowText: true) &&
               double.IsFinite(number);
    }

    private static bool TryInteger(FormulaFunctionArgument argument, out int value)
    {
        if (!TryNumber(argument, out var number) ||
            number < int.MinValue || number > int.MaxValue)
        {
            value = default;
            return false;
        }
        value = checked((int)Math.Truncate(number));
        return true;
    }

    private static bool TryText(FormulaFunctionArgument argument, out string text)
    {
        if (argument.Kind != FormulaFunctionArgumentKind.Scalar ||
            argument.ScalarValue.Kind == CellValueKind.Error)
        {
            text = string.Empty;
            return false;
        }
        text = FormulaValueCoercion.ToText(argument.ScalarValue);
        return true;
    }

    private static bool TryBoolean(FormulaFunctionArgument argument, out bool value)
    {
        value = default;
        return argument.Kind == FormulaFunctionArgumentKind.Scalar &&
               FormulaValueCoercion.TryBoolean(argument.ScalarValue, out value);
    }

    private static FormulaEvaluationResult Number(double value) =>
        double.IsFinite(value)
            ? FormulaEvaluationResult.Success(CellValue.FromNumber(value))
            : Error("#NUM!");

    private static FormulaEvaluationResult Date(DateTime value) =>
        FormulaEvaluationResult.Success(CellValue.FromDateTime(value));

    private static FormulaEvaluationResult Text(string value) =>
        FormulaEvaluationResult.Success(CellValue.FromText(value));

    private static FormulaEvaluationResult Boolean(bool value) =>
        FormulaEvaluationResult.Success(CellValue.FromBoolean(value));

    private static FormulaEvaluationResult Error(string code) =>
        new(
            CellValue.FromError(code),
            FormulaErrorMapping.ToErrorCode(CellValue.FromError(code)),
            Array.Empty<FormulaDependency>());
}
