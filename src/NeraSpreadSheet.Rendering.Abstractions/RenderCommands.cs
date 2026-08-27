using System.Collections.ObjectModel;
using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Rendering;

public abstract record RenderCommand;

public sealed record FillRectangleCommand(RectD Bounds, ColorRgba Color) : RenderCommand;

public sealed record FillPolygonCommand : RenderCommand
{
    public FillPolygonCommand(
        IEnumerable<PointD> points,
        ColorRgba color)
    {
        ArgumentNullException.ThrowIfNull(points);
        var materialized = points.ToArray();
        if (materialized.Length < 3)
        {
            throw new ArgumentException(
                "A filled polygon requires at least three points.",
                nameof(points));
        }
        if (materialized.Any(static point =>
                !double.IsFinite(point.X) ||
                !double.IsFinite(point.Y)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(points),
                "Polygon coordinates must be finite.");
        }

        Points = Array.AsReadOnly(materialized);
        Color = color;
    }

    public ReadOnlyCollection<PointD> Points { get; }

    public ColorRgba Color { get; }
}

public sealed record DrawLineCommand(
    PointD Start,
    PointD End,
    double StrokeWidth,
    ColorRgba Color) : RenderCommand;

public sealed record DrawTextCommand(
    string Text,
    RectD Bounds,
    TextStyle Style) : RenderCommand;

public sealed record DrawDisplayListCommand(DisplayList DisplayList) : RenderCommand;

public sealed record PushClipCommand(RectD Bounds) : RenderCommand;

public sealed record PopClipCommand : RenderCommand;

public sealed record PushTranslationCommand(double DeltaX, double DeltaY) : RenderCommand;

public sealed record PopTranslationCommand : RenderCommand;

public sealed record TextStyle(
    string FontFamily,
    double FontSize,
    int FontWeight,
    ColorRgba Color,
    bool Wrap = false);
