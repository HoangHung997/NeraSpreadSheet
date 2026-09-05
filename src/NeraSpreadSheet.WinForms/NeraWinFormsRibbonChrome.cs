using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.WinForms;

internal sealed record NeraWinFormsRibbonPalette(
    Color Surface, Color Chrome, Color Text, Color Muted, Color Separator,
    Color Hover, Color Pressed, Color Checked, Color Accent)
{
    internal static NeraWinFormsRibbonPalette For(NeraIconTheme theme) => theme switch
    {
        NeraIconTheme.Dark => new(Color.FromArgb(37, 37, 37), Color.FromArgb(32, 32, 32), Color.FromArgb(244, 244, 244), Color.FromArgb(189, 189, 189), Color.FromArgb(73, 73, 73), Color.FromArgb(58, 70, 64), Color.FromArgb(73, 96, 82), Color.FromArgb(53, 77, 64), Color.FromArgb(105, 213, 160)),
        NeraIconTheme.HighContrastDark => new(Color.Black, Color.Black, Color.White, Color.White, Color.White, Color.FromArgb(58, 58, 58), Color.FromArgb(73, 96, 82), Color.FromArgb(58, 58, 58), Color.FromArgb(255, 239, 0)),
        NeraIconTheme.HighContrastLight => new(Color.White, Color.White, Color.Black, Color.Black, Color.Black, Color.FromArgb(217, 229, 255), Color.FromArgb(200, 226, 210), Color.FromArgb(217, 229, 255), Color.FromArgb(0, 53, 178)),
        _ => new(Color.White, Color.FromArgb(245, 247, 246), Color.FromArgb(36, 41, 45), Color.FromArgb(96, 103, 108), Color.FromArgb(222, 227, 224), Color.FromArgb(234, 242, 237), Color.FromArgb(200, 226, 210), Color.FromArgb(221, 239, 228), Color.FromArgb(24, 115, 74)),
    };
}

internal sealed class NeraWinFormsRibbonColorTable(NeraWinFormsRibbonPalette palette) : ProfessionalColorTable
{
    public override Color MenuItemSelected => palette.Hover;
    public override Color MenuItemBorder => palette.Accent;
    public override Color MenuBorder => palette.Separator;
    public override Color ToolStripDropDownBackground => palette.Surface;
    public override Color ImageMarginGradientBegin => palette.Surface;
    public override Color ImageMarginGradientMiddle => palette.Surface;
    public override Color ImageMarginGradientEnd => palette.Surface;
    public override Color SeparatorDark => palette.Separator;
    public override Color SeparatorLight => palette.Surface;
}

internal static class NeraWinFormsRibbonChrome
{
    internal static void ApplyFilter(Control root, NeraIconTheme theme)
    {
        var palette = NeraWinFormsRibbonPalette.For(theme);
        Apply(root);
        void Apply(Control control)
        {
            control.BackColor = palette.Surface;
            control.ForeColor = palette.Text;
            if (control is Button button)
            {
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderSize = 1;
                button.FlatAppearance.BorderColor = palette.Separator;
                button.FlatAppearance.MouseOverBackColor = palette.Hover;
                button.FlatAppearance.MouseDownBackColor = palette.Pressed;
            }
            foreach (Control child in control.Controls) Apply(child);
        }
    }

    internal static Bitmap CreatePreview(RibbonGalleryPreview preview, int width, int height)
    {
        var bitmap = new Bitmap(width, height);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.White);
        var cellWidth = (float)width / preview.Columns;
        var cellHeight = (float)height / preview.Rows;
        for (var row = 0; row < preview.Rows; row++)
        {
            for (var column = 0; column < preview.Columns; column++)
            {
                var cell = preview.Cells[(row * preview.Columns) + column];
                using var fill = new SolidBrush(Color.FromArgb(unchecked((int)cell.BackgroundArgb)));
                using var line = new Pen(Color.FromArgb(unchecked((int)cell.ForegroundArgb)), 1f);
                var x = column * cellWidth;
                var y = row * cellHeight;
                graphics.FillRectangle(fill, x, y, cellWidth, cellHeight);
                graphics.DrawLine(line, x + 3f, y + (cellHeight / 2f), x + cellWidth - 3f, y + (cellHeight / 2f));
            }
        }
        return bitmap;
    }
}
