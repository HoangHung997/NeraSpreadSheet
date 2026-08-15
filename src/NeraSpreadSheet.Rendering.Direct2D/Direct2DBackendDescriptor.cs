namespace NeraSpreadSheet.Rendering.Direct2D;

public static class Direct2DBackendDescriptor
{
    public const string BackendId = "direct2d-directwrite";

    public static bool IsPlatformSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);

    public static string ImplementationStatus =>
        "Executable HWND Direct2D/DirectWrite display-list renderer implemented; WinForms host integration, WPF composition, tile cache and advanced device management remain in progress.";
}
