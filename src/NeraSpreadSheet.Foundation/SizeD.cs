namespace NeraSpreadSheet.Foundation;

public readonly record struct SizeD
{
    public SizeD(double width, double height)
    {
        Width = Guard.NonNegativeFinite(width, nameof(width));
        Height = Guard.NonNegativeFinite(height, nameof(height));
    }

    public double Width { get; }

    public double Height { get; }

    public static SizeD Empty => new(0d, 0d);
}
