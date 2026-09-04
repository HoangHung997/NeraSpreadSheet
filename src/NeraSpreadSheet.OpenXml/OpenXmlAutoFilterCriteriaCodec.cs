using System.Globalization;
using System.Text;
using System.Xml.Linq;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.OpenXml;

internal sealed record ParsedFilterCriteria(
    CellValue[] Values,
    bool IncludeBlank,
    TableFilterCondition? FirstCondition,
    TableFilterCondition? SecondCondition,
    bool CombineWithAnd,
    SpreadsheetFilterDateGroup[] DateGroups,
    SpreadsheetTopBottomFilter? TopBottom,
    SpreadsheetDynamicFilter? DynamicFilter,
    SpreadsheetColorFilter? ColorFilter,
    SpreadsheetIconFilter? IconFilter);

internal static class OpenXmlAutoFilterCriteriaCodec
{
    private const int MaxFilterValuesPerColumn = 100_000;
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    public static ParsedFilterCriteria Parse(
        XElement filterColumn,
        Func<uint, bool, SpreadsheetColorFilter> resolveColor)
    {
        var children = filterColumn.Elements().Where(element => element.Name != Ns + "extLst").ToArray();
        if (children.Length != 1)
        {
            throw new InvalidDataException("A filterColumn requires exactly one filter definition.");
        }
        var definition = children[0];
        if (definition.Name == Ns + "filters") return ParseValues(definition);
        if (definition.Name == Ns + "customFilters") return ParseCustom(definition);
        if (definition.Name == Ns + "top10")
        {
            EnsureOnlyAttributes(definition, "top", "percent", "val");
            var value = ReadDouble(definition, "val", required: true)!.Value;
            return Empty(topBottom: new SpreadsheetTopBottomFilter(
                ReadBoolean(definition, "top", true),
                ReadBoolean(definition, "percent", false),
                value));
        }
        if (definition.Name == Ns + "dynamicFilter")
        {
            EnsureOnlyAttributes(definition, "type", "val", "maxVal");
            var type = ParseDynamic(Required(definition, "type"));
            return Empty(dynamicFilter: new SpreadsheetDynamicFilter(
                type,
                ReadDouble(definition, "val", false),
                ReadDouble(definition, "maxVal", false)));
        }
        if (definition.Name == Ns + "colorFilter")
        {
            EnsureOnlyAttributes(definition, "dxfId", "cellColor");
            var dxfId = ReadUInt(definition, "dxfId");
            var cellColor = ReadBoolean(definition, "cellColor", true);
            return Empty(colorFilter: resolveColor(dxfId, cellColor));
        }
        if (definition.Name == Ns + "iconFilter")
        {
            EnsureOnlyAttributes(definition, "iconSet", "iconId");
            return Empty(iconFilter: new SpreadsheetIconFilter(
                Required(definition, "iconSet"),
                ReadUInt(definition, "iconId")));
        }
        throw new InvalidDataException($"Unsupported AutoFilter type '{definition.Name.LocalName}'.");
    }

