using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

public enum RibbonCustomizationTargetKind : byte
{
    Tab,
    Group,
    Command,
}

/// <summary>
/// Identifies one stable element in a ribbon customization tree.
/// </summary>
public sealed record RibbonCustomizationTarget
{
    private RibbonCustomizationTarget(
        RibbonCustomizationTargetKind kind,
        string tabId,
        string? groupId,
        CommandId? commandId)
    {
        Kind = kind;
        TabId = tabId;
        GroupId = groupId;
        CommandId = commandId;
    }

    public RibbonCustomizationTargetKind Kind { get; }

    public string TabId { get; }

    public string? GroupId { get; }

    public CommandId? CommandId { get; }

    public static RibbonCustomizationTarget Tab(string tabId) =>
        new(
            RibbonCustomizationTargetKind.Tab,
            CustomizationValidation.RequiredId(tabId, nameof(tabId)),
            null,
            null);

    public static RibbonCustomizationTarget Group(string tabId, string groupId) =>
        new(
            RibbonCustomizationTargetKind.Group,
            CustomizationValidation.RequiredId(tabId, nameof(tabId)),
            CustomizationValidation.RequiredId(groupId, nameof(groupId)),
            null);

    public static RibbonCustomizationTarget Command(
        string tabId,
        string groupId,
        CommandId commandId) =>
        new(
            RibbonCustomizationTargetKind.Command,
            CustomizationValidation.RequiredId(tabId, nameof(tabId)),
            CustomizationValidation.RequiredId(groupId, nameof(groupId)),
            commandId);
}

/// <summary>
/// Provides an immutable row for a native customization editor.
/// </summary>
public sealed record RibbonCustomizationEntry(
    RibbonCustomizationTarget Target,
    string Caption,
    int Depth,
    bool IsVisible,
    bool? IsLarge);

/// <summary>
/// Edits ribbon visibility, sibling order and command size without mutating the
/// application definition. Unknown overrides are retained until <see cref="Reset"/>.
/// </summary>
public sealed class RibbonCustomizationSession
{
    private readonly RibbonDefinition _definition;
    private readonly Func<CommandId, string> _commandCaption;
    private readonly List<MutableTab> _tabs = [];
    private List<RibbonTabCustomization> _unknownTabs = [];

    public RibbonCustomizationSession(
        RibbonDefinition definition,
        RibbonCustomization? customization = null,
        Func<CommandId, string>? commandCaption = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _commandCaption = commandCaption ?? (static commandId => commandId.Value);
        Load(customization);
    }

    public IReadOnlyList<RibbonCustomizationEntry> Entries => CreateEntries();

    public bool SetVisible(RibbonCustomizationTarget target, bool isVisible)
    {
        ArgumentNullException.ThrowIfNull(target);
        var node = Find(target);
        if (node.IsVisible == isVisible)
        {
            return false;
        }

        node.IsVisible = isVisible;
        return true;
    }

