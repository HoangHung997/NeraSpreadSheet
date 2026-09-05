using System.Windows;

namespace NeraSpreadSheet.Wpf.Sample;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--ribbon-preview", StringComparer.Ordinal) ||
            (AppContext.TryGetSwitch("NeraSpreadSheet.RibbonDemo", out var ribbonDemo) && ribbonDemo))
        {
            var preview = new RibbonPreviewWindow();
            MainWindow = preview;
            var captureIndex = Array.IndexOf(e.Args, "--capture");
            if (captureIndex >= 0 && captureIndex + 1 < e.Args.Length)
            {
                preview.Loaded += async (_, _) =>
                {
                    try
                    {
                        await preview.CaptureMatrixAsync(e.Args[captureIndex + 1], e.Args.Contains("--table-design-only", StringComparer.Ordinal));
                        Shutdown(0);
                    }
                    catch (Exception exception)
                    {
                        Console.Error.WriteLine(exception);
                        Shutdown(1);
                    }
                };
            }
            preview.Show();
        }
        else
        {
            MainWindow = new MainWindow();
            MainWindow.Show();
        }
    }
}
