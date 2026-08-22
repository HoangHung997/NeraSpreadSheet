using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Wpf;

public sealed partial class NeraAutoFilterPagedPopupPresenter
{
    private Border BuildPopupContent(
        SpreadsheetAutoFilterTarget target)
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
        AutomationProperties.SetAutomationId(
            root,
            "NeraAutoFilterPagedPopup");
        AutomationProperties.SetName(
            root,
            $"Lọc {target.ColumnName} trong {target.OwnerName}");
        KeyboardNavigation.SetTabNavigation(
            root,
            KeyboardNavigationMode.Cycle);

        var panel = new DockPanel
        {
            LastChildFill = false,
        };
        root.Child = panel;
        var title = new TextBlock
        {
            Text = $"{target.OwnerName} — {target.ColumnName}",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0d, 0d, 0d, 8d),
        };
        DockPanel.SetDock(title, Dock.Top);
        panel.Children.Add(title);

        var search = new TextBox
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            ToolTip = "Tìm giá trị trong nguồn lọc",
        };
        AutomationProperties.SetAutomationId(
            search,
            "NeraAutoFilterPagedSearch");
        AutomationProperties.SetName(
            search,
            $"Tìm giá trị trong cột {target.ColumnName}");
        _searchBox = search;
        DockPanel.SetDock(search, Dock.Top);
        panel.Children.Add(search);

        var selectionCommands = new WrapPanel
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
        };
        var selectAll = CreateCommandButton(
            "Chọn tất cả kết quả",
            "NeraAutoFilterPagedSelectAll");
        var selectNone = CreateCommandButton(
            "Bỏ chọn tất cả kết quả",
            "NeraAutoFilterPagedSelectNone");
        selectionCommands.Children.Add(selectAll);
        selectionCommands.Children.Add(selectNone);
        DockPanel.SetDock(selectionCommands, Dock.Top);
        panel.Children.Add(selectionCommands);

        var status = new TextBlock
        {
            Foreground = Brushes.DimGray,
            FontSize = 11d,
            Margin = new Thickness(0d, 0d, 0d, 6d),
        };
        AutomationProperties.SetAutomationId(
            status,
            "NeraAutoFilterPagedStatus");
        _status = status;
        DockPanel.SetDock(status, Dock.Top);
        panel.Children.Add(status);

        var itemsPanel = new StackPanel();
        _itemsPanel = itemsPanel;
        var scroller = new ScrollViewer
        {
            Content = itemsPanel,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 270d,
            CanContentScroll = true,
        };
        AutomationProperties.SetAutomationId(
            scroller,
            "NeraAutoFilterPagedValues");
        DockPanel.SetDock(scroller, Dock.Top);
        panel.Children.Add(scroller);

        var paging = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0d, 8d, 0d, 0d),
        };
        var previous = CreateCommandButton(
            "◀ Trang trước",
            "NeraAutoFilterPagedPrevious");
        var next = CreateCommandButton(
            "Trang sau ▶",
            "NeraAutoFilterPagedNext");
        _previousButton = previous;
        _nextButton = next;
        paging.Children.Add(previous);
        paging.Children.Add(next);
        DockPanel.SetDock(paging, Dock.Bottom);
        panel.Children.Add(paging);

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0d, 8d, 0d, 0d),
        };
        var clear = CreateCommandButton(
            "Xóa lọc",
            "NeraAutoFilterPagedClear");
        var cancel = CreateCommandButton(
            "Hủy",
            "NeraAutoFilterPagedCancel");
        var apply = CreateCommandButton(
            "Áp dụng",
            "NeraAutoFilterPagedApply");
        _applyButton = apply;
        footer.Children.Add(clear);
        footer.Children.Add(cancel);
        footer.Children.Add(apply);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(footer);

        search.TextChanged += (_, _) => ScheduleSearch(search.Text);
        search.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                Close();
                args.Handled = true;
            }
            else if (args.Key == Key.Enter)
            {
                StartOperation(ApplyAndCloseAsync);
                args.Handled = true;
            }
        };
        selectAll.Click += (_, _) => StartOperation(async token =>
        {
            if (_binding is not null)
            {
                await _binding.SelectAllVisibleAsync(token);
                RebuildPage();
            }
        });
        selectNone.Click += (_, _) => StartOperation(async token =>
        {
            if (_binding is not null)
            {
                await _binding.ClearVisibleSelectionAsync(token);
                RebuildPage();
            }
        });
        previous.Click += (_, _) => StartOperation(async token =>
        {
            if (_binding is not null &&
                await _binding.MovePreviousPageAsync(token))
            {
                RebuildPage();
            }
        });
        next.Click += (_, _) => StartOperation(async token =>
        {
            if (_binding is not null &&
                await _binding.MoveNextPageAsync(token))
            {
                RebuildPage();
            }
        });
        clear.Click += (_, _) => StartOperation(ClearAndCloseAsync);
        cancel.Click += (_, _) => Close();
        apply.Click += (_, _) => StartOperation(ApplyAndCloseAsync);
        root.PreviewKeyDown += (_, args) =>
        {
            if (args.Key == Key.Escape)
            {
                Close();
                args.Handled = true;
            }
            else if (args.Key == Key.PageDown &&
                     _nextButton?.IsEnabled == true)
            {
                _nextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                args.Handled = true;
            }
            else if (args.Key == Key.PageUp &&
                     _previousButton?.IsEnabled == true)
            {
                _previousButton.RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent));
                args.Handled = true;
            }
        };
        return root;
    }

    private void OnPopupOpened(object? sender, EventArgs e)
    {
        if (sender is not Popup popup ||
            !ReferenceEquals(_popup, popup))
        {
            return;
        }
        StartOperation(async token =>
        {
            if (_binding is null)
            {
                return;
            }
            await _binding.InitializeAsync(token);
            RebuildPage();
            FocusSearchBox(popup);
        });
    }

    private void OnPopupClosed(object? sender, EventArgs e)
    {
        if (sender is not Popup popup)
        {
            return;
        }
        popup.Opened -= OnPopupOpened;
        popup.Closed -= OnPopupClosed;
        CancelOperations();
        DisposeBinding();
        if (ReferenceEquals(_popup, popup))
        {
            _popup = null;
            _searchBox = null;
            _status = null;
            _itemsPanel = null;
            _previousButton = null;
            _nextButton = null;
            _applyButton = null;
            _valueCheckBoxes.Clear();
        }
        RestoreFocus(_focusBeforeOpen);
        _focusBeforeOpen = null;
    }

    private void RebuildPage()
    {
        if (_binding is null ||
            _itemsPanel is null ||
            _status is null)
        {
            return;
        }

        _valueCheckBoxes.Clear();
        _itemsPanel.Children.Clear();
        for (var index = 0; index < _binding.Items.Count; index++)
        {
            var pageIndex = index;
            var item = _binding.Items[index];
            var checkBox = new CheckBox
            {
                IsChecked = item.IsSelected,
                Content = $"{DisplayValue(item.Value)}  ({item.Count:N0})",
                Margin = new Thickness(2d),
                ToolTip = "Chọn hoặc bỏ chọn giá trị trên trang này",
            };
            AutomationProperties.SetName(
                checkBox,
                $"{DisplayValue(item.Value)}; {item.Count:N0} dòng");
            checkBox.Checked += (_, _) =>
                StartSelectionChange(pageIndex, selected: true);
            checkBox.Unchecked += (_, _) =>
                StartSelectionChange(pageIndex, selected: false);
            _valueCheckBoxes.Add(checkBox);
            _itemsPanel.Children.Add(checkBox);
        }

        var first = _binding.TotalItemCount == 0
            ? 0
            : _binding.PageOffset + 1;
        var last = Math.Min(
            _binding.TotalItemCount,
            _binding.PageOffset + _binding.Items.Count);
        _status.Text = _binding.IsSourceTruncated
            ? $"{first:N0}–{last:N0}/{_binding.TotalItemCount:N0}; nguồn đã bị giới hạn."
            : $"{first:N0}–{last:N0}/{_binding.TotalItemCount:N0} giá trị.";
        _previousButton!.IsEnabled =
            _binding.HasPreviousPage && !_binding.IsBusy;
        _nextButton!.IsEnabled =
            _binding.HasNextPage && !_binding.IsBusy;
        _applyButton!.IsEnabled = !_binding.IsBusy;
    }
}
