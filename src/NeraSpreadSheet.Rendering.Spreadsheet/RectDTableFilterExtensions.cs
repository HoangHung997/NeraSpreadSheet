using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

internal static class RectDTableFilterExtensions
{
    public static RectD Translate(
        this RectD bounds,
        double deltaX,
        double deltaY) =>
        new(
            bounds.X + deltaX,
            bounds.Y + deltaY,
            bounds.Width,
            bounds.Height);
}
