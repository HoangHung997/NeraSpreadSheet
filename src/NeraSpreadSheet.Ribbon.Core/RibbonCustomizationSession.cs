using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

public enum RibbonCustomizationTargetKind : byte { Tab, Group, Command }

/// <summary>Identifies one stable element in a ribbon customization tree.</summary>
public sealed record RibbonCustomizationTarget
{
    private RibbonCustomizationTarget(RibbonCustomizationTargetKind kind, string tabId, string? groupId, CommandId? commandId) =>
        (Kind, TabId, GroupId, CommandId) = (kind, tabId, groupId, commandId);

    public RibbonCustomizationTargetKind Kind { get; }
    public string TabId { get; }
    public string? GroupId { get; }
    public CommandId? CommandId { get; }

    public static RibbonCustomizationTarget Tab(string tabId) => new(RibbonCustomizationTargetKind.Tab, Required(tabId), null, null);
    public static RibbonCustomizationTarget Group(string tabId, string groupId) => new(RibbonCustomizationTargetKind.Group, Required(tabId), Required(groupId), null);
    public static RibbonCustomizationTarget Command(string tabId, string groupId, CommandId commandId) => new(RibbonCustomizationTargetKind.Command, Required(tabId), Required(groupId), commandId);
    private static string Required(string value) => CustomizationValidation.RequiredId(value, nameof(value));
}

/// <summary>Provides an immutable row for a native customization editor.</summary>
public sealed record RibbonCustomizationEntry(
    RibbonCustomizationTarget Target,
    string Caption,
    int Depth,
    bool IsVisible,
    bool? IsLarge,
    bool IsCustom = false,
    bool IsLocked = false)
{
    public RibbonCustomizationEntry(
        RibbonCustomizationTarget target,
        string caption,
        int depth,
        bool isVisible,
        bool? isLarge)
        : this(target, caption, depth, isVisible, isLarge, false, false)
    {
    }
}

/// <summary>Transactional editor for Ribbon structure, command placement and QAT state.</summary>
public sealed class RibbonCustomizationSession
{
    private readonly RibbonDefinition _definition;
    private readonly Func<CommandId, string> _commandCaption;
    private readonly RibbonCustomizationPolicy _policy;
    private readonly Dictionary<string, RibbonItemDefinition> _sourceCommands;
    private RibbonCommandCatalog? _commandCatalog;
    private readonly List<MutableTab> _tabs = [];
    private readonly List<RibbonQuickAccessItemCustomization> _quickAccessToolbar = [];
    private List<RibbonTabCustomization> _unknownTabs = [];
    private RibbonCustomization? _committed;

    public RibbonCustomizationSession(
        RibbonDefinition definition,
        RibbonCustomization? customization = null,
        Func<CommandId, string>? commandCaption = null)
        : this(definition, customization, commandCaption, null)
    {
    }

    public RibbonCustomizationSession(
        RibbonDefinition definition,
        RibbonCustomizationPolicy policy)
        : this(definition, null, null, policy)
    {
    }

    public RibbonCustomizationSession(
        RibbonDefinition definition,
        RibbonCommandCatalog commandCatalog,
        RibbonCustomization? customization = null,
        Func<CommandId, string>? commandCaption = null,
        RibbonCustomizationPolicy? policy = null)
        : this(definition, customization, commandCaption, policy)
    {
        ArgumentNullException.ThrowIfNull(commandCatalog);
        _commandCatalog = commandCatalog;
        foreach (var entry in commandCatalog.Entries)
        {
            _sourceCommands.TryAdd(entry.CommandId.Value, new RibbonItemDefinition(entry.CommandId));
        }
        Load(customization);
    }

