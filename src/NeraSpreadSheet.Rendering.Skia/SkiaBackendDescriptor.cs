namespace NeraSpreadSheet.Rendering.Skia;

public static class SkiaBackendDescriptor
{
    public const string BackendId = "skia";

    public static bool IsPlatformCandidate =>
        OperatingSystem.IsAndroid() ||
        OperatingSystem.IsIOS() ||
        OperatingSystem.IsMacCatalyst() ||
        OperatingSystem.IsWindows() ||
        OperatingSystem.IsLinux();

    public static string ImplementationStatus =>
        "Display-list renderer implemented on SKCanvas; MAUI GPU surface/handler integration is the next layer.";
}
