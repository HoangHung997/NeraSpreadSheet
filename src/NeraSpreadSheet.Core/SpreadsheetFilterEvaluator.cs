namespace NeraSpreadSheet.Core;

internal static class SpreadsheetFilterDate
{
    public static bool TryGetDate(
        CellValue value,
        ExcelDateSystem dateSystem,
        out DateTime date)
    {
        if (value.Kind == CellValueKind.DateTime)
        {
            date = (DateTime)value.RawValue!;
            return true;
        }
        if (value.Kind == CellValueKind.Number)
        {
            try
            {
                var serial = (double)value.RawValue!;
                date = dateSystem == ExcelDateSystem.Date1904
                    ? new DateTime(1904, 1, 1).AddDays(serial)
                    : DateTime.FromOADate(serial < 60d ? serial + 1d : serial);
                return true;
            }
            catch (ArgumentException)
            {
                date = default;
                return false;
            }
        }

        date = default;
        return false;
    }
}

internal static class SpreadsheetFilterEvaluator
{
    public static Func<int, bool> CreateRowPredicate(
        WorksheetSnapshot worksheet,
        CellRange dataRange,
        int worksheetColumnIndex,
        TableFilterColumn filter)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(filter);
        var requiresNumericAggregate = filter.TopBottom is not null ||
            filter.IconFilter is not null ||
            filter.DynamicFilter?.Type is SpreadsheetDynamicFilterType.AboveAverage or
                SpreadsheetDynamicFilterType.BelowAverage;
        var numeric = requiresNumericAggregate
            ? EnumerateNumeric(worksheet, dataRange, worksheetColumnIndex).ToArray()
            : [];
        double? threshold = null;
        if (filter.TopBottom is { } topBottom && numeric.Length > 0)
        {
            var count = topBottom.Percent
                ? Math.Max(1, (int)Math.Ceiling(numeric.Length * Math.Min(100d, topBottom.Value) / 100d))
                : Math.Min(numeric.Length, Math.Max(1, (int)Math.Ceiling(topBottom.Value)));
            var ordered = topBottom.Top
                ? numeric.OrderByDescending(static value => value)
                : numeric.OrderBy(static value => value);
            threshold = ordered.ElementAt(count - 1);
        }
        var average = filter.DynamicFilter?.Type is
            SpreadsheetDynamicFilterType.AboveAverage or SpreadsheetDynamicFilterType.BelowAverage && numeric.Length > 0
                ? numeric.Average()
                : (double?)null;
        var minimum = filter.IconFilter is not null && numeric.Length > 0
            ? numeric.Min()
            : (double?)null;
        var maximum = filter.IconFilter is not null && numeric.Length > 0
            ? numeric.Max()
            : (double?)null;

