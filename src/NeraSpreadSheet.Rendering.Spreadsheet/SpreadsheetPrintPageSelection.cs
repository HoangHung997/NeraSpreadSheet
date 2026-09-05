using System.Globalization;

namespace NeraSpreadSheet.Rendering.Spreadsheet;

public enum SpreadsheetPrintPageParity
{
    All,
    Odd,
    Even,
}

public readonly record struct SpreadsheetPrintPageRange
{
    public SpreadsheetPrintPageRange(
        int firstPageNumber,
        int lastPageNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(firstPageNumber);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            lastPageNumber,
            firstPageNumber);
        FirstPageNumber = firstPageNumber;
        LastPageNumber = lastPageNumber;
    }

    public int FirstPageNumber { get; }

    public int LastPageNumber { get; }

    public bool Contains(int pageNumber) =>
        pageNumber >= FirstPageNumber &&
        pageNumber <= LastPageNumber;
}

public sealed record SpreadsheetPrintPageSelection
{
    public IReadOnlyList<SpreadsheetPrintPageRange> Ranges { get; init; } = [];

    public SpreadsheetPrintPageParity Parity { get; init; } =
        SpreadsheetPrintPageParity.All;

    public bool ReverseOrder { get; init; }

    public static SpreadsheetPrintPageSelection All { get; } = new();

    public IReadOnlyList<int> ResolvePageIndexes(int totalPages)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalPages);
        if (!Enum.IsDefined(Parity))
        {
            throw new InvalidOperationException(
                "The print-page parity is not defined.");
        }
        ArgumentNullException.ThrowIfNull(Ranges);

        IEnumerable<int> pageNumbers;
        if (Ranges.Count == 0)
        {
            pageNumbers = Enumerable.Range(1, totalPages);
        }
        else
        {
            var selected = new SortedSet<int>();
            foreach (var range in Ranges)
            {
                if (range.FirstPageNumber > totalPages)
                {
                    continue;
                }
                var last = Math.Min(range.LastPageNumber, totalPages);
                for (var pageNumber = range.FirstPageNumber;
                     pageNumber <= last;
                     pageNumber++)
                {
                    selected.Add(pageNumber);
                }
            }
            pageNumbers = selected;
        }

        pageNumbers = Parity switch
        {
            SpreadsheetPrintPageParity.All => pageNumbers,
            SpreadsheetPrintPageParity.Odd =>
                pageNumbers.Where(static page => (page & 1) == 1),
            SpreadsheetPrintPageParity.Even =>
                pageNumbers.Where(static page => (page & 1) == 0),
            _ => throw new InvalidOperationException(
                "The print-page parity is not defined."),
        };
        if (ReverseOrder)
        {
            pageNumbers = pageNumbers.Reverse();
        }
        return pageNumbers
            .Select(static pageNumber => pageNumber - 1)
            .ToArray();
    }

    public static SpreadsheetPrintPageSelection Parse(
        string? expression,
        int totalPages,
        SpreadsheetPrintPageParity parity =
            SpreadsheetPrintPageParity.All,
        bool reverseOrder = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(totalPages);
        if (!Enum.IsDefined(parity))
        {
            throw new ArgumentOutOfRangeException(nameof(parity));
        }
        if (string.IsNullOrWhiteSpace(expression))
        {
            return new SpreadsheetPrintPageSelection
            {
                Parity = parity,
                ReverseOrder = reverseOrder,
            };
        }

        var ranges = new List<SpreadsheetPrintPageRange>();
        foreach (var rawTerm in expression.Split(','))
        {
            var term = rawTerm.Trim();
            if (term.Length == 0)
            {
                throw new FormatException(
                    "The page-range expression contains an empty term.");
            }
            var separator = term.IndexOf('-');
            if (separator < 0)
            {
                var page = ParsePositivePageNumber(term);
                ranges.Add(new SpreadsheetPrintPageRange(page, page));
                continue;
            }
            if (term.IndexOf('-', separator + 1) >= 0)
            {
                throw new FormatException(
                    "A page-range term may contain only one hyphen.");
            }

            var firstText = term[..separator].Trim();
            var lastText = term[(separator + 1)..].Trim();
            var first = firstText.Length == 0
                ? 1
                : ParsePositivePageNumber(firstText);
            var last = lastText.Length == 0
                ? totalPages
                : ParsePositivePageNumber(lastText);
            if (last < first)
            {
                throw new FormatException(
                    "A page range cannot end before it starts.");
            }
            ranges.Add(new SpreadsheetPrintPageRange(first, last));
        }

        return new SpreadsheetPrintPageSelection
        {
            Ranges = ranges,
            Parity = parity,
            ReverseOrder = reverseOrder,
        };
    }

    private static int ParsePositivePageNumber(string value)
    {
        if (!int.TryParse(
                value,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var pageNumber) ||
            pageNumber <= 0)
        {
            throw new FormatException(
                $"'{value}' is not a positive page number.");
        }
        return pageNumber;
    }
}

public sealed record SpreadsheetPrintTicket
{
    public SpreadsheetPrintPageSelection Selection { get; init; } =
        SpreadsheetPrintPageSelection.All;

    public int Copies { get; init; } = 1;

    public bool Collate { get; init; } = true;
}

public readonly record struct SpreadsheetPrintPageInvocation(
    int SequenceNumber,
    int CopyNumber,
    int PageIndex,
    int PageNumber);

public static class SpreadsheetPrintTicketPlanner
{
    public const int MaximumPageInvocations = 1_000_000;

    public static IReadOnlyList<SpreadsheetPrintPageInvocation> CreateSequence(
        SpreadsheetPageLayoutPlan plan,
        SpreadsheetPrintTicket? ticket = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ticket ??= new SpreadsheetPrintTicket();
        ArgumentNullException.ThrowIfNull(ticket.Selection);
        if (ticket.Copies <= 0 || ticket.Copies > 999)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ticket),
                "Copies must be between 1 and 999.");
        }
        if (plan.Pages.Count == 0)
        {
            return [];
        }

        var pageIndexes = ticket.Selection.ResolvePageIndexes(
            plan.Pages.Count);
        var invocationCount = checked((long)pageIndexes.Count * ticket.Copies);
        if (invocationCount > MaximumPageInvocations)
        {
            throw new InvalidOperationException(
                $"The print ticket exceeds the invocation limit of " +
                $"{MaximumPageInvocations:N0}.");
        }

        var result = new List<SpreadsheetPrintPageInvocation>(
            checked((int)invocationCount));
        var sequenceNumber = 1;
        if (ticket.Collate)
        {
            for (var copyNumber = 1;
                 copyNumber <= ticket.Copies;
                 copyNumber++)
            {
                foreach (var pageIndex in pageIndexes)
                {
                    result.Add(new SpreadsheetPrintPageInvocation(
                        sequenceNumber++,
                        copyNumber,
                        pageIndex,
                        plan.Pages[pageIndex].PageNumber));
                }
            }
        }
        else
        {
            foreach (var pageIndex in pageIndexes)
            {
                for (var copyNumber = 1;
                     copyNumber <= ticket.Copies;
                     copyNumber++)
                {
                    result.Add(new SpreadsheetPrintPageInvocation(
                        sequenceNumber++,
                        copyNumber,
                        pageIndex,
                        plan.Pages[pageIndex].PageNumber));
                }
            }
        }
        return result;
    }
}
