namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>A color-only cell in a lightweight gallery thumbnail.</summary>
/// <param name="BackgroundArgb">Background color encoded as 0xAARRGGBB.</param>
/// <param name="ForegroundArgb">Representative text-stroke color encoded as 0xAARRGGBB.</param>
public readonly record struct RibbonGalleryPreviewCell(
    uint BackgroundArgb,
    uint ForegroundArgb);

/// <summary>
/// An immutable row-major color grid for a gallery thumbnail. This visual-only
/// contract contains no workbook, table, native image, or rendering backend state.
/// </summary>
public sealed class RibbonGalleryPreview
{
    /// <summary>Gets the largest supported dimension of a thumbnail grid.</summary>
    public const int MaximumDimension = 16;

    /// <summary>Creates a bounded preview, copying exactly rows times columns cells.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A dimension is outside 1–16.</exception>
    /// <exception cref="ArgumentException">The source has an unexpected cell count.</exception>
    public RibbonGalleryPreview(
        int rows,
        int columns,
        IEnumerable<RibbonGalleryPreviewCell> cells)
    {
        if (rows < 1 || rows > MaximumDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(rows));
        }
        if (columns < 1 || columns > MaximumDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(columns));
        }
        ArgumentNullException.ThrowIfNull(cells);
        var count = rows * columns;
        // Bound enumeration even if a consumer accidentally supplies an infinite source.
        var materialized = cells.Take(count + 1).ToArray();
        if (materialized.Length != count)
        {
            throw new ArgumentException("A preview requires exactly rows times columns cells.", nameof(cells));
        }
        Rows = rows;
        Columns = columns;
        Cells = Array.AsReadOnly(materialized);
    }

    public int Rows { get; }

    public int Columns { get; }

    /// <summary>Gets the immutable cells in row-major order.</summary>
    public IReadOnlyList<RibbonGalleryPreviewCell> Cells { get; }
}
