using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Responsive MAUI Table-filter sheet. The host decides whether to place this view
/// as a popup, side sheet or bottom sheet; filtering semantics remain shared.
/// </summary>
public sealed class NeraTableFilterSheet : ContentView, IDisposable
{
    private readonly SpreadsheetTablePresenterController _presenter;
    private readonly Entry _searchBox;
    private readonly Label _summary;
    private readonly VerticalStackLayout _valuesPanel;
    private SpreadsheetTableFilterMenu? _menu;
    private bool _refreshing;
    private bool _disposed;

    public NeraTableFilterSheet(SpreadsheetSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _presenter = new SpreadsheetTablePresenterController(session);
        _searchBox = new Entry
        {
            Placeholder = "Tìm giá trị",
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
        };
        _searchBox.TextChanged += OnSearchChanged;
        _summary = new Label
        {
            FontSize = 12d,
            TextColor = Colors.Gray,
        };
        _valuesPanel = new VerticalStackLayout
        {
            Spacing = 4d,
        };
        var scroll = new ScrollView
        {
            Content = _valuesPanel,
            MaximumHeightRequest = 360d,
        };
        var selectAll = CreateButton("Chọn tất cả", (_, _) =>
            _menu?.SelectAllVisible());
        var clearVisible = CreateButton("Bỏ chọn", (_, _) =>
            _menu?.ClearVisibleSelection());
        var clearFilter = CreateButton("Xóa lọc", OnClearFilter);
        var apply = CreateButton("Áp dụng", OnApply);
        var close = CreateButton("Đóng", (_, _) => Close());
        var buttons = new HorizontalStackLayout
        {
            Spacing = 6d,
            Children =
            {
                selectAll,
                clearVisible,
                clearFilter,
                apply,
                close,
            },
        };
        Content = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Colors.Gray,
            StrokeThickness = 1d,
            Padding = new Thickness(12d),
            Content = new VerticalStackLayout
            {
                Spacing = 8d,
                Children =
                {
                    _searchBox,
                    _summary,
                    scroll,
                    buttons,
                },
            },
        };
        MinimumWidthRequest = 300d;
        MaximumWidthRequest = 520d;
        MaximumHeightRequest = 600d;
        IsVisible = false;
        SemanticProperties.SetDescription(
            this,
            "Bộ lọc cột của bảng tính NeraSpreadSheet");
    }

    public bool IsOpen => IsVisible && _menu is not null;

    public event EventHandler? Closed;

    public void Open(Guid tableId, Guid columnId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Close();
        _menu = _presenter.OpenFilterMenu(tableId, columnId);
        _menu.Changed += OnMenuChanged;
        _searchBox.Text = string.Empty;
        RefreshFromMenu();
        IsVisible = true;
        _searchBox.Focus();
    }

    public void Close()
    {
        var wasOpen = IsOpen;
        IsVisible = false;
        DetachMenu();
        if (wasOpen)
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        Close();
        _searchBox.TextChanged -= OnSearchChanged;
        _disposed = true;
    }

    private static Button CreateButton(
        string text,
        EventHandler handler)
    {
        var button = new Button
        {
            Text = text,
            Padding = new Thickness(8d, 4d),
            MinimumHeightRequest = 36d,
        };
        button.Clicked += handler;
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
                    IsChecked = item.IsSelected,
                };
                var value = item.Value;
                checkBox.CheckedChanged += (_, args) =>
                {
                    if (!_refreshing)
                    {
                        _menu?.SetSelected(value, args.Value);
                    }
                };
                var row = new HorizontalStackLayout
                {
                    Spacing = 8d,
                    Children =
                    {
                        checkBox,
                        new Label
                        {
                            Text = $"{item.DisplayText} ({item.Count})",
                            VerticalTextAlignment = TextAlignment.Center,
                        },
                    },
                };
                SemanticProperties.SetDescription(
                    row,
                    $"{item.DisplayText}, {item.Count} lần");
                _valuesPanel.Children.Add(row);
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void OnSearchChanged(
        object? sender,
        TextChangedEventArgs e)
    {
        if (!_refreshing)
        {
            _menu?.SetSearchText(e.NewTextValue);
        }
    }

    private void OnClearFilter(object? sender, EventArgs e)
    {
        _menu?.ClearColumnFilter();
        Close();
    }

    private void OnApply(object? sender, EventArgs e)
    {
        _menu?.ApplyValueSelection();
        Close();
    }

    private void OnMenuChanged(object? sender, EventArgs e) =>
        RefreshFromMenu();

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
