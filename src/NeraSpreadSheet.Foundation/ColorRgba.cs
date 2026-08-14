namespace NeraSpreadSheet.Foundation;

public readonly record struct ColorRgba(byte Red, byte Green, byte Blue, byte Alpha = byte.MaxValue)
{
    public static ColorRgba Transparent => new(0, 0, 0, 0);

    public static ColorRgba Black => new(0, 0, 0);

    public static ColorRgba White => new(byte.MaxValue, byte.MaxValue, byte.MaxValue);

    public static ColorRgba GridLine => new(214, 214, 214);

    public static ColorRgba Selection => new(33, 115, 70);
}
