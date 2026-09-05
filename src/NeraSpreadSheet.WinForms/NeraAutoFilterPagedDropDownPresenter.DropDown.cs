using NeraSpreadSheet.Commands;
using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.WinForms;

public sealed partial class NeraAutoFilterPagedDropDownPresenter
{
    private void Open(
        Button placementButton,
        SpreadsheetAutoFilterButtonHit hit,
        SpreadsheetAutoFilterTarget target)
    {
        var session = _control.Session
            ?? throw new InvalidOperationException(
                "A spreadsheet session is required before opening AutoFilter.");
        Close();
        CancelOperations();
        DisposeBinding();
        _dropDown?.Dispose();
        _focusBeforeOpen = placementButton;

        var presenter = new SpreadsheetAutoFilterPagedPresenter(
            session,
            target,
            PageSize);
        var binding = new NeraWinFormsAutoFilterPagedBinding(
            presenter,
            _control);
        _binding = binding;
        var content = BuildDropDownPanel(target);
        NeraWinFormsRibbonChrome.ApplyFilter(content, IconTheme);
        var host = new ToolStripControlHost(content)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = content.Size,
        };
        var dropDown = new ToolStripDropDown
        {
            AutoSize = false,
            Padding = Padding.Empty,
            Size = content.Size,
            AccessibleName = Localization.Format("Lọc {0} trong {1}", target.ColumnName, target.OwnerName),
            AccessibleDescription =
                Localization.Get("Dùng tìm kiếm, trang trước/sau và danh sách giá trị để lọc."),
        };
        dropDown.Items.Add(host);
        dropDown.Opened += OnDropDownOpened;
        dropDown.Closed += OnDropDownClosed;
        _dropDown = dropDown;
        dropDown.Show(
            placementButton,
            new Point(0, placementButton.Height),
            ToolStripDropDownDirection.BelowRight);
    }

    private Panel BuildDropDownPanel(
        SpreadsheetAutoFilterTarget target)
    {
        var panel = new Panel
        {
            Size = new Size(DropDownWidth, DropDownHeight),
            BackColor = Color.White,
            Padding = new Padding(10),
            AccessibleName = Localization.Format("Bộ lọc {0}", target.ColumnName),
            AccessibleDescription =
                Localization.Get("Danh sách giá trị được tải theo trang, không tải toàn bộ control cùng lúc."),
            AccessibleRole = AccessibleRole.Pane,
        };
        var title = new Label
        {
            AutoSize = false,
            Text = $"{target.OwnerName} — {target.ColumnName}",
            Font = new Font(_control.Font, FontStyle.Bold),
            Location = new Point(10, 10),
            Size = new Size(DropDownWidth - 20, 24),
            AccessibleRole = AccessibleRole.StaticText,
        };
        var search = new TextBox
        {
            PlaceholderText = Localization.Get("Tìm giá trị"),
            Location = new Point(10, 39),
            Size = new Size(DropDownWidth - 20, 27),
            AccessibleName = Localization.Format("Tìm giá trị trong cột {0}", target.ColumnName),
            AccessibleDescription =
                Localization.Get("Nhấn Enter để áp dụng, Escape để đóng, Page Up hoặc Page Down để đổi trang."),
            AccessibleRole = AccessibleRole.Text,
        };
        _searchBox = search;
        var menuKind = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(10, 72),
            Size = new Size(DropDownWidth - 20, 28),
            AccessibleName = Localization.Get("Nhóm điều kiện lọc"),
            AccessibleDescription = Localization.Get("Chọn lọc giá trị, văn bản, số, ngày, màu, biểu tượng hoặc điều kiện tùy chỉnh."),
        };
        _menuKindBox = menuKind;
        var criterionInput = new TextBox
        {
            PlaceholderText = Localization.Get("Giá trị điều kiện (Top10%, Today, #RRGGBB…)"),
            Location = new Point(10, 105),
            Size = new Size(DropDownWidth - 20, 27),
            AccessibleName = Localization.Get("Giá trị điều kiện lọc"),
        };
        _criterionInput = criterionInput;
        var conditionJoin = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(10, 138),
            Size = new Size(82, 28),
            DataSource = new[] { Localization.Get("Và"), Localization.Get("Hoặc") },
            AccessibleName = Localization.Get("Cách kết hợp điều kiện"),
            Visible = false,
        };
        _conditionJoinBox = conditionJoin;
        var secondCriterion = new TextBox
        {
            PlaceholderText = Localization.Get("Điều kiện thứ hai"),
            Location = new Point(98, 138),
            Size = new Size(DropDownWidth - 108, 27),
            AccessibleName = Localization.Get("Điều kiện lọc thứ hai"),
            Visible = false,
        };
        _secondCriterionInput = secondCriterion;
        var selectAll = CreateCommandButton(
            Localization.Get("Chọn kết quả"),
            new Point(10, 138),
            108,
            Localization.Get("Chọn mọi giá trị khớp tìm kiếm"));
        _selectAllButton = selectAll;
        var selectNone = CreateCommandButton(
            Localization.Get("Bỏ chọn kết quả"),
            new Point(124, 138),
            120,
            Localization.Get("Bỏ chọn mọi giá trị khớp tìm kiếm"));
        _selectNoneButton = selectNone;
        var dateBack = CreateCommandButton(
            Localization.Get("◀ Lùi một cấp ngày"),
            new Point(10, 138),
            150,
            Localization.Get("Quay về cấp ngày cha"));
        dateBack.Visible = false;
        _dateBackButton = dateBack;
        var status = new Label
        {
            AutoSize = false,
            ForeColor = Color.DimGray,
            Location = new Point(10, 171),
            Size = new Size(DropDownWidth - 20, 34),
            AccessibleRole = AccessibleRole.StaticText,
        };
        _status = status;
        var values = new CheckedListBox
        {
            CheckOnClick = true,
            IntegralHeight = false,
            Location = new Point(10, 207),
            Size = new Size(DropDownWidth - 20, 200),
            AccessibleName = Localization.Format("Trang giá trị lọc của cột {0}", target.ColumnName),
            AccessibleDescription =
                Localization.Get("Danh sách chỉ chứa trang hiện hành; Space để chọn hoặc bỏ chọn."),
            AccessibleRole = AccessibleRole.List,
        };
        _valuesList = values;
        var previous = CreateCommandButton(
            Localization.Get("◀ Trang trước"),
            new Point(10, 413),
            105,
            Localization.Get("Tải trang giá trị trước"));
        var next = CreateCommandButton(
            Localization.Get("Trang sau ▶"),
            new Point(121, 413),
            105,
            Localization.Get("Tải trang giá trị sau"));
        _previousButton = previous;
        _nextButton = next;
        var sortAscending = CreateCommandButton(
            Localization.Get("Sắp xếp ↑"), new Point(10, 451), 80, Localization.Get("Sắp xếp cột tăng dần"));
        var sortDescending = CreateCommandButton(
            Localization.Get("Sắp xếp ↓"), new Point(96, 451), 80, Localization.Get("Sắp xếp cột giảm dần"));
        var reapply = CreateCommandButton(
            Localization.Get("Áp dụng lại"), new Point(182, 451), 78, Localization.Get("Áp dụng lại lọc và sắp xếp hiện tại"));
        var clearSort = CreateCommandButton(
            Localization.Get("Xóa SX"), new Point(266, 451), 74, Localization.Get("Xóa trạng thái sắp xếp"));
        var clear = CreateCommandButton(
            Localization.Get("Xóa lọc"),
            new Point(10, 487),
            82,
            Localization.Get("Xóa bộ lọc hiện tại của cột này"));
        var cancel = CreateCommandButton(
            Localization.Get("Hủy"),
            new Point(184, 487),
            66,
            Localization.Get("Đóng mà không áp dụng thay đổi"));
        var apply = CreateCommandButton(
            Localization.Get("Áp dụng"),
            new Point(256, 487),
            84,
            Localization.Get("Áp dụng lựa chọn trên toàn danh sách đã phân trang"));
        _applyButton = apply;
        panel.Controls.AddRange([
            title,
            search,
            menuKind,
            criterionInput,
            conditionJoin,
            secondCriterion,
            selectAll,
            selectNone,
            dateBack,
            status,
            values,
            previous,
            next,
            sortAscending,
            sortDescending,
            reapply,
            clearSort,
            clear,
            cancel,
            apply,
        ]);

        search.TextChanged += (_, _) => ScheduleSearch(search.Text);
        menuKind.SelectedIndexChanged += (_, _) =>
        {
            if (!_rebuilding)
            {
                StartOperation(RefreshSelectedModeAsync);
            }
        };
        values.ItemCheck += OnValueItemCheck;
        values.DoubleClick += (_, _) =>
        {
            if (values.SelectedItem is DateListItem { Node.HasChildren: true } item)
            {
                StartOperation(token => NavigateDateIntoAsync(item.Node, token));
            }
        };
        selectAll.Click += (_, _) => StartOperation(async token =>
        {
            var binding = _binding;
            if (binding is null)
            {
                return;
            }
            await binding.SelectAllVisibleAsync(token);
            if (ReferenceEquals(_binding, binding)) RebuildPage();
        });
        selectNone.Click += (_, _) => StartOperation(async token =>
        {
            var binding = _binding;
            if (binding is null)
            {
                return;
            }
            await binding.ClearVisibleSelectionAsync(token);
            if (ReferenceEquals(_binding, binding)) RebuildPage();
        });
        previous.Click += (_, _) => StartOperation(token =>
            MovePageAsync(next: false, token));
        next.Click += (_, _) => StartOperation(token =>
            MovePageAsync(next: true, token));
        dateBack.Click += (_, _) => StartOperation(NavigateDateBackAsync);
        sortAscending.Click += (_, _) => StartOperation(token =>
            SortAndCloseAsync(false, criterionInput.Text, token));
        sortDescending.Click += (_, _) => StartOperation(token =>
            SortAndCloseAsync(true, criterionInput.Text, token));
        reapply.Click += (_, _) => StartOperation(ReapplyAndCloseAsync);
        clearSort.Click += (_, _) => StartOperation(ClearSortAndCloseAsync);
        clear.Click += (_, _) => StartOperation(ClearAndCloseAsync);
        cancel.Click += (_, _) => Close();
        apply.Click += (_, _) => StartOperation(ApplyAndCloseAsync);

        KeyEventHandler keyHandler = (_, args) => OnDropDownKeyDown(args);
        search.KeyDown += keyHandler;
        menuKind.KeyDown += keyHandler;
        criterionInput.KeyDown += keyHandler;
        conditionJoin.KeyDown += keyHandler;
        secondCriterion.KeyDown += keyHandler;
        values.KeyDown += keyHandler;
        selectAll.KeyDown += keyHandler;
        selectNone.KeyDown += keyHandler;
        previous.KeyDown += keyHandler;
        next.KeyDown += keyHandler;
        sortAscending.KeyDown += keyHandler;
        sortDescending.KeyDown += keyHandler;
        reapply.KeyDown += keyHandler;
        clearSort.KeyDown += keyHandler;
        clear.KeyDown += keyHandler;
        cancel.KeyDown += keyHandler;
        apply.KeyDown += keyHandler;
        return panel;
    }

    private void OnDropDownOpened(object? sender, EventArgs e)
    {
        if (sender is not ToolStripDropDown dropDown ||
            !ReferenceEquals(_dropDown, dropDown))
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
            FocusSearchBox(dropDown);
        });
    }

    private void OnDropDownClosed(object? sender, ToolStripDropDownClosedEventArgs e)
    {
        if (sender is not ToolStripDropDown dropDown)
        {
            return;
        }
        dropDown.Opened -= OnDropDownOpened;
        dropDown.Closed -= OnDropDownClosed;
        CancelOperations();
        DisposeBinding();
        if (ReferenceEquals(_dropDown, dropDown))
        {
            _dropDown = null;
            _searchBox = null;
            _menuKindBox = null;
            _criterionInput = null;
            _secondCriterionInput = null;
            _conditionJoinBox = null;
            _selectAllButton = null;
            _selectNoneButton = null;
            _dateBackButton = null;
            _valuesList = null;
            _status = null;
            _previousButton = null;
            _nextButton = null;
            _applyButton = null;
            _datePage = null;
            _selectedDateGroups.Clear();
        }
        RestoreFocus();
    }

    private void RebuildPage()
    {
        if (_binding is null ||
            _valuesList is null ||
            _status is null)
        {
            return;
        }

        _rebuilding = true;
        if (_menuKindBox is not null)
        {
            var selectedIndex = _menuKindBox.SelectedIndex;
            var labels = _binding.MenuKinds
                .Select(kind => Localization.Get(kind.GetDefaultDisplayName()))
                .ToArray();
            _menuKindBox.BeginUpdate();
            _menuKindBox.DataSource = null;
            _menuKindBox.Items.Clear();
            _menuKindBox.Items.AddRange(labels);
            _menuKindBox.SelectedIndex = labels.Length == 0
                ? -1
                : Math.Clamp(selectedIndex, 0, labels.Length - 1);
            _menuKindBox.EndUpdate();
        }
        var kind = GetSelectedMenuKind(_binding);
        var isValues = kind == SpreadsheetAutoFilterMenuKind.Values;
        var isDate = kind == SpreadsheetAutoFilterMenuKind.Date;
        var isCustom = kind == SpreadsheetAutoFilterMenuKind.Custom;
        _criterionInput!.Visible = !isValues && !isDate;
        _secondCriterionInput!.Visible = isCustom;
        _conditionJoinBox!.Visible = isCustom;
        _searchBox!.Visible = isValues;
        _selectAllButton!.Visible = isValues;
        _selectNoneButton!.Visible = isValues;
        _dateBackButton!.Visible = isDate && _dateParent.Year is not null;
        _valuesList.Visible = isValues || isDate;
        _valuesList.BeginUpdate();
        try
        {
            _valuesList.Items.Clear();
            if (isDate)
            {
                foreach (var node in _datePage?.Nodes ?? [])
                {
                    var group = ToDateGroup(node);
                    _valuesList.Items.Add(
                        new DateListItem(node, DisplayDateNode(node)),
                        _selectedDateGroups.Contains(group));
                }
            }
            else
            {
                foreach (var item in _binding.Items)
                {
                    _valuesList.Items.Add(
                        new FilterListItem(
                            item.Value,
                            DisplayValue(item.Value),
                            item.Count),
                        item.IsSelected);
                }
            }
        }
        finally
        {
            _valuesList.EndUpdate();
            _rebuilding = false;
        }

        var total = isDate ? _datePage?.TotalNodeCount ?? 0 : _binding.TotalItemCount;
        var offset = isDate ? _datePage?.Offset ?? 0 : _binding.PageOffset;
        var pageCount = isDate ? _datePage?.Nodes.Count ?? 0 : _binding.Items.Count;
        var first = total == 0 ? 0 : offset + 1;
        var last = Math.Min(total, offset + pageCount);
        _status.Text = !isValues && !isDate
            ? isCustom
                ? Localization.Get("Nhập một hoặc hai điều kiện rồi chọn cách kết hợp.")
                : Localization.Get("Nhập điều kiện lọc rồi chọn Áp dụng.")
            : isDate
            ? Localization.Format("{0:N0}–{1:N0}/{2:N0} nhóm ngày; đã chọn {3:N0}.", first, last, total, _selectedDateGroups.Count)
            : _binding.IsSourceTruncated
                ? Localization.Format("{0:N0}–{1:N0}/{2:N0}; nguồn bị giới hạn, không thể áp dụng chọn giá trị.", first, last, total)
                : Localization.Format("{0:N0}–{1:N0}/{2:N0} giá trị.", first, last, total);
        _status.AccessibleName = _status.Text;
        _status.AccessibleDescription = _binding.AccessibilityAnnouncement;
        _previousButton!.Visible = isValues || isDate;
        _nextButton!.Visible = isValues || isDate;
        _previousButton.Enabled = isDate
            ? _datePage?.HasPreviousPage == true && !_binding.IsBusy
            : _binding.HasPreviousPage && !_binding.IsBusy;
        _nextButton.Enabled = isDate
            ? _datePage?.HasNextPage == true && !_binding.IsBusy
            : _binding.HasNextPage && !_binding.IsBusy;
        _applyButton!.Enabled =
            !_binding.IsBusy &&
            (!isDate || _selectedDateGroups.Count > 0) &&
            (!isValues || !_binding.IsSourceTruncated);
    }

    private void OnValueItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_rebuilding || _binding is null)
        {
            return;
        }
        if (_valuesList?.Items[e.Index] is DateListItem dateItem)
        {
            var group = ToDateGroup(dateItem.Node);
            if (e.NewValue == CheckState.Checked)
            {
                _selectedDateGroups.Add(group);
            }
            else
            {
                _selectedDateGroups.Remove(group);
            }
            if (_status is not null)
            {
                _status.Text = Localization.Format("Đã chọn {0:N0} nhóm ngày.", _selectedDateGroups.Count);
            }
            return;
        }
        StartOperation(token => _binding.SetSelectedAsync(
            e.Index,
            e.NewValue == CheckState.Checked,
            token));
    }

    private sealed record DateListItem(
        SpreadsheetAutoFilterDateNode Node,
        string Text)
    {
        public override string ToString() =>
            $"{(Node.HasChildren ? "▶ " : string.Empty)}{Text}  ({Node.Count:N0})";
    }

    private void OnDropDownKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            Suppress(e);
            return;
        }
        if (e.KeyCode == Keys.PageDown &&
            _valuesList?.Focused == true &&
            _nextButton?.Enabled == true)
        {
            _nextButton.PerformClick();
            Suppress(e);
            return;
        }
        if (e.KeyCode == Keys.PageUp &&
            _valuesList?.Focused == true &&
            _previousButton?.Enabled == true)
        {
            _previousButton.PerformClick();
            Suppress(e);
            return;
        }
        if (e.KeyCode == Keys.Enter &&
            _searchBox?.Focused == true)
        {
            StartOperation(ApplyAndCloseAsync);
            Suppress(e);
            return;
        }
        if (e.KeyCode == Keys.Enter &&
            _valuesList?.Focused == true &&
            _valuesList.SelectedIndex >= 0)
        {
            var index = _valuesList.SelectedIndex;
            _valuesList.SetItemChecked(index, !_valuesList.GetItemChecked(index));
            Suppress(e);
        }
    }

    private static Button CreateCommandButton(
        string text,
        Point location,
        int width,
        string description) =>
        new()
        {
            Text = text,
            Location = location,
            Size = new Size(width, 29),
            AccessibleName = text,
            AccessibleDescription = description,
            AccessibleRole = AccessibleRole.PushButton,
        };

    private static void Suppress(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private string DisplayValue(CellValue value) =>
        value.IsBlank ? Localization.Get("(Trống)") : value.ToString();

    private sealed record FilterListItem(
        CellValue Value,
        string DisplayText,
        int Count)
    {
        public override string ToString() =>
            $"{DisplayText}  ({Count:N0})";
    }
}
