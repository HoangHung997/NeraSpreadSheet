using System.Globalization;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

public sealed record CellEditState(CellAddress Address, string InitialText);

public sealed class CellEditStateChangedEventArgs : EventArgs
{
    public CellEditStateChangedEventArgs(CellEditState? state) { State = state; }
    public CellEditState? State { get; }
}

public sealed class SpreadsheetCellEditorController
{
    private readonly SpreadsheetSession _session;

    public SpreadsheetCellEditorController(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public CellEditState? State { get; private set; }
    public bool IsEditing => State is not null;
    public event EventHandler<CellEditStateChangedEventArgs>? StateChanged;

    public CellEditState BeginEdit(CellAddress? address = null)
    {
        var target = _session.ActiveWorksheet.ResolveMergedAnchor(address ?? _session.Selection.ActiveCell);
        _session.Selection.SetActiveCell(target);
        var cell = _session.ActiveWorksheet.GetCell(target);
        var initialText = cell.Formula ?? cell.Value.ToString();
        State = new CellEditState(target, initialText);
        StateChanged?.Invoke(this, new CellEditStateChangedEventArgs(State));
        return State;
    }

    public bool Commit(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (State is not { } state)
        {
            return false;
        }

        if (text.StartsWith('='))
        {
            _session.SetFormula(state.Address, text);
        }
        else
        {
            _session.SetValue(state.Address, ParseLiteral(text));
        }

        State = null;
        StateChanged?.Invoke(this, new CellEditStateChangedEventArgs(null));
        return true;
    }

    public bool Cancel()
    {
        if (State is null)
        {
            return false;
        }
        State = null;
        StateChanged?.Invoke(this, new CellEditStateChangedEventArgs(null));
        return true;
    }

    private static object? ParseLiteral(string text)
    {
        if (text.Length == 0)
        {
            return null;
        }
        if (bool.TryParse(text, out var boolean))
        {
            return boolean;
        }
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.CurrentCulture, out var localNumber) && double.IsFinite(localNumber))
        {
            return localNumber;
        }
        if (double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var invariantNumber) && double.IsFinite(invariantNumber))
        {
            return invariantNumber;
        }
        return text;
    }
}
