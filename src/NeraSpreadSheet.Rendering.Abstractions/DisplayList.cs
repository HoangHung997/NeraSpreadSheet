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
    private readonly Stack<RenderStateKind> _states = [];

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

    public void DrawDisplayList(DisplayList displayList)
    {
        ArgumentNullException.ThrowIfNull(displayList);
        _commands.Add(new DrawDisplayListCommand(displayList));
    }

    public void PushClip(RectD bounds)
    {
        _commands.Add(new PushClipCommand(bounds));
        _states.Push(RenderStateKind.Clip);
    }

    public void PopClip()
    {
        PopState(RenderStateKind.Clip, "The display-list clip stack is not on top.");
        _commands.Add(new PopClipCommand());
    }

    public void PushTranslation(double deltaX, double deltaY)
    {
        ValidateFinite(deltaX, nameof(deltaX));
        ValidateFinite(deltaY, nameof(deltaY));
        _commands.Add(new PushTranslationCommand(deltaX, deltaY));
        _states.Push(RenderStateKind.Translation);
    }

    public void PopTranslation()
    {
        PopState(RenderStateKind.Translation, "The display-list translation stack is not on top.");
        _commands.Add(new PopTranslationCommand());
    }

    public void Append(DisplayList displayList)
    {
        ArgumentNullException.ThrowIfNull(displayList);
        _commands.AddRange(displayList.Commands);
    }

    public DisplayList Build()
    {
        if (_states.Count != 0)
        {
            throw new InvalidOperationException("The display-list render-state stack is not balanced.");
        }

        return new DisplayList([.. _commands]);
    }

    public void Clear()
    {
        _commands.Clear();
        _states.Clear();
    }

    private void PopState(RenderStateKind expected, string message)
    {
        if (!_states.TryPeek(out var actual) || actual != expected)
        {
            throw new InvalidOperationException(message);
        }
        _states.Pop();
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Translation must be finite.");
        }
    }

    private enum RenderStateKind
    {
        Clip,
        Translation,
    }
}
