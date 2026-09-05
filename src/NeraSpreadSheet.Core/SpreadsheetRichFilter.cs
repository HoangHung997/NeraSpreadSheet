using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core;

/// <summary>Identifies the precision represented by an AutoFilter date-group item.</summary>
public enum SpreadsheetFilterDateGrouping
{
    Year = 0,
    Month,
    Day,
    Hour,
    Minute,
    Second,
}

/// <summary>Represents one SpreadsheetML date-group selection.</summary>
public sealed record SpreadsheetFilterDateGroup
{
    /// <summary>Creates a validated date-group item at the requested precision.</summary>
    public SpreadsheetFilterDateGroup(
        int year,
        SpreadsheetFilterDateGrouping grouping = SpreadsheetFilterDateGrouping.Year,
        int? month = null,
        int? day = null,
        int? hour = null,
        int? minute = null,
        int? second = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(year, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(year, 9999);
        ValidatePart(month, 1, 12, nameof(month));
        ValidatePart(day, 1, 31, nameof(day));
        ValidatePart(hour, 0, 23, nameof(hour));
        ValidatePart(minute, 0, 59, nameof(minute));
        ValidatePart(second, 0, 59, nameof(second));
        var required = grouping switch
        {
            SpreadsheetFilterDateGrouping.Year => 0,
            SpreadsheetFilterDateGrouping.Month => 1,
            SpreadsheetFilterDateGrouping.Day => 2,
            SpreadsheetFilterDateGrouping.Hour => 3,
            SpreadsheetFilterDateGrouping.Minute => 4,
            SpreadsheetFilterDateGrouping.Second => 5,
            _ => throw new ArgumentOutOfRangeException(nameof(grouping)),
        };
        var parts = new int?[] { month, day, hour, minute, second };
        if (parts.Take(required).Any(static value => value is null) ||
            parts.Skip(required).Any(static value => value is not null))
        {
            throw new ArgumentException(
                "Date-group components must exactly match the selected grouping precision.",
                nameof(grouping));
        }
        if (day is int actualDay)
        {
            _ = new DateTime(year, month!.Value, actualDay);
        }

        Year = year;
        Grouping = grouping;
        Month = month;
        Day = day;
        Hour = hour;
        Minute = minute;
        Second = second;
    }

    /// <summary>Gets the required calendar year.</summary>
    public int Year { get; }
    /// <summary>Gets the comparison precision.</summary>
    public SpreadsheetFilterDateGrouping Grouping { get; }
    /// <summary>Gets the optional calendar month.</summary>
    public int? Month { get; }
    /// <summary>Gets the optional day of month.</summary>
    public int? Day { get; }
    /// <summary>Gets the optional hour.</summary>
    public int? Hour { get; }
    /// <summary>Gets the optional minute.</summary>
    public int? Minute { get; }
    /// <summary>Gets the optional second.</summary>
    public int? Second { get; }

    /// <summary>Returns whether a date belongs to this group.</summary>
    public bool Matches(DateTime value) => Grouping switch
    {
        SpreadsheetFilterDateGrouping.Year => value.Year == Year,
        SpreadsheetFilterDateGrouping.Month => value.Year == Year && value.Month == Month,
        SpreadsheetFilterDateGrouping.Day => value.Year == Year && value.Month == Month && value.Day == Day,
        SpreadsheetFilterDateGrouping.Hour => value.Year == Year && value.Month == Month && value.Day == Day && value.Hour == Hour,
        SpreadsheetFilterDateGrouping.Minute => value.Year == Year && value.Month == Month && value.Day == Day && value.Hour == Hour && value.Minute == Minute,
        SpreadsheetFilterDateGrouping.Second => value.Year == Year && value.Month == Month && value.Day == Day && value.Hour == Hour && value.Minute == Minute && value.Second == Second,
        _ => false,
    };

    private static void ValidatePart(int? value, int minimum, int maximum, string name)
    {
        if (value is int actual && (actual < minimum || actual > maximum))
        {
            throw new ArgumentOutOfRangeException(name);
        }
    }
}

/// <summary>Identifies a relative-date or aggregate dynamic AutoFilter.</summary>
public enum SpreadsheetDynamicFilterType
{
    AboveAverage = 0,
    BelowAverage,
    Today,
    Yesterday,
    Tomorrow,
    ThisWeek,
    LastWeek,
    NextWeek,
    ThisMonth,
    LastMonth,
    NextMonth,
    ThisQuarter,
    LastQuarter,
    NextQuarter,
    ThisYear,
    LastYear,
    NextYear,
    YearToDate,
    Quarter1,
    Quarter2,
    Quarter3,
    Quarter4,
    January,
    February,
    March,
    April,
    May,
    June,
    July,
    August,
    September,
    October,
    November,
    December,
}

/// <summary>Describes a dynamic filter. ReferenceDate makes evaluation deterministic when supplied.</summary>
public sealed record SpreadsheetDynamicFilter
{
    /// <summary>Creates a validated dynamic-filter criterion.</summary>
    public SpreadsheetDynamicFilter(
        SpreadsheetDynamicFilterType Type,
        double? Value = null,
        double? MaximumValue = null,
        DateTime? ReferenceDate = null)
    {
        if (!Enum.IsDefined(Type))
        {
            throw new ArgumentOutOfRangeException(nameof(Type));
        }
        if (Value is double value && !double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(Value));
        }
        if (MaximumValue is double maximum && !double.IsFinite(maximum))
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumValue));
        }
        this.Type = Type;
        this.Value = Value;
        this.MaximumValue = MaximumValue;
        this.ReferenceDate = ReferenceDate;
    }

    public SpreadsheetDynamicFilterType Type { get; }
    public double? Value { get; }
    public double? MaximumValue { get; }
    public DateTime? ReferenceDate { get; }
}

