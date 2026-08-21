global using Microsoft.Maui.Layouts;

using Microsoft.Maui.Controls;

namespace NeraSpreadSheet.Maui;

internal readonly struct NeraSemanticPropertiesAccessor
{
    private readonly BindableObject _target;

    public NeraSemanticPropertiesAccessor(BindableObject target)
    {
        _target = target ?? throw new ArgumentNullException(nameof(target));
    }

    public string? Description
    {
        get => Microsoft.Maui.Controls.SemanticProperties.GetDescription(_target);
        set => Microsoft.Maui.Controls.SemanticProperties.SetDescription(_target, value);
    }
}

internal static class NeraSemanticPropertiesCompatibility
{
    extension(VerticalStackLayout layout)
    {
        internal NeraSemanticPropertiesAccessor SemanticProperties =>
            new(layout);
    }
}
