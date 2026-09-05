using NeraSpreadSheet.Commands;
using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Maui;

public sealed partial class NeraSpreadsheetAutoFilterHost
{
    private SheetParts CreateSheet()
    {
        var overlay = new Grid
        {
            IsVisible = false,
            BackgroundColor = Color.FromRgba(0, 0, 0, 96),
            Padding = new Thickness(12d),
            AutomationId = "NeraAutoFilterPagedOverlay",
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        var panel = new VerticalStackLayout
        {
            Spacing = 8d,
            Padding = new Thickness(14d),
            BackgroundColor = Colors.White,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.End,
            MaximumHeightRequest = 560d,
            AutomationId = "NeraAutoFilterPagedPanel",
        };
        SemanticProperties.SetHeadingLevel(
            panel,
            SemanticHeadingLevel.Level2);
        overlay.Children.Add(panel);

        var title = new Label
        {
            Text = Localization.Get("Bộ lọc bảng tính"),
            FontAttributes = FontAttributes.Bold,
            FontSize = 18d,
            AutomationId = "NeraAutoFilterPagedTitle",
        };
        SemanticProperties.SetHeadingLevel(
            title,
            SemanticHeadingLevel.Level2);
        panel.Children.Add(title);

        var sortCommands = new HorizontalStackLayout
        {
            Spacing = 6d,
        };
        var sortAscending = CreateSheetButton(
            Localization.Get("Sắp xếp ↑"), "NeraAutoFilterSortAscending", Localization.Get("Sắp xếp cột tăng dần"));
        var sortDescending = CreateSheetButton(
            Localization.Get("Sắp xếp ↓"), "NeraAutoFilterSortDescending", Localization.Get("Sắp xếp cột giảm dần"));
        var reapply = CreateSheetButton(
            Localization.Get("Áp dụng lại"), "NeraAutoFilterReapply", Localization.Get("Áp dụng lại lọc và sắp xếp hiện tại"));
        var clearSort = CreateSheetButton(
            Localization.Get("Xóa sắp xếp"), "NeraAutoFilterClearSort", Localization.Get("Xóa trạng thái sắp xếp"));
        sortCommands.Children.Add(sortAscending);
        sortCommands.Children.Add(sortDescending);
        sortCommands.Children.Add(reapply);
        sortCommands.Children.Add(clearSort);
        panel.Children.Add(sortCommands);

        var menuKindPicker = new Picker
        {
            Title = Localization.Get("Nhóm điều kiện lọc"),
            AutomationId = "NeraAutoFilterPagedMenuKind",
        };
        SemanticProperties.SetDescription(
            menuKindPicker,
            Localization.Get("Chọn lọc giá trị, văn bản, số, ngày, màu, biểu tượng hoặc điều kiện tùy chỉnh"));
        panel.Children.Add(menuKindPicker);

        var criterionInput = new Entry
        {
            Placeholder = Localization.Get("Giá trị điều kiện (Top10%, Today, #RRGGBB…)"),
            AutomationId = "NeraAutoFilterPagedCriterion",
        };
        SemanticProperties.SetDescription(criterionInput, Localization.Get("Giá trị điều kiện lọc"));
        panel.Children.Add(criterionInput);

        var customConditions = new Grid
        {
            IsVisible = false,
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(96d)),
                new ColumnDefinition(GridLength.Star),
            },
            ColumnSpacing = 8d,
        };
        var conditionJoin = new Picker
        {
            ItemsSource = new[] { Localization.Get("Và"), Localization.Get("Hoặc") },
            SelectedIndex = 0,
            AutomationId = "NeraAutoFilterPagedConditionJoin",
        };
        SemanticProperties.SetDescription(conditionJoin, Localization.Get("Cách kết hợp hai điều kiện"));
        var secondCriterion = new Entry
        {
            Placeholder = Localization.Get("Điều kiện thứ hai"),
            AutomationId = "NeraAutoFilterPagedSecondCriterion",
        };
        SemanticProperties.SetDescription(secondCriterion, Localization.Get("Điều kiện lọc thứ hai"));
        customConditions.Add(conditionJoin, 0, 0);
        customConditions.Add(secondCriterion, 1, 0);
        panel.Children.Add(customConditions);

        var search = new Entry
        {
            Placeholder = Localization.Get("Tìm giá trị"),
            AutomationId = "NeraAutoFilterPagedSearch",
            ReturnType = ReturnType.Search,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
        };
        SemanticProperties.SetDescription(search, Localization.Get("Tìm giá trị lọc"));
        SemanticProperties.SetHint(
            search,
            Localization.Get("Nhập từ khóa; danh sách tự tải lại từ trang đầu."));
        panel.Children.Add(search);