/// <summary>Describes a Top/Bottom item or percentage filter.</summary>
public sealed record SpreadsheetTopBottomFilter
{
    /// <summary>Creates a validated Top/Bottom item or percentage filter.</summary>
    public SpreadsheetTopBottomFilter(bool top, bool percent, double value)
    {
        var maximum = percent ? 100d : 500d;
        if (!double.IsFinite(value) || value <= 0d || value > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }
        Top = top;
        Percent = percent;
        Value = value;
    }

    /// <summary>Gets whether the highest rather than lowest values are selected.</summary>
    public bool Top { get; }
    /// <summary>Gets whether <see cref="Value"/> is a percentage.</summary>
    public bool Percent { get; }
    /// <summary>Gets the item count or percentage.</summary>
    public double Value { get; }
}

/// <summary>Identifies whether a color filter compares the cell fill or font.</summary>
public enum SpreadsheetFilterColorKind
{
    Fill = 0,
    Font,
}

/// <summary>Describes a resolved fill or font color filter.</summary>
public sealed record SpreadsheetColorFilter(
    SpreadsheetFilterColorKind Kind,
    ColorRgba Color);

/// <summary>Describes an icon-set member used by an AutoFilter.</summary>
public sealed record SpreadsheetIconFilter
{
    /// <summary>Creates an icon filter for one member of a 3/4/5-icon set.</summary>
    public SpreadsheetIconFilter(string iconSet, uint iconId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(iconSet);
        var normalized = iconSet.Trim();
        if (normalized[0] is not ('3' or '4' or '5'))
        {
            throw new ArgumentException(
                "An icon filter requires a 3-, 4-, or 5-icon SpreadsheetML set.",
                nameof(iconSet));
        }
        var iconCount = checked((uint)(normalized[0] - '0'));
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            iconId,
            iconCount);
        IconSet = normalized;
        IconId = iconId;
    }

    /// <summary>Gets the SpreadsheetML icon-set name.</summary>
    public string IconSet { get; }
    /// <summary>Gets the zero-based icon member identifier.</summary>
    public uint IconId { get; }
}

/// <summary>Identifies the value or visual attribute used by an AutoFilter sort condition.</summary>
public enum SpreadsheetFilterSortBy
{
    Value = 0,
    CellColor,
    FontColor,
    Icon,
}

/// <summary>Identifies the non-color state exposed by an AutoFilter header.</summary>
public enum SpreadsheetFilterHeaderState
{
    None = 0,
    Filtered,
    Sorted,
    FilteredAndSorted,
}

