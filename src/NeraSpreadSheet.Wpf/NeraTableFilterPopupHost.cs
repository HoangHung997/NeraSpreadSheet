using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// Native WPF popup for the platform-neutral Table filter menu. The popup never
/// mutates Table state directly; all changes flow through SpreadsheetSession history.
/// </summary>
public sealed class NeraTableFilterPopup : IDisposable
{
    private readonly SpreadsheetTablePresenterController _presenter;
    private readonly Popup _popup;
    private readonly TextBox _searchBox;
    private readonly TextBlock _summary;
    private readonly StackPanel _valuesPanel;
    private SpreadsheetTableFilterMenu? _menu;
    private bool _refreshing;
    private bool _disposed;

    public NeraTableFilterPopup(SpreadsheetSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _presenter = new SpreadsheetTablePresenterController(session);
        _searchBox = new TextBox
        {
            Margin = new Thickness(0d, 0d, 0d, 6d),
            MinWidth = 240d,
            ToolTip = "Tìm giá trị trong cột",
        };
        _searchBox.TextChanged += OnSearchChanged;
        _summary = new TextBlock
        {
            Margin = new Thickness(0d, 0d, 0d, 4d),
            Foreground = Brushes.DimGray,
        };
        _valuesPanel = new StackPanel();
        var scrollViewer = new ScrollViewer
        {
            Content = _valuesPanel,
            MaxHeight = 320d,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };
        var selectAll = CreateButton("Chọn tất cả", OnSelectAll);
        var clearVisible = CreateButton("Bỏ chọn đang thấy", OnClearVisible);
        var clearFilter = CreateButton("Xóa bộ lọc", OnClearFilter);
        var apply = CreateButton("Áp dụng", OnApply);
        var cancel = CreateButton("Đóng", (_, _) => Close());
        var buttons = new WrapPanel
        {
            Margin = new Thickness(0d, 8d, 0d, 0d),
        };
        buttons.Children.Add(selectAll);
        buttons.Children.Add(clearVisible);
        buttons.Children.Add(clearFilter);
        buttons.Children.Add(apply);
        buttons.Children.Add(cancel);

        var content = new StackPanel();
        content.Children.Add(_searchBox);
        content.Children.Add(_summary);
        content.Children.Add(scrollViewer);
        content.Children.Add(buttons);
        var border = new Border
        {
            Background = Brushes.White,
            BorderBrush = Brushes.Gray,
            BorderThickness = new Thickness(1d),
            CornerRadius = new CornerRadius(3d),
            Padding = new Thickness(10d),
            Child = content,
        };
        _popup = new Popup
        {
            AllowsTransparency = true,
            Child = border,
            Placement = PlacementMode.Relative,
            StaysOpen = false,
        };
        _popup.Closed += OnPopupClosed;
    }

    public bool IsOpen => _popup.IsOpen;

    public event EventHandler? Closed;

    public void Open(
        UIElement placementTarget,
        RectD anchorBounds,
        Guid tableId,
        Guid columnId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(placementTarget);
        Close();
        _menu = _presenter.OpenFilterMenu(tableId, columnId);
        _menu.Changed += OnMenuChanged;
        _searchBox.Text = string.Empty;
        _popup.PlacementTarget = placementTarget;
        _popup.HorizontalOffset = anchorBounds.Left;
        _popup.VerticalOffset = anchorBounds.Bottom;
        RefreshFromMenu();
        _popup.IsOpen = true;
        _searchBox.Focus();
    }

    public void Close()
    {
        if (_popup.IsOpen)
        {
            _popup.IsOpen = false;
            return;
        }
        DetachMenu();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Close();
        _popup.Closed -= OnPopupClosed;
        _searchBox.TextChanged -= OnSearchChanged;
        _disposed = true;
    }

    private static Button CreateButton(
        string text,
        RoutedEventHandler handler)
    {
        var button = new Button
        {
            Content = text,
            Margin = new Thickness(0d, 0d, 6d, 6d),
            Padding = new Thickness(8d, 3d, 8d, 3d),
            MinWidth = 72d,
        };
        button.Click += handler;
        return button;
    }

