using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Viewport;

public static class SpreadsheetViewportFrozenPaneExtensions
{
    public static SizeD GetFrozenPaneExtent(this SpreadsheetViewportEngine engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        var worksheet = engine.Session.ActiveWorksheet;
        var view = engine.Session.View;
        return new SizeD(
            GetAxisExtent(
                view.FrozenColumns,
                worksheet.Dimensions.DefaultColumnWidth,
                worksheet.Dimensions.GetColumnOverrides()),
            GetAxisExtent(
                view.FrozenRows,
                worksheet.Dimensions.DefaultRowHeight,
                worksheet.Dimensions.GetRowOverrides()));
    }

    private static double GetAxisExtent(
        int frozenCount,
        double defaultSize,
        IReadOnlyDictionary<int, double> overrides)
    {
        if (frozenCount <= 0)
        {
            return 0d;
        }

        var extent = defaultSize * frozenCount;
        foreach (var (index, size) in overrides)
        {
            if (index >= frozenCount)
            {
                continue;
            }
            extent += size - defaultSize;
        }
        return Math.Max(0d, extent);
    }
}
