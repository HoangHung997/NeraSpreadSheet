using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using System.Globalization;

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
    SpreadsheetAutoFilterRichCriterion? RichCriterion);

/// <summary>Parses the compact, common rich-filter editor used by every native host.</summary>
public static class SpreadsheetAutoFilterCriterionParser
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
                dateGroups: [ToDayGroup(ParseDate(text))])),
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
        var separator = text.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0 &&
            Enum.TryParse<TableFilterComparisonOperator>(text, true, out var blankComparison) &&
            blankComparison is TableFilterComparisonOperator.IsBlank or
                TableFilterComparisonOperator.IsNotBlank)
        {
            return Custom(blankComparison, CellValue.Blank);
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
        return Custom(comparison, value);
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
}

internal static class SpreadsheetAutoFilterRichProjection
{
    public static IReadOnlyList<SpreadsheetAutoFilterMenuKind> GetMenuKinds(
        IEnumerable<CellValue> values)
    {
        var hasText = false;
        var hasNumber = false;
        var hasDate = false;
        foreach (var value in values)
        {
            hasText |= value.Kind == CellValueKind.Text;
            hasNumber |= value.Kind == CellValueKind.Number;
            hasDate |= value.Kind == CellValueKind.DateTime;
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
        if (hasDate) result.Add(SpreadsheetAutoFilterMenuKind.Dynamic);
        return result;
    }

    public static SpreadsheetAutoFilterDatePage CaptureDatePage(
        IEnumerable<KeyValuePair<CellValue, int>> values,
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
        var counts = new Dictionary<(int Year, int? Month, int? Day), int>();
        var index = 0;
        foreach (var pair in values)
        {
            if ((index++ & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (pair.Key.Kind != CellValueKind.DateTime) continue;
            var date = (DateTime)pair.Key.RawValue!;
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
}
