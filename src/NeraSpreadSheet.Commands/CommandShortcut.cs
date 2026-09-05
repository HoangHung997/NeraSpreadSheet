namespace NeraSpreadSheet.Commands;

/// <summary>
/// Represents a normalized platform-neutral keyboard chord.
/// </summary>
public readonly record struct CommandShortcut
{
    private CommandShortcut(string canonicalText)
    {
        CanonicalText = canonicalText;
    }

    public string CanonicalText { get; }

    public static CommandShortcut Parse(string value)
    {
        if (!TryParse(value, out var shortcut))
        {
            throw new FormatException($"Invalid command shortcut '{value}'.");
        }
        return shortcut;
    }

    public static bool TryParse(string? value, out CommandShortcut shortcut)
    {
        shortcut = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var tokens = value.Split(
            '+',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var control = false;
        var alt = false;
        var shift = false;
        var meta = false;
        string? key = null;
        foreach (var token in tokens)
        {
            switch (token.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    if (control)
                    {
                        return false;
                    }
                    control = true;
                    break;
                case "ALT":
                    if (alt)
                    {
                        return false;
                    }
                    alt = true;
                    break;
                case "SHIFT":
                    if (shift)
                    {
                        return false;
                    }
                    shift = true;
                    break;
                case "CMD":
                case "COMMAND":
                case "META":
                case "WIN":
                case "WINDOWS":
                    if (meta)
                    {
                        return false;
                    }
                    meta = true;
                    break;
                default:
                    if (key is not null)
                    {
                        return false;
                    }
                    key = NormalizeKey(token);
                    break;
            }
        }

        if (key is null)
        {
            return false;
        }

        var parts = new List<string>(5);
        if (control)
        {
            parts.Add("Ctrl");
        }
        if (alt)
        {
            parts.Add("Alt");
        }
        if (shift)
        {
            parts.Add("Shift");
        }
        if (meta)
        {
            parts.Add("Meta");
        }
        parts.Add(key);
        shortcut = new CommandShortcut(string.Join('+', parts));
        return true;
    }

    public override string ToString() => CanonicalText;

    private static string NormalizeKey(string value)
    {
        var key = value.Trim();
        return key.Length == 1
            ? key.ToUpperInvariant()
            : string.Concat(
                char.ToUpperInvariant(key[0]),
                key.AsSpan(1).ToString().ToLowerInvariant());
    }
}

/// <summary>
/// Resolves normalized shortcuts and rejects ambiguous chords within one surface.
/// </summary>
public sealed class CommandShortcutMap
{
    private readonly IReadOnlyDictionary<string, CommandId> _commands;

    private CommandShortcutMap(IReadOnlyDictionary<string, CommandId> commands)
    {
        _commands = commands;
    }

    public static CommandShortcutMap Create(IEnumerable<CommandPresentation> commands)
    {
        ArgumentNullException.ThrowIfNull(commands);
        var result = new Dictionary<string, CommandId>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in commands.Where(static command => command.IsRegistered))
        {
            if (string.IsNullOrWhiteSpace(command.Shortcut))
            {
                continue;
            }

            var shortcut = CommandShortcut.Parse(command.Shortcut);
            if (result.TryGetValue(shortcut.CanonicalText, out var existing) &&
                !string.Equals(
                    existing.Value,
                    command.CommandId.Value,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Shortcut '{shortcut}' is assigned to both '{existing}' and " +
                    $"'{command.CommandId}'.");
            }
            result[shortcut.CanonicalText] = command.CommandId;
        }
        return new CommandShortcutMap(result);
    }

    public bool TryResolve(string shortcut, out CommandId commandId)
    {
        commandId = default;
        return CommandShortcut.TryParse(shortcut, out var normalized) &&
            _commands.TryGetValue(normalized.CanonicalText, out commandId);
    }
}
