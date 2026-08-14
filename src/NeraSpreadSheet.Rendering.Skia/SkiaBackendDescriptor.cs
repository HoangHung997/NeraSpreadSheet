namespace NeraSpreadSheet.Rendering.Skia;

public static class SkiaBackendDescriptor
{
    public const string BackendId = "skia-gpu";

    public static bool IsPlatformCandidate =>
        OperatingSystem.IsAndroid() ||
        OperatingSystem.IsIOS() ||
        OperatingSystem.IsMacCatalyst() ||
        OperatingSystem.IsWindows();

    public static string ImplementationStatus =>
        "M0 contract only: GPU surface, MAUI handler and resource cache are not implemented yet.";
}
