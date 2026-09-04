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
            Text = "Bộ lọc bảng tính",
            FontAttributes = FontAttributes.Bold,
            FontSize = 18d,
            AutomationId = "NeraAutoFilterPagedTitle",
        };
        SemanticProperties.SetHeadingLevel(
            title,
            SemanticHeadingLevel.Level2);
        panel.Children.Add(title);

        var menuKindPicker = new Picker
        {
            Title = "Nhóm điều kiện lọc",
            AutomationId = "NeraAutoFilterPagedMenuKind",
        };
        SemanticProperties.SetDescription(
            menuKindPicker,
            "Chọn lọc giá trị, văn bản, số, ngày, màu, biểu tượng hoặc điều kiện tùy chỉnh");
        panel.Children.Add(menuKindPicker);

        var criterionInput = new Entry
        {
            Placeholder = "Giá trị điều kiện (Top10%, Today, #RRGGBB…)",
            AutomationId = "NeraAutoFilterPagedCriterion",
        };
        SemanticProperties.SetDescription(criterionInput, "Giá trị điều kiện lọc");
        panel.Children.Add(criterionInput);

        var search = new Entry
        {
            Placeholder = "Tìm giá trị",
            AutomationId = "NeraAutoFilterPagedSearch",
            ReturnType = ReturnType.Search,
            ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
        };
        SemanticProperties.SetDescription(search, "Tìm giá trị lọc");
        SemanticProperties.SetHint(
            search,
            "Nhập từ khóa; danh sách tự tải lại từ trang đầu.");
        panel.Children.Add(search);

        var selectionCommands = new HorizontalStackLayout
        {
            Spacing = 8d,
        };
        var selectAll = CreateSheetButton(
            "Chọn kết quả",
            "NeraAutoFilterPagedSelectAll",
            "Chọn mọi giá trị khớp tìm kiếm");
        var selectNone = CreateSheetButton(
            "Bỏ chọn kết quả",
            "NeraAutoFilterPagedSelectNone",
            "Bỏ chọn mọi giá trị khớp tìm kiếm");
        selectionCommands.Children.Add(selectAll);
        selectionCommands.Children.Add(selectNone);
        panel.Children.Add(selectionCommands);

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
            "Trang hiện hành của danh sách giá trị lọc");
        panel.Children.Add(values);

        var paging = new HorizontalStackLayout
        {
            Spacing = 8d,
            HorizontalOptions = LayoutOptions.Center,
        };
        var previous = CreateSheetButton(
            "◀ Trang trước",
            "NeraAutoFilterPagedPrevious",
            "Tải trang giá trị trước");
        var next = CreateSheetButton(
            "Trang sau ▶",
            "NeraAutoFilterPagedNext",
            "Tải trang giá trị sau");
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
            "Xóa lọc",
            "NeraAutoFilterPagedClear",
            "Xóa bộ lọc của cột hiện tại");
        var cancel = CreateSheetButton(
            "Hủy",
            "NeraAutoFilterPagedCancel",
            "Đóng mà không áp dụng thay đổi");
        var apply = CreateSheetButton(
            "Áp dụng",
            "NeraAutoFilterPagedApply",
            "Áp dụng lựa chọn trên toàn danh sách phân trang");
        footer.Add(clear, 0, 0);
        footer.Add(cancel, 1, 0);
        footer.Add(apply, 2, 0);
        panel.Children.Add(footer);

        search.TextChanged += (_, args) => ScheduleSearch(args.NewTextValue);
        search.Completed += (_, _) => StartOperation(ApplyAndCloseAsync);
        selectAll.Clicked += (_, _) => StartOperation(async token =>
        {
            if (_binding is null)
            {
                return;
            }
            await _binding.SelectAllVisibleAsync(token);
            UpdateSheetState();
        });
        selectNone.Clicked += (_, _) => StartOperation(async token =>
        {
            if (_binding is null)
            {
                return;
            }
            await _binding.ClearVisibleSelectionAsync(token);
            UpdateSheetState();
        });
        previous.Clicked += (_, _) => StartOperation(async token =>
        {
            if (_binding is not null &&
                await _binding.MovePreviousPageAsync(token))
            {
                UpdateSheetState();
            }
        });
        next.Clicked += (_, _) => StartOperation(async token =>
        {
            if (_binding is not null &&
                await _binding.MoveNextPageAsync(token))
            {
                UpdateSheetState();
            }
        });
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
            search,
            status,
            values,
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
            value.SetBinding(
                Label.TextProperty,
                nameof(SpreadsheetTableFilterValueItem.DisplayText));
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

        var selectedIndex = _menuKindPicker.SelectedIndex;
        _menuKindPicker.ItemsSource = _binding.MenuKinds
            .Select(static kind => kind.GetDefaultDisplayName())
            .ToArray();
        _menuKindPicker.SelectedIndex = _binding.MenuKinds.Count == 0
            ? -1
            : Math.Clamp(selectedIndex, 0, _binding.MenuKinds.Count - 1);

        var first = _binding.TotalItemCount == 0
            ? 0
            : _binding.PageOffset + 1;
        var last = Math.Min(
            _binding.TotalItemCount,
            _binding.PageOffset + _binding.Items.Count);
        _status.Text = _binding.IsSourceTruncated
            ? $"{first:N0}–{last:N0}/{_binding.TotalItemCount:N0}; nguồn đã bị giới hạn."
            : $"{first:N0}–{last:N0}/{_binding.TotalItemCount:N0} giá trị.";
        SemanticProperties.SetDescription(_status, _status.Text);
        _previousButton.IsEnabled =
            _binding.HasPreviousPage && !_binding.IsBusy;
        _nextButton.IsEnabled =
            _binding.HasNextPage && !_binding.IsBusy;
        _applyButton.IsEnabled = !_binding.IsBusy;
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
        Entry Search,
        Label Status,
        CollectionView Values,
        Button Previous,
        Button Next,
        Button Apply);
}