    public RibbonCustomizationSession(
        RibbonDefinition definition,
        RibbonCustomization? customization,
        Func<CommandId, string>? commandCaption,
        RibbonCustomizationPolicy? policy)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _commandCaption = commandCaption ?? (static commandId => commandId.Value);
        _policy = policy ?? RibbonCustomizationPolicy.Unrestricted;
        _sourceCommands = definition.Tabs.SelectMany(static tab => tab.Groups)
            .SelectMany(static group => group.Items)
            .GroupBy(static item => item.CommandId.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.OrdinalIgnoreCase);
        _committed = customization;
        Load(customization);
    }

    public IReadOnlyList<RibbonCustomizationEntry> Entries => CreateEntries();

    /// <summary>Projects SDK default labels for a native editor without changing its working profile.</summary>
    public IReadOnlyList<RibbonCustomizationEntry> GetLocalizedEntries(PresentationLocalization localization)
    {
        ArgumentNullException.ThrowIfNull(localization);
        return Entries.Select(entry =>
        {
            if (entry.IsCustom || entry.Target.Kind == RibbonCustomizationTargetKind.Command) return entry;
            var tab = _definition.Tabs.FirstOrDefault(tab => EqualsId(tab.Id, entry.Target.TabId));
            var group = tab?.Groups.FirstOrDefault(group => EqualsId(group.Id, entry.Target.GroupId ?? string.Empty));
            var caption = entry.Target.Kind == RibbonCustomizationTargetKind.Tab ? tab?.Caption : group?.Caption;
            var key = entry.Target.Kind == RibbonCustomizationTargetKind.Tab ? tab?.CaptionResourceKey : group?.CaptionResourceKey;
            return key is not null && string.Equals(entry.Caption, caption, StringComparison.Ordinal)
                ? entry with { Caption = localization.Get(key) } : entry;
        }).ToArray();
    }

    public IReadOnlyList<CommandId> QuickAccessToolbar => _quickAccessToolbar.OrderBy(static item => item.Order).Select(static item => item.CommandId).ToArray();

    public bool SetVisible(RibbonCustomizationTarget target, bool isVisible)
    {
        _policy.Demand(target, RibbonCustomizationOperation.Visibility);
        var node = Find(target);
        if (node.IsVisible == isVisible) return false;
        node.IsVisible = isVisible;
        return true;
    }

    public bool SetLarge(RibbonCustomizationTarget target, bool isLarge)
    {
        if (target.Kind != RibbonCustomizationTargetKind.Command) throw new InvalidOperationException("Only ribbon commands have a size override.");
        _policy.Demand(target, RibbonCustomizationOperation.ResizeCommand);
        var command = (MutableCommand)Find(target);
        if (command.IsLarge == isLarge) return false;
        command.IsLarge = isLarge;
        return true;
    }

    /// <summary>Moves a target by one or more positions within its current parent.</summary>
    public bool Move(RibbonCustomizationTarget target, int offset)
    {
        ArgumentNullException.ThrowIfNull(target);
        _policy.Demand(target, RibbonCustomizationOperation.Reorder);
        if (offset == 0) return false;
        return target.Kind switch
        {
            RibbonCustomizationTargetKind.Tab => MoveInList(_tabs, tab => EqualsId(tab.Id, target.TabId), offset),
            RibbonCustomizationTargetKind.Group => MoveInList(FindTab(target.TabId).Groups, group => EqualsId(group.Id, target.GroupId), offset),
            RibbonCustomizationTargetKind.Command => MoveInList(FindGroup(target.TabId, target.GroupId!).Commands, command => EqualsId(command.CommandId.Value, target.CommandId?.Value), offset),
            _ => throw new InvalidOperationException($"Unsupported ribbon customization target '{target.Kind}'."),
        };
    }

    public RibbonCustomizationTarget AddTab(string tabId, string caption)
    {
        var target = RibbonCustomizationTarget.Tab(tabId);
        _policy.Demand(target, RibbonCustomizationOperation.Add);
        if (_tabs.Any(tab => EqualsId(tab.Id, tabId)) || _unknownTabs.Any(tab => EqualsId(tab.TabId, tabId)))
            throw new InvalidOperationException($"Ribbon tab id '{tabId}' already exists.");
        _tabs.Add(new MutableTab(tabId.Trim(), RequiredCaption(caption), true));
        return target;
    }

    public RibbonCustomizationTarget AddGroup(string tabId, string groupId, string caption)
    {
        var target = RibbonCustomizationTarget.Group(tabId, groupId);
        _policy.Demand(target, RibbonCustomizationOperation.Add);
        var tab = FindTab(tabId);
        if (tab.Groups.Any(group => EqualsId(group.Id, groupId))) throw new InvalidOperationException($"Ribbon group id '{groupId}' already exists in tab '{tabId}'.");
        tab.Groups.Add(new MutableGroup(groupId.Trim(), RequiredCaption(caption), true));
        return target;
    }

    public bool Rename(RibbonCustomizationTarget target, string caption)
    {
        _policy.Demand(target, RibbonCustomizationOperation.Rename);
        if (target.Kind == RibbonCustomizationTargetKind.Command) throw new InvalidOperationException("Command captions are owned by the command descriptor.");
        var node = Find(target);
        var normalized = RequiredCaption(caption);
        if (string.Equals(node.Caption, normalized, StringComparison.Ordinal)) return false;
        node.Caption = normalized;
        return true;
    }

    public bool Remove(RibbonCustomizationTarget target)
    {
        _policy.Demand(target, RibbonCustomizationOperation.Remove);
        return target.Kind switch
        {
            RibbonCustomizationTargetKind.Tab => RemoveTab(target),
            RibbonCustomizationTargetKind.Group => RemoveGroup(target),
            RibbonCustomizationTargetKind.Command => RemoveCommand(target),
            _ => false,
        };
    }

    public RibbonCustomizationTarget AddCommand(CommandId commandId, string destinationTabId, string destinationGroupId, bool isLarge = false)
    {
        var target = RibbonCustomizationTarget.Command(destinationTabId, destinationGroupId, commandId);
        _policy.Demand(target, RibbonCustomizationOperation.MoveCommand);
        if (!_sourceCommands.TryGetValue(commandId.Value, out var source)) throw new InvalidOperationException($"Command '{commandId}' is not present in the command catalog.");
        if (_tabs.SelectMany(static tab => tab.Groups).SelectMany(static group => group.Commands).Any(command => command.IsPlacement && command.CommandId == commandId))
            throw new InvalidOperationException($"Command '{commandId}' already has a custom placement.");
        var group = FindGroup(destinationTabId, destinationGroupId);
        RemoveAllCommandOccurrences(commandId);
        group.Commands.Add(new MutableCommand(source, null) { IsLarge = isLarge, IsPlacement = true });
        return target;
    }

    public RibbonCustomizationTarget MoveCommand(RibbonCustomizationTarget source, string destinationTabId, string destinationGroupId, int destinationIndex = int.MaxValue)
    {
        if (source.Kind != RibbonCustomizationTargetKind.Command || source.CommandId is not CommandId commandId) throw new InvalidOperationException("Only a command can move between Ribbon groups.");
        _policy.Demand(source, RibbonCustomizationOperation.MoveCommand);
        var destination = RibbonCustomizationTarget.Command(destinationTabId, destinationGroupId, commandId);
        _policy.Demand(destination, RibbonCustomizationOperation.MoveCommand);
        var command = (MutableCommand)Find(source);
        var targetGroup = FindGroup(destinationTabId, destinationGroupId);
        if (targetGroup.Commands.Any(item => item.CommandId == commandId)) throw new InvalidOperationException($"Command '{commandId}' already exists in the destination group.");
        FindGroup(source.TabId, source.GroupId!).Commands.Remove(command);
        command.IsPlacement = true;
        targetGroup.Commands.Insert(Math.Clamp(destinationIndex, 0, targetGroup.Commands.Count), command);
        return destination;
    }

    public bool AddToQuickAccessToolbar(CommandId commandId, int index = int.MaxValue)
    {
        DemandQuickAccess(commandId);
        if (_quickAccessToolbar.Any(item => item.CommandId == commandId)) return false;
        _quickAccessToolbar.Insert(Math.Clamp(index, 0, _quickAccessToolbar.Count), new RibbonQuickAccessItemCustomization(commandId));
        NormalizeQuickAccessOrders();
        return true;
    }

    public bool RemoveFromQuickAccessToolbar(CommandId commandId)
    {
        DemandQuickAccess(commandId);
        var removed = _quickAccessToolbar.RemoveAll(item => item.CommandId == commandId) > 0;
        NormalizeQuickAccessOrders();
        return removed;
    }

    public bool MoveQuickAccessToolbar(CommandId commandId, int offset)
    {
        DemandQuickAccess(commandId);
        var changed = MoveInList(_quickAccessToolbar, item => item.CommandId == commandId, offset);
        NormalizeQuickAccessOrders();
        return changed;
    }

    public RibbonCustomization CreateCustomization() => new(
        _tabs.Select((tab, index) => tab.CreateCustomization(index)).Concat(_unknownTabs),
        _quickAccessToolbar.Select((item, index) => item with { Order = index }));

    public RibbonDefinition Preview() => CreateCustomization().ApplyTo(_definition, _commandCatalog);
    public RibbonDefinition Apply() => Preview();

    public RibbonCustomization Commit()
    {
        _committed = CreateCustomization();
        return _committed;
    }

    public void Cancel() => Load(_committed);

    public void ReplaceCustomization(RibbonCustomization? customization)
    {
        _policy.Demand(RibbonCustomizationTarget.Tab("profile"), RibbonCustomizationOperation.Import);
        if (customization is not null) _policy.DemandImport(_definition, customization);
        Load(customization);
    }

    public void Reset()
    {
        _policy.Demand(RibbonCustomizationTarget.Tab("profile"), RibbonCustomizationOperation.Reset);
        _unknownTabs = [];
        Load(null);
    }

    private void Load(RibbonCustomization? customization)
    {
        _tabs.Clear();
        foreach (var tab in _definition.Tabs)
        {
            var tabOverride = customization?.Tabs.FirstOrDefault(candidate => EqualsId(candidate.TabId, tab.Id));
            _tabs.Add(new MutableTab(tab, tabOverride));
        }
        foreach (var customTab in customization?.Tabs.Where(static tab => tab.IsCustom) ?? [])
        {
            if (_tabs.Any(tab => EqualsId(tab.Id, customTab.TabId))) throw new InvalidOperationException($"Custom ribbon tab id '{customTab.TabId}' collides with the definition.");
            _tabs.Add(new MutableTab(customTab));
        }
        StableSort(_tabs, static tab => tab.Order);
        ApplyPlacements(customization);
        _unknownTabs = (customization?.Tabs ?? []).Where(tab => !tab.IsCustom && !_definition.Tabs.Any(definition => EqualsId(definition.Id, tab.TabId))).ToList();

        _quickAccessToolbar.Clear();
        if (customization?.HasQuickAccessToolbarOverride == true) _quickAccessToolbar.AddRange(customization.QuickAccessToolbar.OrderBy(static item => item.Order));
        else _quickAccessToolbar.AddRange(_definition.QuickAccessToolbar.Select((item, index) => new RibbonQuickAccessItemCustomization(item.CommandId, index, item.KeyTip)));
    }

    private void ApplyPlacements(RibbonCustomization? customization)
    {
        foreach (var tabOverride in customization?.Tabs ?? [])
        {
            var tab = _tabs.FirstOrDefault(candidate => EqualsId(candidate.Id, tabOverride.TabId));
            if (tab is null) continue;
            foreach (var groupOverride in tabOverride.Groups)
            {
                var group = tab.Groups.FirstOrDefault(candidate => EqualsId(candidate.Id, groupOverride.GroupId));
                if (group is null) continue;
                foreach (var item in groupOverride.Items.Where(static item => item.IsPlacement))
                {
                    if (!_sourceCommands.TryGetValue(item.CommandId.Value, out var source)) continue;
                    RemoveAllCommandOccurrences(item.CommandId);
                    group.RemoveUnknown(item.CommandId);
                    group.Commands.Add(new MutableCommand(source, item) { IsPlacement = true });
                }
                StableSort(group.Commands, static command => command.Order);
            }
        }
    }

    private List<RibbonCustomizationEntry> CreateEntries()
    {
        var result = new List<RibbonCustomizationEntry>();
        foreach (var tab in _tabs)
        {
            var tabTarget = RibbonCustomizationTarget.Tab(tab.Id);
            result.Add(new(tabTarget, tab.Caption, 0, tab.IsVisible, null, tab.IsCustom, !_policy.IsAllowed(tabTarget, RibbonCustomizationOperation.Visibility)));
            foreach (var group in tab.Groups)
            {
                var groupTarget = RibbonCustomizationTarget.Group(tab.Id, group.Id);
                result.Add(new(groupTarget, group.Caption, 1, group.IsVisible, null, group.IsCustom, !_policy.IsAllowed(groupTarget, RibbonCustomizationOperation.Visibility)));
                foreach (var command in group.Commands)
                {
                    var target = RibbonCustomizationTarget.Command(tab.Id, group.Id, command.CommandId);
                    result.Add(new(target, _commandCaption(command.CommandId), 2, command.IsVisible, command.IsLarge, false, !_policy.IsAllowed(target, RibbonCustomizationOperation.Visibility)));
                }
            }
        }
        return result;
    }

    private MutableNode Find(RibbonCustomizationTarget target) => target.Kind switch
    {
        RibbonCustomizationTargetKind.Tab => FindTab(target.TabId),
        RibbonCustomizationTargetKind.Group => FindGroup(target.TabId, target.GroupId!),
        RibbonCustomizationTargetKind.Command => FindGroup(target.TabId, target.GroupId!).Commands.Single(command => EqualsId(command.CommandId.Value, target.CommandId?.Value)),
        _ => throw new InvalidOperationException($"Unsupported ribbon customization target '{target.Kind}'."),
    };

    private MutableTab FindTab(string tabId) => _tabs.Single(tab => EqualsId(tab.Id, tabId));
    private MutableGroup FindGroup(string tabId, string groupId) => FindTab(tabId).Groups.Single(group => EqualsId(group.Id, groupId));

    private RibbonCustomizationTarget? FindCommandTarget(CommandId commandId)
    {
        foreach (var tab in _tabs)
        foreach (var group in tab.Groups)
            if (group.Commands.Any(command => command.CommandId == commandId)) return RibbonCustomizationTarget.Command(tab.Id, group.Id, commandId);
        return null;
    }

    private void DemandQuickAccess(CommandId commandId) => _policy.Demand(FindCommandTarget(commandId) ?? RibbonCustomizationTarget.Command("catalog", "catalog", commandId), RibbonCustomizationOperation.QuickAccessToolbar);

    private bool RemoveTab(RibbonCustomizationTarget target)
    {
        var tab = FindTab(target.TabId);
        return tab.IsCustom ? _tabs.Remove(tab) : SetVisible(target, false);
    }

    private bool RemoveGroup(RibbonCustomizationTarget target)
    {
        var group = FindGroup(target.TabId, target.GroupId!);
        return group.IsCustom ? FindTab(target.TabId).Groups.Remove(group) : SetVisible(target, false);
    }

    private bool RemoveCommand(RibbonCustomizationTarget target)
    {
        var group = FindGroup(target.TabId, target.GroupId!);
        var command = (MutableCommand)Find(target);
        return command.IsPlacement ? group.Commands.Remove(command) : SetVisible(target, false);
    }

    private void RemoveAllCommandOccurrences(CommandId commandId)
    {
        foreach (var group in _tabs.SelectMany(static tab => tab.Groups)) group.Commands.RemoveAll(command => command.CommandId == commandId);
    }

    private void NormalizeQuickAccessOrders()
    {
        for (var index = 0; index < _quickAccessToolbar.Count; index++) _quickAccessToolbar[index] = _quickAccessToolbar[index] with { Order = index };
    }

    private static bool MoveInList<T>(List<T> values, Func<T, bool> predicate, int offset)
    {
        var sourceIndex = values.FindIndex(value => predicate(value));
        if (sourceIndex < 0) throw new InvalidOperationException("The customization target no longer exists.");
        var destinationIndex = Math.Clamp(sourceIndex + offset, 0, values.Count - 1);
        if (sourceIndex == destinationIndex) return false;
        var value = values[sourceIndex];
        values.RemoveAt(sourceIndex);
        values.Insert(destinationIndex, value);
        return true;
    }

    private static string RequiredCaption(string caption)
    {
        if (string.IsNullOrWhiteSpace(caption)) throw new ArgumentException("A localized Ribbon caption is required.", nameof(caption));
        return caption.Trim();
    }

    private static bool EqualsId(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    private static void StableSort<T>(List<T> values, Func<T, int> orderSelector)
    {
        var sorted = values.OrderBy(orderSelector).ToArray();
        values.Clear();
        values.AddRange(sorted);
    }

    private abstract class MutableNode(bool isVisible, string caption)
    {
        public bool IsVisible { get; set; } = isVisible;
        public string Caption { get; set; } = caption;
    }

    private sealed class MutableTab : MutableNode
    {
        private readonly List<RibbonGroupCustomization> _unknownGroups;

        public MutableTab(string id, string caption, bool isCustom) : base(true, caption) => (Id, IsCustom, _unknownGroups) = (id, isCustom, []);

        public MutableTab(RibbonTabDefinition definition, RibbonTabCustomization? customization)
            : base(customization?.IsVisible ?? true, customization?.Caption ?? definition.Caption)
        {
            Id = definition.Id;
            Order = customization?.Order ?? definition.Order;
            foreach (var group in definition.Groups)
                Groups.Add(new MutableGroup(group, customization?.Groups.FirstOrDefault(candidate => EqualsId(candidate.GroupId, group.Id))));
            foreach (var customGroup in customization?.Groups.Where(static group => group.IsCustom) ?? []) Groups.Add(new MutableGroup(customGroup));
            StableSort(Groups, static group => group.Order);
            _unknownGroups = (customization?.Groups ?? []).Where(group => !group.IsCustom && !definition.Groups.Any(candidate => EqualsId(candidate.Id, group.GroupId))).ToList();
        }

        public MutableTab(RibbonTabCustomization customization) : base(customization.IsVisible, customization.Caption ?? customization.TabId)
        {
            Id = customization.TabId;
            IsCustom = true;
            Order = customization.Order ?? 0;
            Groups.AddRange(customization.Groups.Where(static group => group.IsCustom).Select(static group => new MutableGroup(group)));
            _unknownGroups = customization.Groups.Where(static group => !group.IsCustom).ToList();
        }

        public string Id { get; }
        public bool IsCustom { get; }
        public int Order { get; }
        public List<MutableGroup> Groups { get; } = [];
        public RibbonTabCustomization CreateCustomization(int order) => new(Id, IsVisible, order, Groups.Select((group, index) => group.CreateCustomization(index)).Concat(_unknownGroups), Caption, IsCustom);
    }

    private sealed class MutableGroup : MutableNode
    {
        private readonly List<RibbonItemCustomization> _unknownCommands;

        public MutableGroup(string id, string caption, bool isCustom) : base(true, caption) => (Id, IsCustom, _unknownCommands) = (id, isCustom, []);

        public MutableGroup(RibbonGroupDefinition definition, RibbonGroupCustomization? customization)
            : base(customization?.IsVisible ?? true, customization?.Caption ?? definition.Caption)
        {
            Id = definition.Id;
            Order = customization?.Order ?? definition.Order;
            foreach (var command in definition.Items)
                Commands.Add(new MutableCommand(command, customization?.Items.FirstOrDefault(item => item.CommandId == command.CommandId && !item.IsPlacement)));
            StableSort(Commands, static command => command.Order);
            _unknownCommands = (customization?.Items ?? []).Where(item => item.IsPlacement || !definition.Items.Any(candidate => candidate.CommandId == item.CommandId)).ToList();
        }

        public MutableGroup(RibbonGroupCustomization customization) : base(customization.IsVisible, customization.Caption ?? customization.GroupId)
        {
            Id = customization.GroupId;
            IsCustom = true;
            Order = customization.Order ?? 0;
            _unknownCommands = customization.Items.ToList();
        }

        public string Id { get; }
        public bool IsCustom { get; }
        public int Order { get; }
        public List<MutableCommand> Commands { get; } = [];
        public void RemoveUnknown(CommandId commandId) => _unknownCommands.RemoveAll(item => item.CommandId == commandId);
        public RibbonGroupCustomization CreateCustomization(int order) => new(Id, IsVisible, order, Commands.Select((command, index) => command.CreateCustomization(index)).Concat(_unknownCommands), Caption, IsCustom);
    }

    private sealed class MutableCommand : MutableNode
    {
        public MutableCommand(RibbonItemDefinition definition, RibbonItemCustomization? customization) : base(customization?.IsVisible ?? true, definition.CommandId.Value)
        {
            CommandId = definition.CommandId;
            IsLarge = customization?.IsLarge ?? definition.IsLarge;
            Order = customization?.Order ?? definition.Order;
            IsPlacement = customization?.IsPlacement ?? false;
        }

        public CommandId CommandId { get; }
        public bool IsLarge { get; set; }
        public int Order { get; }
        public bool IsPlacement { get; set; }
        public RibbonItemCustomization CreateCustomization(int order) => new(CommandId, IsVisible, order, IsLarge, IsPlacement);
    }
}
