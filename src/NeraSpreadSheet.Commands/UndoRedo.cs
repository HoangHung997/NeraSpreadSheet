namespace NeraSpreadSheet.Commands;

public interface IUndoableOperation
{
    string Description { get; }

    void Execute();

    void Undo();
}

public sealed class CompositeUndoableOperation : IUndoableOperation
{
    private readonly IUndoableOperation[] _operations;

    public CompositeUndoableOperation(
        string description,
        IEnumerable<IUndoableOperation> operations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(operations);
        Description = description.Trim();
        _operations = operations.ToArray();
        if (_operations.Length == 0)
        {
            throw new ArgumentException(
                "At least one operation is required.",
                nameof(operations));
        }
    }

    public string Description { get; }

    public void Execute()
    {
        var executed = 0;
        try
        {
            for (; executed < _operations.Length; executed++)
            {
                _operations[executed].Execute();
            }
        }
        catch
        {
            for (var index = executed - 1; index >= 0; index--)
            {
                _operations[index].Undo();
            }
            throw;
        }
    }

    public void Undo()
    {
        for (var index = _operations.Length - 1; index >= 0; index--)
        {
            _operations[index].Undo();
        }
    }
}

public sealed class UndoRedoManager
{
    private readonly Stack<IUndoableOperation> _undo = new();
    private readonly Stack<IUndoableOperation> _redo = new();
    private readonly int _maximumDepth;

    public UndoRedoManager(int maximumDepth = 256)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumDepth);
        _maximumDepth = maximumDepth;
    }

    public int UndoCount => _undo.Count;

    public int RedoCount => _redo.Count;

    public bool CanUndo => _undo.Count > 0;

    public bool CanRedo => _redo.Count > 0;

    public string? NextUndoDescription =>
        _undo.TryPeek(out var operation)
            ? operation.Description
            : null;

    public string? NextRedoDescription =>
        _redo.TryPeek(out var operation)
            ? operation.Description
            : null;

    public void Execute(IUndoableOperation operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        operation.Execute();
        _undo.Push(operation);
        _redo.Clear();
        TrimUndoHistory();
    }

    public bool Undo() => TryUndo(out _);

    public bool TryUndo(out IUndoableOperation? operation)
    {
        if (!_undo.TryPop(out var candidate))
        {
            operation = null;
            return false;
        }

        try
        {
            candidate.Undo();
        }
        catch
        {
            _undo.Push(candidate);
            throw;
        }

        _redo.Push(candidate);
        operation = candidate;
        return true;
    }

    public bool Redo() => TryRedo(out _);

    public bool TryRedo(out IUndoableOperation? operation)
    {
        if (!_redo.TryPop(out var candidate))
        {
            operation = null;
            return false;
        }

        try
        {
            candidate.Execute();
        }
        catch
        {
            _redo.Push(candidate);
            throw;
        }

        _undo.Push(candidate);
        TrimUndoHistory();
        operation = candidate;
        return true;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private void TrimUndoHistory()
    {
        if (_undo.Count <= _maximumDepth)
        {
            return;
        }

        var keep = _undo.Take(_maximumDepth).Reverse().ToArray();
        _undo.Clear();
        foreach (var operation in keep)
        {
            _undo.Push(operation);
        }
    }
}
