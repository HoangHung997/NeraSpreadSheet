using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Rendering;

public abstract record RenderCommand;

public sealed record FillRectangleCommand(RectD Bounds, ColorRgba Color) : RenderCommand;

public sealed record DrawLineCommand(
    PointD Start,
    PointD End,
    double StrokeWidth,
    ColorRgba Color) : RenderCommand;

public sealed record DrawTextCommand(
    string Text,
    RectD Bounds,
    TextStyle Style) : RenderCommand;

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
