namespace NeraSpreadSheet.Rendering.Direct2D;

public static class Direct2DBackendDescriptor
{
    public const string BackendId = "direct2d-directwrite";

    public static bool IsPlatformSupported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041);

    public static string ImplementationStatus =>
        "M0 contract only: device, composition surface, DirectWrite cache and render loop are not implemented yet.";
}