    public static XElement Build(
        TableFilterColumn filter,
        Func<SpreadsheetColorFilter, uint> getColorStyleId)
    {
        if (filter.TopBottom is { } top)
        {
            return new XElement(Ns + "top10",
                new XAttribute("top", top.Top ? 1 : 0),
                new XAttribute("percent", top.Percent ? 1 : 0),
                new XAttribute("val", top.Value.ToString("R", CultureInfo.InvariantCulture)));
        }
        if (filter.DynamicFilter is { } dynamicFilter)
        {
            var result = new XElement(Ns + "dynamicFilter", new XAttribute("type", FormatDynamic(dynamicFilter.Type)));
            if (dynamicFilter.Value is double value) result.Add(new XAttribute("val", value.ToString("R", CultureInfo.InvariantCulture)));
            if (dynamicFilter.MaximumValue is double maximum) result.Add(new XAttribute("maxVal", maximum.ToString("R", CultureInfo.InvariantCulture)));
            return result;
        }
        if (filter.ColorFilter is { } color)
        {
            return new XElement(Ns + "colorFilter",
                new XAttribute("dxfId", getColorStyleId(color)),
                new XAttribute("cellColor", color.Kind == SpreadsheetFilterColorKind.Fill ? 1 : 0));
        }
        if (filter.IconFilter is { } icon)
        {
            return new XElement(Ns + "iconFilter", new XAttribute("iconSet", icon.IconSet), new XAttribute("iconId", icon.IconId));
        }
        if (filter.Values.Count > 0 || filter.DateGroups.Count > 0 || filter.IncludeBlank)
        {
            var values = new XElement(Ns + "filters");
            if (filter.IncludeBlank || filter.Values.Any(static value => value.IsBlank)) values.Add(new XAttribute("blank", 1));
            foreach (var value in filter.Values.Where(static value => !value.IsBlank))
            {
                values.Add(new XElement(Ns + "filter", new XAttribute("val", FormatValue(value))));
            }
            foreach (var group in filter.DateGroups) values.Add(BuildDateGroup(group));
            return values;
        }

        var custom = new XElement(Ns + "customFilters");
        if (filter.SecondCondition is not null && filter.CombineWithAnd) custom.Add(new XAttribute("and", 1));
        custom.Add(BuildCustom(filter.FirstCondition ?? throw new InvalidOperationException("A custom filter is missing its first condition.")));
        if (filter.SecondCondition is not null) custom.Add(BuildCustom(filter.SecondCondition));
        return custom;
    }

    public static SpreadsheetFilterSortState? ParseSortState(
        XElement autoFilter,
        CellRange ownerRange,
        Func<uint, bool, SpreadsheetColorFilter> resolveColor)
    {
        var elements = autoFilter.Elements(Ns + "sortState").ToArray();
        if (elements.Length == 0) return null;
        if (elements.Length > 1) throw new InvalidDataException("An AutoFilter cannot contain duplicate sortState elements.");
        var element = elements[0];
        EnsureOnlyAttributes(element, "ref", "caseSensitive", "columnSort");
        if (ReadBoolean(element, "columnSort", false))
        {
            throw new InvalidDataException(
                "Left-to-right AutoFilter sort state is not supported yet.");
        }
        var conditions = element.Elements(Ns + "sortCondition").Select(condition =>
        {
            EnsureOnlyAttributes(
                condition,
                "ref",
                "descending",
                "sortBy",
                "customList",
                "dxfId",
                "iconSet",
                "iconId");
            var reference = ParseRange(Required(condition, "ref"));
            if (reference.Left != reference.Right || reference.Left < ownerRange.Left || reference.Left > ownerRange.Right)
                throw new InvalidDataException("An AutoFilter sort condition must reference one owner column.");
            var sortBy = ((string?)condition.Attribute("sortBy") ?? "value") switch
            {
                "value" => SpreadsheetFilterSortBy.Value,
                "cellColor" => SpreadsheetFilterSortBy.CellColor,
                "fontColor" => SpreadsheetFilterSortBy.FontColor,
                "icon" => SpreadsheetFilterSortBy.Icon,
                var value => throw new InvalidDataException($"Unsupported sortBy value '{value}'."),
            };
            SpreadsheetColorFilter? color = null;
            SpreadsheetIconFilter? icon = null;
            if (sortBy is SpreadsheetFilterSortBy.CellColor or SpreadsheetFilterSortBy.FontColor)
                color = resolveColor(ReadUInt(condition, "dxfId"), sortBy == SpreadsheetFilterSortBy.CellColor);
            if (sortBy == SpreadsheetFilterSortBy.Icon)
                icon = new SpreadsheetIconFilter(Required(condition, "iconSet"), ReadUInt(condition, "iconId"));
            return new SpreadsheetFilterSortCondition(
                reference.Left - ownerRange.Left,
                ReadBoolean(condition, "descending", false),
                sortBy,
                (string?)condition.Attribute("customList"),
                color,
                icon);
        }).ToArray();
        return conditions.Length == 0 ? null : new SpreadsheetFilterSortState(
            conditions,
            ReadBoolean(element, "caseSensitive", false),
            ReadBoolean(element, "columnSort", false));
    }

