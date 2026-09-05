using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>Validates that a product capability is registered and reachable from Ribbon chrome.</summary>
public static class RibbonCommandCatalogAudit
{
    public static void Validate(
        CommandRegistry registry,
        RibbonDefinition definition,
        IEnumerable<CommandId> productionCapabilities)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(definition);
        var expected = (productionCapabilities ??
            throw new ArgumentNullException(nameof(productionCapabilities)))
            .Distinct()
            .ToArray();
        var reachable = definition.Tabs
            .SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items)
            .Where(static item => item.Kind != RibbonItemKind.Separator)
            .Select(static item => item.CommandId)
            .Concat(definition.QuickAccessToolbar.Select(static item => item.CommandId))
            .Concat(definition.Backstage.Select(static item => item.CommandId))
            .ToHashSet();

        var missingRegistrations = expected.Where(commandId =>
            !registry.TryResolve(commandId, out _, out _)).ToArray();
        var missingPlacements = expected.Where(commandId => !reachable.Contains(commandId)).ToArray();
        if (missingRegistrations.Length == 0 && missingPlacements.Length == 0)
        {
            return;
        }

        throw new InvalidOperationException(string.Join(" ", new[]
        {
            missingRegistrations.Length == 0 ? null :
                $"Unregistered production command(s): {string.Join(", ", missingRegistrations)}.",
            missingPlacements.Length == 0 ? null :
                $"Production command(s) absent from Ribbon catalog: {string.Join(", ", missingPlacements)}.",
        }.Where(static message => message is not null)));
    }

    /// <summary>
    /// Validates an exact production registry snapshot as well as Ribbon reachability.
    /// This overload is intended for the product integration gate; hosts that register
    /// additional application commands should continue to use <see cref="Validate"/>.
    /// </summary>
    public static void ValidateExact(
        CommandRegistry registry,
        RibbonDefinition definition,
        IEnumerable<CommandId> productionCapabilities)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(definition);
        var expected = (productionCapabilities ??
            throw new ArgumentNullException(nameof(productionCapabilities)))
            .ToHashSet();
        Validate(registry, definition, expected);

        var unexpectedRegistrations = registry.RegisteredCommandIds
            .Where(commandId => !expected.Contains(commandId))
            .OrderBy(static commandId => commandId.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unexpectedRegistrations.Length > 0)
        {
            throw new InvalidOperationException(
                $"Registered production command(s) absent from the audited manifest: " +
                $"{string.Join(", ", unexpectedRegistrations)}.");
        }
    }
}
