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
            AccessibleName = $"Lọc {target.ColumnName} trong {target.OwnerName}",
            AccessibleDescription =
                "Dùng tìm kiếm, trang trước/sau và danh sách giá trị để lọc.",
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
            AccessibleName = $"Bộ lọc {target.ColumnName}",
            AccessibleDescription =
                "Danh sách giá trị được tải theo trang, không tải toàn bộ control cùng lúc.",
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
            PlaceholderText = "Tìm giá trị",
            Location = new Point(10, 39),
            Size = new Size(DropDownWidth - 20, 27),
            AccessibleName = $"Tìm giá trị trong cột {target.ColumnName}",
            AccessibleDescription =
                "Nhấn Enter để áp dụng, Escape để đóng, Page Up hoặc Page Down để đổi trang.",
            AccessibleRole = AccessibleRole.Text,
        };
        _searchBox = search;
        var selectAll = CreateCommandButton(
            "Chọn kết quả",
            new Point(10, 72),
            108,
            "Chọn mọi giá trị khớp tìm kiếm");
        var selectNone = CreateCommandButton(
            "Bỏ chọn kết quả",
            new Point(124, 72),
            120,
            "Bỏ chọn mọi giá trị khớp tìm kiếm");
        var status = new Label
        {
            AutoSize = false,
            ForeColor = Color.DimGray,
            Location = new Point(10, 105),
            Size = new Size(DropDownWidth - 20, 34),
            AccessibleRole = AccessibleRole.StaticText,
        };
        _status = status;
        var values = new CheckedListBox
        {
            CheckOnClick = true,
            IntegralHeight = false,
            Location = new Point(10, 141),
            Size = new Size(DropDownWidth - 20, 218),
            AccessibleName = $"Trang giá trị lọc của cột {target.ColumnName}",
            AccessibleDescription =
                "Danh sách chỉ chứa trang hiện hành; Space để chọn hoặc bỏ chọn.",
            AccessibleRole = AccessibleRole.List,
        };
        _valuesList = values;
        var previous = CreateCommandButton(
            "◀ Trang trước",
            new Point(10, 365),
            105,
            "Tải trang giá trị trước");
        var next = CreateCommandButton(
            "Trang sau ▶",
            new Point(121, 365),
            105,
            "Tải trang giá trị sau");
        _previousButton = previous;
        _nextButton = next;
        var clear = CreateCommandButton(
            "Xóa lọc",
            new Point(10, 419),
            82,
            "Xóa bộ lọc hiện tại của cột này");
        var cancel = CreateCommandButton(
            "Hủy",
            new Point(184, 419),
            66,
            "Đóng mà không áp dụng thay đổi");
        var apply = CreateCommandButton(
            "Áp dụng",
            new Point(256, 419),
            84,
            "Áp dụng lựa chọn trên toàn danh sách đã phân trang");
        _applyButton = apply;
        panel.Controls.AddRange([
            title,
            search,
            selectAll,
            selectNone,
            status,
            values,
            previous,
            next,
            clear,
            cancel,
            apply,
        ]);

        search.TextChanged += (_, _) => ScheduleSearch(search.Text);
        values.ItemCheck += OnValueItemCheck;
        selectAll.Click += (_, _) => StartOperation(async token =>
        {
            if (_binding is null)
            {
                return;
            }
            await _binding.SelectAllVisibleAsync(token);
            RebuildPage();
        });
        selectNone.Click += (_, _) => StartOperation(async token =>
        {
            if (_binding is null)
            {
                return;
            }
            await _binding.ClearVisibleSelectionAsync(token);
            RebuildPage();
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

        KeyEventHandler keyHandler = (_, args) => OnDropDownKeyDown(args);
        search.KeyDown += keyHandler;
        values.KeyDown += keyHandler;
        selectAll.KeyDown += keyHandler;
        selectNone.KeyDown += keyHandler;
        previous.KeyDown += keyHandler;
        next.KeyDown += keyHandler;
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
            if (_binding is null)
            {
                return;
            }
            await _binding.InitializeAsync(token);
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
            _valuesList = null;
            _status = null;
            _previousButton = null;
            _nextButton = null;
            _applyButton = null;
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
        _valuesList.BeginUpdate();
        try
        {
            _valuesList.Items.Clear();
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
        finally
        {
            _valuesList.EndUpdate();
            _rebuilding = false;
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
        _status.AccessibleName = _status.Text;
        _previousButton!.Enabled =
            _binding.HasPreviousPage && !_binding.IsBusy;
        _nextButton!.Enabled =
            _binding.HasNextPage && !_binding.IsBusy;
        _applyButton!.Enabled = !_binding.IsBusy;
    }

    private void OnValueItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_rebuilding || _binding is null)
        {
            return;
        }
        StartOperation(token => _binding.SetSelectedAsync(
            e.Index,
            e.NewValue == CheckState.Checked,
            token));
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
            _nextButton?.Enabled == true)
        {
            _nextButton.PerformClick();
            Suppress(e);
            return;
        }
        if (e.KeyCode == Keys.PageUp &&
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

    private static string DisplayValue(CellValue value) =>
        value.IsBlank ? "(Trống)" : value.ToString();

    private sealed record FilterListItem(
        CellValue Value,
        string DisplayText,
        int Count)
    {
        public override string ToString() =>
            $"{DisplayText}  ({Count:N0})";
    }
}
