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
}
