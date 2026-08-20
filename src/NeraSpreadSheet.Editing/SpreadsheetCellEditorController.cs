using System.Globalization;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Formulas;

namespace NeraSpreadSheet.Editing;

public sealed record CellEditState(
    CellAddress Address,
    string InitialText);

public sealed class CellEditStateChangedEventArgs : EventArgs
{
    public CellEditStateChangedEventArgs(CellEditState? state)
    {
        State = state;
    }

    public CellEditState? State { get; }
}

public sealed class CellValidationFailedEventArgs : EventArgs
{
    public CellValidationFailedEventArgs(
        CellAddress address,
        DataValidationEvaluationResult result)
    {
        Address = address;
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    public CellAddress Address { get; }

    public DataValidationEvaluationResult Result { get; }
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

    public DataValidationEvaluationResult? LastValidationResult
    {
        get;
        private set;
    }

    public DataValidationInputMessage? CurrentInputMessage =>
        State is { } state
            ? _session.Validation.GetInputMessage(state.Address)
            : null;

    public event EventHandler<CellEditStateChangedEventArgs>? StateChanged;

    public event EventHandler<CellValidationFailedEventArgs>? ValidationFailed;

    public CellEditState BeginEdit(CellAddress? address = null)
    {
        var target = _session.ActiveWorksheet.ResolveMergedAnchor(
            address ?? _session.Selection.ActiveCell);
        _session.Selection.SetActiveCell(target);
        var cell = _session.ActiveWorksheet.GetCell(target);
        var initialText = cell.Formula ?? cell.Value.ToString();
        LastValidationResult = null;
        State = new CellEditState(target, initialText);
        StateChanged?.Invoke(
            this,
            new CellEditStateChangedEventArgs(State));
        return State;
    }

    public bool Commit(string text) =>
        Commit(text, acceptValidationWarning: false);

    public bool Commit(
        string text,
        bool acceptValidationWarning)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (State is not { } state)
        {
            return false;
        }

        object? literal = null;
        var validation = text.StartsWith('=')
            ? _session.Validation.ValidateFormula(state.Address, text)
            : _session.Validation.ValidateValue(
                state.Address,
                literal = ParseLiteral(text));
        LastValidationResult = validation;
        if (!validation.IsValid)
        {
            ValidationFailed?.Invoke(
                this,
                new CellValidationFailedEventArgs(
                    state.Address,
                    validation));
            if (validation.ErrorStyle == DataValidationErrorStyle.Stop ||
                !acceptValidationWarning)
            {
                return false;
            }
        }

        if (text.StartsWith('='))
        {
            _session.SetFormula(state.Address, text);
        }
        else
        {
            _session.SetValue(state.Address, literal);
        }

        LastValidationResult = null;
        State = null;
        StateChanged?.Invoke(
            this,
            new CellEditStateChangedEventArgs(null));
        return true;
    }

    public bool Cancel()
    {
        if (State is null)
        {
            return false;
        }

        LastValidationResult = null;
        State = null;
        StateChanged?.Invoke(
            this,
            new CellEditStateChangedEventArgs(null));
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
        if (double.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.CurrentCulture,
                out var localNumber) &&
            double.IsFinite(localNumber))
        {
            return localNumber;
        }
        if (double.TryParse(
                text,
                NumberStyles.Float | NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var invariantNumber) &&
            double.IsFinite(invariantNumber))
        {
            return invariantNumber;
        }
        if (DateTime.TryParse(
                text,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var dateTime))
        {
            return dateTime;
        }
        return text;
    }
}
