using System.Windows;
using System.Windows.Media;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Wpf;

internal static class NeraRibbonChrome
{
    internal static void Install(FrameworkElement owner)
    {
        owner.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("/NeraSpreadSheet.Wpf;component/RibbonChrome.xaml", UriKind.Relative),
        });
        ApplyTheme(owner, NeraIconTheme.Light);
    }

    internal static void ApplyTheme(FrameworkElement owner, NeraIconTheme theme)
    {
        var dark = theme is NeraIconTheme.Dark or NeraIconTheme.HighContrastDark;
        var highContrast = theme is NeraIconTheme.HighContrastDark or NeraIconTheme.HighContrastLight;
        Set("RibbonSurface", highContrast ? (dark ? "#000000" : "#FFFFFF") : (dark ? "#252525" : "#FFFFFF"));
        Set("RibbonTopSurface", highContrast ? (dark ? "#000000" : "#FFFFFF") : (dark ? "#202020" : "#F5F7F6"));
        Set("RibbonFieldSurface", highContrast ? (dark ? "#000000" : "#FFFFFF") : (dark ? "#303030" : "#FFFFFF"));
        Set("RibbonForeground", highContrast ? (dark ? "#FFFFFF" : "#000000") : (dark ? "#F4F4F4" : "#24292D"));
        Set("RibbonMuted", highContrast ? (dark ? "#FFFFFF" : "#000000") : (dark ? "#BDBDBD" : "#60676C"));
        Set("RibbonDivider", highContrast ? (dark ? "#FFFFFF" : "#000000") : (dark ? "#494949" : "#DEE3E0"));
        Set("RibbonFieldBorder", highContrast ? (dark ? "#FFFFFF" : "#000000") : (dark ? "#666666" : "#CAD1CD"));
        Set("RibbonAccent", highContrast ? (dark ? "#FFEF00" : "#0035B2") : (dark ? "#69D5A0" : "#18734A"));
        Set("RibbonHover", highContrast ? (dark ? "#3A3A3A" : "#D9E5FF") : (dark ? "#3A4640" : "#EAF2ED"));
        Set("RibbonHoverBorder", highContrast ? (dark ? "#FFEF00" : "#0035B2") : (dark ? "#688473" : "#B9D2C2"));
        Set("RibbonPressed", dark ? "#496052" : "#C8E2D2");
        Set("RibbonChecked", highContrast ? (dark ? "#3A3A3A" : "#D9E5FF") : (dark ? "#354D40" : "#DDEFE4"));
        Set("RibbonRail", highContrast ? (dark ? "#000000" : "#FFFFFF") : (dark ? "#172B21" : "#F0F5F2"));

        void Set(string key, string color)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
            brush.Freeze();
            owner.Resources[key] = brush;
        }
    }
}
