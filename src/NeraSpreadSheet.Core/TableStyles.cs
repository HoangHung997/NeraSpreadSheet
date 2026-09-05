using System.Collections.Concurrent;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core;

public enum WorkbookThemeColor
{
    Light1 = 0,
    Dark1,
    Light2,
    Dark2,
    Accent1,
    Accent2,
    Accent3,
    Accent4,
    Accent5,
    Accent6,
    Hyperlink,
    FollowedHyperlink,
}

/// <summary>Defines the workbook color scheme used by theme-aware styles.</summary>
public sealed record WorkbookTheme
{
    public static WorkbookTheme Office { get; } = new();

    public ColorRgba Light1 { get; init; } = ColorRgba.White;
    public ColorRgba Dark1 { get; init; } = ColorRgba.Black;
    public ColorRgba Light2 { get; init; } = new(238, 236, 225);
    public ColorRgba Dark2 { get; init; } = new(31, 73, 125);
    public ColorRgba Accent1 { get; init; } = new(79, 129, 189);
    public ColorRgba Accent2 { get; init; } = new(192, 80, 77);
    public ColorRgba Accent3 { get; init; } = new(155, 187, 89);
    public ColorRgba Accent4 { get; init; } = new(128, 100, 162);
    public ColorRgba Accent5 { get; init; } = new(75, 172, 198);
    public ColorRgba Accent6 { get; init; } = new(247, 150, 70);
    public ColorRgba Hyperlink { get; init; } = new(0, 0, 255);
    public ColorRgba FollowedHyperlink { get; init; } = new(128, 0, 128);

    public ColorRgba GetColor(WorkbookThemeColor color) => color switch
    {
        WorkbookThemeColor.Light1 => Light1,
        WorkbookThemeColor.Dark1 => Dark1,
        WorkbookThemeColor.Light2 => Light2,
        WorkbookThemeColor.Dark2 => Dark2,
        WorkbookThemeColor.Accent1 => Accent1,
        WorkbookThemeColor.Accent2 => Accent2,
        WorkbookThemeColor.Accent3 => Accent3,
        WorkbookThemeColor.Accent4 => Accent4,
        WorkbookThemeColor.Accent5 => Accent5,
        WorkbookThemeColor.Accent6 => Accent6,
        WorkbookThemeColor.Hyperlink => Hyperlink,
        WorkbookThemeColor.FollowedHyperlink => FollowedHyperlink,
        _ => throw new ArgumentOutOfRangeException(nameof(color)),
    };
}

/// <summary>Represents either an RGB color or a workbook-theme color with tint.</summary>
public readonly record struct TableStyleColor
{
    private TableStyleColor(
        ColorRgba? rgb,
        WorkbookThemeColor? themeColor,
        double tint)
    {
        if (!double.IsFinite(tint) || tint is < -1d or > 1d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tint),
                "A table-style tint must be finite and between -1 and 1.");
        }
        if ((rgb is null) == (themeColor is null))
        {
            throw new ArgumentException(
                "A table-style color requires exactly one RGB or theme source.");
        }

        Rgb = rgb;
        ThemeColor = themeColor;
        Tint = tint;
    }

    public ColorRgba? Rgb { get; }
    public WorkbookThemeColor? ThemeColor { get; }
    public double Tint { get; }

    public static TableStyleColor FromRgb(ColorRgba color) =>
        new(color, null, 0d);

    public static TableStyleColor FromTheme(
        WorkbookThemeColor color,
        double tint = 0d) =>
        new(null, color, tint);

    public ColorRgba Resolve(WorkbookTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        var source = Rgb ?? theme.GetColor(ThemeColor!.Value);
        return Tint == 0d ? source : ApplyTint(source, Tint);
    }

    private static ColorRgba ApplyTint(ColorRgba source, double tint)
    {
        RgbToHsl(source, out var hue, out var saturation, out var luminance);
        luminance = tint < 0d
            ? luminance * (1d + tint)
            : luminance + ((1d - luminance) * tint);
        return HslToRgb(hue, saturation, Math.Clamp(luminance, 0d, 1d), source.Alpha);
    }

    private static void RgbToHsl(
        ColorRgba color,
        out double hue,
        out double saturation,
        out double luminance)
    {
        var red = color.Red / 255d;
        var green = color.Green / 255d;
        var blue = color.Blue / 255d;
        var maximum = Math.Max(red, Math.Max(green, blue));
        var minimum = Math.Min(red, Math.Min(green, blue));
        var delta = maximum - minimum;
        luminance = (maximum + minimum) / 2d;
        if (delta == 0d)
        {
            hue = 0d;
            saturation = 0d;
            return;
        }

        saturation = delta / (1d - Math.Abs((2d * luminance) - 1d));
        if (maximum == red)
        {
            hue = 60d * (((green - blue) / delta) % 6d);
        }
        else if (maximum == green)
        {
            hue = 60d * (((blue - red) / delta) + 2d);
        }
        else
        {
            hue = 60d * (((red - green) / delta) + 4d);
        }
        if (hue < 0d)
        {
            hue += 360d;
        }
    }

    private static ColorRgba HslToRgb(
        double hue,
        double saturation,
        double luminance,
        byte alpha)
    {
        var chroma = (1d - Math.Abs((2d * luminance) - 1d)) * saturation;
        var segment = hue / 60d;
        var intermediate = chroma * (1d - Math.Abs((segment % 2d) - 1d));
        var (red, green, blue) = segment switch
        {
            < 1d => (chroma, intermediate, 0d),
            < 2d => (intermediate, chroma, 0d),
            < 3d => (0d, chroma, intermediate),
            < 4d => (0d, intermediate, chroma),
            < 5d => (intermediate, 0d, chroma),
            _ => (chroma, 0d, intermediate),
        };
        var match = luminance - (chroma / 2d);
        return new ColorRgba(
            ToByte(red + match),
            ToByte(green + match),
            ToByte(blue + match),
            alpha);
    }

    private static byte ToByte(double value) =>
        checked((byte)Math.Round(
            Math.Clamp(value, 0d, 1d) * 255d,
            MidpointRounding.AwayFromZero));
}

