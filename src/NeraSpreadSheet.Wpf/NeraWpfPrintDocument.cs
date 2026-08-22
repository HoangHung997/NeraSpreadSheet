using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// WPF document source backed by the shared page plan, print ticket and
/// spreadsheet display-list composer. It can be passed directly to
/// PrintDialog.PrintDocument without reimplementing cell rendering.
/// </summary>
public sealed class NeraWpfPrintDocument : IDocumentPaginatorSource
{
    public NeraWpfPrintDocument(
        WorksheetSnapshot worksheet,
        SpreadsheetPageLayoutPlan plan,
        SpreadsheetPrintTicket? ticket = null,
        CellStyleCatalog? styles = null,
        SpreadsheetPrintDisplayListOptions? displayListOptions = null)
    {
        Paginator = new NeraWpfPrintPaginator(
            this,
            worksheet,
            plan,
            ticket,
            styles,
            displayListOptions);
    }

    public NeraWpfPrintPaginator Paginator { get; }

    DocumentPaginator IDocumentPaginatorSource.DocumentPaginator =>
        Paginator;
}

public sealed class NeraWpfPrintPaginator : DocumentPaginator
{
    private readonly IDocumentPaginatorSource _source;
    private readonly WorksheetSnapshot _worksheet;
    private readonly SpreadsheetPageLayoutPlan _plan;
    private readonly CellStyleCatalog? _styles;
    private readonly SpreadsheetPrintDisplayListOptions _displayListOptions;
    private readonly SpreadsheetPrintPageInvocation[] _invocations;
    private readonly WpfDisplayListRenderer _renderer = new();
    private Size _pageSize;

    internal NeraWpfPrintPaginator(
        IDocumentPaginatorSource source,
        WorksheetSnapshot worksheet,
        SpreadsheetPageLayoutPlan plan,
        SpreadsheetPrintTicket? ticket,
        CellStyleCatalog? styles,
        SpreadsheetPrintDisplayListOptions? displayListOptions)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
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
                "The WPF print document does not contain a selected page.");
        }
        _pageSize = new Size(
            plan.PaperSizeDips.Width,
            plan.PaperSizeDips.Height);
    }

    public IReadOnlyList<SpreadsheetPrintPageInvocation> Invocations =>
        _invocations;

    public override bool IsPageCountValid => true;

    public override int PageCount => _invocations.Length;

    public override Size PageSize
    {
        get => _pageSize;
        set
        {
            if (!double.IsFinite(value.Width) || value.Width <= 0d ||
                !double.IsFinite(value.Height) || value.Height <= 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            _pageSize = value;
        }
    }

    public override IDocumentPaginatorSource Source => _source;

    public override DocumentPage GetPage(int pageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(pageNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(
            pageNumber,
            _invocations.Length);
        var invocation = _invocations[pageNumber];
        var composed = SpreadsheetPrintDisplayListComposer.Compose(
            _worksheet,
            _plan,
            invocation.PageIndex,
            _styles,
            _displayListOptions);
        var sourceSize = composed.Page.PaperSizeDips;
        var scale = Math.Min(
            _pageSize.Width / sourceSize.Width,
            _pageSize.Height / sourceSize.Height);
        var offsetX = (_pageSize.Width - (sourceSize.Width * scale)) / 2d;
        var offsetY = (_pageSize.Height - (sourceSize.Height * scale)) / 2d;
        var visual = new DrawingVisual();
        using (var drawingContext = visual.RenderOpen())
        {
            drawingContext.PushClip(new RectangleGeometry(
                new Rect(0d, 0d, _pageSize.Width, _pageSize.Height)));
            var transform = new MatrixTransform(new Matrix(
                scale,
                0d,
                0d,
                scale,
                offsetX,
                offsetY));
            transform.Freeze();
            drawingContext.PushTransform(transform);
            _renderer.Render(
                drawingContext,
                composed.DisplayList,
                pixelsPerDip: 1d);
            drawingContext.Pop();
            drawingContext.Pop();
        }

        var printable = composed.Page.PrintableBoundsDips;
        var contentBox = new Rect(
            offsetX + (printable.X * scale),
            offsetY + (printable.Y * scale),
            printable.Width * scale,
            printable.Height * scale);
        var pageBounds = new Rect(
            0d,
            0d,
            _pageSize.Width,
            _pageSize.Height);
        return new DocumentPage(
            visual,
            _pageSize,
            pageBounds,
            contentBox);
    }
}