    private void RefreshFromMenu()
    {
        if (_menu is null)
        {
            return;
        }
        _refreshing = true;
        try
        {
            var state = _menu.Capture();
            _summary.Text = state.IsTruncated
                ? $"Đã quét {state.ScannedRowCount}/{state.SourceRowCount} hàng; danh sách bị giới hạn."
                : $"{state.DistinctValueCount} giá trị; {state.SourceRowCount} hàng dữ liệu.";
            _valuesPanel.Children.Clear();
            foreach (var item in state.Values)
            {
                var checkBox = new CheckBox
                {
                    Content = $"{item.DisplayText} ({item.Count})",
                    IsChecked = item.IsSelected,
                    Tag = item.Value,
                    Margin = new Thickness(0d, 2d, 0d, 2d),
                    MinWidth = 220d,
                };
                checkBox.Checked += OnValueChecked;
                checkBox.Unchecked += OnValueUnchecked;
                _valuesPanel.Children.Add(checkBox);
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void OnSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!_refreshing)
        {
            _menu?.SetSearchText(_searchBox.Text);
        }
    }

    private void OnValueChecked(object sender, RoutedEventArgs e) =>
        SetValueSelection(sender, selected: true);

    private void OnValueUnchecked(object sender, RoutedEventArgs e) =>
        SetValueSelection(sender, selected: false);

    private void SetValueSelection(object sender, bool selected)
    {
        if (_refreshing ||
            _menu is null ||
            sender is not CheckBox { Tag: CellValue value })
        {
            return;
        }
        _menu.SetSelected(value, selected);
    }

    private void OnSelectAll(object sender, RoutedEventArgs e) =>
        _menu?.SelectAllVisible();

    private void OnClearVisible(object sender, RoutedEventArgs e) =>
        _menu?.ClearVisibleSelection();

    private void OnClearFilter(object sender, RoutedEventArgs e)
    {
        _menu?.ClearColumnFilter();
        Close();
    }

    private void OnApply(object sender, RoutedEventArgs e)
    {
        _menu?.ApplyValueSelection();
        Close();
    }

    private void OnMenuChanged(object? sender, EventArgs e) =>
        RefreshFromMenu();

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        DetachMenu();
        Closed?.Invoke(this, EventArgs.Empty);
    }

    private void DetachMenu()
    {
        if (_menu is null)
        {
            return;
        }
        _menu.Changed -= OnMenuChanged;
        _menu = null;
    }
}

/// <summary>
/// Wires Table-header filter-button hit testing to a NeraSpreadsheetControl.
/// </summary>
public sealed class NeraTableFilterPopupHost : IDisposable
{
    private readonly NeraSpreadsheetControl _control;
    private readonly NeraTableFilterPopup _popup;
    private bool _disposed;

    public NeraTableFilterPopupHost(NeraSpreadsheetControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _popup = new NeraTableFilterPopup(
            control.Session ?? throw new InvalidOperationException(
                "Assign a SpreadsheetSession before enabling the Table filter popup."));
        _control.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
    }

    public NeraTableFilterPopup Popup => _popup;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _control.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
        _popup.Dispose();
        _disposed = true;
    }

    private void OnPreviewMouseLeftButtonDown(
        object sender,
        MouseButtonEventArgs e)
    {
        var session = _control.Session;
        if (session is null ||
            _control.ActualWidth <= 0d ||
            _control.ActualHeight <= 0d)
        {
            return;
        }
        var point = e.GetPosition(_control);
        var chrome = SpreadsheetChromeGeometry.Calculate(
            _control.ActualWidth,
            _control.ActualHeight,
            _control.RenderTheme);
        var chromeHit = SpreadsheetChromeGeometry.HitTest(
            point.X,
            point.Y,
            _control.ActualWidth,
            _control.ActualHeight,
            _control.RenderTheme);
        if (chromeHit.Region != SpreadsheetChromeRegion.Body ||
            chrome.BodyWidth <= 0d ||
            chrome.BodyHeight <= 0d)
        {
            return;
        }

        var scroll = _control.ScrollSnapshot;
        var frame = new SpreadsheetViewportEngine(session).Compose(
            scroll.OffsetX,
            scroll.OffsetY,
            chrome.BodyWidth,
            chrome.BodyHeight,
            _control.OverscanPixels,
            _control.RenderTheme);
        if (!SpreadsheetTableFilterButtonGeometry.TryHitTest(
                WorksheetSnapshot.Capture(session.ActiveWorksheet),
                frame.Layout,
                chromeHit.BodyX,
                chromeHit.BodyY,
                _control.RenderTheme,
                out var hit))
        {
            return;
        }

        _popup.Open(
            _control,
            hit.Bounds.Translate(
                chrome.RowHeaderWidth,
                chrome.ColumnHeaderHeight),
            hit.TableId,
            hit.ColumnId);
        e.Handled = true;
    }
}
