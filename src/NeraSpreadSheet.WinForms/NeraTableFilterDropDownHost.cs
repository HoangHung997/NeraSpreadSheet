using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

/// <summary>
/// Native WinForms Table-filter dropdown backed by the platform-neutral presenter.
/// </summary>
public sealed class NeraTableFilterDropDown : IDisposable
{
    private readonly SpreadsheetTablePresenterController _presenter;
    private readonly ToolStripDropDown _dropDown;
    private readonly TextBox _searchBox;
    private readonly Label _summary;
    private readonly CheckedListBox _values;
    private SpreadsheetTableFilterMenu? _menu;
    private bool _refreshing;
    private bool _disposed;

    public NeraTableFilterDropDown(SpreadsheetSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _presenter = new SpreadsheetTablePresenterController(session);
        var panel = new TableLayoutPanel
        {
            AutoSize = false,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(8),
            Size = new Size(340, 430),
        };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 30f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 72f));
        _searchBox = new TextBox
        {
            Dock = DockStyle.Fill,
            PlaceholderText = "Tìm giá trị",
        };
        _searchBox.TextChanged += OnSearchChanged;
        _summary = new Label
        {
            Dock = DockStyle.Fill,
            AutoEllipsis = true,
            ForeColor = Color.DimGray,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        _values = new CheckedListBox
        {
            Dock = DockStyle.Fill,
            CheckOnClick = true,
            IntegralHeight = false,
        };
        _values.ItemCheck += OnItemCheck;
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
        };
        buttons.Controls.Add(CreateButton("Chọn tất cả", (_, _) =>
            _menu?.SelectAllVisible()));
        buttons.Controls.Add(CreateButton("Bỏ chọn", (_, _) =>
            _menu?.ClearVisibleSelection()));
        buttons.Controls.Add(CreateButton("Xóa lọc", OnClearFilter));
        buttons.Controls.Add(CreateButton("Áp dụng", OnApply));
        buttons.Controls.Add(CreateButton("Đóng", (_, _) => Close()));
        panel.Controls.Add(_searchBox, 0, 0);
        panel.Controls.Add(_summary, 0, 1);
        panel.Controls.Add(_values, 0, 2);
        panel.Controls.Add(buttons, 0, 3);

        var host = new ToolStripControlHost(panel)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = panel.Size,
        };
        _dropDown = new ToolStripDropDown
        {
            AutoClose = true,
            Padding = Padding.Empty,
        };
        _dropDown.Items.Add(host);
        _dropDown.Closed += OnClosed;
    }

    public bool IsOpen => _dropDown.Visible;

    public event EventHandler? Closed;

    public void Show(
        Control owner,
        Rectangle anchorBounds,
        Guid tableId,
        Guid columnId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(owner);
        Close();
        _menu = _presenter.OpenFilterMenu(tableId, columnId);
        _menu.Changed += OnMenuChanged;
        _searchBox.Text = string.Empty;
        RefreshFromMenu();
        _dropDown.Show(owner, new Point(anchorBounds.Left, anchorBounds.Bottom));
        _searchBox.Focus();
    }

    public void Close()
    {
        if (_dropDown.Visible)
        {
            _dropDown.Close();
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
        _dropDown.Closed -= OnClosed;
        _searchBox.TextChanged -= OnSearchChanged;
        _values.ItemCheck -= OnItemCheck;
        _dropDown.Dispose();
        _disposed = true;
    }

    private static Button CreateButton(
        string text,
        EventHandler handler)
    {
        var button = new Button
        {
            AutoSize = true,
            Text = text,
            Margin = new Padding(0, 0, 6, 6),
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
            _values.BeginUpdate();
            try
            {
                _values.Items.Clear();
                foreach (var item in state.Values)
                {
                    _values.Items.Add(
                        new ValueItem(
                            item.Value,
                            $"{item.DisplayText} ({item.Count})"),
                        item.IsSelected);
                }
            }
            finally
            {
                _values.EndUpdate();
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void OnSearchChanged(object? sender, EventArgs e)
    {
        if (!_refreshing)
        {
            _menu?.SetSearchText(_searchBox.Text);
        }
    }

    private void OnItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_refreshing ||
            _menu is null ||
            _values.Items[e.Index] is not ValueItem item)
        {
            return;
        }
        _menu.SetSelected(item.Value, e.NewValue == CheckState.Checked);
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

    private void OnClosed(object? sender, ToolStripDropDownClosedEventArgs e)
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

    private sealed record ValueItem(CellValue Value, string Text)
    {
        public override string ToString() => Text;
    }
}

/// <summary>
/// Hooks Table-header filter-button hit testing to a WinForms spreadsheet control.
/// </summary>
public sealed class NeraTableFilterDropDownHost : IDisposable
{
    private readonly NeraSpreadsheetControl _control;
    private readonly NeraTableFilterDropDown _dropDown;
    private bool _disposed;

    public NeraTableFilterDropDownHost(NeraSpreadsheetControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _dropDown = new NeraTableFilterDropDown(
            control.Session ?? throw new InvalidOperationException(
                "Assign a SpreadsheetSession before enabling the Table filter dropdown."));
        _control.MouseDown += OnMouseDown;
    }

    public NeraTableFilterDropDown DropDown => _dropDown;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _control.MouseDown -= OnMouseDown;
        _dropDown.Dispose();
        _disposed = true;
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        var session = _control.Session;
        if (e.Button != MouseButtons.Left ||
            session is null ||
            _control.ClientSize.Width <= 0 ||
            _control.ClientSize.Height <= 0)
        {
            return;
        }
        var chrome = SpreadsheetChromeGeometry.Calculate(
            _control.ClientSize.Width,
            _control.ClientSize.Height,
            _control.RenderTheme);
        var chromeHit = SpreadsheetChromeGeometry.HitTest(
            e.X,
            e.Y,
            _control.ClientSize.Width,
            _control.ClientSize.Height,
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

        var anchor = hit.Bounds.Translate(
            chrome.RowHeaderWidth,
            chrome.ColumnHeaderHeight);
        _dropDown.Show(
            _control,
            Rectangle.Round(new RectangleF(
                (float)anchor.X,
                (float)anchor.Y,
                (float)anchor.Width,
                (float)anchor.Height)),
            hit.TableId,
            hit.ColumnId);
    }
}
