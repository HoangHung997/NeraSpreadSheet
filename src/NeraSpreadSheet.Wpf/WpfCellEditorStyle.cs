using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Wpf;

internal static class WpfCellEditorStyle
{
    public static void Apply(TextBox editor, CellStyle style)
    {
        ArgumentNullException.ThrowIfNull(editor);
        ArgumentNullException.ThrowIfNull(style);

        editor.FontFamily = new FontFamily(style.Font.Family);
        editor.FontSize = style.Font.Size;
        editor.FontWeight = FontWeight.FromOpenTypeWeight(style.Font.Weight);
        editor.FontStyle = style.Font.Italic
            ? FontStyles.Italic
            : FontStyles.Normal;
        if (style.Font.Underline ||
            style.Font.DoubleUnderline ||
            style.Font.StrikeThrough)
        {
            var decorations = new TextDecorationCollection();
            if (style.Font.Underline || style.Font.DoubleUnderline)
            {
                decorations.Add(TextDecorations.Underline[0]);
            }
            if (style.Font.StrikeThrough)
            {
                decorations.Add(TextDecorations.Strikethrough[0]);
            }
            editor.TextDecorations = decorations;
        }
        else
        {
            editor.TextDecorations = null;
        }
        editor.Foreground = new SolidColorBrush(Color.FromArgb(
            style.Font.Color.Alpha,
            style.Font.Color.Red,
            style.Font.Color.Green,
            style.Font.Color.Blue));
        editor.TextWrapping = style.Alignment.WrapText
            ? TextWrapping.Wrap
            : TextWrapping.NoWrap;
        editor.AcceptsReturn = true;
        editor.TextAlignment = style.Alignment.Horizontal switch
        {
            CellHorizontalAlignment.Center => TextAlignment.Center,
            CellHorizontalAlignment.Right => TextAlignment.Right,
            CellHorizontalAlignment.Justify or CellHorizontalAlignment.Distributed => TextAlignment.Justify,
            CellHorizontalAlignment.CenterContinuous => TextAlignment.Center,
            _ => TextAlignment.Left,
        };
        editor.VerticalContentAlignment = style.Alignment.Vertical switch
        {
            CellVerticalAlignment.Top => VerticalAlignment.Top,
            CellVerticalAlignment.Bottom => VerticalAlignment.Bottom,
            _ => VerticalAlignment.Center,
        };
    }
}
