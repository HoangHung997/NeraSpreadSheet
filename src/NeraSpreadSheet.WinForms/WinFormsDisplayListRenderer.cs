using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering;

namespace NeraSpreadSheet.WinForms;

internal sealed class WinFormsDisplayListRenderer : IDisposable
{
    private readonly Dictionary<ColorRgba, SolidBrush> _brushes = [];
    private readonly Dictionary<(ColorRgba Color, float Width), Pen> _pens = [];
    private readonly Dictionary<(string Family, float Size, FontStyle Style), Font> _fonts = [];
    private readonly StringFormat _singleLineFormat;
    private readonly StringFormat _wrappedFormat;
    private bool _disposed;

    public WinFormsDisplayListRenderer()
    {
        _singleLineFormat = new StringFormat(StringFormat.GenericDefault)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        _wrappedFormat = new StringFormat(StringFormat.GenericDefault)
        {
            Alignment = StringAlignment.Near,
            LineAlignment = StringAlignment.Near,
            Trimming = StringTrimming.EllipsisCharacter,
        };
    }

    public void Render(Graphics graphics, DisplayList displayList)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(graphics);
        ArgumentNullException.ThrowIfNull(displayList);

        graphics.SmoothingMode = SmoothingMode.None;
        graphics.PixelOffsetMode = PixelOffsetMode.Half;
        graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        var states = new Stack<RenderState>();
        var offsetX = 0d;
        var offsetY = 0d;
        try
        {
            ExecuteDisplayList(graphics, displayList, states, ref offsetX, ref offsetY);
        }
        finally
        {
            while (states.TryPop(out var state))
            {
                if (state.Kind == RenderStateKind.Clip)
                {
                    graphics.Restore(state.GraphicsState!);
                }
                else
                {
                    offsetX = state.PreviousOffsetX;
                    offsetY = state.PreviousOffsetY;
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        foreach (var brush in _brushes.Values) brush.Dispose();
        foreach (var pen in _pens.Values) pen.Dispose();
        foreach (var font in _fonts.Values) font.Dispose();
        _singleLineFormat.Dispose();
        _wrappedFormat.Dispose();
        _disposed = true;
    }

    private void ExecuteDisplayList(
        Graphics graphics,
        DisplayList displayList,
        Stack<RenderState> states,
        ref double offsetX,
        ref double offsetY)
    {
        foreach (var command in displayList.Commands)
        {
            switch (command)
            {
                case FillRectangleCommand fill:
                    graphics.FillRectangle(GetBrush(fill.Color), ToRectangleF(fill.Bounds.Translate(offsetX, offsetY)));
                    break;
                case FillPolygonCommand polygon:
                    DrawPolygon(graphics, polygon, offsetX, offsetY);
                    break;
                case DrawLineCommand line:
                    graphics.DrawLine(
                        GetPen(line.Color, line.StrokeWidth),
                        ToPointF(line.Start, offsetX, offsetY),
                        ToPointF(line.End, offsetX, offsetY));
                    break;
                case DrawTextCommand text:
                    DrawText(graphics, text, offsetX, offsetY);
                    break;
                case DrawDisplayListCommand nested:
                    ExecuteDisplayList(graphics, nested.DisplayList, states, ref offsetX, ref offsetY);
                    break;
                case PushClipCommand pushClip:
                {
                    var graphicsState = graphics.Save();
                    graphics.SetClip(ToRectangleF(pushClip.Bounds.Translate(offsetX, offsetY)), CombineMode.Intersect);
                    states.Push(new RenderState(RenderStateKind.Clip, graphicsState, offsetX, offsetY));
                    break;
                }
                case PopClipCommand:
                {
                    var state = EnsureTopState(states, RenderStateKind.Clip);
                    states.Pop();
                    graphics.Restore(state.GraphicsState!);
                    break;
                }
                case PushTranslationCommand translation:
                    states.Push(new RenderState(RenderStateKind.Translation, null, offsetX, offsetY));
                    offsetX += translation.DeltaX;
                    offsetY += translation.DeltaY;
                    break;
                case PopTranslationCommand:
                {
                    var state = EnsureTopState(states, RenderStateKind.Translation);
                    states.Pop();
                    offsetX = state.PreviousOffsetX;
                    offsetY = state.PreviousOffsetY;
                    break;
                }
                default:
                    throw new NotSupportedException($"Unsupported render command '{command.GetType().Name}'.");
            }
        }
    }

    private void DrawPolygon(
        Graphics graphics,
        FillPolygonCommand command,
        double offsetX,
        double offsetY)
    {
        var points = new PointF[command.Points.Count];
        for (var index = 0; index < command.Points.Count; index++)
        {
            points[index] = ToPointF(command.Points[index], offsetX, offsetY);
        }
        graphics.FillPolygon(GetBrush(command.Color), points);
    }

    private void DrawText(Graphics graphics, DrawTextCommand command, double offsetX, double offsetY)
    {
        if (string.IsNullOrEmpty(command.Text) || command.Bounds.Width <= 0d || command.Bounds.Height <= 0d)
        {
            return;
        }
        graphics.DrawString(
            command.Text,
            GetFont(command.Style),
            GetBrush(command.Style.Color),
            ToRectangleF(command.Bounds.Translate(offsetX, offsetY)),
            command.Style.Wrap ? _wrappedFormat : _singleLineFormat);
    }

    private SolidBrush GetBrush(ColorRgba color)
    {
        if (_brushes.TryGetValue(color, out var brush)) return brush;
        brush = new SolidBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue));
        _brushes.Add(color, brush);
        return brush;
    }

    private Pen GetPen(ColorRgba color, double width)
    {
        var floatWidth = (float)width;
        var key = (color, floatWidth);
        if (_pens.TryGetValue(key, out var pen)) return pen;
        pen = new Pen(GetBrush(color), floatWidth);
        _pens.Add(key, pen);
        return pen;
    }

    private Font GetFont(TextStyle style)
    {
        var fontStyle = style.FontWeight >= 600 ? FontStyle.Bold : FontStyle.Regular;
        var key = (style.FontFamily, (float)style.FontSize, fontStyle);
        if (_fonts.TryGetValue(key, out var font)) return font;
        font = new Font(style.FontFamily, key.Item2, fontStyle, GraphicsUnit.Pixel);
        _fonts.Add(key, font);
        return font;
    }

    private static RenderState EnsureTopState(Stack<RenderState> states, RenderStateKind expected)
    {
        if (!states.TryPeek(out var state) || state.Kind != expected)
        {
            throw new InvalidOperationException("Display-list render-state stack is unbalanced.");
        }
        return state;
    }

    private static RectangleF ToRectangleF(RectD bounds) =>
        new((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height);

    private static PointF ToPointF(PointD point, double offsetX, double offsetY) =>
        new((float)(point.X + offsetX), (float)(point.Y + offsetY));

    private readonly record struct RenderState(
        RenderStateKind Kind,
        GraphicsState? GraphicsState,
        double PreviousOffsetX,
        double PreviousOffsetY);

    private enum RenderStateKind
    {
        Clip,
        Translation,
    }
}