/// <summary>Represents one sort key relative to an AutoFilter range.</summary>
public sealed record SpreadsheetFilterSortCondition
{
    /// <summary>Creates one validated sort key relative to the owner filter range.</summary>
    public SpreadsheetFilterSortCondition(
        int columnOffset,
        bool descending = false,
        SpreadsheetFilterSortBy sortBy = SpreadsheetFilterSortBy.Value,
        string? customList = null,
        SpreadsheetColorFilter? color = null,
        SpreadsheetIconFilter? icon = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(columnOffset);
        if (sortBy is SpreadsheetFilterSortBy.CellColor or SpreadsheetFilterSortBy.FontColor && color is null)
        {
            throw new ArgumentException("A color sort requires a color value.", nameof(color));
        }
        if (sortBy == SpreadsheetFilterSortBy.Icon && icon is null)
        {
            throw new ArgumentException("An icon sort requires an icon value.", nameof(icon));
        }
        ColumnOffset = columnOffset;
        Descending = descending;
        SortBy = sortBy;
        CustomList = string.IsNullOrWhiteSpace(customList) ? null : customList.Trim();
        Color = color;
        Icon = icon;
    }

    /// <summary>Gets the zero-based column offset within the owner filter range.</summary>
    public int ColumnOffset { get; }
    /// <summary>Gets whether this key sorts descending.</summary>
    public bool Descending { get; }
    /// <summary>Gets the value or visual attribute used by the key.</summary>
    public SpreadsheetFilterSortBy SortBy { get; }
    /// <summary>Gets optional producer-compatible custom-list metadata.</summary>
    public string? CustomList { get; }
    /// <summary>Gets the color used by a color sort.</summary>
    public SpreadsheetColorFilter? Color { get; }
    /// <summary>Gets the icon used by an icon sort.</summary>
    public SpreadsheetIconFilter? Icon { get; }
}

/// <summary>Represents SpreadsheetML sort state owned by a Table or worksheet AutoFilter.</summary>
public sealed class SpreadsheetFilterSortState : IEquatable<SpreadsheetFilterSortState>
{
    private readonly SpreadsheetFilterSortCondition[] _conditions;

    /// <summary>Creates sort state from one or more unique column keys.</summary>
    public SpreadsheetFilterSortState(
        IEnumerable<SpreadsheetFilterSortCondition> conditions,
        bool caseSensitive = false,
        bool sortLeftToRight = false)
    {
        ArgumentNullException.ThrowIfNull(conditions);
        if (sortLeftToRight)
        {
            throw new NotSupportedException(
                "Left-to-right filter sorting is reserved for FILTER-007.");
        }
        _conditions = conditions.Select(static condition => condition ?? throw new ArgumentException(
            "A sort state cannot contain a null condition.", nameof(conditions))).ToArray();
        if (_conditions.Length == 0)
        {
            throw new ArgumentException("A sort state requires at least one condition.", nameof(conditions));
        }
        if (_conditions.Select(static item => item.ColumnOffset).Distinct().Count() != _conditions.Length)
        {
            throw new ArgumentException("A sort state cannot contain duplicate column offsets.", nameof(conditions));
        }
        CaseSensitive = caseSensitive;
        SortLeftToRight = sortLeftToRight;
    }

    /// <summary>Gets the ordered sort keys.</summary>
    public IReadOnlyList<SpreadsheetFilterSortCondition> Conditions => _conditions;
    /// <summary>Gets whether text comparisons are case-sensitive.</summary>
    public bool CaseSensitive { get; }
    /// <summary>Gets whether the sort orientation is left-to-right.</summary>
    public bool SortLeftToRight { get; }
    /// <summary>Creates an independent immutable copy.</summary>
    public SpreadsheetFilterSortState Copy() => new(_conditions, CaseSensitive, SortLeftToRight);

    public bool Equals(SpreadsheetFilterSortState? other) => other is not null &&
        CaseSensitive == other.CaseSensitive && SortLeftToRight == other.SortLeftToRight &&
        _conditions.SequenceEqual(other._conditions);
    public override bool Equals(object? obj) => Equals(obj as SpreadsheetFilterSortState);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(CaseSensitive);
        hash.Add(SortLeftToRight);
        foreach (var condition in _conditions) hash.Add(condition);
        return hash.ToHashCode();
    }
}