public enum TableStyleElementType
{
    WholeTable = 0,
    HeaderRow,
    TotalsRow,
    FirstColumn,
    LastColumn,
    FirstRowStripe,
    SecondRowStripe,
    FirstColumnStripe,
    SecondColumnStripe,
    FilterButton,
}

public sealed record TableStyleBorderSide
{
    public CellBorderLineStyle Style { get; init; } = CellBorderLineStyle.Thin;
    public TableStyleColor Color { get; init; } =
        TableStyleColor.FromRgb(ColorRgba.Black);
    public double Width { get; init; } = 1d;

    internal CellBorderSide Resolve(WorkbookTheme theme)
    {
        if (!double.IsFinite(Width) || Width <= 0d)
        {
            throw new InvalidOperationException(
                "A table-style border width must be finite and positive.");
        }
        return new CellBorderSide
        {
            Style = Style,
            Color = Color.Resolve(theme),
            Width = Width,
        };
    }
}

public sealed record TableStyleBorder
{
    public TableStyleBorderSide? Left { get; init; }
    public TableStyleBorderSide? Top { get; init; }
    public TableStyleBorderSide? Right { get; init; }
    public TableStyleBorderSide? Bottom { get; init; }
}

/// <summary>Defines the properties contributed by one table-style element.</summary>
public sealed record TableStyleFormat
{
    public string? FontFamily { get; init; }
    public double? FontSize { get; init; }
    public int? FontWeight { get; init; }
    public bool? FontItalic { get; init; }
    public bool? FontUnderline { get; init; }
    public bool? FontStrikeThrough { get; init; }
    public TableStyleColor? FontColor { get; init; }
    public TableStyleColor? FillColor { get; init; }
    public TableStyleColor? FillBackgroundColor { get; init; }
    public CellFillPattern FillPattern { get; init; } = CellFillPattern.Solid;
    public TableStyleBorder? Border { get; init; }
    public CellHorizontalAlignment? HorizontalAlignment { get; init; }
    public CellVerticalAlignment? VerticalAlignment { get; init; }