        var selectionCommands = new HorizontalStackLayout
        {
            Spacing = 8d,
        };
        var selectAll = CreateSheetButton(
            Localization.Get("Chọn kết quả"),
            "NeraAutoFilterPagedSelectAll",
            Localization.Get("Chọn mọi giá trị khớp tìm kiếm"));
        var selectNone = CreateSheetButton(
            Localization.Get("Bỏ chọn kết quả"),
            "NeraAutoFilterPagedSelectNone",
            Localization.Get("Bỏ chọn mọi giá trị khớp tìm kiếm"));
        selectionCommands.Children.Add(selectAll);
        selectionCommands.Children.Add(selectNone);
        panel.Children.Add(selectionCommands);

        var dateBack = CreateSheetButton(
            Localization.Get("◀ Lùi một cấp ngày"),
            "NeraAutoFilterPagedDateBack",
            Localization.Get("Quay về năm hoặc danh sách năm"));
        dateBack.IsVisible = false;
        dateBack.HorizontalOptions = LayoutOptions.Start;
        panel.Children.Add(dateBack);

        var status = new Label
        {
            TextColor = Colors.Gray,
            FontSize = 12d,
            AutomationId = "NeraAutoFilterPagedStatus",
        };
        panel.Children.Add(status);

        var values = new CollectionView
        {
            AutomationId = "NeraAutoFilterPagedValues",
            SelectionMode = SelectionMode.None,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
            {
                ItemSpacing = 2d,
            },
            HeightRequest = 280d,
            ItemTemplate = CreateValueTemplate(),
        };
        SemanticProperties.SetDescription(
            values,
            Localization.Get("Trang hiện hành của danh sách giá trị lọc"));
        panel.Children.Add(values);

        var dateValues = new CollectionView
        {
            AutomationId = "NeraAutoFilterPagedDateValues",
            SelectionMode = SelectionMode.None,
            ItemsLayout = new LinearItemsLayout(ItemsLayoutOrientation.Vertical)
            {
                ItemSpacing = 2d,
            },
            HeightRequest = 280d,
            ItemTemplate = CreateDateTemplate(),
            IsVisible = false,
        };
        SemanticProperties.SetDescription(
            dateValues,
            Localization.Get("Cây ngày được tải lười theo năm, tháng và ngày"));
        panel.Children.Add(dateValues);

        var paging = new HorizontalStackLayout
        {
            Spacing = 8d,
            HorizontalOptions = LayoutOptions.Center,
        };
        var previous = CreateSheetButton(
            Localization.Get("◀ Trang trước"),
            "NeraAutoFilterPagedPrevious",
            Localization.Get("Tải trang giá trị trước"));
        var next = CreateSheetButton(
            Localization.Get("Trang sau ▶"),
            "NeraAutoFilterPagedNext",
            Localization.Get("Tải trang giá trị sau"));
        paging.Children.Add(previous);
        paging.Children.Add(next);
        panel.Children.Add(paging);

