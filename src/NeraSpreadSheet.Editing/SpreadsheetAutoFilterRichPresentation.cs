using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NeraSpreadSheet.Editing;

/// <summary>Identifies a host-neutral section shown by a rich AutoFilter surface.</summary>
public enum SpreadsheetAutoFilterMenuKind
{
    Values = 0,
    Text,
    Number,
    Date,
    FillColor,
    FontColor,
    Icon,
    Custom,
    TopBottom,
    Dynamic,
}

/// <summary>Provides the default localizable label for a rich filter section.</summary>
public static class SpreadsheetAutoFilterMenuKindExtensions
{
    public static string GetDefaultDisplayName(this SpreadsheetAutoFilterMenuKind kind) =>
        kind switch
        {
            SpreadsheetAutoFilterMenuKind.Values => "Giá trị",
            SpreadsheetAutoFilterMenuKind.Text => "Bộ lọc văn bản",
            SpreadsheetAutoFilterMenuKind.Number => "Bộ lọc số",
            SpreadsheetAutoFilterMenuKind.Date => "Bộ lọc ngày",
            SpreadsheetAutoFilterMenuKind.FillColor => "Màu nền",
            SpreadsheetAutoFilterMenuKind.FontColor => "Màu chữ",
            SpreadsheetAutoFilterMenuKind.Icon => "Biểu tượng",
            SpreadsheetAutoFilterMenuKind.Custom => "Điều kiện tùy chỉnh",
            SpreadsheetAutoFilterMenuKind.TopBottom => "Trên/Dưới",
            SpreadsheetAutoFilterMenuKind.Dynamic => "Ngày động/Trung bình",
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
}

/// <summary>Identifies one lazily requested level of the calendar tree.</summary>
public enum SpreadsheetAutoFilterDateNodeLevel
{
    Year = 0,
    Month,
    Day,
}

/// <summary>Identifies the parent of a lazy date-tree request.</summary>
public readonly record struct SpreadsheetAutoFilterDateParent(int? Year, int? Month)
{
    public SpreadsheetAutoFilterDateNodeLevel ChildLevel =>
        Year is null
            ? SpreadsheetAutoFilterDateNodeLevel.Year
            : Month is null
                ? SpreadsheetAutoFilterDateNodeLevel.Month
                : SpreadsheetAutoFilterDateNodeLevel.Day;
}

/// <summary>Represents one bounded date-tree node without a native child control.</summary>
public sealed record SpreadsheetAutoFilterDateNode(
    SpreadsheetAutoFilterDateNodeLevel Level,
    int Year,
    int? Month,
    int? Day,
    int Count,
    bool HasChildren);

/// <summary>Represents one generation-checked page of a lazy date tree.</summary>
public sealed record SpreadsheetAutoFilterDatePage(
    long Generation,
    SpreadsheetAutoFilterDateParent Parent,
    int Offset,
    int PageSize,
    int TotalNodeCount,
    bool HasPreviousPage,
    bool HasNextPage,
    IReadOnlyList<SpreadsheetAutoFilterDateNode> Nodes);

/// <summary>
/// Carries exactly one rich criterion from a native surface to the production
/// Table or worksheet filter mutation path.
/// </summary>
public sealed record SpreadsheetAutoFilterRichCriterion
{
    public SpreadsheetAutoFilterRichCriterion(
        IEnumerable<SpreadsheetFilterDateGroup>? dateGroups = null,
        SpreadsheetTopBottomFilter? topBottom = null,
        SpreadsheetDynamicFilter? dynamicFilter = null,
        SpreadsheetColorFilter? colorFilter = null,
        SpreadsheetIconFilter? iconFilter = null)
    {
        DateGroups = dateGroups?.Distinct().ToArray() ?? [];
        TopBottom = topBottom;
        DynamicFilter = dynamicFilter;
        ColorFilter = colorFilter;
        IconFilter = iconFilter;
        var definitionCount = (DateGroups.Count > 0 ? 1 : 0) +
                              (topBottom is not null ? 1 : 0) +
                              (dynamicFilter is not null ? 1 : 0) +
                              (colorFilter is not null ? 1 : 0) +
                              (iconFilter is not null ? 1 : 0);
        if (definitionCount != 1)
        {
            throw new ArgumentException(
                "A rich AutoFilter request must contain exactly one criterion.");
        }
    }

