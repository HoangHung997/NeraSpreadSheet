using System.Diagnostics;
using global::Avalonia;
using global::Avalonia.Controls.ApplicationLifetimes;
using global::Avalonia.Themes.Fluent;
using global::Avalonia.Threading;

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
                // Preserve the existing basic-host native regression independently.
                var window = new MainWindow(); desktop.MainWindow = window;
                window.Opened += (_, _) => window.StartSmoke(desktop);
            }
            else
            {
                var window = new FullShellWindow(); desktop.MainWindow = window;
                if (desktop.Args?.Contains("--compatibility-smoke", StringComparer.Ordinal) == true)
                    window.Opened += (_, _) => StartRibbonAfterNativeFrame(window, desktop, compatibility: true);
                else if (desktop.Args?.Contains("--dialogs-smoke", StringComparer.Ordinal) == true)
                    window.Opened += (_, _) => StartRibbonAfterNativeFrame(window, desktop, true);
                else if (desktop.Args?.Contains("--ribbon-visual-smoke", StringComparer.Ordinal) == true)
                    window.Opened += (_, _) => StartRibbonAfterNativeFrame(window, desktop);
                else if (desktop.Args?.Contains("--formula-ux-smoke", StringComparer.Ordinal) == true)
                    window.Opened += (_, _) => window.StartFormulaSmoke(desktop);
                else if (desktop.Args?.Contains("--full-ui-smoke", StringComparer.Ordinal) == true)
                    window.Opened += (_, _) => window.StartFullSmoke(desktop);
            }
        }
        base.OnFrameworkInitializationCompleted();
    }
    private static void StartRibbonAfterNativeFrame(FullShellWindow window, IClassicDesktopStyleApplicationLifetime lifetime, bool dialogs = false, bool compatibility = false)
    {
        var deadline = Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        timer.Tick += (_, _) =>
        {
            if (window.Spreadsheet.ActiveSpreadsheet.RenderedFrameCount == 0 && deadline.Elapsed < TimeSpan.FromSeconds(15)) return;
            timer.Stop();
            // The smoke checks the actual count and fails if the deadline elapsed
            // without a frame. Waiting alone is never treated as render evidence.
            if (compatibility) window.StartCompatibilitySmoke(lifetime); else if (dialogs) window.StartDialogsSmoke(lifetime); else window.StartRibbonVisualSmoke(lifetime);
        };
        window.Closed += (_, _) => timer.Stop();
        timer.Start();
    }
}
