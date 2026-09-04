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
    private const int MaximumGeneratedTipLength = 4;
    private const string KeyTipAlphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    private static readonly IReadOnlyDictionary<string, CommandId> EmptyCommandTips =
        new Dictionary<string, CommandId>(StringComparer.OrdinalIgnoreCase);
    private readonly IReadOnlyDictionary<string, string> _tabTips;
    private readonly Dictionary<string, CommandId> _quickAccessToolbarTips;
    private readonly Dictionary<string, CommandId> _backstageTips;
    private readonly Dictionary<string, Dictionary<string, CommandId>> _tabCommandTips;
    private readonly Dictionary<CommandId, string> _quickAccessToolbarTipsByCommand;
    private readonly Dictionary<CommandId, string> _backstageTipsByCommand;
    private readonly Dictionary<string, Dictionary<CommandId, string>> _tabCommandTipsByCommand;
    private string _input = string.Empty;

    internal RibbonKeyTipController(
        RibbonDefinition definition,
        RibbonPresentationSnapshot snapshot)
    {
        _tabTips = CreateTabTips(definition, snapshot);
        _quickAccessToolbarTips = CreateSurfaceTipMap(definition.QuickAccessToolbar);
        _backstageTips = CreateSurfaceTipMap(definition.Backstage);
        _tabCommandTips = snapshot.Tabs.ToDictionary(
            static tab => tab.Id,
            static tab => CreateUniqueCommandTips(
                tab.Groups.SelectMany(static group => group.Items)
                    .Select(static item => item.Command.CommandId)),
            StringComparer.OrdinalIgnoreCase);
        _quickAccessToolbarTipsByCommand = Reverse(_quickAccessToolbarTips);
        _backstageTipsByCommand = Reverse(_backstageTips);
        _tabCommandTipsByCommand = _tabCommandTips.ToDictionary(
            static pair => pair.Key,
            static pair => Reverse(pair.Value),
            StringComparer.OrdinalIgnoreCase);
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
                normalized, _quickAccessToolbarTips),
            RibbonKeyTipScope.Backstage => ProcessSurface(normalized, _backstageTips),
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
        var hasPrefix = false;
        var hasExactMatch = false;
        foreach (var tip in GetCurrentTipValues())
        {
            hasPrefix |= tip.StartsWith(_input, StringComparison.OrdinalIgnoreCase);
            hasExactMatch |= string.Equals(tip, _input, StringComparison.OrdinalIgnoreCase);
        }
        if (!hasPrefix)
        {
            _input = string.Empty;
            return new(RibbonKeyTipAction.None);
        }
        if (!hasExactMatch)
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

    internal void RestoreScope(RibbonKeyTipScope scope, string? activeTabId)
    {
        _input = string.Empty;
        ActiveTabId = null;
        Scope = scope;
        if (scope != RibbonKeyTipScope.Tab || activeTabId is null ||
            !_tabCommandTips.ContainsKey(activeTabId))
        {
            if (scope == RibbonKeyTipScope.Tab)
            {
                Scope = RibbonKeyTipScope.Tabs;
            }
            return;
        }
        ActiveTabId = activeTabId;
    }

    public IReadOnlyDictionary<string, CommandId> GetCommandTips()
    {
        return Scope switch
        {
            RibbonKeyTipScope.QuickAccessToolbar => _quickAccessToolbarTips,
            RibbonKeyTipScope.Backstage => _backstageTips,
            RibbonKeyTipScope.Tab when ActiveTabId is { } tabId &&
                _tabCommandTips.TryGetValue(tabId, out var tips) => tips,
            _ => EmptyCommandTips,
        };
    }

    /// <summary>Gets a cached key tip for a command in the active scope.</summary>
    public bool TryGetCommandTip(CommandId commandId, out string keyTip)
    {
        Dictionary<CommandId, string>? tips = Scope switch
        {
            RibbonKeyTipScope.QuickAccessToolbar => _quickAccessToolbarTipsByCommand,
            RibbonKeyTipScope.Backstage => _backstageTipsByCommand,
            RibbonKeyTipScope.Tab when ActiveTabId is { } tabId &&
                _tabCommandTipsByCommand.TryGetValue(tabId, out var tabTips) => tabTips,
            _ => null,
        };
        if (tips is not null && tips.TryGetValue(commandId, out var resolved))
        {
            keyTip = resolved;
            return true;
        }
        keyTip = string.Empty;
        return false;
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

    private IEnumerable<string> GetCurrentTipValues() => Scope switch
    {
        RibbonKeyTipScope.Tabs => _tabTips.Values.Concat(["F", "Q"]),
        _ => GetCommandTips().Keys,
    };

    private RibbonKeyTipResult ProcessTabCommand(string keyTip) =>
        ProcessSurface(keyTip, GetCommandTips());

    private RibbonKeyTipResult ProcessSurface(
        string keyTip,
        IReadOnlyDictionary<string, CommandId> tips)
    {
        if (!tips.TryGetValue(keyTip, out var commandId))
        {
            return new(RibbonKeyTipAction.None);
        }
        Scope = RibbonKeyTipScope.Inactive;
        ActiveTabId = null;
        return new(RibbonKeyTipAction.ActivateCommand, commandId);
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
        var generatedTabs = new List<RibbonTabPresentation>();
        foreach (var tab in snapshot.Tabs)
        {
            var explicitTip = definition.ContextualTabs.FirstOrDefault(rule =>
                string.Equals(rule.TabId, tab.Id, StringComparison.OrdinalIgnoreCase))?.KeyTip;
            if (explicitTip is null)
            {
                generatedTabs.Add(tab);
                continue;
            }
            result.Add(tab.Id, explicitTip);
        }
        var generatedTips = AllocateUniqueTips(
            generatedTabs.Select(static tab => (tab.Caption, tab.Id)).ToArray(),
            used);
        for (var index = 0; index < generatedTabs.Count; index++)
        {
            result.Add(generatedTabs[index].Id, generatedTips[index]);
        }
        return result;
    }

    private static Dictionary<string, CommandId> CreateUniqueCommandTips(
        IEnumerable<CommandId> commandIds)
    {
        var ids = commandIds.Distinct().ToArray();
        var tips = AllocateUniqueTips(
            ids.Select(static commandId => (commandId.Value, commandId.Value)).ToArray(),
            []);
        var result = new Dictionary<string, CommandId>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < ids.Length; index++)
        {
            result.Add(tips[index], ids[index]);
        }
        return result;
    }

    private static string[] AllocateUniqueTips(
        IReadOnlyList<(string Caption, string Fallback)> entries,
        HashSet<string> reserved)
    {
        for (var length = 1; length <= MaximumGeneratedTipLength; length++)
        {
            if (TryAllocateUniqueTips(entries, reserved, length, out var result))
            {
                return result;
            }
        }
        throw new InvalidOperationException(
            $"Ribbon key-tip capacity exceeded for {entries.Count} generated item(s).");
    }

    private static bool TryAllocateUniqueTips(
        IReadOnlyList<(string Caption, string Fallback)> entries,
        HashSet<string> reserved,
        int length,
        out string[] result)
    {
        var used = new HashSet<string>(reserved, StringComparer.OrdinalIgnoreCase);
        result = new string[entries.Count];
        using var fallbackCodes = EnumerateCodes(length).GetEnumerator();
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var candidate = PreferredCodes(entry.Caption, entry.Fallback, length)
                .FirstOrDefault(value => IsAvailable(value, used));
            while (candidate is null && fallbackCodes.MoveNext())
            {
                if (IsAvailable(fallbackCodes.Current, used))
                {
                    candidate = fallbackCodes.Current;
                }
            }
            if (candidate is null)
            {
                result = [];
                return false;
            }
            result[index] = candidate;
            used.Add(candidate);
        }
        return true;
    }

    private static IEnumerable<string> PreferredCodes(
        string caption,
        string fallback,
        int length)
    {
        var seed = string.Concat(caption.Concat(fallback)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant));
        if (seed.Length < length)
        {
            yield break;
        }
        for (var index = 0; index <= seed.Length - length; index++)
        {
            yield return seed.Substring(index, length);
        }
    }

    private static IEnumerable<string> EnumerateCodes(int length)
    {
        var digits = new int[length];
        var total = 1;
        for (var index = 0; index < length; index++)
        {
            total *= KeyTipAlphabet.Length;
        }
        for (var value = 0; value < total; value++)
        {
            var remaining = value;
            for (var index = length - 1; index >= 0; index--)
            {
                digits[index] = remaining % KeyTipAlphabet.Length;
                remaining /= KeyTipAlphabet.Length;
            }
            yield return string.Create(length, digits, static (span, state) =>
            {
                for (var index = 0; index < span.Length; index++)
                {
                    span[index] = KeyTipAlphabet[state[index]];
                }
            });
        }
    }

    private static bool IsAvailable(string candidate, HashSet<string> used) =>
        !used.Any(existing => HasPrefixCollision(existing, candidate));

    private static Dictionary<string, CommandId> CreateSurfaceTipMap(
        IEnumerable<RibbonCommandSurfaceItem> items) =>
        items.ToDictionary(
            static item => item.KeyTip,
            static item => item.CommandId,
            StringComparer.OrdinalIgnoreCase);

    private static Dictionary<CommandId, string> Reverse(
        IReadOnlyDictionary<string, CommandId> tips) =>
        tips.ToDictionary(static pair => pair.Value, static pair => pair.Key);

    private static bool HasPrefixCollision(string left, string right) =>
        left.StartsWith(right, StringComparison.OrdinalIgnoreCase) ||
        right.StartsWith(left, StringComparison.OrdinalIgnoreCase);
}
