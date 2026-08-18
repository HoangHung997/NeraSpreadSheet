using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Core;

public sealed record CellStylePatch
{
    public string? FontFamily { get; init; }

    public double? FontSize { get; init; }

    public int? FontWeight { get; init; }

    public bool? FontItalic { get; init; }

    public bool? FontUnderline { get; init; }

    public ColorRgba? FontColor { get; init; }

    public CellFillStyle? Fill { get; init; }

    public CellBorderStyle? Border { get; init; }

    public CellHorizontalAlignment? HorizontalAlignment { get; init; }

    public CellVerticalAlignment? VerticalAlignment { get; init; }

    public bool? WrapText { get; init; }

    public int? TextRotationDegrees { get; init; }

    public string? NumberFormatCode { get; init; }

    public bool IsEmpty =>
        FontFamily is null &&
        FontSize is null &&
        FontWeight is null &&
        FontItalic is null &&
        FontUnderline is null &&
        FontColor is null &&
        Fill is null &&
        Border is null &&
        HorizontalAlignment is null &&
        VerticalAlignment is null &&
        WrapText is null &&
        TextRotationDegrees is null &&
        NumberFormatCode is null;

    public CellStyle Apply(CellStyle source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (IsEmpty)
        {
            return source;
        }

        var font = source.Font with
        {
            Family = FontFamily ?? source.Font.Family,
            Size = FontSize ?? source.Font.Size,
            Weight = FontWeight ?? source.Font.Weight,
            Italic = FontItalic ?? source.Font.Italic,
            Underline = FontUnderline ?? source.Font.Underline,
            Color = FontColor ?? source.Font.Color,
        };
        var alignment = source.Alignment with
        {
            Horizontal = HorizontalAlignment ?? source.Alignment.Horizontal,
            Vertical = VerticalAlignment ?? source.Alignment.Vertical,
            WrapText = WrapText ?? source.Alignment.WrapText,
            TextRotationDegrees =
                TextRotationDegrees ?? source.Alignment.TextRotationDegrees,
        };
        var numberFormat = NumberFormatCode is null
            ? source.NumberFormat
            : new CellNumberFormatStyle(NumberFormatCode);
        return source with
        {
            Font = font,
            Fill = Fill ?? source.Fill,
            Border = Border ?? source.Border,
            Alignment = alignment,
            NumberFormat = numberFormat,
        };
    }

    public static CellStylePatch FromDifference(
        CellStyle before,
        CellStyle after)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(after);
        return new CellStylePatch
        {
            FontFamily = !string.Equals(
                before.Font.Family,
                after.Font.Family,
                StringComparison.Ordinal)
                ? after.Font.Family
                : null,
            FontSize = before.Font.Size != after.Font.Size
                ? after.Font.Size
                : null,
            FontWeight = before.Font.Weight != after.Font.Weight
                ? after.Font.Weight
                : null,
            FontItalic = before.Font.Italic != after.Font.Italic
                ? after.Font.Italic
                : null,
            FontUnderline = before.Font.Underline != after.Font.Underline
                ? after.Font.Underline
                : null,
            FontColor = before.Font.Color != after.Font.Color
                ? after.Font.Color
                : null,
            Fill = before.Fill != after.Fill ? after.Fill : null,
            Border = before.Border != after.Border ? after.Border : null,
            HorizontalAlignment =
                before.Alignment.Horizontal != after.Alignment.Horizontal
                    ? after.Alignment.Horizontal
                    : null,
            VerticalAlignment =
                before.Alignment.Vertical != after.Alignment.Vertical
                    ? after.Alignment.Vertical
                    : null,
            WrapText = before.Alignment.WrapText != after.Alignment.WrapText
                ? after.Alignment.WrapText
                : null,
            TextRotationDegrees =
                before.Alignment.TextRotationDegrees !=
                after.Alignment.TextRotationDegrees
                    ? after.Alignment.TextRotationDegrees
                    : null,
            NumberFormatCode = !string.Equals(
                before.NumberFormat.FormatCode,
                after.NumberFormat.FormatCode,
                StringComparison.Ordinal)
                ? after.NumberFormat.FormatCode
                : null,
        };
    }
}
