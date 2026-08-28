namespace NeraSpreadSheet.Bars.Core;

/// <summary>
/// Identifies one customizable bar item through its stable ancestor ids.
/// </summary>
public sealed class BarCustomizationTarget
{
    public BarCustomizationTarget(IEnumerable<string> itemIds)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        ItemIds = itemIds
            .Select((id, index) => BarCustomizationValidation.RequiredId(
                id,
                $"itemIds[{index}]"))
            .ToArray();
        if (ItemIds.Count == 0)
        {
            throw new ArgumentException("At least one bar item id is required.", nameof(itemIds));
        }
    }

    public IReadOnlyList<string> ItemIds { get; }
}

public sealed record BarCustomizationEntry(
    BarCustomizationTarget Target,
    string Caption,
    int Depth,
    BarItemKind Kind,
    bool IsVisible);

/// <summary>
/// Edits stable bar items without mutating the application definition.
/// </summary>
public sealed class BarCustomizationSession
{
    private readonly BarDefinition _definition;
    private readonly Func<BarItemDefinition, string> _caption;
    private readonly List<MutableItem> _items = [];
    private List<BarItemCustomization> _unknownItems = [];

    public BarCustomizationSession(
        BarDefinition definition,
        BarCustomization? customization = null,
        Func<BarItemDefinition, string>? caption = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        if (customization is not null &&
            !string.Equals(
                customization.BarId,
                definition.Id,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Bar customization '{customization.BarId}' cannot edit bar '{definition.Id}'.");
        }

        _caption = caption ?? (static item =>
            item.Caption ?? item.CommandId?.Value ?? item.Id ?? "—");
        Load(customization);
    }

    public IReadOnlyList<BarCustomizationEntry> Entries
    {
        get
        {
            var result = new List<BarCustomizationEntry>();
            AppendEntries(_items, [], 0, result);
            return result;
        }
    }

    public bool SetVisible(BarCustomizationTarget target, bool isVisible)
    {
        var item = Find(target);
        if (item.IsVisible == isVisible)
        {
            return false;
        }

        item.IsVisible = isVisible;
        return true;
    }

    public bool Move(BarCustomizationTarget target, int offset)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (offset == 0)
        {
            return false;
        }

        List<MutableItem> siblings = target.ItemIds.Count == 1
            ? _items
            : Find(new BarCustomizationTarget(target.ItemIds.Take(
                target.ItemIds.Count - 1))).Children;
        var itemId = target.ItemIds[^1];
        var sourceIndex = siblings.FindIndex(item => EqualsId(item.Id, itemId));
        if (sourceIndex < 0)
        {
            throw new InvalidOperationException("The customization target no longer exists.");
        }

        var destinationIndex = Math.Clamp(sourceIndex + offset, 0, siblings.Count - 1);
        if (sourceIndex == destinationIndex)
        {
            return false;
        }

        var item = siblings[sourceIndex];
        siblings.RemoveAt(sourceIndex);
        siblings.Insert(destinationIndex, item);
        return true;
    }

    public BarCustomization CreateCustomization() =>
        new(
            _definition.Id,
            _items.Select((item, order) => item.CreateCustomization(order))
                .Concat(_unknownItems));

    public BarDefinition Apply() => CreateCustomization().ApplyTo(_definition);

    public void Reset()
    {
        _unknownItems = [];
        Load(customization: null);
    }

    private void Load(BarCustomization? customization)
    {
        _items.Clear();
        var overrides = (customization?.Items ?? [])
            .ToDictionary(item => item.ItemId, StringComparer.OrdinalIgnoreCase);
        foreach (var definition in _definition.Items.Where(item => item.Id is not null))
        {
            overrides.TryGetValue(definition.Id!, out var itemOverride);
            _items.Add(new MutableItem(definition, itemOverride));
        }

        StableSort(_items);
        _unknownItems = (customization?.Items ?? [])
            .Where(item => !_definition.Items.Any(
                definition => EqualsId(definition.Id, item.ItemId)))
            .ToList();
    }

    private MutableItem Find(BarCustomizationTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        IReadOnlyList<MutableItem> candidates = _items;
        MutableItem? match = null;
        foreach (var itemId in target.ItemIds)
        {
            match = candidates.Single(item => EqualsId(item.Id, itemId));
            candidates = match.Children;
        }

        return match!;
    }

    private void AppendEntries(
        IReadOnlyList<MutableItem> items,
        IReadOnlyList<string> ancestors,
        int depth,
        List<BarCustomizationEntry> result)
    {
        foreach (var item in items)
        {
            var path = ancestors.Append(item.Id).ToArray();
            result.Add(new BarCustomizationEntry(
                new BarCustomizationTarget(path),
                _caption(item.Definition),
                depth,
                item.Definition.Kind,
                item.IsVisible));
            AppendEntries(item.Children, path, depth + 1, result);
        }
    }

    private static bool EqualsId(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void StableSort(List<MutableItem> values)
    {
        var sorted = values.OrderBy(static item => item.Order).ToArray();
        values.Clear();
        values.AddRange(sorted);
    }

    private sealed class MutableItem
    {
        private readonly List<BarItemCustomization> _unknownChildren;

        public MutableItem(
            BarItemDefinition definition,
            BarItemCustomization? customization)
        {
            Definition = definition;
            Id = definition.Id!;
            IsVisible = customization?.IsVisible ?? true;
            Order = customization?.Order ?? definition.Order;
            var overrides = (customization?.Children ?? [])
                .ToDictionary(item => item.ItemId, StringComparer.OrdinalIgnoreCase);
            foreach (var child in definition.Children.Where(item => item.Id is not null))
            {
                overrides.TryGetValue(child.Id!, out var childOverride);
                Children.Add(new MutableItem(child, childOverride));
            }

            StableSort(Children);
            _unknownChildren = (customization?.Children ?? [])
                .Where(item => !definition.Children.Any(
                    child => EqualsId(child.Id, item.ItemId)))
                .ToList();
        }

        public BarItemDefinition Definition { get; }

        public string Id { get; }

        public int Order { get; }

        public bool IsVisible { get; set; }

        public List<MutableItem> Children { get; } = [];

        public BarItemCustomization CreateCustomization(int order) =>
            new(
                Id,
                IsVisible,
                order,
                Children.Select((child, childOrder) =>
                        child.CreateCustomization(childOrder))
                    .Concat(_unknownChildren));
    }
}