        var footer = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto),
            },
            ColumnSpacing = 8d,
        };
        var clear = CreateSheetButton(
            Localization.Get("Xóa lọc"),
            "NeraAutoFilterPagedClear",
            Localization.Get("Xóa bộ lọc của cột hiện tại"));
        var cancel = CreateSheetButton(
            Localization.Get("Hủy"),
            "NeraAutoFilterPagedCancel",
            Localization.Get("Đóng mà không áp dụng thay đổi"));
        var apply = CreateSheetButton(
            Localization.Get("Áp dụng"),
            "NeraAutoFilterPagedApply",
            Localization.Get("Áp dụng lựa chọn trên toàn danh sách phân trang"));
        footer.Add(clear, 0, 0);
        footer.Add(cancel, 1, 0);
        footer.Add(apply, 2, 0);
        panel.Children.Add(footer);

        search.TextChanged += (_, args) => ScheduleSearch(args.NewTextValue);
        sortAscending.Clicked += (_, _) => StartOperation(token =>
            SortAndCloseAsync(false, criterionInput.Text, token));
        sortDescending.Clicked += (_, _) => StartOperation(token =>
            SortAndCloseAsync(true, criterionInput.Text, token));
        reapply.Clicked += (_, _) => StartOperation(ReapplyAndCloseAsync);
        clearSort.Clicked += (_, _) => StartOperation(ClearSortAndCloseAsync);
        menuKindPicker.SelectedIndexChanged += (_, _) =>
        {
            if (!_rebuilding)
            {
                StartOperation(RefreshSelectedModeAsync);
            }
        };
        search.Completed += (_, _) => StartOperation(ApplyAndCloseAsync);
        selectAll.Clicked += (_, _) => StartOperation(async token =>
        {
            var binding = _binding;
            if (binding is null)
            {
                return;
            }
            await binding.SelectAllVisibleAsync(token);
            if (ReferenceEquals(_binding, binding)) UpdateSheetState();
        });
        selectNone.Clicked += (_, _) => StartOperation(async token =>
        {
            var binding = _binding;
            if (binding is null)
            {
                return;
            }
            await binding.ClearVisibleSelectionAsync(token);
            if (ReferenceEquals(_binding, binding)) UpdateSheetState();
        });
        previous.Clicked += (_, _) => StartOperation(token =>
            MovePageAsync(next: false, token));
        next.Clicked += (_, _) => StartOperation(token =>
            MovePageAsync(next: true, token));
        dateBack.Clicked += (_, _) => StartOperation(NavigateDateBackAsync);
        clear.Clicked += (_, _) => StartOperation(ClearAndCloseAsync);
        cancel.Clicked += (_, _) => CloseFilterSheet();
        apply.Clicked += (_, _) => StartOperation(ApplyAndCloseAsync);
        overlay.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() =>
            {
                if (_sheetOverlay.IsVisible)
                {
                    CloseFilterSheet();
                }
            }),
        });
        panel.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(() => { }),
        });

        return new SheetParts(
            overlay,
            panel,
            menuKindPicker,
            criterionInput,
            secondCriterion,
            conditionJoin,
            selectionCommands,
            dateBack,
            search,
            status,
            values,
            dateValues,
            previous,
            next,
            apply);
    }

    private DataTemplate CreateValueTemplate() =>
        new(() =>
        {
            var checkBox = new CheckBox
            {
                VerticalOptions = LayoutOptions.Center,
            };
            checkBox.SetBinding(
                CheckBox.IsCheckedProperty,
                nameof(SpreadsheetTableFilterValueItem.IsSelected),
                mode: BindingMode.OneWay);
            var value = new Label
            {
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
            };
            var count = new Label
            {
                VerticalOptions = LayoutOptions.Center,
                HorizontalOptions = LayoutOptions.End,
                TextColor = Colors.Gray,
            };
            count.SetBinding(
                Label.TextProperty,
                new Binding(
                    nameof(SpreadsheetTableFilterValueItem.Count),
                    stringFormat: "{0:N0}"));
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                },
                ColumnSpacing = 8d,
                Padding = new Thickness(4d, 2d),
            };
            row.Add(checkBox, 0, 0);
            row.Add(value, 1, 0);
            row.Add(count, 2, 0);
            checkBox.CheckedChanged += OnValueCheckChanged;
            NeraMauiRibbonChrome.ConfigureFilter(row, NeraMauiRibbonPalette.For(IconTheme));
            _presentationRows.RemoveAll(static reference => !reference.TryGetTarget(out _));
            _presentationRows.Add(new WeakReference<Grid>(row));
            row.BindingContextChanged += (_, _) => UpdatePresentationRow(row);
            return row;
        });

    private DataTemplate CreateDateTemplate() =>
        new(() =>
        {
            var checkBox = new CheckBox
            {
                VerticalOptions = LayoutOptions.Center,
            };
            checkBox.SetBinding(
                CheckBox.IsCheckedProperty,
                nameof(DateItem.IsSelected),
                mode: BindingMode.OneWay);
            var value = new Label
            {
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
            };
            var drill = CreateSheetButton(
                Localization.Get("Mở ▶"),
                "NeraAutoFilterPagedDateDrill",
                Localization.Get("Tải lười cấp ngày con"));
            drill.SetBinding(
                VisualElement.IsVisibleProperty,
                nameof(DateItem.HasChildren));
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                },
                ColumnSpacing = 8d,
                Padding = new Thickness(4d, 2d),
            };
            row.Add(checkBox, 0, 0);
            row.Add(value, 1, 0);
            row.Add(drill, 2, 0);
            checkBox.CheckedChanged += OnDateCheckChanged;
            drill.Clicked += OnDateDrillClicked;
            NeraMauiRibbonChrome.ConfigureFilter(row, NeraMauiRibbonPalette.For(IconTheme));
            _presentationRows.RemoveAll(static reference => !reference.TryGetTarget(out _));
            _presentationRows.Add(new WeakReference<Grid>(row));
            row.BindingContextChanged += (_, _) => UpdatePresentationRow(row);
            return row;
        });

    private void OnValueCheckChanged(
        object? sender,
        CheckedChangedEventArgs e)
    {
        if (sender is not CheckBox
            {
                BindingContext: SpreadsheetTableFilterValueItem item,
            } ||
            item.IsSelected == e.Value ||
            _binding is null)
        {
            return;
        }
        var index = _binding.Items.IndexOf(item);
        if (index >= 0)
        {
            StartOperation(token => _binding.SetSelectedAsync(
                index,
                e.Value,
                token));
        }
    }

    private void OnDateCheckChanged(
        object? sender,
        CheckedChangedEventArgs e)
    {
        if (_rebuilding || sender is not CheckBox { BindingContext: DateItem item })
        {
            return;
        }
        var group = ToDateGroup(item.Node);
        if (e.Value)
        {
            _selectedDateGroups.Add(group);
        }
        else
        {
            _selectedDateGroups.Remove(group);
        }
        _status.Text = Localization.Format("Đã chọn {0:N0} nhóm ngày.", _selectedDateGroups.Count);
        _applyButton.IsEnabled = _selectedDateGroups.Count > 0;
    }

    private void OnDateDrillClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: DateItem { HasChildren: true } item })
        {
            StartOperation(token => NavigateDateIntoAsync(item.Node, token));
        }
    }

    private void UpdateSheetState()
    {
        if (_binding is null)
        {
            _status.Text = string.Empty;
            _previousButton.IsEnabled = false;
            _nextButton.IsEnabled = false;
            _applyButton.IsEnabled = false;
            return;
        }
        _keyboardActiveIndex = _binding.Items.Count == 0
            ? 0
            : Math.Clamp(_keyboardActiveIndex, 0, _binding.Items.Count - 1);

        _rebuilding = true;
        var selectedIndex = _menuKindPicker.SelectedIndex;
        _menuKindPicker.ItemsSource = _binding.MenuKinds
            .Select(kind => Localization.Get(kind.GetDefaultDisplayName()))
            .ToArray();
        _menuKindPicker.SelectedIndex = _binding.MenuKinds.Count == 0
            ? -1
            : Math.Clamp(selectedIndex, 0, _binding.MenuKinds.Count - 1);
        _rebuilding = false;
        var kind = GetSelectedMenuKind(_binding);
        var isValues = kind == SpreadsheetAutoFilterMenuKind.Values;
        var isDate = kind == SpreadsheetAutoFilterMenuKind.Date;
        var isCustom = kind == SpreadsheetAutoFilterMenuKind.Custom;
        _criterionInput.IsVisible = !isValues && !isDate;
        if (_secondCriterionInput.Parent is VisualElement customPanel)
        {
            customPanel.IsVisible = isCustom;
        }
        _search.IsVisible = isValues;
        _selectionCommands.IsVisible = isValues;
        _values.IsVisible = isValues;
        _dateValues.IsVisible = isDate;
        _dateBackButton.IsVisible = isDate && _dateParent.Year is not null;
        _dateValues.ItemsSource = isDate
            ? _datePage?.Nodes.Select(node => new DateItem(
                node,
                DisplayDateNode(node),
                _selectedDateGroups.Contains(ToDateGroup(node)))).ToArray()
            : null;

        UpdateStatusText();
        _previousButton.IsEnabled = isDate
            ? _datePage?.HasPreviousPage == true && !_binding.IsBusy
            : _binding.HasPreviousPage && !_binding.IsBusy;
        _nextButton.IsEnabled = isDate
            ? _datePage?.HasNextPage == true && !_binding.IsBusy
            : _binding.HasNextPage && !_binding.IsBusy;
        _applyButton.IsEnabled =
            !_binding.IsBusy &&
            (!isDate || _selectedDateGroups.Count > 0) &&
            (!isValues || !_binding.IsSourceTruncated);
    }

    private void UpdateStatusText()
    {
        if (_binding is null) return;
        var kind = GetSelectedMenuKind(_binding);
        var isValues = kind == SpreadsheetAutoFilterMenuKind.Values;
        var isDate = kind == SpreadsheetAutoFilterMenuKind.Date;
        var isCustom = kind == SpreadsheetAutoFilterMenuKind.Custom;
        var total = isDate ? _datePage?.TotalNodeCount ?? 0 : _binding.TotalItemCount;
        var offset = isDate ? _datePage?.Offset ?? 0 : _binding.PageOffset;
        var count = isDate ? _datePage?.Nodes.Count ?? 0 : _binding.Items.Count;
        var first = total == 0 ? 0 : offset + 1;
        var last = Math.Min(total, offset + count);
        _status.Text = !isValues && !isDate
            ? isCustom
                ? Localization.Get("Nhập một hoặc hai điều kiện rồi chọn cách kết hợp.")
                : Localization.Get("Nhập điều kiện lọc rồi chọn Áp dụng.")
            : isDate
            ? Localization.Format("{0:N0}–{1:N0}/{2:N0} nhóm ngày; đã chọn {3:N0}.", first, last, total, _selectedDateGroups.Count)
            : _binding.IsSourceTruncated
                ? Localization.Format("{0:N0}–{1:N0}/{2:N0}; nguồn bị giới hạn, không thể áp dụng chọn giá trị.", first, last, total)
                : Localization.Format("{0:N0}–{1:N0}/{2:N0} giá trị.", first, last, total);
        SemanticProperties.SetDescription(
            _status,
            $"{_binding.AccessibilityAnnouncement} {_status.Text}");

    }

    /// <summary>Updates labels and palette in place on the UI thread, retaining query, selection, focus and caret.</summary>
    public void SetPresentation(PresentationLocalization localization, NeraSpreadSheet.Iconography.NeraIconTheme iconTheme)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _shellResources.Apply(localization);
        Localization = localization;
        IconTheme = iconTheme;
        NeraMauiRibbonChrome.ConfigureFilter(_sheetPanel, NeraMauiRibbonPalette.For(iconTheme));
        if (_binding is not null) _binding.Localization = localization;
        _rebuilding = true;
        try
        {
            var join = _conditionJoinPicker.SelectedIndex;
            var joinLabels = new[] { Localization.Get("Và"), Localization.Get("Hoặc") };
            if (!_conditionJoinPicker.Items.SequenceEqual(joinLabels))
            {
                _conditionJoinPicker.ItemsSource = joinLabels;
                _conditionJoinPicker.SelectedIndex = join;
            }
            var selected = _menuKindPicker.SelectedIndex;
            var menuLabels = _binding?.MenuKinds.Select(kind => Localization.Get(kind.GetDefaultDisplayName())).ToArray() ?? [];
            if (!_menuKindPicker.Items.SequenceEqual(menuLabels))
            {
                _menuKindPicker.ItemsSource = menuLabels;
                _menuKindPicker.SelectedIndex = selected;
            }
            UpdateStatusText();
            _presentationRows.RemoveAll(static reference => !reference.TryGetTarget(out _));
            foreach (var reference in _presentationRows)
                if (reference.TryGetTarget(out var row)) UpdatePresentationRow(row);
        }
        finally { _rebuilding = false; }
    }

    private void UpdatePresentationRow(Grid row)
    {
        NeraMauiRibbonChrome.ConfigureFilter(row, NeraMauiRibbonPalette.For(IconTheme));
        var text = row.BindingContext switch
        {
            SpreadsheetTableFilterValueItem item => item.Value.IsBlank ? Localization.Get("(Trống)") : item.DisplayText,
            DateItem item => DisplayDateNode(item.Node),
            _ => null,
        };
        if (text is null) return;
        if (row.Children[0] is CheckBox checkBox) SemanticProperties.SetDescription(checkBox, text);
        if (row.Children[1] is Label label) label.Text = text;
        if (row.Children[2] is Button drill)
        {
            drill.Text = Localization.Get("Mở ▶");
            SemanticProperties.SetDescription(drill, Localization.Format("Mở {0}", text));
            SemanticProperties.SetHint(drill, Localization.Get("Tải lười cấp ngày con"));
        }
    }

    private static Button CreateSheetButton(
        string text,
        string automationId,
        string hint)
    {
        var button = new Button
        {
            Text = text,
            AutomationId = automationId,
            Padding = new Thickness(10d, 6d),
            CornerRadius = 4,
            MinimumHeightRequest = 38d,
        };
        SemanticProperties.SetDescription(button, text);
        SemanticProperties.SetHint(button, hint);
        return button;
    }

    private sealed record SheetParts(
        Grid Overlay,
        VerticalStackLayout Panel,
        Picker MenuKindPicker,
        Entry CriterionInput,
        Entry SecondCriterionInput,
        Picker ConditionJoinPicker,
        HorizontalStackLayout SelectionCommands,
        Button DateBack,
        Entry Search,
        Label Status,
        CollectionView Values,
        CollectionView DateValues,
        Button Previous,
        Button Next,
        Button Apply);

    private sealed record DateItem(
        SpreadsheetAutoFilterDateNode Node,
        string DisplayText,
        bool IsSelected)
    {
        public bool HasChildren => Node.HasChildren;
    }
}
