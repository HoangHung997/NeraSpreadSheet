using Microsoft.Maui.Controls;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Maui;

public static class NeraMauiCommandChrome
{
    public static readonly BindableProperty CommandIdProperty =
        BindableProperty.CreateAttached(
            "CommandId",
            typeof(string),
            typeof(NeraMauiCommandChrome),
            default(string));

    public static readonly BindableProperty IsCommandCheckedProperty =
        BindableProperty.CreateAttached(
            "IsCommandChecked",
            typeof(bool?),
            typeof(NeraMauiCommandChrome),
            default(bool?));

    public static readonly BindableProperty ShortcutProperty =
        BindableProperty.CreateAttached(
            "Shortcut",
            typeof(string),
            typeof(NeraMauiCommandChrome),
            default(string));

    public static readonly BindableProperty TooltipProperty =
        BindableProperty.CreateAttached(
            "Tooltip",
            typeof(string),
            typeof(NeraMauiCommandChrome),
            default(string));

    public static string? GetCommandId(BindableObject bindable)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        return (string?)bindable.GetValue(CommandIdProperty);
    }

    public static void SetCommandId(BindableObject bindable, string? value)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        bindable.SetValue(CommandIdProperty, value);
    }

    public static bool? GetIsCommandChecked(BindableObject bindable)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        return (bool?)bindable.GetValue(IsCommandCheckedProperty);
    }

    public static void SetIsCommandChecked(BindableObject bindable, bool? value)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        bindable.SetValue(IsCommandCheckedProperty, value);
    }

    public static string? GetShortcut(BindableObject bindable)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        return (string?)bindable.GetValue(ShortcutProperty);
    }

    public static void SetShortcut(BindableObject bindable, string? value)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        bindable.SetValue(ShortcutProperty, value);
    }

    public static string? GetTooltip(BindableObject bindable)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        return (string?)bindable.GetValue(TooltipProperty);
    }

    public static void SetTooltip(BindableObject bindable, string? value)
    {
        ArgumentNullException.ThrowIfNull(bindable);
        bindable.SetValue(TooltipProperty, value);
    }

    internal static void Configure(
        Button button,
        CommandPresentation command,
        string automationPrefix,
        bool isLarge = false,
        string? automationSuffix = null)
    {
        ArgumentNullException.ThrowIfNull(button);
        var descriptor = NeraMauiCommandChromeDescriptor.From(
            command,
            automationPrefix,
            isLarge);
        button.Text = descriptor.Caption;
        button.CommandParameter = command.CommandId;
        button.IsEnabled = descriptor.IsEnabled;
        button.AutomationId = automationSuffix is null
            ? descriptor.AutomationId
            : $"{descriptor.AutomationId}-{automationSuffix}";
        SemanticProperties.SetDescription(button, descriptor.Description);
        SetCommandId(button, descriptor.CommandId);
        SetIsCommandChecked(button, descriptor.IsChecked);
        SetShortcut(button, descriptor.Shortcut);
        SetTooltip(button, descriptor.Tooltip);
    }
}
