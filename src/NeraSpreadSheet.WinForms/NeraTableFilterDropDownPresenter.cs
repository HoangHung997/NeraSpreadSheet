using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

/// <summary>
/// Attachable native WinForms presenter for Table AutoFilter buttons and menus.
/// It creates one button per visible Table header column, never one control per cell.
/// </summary>
public sealed class NeraTableFilterDropDownPresenter : IDisposable
{
    private const int DropDownWidth = 340;
    private const int DropDownHeight = 430;

    private readonly NeraSpreadsheetControl _control;
    private readonly Dictionary<(Guid TableId, Guid ColumnId), Button>
        _buttons = [];
    private SpreadsheetSession? _viewportSession;
    private SpreadsheetViewportEngine? _viewport;
    private ToolStripDropDown? _dropDown;
    private bool _disposed;

    public NeraTableFilterDropDownPresenter(NeraSpreadsheetControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _control.Paint += OnControlPaint;
        _control.Resize += OnControlLayoutChanged;
        _control.ScrollChanged += OnScrollChanged;
        _control.Disposed += OnControlDisposed;
        UpdateButtons();
    }

    public bool IsOpen => _dropDown?.Visible == true;

    public void Close()
    {
        if (_dropDown is not null)
        {
            _dropDown.Close(ToolStripDropDownCloseReason.CloseCalled);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Close();
        _control.Paint -= OnControlPaint;
        _control.Resize -= OnControlLayoutChanged;
        _control.ScrollChanged -= OnScrollChanged;
        _control.Disposed -= OnControlDisposed;
        foreach (var button in _buttons.Values)
        {
            button.Click -= OnFilterButtonClick;
            _control.Controls.Remove(button);
            button.Dispose();
        }
        _buttons.Clear();
        _dropDown?.Dispose();
        _dropDown = null;
        _disposed = true;
    }

    public void Refresh() => UpdateButtons();

    private void UpdateButtons()
    {
        if (_disposed || _control.IsDisposed)
        {
            return;
        }

        var hits = GetVisibleButtons();
        var visibleKeys = new HashSet<(Guid, Guid)>();
        foreach (var hit in hits)
        {
            var key = (hit.TableId, hit.ColumnId);
            visibleKeys.Add(key);
            if (!_buttons.TryGetValue(key, out var button))
            {
                button = CreateFilterButton();
                _buttons.Add(key, button);
                _control.Controls.Add(button);
            }

            button.Tag = hit;
            button.Bounds = ToRectangle(hit.Bounds);
            button.BackColor = ToColor(
                hit.IsFiltered
                    ? _control.RenderTheme.TableFilterButtonActiveBackground
                    : _control.RenderTheme.TableFilterButtonBackground);
            button.ForeColor = ToColor(
                _control.RenderTheme.TableFilterButtonGlyph);
            button.Visible = true;
            button.BringToFront();
        }

        foreach (var (key, button) in _buttons)
        {
            if (!visibleKeys.Contains(key))
            {
                button.Visible = false;
            }
        }
    }

    private Button CreateFilterButton()
    {
        var button = new Button
        {
            Text = "▼",
            FlatStyle = FlatStyle.Flat,
            Padding = Padding.Empty,
            Margin = Padding.Empty,
            TabStop = false,
            Font = new Font(
                _control.Font.FontFamily,
                Math.Max(6f, _control.Font.Size - 2f),
                FontStyle.Regular,
                GraphicsUnit.Point),
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = ToColor(
            _control.RenderTheme.TableFilterButtonBorder);
        button.Click += OnFilterButtonClick;
        return button;
    }

    private IReadOnlyList<SpreadsheetTableFilterButtonHit> GetVisibleButtons()
    {
        var session = _control.Session;
        if (session is null ||
            _control.ClientSize.Width <= 0 ||
            _control.ClientSize.Height <= 0 ||
            !_control.RenderTheme.ShowTableFilterButtons)
        {
            return [];
        }

        var chrome = SpreadsheetChromeGeometry.Calculate(
            _control.ClientSize.Width,
            _control.ClientSize.Height,
            _control.RenderTheme);
        if (chrome.BodyWidth <= 0d || chrome.BodyHeight <= 0d)
        {
            return [];
        }

        if (!ReferenceEquals(_viewportSession, session))
        {
            _viewportSession = session;
            _viewport = new SpreadsheetViewportEngine(session);
        }

        var scroll = _control.ScrollSnapshot;
        var frame = _viewport!.Compose(
            scroll.OffsetX,
            scroll.OffsetY,
            chrome.BodyWidth,
            chrome.BodyHeight,
            overscan: 0d,
            _control.RenderTheme);
        return SpreadsheetTableFilterButtonGeometry.GetVisibleButtons(
                WorksheetSnapshot.Capture(session.ActiveWorksheet),
                frame.Layout,
                _control.RenderTheme)
            .Select(button => button with
            {
                Bounds = button.Bounds.Translate(
                    chrome.RowHeaderWidth,
                    chrome.ColumnHeaderHeight),
            })
            .ToArray();
    }

    private void OnFilterButtonClick(object? sender, EventArgs e)
    {
        if (sender is not Button
            {
                Tag: SpreadsheetTableFilterButtonHit hit,
            } button)
        {
            return;
        }

        Open(button, hit);
    }

    private void Open(
        Button placementButton,
        SpreadsheetTableFilterButtonHit hit)
    {
        var session = _control.Session
            ?? throw new InvalidOperationException(
                "A spreadsheet session is required before opening a Table filter menu.");
        var menu = new SpreadsheetTablePresenterController(session)
            .OpenFilterMenu(hit.TableId, hit.ColumnId);
        Close();
        _dropDown?.Dispose();

        var panel = BuildDropDownPanel(menu);
        var host = new ToolStripControlHost(panel)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = panel.Size,
        };
        var dropDown = new ToolStripDropDown
        {
            AutoSize = false,
            Padding = Padding.Empty,
            Size = panel.Size,
        };
        dropDown.Items.Add(host);
        dropDown.Closed += (_, _) =>
        {
            if (ReferenceEquals(_dropDown, dropDown))
            {
                _dropDown = null;
            }
        };
        _dropDown = dropDown;
        dropDown.Show(
            placementButton,
            new Point(0, placementButton.Height),
            ToolStripDropDownDirection.BelowRight);
    }

    private Panel BuildDropDownPanel(SpreadsheetTableFilterMenu menu)
    {
        var panel = new Panel
        {
            Size = new Size(DropDownWidth, DropDownHeight),
            BackColor = Color.White,
            Padding = new Padding(10),
        };
        var title = new Label
        {
            AutoSize = false,
            Text = $"{menu.TableName} — {menu.ColumnName}",
            Font = new Font(
                _control.Font,
                FontStyle.Bold),
            Location = new Point(10, 10),
            Size = new Size(DropDownWidth - 20, 24),
        };
        var search = new TextBox
        {
            PlaceholderText = "Tìm giá trị",
            Location = new Point(10, 39),
            Size = new Size(DropDownWidth - 20, 27),
        };
        var selectAll = CreateCommandButton(
            "Chọn tất cả",
            new Point(10, 72),
            100);
        var selectNone = CreateCommandButton(
            "Bỏ chọn",
            new Point(116, 72),
            88);
        var status = new Label
        {
            AutoSize = false,
            ForeColor = Color.DimGray,
            Location = new Point(10, 105),
            Size = new Size(DropDownWidth - 20, 34),
        };
        var values = new CheckedListBox
        {
            CheckOnClick = true,
            IntegralHeight = false,
            Location = new Point(10, 141),
            Size = new Size(DropDownWidth - 20, 224),
        };
        var clear = CreateCommandButton(
            "Xóa lọc",
            new Point(10, 382),
            82);
        var cancel = CreateCommandButton(
            "Hủy",
            new Point(174, 382),
            66);
        var apply = CreateCommandButton(
            "Áp dụng",
            new Point(246, 382),
            84);
        panel.Controls.AddRange([
            title,
            search,
            selectAll,
            selectNone,
            status,
            values,
            clear,
            cancel,
            apply,
        ]);

        var rebuilding = false;
        void RebuildItems()
        {
            rebuilding = true;
            values.BeginUpdate();
            try
            {
                values.Items.Clear();
                foreach (var item in menu.GetVisibleItems())
                {
                    values.Items.Add(
                        new FilterListItem(
                            item.Value,
                            DisplayValue(item.Value),
                            item.Count),
                        item.IsSelected);
                }
            }
            finally
            {
                values.EndUpdate();
                rebuilding = false;
            }

            status.Text = menu.ValuesTruncated
                ? $"Đã quét {menu.ScannedRowCount:N0} hàng; danh sách giá trị đã bị giới hạn."
                : $"{menu.DistinctValueCount:N0} giá trị khác nhau trong {menu.ScannedRowCount:N0} hàng.";
            apply.Enabled = menu.CanApplyValueSelection;
        }

        search.TextChanged += (_, _) =>
        {
            menu.SetSearchText(search.Text);
            RebuildItems();
        };
        values.ItemCheck += (_, e) =>
        {
            if (rebuilding || values.Items[e.Index] is not FilterListItem item)
            {
                return;
            }

            menu.SelectValue(
                item.Value,
                e.NewValue == CheckState.Checked);
            BeginInvokeIfHandleCreated(() =>
                apply.Enabled = menu.CanApplyValueSelection);
        };
        selectAll.Click += (_, _) =>
        {
            menu.SelectAllVisible();
            RebuildItems();
        };
        selectNone.Click += (_, _) =>
        {
            menu.ClearVisibleSelection();
            RebuildItems();
        };
        apply.Click += (_, _) =>
        {
            menu.ApplyValueSelection();
            CloseAndRefresh();
        };
        clear.Click += (_, _) =>
        {
            menu.ClearColumnFilter();
            CloseAndRefresh();
        };
        cancel.Click += (_, _) => Close();

        RebuildItems();
        return panel;
    }

    private static Button CreateCommandButton(
        string text,
        Point location,
        int width) =>
        new()
        {
            Text = text,
            Location = location,
            Size = new Size(width, 28),
            FlatStyle = FlatStyle.System,
        };

    private void CloseAndRefresh()
    {
        Close();
        _viewport?.InvalidateMetrics();
        UpdateButtons();
        _control.Invalidate();
    }

    private void BeginInvokeIfHandleCreated(Action action)
    {
        if (_control.IsHandleCreated && !_control.IsDisposed)
        {
            _control.BeginInvoke(action);
        }
    }

    private void OnControlPaint(object? sender, PaintEventArgs e) =>
        UpdateButtons();

    private void OnControlLayoutChanged(object? sender, EventArgs e) =>
        UpdateButtons();

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        UpdateButtons();

    private void OnControlDisposed(object? sender, EventArgs e) => Dispose();

    private static Rectangle ToRectangle(NeraSpreadSheet.Foundation.RectD bounds) =>
        Rectangle.FromLTRB(
            checked((int)Math.Round(bounds.Left)),
            checked((int)Math.Round(bounds.Top)),
            checked((int)Math.Round(bounds.Right)),
            checked((int)Math.Round(bounds.Bottom)));

    private static Color ToColor(NeraSpreadSheet.Foundation.ColorRgba color) =>
        Color.FromArgb(
            color.Alpha,
            color.Red,
            color.Green,
            color.Blue);

    private static string DisplayValue(CellValue value) =>
        value.IsBlank ? "(Trống)" : value.ToString();

    private sealed record FilterListItem(
        CellValue Value,
        string DisplayText,
        int Count)
    {
        public override string ToString() =>
            $"{DisplayText}  ({Count})";
    }
}