    internal ResolvedTableStyleFormat Resolve(WorkbookTheme theme)
    {
        if (FontSize is { } fontSize &&
            (!double.IsFinite(fontSize) || fontSize <= 0d))
        {
            throw new InvalidOperationException(
                "A table-style font size must be finite and positive.");
        }
        if (FontWeight is < 1 or > 999)
        {
            throw new InvalidOperationException(
                "A table-style font weight must be between 1 and 999.");
        }
        return new ResolvedTableStyleFormat(
            FontFamily,
            FontSize,
            FontWeight,
            FontItalic,
            FontUnderline,
            FontStrikeThrough,
            FontColor?.Resolve(theme),
            FillColor?.Resolve(theme),
            FillBackgroundColor?.Resolve(theme),
            FillPattern,
            Border?.Left?.Resolve(theme),
            Border?.Top?.Resolve(theme),
            Border?.Right?.Resolve(theme),
            Border?.Bottom?.Resolve(theme),
            HorizontalAlignment,
            VerticalAlignment);
    }
}

public sealed record TableStyleElement
{
    public const int MaximumStripeSize = 1024;

    public TableStyleElement(
        TableStyleElementType type,
        TableStyleFormat format,
        int stripeSize = 1)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (stripeSize is < 1 or > MaximumStripeSize)
        {
            throw new ArgumentOutOfRangeException(nameof(stripeSize));
        }
        Type = type;
        Format = format;
        StripeSize = stripeSize;
    }

    public TableStyleElementType Type { get; }
    public TableStyleFormat Format { get; }
    public int StripeSize { get; }
}

public sealed class TableStyleDefinition
{
    public const int MaximumNameLength = 255;
    private readonly TableStyleElement[] _elements;

    public TableStyleDefinition(
        string id,
        string name,
        IEnumerable<TableStyleElement> elements,
        bool isBuiltIn = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(elements);
        var normalizedName = name.Trim();
        if (normalizedName.Length > MaximumNameLength)
        {
            throw new ArgumentOutOfRangeException(nameof(name));
        }
        _elements = elements
            .Select(static element => element ?? throw new ArgumentException(
                "A table style cannot contain a null element.",
                nameof(elements)))
            .ToArray();
        if (_elements.Select(static element => element.Type).Distinct().Count() !=
            _elements.Length)
        {
            throw new ArgumentException(
                "A table style cannot contain duplicate element types.",
                nameof(elements));
        }
        Id = id.Trim();
        Name = normalizedName;
        IsBuiltIn = isBuiltIn;
    }

    public string Id { get; }
    public string Name { get; }
    public bool IsBuiltIn { get; }
    public IReadOnlyList<TableStyleElement> Elements => _elements;

    public TableStyleDefinition Copy() =>
        new(Id, Name, _elements, IsBuiltIn);
}

public sealed record TableStyleGalleryEntry(
    string Id,
    string Name,
    string Group);

/// <summary>Stores the built-in gallery and workbook-owned custom table styles.</summary>
public sealed class TableStyleCatalog
{
    private readonly Dictionary<string, TableStyleDefinition> _byName;
    private readonly Dictionary<string, TableStyleDefinition> _customById =
        new(StringComparer.Ordinal);
    private readonly Action? _changed;
    private readonly IReadOnlyList<TableStyleGalleryEntry> _builtInGallery =
        BuiltInTableStyles.Gallery;

    public TableStyleCatalog()
        : this(changed: null)
    {
    }

