using NeraSpreadSheet.Foundation;

namespace NeraSpreadSheet.Rendering;

public sealed class DisplayList
{
    private readonly RenderCommand[] _commands;

    internal DisplayList(RenderCommand[] commands)
    {
        _commands = commands;
    }

    public IReadOnlyList<RenderCommand> Commands => _commands;

    public int Count => _commands.Length;
}

public sealed class DisplayListBuilder
{
    private readonly List<RenderCommand> _commands = [];
    private int _clipDepth;

    public int Count => _commands.Count;

    public void FillRectangle(RectD bounds, ColorRgba color) =>
        _commands.Add(new FillRectangleCommand(bounds, color));

    public void DrawLine(PointD start, PointD end, double strokeWidth, ColorRgba color)
    {
        if (!double.IsFinite(strokeWidth) || strokeWidth <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(strokeWidth));
        }

        _commands.Add(new DrawLineCommand(start, end, strokeWidth, color));
    }

    public void DrawText(string text, RectD bounds, TextStyle style)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(style);
        _commands.Add(new DrawTextCommand(text, bounds, style));
    }

    public void PushClip(RectD bounds)
    {
        _commands.Add(new PushClipCommand(bounds));
        _clipDepth++;
    }

    public void PopClip()
    {
        if (_clipDepth <= 0)
        {
            throw new InvalidOperationException("The display-list clip stack is empty.");
        }

        _commands.Add(new PopClipCommand());
        _clipDepth--;
    }

    public DisplayList Build()
    {
        if (_clipDepth != 0)
        {
            throw new InvalidOperationException("The display-list clip stack is not balanced.");
        }

        return new DisplayList([.. _commands]);
    }

    public void Clear()
    {
        _commands.Clear();
        _clipDepth = 0;
    }
}
