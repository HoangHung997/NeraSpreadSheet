namespace NeraSpreadSheet.Bars.Core;

/// <summary>
/// Defines host-neutral visibility and order overrides for a bar item tree.
/// Unknown target ids are ignored so optional commands can be added or removed.
/// </summary>
public sealed class BarCustomization
{
    public BarCustomization(
        string barId,
        IEnumerable<BarItemCustomization> items)
    {
        BarId = BarCustomizationValidation.RequiredId(barId, nameof(barId));
        Items = BarCustomizationValidation.MaterializeUnique(
            items,
            $"bar item customization in '{BarId}'");
    }

    public string BarId { get; }

    public IReadOnlyList<BarItemCustomization> Items { get; }

    /// <summary>
    /// Applies the overrides without mutating the source definition.
    /// </summary>
    public BarDefinition ApplyTo(BarDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!string.Equals(BarId, definition.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Bar customization '{BarId}' cannot be applied to bar '{definition.Id}'.");
        }

        return new BarDefinition(
            definition.Id,
            definition.Kind,
            ApplyItems(definition.Items, Items),
            definition.Caption);
    }

    private static List<BarItemDefinition> ApplyItems(
        IReadOnlyList<BarItemDefinition> source,
        IReadOnlyList<BarItemCustomization> customizations)
    {
        var overrides = customizations.ToDictionary(
            customization => customization.ItemId,
            StringComparer.OrdinalIgnoreCase);
        var result = new List<BarItemDefinition>(source.Count);

        foreach (var item in source)
        {
            BarItemCustomization? customization = null;
            if (item.Id is not null)
            {
                overrides.TryGetValue(item.Id, out customization);
            }

            if (customization is { IsVisible: false })
            {
                continue;
            }

            var order = customization?.Order ?? item.Order;
            result.Add(item.Kind switch
            {
                BarItemKind.Command => BarItemDefinition.Command(
                    item.CommandId!.Value,
                    item.Id,
                    order),
                BarItemKind.Separator => BarItemDefinition.Separator(
                    item.Id,
                    order),
                BarItemKind.Submenu => BarItemDefinition.Submenu(
                    item.Caption!,
                    ApplyItems(item.Children, customization?.Children ?? []),
                    item.Id,
                    order),
                _ => throw new InvalidOperationException(
                    $"Unsupported bar item kind '{item.Kind}'."),
            });
        }

        return result;
    }
}

public sealed class BarItemCustomization
{
    /// <summary>
    /// Creates overrides scoped to one stable bar item id and its optional children.
    /// </summary>
    public BarItemCustomization(
        string itemId,
        bool isVisible = true,
        int? order = null,
        IEnumerable<BarItemCustomization>? children = null)
    {
        ItemId = BarCustomizationValidation.RequiredId(itemId, nameof(itemId));
        IsVisible = isVisible;
        Order = order;
        Children = BarCustomizationValidation.MaterializeUnique(
            children ?? [],
            $"child bar item customization in '{ItemId}'");
    }

    public string ItemId { get; }

    public bool IsVisible { get; }

    public int? Order { get; }

    public IReadOnlyList<BarItemCustomization> Children { get; }
}

internal static class BarCustomizationValidation
{
    public static string RequiredId(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A customization target id is required.", parameterName);
        }

        return value.Trim();
    }

    public static IReadOnlyList<BarItemCustomization> MaterializeUnique(
        IEnumerable<BarItemCustomization> values,
        string scope)
    {
        ArgumentNullException.ThrowIfNull(values);

        var materialized = values.ToArray();
        string[] duplicates = materialized
            .GroupBy(value => value.ItemId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate {scope} id(s): {string.Join(", ", duplicates)}.");
        }

        return materialized;
    }

    public static void ValidateDefinitionItems(
        IReadOnlyList<BarItemDefinition> items,
        string scope)
    {
        string[] duplicates = items
            .Where(item => item.Id is not null)
            .GroupBy(item => item.Id!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate bar item id(s) in '{scope}': {string.Join(", ", duplicates)}.");
        }

        foreach (var submenu in items.Where(item => item.Kind == BarItemKind.Submenu))
        {
            ValidateDefinitionItems(
                submenu.Children,
                submenu.Id ?? submenu.Caption ?? "submenu");
        }
    }
}