    internal TableStyleCatalog(Action? changed)
    {
        _changed = changed;
        _byName = BuiltInTableStyles.Definitions.ToDictionary(
            static definition => definition.Name,
            static definition => definition,
            StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<TableStyleGalleryEntry> BuiltInGallery =>
        _builtInGallery;

    public IReadOnlyList<TableStyleDefinition> CustomStyles =>
        _customById.Values
            .OrderBy(static style => style.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static style => style.Copy())
            .ToArray();

    public bool TryGet(string name, out TableStyleDefinition? definition)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (_byName.TryGetValue(name, out var found))
        {
            definition = found;
            return true;
        }
        definition = null;
        return false;
    }

    public TableStyleDefinition Get(string name) =>
        TryGet(name, out var definition)
            ? definition!
            : throw new KeyNotFoundException(
                $"Table style '{name}' was not found.");

    public void AddOrReplaceCustom(TableStyleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.IsBuiltIn)
        {
            throw new ArgumentException(
                "A workbook custom style cannot be marked as built-in.",
                nameof(definition));
        }
        if (BuiltInTableStyles.Definitions.Any(candidate =>
                string.Equals(candidate.Id, definition.Id, StringComparison.Ordinal) ||
                string.Equals(candidate.Name, definition.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "A custom table style cannot replace a built-in style.");
        }

        if (_byName.TryGetValue(definition.Name, out var sameName) &&
            !string.Equals(
                sameName.Id,
                definition.Id,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"A table style named '{sameName.Name}' already exists.");
        }

        if (_customById.TryGetValue(definition.Id, out var prior))
        {
            _byName.Remove(prior.Name);
        }

        var copy = definition.Copy();
        _customById[copy.Id] = copy;
        _byName[copy.Name] = copy;
        _changed?.Invoke();
    }

    internal TableStyleDefinition[] Snapshot() =>
        _byName.Values.Select(static style => style.Copy()).ToArray();
}

/// <summary>A theme-resolved table-style contribution.</summary>
public sealed record ResolvedTableStyleFormat(
    string? FontFamily,
    double? FontSize,
    int? FontWeight,
    bool? FontItalic,
    bool? FontUnderline,
    bool? FontStrikeThrough,
    ColorRgba? FontColor,
    ColorRgba? FillColor,
    ColorRgba? FillBackgroundColor,
    CellFillPattern FillPattern,
    CellBorderSide? LeftBorder,
    CellBorderSide? TopBorder,
    CellBorderSide? RightBorder,
    CellBorderSide? BottomBorder,
    CellHorizontalAlignment? HorizontalAlignment,
    CellVerticalAlignment? VerticalAlignment)
{
    public CellStyle Apply(CellStyle source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var font = source.Font with
        {
            Family = FontFamily ?? source.Font.Family,
            Size = FontSize ?? source.Font.Size,
            Weight = FontWeight ?? source.Font.Weight,
            Italic = FontItalic ?? source.Font.Italic,
            Underline = FontUnderline ?? source.Font.Underline,
            StrikeThrough = FontStrikeThrough ?? source.Font.StrikeThrough,
            Color = FontColor ?? source.Font.Color,
        };
        var fill = FillColor is { } fillColor
            ? new CellFillStyle
            {
                IsVisible = true,
                Color = fillColor,
                BackgroundColor = FillBackgroundColor ?? ColorRgba.Transparent,
                Pattern = FillPattern,
            }
            : source.Fill;
        var border = source.Border with
        {
            Left = LeftBorder ?? source.Border.Left,
            Top = TopBorder ?? source.Border.Top,
            Right = RightBorder ?? source.Border.Right,
            Bottom = BottomBorder ?? source.Border.Bottom,
        };
        var alignment = source.Alignment with
        {
            Horizontal = HorizontalAlignment ?? source.Alignment.Horizontal,
            Vertical = VerticalAlignment ?? source.Alignment.Vertical,
        };
        return source with
        {
            Font = font,
            Fill = fill,
            Border = border,
            Alignment = alignment,
        };
    }
}

/// <summary>Provides the single theme-resolved style contract used by renderers.</summary>
public sealed class ResolvedTableStyle
{
    private readonly Dictionary<TableStyleElementType, ResolvedElement>
        _elements;
    private readonly ConcurrentDictionary<CellKey, CellStyle> _cellCache = [];

    internal ResolvedTableStyle(
        TableStyleDefinition definition,
        WorkbookTheme theme)
    {
        Id = definition.Id;
        Name = definition.Name;
        _elements = definition.Elements.ToDictionary(
            static element => element.Type,
            element => new ResolvedElement(
                element.Format.Resolve(theme),
                element.StripeSize));
    }

    public string Id { get; }
    public string Name { get; }

