using global::Avalonia.Controls;
using global::Avalonia.Markup.Xaml;
using global::Avalonia.Media;
using global::Avalonia.Media.Immutable;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Per-presenter resources: no application-level styles or shared mutable brushes.</summary>
internal sealed partial class NeraRibbonResources : ResourceDictionary
{
    private NeraIconTheme? _theme;

    public NeraRibbonResources() => AvaloniaXamlLoader.Load(this);

    internal void Apply(NeraIconTheme theme)
    {
        if (_theme == theme) return;
        var palette = NeraRibbonPalette.For(theme);
        Set("Surface", palette.Surface); Set("TopSurface", palette.Chrome);
        Set("FieldSurface", palette.Field); Set("Foreground", palette.Text);
        Set("Muted", palette.Muted); Set("Divider", palette.Divider);
        Set("FieldBorder", palette.FieldBorder); Set("Accent", palette.Accent);
        Set("Hover", palette.Hover); Set("HoverBorder", palette.HoverBorder);
        Set("Pressed", palette.Pressed); Set("Checked", palette.Checked); Set("Rail", palette.Rail);
        _theme = theme;
    }

    internal IBrush Brush(string name) => (IBrush)this["NeraRibbon" + name]!;
    private void Set(string key, string color) => this["NeraRibbon" + key] = new ImmutableSolidColorBrush(Color.Parse(color));
}

internal sealed record NeraRibbonPalette(string Surface, string Chrome, string Field,
    string Text, string Muted, string Divider, string FieldBorder, string Accent,
    string Hover, string HoverBorder, string Pressed, string Checked, string Rail)
{
    private static readonly NeraRibbonPalette Light = new("#FFFFFF", "#F5F7F6", "#FFFFFF", "#24292D", "#60676C", "#DEE3E0", "#CAD1CD", "#18734A", "#EAF2ED", "#B9D2C2", "#C8E2D2", "#DDEFE4", "#F0F5F2");
    private static readonly NeraRibbonPalette Dark = new("#252525", "#202020", "#303030", "#F4F4F4", "#BDBDBD", "#494949", "#666666", "#69D5A0", "#3A4640", "#688473", "#496052", "#354D40", "#172B21");
    private static readonly NeraRibbonPalette ContrastLight = new("#FFFFFF", "#FFFFFF", "#FFFFFF", "#000000", "#000000", "#000000", "#000000", "#0035B2", "#D9E5FF", "#0035B2", "#B8CCFF", "#D9E5FF", "#FFFFFF");
    private static readonly NeraRibbonPalette ContrastDark = new("#000000", "#000000", "#000000", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#FFEF00", "#3A3A3A", "#FFEF00", "#555555", "#3A3A3A", "#000000");

    internal static NeraRibbonPalette For(NeraIconTheme theme) => theme switch
    {
        NeraIconTheme.Light => Light, NeraIconTheme.Dark => Dark,
        NeraIconTheme.HighContrastLight => ContrastLight, NeraIconTheme.HighContrastDark => ContrastDark,
        _ => throw new ArgumentOutOfRangeException(nameof(theme)),
    };
}
