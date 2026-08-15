using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core;

public enum CellHorizontalAlignment
{
    General,
    Left,
    Center,
    Right,
}

public enum CellVerticalAlignment
{
    Top,
    Center,
    Bottom,
}

public enum CellBorderLineStyle
{
    None,
    Thin,
    Medium,
    Thick,
    Dashed,
    Dotted,
    DoubleLine,
}

public sealed record CellFontStyle
{
    public string Family { get; init; } = "Segoe UI";
    public double Size { get; init; } = 12d;
    public int Weight { get; init; } = 400;
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public ColorRgba Color { get; init; } = ColorRgba.Black;
}

public sealed record CellFillStyle
{
    public bool IsVisible { get; init; }
    public ColorRgba Color { get; init; } = ColorRgba.Transparent;
}

public sealed record CellBorderSide
{
    public CellBorderLineStyle Style { get; init; }
    public ColorRgba Color { get; init; } = ColorRgba.Black;
    public double Width { get; init; } = 1d;
}

public sealed record CellBorderStyle
{
    public CellBorderSide Left { get; init; } = new();
    public CellBorderSide Top { get; init; } = new();
    public CellBorderSide Right { get; init; } = new();
    public CellBorderSide Bottom { get; init; } = new();
}

public sealed record CellAlignmentStyle
{
    public CellHorizontalAlignment Horizontal { get; init; } = CellHorizontalAlignment.General;
    public CellVerticalAlignment Vertical { get; init; } = CellVerticalAlignment.Bottom;
    public bool WrapText { get; init; }
    public int TextRotationDegrees { get; init; }
}

public sealed record CellNumberFormatStyle
{
    public string FormatCode { get; init; } = "General";
}

public sealed record CellStyle
{
    public static CellStyle Default { get; } = new();
    public CellFontStyle Font { get; init; } = new();
    public CellFillStyle Fill { get; init; } = new();
    public CellBorderStyle Border { get; init; } = new();
    public CellAlignmentStyle Alignment { get; init; } = new();
    public CellNumberFormatStyle NumberFormat { get; init; } = new();
}

public sealed class CellStyleCatalog
{
    public const int DefaultStyleId = 0;
    private readonly List<CellStyle> _styles = [CellStyle.Default];
    private readonly Dictionary<CellStyle, int> _ids = new() { [CellStyle.Default] = DefaultStyleId };

    public int Count => _styles.Count;

    public CellStyle Get(int styleId)
    {
        if ((uint)styleId >= (uint)_styles.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(styleId));
        }
        return _styles[styleId];
    }

    public int Intern(CellStyle style)
    {
        ArgumentNullException.ThrowIfNull(style);
        Validate(style);
        if (_ids.TryGetValue(style, out var existing))
        {
            return existing;
        }
        var id = _styles.Count;
        _styles.Add(style);
        _ids.Add(style, id);
        return id;
    }

    public IReadOnlyList<CellStyle> Snapshot() => _styles.ToArray();

    private static void Validate(CellStyle style)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(style.Font.Family);
        if (!double.IsFinite(style.Font.Size) || style.Font.Size <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(style), "Font size must be finite and positive.");
        }
        if (style.Font.Weight is < 1 or > 999)
        {
            throw new ArgumentOutOfRangeException(nameof(style), "Font weight must be between 1 and 999.");
        }
        if (style.Alignment.TextRotationDegrees is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(style), "Text rotation must be between -90 and 90 degrees.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(style.NumberFormat.FormatCode);
        ValidateBorder(style.Border.Left);
        ValidateBorder(style.Border.Top);
        ValidateBorder(style.Border.Right);
        ValidateBorder(style.Border.Bottom);
    }

    private static void ValidateBorder(CellBorderSide border)
    {
        ArgumentNullException.ThrowIfNull(border);
        if (!double.IsFinite(border.Width) || border.Width <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(border), "Border width must be finite and positive.");
        }
    }
}
