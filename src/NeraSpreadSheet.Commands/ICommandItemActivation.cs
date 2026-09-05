namespace NeraSpreadSheet.Commands;

/// <summary>
/// Describes a selected value from composite command chrome without coupling a
/// command handler to a particular Ribbon or platform presenter.
/// </summary>
public interface ICommandItemActivation
{
    /// <summary>Gets the stable selected value, or <see langword="null"/>.</summary>
    string? SelectedValue { get; }

    /// <summary>Gets the original parameter supplied by the host.</summary>
    object? OriginalParameter { get; }
}