        return rowIndex =>
        {
            var address = new CellAddress(rowIndex, worksheetColumnIndex);
            var value = worksheet.GetCell(address).Value;
            if (filter.TopBottom is { } top)
            {
                return threshold is not null && TryGetNumeric(value, out var number) &&
                       (top.Top ? number >= threshold : number <= threshold);
            }
            if (filter.DynamicFilter is { } dynamicFilter)
            {
                return MatchesDynamic(value, dynamicFilter, worksheet.DateSystem, average);
            }
            if (filter.ColorFilter is { } color)
            {
                var style = worksheet.GetEffectiveStyle(address);
                return color.Kind == SpreadsheetFilterColorKind.Fill
                    ? style.Fill.IsVisible && style.Fill.Color == color.Color
                    : style.Font.Color == color.Color;
            }
            if (filter.IconFilter is { } icon)
            {
                if (minimum is null || maximum is null || !TryGetNumeric(value, out var number))
                {
                    return false;
                }
                var iconCount = InferIconCount(icon.IconSet);
                var normalized = maximum == minimum ? 1d : (number - minimum.Value) / (maximum.Value - minimum.Value);
                var iconId = Math.Min(iconCount - 1U, (uint)Math.Floor(normalized * iconCount));
                return iconId == icon.IconId;
            }
            if (filter.DateGroups.Count > 0)
            {
                if (value.IsBlank)
                {
                    return filter.IncludeBlank || filter.Values.Any(static candidate => candidate.IsBlank);
                }
                if (filter.Values.Any(candidate => TableValueComparer.Compare(candidate, value) == 0))
                {
                    return true;
                }
                return SpreadsheetFilterDate.TryGetDate(value, worksheet.DateSystem, out var date) &&
                       filter.DateGroups.Any(group => group.Matches(date));
            }
            return filter.Matches(value, worksheet.DateSystem);
        };
    }

    public static bool MatchesDynamic(
        CellValue value,
        SpreadsheetDynamicFilter filter,
        ExcelDateSystem dateSystem,
        double? aggregateAverage)
    {
        if (filter.Type is SpreadsheetDynamicFilterType.AboveAverage or SpreadsheetDynamicFilterType.BelowAverage)
        {
            return aggregateAverage is not null && TryGetNumeric(value, out var number) &&
                   (filter.Type == SpreadsheetDynamicFilterType.AboveAverage
                       ? number > aggregateAverage
                       : number < aggregateAverage);
        }
        if (!SpreadsheetFilterDate.TryGetDate(value, dateSystem, out var candidate))
        {
            return false;
        }

        var reference = (filter.ReferenceDate ?? DateTime.Today).Date;
        var date = candidate.Date;
        var (start, end) = GetDynamicWindow(filter.Type, reference);
        return date >= start && date < end;
    }

    private static IEnumerable<double> EnumerateNumeric(
        WorksheetSnapshot worksheet,
        CellRange range,
        int columnIndex)
    {
        for (var row = range.Top; row <= range.Bottom; row++)
        {
            if (TryGetNumeric(worksheet.GetCell(new CellAddress(row, columnIndex)).Value, out var value))
            {
                yield return value;
            }
        }
    }

    private static bool TryGetNumeric(CellValue value, out double number)
    {
        if (value.Kind == CellValueKind.Number)
        {
            number = (double)value.RawValue!;
            return double.IsFinite(number);
        }
        if (value.Kind == CellValueKind.DateTime)
        {
            number = ((DateTime)value.RawValue!).ToOADate();
            return true;
        }
        number = 0d;
        return false;
    }

    private static uint InferIconCount(string iconSet) =>
        iconSet.Length > 0 && iconSet[0] is '4' or '5'
            ? (uint)(iconSet[0] - '0')
            : 3U;

    private static (DateTime Start, DateTime End) GetDynamicWindow(
        SpreadsheetDynamicFilterType type,
        DateTime reference)
    {
        var monday = reference.AddDays(-(((int)reference.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7));
        var month = new DateTime(reference.Year, reference.Month, 1);
        var quarter = new DateTime(reference.Year, (((reference.Month - 1) / 3) * 3) + 1, 1);
        return type switch
        {
            SpreadsheetDynamicFilterType.Today => (reference, reference.AddDays(1)),
            SpreadsheetDynamicFilterType.Yesterday => (reference.AddDays(-1), reference),
            SpreadsheetDynamicFilterType.Tomorrow => (reference.AddDays(1), reference.AddDays(2)),
            SpreadsheetDynamicFilterType.ThisWeek => (monday, monday.AddDays(7)),
            SpreadsheetDynamicFilterType.LastWeek => (monday.AddDays(-7), monday),
            SpreadsheetDynamicFilterType.NextWeek => (monday.AddDays(7), monday.AddDays(14)),
            SpreadsheetDynamicFilterType.ThisMonth => (month, month.AddMonths(1)),
            SpreadsheetDynamicFilterType.LastMonth => (month.AddMonths(-1), month),
            SpreadsheetDynamicFilterType.NextMonth => (month.AddMonths(1), month.AddMonths(2)),
            SpreadsheetDynamicFilterType.ThisQuarter => (quarter, quarter.AddMonths(3)),
            SpreadsheetDynamicFilterType.LastQuarter => (quarter.AddMonths(-3), quarter),
            SpreadsheetDynamicFilterType.NextQuarter => (quarter.AddMonths(3), quarter.AddMonths(6)),
            SpreadsheetDynamicFilterType.ThisYear => (new(reference.Year, 1, 1), new(reference.Year + 1, 1, 1)),
            SpreadsheetDynamicFilterType.LastYear => (new(reference.Year - 1, 1, 1), new(reference.Year, 1, 1)),
            SpreadsheetDynamicFilterType.NextYear => (new(reference.Year + 1, 1, 1), new(reference.Year + 2, 1, 1)),
            SpreadsheetDynamicFilterType.YearToDate => (new(reference.Year, 1, 1), reference.AddDays(1)),
            SpreadsheetDynamicFilterType.Quarter1 => MonthWindow(reference.Year, 1, 3),
            SpreadsheetDynamicFilterType.Quarter2 => MonthWindow(reference.Year, 4, 3),
            SpreadsheetDynamicFilterType.Quarter3 => MonthWindow(reference.Year, 7, 3),
            SpreadsheetDynamicFilterType.Quarter4 => MonthWindow(reference.Year, 10, 3),
            >= SpreadsheetDynamicFilterType.January and <= SpreadsheetDynamicFilterType.December =>
                MonthWindow(reference.Year, type - SpreadsheetDynamicFilterType.January + 1, 1),
            _ => (DateTime.MaxValue, DateTime.MinValue),
        };
    }

    private static (DateTime Start, DateTime End) MonthWindow(int year, int month, int count)
    {
        var start = new DateTime(year, month, 1);
        return (start, start.AddMonths(count));
    }
}