    public IReadOnlyList<SpreadsheetFilterDateGroup> DateGroups { get; }

    public SpreadsheetTopBottomFilter? TopBottom { get; }

    public SpreadsheetDynamicFilter? DynamicFilter { get; }

    public SpreadsheetColorFilter? ColorFilter { get; }

    public SpreadsheetIconFilter? IconFilter { get; }

    internal TableFilterColumn CreateTableColumn(Guid columnId) => new(
        columnId,
        dateGroups: DateGroups,
        topBottom: TopBottom,
        dynamicFilter: DynamicFilter,
        colorFilter: ColorFilter,
        iconFilter: IconFilter);

    internal WorksheetAutoFilterColumn CreateWorksheetColumn(int columnOffset) => new(
        columnOffset,
        dateGroups: DateGroups,
        topBottom: TopBottom,
        dynamicFilter: DynamicFilter,
        colorFilter: ColorFilter,
        iconFilter: IconFilter);
}

/// <summary>Represents a parsed native editor action.</summary>
public sealed record SpreadsheetAutoFilterParsedCriterion(
    TableFilterCondition? CustomCondition,
    SpreadsheetAutoFilterRichCriterion? RichCriterion,
    TableFilterCondition? SecondCustomCondition = null,
    bool CombineWithAnd = true);

/// <summary>Parses the compact, common rich-filter editor used by every native host.</summary>
public static partial class SpreadsheetAutoFilterCriterionParser
{
    public static SpreadsheetAutoFilterParsedCriterion Parse(
        SpreadsheetAutoFilterMenuKind kind,
        string? input)
    {
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            throw new ArgumentException("Nhập giá trị cho điều kiện lọc.", nameof(input));
        }

        return kind switch
        {
            SpreadsheetAutoFilterMenuKind.Text => Custom(
                TableFilterComparisonOperator.Contains,
                CellValue.FromText(text)),
            SpreadsheetAutoFilterMenuKind.Number => Custom(
                TableFilterComparisonOperator.Equal,
                CellValue.FromNumber(ParseDouble(text))),
            SpreadsheetAutoFilterMenuKind.Date => Rich(new SpreadsheetAutoFilterRichCriterion(
                dateGroups: ParseDateGroups(text))),
            SpreadsheetAutoFilterMenuKind.FillColor => Rich(new SpreadsheetAutoFilterRichCriterion(
                colorFilter: new SpreadsheetColorFilter(
                    SpreadsheetFilterColorKind.Fill,
                    ParseColor(text)))),
            SpreadsheetAutoFilterMenuKind.FontColor => Rich(new SpreadsheetAutoFilterRichCriterion(
                colorFilter: new SpreadsheetColorFilter(
                    SpreadsheetFilterColorKind.Font,
                    ParseColor(text)))),
            SpreadsheetAutoFilterMenuKind.Icon => Rich(new SpreadsheetAutoFilterRichCriterion(
                iconFilter: ParseIcon(text))),
            SpreadsheetAutoFilterMenuKind.Custom => ParseCustom(text),
            SpreadsheetAutoFilterMenuKind.TopBottom => Rich(new SpreadsheetAutoFilterRichCriterion(
                topBottom: ParseTopBottom(text))),
            SpreadsheetAutoFilterMenuKind.Dynamic => Rich(new SpreadsheetAutoFilterRichCriterion(
                dynamicFilter: new SpreadsheetDynamicFilter(
                    Enum.Parse<SpreadsheetDynamicFilterType>(text, ignoreCase: true)))),
            _ => throw new ArgumentException(
                "The value checklist does not use a rich criterion input.",
                nameof(kind)),
        };
    }

