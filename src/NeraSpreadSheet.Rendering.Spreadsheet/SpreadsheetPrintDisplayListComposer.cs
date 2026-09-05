using System.Globalization;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public sealed record SpreadsheetPrintDisplayListOptions
{
    public SpreadsheetRenderTheme Theme { get; init; } = new();

    public ColorRgba PaperBackground { get; init; } = ColorRgba.White;

    public ColorRgba HeaderFooterColor { get; init; } = ColorRgba.Black;

    public double HeaderFooterFontSize { get; init; } = 10d;

    public string? WorkbookName { get; init; }

    public DateTime? Timestamp { get; init; }

    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;

    public bool IncludeValidationErrors { get; init; }
}

public sealed record SpreadsheetPrintDisplayListResult(
    SpreadsheetPrintPage Page,
    SpreadsheetPrintPageGrid Grid,
    DisplayList DisplayList,
    string HeaderText,
    string FooterText);

/// <summary>
/// Composes one physical print page by nesting the production spreadsheet
/// display-list composer inside page/paper/header/footer commands. Cell, style,
/// conditional-formatting and formula display semantics therefore remain shared
/// with WPF, WinForms and MAUI rather than being reimplemented for printing.
/// </summary>
public static class SpreadsheetPrintDisplayListComposer
{
    public static SpreadsheetPrintDisplayListResult Compose(
        WorksheetSnapshot worksheet,
        SpreadsheetPageLayoutPlan plan,
        int pageIndex,
        CellStyleCatalog? styles = null,
        SpreadsheetPrintDisplayListOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(worksheet);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            pageIndex,
            plan.Pages.Count);
        options ??= new SpreadsheetPrintDisplayListOptions();
        ValidateOptions(options);
        if (plan.Setup.PrintHeadings)
        {
            throw new NotSupportedException(
                "Printed row and column headings are not implemented yet.");
        }

        var page = plan.Pages[pageIndex];
        var grid = SpreadsheetPrintPageGridBuilder.Create(
            worksheet,
            page);
        var contentOrigin = new PointD(
            page.PrintableBoundsDips.X + page.ContentOffsetDips.X,
            page.PrintableBoundsDips.Y + page.ContentOffsetDips.Y);
        var rows = grid.Rows
            .Select(slot => new AxisSlot(
                slot.WorksheetIndex,
                slot.StartDips - contentOrigin.Y,
                slot.SizeDips))
            .ToArray();
        var columns = grid.Columns
            .Select(slot => new AxisSlot(
                slot.WorksheetIndex,
                slot.StartDips - contentOrigin.X,
                slot.SizeDips))
            .ToArray();
        var contentSize = new SizeD(
            page.UnscaledContentSizeDips.Width * page.Scale,
            page.UnscaledContentSizeDips.Height * page.Scale);
        var layout = new ViewportLayout(
            0d,
            0d,
            contentSize,
            contentSize.Width,
            contentSize.Height,
            0d,
            0d,
            rows,
            columns);
        var printTheme = options.Theme with
        {
            GridLine = plan.Setup.PrintGridlines
                ? options.Theme.GridLine
                : ColorRgba.Transparent,
            ShowValidationErrors = options.IncludeValidationErrors,
            ShowTableFilterButtons = false,
            ShowHeaders = false,
            ShowSplitPaneScrollBars = false,
        };
        var sheetContent = SpreadsheetDisplayListComposer.Compose(
            worksheet,
            layout,
            selection: null,
            printTheme,
            styles,
            includeFreezeSeparators: false);

        var context = new SpreadsheetHeaderFooterContext(
            page.PageNumber,
            plan.Pages.Count,
            worksheet.Name,
            options.WorkbookName,
            options.Timestamp);
        var header = SpreadsheetHeaderFooterFormatter.Format(
            plan.Setup.OddHeader,
            context,
            options.Culture);
        var footer = SpreadsheetHeaderFooterFormatter.Format(
            plan.Setup.OddFooter,
            context,
            options.Culture);
        var builder = new DisplayListBuilder();
        var paperBounds = new RectD(
            0d,
            0d,
            page.PaperSizeDips.Width,
            page.PaperSizeDips.Height);
        builder.FillRectangle(paperBounds, options.PaperBackground);
        builder.PushClip(paperBounds);
        builder.PushClip(page.PrintableBoundsDips);
        builder.PushTranslation(contentOrigin.X, contentOrigin.Y);
        builder.DrawDisplayList(sheetContent);
        builder.PopTranslation();
        builder.PopClip();
        AppendHeaderFooter(
            builder,
            page,
            plan.Setup,
            header,
            footer,
            options);
        builder.PopClip();
        return new SpreadsheetPrintDisplayListResult(
            page,
            grid,
            builder.Build(),
            header,
            footer);
    }

    private static void AppendHeaderFooter(
        DisplayListBuilder builder,
        SpreadsheetPrintPage page,
        SpreadsheetPageSetup setup,
        string header,
        string footer,
        SpreadsheetPrintDisplayListOptions options)
    {
        var left = setup.Margins.LeftInches *
                   SpreadsheetPageLayoutPlanner.DipsPerInch;
        var right = setup.Margins.RightInches *
                    SpreadsheetPageLayoutPlanner.DipsPerInch;
        var width = Math.Max(
            0d,
            page.PaperSizeDips.Width - left - right);
        var lineHeight = Math.Max(
            options.HeaderFooterFontSize * 1.5d,
            1d);
        var style = new TextStyle(
            options.Theme.FontFamily,
            options.HeaderFooterFontSize,
            400,
            options.HeaderFooterColor,
            Wrap: false);
        if (header.Length > 0)
        {
            var y = Math.Clamp(
                setup.Margins.HeaderInches *
                SpreadsheetPageLayoutPlanner.DipsPerInch,
                0d,
                Math.Max(0d, page.PaperSizeDips.Height - lineHeight));
            builder.DrawText(
                header,
                new RectD(left, y, width, lineHeight),
                style);
        }
        if (footer.Length > 0)
        {
            var y = Math.Clamp(
                page.PaperSizeDips.Height -
                (setup.Margins.FooterInches *
                 SpreadsheetPageLayoutPlanner.DipsPerInch) -
                lineHeight,
                0d,
                Math.Max(0d, page.PaperSizeDips.Height - lineHeight));
            builder.DrawText(
                footer,
                new RectD(left, y, width, lineHeight),
                style);
        }
    }

    private static void ValidateOptions(
        SpreadsheetPrintDisplayListOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.Theme);
        ArgumentNullException.ThrowIfNull(options.Culture);
        if (!double.IsFinite(options.HeaderFooterFontSize) ||
            options.HeaderFooterFontSize <= 0d ||
            options.HeaderFooterFontSize > 200d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "HeaderFooterFontSize must be finite and between 0 and 200.");
        }
    }
}