    public CellStyle ResolveCell(
        SpreadsheetTable table,
        CellAddress address)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!table.Range.Contains(address))
        {
            return CellStyle.Default;
        }
        var key = CreateKey(table, address);
        return _cellCache.GetOrAdd(
            key,
            static (cellKey, style) => style.ComposeCell(cellKey),
            this);
    }

    public CellStyle ResolveFilterButton(SpreadsheetTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        var style = CellStyle.Default;
        style = Apply(style, TableStyleElementType.WholeTable);
        style = Apply(style, TableStyleElementType.HeaderRow);
        return Apply(style, TableStyleElementType.FilterButton);
    }

    private CellStyle ComposeCell(CellKey key)
    {
        var style = CellStyle.Default;
        style = Apply(style, TableStyleElementType.WholeTable);
        if (key.RowStripe is { } rowStripe)
        {
            style = Apply(style, rowStripe);
        }
        if (key.ColumnStripe is { } columnStripe)
        {
            style = Apply(style, columnStripe);
        }
        if (key.FirstColumn)
        {
            style = Apply(style, TableStyleElementType.FirstColumn);
        }
        if (key.LastColumn)
        {
            style = Apply(style, TableStyleElementType.LastColumn);
        }
        if (key.Header)
        {
            style = Apply(style, TableStyleElementType.HeaderRow);
        }
        if (key.Totals)
        {
            style = Apply(style, TableStyleElementType.TotalsRow);
        }
        return style;
    }

    private CellStyle Apply(CellStyle style, TableStyleElementType type) =>
        _elements.TryGetValue(type, out var element)
            ? element.Format.Apply(style)
            : style;

    private CellKey CreateKey(
        SpreadsheetTable table,
        CellAddress address)
    {
        var isData = table.DataRange is { } data && data.Contains(address);
        TableStyleElementType? rowStripe = null;
        TableStyleElementType? columnStripe = null;
        if (isData && table.ShowRowStripes)
        {
            rowStripe = ResolveStripe(
                address.RowIndex - table.DataRange!.Value.Top,
                TableStyleElementType.FirstRowStripe,
                TableStyleElementType.SecondRowStripe);
        }
        if (isData && table.ShowColumnStripes)
        {
            columnStripe = ResolveStripe(
                address.ColumnIndex - table.Range.Left,
                TableStyleElementType.FirstColumnStripe,
                TableStyleElementType.SecondColumnStripe);
        }
        return new CellKey(
            table.HasHeaders && address.RowIndex == table.Range.Top,
            table.HasTotalsRow && address.RowIndex == table.Range.Bottom,
            table.ShowFirstColumn && address.ColumnIndex == table.Range.Left,
            table.ShowLastColumn && address.ColumnIndex == table.Range.Right,
            rowStripe,
            columnStripe);
    }

    private TableStyleElementType? ResolveStripe(
        int offset,
        TableStyleElementType first,
        TableStyleElementType second)
    {
        var hasFirst = _elements.TryGetValue(first, out var firstElement);
        var hasSecond = _elements.TryGetValue(second, out var secondElement);
        if (!hasFirst && !hasSecond)
        {
            return null;
        }
        var firstSize = hasFirst ? firstElement!.StripeSize : 1;
        var secondSize = hasSecond ? secondElement!.StripeSize : 1;
        var cycle = firstSize + secondSize;
        var position = offset % cycle;
        return position < firstSize ? first : second;
    }

    private sealed record ResolvedElement(
        ResolvedTableStyleFormat Format,
        int StripeSize);

    private readonly record struct CellKey(
        bool Header,
        bool Totals,
        bool FirstColumn,
        bool LastColumn,
        TableStyleElementType? RowStripe,
        TableStyleElementType? ColumnStripe);
}

public static class TableStyleResolver
{
    public static ResolvedTableStyle Resolve(
        TableStyleDefinition definition,
        WorkbookTheme theme)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(theme);
        return new ResolvedTableStyle(definition, theme);
    }
}

public sealed record TableStylePreviewCell(
    int RowIndex,
    int ColumnIndex,
    CellStyle Style);

public static class TableStylePreview
{
    public const int MaximumRows = 12;
    public const int MaximumColumns = 12;

    public static IReadOnlyList<TableStylePreviewCell> Create(
        TableStyleDefinition definition,
        WorkbookTheme theme,
        int rows = 4,
        int columns = 5)
    {
        if (rows is < 2 or > MaximumRows)
        {
            throw new ArgumentOutOfRangeException(nameof(rows));
        }
        if (columns is < 1 or > MaximumColumns)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }
        var table = new SpreadsheetTable(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            "PreviewTable",
            new CellRange(
                default,
                new CellAddress(rows - 1, columns - 1)),
            Enumerable.Range(0, columns).Select(index =>
                new SpreadsheetTableColumn(
                    CreatePreviewColumnId(index),
                    $"Column{index + 1}")),
            hasTotalsRow: true,
            styleName: definition.Name,
            showFirstColumn: true,
            showLastColumn: true,
            showRowStripes: true,
            showColumnStripes: true);
        var resolved = TableStyleResolver.Resolve(definition, theme);
        var result = new TableStylePreviewCell[rows * columns];
        var resultIndex = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var address = new CellAddress(row, column);
                result[resultIndex++] = new TableStylePreviewCell(
                    row,
                    column,
                    resolved.ResolveCell(table, address));
            }
        }
        return result;
    }

    private static Guid CreatePreviewColumnId(int index)
    {
        Span<byte> bytes = stackalloc byte[16];
        bytes[15] = checked((byte)(index + 1));
        return new Guid(bytes);
    }
}