    public static XElement? BuildSortState(
        SpreadsheetFilterSortState? state,
        CellRange dataRange,
        Func<SpreadsheetColorFilter, uint> getColorStyleId)
    {
        if (state is null) return null;
        if (state.SortLeftToRight)
        {
            throw new NotSupportedException(
                "Left-to-right AutoFilter sort state is not supported yet.");
        }
        var result = new XElement(Ns + "sortState", new XAttribute("ref", ToRange(dataRange)));
        if (state.CaseSensitive) result.Add(new XAttribute("caseSensitive", 1));
        if (state.SortLeftToRight) result.Add(new XAttribute("columnSort", 1));
        foreach (var condition in state.Conditions)
        {
            var column = dataRange.Left + condition.ColumnOffset;
            var item = new XElement(Ns + "sortCondition",
                new XAttribute("ref", ToRange(new CellRange(new CellAddress(dataRange.Top, column), new CellAddress(dataRange.Bottom, column)))));
            if (condition.Descending) item.Add(new XAttribute("descending", 1));
            if (condition.SortBy != SpreadsheetFilterSortBy.Value) item.Add(new XAttribute("sortBy", FormatSortBy(condition.SortBy)));
            if (condition.CustomList is not null) item.Add(new XAttribute("customList", condition.CustomList));
            if (condition.Color is { } color) item.Add(new XAttribute("dxfId", getColorStyleId(color)));
            if (condition.Icon is { } icon)
            {
                item.Add(new XAttribute("iconSet", icon.IconSet));
                item.Add(new XAttribute("iconId", icon.IconId));
            }
            result.Add(item);
        }
        return result;
    }

    private static ParsedFilterCriteria ParseValues(XElement element)
    {
        EnsureOnlyAttributes(element, "blank");
        var values = element.Elements(Ns + "filter").Select(item =>
        {
            EnsureOnlyAttributes(item, "val");
            return ParseValue(RequiredAllowEmpty(item, "val"));
        }).ToArray();
        var groups = element.Elements(Ns + "dateGroupItem").Select(ParseDateGroup).ToArray();
        if (values.Length + groups.Length > MaxFilterValuesPerColumn) throw new InvalidDataException("The value-filter collection is too large.");
        if (element.Elements().Any(item => item.Name != Ns + "filter" && item.Name != Ns + "dateGroupItem"))
            throw new InvalidDataException("The value-filter collection contains unsupported markup.");
        var blank = ReadBoolean(element, "blank", false);
        if (values.Length == 0 && groups.Length == 0 && !blank) throw new InvalidDataException("A value filter requires a value, date group, or blank matching.");
        var effectiveValues = values.Length == 0 && groups.Length == 0 && blank
            ? [CellValue.Blank]
            : values;
        return new ParsedFilterCriteria(effectiveValues, blank, null, null, true, groups, null, null, null, null);
    }

    private static ParsedFilterCriteria ParseCustom(XElement element)
    {
        EnsureOnlyAttributes(element, "and");
        var conditions = element.Elements(Ns + "customFilter").Select(ParseCustomCondition).ToArray();
        if (conditions.Length is < 1 or > 2 || element.Elements().Any(item => item.Name != Ns + "customFilter"))
            throw new InvalidDataException("A custom filter requires one or two conditions.");
        return new ParsedFilterCriteria([], false, conditions[0], conditions.Length == 2 ? conditions[1] : null,
            ReadBoolean(element, "and", false), [], null, null, null, null);
    }

    private static TableFilterCondition ParseCustomCondition(XElement element)
    {
        EnsureOnlyAttributes(element, "operator", "val");
        var op = (string?)element.Attribute("operator") ?? "equal";
        var value = RequiredAllowEmpty(element, "val");
        if (op is "equal" or "notEqual" && value.Length == 0)
            return new(op == "equal" ? TableFilterComparisonOperator.IsBlank : TableFilterComparisonOperator.IsNotBlank, CellValue.Blank);
        if (op is "equal" or "notEqual")
        {
            var pattern = ParseWildcardPattern(value);
            if (pattern.HasWildcard)
            {
                if (op == "notEqual" &&
                    pattern.Operator != TableFilterComparisonOperator.Contains)
                {
                    throw new InvalidDataException(
                        "Negated leading-only or trailing-only wildcard filters are not supported.");
                }
                return new(
                    op == "notEqual"
                        ? TableFilterComparisonOperator.DoesNotContain
                        : pattern.Operator,
                    CellValue.FromText(pattern.Literal));
            }
            return new(
                op == "equal"
                    ? TableFilterComparisonOperator.Equal
                    : TableFilterComparisonOperator.NotEqual,
                pattern.HadEscape
                    ? CellValue.FromText(pattern.Literal)
                    : ParseValue(pattern.Literal));
        }
        return new(op switch
        {
            "greaterThan" => TableFilterComparisonOperator.GreaterThan,
            "greaterThanOrEqual" => TableFilterComparisonOperator.GreaterThanOrEqual,
            "lessThan" => TableFilterComparisonOperator.LessThan,
            "lessThanOrEqual" => TableFilterComparisonOperator.LessThanOrEqual,
            _ => throw new InvalidDataException($"Unsupported custom-filter operator '{op}'."),
        }, ParseValue(value));
    }

