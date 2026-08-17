namespace NeraSpreadSheet.Rendering.Direct2D;

public static class Direct2DBackendDescriptor
{
    public const string BackendId = "direct2d-directwrite";

    public static bool IsPlatformSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);

    public static string ImplementationStatus =>
        "Executable Direct2D/DirectWrite HWND renderer implemented with WinForms integration, translated viewport tile cache, retained dirty-region repaint, bounded DirectWrite text-layout caching and one-shot device recovery. WPF GPU composition and the D3D11/DXGI composition backend are not implemented yet.";
}
