namespace NeraSpreadSheet.Rendering.Spreadsheet;

public enum SpreadsheetChromeRegion
{
    Outside,
    Body,
    RowHeader,
    ColumnHeader,
    Corner,
}

public readonly record struct SpreadsheetChromeMetrics(
    double RowHeaderWidth,
    double ColumnHeaderHeight,
    double BodyWidth,
    double BodyHeight)
{
    public double FullWidth => RowHeaderWidth + BodyWidth;
    public double FullHeight => ColumnHeaderHeight + BodyHeight;
}

public readonly record struct SpreadsheetChromeHit(
    SpreadsheetChromeRegion Region,
    double BodyX,
    double BodyY);

public static class SpreadsheetChromeGeometry
{
    public static SpreadsheetChromeMetrics Calculate(
        double fullWidth,
        double fullHeight,
        SpreadsheetRenderTheme theme)
    {
        ArgumentNullException.ThrowIfNull(theme);
        if (!double.IsFinite(fullWidth) || fullWidth < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(fullWidth));
        }
        if (!double.IsFinite(fullHeight) || fullHeight < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(fullHeight));
        }

        var headerWidth = theme.ShowHeaders ? ValidateHeaderExtent(theme.RowHeaderWidth, nameof(theme.RowHeaderWidth)) : 0d;
        var headerHeight = theme.ShowHeaders ? ValidateHeaderExtent(theme.ColumnHeaderHeight, nameof(theme.ColumnHeaderHeight)) : 0d;
        return new SpreadsheetChromeMetrics(
            Math.Min(headerWidth, fullWidth),
            Math.Min(headerHeight, fullHeight),
            Math.Max(0d, fullWidth - headerWidth),
            Math.Max(0d, fullHeight - headerHeight));
    }

    public static SpreadsheetChromeHit HitTest(
        double x,
        double y,
        double fullWidth,
        double fullHeight,
        SpreadsheetRenderTheme theme)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y) || x < 0d || y < 0d || x >= fullWidth || y >= fullHeight)
        {
            return new SpreadsheetChromeHit(SpreadsheetChromeRegion.Outside, 0d, 0d);
        }

        var metrics = Calculate(fullWidth, fullHeight, theme);
        if (!theme.ShowHeaders)
        {
            return new SpreadsheetChromeHit(SpreadsheetChromeRegion.Body, x, y);
        }

        var inRowHeader = x < metrics.RowHeaderWidth;
        var inColumnHeader = y < metrics.ColumnHeaderHeight;
        if (inRowHeader && inColumnHeader)
        {
            return new SpreadsheetChromeHit(SpreadsheetChromeRegion.Corner, 0d, 0d);
        }
        if (inRowHeader)
        {
            return new SpreadsheetChromeHit(
                SpreadsheetChromeRegion.RowHeader,
                0d,
                y - metrics.ColumnHeaderHeight);
        }
        if (inColumnHeader)
        {
            return new SpreadsheetChromeHit(
                SpreadsheetChromeRegion.ColumnHeader,
                x - metrics.RowHeaderWidth,
                0d);
        }
        return new SpreadsheetChromeHit(
            SpreadsheetChromeRegion.Body,
            x - metrics.RowHeaderWidth,
            y - metrics.ColumnHeaderHeight);
    }

    private static double ValidateHeaderExtent(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Header extent must be finite and positive.");
        }
        return value;
    }
}
