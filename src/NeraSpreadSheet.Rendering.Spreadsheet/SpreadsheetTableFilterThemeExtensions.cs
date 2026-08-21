namespace NeraSpreadSheet.Rendering.Spreadsheet;

/// <summary>
/// Names the filtered-state color in host terminology while retaining the
/// canonical render-theme property used by the shared display-list layer.
/// </summary>
public static class SpreadsheetTableFilterThemeExtensions
{
    extension(SpreadsheetRenderTheme theme)
    {
        public NeraSpreadSheet.Foundation.ColorRgba TableFilterButtonActiveBackground =>
            theme.TableFilterButtonFilteredBackground;
    }
}