internal static class BuiltInTableStyles
{
    public static IReadOnlyList<TableStyleDefinition> Definitions { get; } =
        CreateDefinitions();

    public static IReadOnlyList<TableStyleGalleryEntry> Gallery { get; } =
        Definitions.Select(static definition => new TableStyleGalleryEntry(
            definition.Id,
            definition.Name,
            definition.Name.StartsWith("TableStyleLight", StringComparison.Ordinal)
                ? "Light"
                : definition.Name.StartsWith("TableStyleMedium", StringComparison.Ordinal)
                    ? "Medium"
                    : "Dark")).ToArray();

    private static TableStyleDefinition[] CreateDefinitions()
    {
        var result = new List<TableStyleDefinition>(60);
        AddGroup(result, "Light", 21);
        AddGroup(result, "Medium", 28);
        AddGroup(result, "Dark", 11);
        return [.. result];
    }

    private static void AddGroup(
        List<TableStyleDefinition> result,
        string group,
        int count)
    {
        for (var index = 1; index <= count; index++)
        {
            var name = $"TableStyle{group}{index}";
            var accent = (WorkbookThemeColor)((int)WorkbookThemeColor.Accent1 +
                ((index - 1) % 6));
            result.Add(new TableStyleDefinition(
                $"builtin:{name}",
                name,
                CreateElements(group, accent, index),
                isBuiltIn: true));
        }
    }

    private static TableStyleElement[] CreateElements(
        string group,
        WorkbookThemeColor accent,
        int variant)
    {
        var borderColor = TableStyleColor.FromTheme(accent, group == "Dark" ? -0.25d : 0d);
        var border = new TableStyleBorderSide
        {
            Color = borderColor,
            Style = CellBorderLineStyle.Thin,
        };
        var headerFill = TableStyleColor.FromTheme(
            accent,
            group == "Light" ? 0.8d : group == "Dark" ? -0.35d : 0d);
        var stripeFill = TableStyleColor.FromTheme(
            accent,
            group == "Dark" ? 0.55d : 0.85d - ((variant % 3) * 0.05d));
        var headerFont = group == "Light"
            ? TableStyleColor.FromTheme(WorkbookThemeColor.Dark1)
            : TableStyleColor.FromTheme(WorkbookThemeColor.Light1);
        return
        [
            new TableStyleElement(
                TableStyleElementType.WholeTable,
                new TableStyleFormat
                {
                    Border = new TableStyleBorder
                    {
                        Left = border,
                        Top = border,
                        Right = border,
                        Bottom = border,
                    },
                }),
            new TableStyleElement(
                TableStyleElementType.HeaderRow,
                new TableStyleFormat
                {
                    FillColor = headerFill,
                    FontColor = headerFont,
                    FontWeight = 700,
                    Border = new TableStyleBorder { Bottom = border },
                }),
            new TableStyleElement(
                TableStyleElementType.TotalsRow,
                new TableStyleFormat
                {
                    FontWeight = 700,
                    Border = new TableStyleBorder { Top = border },
                }),
            new TableStyleElement(
                TableStyleElementType.FirstRowStripe,
                new TableStyleFormat { FillColor = stripeFill }),
            new TableStyleElement(
                TableStyleElementType.SecondRowStripe,
                new TableStyleFormat()),
            new TableStyleElement(
                TableStyleElementType.FirstColumn,
                new TableStyleFormat { FontWeight = 700 }),
            new TableStyleElement(
                TableStyleElementType.LastColumn,
                new TableStyleFormat { FontWeight = 700 }),
            new TableStyleElement(
                TableStyleElementType.FilterButton,
                new TableStyleFormat
                {
                    FillColor = TableStyleColor.FromTheme(WorkbookThemeColor.Light1, -0.05d),
                    FontColor = TableStyleColor.FromTheme(accent, -0.35d),
                    Border = new TableStyleBorder
                    {
                        Left = border,
                        Top = border,
                        Right = border,
                        Bottom = border,
                    },
                }),
        ];
    }
}
