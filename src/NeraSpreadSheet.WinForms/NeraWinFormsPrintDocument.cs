using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.WinForms;

/// <summary>
/// WinForms/GDI printer document backed by the shared page plan, print ticket
/// and spreadsheet display-list composer. Physical printer capability and hard
/// margin validation remain the responsibility of the selected PrintController.
/// </summary>
public sealed class NeraWinFormsPrintDocument : PrintDocument
{
    private readonly WorksheetSnapshot _worksheet;
    private readonly SpreadsheetPageLayoutPlan _plan;
    private readonly CellStyleCatalog? _styles;
    private readonly SpreadsheetPrintDisplayListOptions _displayListOptions;
    private readonly SpreadsheetPrintPageInvocation[] _invocations;
    private readonly WinFormsDisplayListRenderer _renderer = new();
    private int _nextInvocationIndex;

    public NeraWinFormsPrintDocument(
        WorksheetSnapshot worksheet,
        SpreadsheetPageLayoutPlan plan,
        SpreadsheetPrintTicket? ticket = null,
        CellStyleCatalog? styles = null,
        SpreadsheetPrintDisplayListOptions? displayListOptions = null)
    {
        _worksheet = worksheet ??
            throw new ArgumentNullException(nameof(worksheet));
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _styles = styles;
        _displayListOptions = displayListOptions ??
            new SpreadsheetPrintDisplayListOptions();
        _invocations = SpreadsheetPrintTicketPlanner.CreateSequence(
                plan,
                ticket)
            .ToArray();
        if (_invocations.Length == 0)
        {
            throw new InvalidOperationException(
                "The WinForms print document does not contain a selected page.");
        }

        DocumentName = worksheet.Name;
        OriginAtMargins = false;
        DefaultPageSettings.Landscape = false;
        DefaultPageSettings.PaperSize = new PaperSize(
            plan.Setup.PaperSize.Name,
            ToHundredthsOfInch(plan.PaperSizeDips.Width),
            ToHundredthsOfInch(plan.PaperSizeDips.Height));
        DefaultPageSettings.Margins = new Margins(
            ToHundredthsOfInch(
                plan.Setup.Margins.LeftInches *
                SpreadsheetPageLayoutPlanner.DipsPerInch),
            ToHundredthsOfInch(
                plan.Setup.Margins.RightInches *
                SpreadsheetPageLayoutPlanner.DipsPerInch),
            ToHundredthsOfInch(
                plan.Setup.Margins.TopInches *
                SpreadsheetPageLayoutPlanner.DipsPerInch),
            ToHundredthsOfInch(
                plan.Setup.Margins.BottomInches *
                SpreadsheetPageLayoutPlanner.DipsPerInch));
    }

    public IReadOnlyList<SpreadsheetPrintPageInvocation> Invocations =>
        _invocations;

    public SpreadsheetPrintJobPage ComposePage(int sequenceIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sequenceIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            sequenceIndex,
            _invocations.Length);
        var invocation = _invocations[sequenceIndex];
        var composed = SpreadsheetPrintDisplayListComposer.Compose(
            _worksheet,
            _plan,
            invocation.PageIndex,
            _styles,
            _displayListOptions);
        return new SpreadsheetPrintJobPage(
            invocation,
            composed.Page,
            composed.DisplayList);
    }

    protected override void OnBeginPrint(PrintEventArgs e)
    {
        _nextInvocationIndex = 0;
        base.OnBeginPrint(e);
    }

    protected override void OnPrintPage(PrintPageEventArgs e)
    {
        if (_nextInvocationIndex >= _invocations.Length)
        {
            e.HasMorePages = false;
            return;
        }

        var page = ComposePage(_nextInvocationIndex);
        var graphics = e.Graphics;
        var state = graphics.Save();
        try
        {
            graphics.PageUnit = GraphicsUnit.Pixel;
            graphics.PageScale = 1f;
            using var transform = new Matrix(
                graphics.DpiX / 96f,
                0f,
                0f,
                graphics.DpiY / 96f,
                0f,
                0f);
            graphics.Transform = transform;
            _renderer.Render(graphics, page.DisplayList);
        }
        finally
        {
            graphics.Restore(state);
        }

        _nextInvocationIndex++;
        e.HasMorePages = _nextInvocationIndex < _invocations.Length;
        base.OnPrintPage(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _renderer.Dispose();
        }
        base.Dispose(disposing);
    }

    private static int ToHundredthsOfInch(double dips)
    {
        if (!double.IsFinite(dips) || dips < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(dips));
        }
        return checked((int)Math.Round(
            dips * 100d / SpreadsheetPageLayoutPlanner.DipsPerInch,
            MidpointRounding.AwayFromZero));
    }
}
