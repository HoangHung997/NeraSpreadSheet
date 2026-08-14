namespace NeraSpreadSheet.Core;

public sealed record CellData
{
    public CellData(CellValue value, string? formula = null, int styleId = 0)
    {
        if (formula is not null && !formula.StartsWith("=", StringComparison.Ordinal))
        {
            throw new ArgumentException("Formula must start with '='.", nameof(formula));
        }

        if (styleId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(styleId));
        }

        Value = value;
        Formula = formula;
        StyleId = styleId;
    }

    public CellValue Value { get; }

    public string? Formula { get; }

    public int StyleId { get; }

    public bool IsEmpty => Value.IsBlank && Formula is null && StyleId == 0;

    public static CellData Empty { get; } = new(CellValue.Blank);
}
