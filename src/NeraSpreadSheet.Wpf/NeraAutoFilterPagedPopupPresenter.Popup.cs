using NeraSpreadSheet.Commands;
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
            Localization.Format("Lọc {0} trong {1}, {2}", target.ColumnName, target.OwnerName, GetHeaderStateText(target.HeaderState, target.SortDescending)));
        AutomationProperties.SetHelpText(
            root,
            Localization.Get("Alt+mũi tên xuống để mở; dùng phím mũi tên, Home, End, Page Up, Page Down, Space, Enter và Escape để thao tác."));
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

        var sortCommands = new WrapPanel
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
        };
        var sortAscending = CreateCommandButton(Localization.Get("Sắp xếp ↑"), "NeraAutoFilterSortAscending");
        var sortDescending = CreateCommandButton(Localization.Get("Sắp xếp ↓"), "NeraAutoFilterSortDescending");
        var reapply = CreateCommandButton(Localization.Get("Áp dụng lại"), "NeraAutoFilterReapply");
        var clearSort = CreateCommandButton(Localization.Get("Xóa sắp xếp"), "NeraAutoFilterClearSort");
        sortCommands.Children.Add(sortAscending);
        sortCommands.Children.Add(sortDescending);
        sortCommands.Children.Add(reapply);
        sortCommands.Children.Add(clearSort);
        DockPanel.SetDock(sortCommands, Dock.Top);
        panel.Children.Add(sortCommands);

        var menuKind = new ComboBox
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            ToolTip = Localization.Get("Chọn nhóm điều kiện lọc"),
        };
        AutomationProperties.SetAutomationId(menuKind, "NeraAutoFilterPagedMenuKind");
        AutomationProperties.SetName(menuKind, Localization.Get("Nhóm điều kiện lọc"));
        _menuKindBox = menuKind;
        DockPanel.SetDock(menuKind, Dock.Top);
        panel.Children.Add(menuKind);

        var criterionInput = new TextBox
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            ToolTip = Localization.Get("Ví dụ: North; 10; 2026-09-04; #33AA66; 3TrafficLights1:0; Top10%; Today"),
        };
        AutomationProperties.SetAutomationId(criterionInput, "NeraAutoFilterPagedCriterion");
        AutomationProperties.SetName(criterionInput, Localization.Get("Giá trị điều kiện lọc"));
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
            ItemsSource = new[] { Localization.Get("Và"), Localization.Get("Hoặc") },
            SelectedIndex = 0,
            Margin = new Thickness(0d, 0d, 6d, 0d),
            ToolTip = Localization.Get("Kết hợp hai điều kiện bằng AND hoặc OR"),
        };
        AutomationProperties.SetAutomationId(
            conditionJoin,
            "NeraAutoFilterPagedConditionJoin");
        AutomationProperties.SetName(conditionJoin, Localization.Get("Cách kết hợp điều kiện"));
        _conditionJoinBox = conditionJoin;
        var secondCriterion = new TextBox
        {
            ToolTip = Localization.Get("Điều kiện thứ hai, ví dụ LessThan:100"),
        };
        AutomationProperties.SetAutomationId(
            secondCriterion,
            "NeraAutoFilterPagedSecondCriterion");
        AutomationProperties.SetName(secondCriterion, Localization.Get("Điều kiện lọc thứ hai"));
        _secondCriterionInput = secondCriterion;
        Grid.SetColumn(secondCriterion, 1);
        customConditionPanel.Children.Add(conditionJoin);
        customConditionPanel.Children.Add(secondCriterion);
        DockPanel.SetDock(customConditionPanel, Dock.Top);
        panel.Children.Add(customConditionPanel);

        var search = new TextBox
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
            ToolTip = Localization.Get("Tìm giá trị trong nguồn lọc"),
        };
        AutomationProperties.SetAutomationId(
            search,
            "NeraAutoFilterPagedSearch");
        AutomationProperties.SetName(
            search,
            Localization.Format("Tìm giá trị trong cột {0}", target.ColumnName));
        _searchBox = search;
        DockPanel.SetDock(search, Dock.Top);
        panel.Children.Add(search);

        var selectionCommands = new WrapPanel
        {
            Margin = new Thickness(0d, 0d, 0d, 8d),
        };
        _selectionCommands = selectionCommands;
        var selectAll = CreateCommandButton(
            Localization.Get("Chọn tất cả kết quả"),
            "NeraAutoFilterPagedSelectAll");
        var selectNone = CreateCommandButton(
            Localization.Get("Bỏ chọn tất cả kết quả"),
            "NeraAutoFilterPagedSelectNone");
        selectionCommands.Children.Add(selectAll);
        selectionCommands.Children.Add(selectNone);
        DockPanel.SetDock(selectionCommands, Dock.Top);
        panel.Children.Add(selectionCommands);

        var dateBack = CreateCommandButton(
            Localization.Get("◀ Lùi một cấp ngày"),
            "NeraAutoFilterPagedDateBack");
        dateBack.HorizontalAlignment = HorizontalAlignment.Left;
        dateBack.Margin = new Thickness(0d, 0d, 0d, 8d);
        dateBack.Visibility = Visibility.Collapsed;
        _dateBackButton = dateBack;
        DockPanel.SetDock(dateBack, Dock.Top);
        panel.Children.Add(dateBack);

        var status = new TextBlock
        {

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
            Localization.Get("◀ Trang trước"),
            "NeraAutoFilterPagedPrevious");
        var next = CreateCommandButton(
            Localization.Get("Trang sau ▶"),
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
            Localization.Get("Xóa lọc"),
            "NeraAutoFilterPagedClear");
        var cancel = CreateCommandButton(
            Localization.Get("Hủy"),
            "NeraAutoFilterPagedCancel");
        var apply = CreateCommandButton(
            Localization.Get("Áp dụng"),
            "NeraAutoFilterPagedApply");
        _applyButton = apply;
        footer.Children.Add(clear);
        footer.Children.Add(cancel);
        footer.Children.Add(apply);
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(footer);

        search.TextChanged += (_, _) => ScheduleSearch(search.Text);
        sortAscending.Click += (_, _) => StartOperation(token =>
            SortAndCloseAsync(false, criterionInput.Text, token));
        sortDescending.Click += (_, _) => StartOperation(token =>
            SortAndCloseAsync(true, criterionInput.Text, token));
        reapply.Click += (_, _) => StartOperation(ReapplyAndCloseAsync);
        clearSort.Click += (_, _) => StartOperation(ClearSortAndCloseAsync);
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
            var searchFocused = _searchBox?.IsKeyboardFocusWithin == true;
            var focusedValueIndex = _valueCheckBoxes.FindIndex(
                static item => item.IsKeyboardFocusWithin);
            var valueFocused = focusedValueIndex >= 0;
            if (args.Key == Key.Escape)
            {
                Close();
                args.Handled = true;
            }
            else if (args.Key == Key.PageDown &&
                     valueFocused &&
                     _nextButton?.IsEnabled == true)
            {
                _nextButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                args.Handled = true;
            }
            else if (args.Key == Key.PageUp &&
                     valueFocused &&
                     _previousButton?.IsEnabled == true)
            {
                _previousButton.RaiseEvent(
                    new RoutedEventArgs(Button.ClickEvent));
                args.Handled = true;
            }
            else if (((valueFocused &&
                       args.Key is Key.Up or Key.Down or Key.Home or Key.End) ||
                      (searchFocused && args.Key is Key.Up or Key.Down)) &&
                     _valueCheckBoxes.Count > 0)
            {
                var nextIndex = args.Key switch
                {
                    Key.Home => 0,
                    Key.End => _valueCheckBoxes.Count - 1,
                    Key.Up when searchFocused => _valueCheckBoxes.Count - 1,
                    Key.Down when searchFocused => 0,
                    Key.Up => Math.Max(0, focusedValueIndex - 1),
                    _ => Math.Min(
                        _valueCheckBoxes.Count - 1,
                        focusedValueIndex + 1),
                };
                _valueCheckBoxes[nextIndex].Focus();
                _valueCheckBoxes[nextIndex].BringIntoView();
                args.Handled = true;
            }
            else if (args.Key == Key.Enter &&
                     valueFocused)
            {
                var focused = _valueCheckBoxes[focusedValueIndex];
                focused.IsChecked = focused.IsChecked != true;
                args.Handled = true;
            }
        };
        NeraRibbonChrome.InstallFilter(root, IconTheme);
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
                .Select(kind => Localization.Get(kind.GetDefaultDisplayName()))
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
                ? Localization.Get("Nhập một hoặc hai điều kiện rồi chọn cách kết hợp.")
                : Localization.Get("Nhập điều kiện lọc rồi chọn Áp dụng.");
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
                ToolTip = Localization.Get("Chọn hoặc bỏ chọn giá trị trên trang này"),
            };
            AutomationProperties.SetName(
                checkBox,
                Localization.Format("{0}; {1:N0} dòng", DisplayValue(item.Value), item.Count));
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
            ? Localization.Format("{0:N0}–{1:N0}/{2:N0}; nguồn bị giới hạn, không thể áp dụng chọn giá trị.", first, last, _binding.TotalItemCount)
            : Localization.Format("{0:N0}–{1:N0}/{2:N0} giá trị.", first, last, _binding.TotalItemCount);
        AutomationProperties.SetName(
            _status,
            $"{_binding.AccessibilityAnnouncement} {_status.Text}");
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
                Localization.Format("{0}; {1:N0} dòng", DisplayDateNode(node), node.Count));
            checkBox.Checked += (_, _) => _selectedDateGroups.Add(group);
            checkBox.Unchecked += (_, _) => _selectedDateGroups.Remove(group);
            row.Children.Add(checkBox);
            if (node.HasChildren)
            {
                var drill = CreateCommandButton(
                    Localization.Get("Mở ▶"),
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
        _status.Text = Localization.Format("{0:N0}–{1:N0}/{2:N0} nhóm ngày; ", first, last, total) +
            Localization.Format("đã chọn {0:N0}.", _selectedDateGroups.Count);
        _previousButton!.Visibility = Visibility.Visible;
        _nextButton!.Visibility = Visibility.Visible;
        _previousButton.IsEnabled = _datePage?.HasPreviousPage == true && !_binding.IsBusy;
        _nextButton.IsEnabled = _datePage?.HasNextPage == true && !_binding.IsBusy;
        _applyButton!.IsEnabled = !_binding.IsBusy && _selectedDateGroups.Count > 0;
    }

    private string GetHeaderStateText(
        SpreadsheetFilterHeaderState state,
        bool? descending) => state switch
        {
            SpreadsheetFilterHeaderState.Filtered => Localization.Get("đang lọc"),
            SpreadsheetFilterHeaderState.Sorted => descending == true
                ? Localization.Get("đang sắp xếp giảm dần")
                : Localization.Get("đang sắp xếp tăng dần"),
            SpreadsheetFilterHeaderState.FilteredAndSorted => descending == true
                ? Localization.Get("đang lọc và sắp xếp giảm dần")
                : Localization.Get("đang lọc và sắp xếp tăng dần"),
            _ => Localization.Get("chưa lọc hoặc sắp xếp"),
        };
}