    public bool SetLarge(RibbonCustomizationTarget target, bool isLarge)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.Kind != RibbonCustomizationTargetKind.Command)
        {
            throw new InvalidOperationException("Only ribbon commands have a size override.");
        }

        var command = (MutableCommand)Find(target);
        if (command.IsLarge == isLarge)
        {
            return false;
        }

        command.IsLarge = isLarge;
        return true;
    }

    /// <summary>
    /// Moves a target by one or more positions within its current parent.
    /// </summary>
    public bool Move(RibbonCustomizationTarget target, int offset)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (offset == 0)
        {
            return false;
        }

        return target.Kind switch
        {
            RibbonCustomizationTargetKind.Tab => MoveInList(
                _tabs,
                tab => EqualsId(tab.Id, target.TabId),
                offset),
            RibbonCustomizationTargetKind.Group => MoveInList(
                FindTab(target.TabId).Groups,
                group => EqualsId(group.Id, target.GroupId),
                offset),
            RibbonCustomizationTargetKind.Command => MoveInList(
                FindGroup(target.TabId, target.GroupId!).Commands,
                command => EqualsId(
                    command.CommandId.Value,
                    target.CommandId?.Value),
                offset),
            _ => throw new InvalidOperationException(
                $"Unsupported ribbon customization target '{target.Kind}'."),
        };
    }

    public RibbonCustomization CreateCustomization() =>
        new(_tabs.Select((tab, tabOrder) => tab.CreateCustomization(tabOrder))
            .Concat(_unknownTabs));

    public RibbonDefinition Apply() => CreateCustomization().ApplyTo(_definition);

    /// <summary>
    /// Replaces the working state while retaining unknown overrides from the new value.
    /// </summary>
    public void ReplaceCustomization(RibbonCustomization? customization) => Load(customization);

    /// <summary>
    /// Restores the application definition and intentionally removes unknown overrides.
    /// </summary>
    public void Reset()
    {
        _unknownTabs = [];
        Load(customization: null);
    }

    private void Load(RibbonCustomization? customization)
    {
        _tabs.Clear();
        var tabOverrides = (customization?.Tabs ?? [])
            .ToDictionary(tab => tab.TabId, StringComparer.OrdinalIgnoreCase);
        foreach (var tab in _definition.Tabs)
        {
            tabOverrides.TryGetValue(tab.Id, out var tabOverride);
            _tabs.Add(new MutableTab(tab, tabOverride));
        }

        StableSort(_tabs, static tab => tab.Order);
        _unknownTabs = (customization?.Tabs ?? [])
            .Where(tab => !_definition.Tabs.Any(
                definition => EqualsId(definition.Id, tab.TabId)))
            .ToList();
    }

    private List<RibbonCustomizationEntry> CreateEntries()
    {
        var result = new List<RibbonCustomizationEntry>();
        foreach (var tab in _tabs)
        {
            result.Add(new RibbonCustomizationEntry(
                RibbonCustomizationTarget.Tab(tab.Id),
                tab.Caption,
                0,
                tab.IsVisible,
                null));
            foreach (var group in tab.Groups)
            {
                result.Add(new RibbonCustomizationEntry(
                    RibbonCustomizationTarget.Group(tab.Id, group.Id),
                    group.Caption,
                    1,
                    group.IsVisible,
                    null));
                result.AddRange(group.Commands.Select(command =>
                    new RibbonCustomizationEntry(
                        RibbonCustomizationTarget.Command(
                            tab.Id,
                            group.Id,
                            command.CommandId),
                        _commandCaption(command.CommandId),
                        2,
                        command.IsVisible,
                        command.IsLarge)));
            }
        }

        return result;
    }

    private MutableNode Find(RibbonCustomizationTarget target) => target.Kind switch
    {
        RibbonCustomizationTargetKind.Tab => FindTab(target.TabId),
        RibbonCustomizationTargetKind.Group => FindGroup(target.TabId, target.GroupId!),
        RibbonCustomizationTargetKind.Command => FindGroup(target.TabId, target.GroupId!)
            .Commands.Single(command => EqualsId(
                command.CommandId.Value,
                target.CommandId?.Value)),
        _ => throw new InvalidOperationException(
            $"Unsupported ribbon customization target '{target.Kind}'."),
    };

    private MutableTab FindTab(string tabId) =>
        _tabs.Single(tab => EqualsId(tab.Id, tabId));

    private MutableGroup FindGroup(string tabId, string groupId) =>
        FindTab(tabId).Groups.Single(group => EqualsId(group.Id, groupId));

    private static bool MoveInList<T>(
        List<T> values,
        Func<T, bool> predicate,
        int offset)
    {
        var sourceIndex = values.FindIndex(value => predicate(value));
        if (sourceIndex < 0)
        {
            throw new InvalidOperationException("The customization target no longer exists.");
        }

        var destinationIndex = Math.Clamp(sourceIndex + offset, 0, values.Count - 1);
        if (sourceIndex == destinationIndex)
        {
            return false;
        }

        var value = values[sourceIndex];
        values.RemoveAt(sourceIndex);
        values.Insert(destinationIndex, value);
        return true;
    }

    private static bool EqualsId(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static void StableSort<T>(List<T> values, Func<T, int> orderSelector)
    {
        var sorted = values.OrderBy(orderSelector).ToArray();
        values.Clear();
        values.AddRange(sorted);
    }

    private abstract class MutableNode
    {
        protected MutableNode(bool isVisible)
        {
            IsVisible = isVisible;
        }

        public bool IsVisible { get; set; }
    }

    private sealed class MutableTab : MutableNode
    {
        private readonly List<RibbonGroupCustomization> _unknownGroups;

        public MutableTab(RibbonTabDefinition definition, RibbonTabCustomization? customization)
            : base(customization?.IsVisible ?? true)
        {
            Id = definition.Id;
            Caption = definition.Caption;
            Order = customization?.Order ?? definition.Order;
            var groupOverrides = (customization?.Groups ?? [])
                .ToDictionary(group => group.GroupId, StringComparer.OrdinalIgnoreCase);
            foreach (var group in definition.Groups)
            {
                groupOverrides.TryGetValue(group.Id, out var groupOverride);
                Groups.Add(new MutableGroup(group, groupOverride));
            }

            StableSort(Groups, static group => group.Order);
            _unknownGroups = (customization?.Groups ?? [])
                .Where(group => !definition.Groups.Any(
                    candidate => EqualsId(candidate.Id, group.GroupId)))
                .ToList();
        }

        public string Id { get; }

        public string Caption { get; }

        public int Order { get; }

        public List<MutableGroup> Groups { get; } = [];

        public RibbonTabCustomization CreateCustomization(int order) =>
            new(
                Id,
                IsVisible,
                order,
                Groups.Select((group, groupOrder) => group.CreateCustomization(groupOrder))
                    .Concat(_unknownGroups));
    }

    private sealed class MutableGroup : MutableNode
    {
        private readonly List<RibbonItemCustomization> _unknownCommands;

        public MutableGroup(
            RibbonGroupDefinition definition,
            RibbonGroupCustomization? customization)
            : base(customization?.IsVisible ?? true)
        {
            Id = definition.Id;
            Caption = definition.Caption;
            Order = customization?.Order ?? definition.Order;
            var commandOverrides = (customization?.Items ?? [])
                .ToDictionary(item => item.CommandId.Value, StringComparer.OrdinalIgnoreCase);
            foreach (var command in definition.Items)
            {
                commandOverrides.TryGetValue(command.CommandId.Value, out var commandOverride);
                Commands.Add(new MutableCommand(command, commandOverride));
            }

            StableSort(Commands, static command => command.Order);
            _unknownCommands = (customization?.Items ?? [])
                .Where(item => !definition.Items.Any(
                    candidate => EqualsId(
                        candidate.CommandId.Value,
                        item.CommandId.Value)))
                .ToList();
        }

        public string Id { get; }

        public string Caption { get; }

        public int Order { get; }

        public List<MutableCommand> Commands { get; } = [];

        public RibbonGroupCustomization CreateCustomization(int order) =>
            new(
                Id,
                IsVisible,
                order,
                Commands.Select((command, commandOrder) =>
                        command.CreateCustomization(commandOrder))
                    .Concat(_unknownCommands));
    }

    private sealed class MutableCommand : MutableNode
    {
        public MutableCommand(
            RibbonItemDefinition definition,
            RibbonItemCustomization? customization)
            : base(customization?.IsVisible ?? true)
        {
            CommandId = definition.CommandId;
            IsLarge = customization?.IsLarge ?? definition.IsLarge;
            Order = customization?.Order ?? definition.Order;
        }

        public CommandId CommandId { get; }

        public bool IsLarge { get; set; }

        public int Order { get; }

        public RibbonItemCustomization CreateCustomization(int order) =>
            new(CommandId, IsVisible, order, IsLarge);
    }
}
