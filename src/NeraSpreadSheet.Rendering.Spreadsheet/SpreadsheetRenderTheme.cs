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
    public ColorRgba FreezePaneLine { get; init; } = new(128, 128, 128);
    public ColorRgba HeaderBackground { get; init; } = new(245, 245, 245);
    public ColorRgba HeaderSelectedBackground { get; init; } = new(216, 235, 223);
    public ColorRgba HeaderActiveBackground { get; init; } = new(198, 225, 209);
    public ColorRgba HeaderText { get; init; } = new(64, 64, 64);
    public ColorRgba HeaderBorder { get; init; } = new(190, 190, 190);
    public string FontFamily { get; init; } = "Segoe UI";
    public double FontSize { get; init; } = 12d;
    public double SelectionStrokeWidth { get; init; } = 2d;
    public double GridStrokeWidth { get; init; } = 1d;
    public double FreezePaneStrokeWidth { get; init; } = 2d;
    public bool ShowHeaders { get; init; }
    public double RowHeaderWidth { get; init; } = 48d;
    public double ColumnHeaderHeight { get; init; } = 24d;
    public double HeaderFontSize { get; init; } = 11d;
    public double HeaderStrokeWidth { get; init; } = 1d;

    public TextStyle CreateTextStyle() => new(FontFamily, FontSize, 400, Text, false);

    public TextStyle CreateHeaderTextStyle() => new(FontFamily, HeaderFontSize, 400, HeaderText, false);
}
