using global::Avalonia;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Themes.Fluent;

namespace NeraSpreadSheet.Avalonia.Sample;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont();
}
public sealed class App : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (desktop.Args?.Contains("--smoke", StringComparer.Ordinal) == true)
            {
                // Keep the existing basic-host native regression as an independent gate.
                var window = new MainWindow(); desktop.MainWindow = window;
                window.Opened += (_, _) => window.StartSmoke(desktop);
            }
            else
            {
                var window = new FullShellWindow(); desktop.MainWindow = window;
                if (desktop.Args?.Contains("--full-ui-smoke", StringComparer.Ordinal) == true)
                    window.Opened += (_, _) => window.StartFullSmoke(desktop);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
}
