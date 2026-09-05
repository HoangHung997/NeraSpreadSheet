namespace NeraSpreadSheet.Iconography;

public readonly record struct NeraIconRequest(
    string IconKey,
    int PixelSize,
    NeraIconTheme Theme = NeraIconTheme.Light);
