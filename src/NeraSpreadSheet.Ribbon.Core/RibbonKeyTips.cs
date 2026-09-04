using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

public enum RibbonKeyTipScope
{
    Inactive,
    Tabs,
    Tab,
    QuickAccessToolbar,
    Backstage,
}

public enum RibbonKeyTipAction
{
    None,
    ScopeChanged,
    ActivateCommand,
    Exit,
}

public sealed record RibbonKeyTipResult(
    RibbonKeyTipAction Action,
    CommandId? CommandId = null,
    string? TabId = null);

/// <summary>Scoped, collision-free key-tip navigation over one immutable snapshot.</summary>
public sealed class RibbonKeyTipController
{
    private readonly RibbonDefinition _definition;
    private readonly RibbonPresentationSnapshot _snapshot;
    private readonly IReadOnlyDictionary<string, string> _tabTips;
    private string _input = string.Empty;

    internal RibbonKeyTipController(
        RibbonDefinition definition,
        RibbonPresentationSnapshot snapshot)
    {
        _definition = definition;
        _snapshot = snapshot;
        _tabTips = CreateTabTips(definition, snapshot);
    }

    public RibbonKeyTipScope Scope { get; private set; }
    public string? ActiveTabId { get; private set; }
    public IReadOnlyDictionary<string, string> TabTips => _tabTips;

    public RibbonKeyTipResult Enter()
    {
        Scope = RibbonKeyTipScope.Tabs;
        ActiveTabId = null;
        _input = string.Empty;
        return new(RibbonKeyTipAction.ScopeChanged);
    }

    public RibbonKeyTipResult OpenQuickAccessToolbar()
    {
        Scope = RibbonKeyTipScope.QuickAccessToolbar;
        ActiveTabId = null;
        _input = string.Empty;
        return new(RibbonKeyTipAction.ScopeChanged);
    }

    public RibbonKeyTipResult OpenBackstage()
    {
        Scope = RibbonKeyTipScope.Backstage;
        ActiveTabId = null;
        _input = string.Empty;
        return new(RibbonKeyTipAction.ScopeChanged);
    }

    public RibbonKeyTipResult Process(string keyTip)
    {
        var normalized = RibbonKeyTip.NormalizeRequired(keyTip, nameof(keyTip));
        return Scope switch
        {
            RibbonKeyTipScope.Tabs => ProcessTab(normalized),
            RibbonKeyTipScope.Tab => ProcessTabCommand(normalized),
            RibbonKeyTipScope.QuickAccessToolbar => ProcessSurface(
                normalized, _definition.QuickAccessToolbar),
            RibbonKeyTipScope.Backstage => ProcessSurface(normalized, _definition.Backstage),
            _ => new(RibbonKeyTipAction.None),
        };
    }

    /// <summary>Consumes one alphanumeric keyboard character, including multi-character tips.</summary>
    public RibbonKeyTipResult ProcessCharacter(char character)
    {
        if (!char.IsLetterOrDigit(character) || Scope == RibbonKeyTipScope.Inactive)
        {
            return new(RibbonKeyTipAction.None);
        }
        _input += char.ToUpperInvariant(character);
        var candidates = GetCurrentTipValues();
        if (!candidates.Any(tip => tip.StartsWith(_input, StringComparison.OrdinalIgnoreCase)))
        {
            _input = string.Empty;
            return new(RibbonKeyTipAction.None);
        }
        if (!candidates.Any(tip => string.Equals(tip, _input, StringComparison.OrdinalIgnoreCase)))
        {
            return new(RibbonKeyTipAction.ScopeChanged);
        }
        var input = _input;
        _input = string.Empty;
        return Process(input);
    }

    public RibbonKeyTipResult Escape()
    {
        if (Scope == RibbonKeyTipScope.Tab ||
            Scope == RibbonKeyTipScope.QuickAccessToolbar ||
            Scope == RibbonKeyTipScope.Backstage)
        {
            Scope = RibbonKeyTipScope.Tabs;
            ActiveTabId = null;
            _input = string.Empty;
            return new(RibbonKeyTipAction.ScopeChanged);
        }
        Scope = RibbonKeyTipScope.Inactive;
        ActiveTabId = null;
        _input = string.Empty;
        return new(RibbonKeyTipAction.Exit);
    }

    public IReadOnlyDictionary<string, CommandId> GetCommandTips()
    {
        IEnumerable<RibbonCommandSurfaceItem> items = Scope switch
        {
            RibbonKeyTipScope.QuickAccessToolbar => _definition.QuickAccessToolbar,
            RibbonKeyTipScope.Backstage => _definition.Backstage,
            RibbonKeyTipScope.Tab => CreateTabCommandItems(),
            _ => [],
        };
        return items.ToDictionary(static item => item.KeyTip, static item => item.CommandId,
            StringComparer.OrdinalIgnoreCase);
    }

