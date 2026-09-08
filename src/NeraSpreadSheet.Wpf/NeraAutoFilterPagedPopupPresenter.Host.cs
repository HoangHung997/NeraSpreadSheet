using NeraSpreadSheet.Commands;
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
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SynchronizeHost();
        AttachAdorner();
        RefreshHostPresentation();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        CloseForHostChange();
        _nativeButtons = [];
        DetachAdorner();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) =>
        QueueHostRefresh();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        QueueHostRefresh();

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        QueueHostRefresh();

    private void OnControlPreviewKeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Handled || IsOpen)
        {
            return;
        }
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if ((e.KeyboardDevice.Modifiers & ModifierKeys.Alt) != 0 &&
            key == Key.Down &&
            (_control.CurrentEditorDraft is not null || TryOpenForActiveCell()))
        {
            e.Handled = true;
        }
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.Handled || _inputSurface is null) return;
        var point = e.GetPosition(_inputSurface);
        if (TryHitTest(point.X, point.Y, out _))
        {
            _inputSurface.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
        }
        else if (Equals(_inputSurface.GetValue(FrameworkElement.CursorProperty), Cursors.Hand))
        {
            _inputSurface.ClearValue(FrameworkElement.CursorProperty);
        }
    }

    private void OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.Handled || _inputSurface is null) return;
        var point = e.GetPosition(_inputSurface);
        if (!TryHitTest(point.X, point.Y, out var button)) return;
        if (_control.CurrentEditorDraft is not null)
        {
            e.Handled = true;
            return;
        }
        if (!IsHostReady || !_inputSurface.CaptureMouse()) return;
        _pendingPointer = (_openGeneration, button);
        e.Handled = true;
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_pendingPointer is not { } pending || _inputSurface is null) return;
        var point = e.GetPosition(_inputSurface);
        var shouldOpen = !e.Handled && pending.Generation == _openGeneration &&
            TryHitTest(point.X, point.Y, out var button) && SameHeader(button, pending.Button);
        // Release the initiating click before the popup takes its outside-click capture.
        CancelPointerOpen();
        e.Handled = true;
        if (shouldOpen) TryOpenAt(point.X, point.Y);
    }

    private void OnFilterLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (_inputSurface?.IsMouseCaptured != true) _pendingPointer = null;
    }

    private void CancelPointerOpen()
    {
        if (_pendingPointer is null) return;
        _pendingPointer = null;
        if (_inputSurface?.IsMouseCaptured == true) _inputSurface.ReleaseMouseCapture();
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
        _adorner.Refresh();
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

    private string DisplayValue(CellValue value) =>
        value.IsBlank ? Localization.Get("(Trống)") : value.ToString();

    private static System.Windows.Media.Color ToColor(ColorRgba color) =>
        System.Windows.Media.Color.FromArgb(
            color.Alpha,
            color.Red,
            color.Green,
            color.Blue);

    private sealed class FilterButtonAdorner : Adorner
    {
        private readonly NeraAutoFilterPagedPopupPresenter _presenter;
        private SpreadsheetAutoFilterButtonHit[] _buttons = [];
        private SpreadsheetRenderTheme? _theme;

        public FilterButtonAdorner(
            UIElement adornedElement,
            NeraAutoFilterPagedPopupPresenter presenter)
            : base(adornedElement)
        {
            _presenter = presenter;
            IsHitTestVisible = false;
        }

        internal void Refresh()
        {
            var buttons = _presenter.GetVisibleButtons();
            var theme = _presenter._control.RenderTheme;
            if (ReferenceEquals(theme, _theme) && buttons.SequenceEqual(_buttons)) return;
            _buttons = buttons;
            _theme = theme;
            // InvalidateVisual also invalidates arrange. Repeating it for every
            // LayoutUpdated event would starve dispatcher idle work indefinitely.
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            var theme = _presenter._control.RenderTheme;
            foreach (var button in _buttons)
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

                WpfFilterHeaderGlyphRenderer.Draw(
                    drawingContext,
                    bounds,
                    button.HeaderState,
                    button.SortDescending,
                    new SolidColorBrush(ToColor(
                        theme.TableFilterButtonGlyph)));
            }
        }
    }
}
