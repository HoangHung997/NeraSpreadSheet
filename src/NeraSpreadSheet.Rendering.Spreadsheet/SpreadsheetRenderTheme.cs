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
    public IReadOnlyList<ColorRgba> FormulaReferenceColors { get; init; } =
    [
        new(33, 115, 201),
        new(196, 62, 62),
        new(139, 74, 168),
        new(210, 124, 31),
        new(27, 140, 122),
    ];
    public ColorRgba InvalidCell { get; init; } = new(196, 32, 32);
    public ColorRgba FreezePaneLine { get; init; } = new(128, 128, 128);
    public ColorRgba SplitPaneSeparator { get; init; } = new(176, 176, 176);
    public ColorRgba ActivePaneBorder { get; init; } = new(80, 120, 96);
    public ColorRgba HeaderBackground { get; init; } = new(245, 245, 245);
    public ColorRgba HeaderSelectedBackground { get; init; } = new(216, 235, 223);
    public ColorRgba HeaderActiveBackground { get; init; } = new(198, 225, 209);
    public ColorRgba HeaderText { get; init; } = new(64, 64, 64);
    public ColorRgba HeaderBorder { get; init; } = new(190, 190, 190);
    public ColorRgba TableFilterButtonBackground { get; init; } = new(250, 250, 250);
    public ColorRgba TableFilterButtonFilteredBackground { get; init; } = new(210, 232, 219);
    public ColorRgba TableFilterButtonBorder { get; init; } = new(145, 145, 145);
    public ColorRgba TableFilterButtonGlyph { get; init; } = new(55, 55, 55);
    public ColorRgba ScrollBarBackground { get; init; } = new(241, 241, 241);
    public ColorRgba ScrollBarTrack { get; init; } = new(232, 232, 232);
    public ColorRgba ScrollBarButtonBackground { get; init; } = new(239, 239, 239);
    public ColorRgba ScrollBarThumb { get; init; } = new(174, 174, 174);
    public ColorRgba ScrollBarActiveThumb { get; init; } = new(126, 154, 137);
    public ColorRgba ScrollBarBorder { get; init; } = new(146, 146, 146);
    public ColorRgba ScrollBarGlyph { get; init; } = new(70, 70, 70);
    public ColorRgba ScrollBarCorner { get; init; } = new(224, 224, 224);
    public string FontFamily { get; init; } = "Segoe UI";
    public double FontSize { get; init; } = 12d;
    public double SelectionStrokeWidth { get; init; } = 2d;
    public double FormulaReferenceStrokeWidth { get; init; } = 2d;
    public double InvalidCellStrokeWidth { get; init; } = 2d;
    public double GridStrokeWidth { get; init; } = 1d;
    public double FreezePaneStrokeWidth { get; init; } = 2d;
    public double ActivePaneStrokeWidth { get; init; } = 1d;
    public bool ShowValidationErrors { get; init; } = true;
    public bool ShowTableFilterButtons { get; init; } = true;
    public double TableFilterButtonExtent { get; init; } = 14d;
    public double TableFilterButtonMinimumExtent { get; init; } = 8d;
    public double TableFilterButtonMargin { get; init; } = 2d;
    public double TableFilterButtonStrokeWidth { get; init; } = 1d;
    public bool ShowHeaders { get; init; }
    public double RowHeaderWidth { get; init; } = 48d;
    public double ColumnHeaderHeight { get; init; } = 24d;
    public double HeaderFontSize { get; init; } = 11d;
    public double HeaderStrokeWidth { get; init; } = 1d;
    public bool ShowSplitPaneScrollBars { get; init; } = true;
    public double ScrollBarThickness { get; init; } = 14d;
    public double ScrollBarButtonExtent { get; init; } = 14d;
    public double ScrollBarMinimumThumbExtent { get; init; } = 24d;
    public double ScrollBarLineStep { get; init; } = 48d;
    public double ScrollBarPageFactor { get; init; } = 0.9d;
    public double ScrollBarStrokeWidth { get; init; } = 1d;

    public TextStyle CreateTextStyle() =>
        new(FontFamily, FontSize, 400, Text, false);

    public TextStyle CreateHeaderTextStyle() =>
        new(FontFamily, HeaderFontSize, 400, HeaderText, false);
}
