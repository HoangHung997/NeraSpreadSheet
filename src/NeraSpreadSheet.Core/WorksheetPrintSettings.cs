namespace NeraSpreadSheet.Core;

public enum SpreadsheetPageOrientation
{
    Portrait,
    Landscape,
}

public readonly record struct SpreadsheetPaperSize
{
    public static SpreadsheetPaperSize A4 { get; } =
        new(8.2677165354d, 11.6929133858d, "A4");

    public static SpreadsheetPaperSize A3 { get; } =
        new(11.6929133858d, 16.5354330709d, "A3");

    public static SpreadsheetPaperSize Letter { get; } =
        new(8.5d, 11d, "Letter");

    public static SpreadsheetPaperSize Legal { get; } =
        new(8.5d, 14d, "Legal");

    public SpreadsheetPaperSize(
        double widthInches,
        double heightInches,
        string? name = null)
    {
        if (!double.IsFinite(widthInches) || widthInches <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(widthInches));
        }
        if (!double.IsFinite(heightInches) || heightInches <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(heightInches));
        }

        WidthInches = widthInches;
        HeightInches = heightInches;
        Name = string.IsNullOrWhiteSpace(name)
            ? "Custom"
            : name.Trim();
    }

    public double WidthInches { get; }

    public double HeightInches { get; }

    public string Name { get; }
}

public readonly record struct SpreadsheetPageMargins
{
    public static SpreadsheetPageMargins Normal { get; } =
        new(0.7d, 0.7d, 0.75d, 0.75d, 0.3d, 0.3d);

    public static SpreadsheetPageMargins Narrow { get; } =
        new(0.25d, 0.25d, 0.75d, 0.75d, 0.3d, 0.3d);

    public SpreadsheetPageMargins(
        double leftInches,
        double rightInches,
        double topInches,
        double bottomInches,
        double headerInches = 0d,
        double footerInches = 0d)
    {
        Validate(leftInches, nameof(leftInches));
        Validate(rightInches, nameof(rightInches));
        Validate(topInches, nameof(topInches));
        Validate(bottomInches, nameof(bottomInches));
        Validate(headerInches, nameof(headerInches));
        Validate(footerInches, nameof(footerInches));

        LeftInches = leftInches;
        RightInches = rightInches;
        TopInches = topInches;
        BottomInches = bottomInches;
        HeaderInches = headerInches;
        FooterInches = footerInches;
    }

    public double LeftInches { get; }

    public double RightInches { get; }

    public double TopInches { get; }

    public double BottomInches { get; }

    public double HeaderInches { get; }

    public double FooterInches { get; }

    private static void Validate(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value < 0d)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}

public readonly record struct SpreadsheetRepeatTitles
{
    public SpreadsheetRepeatTitles(
        CellRange? rows = null,
        CellRange? columns = null)
    {
        Rows = rows;
        Columns = columns;
    }

    public CellRange? Rows { get; }

    public CellRange? Columns { get; }
}

public sealed record SpreadsheetPageSetup
{
    public SpreadsheetPaperSize PaperSize { get; init; } =
        SpreadsheetPaperSize.A4;

    public SpreadsheetPageOrientation Orientation { get; init; } =
        SpreadsheetPageOrientation.Portrait;

    public SpreadsheetPageMargins Margins { get; init; } =
        SpreadsheetPageMargins.Normal;

    public double ScalePercent { get; init; } = 100d;

    public int? FitToPagesWide { get; init; }

    public int? FitToPagesTall { get; init; }

    public SpreadsheetRepeatTitles RepeatTitles { get; init; }

    public IReadOnlyList<int> ManualRowBreaks { get; init; } = [];

    public IReadOnlyList<int> ManualColumnBreaks { get; init; } = [];

    public bool CenterHorizontally { get; init; }

    public bool CenterVertically { get; init; }

    public bool PrintGridlines { get; init; }

    public bool PrintHeadings { get; init; }

    public string? OddHeader { get; init; }

    public string? OddFooter { get; init; }

    public SpreadsheetPageSetup Copy() => this with
    {
        ManualRowBreaks = [.. ManualRowBreaks],
        ManualColumnBreaks = [.. ManualColumnBreaks],
    };
}

public sealed record WorksheetPrintSettings
{
    public CellRange? PrintArea { get; init; }

    public SpreadsheetPageSetup PageSetup { get; init; } = new();

    public WorksheetPrintSettings Copy() => this with
    {
        PageSetup = PageSetup.Copy(),
    };
}
