using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// Attachable native WPF presenter for Table AutoFilter buttons and value menus.
/// All filter semantics remain in <see cref="SpreadsheetTablePresenterController"/>.
/// </summary>
public sealed class NeraTableFilterPopupPresenter : IDisposable
{
    private const double PopupWidth = 320d;
    private const double PopupMaximumHeight = 440d;

    private readonly NeraSpreadsheetControl _control;
    private SpreadsheetSession? _viewportSession;
    private SpreadsheetViewportEngine? _viewport;
    private FilterButtonAdorner? _adorner;
    private Popup? _popup;
    private bool _disposed;

    public NeraTableFilterPopupPresenter(NeraSpreadsheetControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _control.Loaded += OnLoaded;
        _control.Unloaded += OnUnloaded;
        _control.LayoutUpdated += OnLayoutUpdated;
        _control.SizeChanged += OnSizeChanged;
        _control.ScrollChanged += OnScrollChanged;
        _control.PreviewMouseMove += OnPreviewMouseMove;
        _control.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
        if (_control.IsLoaded)
        {
            AttachAdorner();
        }
    }

    public bool IsOpen => _popup?.IsOpen == true;

    public void Close()
    {
        if (_popup is not null)
        {
            _popup.IsOpen = false;
        }
    }

    public bool TryOpenAt(double x, double y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryHitTest(x, y, out var hit))
        {
            return false;
        }

