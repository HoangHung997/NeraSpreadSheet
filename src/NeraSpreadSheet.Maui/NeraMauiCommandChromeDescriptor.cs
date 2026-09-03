using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Maui;

public sealed record NeraMauiCommandChromeDescriptor(
    string CommandId,
    string Caption,
    bool IsEnabled,
    bool? IsChecked,
    string? Shortcut,
    string? Tooltip,
    string? IconKey,
    string AutomationId,
    string Description,
    bool IsLarge)
{
    public static NeraMauiCommandChromeDescriptor From(
        CommandPresentation command,
        string automationPrefix,
        bool isLarge = false)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(automationPrefix))
        {
            throw new ArgumentException(
                "An automation prefix is required.",
                nameof(automationPrefix));
        }

        return new NeraMauiCommandChromeDescriptor(
            command.CommandId.Value,
            command.Caption,
            command.IsEnabled,
            command.IsChecked,
            command.Shortcut,
            command.Tooltip,
            command.IconKey,
            $"{automationPrefix}-{command.CommandId.Value}",
            BuildDescription(command),
            isLarge);
    }

    private static string BuildDescription(CommandPresentation command)
    {
        if (!string.IsNullOrWhiteSpace(command.Tooltip) &&
            !string.IsNullOrWhiteSpace(command.Shortcut))
        {
            return $"{command.Tooltip} ({command.Shortcut})";
        }
        if (!string.IsNullOrWhiteSpace(command.Tooltip))
        {
            return command.Tooltip;
        }
        if (!string.IsNullOrWhiteSpace(command.Shortcut))
        {
            return command.Shortcut;
        }
        return command.Caption;
    }
}
