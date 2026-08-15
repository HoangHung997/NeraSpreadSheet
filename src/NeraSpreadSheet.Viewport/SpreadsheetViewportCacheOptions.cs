namespace NeraSpreadSheet.Viewport;

public sealed record SpreadsheetViewportCacheOptions
{
    public bool Enabled { get; init; } = true;

    public double ScrollTileSize { get; init; } = 256d;

    public int MaxEntries { get; init; } = 8;
}
