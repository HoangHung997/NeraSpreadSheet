using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public sealed record SpreadsheetRenderTheme
{
    public ColorRgba Background { get; init; } = ColorRgba.White;
    public ColorRgba GridLine { get; init; } = ColorRgba.GridLine;
    public ColorRgba Text { get; init; } = ColorRgba.Black;
    public ColorRgba Selection { get; init; } = ColorRgba.Selection;
    public ColorRgba ActiveCell { get; init; } = new(16, 92, 52);
    public string FontFamily { get; init; } = "Segoe UI";
    public double FontSize { get; init; } = 12d;
    public double SelectionStrokeWidth { get; init; } = 2d;
    public double GridStrokeWidth { get; init; } = 1d;

    public TextStyle CreateTextStyle() => new(FontFamily, FontSize, 400, Text, false);
}
