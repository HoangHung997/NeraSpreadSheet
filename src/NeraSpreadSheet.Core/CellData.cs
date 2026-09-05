namespace NeraSpreadSheet.Core;

public sealed record CellData
{
    public CellData(CellValue value, string? formula = null, int styleId = 0)
    {
        if (formula is not null && !formula.StartsWith('='))
        {
            throw new ArgumentException("Formula must start with '='.", nameof(formula));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(styleId);

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