    private static SpreadsheetAutoFilterParsedCriterion ParseCustom(string text)
    {
        var connector = CustomConditionConnectorRegex().Match(text);
        if (!connector.Success)
        {
            return new SpreadsheetAutoFilterParsedCriterion(
                ParseCustomCondition(text),
                RichCriterion: null);
        }
        if (connector.NextMatch().Success)
        {
            throw new FormatException(
                "Chỉ hỗ trợ tối đa hai điều kiện tùy chỉnh kết hợp bằng AND hoặc OR.");
        }

        var firstText = text[..connector.Index].Trim();
        var secondText = text[(connector.Index + connector.Length)..].Trim();
        if (firstText.Length == 0 || secondText.Length == 0)
        {
            throw new FormatException(
                "Hai vế của điều kiện tùy chỉnh không được để trống.");
        }
        return new SpreadsheetAutoFilterParsedCriterion(
            ParseCustomCondition(firstText),
            RichCriterion: null,
            ParseCustomCondition(secondText),
            string.Equals(
                connector.Groups[1].Value,
                "AND",
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                connector.Groups[1].Value,
                "&&",
                StringComparison.Ordinal));
    }

    /// <summary>Parses one custom comparison without an AND/OR connector.</summary>
    public static TableFilterCondition ParseCustomCondition(string input)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(input);
        var text = input.Trim();
        var separator = text.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0 &&
            Enum.TryParse<TableFilterComparisonOperator>(text, true, out var blankComparison) &&
            blankComparison is TableFilterComparisonOperator.IsBlank or
                TableFilterComparisonOperator.IsNotBlank)
        {
            return new TableFilterCondition(blankComparison, CellValue.Blank);
        }
        if (separator <= 0 || separator == text.Length - 1 ||
            !Enum.TryParse<TableFilterComparisonOperator>(
                text[..separator],
                ignoreCase: true,
                out var comparison))
        {
            throw new FormatException(
                "Dùng cú pháp TênPhépSoSánh:GiáTrị, ví dụ Contains:North.");
        }
        var operand = text[(separator + 1)..].Trim();
        var value = comparison switch
        {
            TableFilterComparisonOperator.OnDate or
            TableFilterComparisonOperator.BeforeDate or
            TableFilterComparisonOperator.AfterDate =>
                CellValue.FromDateTime(ParseDate(operand)),
            TableFilterComparisonOperator.GreaterThan or
            TableFilterComparisonOperator.GreaterThanOrEqual or
            TableFilterComparisonOperator.LessThan or
            TableFilterComparisonOperator.LessThanOrEqual =>
                CellValue.FromNumber(ParseDouble(operand)),
            _ => CellValue.FromText(operand),
        };
        return new TableFilterCondition(comparison, value);
    }

    private static SpreadsheetFilterDateGroup[] ParseDateGroups(
        string text)
    {
        var segments = text.Split(';', StringSplitOptions.TrimEntries);
        if (segments.Any(static segment => segment.Length == 0))
        {
            throw new FormatException(
                "Nhóm ngày phải được phân cách bằng dấu chấm phẩy và không được để trống.");
        }
        return segments.Select(ParseDateGroup).ToArray();
    }

    private static SpreadsheetFilterDateGroup ParseDateGroup(string text)
    {
        SpreadsheetFilterDateGrouping? requestedGrouping = null;
        var separator = text.IndexOf(':', StringComparison.Ordinal);
        if (separator > 0 &&
            Enum.TryParse<SpreadsheetFilterDateGrouping>(
                text[..separator],
                ignoreCase: true,
                out var parsedGrouping))
        {
            requestedGrouping = parsedGrouping;
            text = text[(separator + 1)..].Trim();
        }

        var match = DateGroupRegex().Match(text);
        if (!match.Success)
        {
            if (requestedGrouping is not null)
            {
                throw new FormatException(
                    "Nhóm ngày phải dùng dạng yyyy, yyyy-MM, yyyy-MM-dd hoặc thêm phần giờ:phút:giây.");
            }
            return ToDayGroup(ParseDate(text));
        }

        var parts = new int?[6];
        for (var index = 0; index < parts.Length; index++)
        {
            var group = match.Groups[index + 1];
            parts[index] = group.Success
                ? int.Parse(group.Value, CultureInfo.InvariantCulture)
                : null;
        }
        var inferredGrouping = parts[5] is not null
            ? SpreadsheetFilterDateGrouping.Second
            : parts[4] is not null
                ? SpreadsheetFilterDateGrouping.Minute
                : parts[3] is not null
                    ? SpreadsheetFilterDateGrouping.Hour
                    : parts[2] is not null
                        ? SpreadsheetFilterDateGrouping.Day
                        : parts[1] is not null
                            ? SpreadsheetFilterDateGrouping.Month
                            : SpreadsheetFilterDateGrouping.Year;
        if (requestedGrouping is not null &&
            requestedGrouping.Value != inferredGrouping)
        {
            throw new FormatException(
                "Độ chính xác của nhóm ngày không khớp với các thành phần đã nhập.");
        }
        return new SpreadsheetFilterDateGroup(
            parts[0]!.Value,
            requestedGrouping ?? inferredGrouping,
            parts[1],
            parts[2],
            parts[3],
            parts[4],
            parts[5]);
    }

    private static SpreadsheetTopBottomFilter ParseTopBottom(string text)
    {
        var normalized = text.Replace(" ", string.Empty, StringComparison.Ordinal);
        var top = true;
        if (normalized.StartsWith("bottom", StringComparison.OrdinalIgnoreCase))
        {
            top = false;
            normalized = normalized[6..];
        }
        else if (normalized.StartsWith("top", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[3..];
        }
        var percent = normalized.EndsWith('%');
        if (percent) normalized = normalized[..^1];
        return new SpreadsheetTopBottomFilter(top, percent, ParseDouble(normalized));
    }

    private static SpreadsheetIconFilter ParseIcon(string text)
    {
        var separator = text.LastIndexOf(':');
        if (separator <= 0 ||
            !uint.TryParse(text[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var iconId))
        {
            throw new FormatException(
                "Dùng cú pháp TênBộBiểuTượng:ChỉSố, ví dụ 3TrafficLights1:0.");
        }
        return new SpreadsheetIconFilter(text[..separator], iconId);
    }

    private static ColorRgba ParseColor(string text)
    {
        var hex = text.TrimStart('#');
        if (hex.Length is not (6 or 8) ||
            !uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
        {
            throw new FormatException("Màu phải có dạng #RRGGBB hoặc #AARRGGBB.");
        }
        return hex.Length == 6
            ? new ColorRgba((byte)(value >> 16), (byte)(value >> 8), (byte)value)
            : new ColorRgba((byte)(value >> 16), (byte)(value >> 8), (byte)value, (byte)(value >> 24));
    }

    private static double ParseDouble(string text)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ||
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return value;
        }
        throw new FormatException("Giá trị số không hợp lệ.");
    }

    private static DateTime ParseDate(string text)
    {
        if (DateTime.TryParse(text, CultureInfo.CurrentCulture, DateTimeStyles.None, out var value) ||
            DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out value))
        {
            return value;
        }
        throw new FormatException("Ngày không hợp lệ.");
    }

    private static SpreadsheetFilterDateGroup ToDayGroup(DateTime value) => new(
        value.Year,
        SpreadsheetFilterDateGrouping.Day,
        value.Month,
        value.Day);

    private static SpreadsheetAutoFilterParsedCriterion Custom(
        TableFilterComparisonOperator comparison,
        CellValue value) => new(new TableFilterCondition(comparison, value), null);

    private static SpreadsheetAutoFilterParsedCriterion Rich(
        SpreadsheetAutoFilterRichCriterion criterion) => new(null, criterion);

    [GeneratedRegex(
        @"\s+(AND|OR|&&|\|\|)\s+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CustomConditionConnectorRegex();

    [GeneratedRegex(
        @"^(\d{4})(?:-(\d{1,2})(?:-(\d{1,2})(?:[ T](\d{1,2})(?::(\d{1,2})(?::(\d{1,2}))?)?)?)?)?$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DateGroupRegex();
}

internal static class SpreadsheetAutoFilterRichProjection
{
    public static IReadOnlyList<SpreadsheetAutoFilterMenuKind> GetMenuKinds(
        IEnumerable<CellValue> values,
        IReadOnlyDictionary<DateTime, int> dateCounts)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(dateCounts);
        var hasText = false;
        var hasNumber = false;
        var hasDate = dateCounts.Count > 0;
        foreach (var value in values)
        {
            hasText |= value.Kind == CellValueKind.Text;
            hasNumber |= value.Kind == CellValueKind.Number;
        }

        var result = new List<SpreadsheetAutoFilterMenuKind>
        {
            SpreadsheetAutoFilterMenuKind.Values,
        };
        if (hasText) result.Add(SpreadsheetAutoFilterMenuKind.Text);
        if (hasNumber) result.Add(SpreadsheetAutoFilterMenuKind.Number);
        if (hasDate) result.Add(SpreadsheetAutoFilterMenuKind.Date);
        result.Add(SpreadsheetAutoFilterMenuKind.FillColor);
        result.Add(SpreadsheetAutoFilterMenuKind.FontColor);
        result.Add(SpreadsheetAutoFilterMenuKind.Icon);
        result.Add(SpreadsheetAutoFilterMenuKind.Custom);
        if (hasNumber) result.Add(SpreadsheetAutoFilterMenuKind.TopBottom);
        if (hasDate || hasNumber)
        {
            result.Add(SpreadsheetAutoFilterMenuKind.Dynamic);
        }
        return result;
    }

    public static SpreadsheetAutoFilterDatePage CaptureDatePage(
        IEnumerable<KeyValuePair<DateTime, int>> values,
        long generation,
        SpreadsheetAutoFilterDateParent parent,
        int offset,
        int pageSize,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            pageSize,
            SpreadsheetTableFilterMenu.MaximumPageSize);
        ValidateParent(parent);
        var counts = new Dictionary<(int Year, int? Month, int? Day), int>();
        var index = 0;
        foreach (var pair in values)
        {
            if ((index++ & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            var date = pair.Key;
            if (parent.Year is int year && date.Year != year) continue;
            if (parent.Month is int month && date.Month != month) continue;
            var key = parent.ChildLevel switch
            {
                SpreadsheetAutoFilterDateNodeLevel.Year => (date.Year, (int?)null, (int?)null),
                SpreadsheetAutoFilterDateNodeLevel.Month => (date.Year, date.Month, (int?)null),
                _ => (date.Year, date.Month, (int?)date.Day),
            };
            counts[key] = counts.TryGetValue(key, out var count)
                ? checked(count + pair.Value)
                : pair.Value;
        }

        var nodes = counts
            .OrderBy(static pair => pair.Key.Year)
            .ThenBy(static pair => pair.Key.Month)
            .ThenBy(static pair => pair.Key.Day)
            .Skip(offset)
            .Take(pageSize)
            .Select(pair => new SpreadsheetAutoFilterDateNode(
                parent.ChildLevel,
                pair.Key.Year,
                pair.Key.Month,
                pair.Key.Day,
                pair.Value,
                parent.ChildLevel != SpreadsheetAutoFilterDateNodeLevel.Day))
            .ToArray();
        return new SpreadsheetAutoFilterDatePage(
            generation,
            parent,
            offset,
            pageSize,
            counts.Count,
            offset > 0,
            checked(offset + nodes.Length) < counts.Count,
            nodes);
    }

    private static void ValidateParent(SpreadsheetAutoFilterDateParent parent)
    {
        if (parent.Month is not null && parent.Year is null)
        {
            throw new ArgumentException(
                "A date-tree month parent requires a year.",
                nameof(parent));
        }
        if (parent.Year is int year && (year < 1 || year > 9999))
        {
            throw new ArgumentOutOfRangeException(nameof(parent));
        }
        if (parent.Month is int month && (month < 1 || month > 12))
        {
            throw new ArgumentOutOfRangeException(nameof(parent));
        }
    }
}

internal static partial class SpreadsheetAutoFilterDateProjection
{
    public static bool TryGetDate(
        CellValue value,
        string? formatCode,
        ExcelDateSystem dateSystem,
        out DateTime date)
    {
        if (value.Kind == CellValueKind.DateTime)
        {
            date = (DateTime)value.RawValue!;
            return true;
        }
        if (value.Kind != CellValueKind.Number)
        {
            date = default;
            return false;
        }

        var serial = (double)value.RawValue!;
        if (!double.IsFinite(serial) ||
            !IsDateTimeFormat(SelectNumericSection(formatCode, serial)))
        {
            date = default;
            return false;
        }

        try
        {
            date = dateSystem == ExcelDateSystem.Date1904
                ? new DateTime(1904, 1, 1).AddDays(serial)
                : DateTime.FromOADate(serial);
            return true;
        }
        catch (ArgumentException)
        {
            date = default;
            return false;
        }
        catch (OverflowException)
        {
            date = default;
            return false;
        }
    }

    private static string SelectNumericSection(string? formatCode, double value)
    {
        if (string.IsNullOrWhiteSpace(formatCode))
        {
            return "General";
        }
        var sections = SplitSections(formatCode.Trim());
        if (sections.Count == 0)
        {
            return "General";
        }

        var candidates = sections.Take(Math.Min(3, sections.Count)).ToArray();
        var conditions = candidates.Select(ReadCondition).ToArray();
        if (conditions.Any(static condition => condition is not null))
        {
            for (var index = 0; index < candidates.Length; index++)
            {
                if (conditions[index] is null ||
                    conditions[index]!.Value.Matches(value))
                {
                    return candidates[index];
                }
            }
            return candidates[^1];
        }

        var sectionIndex = value > 0d
            ? 0
            : value < 0d && candidates.Length >= 2
                ? 1
                : value == 0d && candidates.Length >= 3
                    ? 2
                    : 0;
        return candidates[sectionIndex];
    }

    private static List<string> SplitSections(string formatCode)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var escaped = false;
        foreach (var character in formatCode)
        {
            if (escaped)
            {
                current.Append(character);
                escaped = false;
                continue;
            }
            if (character == '\\')
            {
                current.Append(character);
                escaped = true;
                continue;
            }
            if (character == '"')
            {
                quoted = !quoted;
                current.Append(character);
                continue;
            }
            if (character == ';' && !quoted)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(character);
        }
        result.Add(current.ToString());
        return result;
    }

    private static NumericCondition? ReadCondition(string section)
    {
        var match = NumericConditionRegex().Match(section);
        if (!match.Success ||
            !double.TryParse(
                match.Groups[2].Value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var operand))
        {
            return null;
        }
        return new NumericCondition(match.Groups[1].Value, operand);
    }

    private static bool IsDateTimeFormat(string formatCode)
    {
        var visible = new StringBuilder(formatCode.Length);
        var quoted = false;
        for (var index = 0; index < formatCode.Length; index++)
        {
            var character = formatCode[index];
            if (character == '"')
            {
                quoted = !quoted;
                continue;
            }
            if (quoted)
            {
                continue;
            }
            if (character == '\\' || character is '_' or '*')
            {
                index++;
                continue;
            }
            if (character == '[')
            {
                var end = formatCode.IndexOf(']', index + 1);
                if (end < 0)
                {
                    break;
                }
                var directive = formatCode[(index + 1)..end];
                if (directive.All(static item =>
                        char.ToLowerInvariant(item) is 'h' or 'm' or 's'))
                {
                    visible.Append(directive);
                }
                index = end;
                continue;
            }
            visible.Append(character);
        }

        var normalized = visible.ToString().ToLowerInvariant();
        return normalized.Contains("am/pm", StringComparison.Ordinal) ||
               normalized.Contains("a/p", StringComparison.Ordinal) ||
               normalized.IndexOfAny(['y', 'm', 'd', 'h', 's']) >= 0;
    }

    [GeneratedRegex(
        @"\[(<=|>=|<>|=|<|>)([-+]?(?:\d+(?:\.\d*)?|\.\d+)(?:[Ee][-+]?\d+)?)\]",
        RegexOptions.CultureInvariant)]
    private static partial Regex NumericConditionRegex();

    private readonly record struct NumericCondition(string Operator, double Operand)
    {
        public bool Matches(double value) => Operator switch
        {
            "<" => value < Operand,
            "<=" => value <= Operand,
            ">" => value > Operand,
            ">=" => value >= Operand,
            "=" => value == Operand,
            "<>" => value != Operand,
            _ => false,
        };
    }
}
