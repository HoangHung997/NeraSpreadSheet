using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Formulas.Tests;

internal sealed class FormulaSurfaceTestContext :
    IFormulaClockEvaluationContext
{
    private readonly IReadOnlyDictionary<CellAddress, CellValue> _values;

    public FormulaSurfaceTestContext(
        IReadOnlyDictionary<CellAddress, CellValue>? values = null,
        DateTime? currentDateTime = null)
    {
        _values = values ??
            new Dictionary<CellAddress, CellValue>();
        CurrentDateTime = currentDateTime ??
            new DateTime(2026, 8, 23, 14, 30, 45);
    }

    public DateTime CurrentDateTime { get; }

    public CellValue GetCellValue(
        string? worksheetName,
        CellAddress address) =>
        _values.GetValueOrDefault(address, CellValue.Blank);
}