    private static XElement BuildCustom(TableFilterCondition condition)
    {
        var (op, value) = condition.Operator switch
        {
            TableFilterComparisonOperator.Equal => ("equal", FormatCustomLiteral(condition.Value)),
            TableFilterComparisonOperator.NotEqual => ("notEqual", FormatCustomLiteral(condition.Value)),
            TableFilterComparisonOperator.GreaterThan => ("greaterThan", FormatValue(condition.Value)),
            TableFilterComparisonOperator.GreaterThanOrEqual => ("greaterThanOrEqual", FormatValue(condition.Value)),
            TableFilterComparisonOperator.LessThan => ("lessThan", FormatValue(condition.Value)),
            TableFilterComparisonOperator.LessThanOrEqual => ("lessThanOrEqual", FormatValue(condition.Value)),
            TableFilterComparisonOperator.BeginsWith => ("equal", Wildcard(condition.Value, false, true)),
            TableFilterComparisonOperator.EndsWith => ("equal", Wildcard(condition.Value, true, false)),
            TableFilterComparisonOperator.Contains => ("equal", Wildcard(condition.Value, true, true)),
            TableFilterComparisonOperator.DoesNotContain => ("notEqual", Wildcard(condition.Value, true, true)),
            TableFilterComparisonOperator.IsBlank => ("equal", string.Empty),
            TableFilterComparisonOperator.IsNotBlank => ("notEqual", string.Empty),
            _ => throw new InvalidOperationException($"Filter operator '{condition.Operator}' cannot be represented as customFilters."),
        };
        return new XElement(Ns + "customFilter", new XAttribute("operator", op), new XAttribute("val", value));
    }

    private static SpreadsheetFilterDateGroup ParseDateGroup(XElement item)
    {
        EnsureOnlyAttributes(
            item,
            "year",
            "month",
            "day",
            "hour",
            "minute",
            "second",
            "dateTimeGrouping");
        var grouping = Required(item, "dateTimeGrouping") switch
        {
            "year" => SpreadsheetFilterDateGrouping.Year,
            "month" => SpreadsheetFilterDateGrouping.Month,
            "day" => SpreadsheetFilterDateGrouping.Day,
            "hour" => SpreadsheetFilterDateGrouping.Hour,
            "minute" => SpreadsheetFilterDateGrouping.Minute,
            "second" => SpreadsheetFilterDateGrouping.Second,
            var value => throw new InvalidDataException($"Unsupported date grouping '{value}'."),
        };
        return new SpreadsheetFilterDateGroup((int)ReadUInt(item, "year"), grouping,
            OptionalInt(item, "month"), OptionalInt(item, "day"), OptionalInt(item, "hour"), OptionalInt(item, "minute"), OptionalInt(item, "second"));
    }

    private static XElement BuildDateGroup(SpreadsheetFilterDateGroup group)
    {
        var item = new XElement(Ns + "dateGroupItem", new XAttribute("year", group.Year),
            new XAttribute("dateTimeGrouping", group.Grouping.ToString().ToLowerInvariant()));
        Add(item, "month", group.Month); Add(item, "day", group.Day); Add(item, "hour", group.Hour);
        Add(item, "minute", group.Minute); Add(item, "second", group.Second);
        return item;
    }

    private static ParsedFilterCriteria Empty(SpreadsheetTopBottomFilter? topBottom = null, SpreadsheetDynamicFilter? dynamicFilter = null,
        SpreadsheetColorFilter? colorFilter = null, SpreadsheetIconFilter? iconFilter = null) =>
        new([], false, null, null, true, [], topBottom, dynamicFilter, colorFilter, iconFilter);

