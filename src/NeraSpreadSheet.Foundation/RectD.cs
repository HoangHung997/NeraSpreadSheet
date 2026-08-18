namespace NeraSpreadSheet.Foundation;

public readonly record struct RectD
{
    public RectD(double x, double y, double width, double height)
    {
        if (!double.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if (!double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        X = x;
        Y = y;
        Width = Guard.NonNegativeFinite(width, nameof(width));
        Height = Guard.NonNegativeFinite(height, nameof(height));
    }

    public double X { get; }

    public double Y { get; }

    public double Width { get; }

    public double Height { get; }

    public double Left => X;

    public double Top => Y;

    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool IsEmpty => Width <= 0d || Height <= 0d;

    public static RectD Empty => new(0d, 0d, 0d, 0d);

    public bool Contains(PointD point) =>
        point.X >= Left && point.X <= Right && point.Y >= Top && point.Y <= Bottom;

    public bool Contains(RectD other) =>
        other.Left >= Left &&
        other.Top >= Top &&
        other.Right <= Right &&
        other.Bottom <= Bottom;

    public bool IntersectsWith(RectD other) =>
        other.Left < Right && other.Right > Left && other.Top < Bottom && other.Bottom > Top;

    public RectD Translate(double deltaX, double deltaY) => new(X + deltaX, Y + deltaY, Width, Height);

    public RectD Intersect(RectD other)
    {
        var left = Math.Max(Left, other.Left);
        var top = Math.Max(Top, other.Top);
        var right = Math.Min(Right, other.Right);
        var bottom = Math.Min(Bottom, other.Bottom);

        return right <= left || bottom <= top
            ? Empty
            : new RectD(left, top, right - left, bottom - top);
    }
}
