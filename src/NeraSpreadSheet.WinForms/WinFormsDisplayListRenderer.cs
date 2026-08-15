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
        var states = new Stack<GraphicsState>();
        try
        {
            foreach (var command in displayList.Commands)
            {
                switch (command)
                {
                    case FillRectangleCommand fill:
                        graphics.FillRectangle(GetBrush(fill.Color), ToRectangleF(fill.Bounds));
                        break;
                    case DrawLineCommand line:
                        graphics.DrawLine(GetPen(line.Color, line.StrokeWidth), ToPointF(line.Start), ToPointF(line.End));
                        break;
                    case DrawTextCommand text:
                        DrawText(graphics, text);
                        break;
                    case PushClipCommand pushClip:
                        states.Push(graphics.Save());
                        graphics.SetClip(ToRectangleF(pushClip.Bounds), CombineMode.Intersect);
                        break;
                    case PopClipCommand:
                        if (!states.TryPop(out var state))
                        {
                            throw new InvalidOperationException("Display-list clip stack is unbalanced.");
                        }
                        graphics.Restore(state);
                        break;
                    default:
                        throw new NotSupportedException($"Unsupported render command '{command.GetType().Name}'.");
                }
            }
        }
        finally
        {
            while (states.TryPop(out var state))
            {
                graphics.Restore(state);
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

    private void DrawText(Graphics graphics, DrawTextCommand command)
    {
        if (string.IsNullOrEmpty(command.Text) || command.Bounds.Width <= 0d || command.Bounds.Height <= 0d)
        {
            return;
        }
        graphics.DrawString(
            command.Text,
            GetFont(command.Style),
            GetBrush(command.Style.Color),
            ToRectangleF(command.Bounds),
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

    private static RectangleF ToRectangleF(RectD bounds) => new((float)bounds.X, (float)bounds.Y, (float)bounds.Width, (float)bounds.Height);
    private static PointF ToPointF(PointD point) => new((float)point.X, (float)point.Y);
}