    private RibbonKeyTipResult ProcessTab(string keyTip)
    {
        if (string.Equals(keyTip, "F", StringComparison.OrdinalIgnoreCase))
        {
            return OpenBackstage();
        }
        if (string.Equals(keyTip, "Q", StringComparison.OrdinalIgnoreCase))
        {
            return OpenQuickAccessToolbar();
        }
        var tab = _tabTips.FirstOrDefault(pair => string.Equals(
            pair.Value, keyTip, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(tab.Key))
        {
            return new(RibbonKeyTipAction.None);
        }
        Scope = RibbonKeyTipScope.Tab;
        ActiveTabId = tab.Key;
        return new(RibbonKeyTipAction.ScopeChanged, TabId: tab.Key);
    }

    private string[] GetCurrentTipValues() => Scope switch
    {
        RibbonKeyTipScope.Tabs => _tabTips.Values.ToArray(),
        _ => GetCommandTips().Keys.ToArray(),
    };

    private RibbonKeyTipResult ProcessTabCommand(string keyTip) =>
        ProcessSurface(keyTip, CreateTabCommandItems());

    private RibbonKeyTipResult ProcessSurface(
        string keyTip,
        IEnumerable<RibbonCommandSurfaceItem> items)
    {
        var item = items.FirstOrDefault(candidate => string.Equals(
            candidate.KeyTip, keyTip, StringComparison.OrdinalIgnoreCase));
        if (item is null)
        {
            return new(RibbonKeyTipAction.None);
        }
        Scope = RibbonKeyTipScope.Inactive;
        ActiveTabId = null;
        return new(RibbonKeyTipAction.ActivateCommand, item.CommandId);
    }

    private RibbonCommandSurfaceItem[] CreateTabCommandItems()
    {
        var tab = _snapshot.Tabs.FirstOrDefault(candidate => string.Equals(
            candidate.Id, ActiveTabId, StringComparison.OrdinalIgnoreCase));
        if (tab is null)
        {
            return [];
        }
        return CreateUniqueCommandTips(tab.Groups.SelectMany(static group => group.Items)
            .Select(static item => item.Command.CommandId));
    }

    private static Dictionary<string, string> CreateTabTips(
        RibbonDefinition definition,
        RibbonPresentationSnapshot snapshot)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        used.Add("F");
        used.Add("Q");
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in definition.ContextualTabs.Where(static rule => rule.KeyTip is not null))
        {
            var tip = rule.KeyTip!;
            if (used.Any(existing => HasPrefixCollision(existing, tip)))
            {
                throw new InvalidOperationException($"Key-tip collision in ribbon tabs: {tip}.");
            }
            used.Add(tip);
        }
        foreach (var tab in snapshot.Tabs)
        {
            var explicitTip = definition.ContextualTabs.FirstOrDefault(rule =>
                string.Equals(rule.TabId, tab.Id, StringComparison.OrdinalIgnoreCase))?.KeyTip;
            var tip = explicitTip ?? CreateUniqueTip(tab.Caption, tab.Id, used);
            if (explicitTip is not null)
            {
                used.Remove(tip);
                if (used.Any(existing => HasPrefixCollision(existing, tip)))
                {
                    throw new InvalidOperationException($"Key-tip collision in ribbon tabs: {tip}.");
                }
                used.Add(tip);
            }
            result.Add(tab.Id, tip);
        }
        return result;
    }

    private static RibbonCommandSurfaceItem[] CreateUniqueCommandTips(
        IEnumerable<CommandId> commandIds)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<RibbonCommandSurfaceItem>();
        foreach (var commandId in commandIds.Distinct())
        {
            var tip = CreateUniqueTip(commandId.Value, commandId.Value, used);
            used.Add(tip);
            result.Add(new RibbonCommandSurfaceItem(commandId, tip));
        }
        return result.ToArray();
    }

    private static string CreateUniqueTip(string caption, string fallback, HashSet<string> used)
    {
        var candidates = caption.Where(char.IsLetterOrDigit)
            .Select(character => char.ToUpperInvariant(character).ToString())
            .Concat(Enumerable.Range(1, 99).Select(number => $"{char.ToUpperInvariant(fallback[0])}{number}"));
        return candidates.First(candidate =>
            !used.Any(existing => HasPrefixCollision(existing, candidate)));
    }

    private static bool HasPrefixCollision(string left, string right) =>
        left.StartsWith(right, StringComparison.OrdinalIgnoreCase) ||
        right.StartsWith(left, StringComparison.OrdinalIgnoreCase);
}
