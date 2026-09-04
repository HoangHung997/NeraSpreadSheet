using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Wpf;

public sealed partial class NeraAutoFilterPagedPopupPresenter
{
    private void OnLoaded(object sender, RoutedEventArgs e) =>
        AttachAdorner();

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Close();
        DetachAdorner();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) =>
        _adorner?.InvalidateVisual();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        _adorner?.InvalidateVisual();

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        _adorner?.InvalidateVisual();

    private void OnControlPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (IsOpen)
        {
            return;
        }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if ((e.KeyboardDevice.Modifiers & ModifierKeys.Alt) != 0 &&
            key == Key.Down &&
            TryOpenForActiveCell())
        {
            e.Handled = true;
        }
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(_control);
        if (TryHitTest(point.X, point.Y, out _))
        {
            _control.Cursor = Cursors.Hand;
        }
        else if (_control.Cursor == Cursors.Hand)
        {
            _control.Cursor = null;
        }
    }

    private void OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var point = e.GetPosition(_control);
        if (TryOpenAt(point.X, point.Y))
        {
            e.Handled = true;
        }
    }

    private void AttachAdorner()
    {
        if (_adorner is not null)
        {
            return;
        }
        var layer = AdornerLayer.GetAdornerLayer(_control);
        if (layer is null)
        {
            return;
        }
        _adorner = new FilterButtonAdorner(_control, this);
        layer.Add(_adorner);
    }

    private void DetachAdorner()
    {
        if (_adorner is null)
        {
            return;
        }
        AdornerLayer.GetAdornerLayer(_control)?.Remove(_adorner);
        _adorner = null;
    }

    private static SpreadsheetAutoFilterButtonOwnerKind ToGeometryOwner(
        SpreadsheetAutoFilterOwnerKind ownerKind) =>
        ownerKind switch
        {
            SpreadsheetAutoFilterOwnerKind.Table =>
                SpreadsheetAutoFilterButtonOwnerKind.Table,
            SpreadsheetAutoFilterOwnerKind.Worksheet =>
                SpreadsheetAutoFilterButtonOwnerKind.Worksheet,
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind)),
        };

    private static string DisplayValue(CellValue value) =>
        value.IsBlank ? "(Trống)" : value.ToString();

    private static System.Windows.Media.Color ToColor(ColorRgba color) =>
        System.Windows.Media.Color.FromArgb(
            color.Alpha,
            color.Red,
            color.Green,
            color.Blue);

    private sealed class FilterButtonAdorner : Adorner
    {
        private readonly NeraAutoFilterPagedPopupPresenter _presenter;

        public FilterButtonAdorner(
            UIElement adornedElement,
            NeraAutoFilterPagedPopupPresenter presenter)
            : base(adornedElement)
        {
            _presenter = presenter;
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var theme = _presenter._control.RenderTheme;
            foreach (var button in _presenter.GetVisibleButtons())
            {
                var bounds = new Rect(
                    button.Bounds.X,
                    button.Bounds.Y,
                    button.Bounds.Width,
                    button.Bounds.Height);
                var fill = new SolidColorBrush(ToColor(
                    button.IsFiltered
                        ? theme.TableFilterButtonActiveBackground
                        : theme.TableFilterButtonBackground));
                var border = new Pen(
                    new SolidColorBrush(ToColor(
                        theme.TableFilterButtonBorder)),
                    1d);
                drawingContext.DrawRoundedRectangle(
                    fill,
                    border,
                    bounds,
                    2d,
                    2d);

                var centerX = bounds.Left + (bounds.Width / 2d);
                var centerY = bounds.Top + (bounds.Height / 2d) + 1d;
                var glyph = new StreamGeometry();
                using (var context = glyph.Open())
                {
                    var pointsUp = button.IsSorted && button.SortDescending != true;
                    context.BeginFigure(
                        pointsUp
                            ? new Point(centerX, centerY - 3d)
                            : new Point(centerX - 3.5d, centerY - 2d),
                        isFilled: true,
                        isClosed: true);
                    context.LineTo(
                        pointsUp
                            ? new Point(centerX + 3.5d, centerY + 2.5d)
                            : new Point(centerX + 3.5d, centerY - 2d),
                        isStroked: true,
                        isSmoothJoin: false);
                    context.LineTo(
                        pointsUp
                            ? new Point(centerX - 3.5d, centerY + 2.5d)
                            : new Point(centerX, centerY + 2.5d),
                        isStroked: true,
                        isSmoothJoin: false);
                }
                drawingContext.DrawGeometry(
                    new SolidColorBrush(ToColor(
                        theme.TableFilterButtonGlyph)),
                    pen: null,
                    glyph);
            }
        }
    }
}