        Open(hit);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Close();
        DetachAdorner();
        _control.Loaded -= OnLoaded;
        _control.Unloaded -= OnUnloaded;
        _control.LayoutUpdated -= OnLayoutUpdated;
        _control.SizeChanged -= OnSizeChanged;
        _control.ScrollChanged -= OnScrollChanged;
        _control.PreviewMouseMove -= OnPreviewMouseMove;
        _control.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        _disposed = true;
    }

    internal IReadOnlyList<SpreadsheetTableFilterButtonHit> GetVisibleButtons()
    {
        var session = _control.Session;
        if (session is null ||
            _control.ActualWidth <= 0d ||
            _control.ActualHeight <= 0d ||
            !_control.RenderTheme.ShowTableFilterButtons)
        {
            return [];
        }

        var chrome = SpreadsheetChromeGeometry.Calculate(
            _control.ActualWidth,
            _control.ActualHeight,
            _control.RenderTheme);
        if (chrome.BodyWidth <= 0d || chrome.BodyHeight <= 0d)
        {
            return [];
        }

        if (!ReferenceEquals(_viewportSession, session))
        {
            _viewportSession = session;
            _viewport = new SpreadsheetViewportEngine(session);
        }

        var scroll = _control.ScrollSnapshot;
        var frame = _viewport!.Compose(
            scroll.OffsetX,
            scroll.OffsetY,
            chrome.BodyWidth,
            chrome.BodyHeight,
            overscan: 0d,
            _control.RenderTheme);
        var buttons = SpreadsheetTableFilterButtonGeometry.GetVisibleButtons(
            WorksheetSnapshot.Capture(session.ActiveWorksheet),
            frame.Layout,
            _control.RenderTheme);
        return buttons
            .Select(button => button with
            {
                Bounds = button.Bounds.Translate(
                    chrome.RowHeaderWidth,
                    chrome.ColumnHeaderHeight),
            })
            .ToArray();
    }

    private bool TryHitTest(
        double x,
        double y,
        out SpreadsheetTableFilterButtonHit hit)
    {
        var point = new PointD(x, y);
        foreach (var candidate in GetVisibleButtons())
        {
            if (candidate.Bounds.Contains(point))
            {
                hit = candidate;
                return true;
            }
        }

        hit = default;
        return false;
    }

    private void Open(SpreadsheetTableFilterButtonHit hit)
    {
        var session = _control.Session
            ?? throw new InvalidOperationException(
                "A spreadsheet session is required before opening a Table filter menu.");
        var menu = new SpreadsheetTablePresenterController(session)
            .OpenFilterMenu(hit.TableId, hit.ColumnId);
        Close();

        var popup = new Popup
        {
            PlacementTarget = _control,
            Placement = PlacementMode.RelativePoint,
            HorizontalOffset = Math.Max(0d, hit.Bounds.Left),
            VerticalOffset = Math.Max(0d, hit.Bounds.Bottom),
            StaysOpen = false,
            AllowsTransparency = true,
            Child = BuildPopupContent(menu),
        };
        popup.Closed += (_, _) =>
        {
            if (ReferenceEquals(_popup, popup))
            {
                _popup = null;
            }
        };
        _popup = popup;
        popup.IsOpen = true;
    }

    private Border BuildPopupContent(
        SpreadsheetTableFilterMenu menu)
    {
        var root = new Border
        {
            Width = PopupWidth,
            MaxHeight = PopupMaximumHeight,
            Background = Brushes.White,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1d),
            Padding = new Thickness(10d),
            CornerRadius = new CornerRadius(4d),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 12d,
                Opacity = 0.25d,
                ShadowDepth = 2d,
            },
        };
        var panel = new DockPanel
        {
            LastChildFill = true,
        };
        root.Child = panel;

        var title = new TextBlock
        {
            Text = $"{menu.TableName} — {menu.ColumnName}",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 0d, 0d, 8d),
        };
        DockPanel.SetDock(title, Dock.Top);
        panel.Children.Add(title);

        var search = new TextBox
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            ToolTip = "Tìm giá trị",
        };
        DockPanel.SetDock(search, Dock.Top);
        panel.Children.Add(search);

        var commands = new WrapPanel
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
        };
        var selectAll = CreateCommandButton("Chọn tất cả");
        var selectNone = CreateCommandButton("Bỏ chọn");
        commands.Children.Add(selectAll);
        commands.Children.Add(selectNone);
        DockPanel.SetDock(commands, Dock.Top);
        panel.Children.Add(commands);

        var status = new TextBlock
        {
            Foreground = Brushes.DimGray,
            FontSize = 11d,
            Margin = new Thickness(0d, 0d, 0d, 6d),
        };
        DockPanel.SetDock(status, Dock.Top);
        panel.Children.Add(status);

        var itemsPanel = new StackPanel();
        var scroller = new ScrollViewer
        {
            Content = itemsPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 270d,
        };
        panel.Children.Add(scroller);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0d, 8d, 0d, 0d),
        };
        var clear = CreateCommandButton("Xóa lọc");
        var cancel = CreateCommandButton("Hủy");
        var apply = CreateCommandButton("Áp dụng");
        footer.Children.Add(clear);
        footer.Children.Add(cancel);
        footer.Children.Add(apply);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(footer);

        void RebuildItems()
        {
            itemsPanel.Children.Clear();
            foreach (var item in menu.GetVisibleItems())
            {
                var value = item.Value;
                var checkBox = new CheckBox
                {
                    IsChecked = item.IsSelected,
                    Content = $"{DisplayValue(item.Value)}  ({item.Count})",
                    Margin = new Thickness(2d),
                };
                checkBox.Checked += (_, _) => menu.SelectValue(value, true);
                checkBox.Unchecked += (_, _) => menu.SelectValue(value, false);
                itemsPanel.Children.Add(checkBox);
            }

            status.Text = menu.ValuesTruncated
                ? $"Đã quét {menu.ScannedRowCount:N0} hàng; danh sách giá trị đã bị giới hạn."
                : $"{menu.DistinctValueCount:N0} giá trị khác nhau trong {menu.ScannedRowCount:N0} hàng.";
            apply.IsEnabled = menu.CanApplyValueSelection;
        }

        search.TextChanged += (_, _) =>
        {
            menu.SetSearchText(search.Text);
            RebuildItems();
        };
        selectAll.Click += (_, _) =>
        {
            menu.SelectAllVisible();
            RebuildItems();
        };
        selectNone.Click += (_, _) =>
        {
            menu.ClearVisibleSelection();
            RebuildItems();
        };
        apply.Click += (_, _) =>
        {
            menu.ApplyValueSelection();
            CloseAndRefresh();
        };
        clear.Click += (_, _) =>
        {
            menu.ClearColumnFilter();
            CloseAndRefresh();
        };
        cancel.Click += (_, _) => Close();

        RebuildItems();
        return root;
    }

    private static Button CreateCommandButton(string text) =>
        new()
        {
            Content = text,
            MinWidth = 74d,
            Margin = new Thickness(2d),
            Padding = new Thickness(8d, 3d, 8d, 3d),
        };

    private void CloseAndRefresh()
    {
        Close();
        _viewport?.InvalidateMetrics();
        _control.InvalidateVisual();
        _adorner?.InvalidateVisual();
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => AttachAdorner();

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
        if (!TryOpenAt(point.X, point.Y))
        {
            return;
        }

        e.Handled = true;
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
        private readonly NeraTableFilterPopupPresenter _presenter;

        public FilterButtonAdorner(
            UIElement adornedElement,
            NeraTableFilterPopupPresenter presenter)
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
                    new SolidColorBrush(ToColor(theme.TableFilterButtonBorder)),
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
                    context.BeginFigure(
                        new Point(centerX - 3.5d, centerY - 2d),
                        isFilled: true,
                        isClosed: true);
                    context.LineTo(
                        new Point(centerX + 3.5d, centerY - 2d),
                        isStroked: true,
                        isSmoothJoin: false);
                    context.LineTo(
                        new Point(centerX, centerY + 2.5d),
                        isStroked: true,
                        isSmoothJoin: false);
                }
                drawingContext.DrawGeometry(
                    new SolidColorBrush(ToColor(theme.TableFilterButtonGlyph)),
                    pen: null,
                    glyph);
            }
        }
    }
}
