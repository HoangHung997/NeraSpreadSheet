using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public interface ISpreadsheetEditOperation : IUndoableOperation
{
    Worksheet Worksheet { get; }
    CellRange AffectedRange { get; }
}

public sealed class SetCellsOperation : ISpreadsheetEditOperation
{
    private readonly KeyValuePair<CellAddress, CellData>[] _updates;
    private KeyValuePair<CellAddress, CellData>[]? _originals;

    public SetCellsOperation(Worksheet worksheet, IEnumerable<KeyValuePair<CellAddress, CellData>> updates, string description = "Edit cells")
    {
        Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
        ArgumentNullException.ThrowIfNull(updates);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        Description = description.Trim();

        var requested = new Dictionary<CellAddress, CellData>();
        foreach (var pair in updates)
        {
            ArgumentNullException.ThrowIfNull(pair.Value);
            requested[pair.Key] = pair.Value;
        }

        if (requested.Count == 0)
        {
            throw new ArgumentException("At least one cell update is required.", nameof(updates));
        }

        _updates = requested.ToArray();
        AffectedRange = CalculateRange(_updates);
    }

    public string Description { get; }
    public Worksheet Worksheet { get; }
    public CellRange AffectedRange { get; }

    public void Execute()
    {
        _originals ??= _updates.Select(pair => new KeyValuePair<CellAddress, CellData>(pair.Key, Worksheet.GetCell(pair.Key))).ToArray();
        Worksheet.SetCells(_updates);
    }

    public void Undo()
    {
        if (_originals is null)
        {
            throw new InvalidOperationException("The operation has not been executed yet.");
        }
        Worksheet.SetCells(_originals);
    }

    private static CellRange CalculateRange(KeyValuePair<CellAddress, CellData>[] updates)
    {
        var top = updates.Min(pair => pair.Key.RowIndex);
        var left = updates.Min(pair => pair.Key.ColumnIndex);
        var bottom = updates.Max(pair => pair.Key.RowIndex);
        var right = updates.Max(pair => pair.Key.ColumnIndex);
        return new CellRange(new CellAddress(top, left), new CellAddress(bottom, right));
    }
}
