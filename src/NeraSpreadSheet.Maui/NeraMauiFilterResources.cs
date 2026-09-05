using Microsoft.Maui.Controls;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Maui;

/// <summary>Captures only the empty SDK shell, before any workbook values are attached.</summary>
internal sealed class NeraMauiFilterResources
{
    private readonly List<(BindableObject Target, BindableProperty Property, string Key)> _labels = [];

    internal NeraMauiFilterResources(VisualElement root) => Capture(root);

    internal void Apply(PresentationLocalization localization)
    {
        // Resolve all resources before changing the native shell; callbacks may throw.
        var values = _labels.Select(label => localization.Get(label.Key)).ToArray();
        for (var index = 0; index < _labels.Count; index++)
        {
            var label = _labels[index];
            label.Target.SetValue(label.Property, values[index]);
        }
    }

    private void Capture(VisualElement element)
    {
        Add(element, SemanticProperties.DescriptionProperty);
        Add(element, SemanticProperties.HintProperty);
        switch (element)
        {
            case Button: Add(element, Button.TextProperty); break;
            case Label: Add(element, Label.TextProperty); break;
            case Entry: Add(element, Entry.PlaceholderProperty); break;
            case Picker: Add(element, Picker.TitleProperty); break;
        }
        IEnumerable<VisualElement> children = element switch
        {
            Microsoft.Maui.Controls.Layout layout => layout.Children.OfType<VisualElement>(),
            ScrollView { Content: { } content } => [content],
            ContentView { Content: { } content } => [content],
            Border { Content: { } content } => [content],
            _ => [],
        };
        foreach (var child in children) Capture(child);
    }

    private void Add(BindableObject target, BindableProperty property)
    {
        if (target.GetValue(property) is string { Length: > 0 } key && PresentationLocalization.ContainsKey(key))
            _labels.Add((target, property, key));
    }
}
