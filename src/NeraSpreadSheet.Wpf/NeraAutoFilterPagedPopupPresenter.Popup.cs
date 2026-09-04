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

        var menuKind = new ComboBox
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            ToolTip = "Chọn nhóm điều kiện lọc",
        };
        AutomationProperties.SetAutomationId(menuKind, "NeraAutoFilterPagedMenuKind");
        AutomationProperties.SetName(menuKind, "Nhóm điều kiện lọc");
        _menuKindBox = menuKind;
        DockPanel.SetDock(menuKind, Dock.Top);
        panel.Children.Add(menuKind);

        var criterionInput = new TextBox
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            ToolTip = "Ví dụ: North; 10; 2026-09-04; #33AA66; 3TrafficLights1:0; Top10%; Today",
        };
        AutomationProperties.SetAutomationId(criterionInput, "NeraAutoFilterPagedCriterion");
        AutomationProperties.SetName(criterionInput, "Giá trị điều kiện lọc");
        _criterionInput = criterionInput;
        DockPanel.SetDock(criterionInput, Dock.Top);
        panel.Children.Add(criterionInput);

        var customConditionPanel = new Grid
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            Visibility = Visibility.Collapsed,
        };
        _customConditionPanel = customConditionPanel;
        customConditionPanel.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(84d) });
        customConditionPanel.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
        var conditionJoin = new ComboBox
        {
            ItemsSource = new[] { "Và", "Hoặc" },
            SelectedIndex = 0,
            Margin = new Thickness(0d, 0d, 6d, 0d),
            ToolTip = "Kết hợp hai điều kiện bằng AND hoặc OR",
        };
        AutomationProperties.SetAutomationId(
            conditionJoin,
            "NeraAutoFilterPagedConditionJoin");
        AutomationProperties.SetName(conditionJoin, "Cách kết hợp điều kiện");
        _conditionJoinBox = conditionJoin;
        var secondCriterion = new TextBox
        {
            ToolTip = "Điều kiện thứ hai, ví dụ LessThan:100",
        };
        AutomationProperties.SetAutomationId(
            secondCriterion,
            "NeraAutoFilterPagedSecondCriterion");
        AutomationProperties.SetName(secondCriterion, "Điều kiện lọc thứ hai");
        _secondCriterionInput = secondCriterion;
        Grid.SetColumn(secondCriterion, 1);
        customConditionPanel.Children.Add(conditionJoin);
        customConditionPanel.Children.Add(secondCriterion);
        DockPanel.SetDock(customConditionPanel, Dock.Top);
        panel.Children.Add(customConditionPanel);

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
        _selectionCommands = selectionCommands;
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

        var dateBack = CreateCommandButton(
            "◀ Lùi một cấp ngày",
            "NeraAutoFilterPagedDateBack");
        dateBack.HorizontalAlignment = HorizontalAlignment.Left;
        dateBack.Margin = new Thickness(0d, 0d, 0d, 8d);
        dateBack.Visibility = Visibility.Collapsed;
        _dateBackButton = dateBack;
        DockPanel.SetDock(dateBack, Dock.Top);
        panel.Children.Add(dateBack);

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
        _itemsScroller = scroller;
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
        menuKind.SelectionChanged += (_, _) =>
        {
            if (!_rebuilding)
            {
                StartOperation(RefreshSelectedModeAsync);
            }
        };
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
            var binding = _binding;
            if (binding is not null)
            {
                await binding.SelectAllVisibleAsync(token);
                if (ReferenceEquals(_binding, binding)) RebuildPage();
            }
        });
        selectNone.Click += (_, _) => StartOperation(async token =>
        {
            var binding = _binding;
            if (binding is not null)
            {
                await binding.ClearVisibleSelectionAsync(token);
                if (ReferenceEquals(_binding, binding)) RebuildPage();
            }
        });
        previous.Click += (_, _) => StartOperation(token =>
            MovePageAsync(next: false, token));
        next.Click += (_, _) => StartOperation(token =>
            MovePageAsync(next: true, token));
        dateBack.Click += (_, _) => StartOperation(NavigateDateBackAsync);
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
            var binding = _binding;
            if (binding is null)
            {
                return;
            }
            await binding.InitializeAsync(token);
            if (!ReferenceEquals(_binding, binding)) return;
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
            _menuKindBox = null;
            _criterionInput = null;
            _secondCriterionInput = null;
            _conditionJoinBox = null;
            _customConditionPanel = null;
            _selectionCommands = null;
            _dateBackButton = null;
            _status = null;
            _itemsPanel = null;
            _itemsScroller = null;
            _previousButton = null;
            _nextButton = null;
            _applyButton = null;
            _valueCheckBoxes.Clear();
            _datePage = null;
            _selectedDateGroups.Clear();
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
        _rebuilding = true;
        if (_menuKindBox is not null)
        {
            var selectedIndex = _menuKindBox.SelectedIndex;
            _menuKindBox.ItemsSource = _binding.MenuKinds
                .Select(static kind => kind.GetDefaultDisplayName())
                .ToArray();
            _menuKindBox.SelectedIndex = _binding.MenuKinds.Count == 0
                ? -1
                : Math.Clamp(selectedIndex, 0, _binding.MenuKinds.Count - 1);
        }
        _rebuilding = false;
        var kind = GetSelectedMenuKind(_binding);
        var isValues = kind == SpreadsheetAutoFilterMenuKind.Values;
        var isDate = kind == SpreadsheetAutoFilterMenuKind.Date;
        var isCustom = kind == SpreadsheetAutoFilterMenuKind.Custom;
        if (_criterionInput is not null)
        {
            _criterionInput.Visibility = isValues || isDate
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        if (_customConditionPanel is not null)
        {
            _customConditionPanel.Visibility = isCustom
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        if (_searchBox is not null)
        {
            _searchBox.Visibility = isValues
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        if (_selectionCommands is not null)
        {
            _selectionCommands.Visibility = isValues
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        if (_dateBackButton is not null)
        {
            _dateBackButton.Visibility = isDate && _dateParent.Year is not null
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        if (_itemsScroller is not null)
        {
            _itemsScroller.Visibility = isValues || isDate
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        _itemsPanel.Children.Clear();
        if (isDate)
        {
            RebuildDatePage();
            return;
        }
        if (!isValues)
        {
            _status.Text = isCustom
                ? "Nhập một hoặc hai điều kiện rồi chọn cách kết hợp."
                : "Nhập điều kiện lọc rồi chọn Áp dụng.";
            _previousButton!.Visibility = Visibility.Collapsed;
            _nextButton!.Visibility = Visibility.Collapsed;
            _applyButton!.IsEnabled = !_binding.IsBusy;
            return;
        }
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
            ? $"{first:N0}–{last:N0}/{_binding.TotalItemCount:N0}; nguồn bị giới hạn, không thể áp dụng chọn giá trị."
            : $"{first:N0}–{last:N0}/{_binding.TotalItemCount:N0} giá trị.";
        _previousButton!.Visibility = isValues
            ? Visibility.Visible
            : Visibility.Collapsed;
        _nextButton!.Visibility = isValues
            ? Visibility.Visible
            : Visibility.Collapsed;
        _previousButton.IsEnabled = _binding.HasPreviousPage && !_binding.IsBusy;
        _nextButton.IsEnabled = _binding.HasNextPage && !_binding.IsBusy;
        _applyButton!.IsEnabled =
            !_binding.IsBusy && (!isValues || !_binding.IsSourceTruncated);
    }

    private void RebuildDatePage()
    {
        if (_itemsPanel is null || _status is null || _binding is null)
        {
            return;
        }
        foreach (var node in _datePage?.Nodes ?? [])
        {
            var group = ToDateGroup(node);
            var row = new Grid { Margin = new Thickness(2d) };
            row.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1d, GridUnitType.Star) });
            row.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });
            var checkBox = new CheckBox
            {
                Content = $"{DisplayDateNode(node)}  ({node.Count:N0})",
                IsChecked = _selectedDateGroups.Contains(group),
                VerticalAlignment = VerticalAlignment.Center,
            };
            AutomationProperties.SetName(
                checkBox,
                $"{DisplayDateNode(node)}; {node.Count:N0} dòng");
            checkBox.Checked += (_, _) => _selectedDateGroups.Add(group);
            checkBox.Unchecked += (_, _) => _selectedDateGroups.Remove(group);
            row.Children.Add(checkBox);
            if (node.HasChildren)
            {
                var drill = CreateCommandButton(
                    "Mở ▶",
                    $"NeraAutoFilterDateDrill{node.Year}{node.Month}");
                drill.MinWidth = 58d;
                drill.Click += (_, _) => StartOperation(token =>
                    NavigateDateIntoAsync(node, token));
                Grid.SetColumn(drill, 1);
                row.Children.Add(drill);
            }
            _itemsPanel.Children.Add(row);
        }

        var first = _datePage is null || _datePage.TotalNodeCount == 0
            ? 0
            : _datePage.Offset + 1;
        var count = _datePage?.Nodes.Count ?? 0;
        var last = Math.Min(_datePage?.TotalNodeCount ?? 0, first + count - 1);
        var total = _datePage?.TotalNodeCount ?? 0;
        _status.Text = $"{first:N0}–{last:N0}/{total:N0} nhóm ngày; " +
            $"đã chọn {_selectedDateGroups.Count:N0}.";
        _previousButton!.Visibility = Visibility.Visible;
        _nextButton!.Visibility = Visibility.Visible;
        _previousButton.IsEnabled = _datePage?.HasPreviousPage == true && !_binding.IsBusy;
        _nextButton.IsEnabled = _datePage?.HasNextPage == true && !_binding.IsBusy;
        _applyButton!.IsEnabled = !_binding.IsBusy && _selectedDateGroups.Count > 0;
    }
}
