using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core;

public enum CellHorizontalAlignment
{
    General,
    Left,
    Center,
    Right,
    Fill,
    Justify,
    CenterContinuous,
    Distributed,
}

public enum CellVerticalAlignment
{
    Top,
    Center,
    Bottom,
    Justify,
    Distributed,
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
    Hair,
    MediumDashed,
    DashDot,
    MediumDashDot,
    DashDotDot,
    MediumDashDotDot,
    SlantDashDot,
}

public enum CellFontVerticalAlignment
{
    None,
    Superscript,
    Subscript,
}

public enum CellFillPattern
{
    None,
    Solid,
    Gray125,
    DarkGray,
    MediumGray,
    LightGray,
    DarkHorizontal,
    DarkVertical,
    DarkDown,
    DarkUp,
    DarkGrid,
    DarkTrellis,
    LightHorizontal,
    LightVertical,
    LightDown,
    LightUp,
    LightGrid,
    LightTrellis,
}

public enum CellReadingOrder
{
    Context,
    LeftToRight,
    RightToLeft,
}

public sealed record CellFontStyle
{
    public string Family { get; init; } = "Segoe UI";
    public double Size { get; init; } = 12d;
    public int Weight { get; init; } = 400;
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public bool DoubleUnderline { get; init; }
    public bool StrikeThrough { get; init; }
    public bool Outline { get; init; }
    public bool Shadow { get; init; }
    public CellFontVerticalAlignment VerticalAlignment { get; init; }
    public ColorRgba Color { get; init; } = ColorRgba.Black;
}

public sealed record CellFillStyle
{
    public bool IsVisible { get; init; }
    public ColorRgba Color { get; init; } = ColorRgba.Transparent;
    public ColorRgba BackgroundColor { get; init; } = ColorRgba.Transparent;
    public CellFillPattern Pattern { get; init; }
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
    public CellBorderSide Diagonal { get; init; } = new();
    public bool DiagonalUp { get; init; }
    public bool DiagonalDown { get; init; }
}

public sealed record CellAlignmentStyle
{
    public CellHorizontalAlignment Horizontal { get; init; } = CellHorizontalAlignment.General;
    public CellVerticalAlignment Vertical { get; init; } = CellVerticalAlignment.Bottom;
    public bool WrapText { get; init; }
    public bool ShrinkToFit { get; init; }
    public bool JustifyLastLine { get; init; }
    public int Indent { get; init; }
    public int RelativeIndent { get; init; }
    public CellReadingOrder ReadingOrder { get; init; }
    public int TextRotationDegrees { get; init; }
}

public sealed record CellNumberFormatStyle
{
    public string FormatCode { get; init; } = "General";
}

public sealed record CellProtectionStyle
{
    public bool Locked { get; init; } = true;
    public bool FormulaHidden { get; init; }
}

public sealed record CellStyle
{
    public static CellStyle Default { get; } = new();
    public CellFontStyle Font { get; init; } = new();
    public CellFillStyle Fill { get; init; } = new();
    public CellBorderStyle Border { get; init; } = new();
    public CellAlignmentStyle Alignment { get; init; } = new();
    public CellNumberFormatStyle NumberFormat { get; init; } = new();
    public CellProtectionStyle Protection { get; init; } = new();
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
        if (style.Alignment.Indent is < 0 or > 250 ||
            style.Alignment.RelativeIndent is < -250 or > 250)
        {
            throw new ArgumentOutOfRangeException(nameof(style), "Alignment indentation is outside the Excel range.");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(style.NumberFormat.FormatCode);
        ValidateBorder(style.Border.Left);
        ValidateBorder(style.Border.Top);
        ValidateBorder(style.Border.Right);
        ValidateBorder(style.Border.Bottom);
        ValidateBorder(style.Border.Diagonal);
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