    private static SpreadsheetDynamicFilterType ParseDynamic(string value) => value switch
    {
        "Q1" => SpreadsheetDynamicFilterType.Quarter1,
        "Q2" => SpreadsheetDynamicFilterType.Quarter2,
        "Q3" => SpreadsheetDynamicFilterType.Quarter3,
        "Q4" => SpreadsheetDynamicFilterType.Quarter4,
        "M1" => SpreadsheetDynamicFilterType.January,
        "M2" => SpreadsheetDynamicFilterType.February,
        "M3" => SpreadsheetDynamicFilterType.March,
        "M4" => SpreadsheetDynamicFilterType.April,
        "M5" => SpreadsheetDynamicFilterType.May,
        "M6" => SpreadsheetDynamicFilterType.June,
        "M7" => SpreadsheetDynamicFilterType.July,
        "M8" => SpreadsheetDynamicFilterType.August,
        "M9" => SpreadsheetDynamicFilterType.September,
        "M10" => SpreadsheetDynamicFilterType.October,
        "M11" => SpreadsheetDynamicFilterType.November,
        "M12" => SpreadsheetDynamicFilterType.December,
        _ when Enum.TryParse<SpreadsheetDynamicFilterType>(value, true, out var parsed) => parsed,
        _ => throw new InvalidDataException($"Unsupported dynamic-filter type '{value}'."),
    };
    private static string FormatDynamic(SpreadsheetDynamicFilterType value) => value switch
    {
        SpreadsheetDynamicFilterType.Quarter1 => "Q1",
        SpreadsheetDynamicFilterType.Quarter2 => "Q2",
        SpreadsheetDynamicFilterType.Quarter3 => "Q3",
        SpreadsheetDynamicFilterType.Quarter4 => "Q4",
        >= SpreadsheetDynamicFilterType.January and <= SpreadsheetDynamicFilterType.December =>
            $"M{(int)(value - SpreadsheetDynamicFilterType.January) + 1}",
        _ => char.ToLowerInvariant(value.ToString()[0]) + value.ToString()[1..],
    };
    private static string FormatSortBy(SpreadsheetFilterSortBy value) => value switch
    { SpreadsheetFilterSortBy.CellColor => "cellColor", SpreadsheetFilterSortBy.FontColor => "fontColor", SpreadsheetFilterSortBy.Icon => "icon", _ => "value" };
    private static CellValue ParseValue(string value) => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number)
        ? CellValue.FromNumber(number) : bool.TryParse(value, out var boolean) ? CellValue.FromBoolean(boolean) : CellValue.FromText(value);
    private static string FormatValue(CellValue value) => value.Kind switch
    {
        CellValueKind.Blank => "",
        CellValueKind.Number => ((double)value.RawValue!).ToString("R", CultureInfo.InvariantCulture),
        CellValueKind.Boolean => (bool)value.RawValue! ? "1" : "0",
        CellValueKind.DateTime => ((DateTime)value.RawValue!).ToOADate().ToString("R", CultureInfo.InvariantCulture),
        _ => value.ToString()
    };
    private static string FormatCustomLiteral(CellValue value) =>
        value.Kind == CellValueKind.Text
            ? EscapeWildcardLiteral((string)value.RawValue!)
            : FormatValue(value);
    private static string Required(XElement element, string name) { var value = (string?)element.Attribute(name); return string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException($"Required attribute '{name}' is missing.") : value; }
    private static string RequiredAllowEmpty(XElement element, string name) => element.Attribute(name)?.Value ?? throw new InvalidDataException($"Required attribute '{name}' is missing.");
    private static uint ReadUInt(XElement element, string name) => uint.TryParse(Required(element, name), NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : throw new InvalidDataException($"Attribute '{name}' is not an unsigned integer.");
    private static int? OptionalInt(XElement element, string name) => element.Attribute(name) is { } attribute && int.TryParse(attribute.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : null;
    private static double? ReadDouble(XElement element, string name, bool required) { var text = (string?)element.Attribute(name); if (text is null && !required) return null; return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : throw new InvalidDataException($"Attribute '{name}' is not a finite number."); }
    private static bool ReadBoolean(XElement element, string name, bool fallback) => (string?)element.Attribute(name) switch { null => fallback, "1" or "true" => true, "0" or "false" => false, _ => throw new InvalidDataException($"Attribute '{name}' is not a boolean.") };
    private static void Add(XElement element, string name, int? value) { if (value is not null) element.Add(new XAttribute(name, value.Value)); }
    private static WildcardPattern ParseWildcardPattern(string pattern)
    {
        var tokens = new List<(char Value, bool IsWildcard)>();
        var hadEscape = false;
        for (var index = 0; index < pattern.Length; index++)
        {
            var value = pattern[index];
            if (value == '~' && index + 1 < pattern.Length &&
                pattern[index + 1] is '~' or '*' or '?')
            {
                hadEscape = true;
                tokens.Add((pattern[++index], false));
                continue;
            }
            if (value == '?')
            {
                throw new InvalidDataException(
                    "Single-character wildcard filters are not supported.");
            }
            tokens.Add((value, value == '*'));
        }

        var wildcardIndexes = tokens
            .Select((token, index) => (token, index))
            .Where(static item => item.token.IsWildcard)
            .Select(static item => item.index)
            .ToArray();
        if (wildcardIndexes.Length == 0)
        {
            return new WildcardPattern(
                false,
                hadEscape,
                default,
                new string(tokens.Select(static token => token.Value).ToArray()));
        }
        if (wildcardIndexes.Length > 2 ||
            wildcardIndexes.Any(index => index != 0 && index != tokens.Count - 1))
        {
            throw new InvalidDataException(
                "Only leading and trailing multi-character wildcards are supported.");
        }

        var leading = wildcardIndexes.Contains(0);
        var trailing = wildcardIndexes.Contains(tokens.Count - 1);
        var literal = new string(tokens
            .Where(static token => !token.IsWildcard)
            .Select(static token => token.Value)
            .ToArray());
        var @operator = leading && trailing
            ? TableFilterComparisonOperator.Contains
            : leading
                ? TableFilterComparisonOperator.EndsWith
                : TableFilterComparisonOperator.BeginsWith;
        return new WildcardPattern(true, hadEscape, @operator, literal);
    }

    private static string EscapeWildcardLiteral(string value)
    {
        var result = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (character is '~' or '*' or '?')
            {
                result.Append('~');
            }
            result.Append(character);
        }
        return result.ToString();
    }

    private static void EnsureOnlyAttributes(
        XElement element,
        params string[] allowedNames)
    {
        var allowed = allowedNames.ToHashSet(StringComparer.Ordinal);
        var unsupported = element.Attributes().FirstOrDefault(attribute =>
            !attribute.IsNamespaceDeclaration &&
            attribute.Name.Namespace == XNamespace.None &&
            !allowed.Contains(attribute.Name.LocalName));
        if (unsupported is not null)
        {
            throw new InvalidDataException(
                $"Unsupported '{element.Name.LocalName}' attribute '{unsupported.Name.LocalName}'.");
        }
    }

    private readonly record struct WildcardPattern(
        bool HasWildcard,
        bool HadEscape,
        TableFilterComparisonOperator Operator,
        string Literal);
    private static string Wildcard(CellValue value, bool leading, bool trailing) { var text = value.Kind == CellValueKind.Text ? (string)value.RawValue! : value.ToString(); text = text.Replace("~", "~~", StringComparison.Ordinal).Replace("*", "~*", StringComparison.Ordinal).Replace("?", "~?", StringComparison.Ordinal); return (leading ? "*" : "") + text + (trailing ? "*" : ""); }
    private static CellRange ParseRange(string text) { var parts = text.Split(':'); if (parts.Length is < 1 or > 2 || !CellAddress.TryParseA1(parts[0], out var first) || !CellAddress.TryParseA1(parts[^1], out var second) || first.RowIndex > second.RowIndex || first.ColumnIndex > second.ColumnIndex) throw new InvalidDataException($"'{text}' is not a valid range."); return new(first, second); }
    private static string ToRange(CellRange range) => $"{range.TopLeft.ToA1()}:{range.BottomRight.ToA1()}";
}
