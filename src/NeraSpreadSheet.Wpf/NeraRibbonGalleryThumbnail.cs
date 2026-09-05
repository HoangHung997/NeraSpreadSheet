using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Wpf;

internal sealed class NeraRibbonGalleryThumbnail(RibbonGalleryPreview preview) : FrameworkElement
{
    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var width = ActualWidth / preview.Columns;
        var height = ActualHeight / preview.Rows;
        for (var row = 0; row < preview.Rows; row++)
        {
            for (var column = 0; column < preview.Columns; column++)
            {
                var cell = preview.Cells[(row * preview.Columns) + column];
                var bounds = new Rect(column * width, row * height, width, height);
                drawingContext.DrawRectangle(ToBrush(cell.BackgroundArgb), null, bounds);
                var line = new Rect(bounds.X + 2d, bounds.Y + (height / 2d), Math.Max(1d, width - 5d), 1d);
                drawingContext.DrawRectangle(ToBrush(cell.ForegroundArgb), null, line);
            }
        }
    }

    private static SolidColorBrush ToBrush(uint argb) => new(Color.FromArgb(
        (byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
}

internal sealed class NeraRibbonColorConverter : IValueConverter
{
    internal static NeraRibbonColorConverter Instance { get; } = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string text && text.StartsWith('#') && (text.Length is 7 or 9) &&
            uint.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
        {
            if (text.Length == 7) argb |= 0xFF000000;
            return new SolidColorBrush(Color.FromArgb((byte)(argb >> 24), (byte)(argb >> 16), (byte)(argb >> 8), (byte)argb));
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
