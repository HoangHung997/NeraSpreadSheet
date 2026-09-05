using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Commands;
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
    /// <summary>Resources used when the filter surface is next opened or refreshed.</summary>
    public PresentationLocalization Localization { get; set; } = PresentationLocalization.Default;

    /// <summary>Gets or sets the palette used the next time the filter opens.</summary>
    public NeraIconTheme IconTheme { get; set; } = NeraIconTheme.Light;

    private const int DropDownWidth = 340;
    private const int DropDownHeight = 466;

    private readonly NeraSpreadsheetControl _control;
    private readonly Dictionary<(Guid TableId, Guid ColumnId), Button>
        _buttons = [];
    private SpreadsheetSession? _viewportSession;
    private SpreadsheetViewportEngine? _viewport;
    private SpreadsheetTableFilterNavigator? _navigator;
    private ToolStripDropDown? _dropDown;
    private TextBox? _searchBox;
    private CheckedListBox? _valuesList;
    private Button? _applyButton;
    private Control? _focusBeforeOpen;
    private bool _disposed;

    public NeraTableFilterDropDownPresenter(NeraSpreadsheetControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _control.Paint += OnControlPaint;
        _control.Resize += OnControlLayoutChanged;
        _control.ScrollChanged += OnScrollChanged;
        _control.PreviewKeyDown += OnControlPreviewKeyDown;
        _control.KeyDown += OnControlKeyDown;
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

    public bool TryOpenForActiveCell()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var session = _control.Session;
        if (session is null ||
            !session.TryResolveActiveTableFilterTarget(out var target))
        {
            return false;
        }

        UpdateButtons();
        var key = (target.TableId, target.ColumnId);
        if (!_buttons.TryGetValue(key, out var button) ||
            !button.Visible ||
            button.Tag is not SpreadsheetTableFilterButtonHit hit)
        {
            return false;
        }

        Open(button, hit);
        return true;
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
        _control.PreviewKeyDown -= OnControlPreviewKeyDown;
        _control.KeyDown -= OnControlKeyDown;
        _control.Disposed -= OnControlDisposed;
        foreach (var button in _buttons.Values)
        {
            button.Click -= OnFilterButtonClick;
            _control.Controls.Remove(button);
            button.Dispose();
        }
        _buttons.Clear();
        _navigator?.Dispose();
        _navigator = null;
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
            var session = _control.Session;
            var table = session?.ActiveWorksheet.Tables.FirstOrDefault(candidate =>
                candidate.Id == hit.TableId);
            var visual = session is null || table is null
                ? new SpreadsheetTableFilterButtonVisual(
                    _control.RenderTheme.TableFilterButtonBackground,
                    _control.RenderTheme.TableFilterButtonActiveBackground,
                    _control.RenderTheme.TableFilterButtonBorder,
                    _control.RenderTheme.TableFilterButtonGlyph)
                : SpreadsheetTableStyleVisuals.ResolveFilterButton(
                    session.Workbook,
                    table,
                    _control.RenderTheme);
            var key = (hit.TableId, hit.ColumnId);
            visibleKeys.Add(key);
            if (!_buttons.TryGetValue(key, out var button))
            {
                button = CreateFilterButton();
                _buttons.Add(key, button);
                _control.Controls.Add(button);
            }

            button.Tag = hit;
            button.Text = NeraWinFormsFilterHeaderGlyphs.Get(
                hit.HeaderState,
                hit.SortDescending);
            button.Bounds = ToRectangle(hit.Bounds);
            button.BackColor = ToColor(
                hit.IsFiltered
                    ? visual.ActiveBackground
                    : visual.Background);
            button.ForeColor = ToColor(
                visual.Glyph);
            button.FlatAppearance.BorderColor = ToColor(visual.Border);
            button.AccessibleName = $"{GetFilterButtonAccessibleName(hit)}, {GetHeaderStateText(hit)}";
            button.AccessibleDescription =
                Localization.Get("Mở menu lọc bằng Enter, Space hoặc Alt+mũi tên xuống từ ô đang chọn.");
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
            TabStop = true,
            AccessibleRole = AccessibleRole.PushButton,
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

    private string GetHeaderStateText(SpreadsheetTableFilterButtonHit hit) =>
        hit.HeaderState switch
        {
            SpreadsheetFilterHeaderState.Filtered => Localization.Get("đang lọc"),
            SpreadsheetFilterHeaderState.Sorted => hit.SortDescending == true
                ? Localization.Get("đang sắp xếp giảm dần")
                : Localization.Get("đang sắp xếp tăng dần"),
            SpreadsheetFilterHeaderState.FilteredAndSorted => hit.SortDescending == true
                ? Localization.Get("đang lọc và sắp xếp giảm dần")
                : Localization.Get("đang lọc và sắp xếp tăng dần"),
            _ => Localization.Get("chưa lọc hoặc sắp xếp"),
        };

    private SpreadsheetTableFilterButtonHit[] GetVisibleButtons()
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

    private string GetFilterButtonAccessibleName(
        SpreadsheetTableFilterButtonHit hit)
    {
        var worksheet = _control.Session?.ActiveWorksheet;
        if (worksheet is not null &&
            worksheet.TryGetTable(hit.TableId, out var table) &&
            table is not null &&
            table.TryGetColumn(hit.ColumnId, out var column) &&
            column is not null)
        {
            return Localization.Format("Lọc cột {0} trong Table {1}", column.Name, table.Name);
        }

        return Localization.Get("Mở bộ lọc Table");
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
        Close();
        _dropDown?.Dispose();
        _focusBeforeOpen = placementButton;
        var menu = new SpreadsheetTablePresenterController(session)
            .OpenFilterMenu(hit.TableId, hit.ColumnId);
        var navigator = new SpreadsheetTableFilterNavigator(menu);
        _navigator = navigator;

        var content = BuildDropDownPanel(menu, navigator);
        NeraWinFormsRibbonChrome.ApplyFilter(content.Panel, IconTheme);
        _searchBox = content.Search;
        _valuesList = content.Values;
        _applyButton = content.Apply;
        var host = new ToolStripControlHost(content.Panel)
        {
            AutoSize = false,
            Margin = Padding.Empty,
            Padding = Padding.Empty,
            Size = content.Panel.Size,
        };
        var dropDown = new ToolStripDropDown
        {
            AutoSize = false,
            Padding = Padding.Empty,
            Size = content.Panel.Size,
            AccessibleName = Localization.Format("Lọc {0} trong Table {1}", menu.ColumnName, menu.TableName),
            AccessibleDescription =
                Localization.Get("Dùng mũi tên để duyệt, Space hoặc Enter để chọn, Escape để đóng."),
        };
        dropDown.Items.Add(host);
        dropDown.Opened += (_, _) => FocusSearchBox(dropDown, content.Search);
        dropDown.Closed += (_, _) =>
        {
            navigator.Dispose();
            if (ReferenceEquals(_dropDown, dropDown))
            {
                _dropDown = null;
                _navigator = null;
                _searchBox = null;
                _valuesList = null;
                _applyButton = null;
            }
            RestoreFocus();
        };
        _dropDown = dropDown;
        dropDown.Show(
            placementButton,
            new Point(0, placementButton.Height),
            ToolStripDropDownDirection.BelowRight);
    }

    private DropDownContent BuildDropDownPanel(
        SpreadsheetTableFilterMenu menu,
        SpreadsheetTableFilterNavigator navigator)
    {
        var panel = new Panel
        {
            Size = new Size(DropDownWidth, DropDownHeight),
            BackColor = Color.White,
            Padding = new Padding(10),
            AccessibleName = Localization.Format("Bộ lọc {0}", menu.ColumnName),
            AccessibleDescription =
                Localization.Get("Tab để chuyển vùng; mũi tên, Home, End, Page Up và Page Down để duyệt giá trị."),
            AccessibleRole = AccessibleRole.Pane,
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
            AccessibleName = Localization.Format("Lọc {0} trong Table {1}", menu.ColumnName, menu.TableName),
            AccessibleRole = AccessibleRole.StaticText,
        };
        var search = new TextBox
        {
            PlaceholderText = Localization.Get("Tìm giá trị"),
            Location = new Point(10, 75),
            Size = new Size(DropDownWidth - 20, 27),
            AccessibleName = Localization.Format("Tìm giá trị trong cột {0}", menu.ColumnName),
            AccessibleDescription =
                Localization.Get("Nhấn Enter để áp dụng, Escape để đóng, hoặc mũi tên xuống để vào danh sách."),
            AccessibleRole = AccessibleRole.Text,
        };
        var selectAll = CreateCommandButton(
            Localization.Get("Chọn tất cả"),
            new Point(10, 108),
            100,
            Localization.Get("Chọn mọi giá trị đang hiển thị"));
        var selectNone = CreateCommandButton(
            Localization.Get("Bỏ chọn"),
            new Point(116, 108),
            88,
            Localization.Get("Bỏ chọn mọi giá trị đang hiển thị"));
        var status = new Label
        {
            AutoSize = false,
            ForeColor = Color.DimGray,
            Location = new Point(10, 141),
            Size = new Size(DropDownWidth - 20, 34),
            AccessibleRole = AccessibleRole.StaticText,
        };
        var values = new CheckedListBox
        {
            CheckOnClick = true,
            IntegralHeight = false,
            Location = new Point(10, 177),
            Size = new Size(DropDownWidth - 20, 224),
            AccessibleName = Localization.Format("Giá trị lọc của cột {0}", menu.ColumnName),
            AccessibleDescription =
                Localization.Get("Dùng mũi tên để duyệt; Space hoặc Enter để chọn hay bỏ chọn."),
            AccessibleRole = AccessibleRole.List,
        };
        var clear = CreateCommandButton(
            Localization.Get("Xóa lọc"),
            new Point(10, 418),
            82,
            Localization.Get("Xóa bộ lọc hiện tại của cột này"));
        var cancel = CreateCommandButton(
            Localization.Get("Hủy"),
            new Point(174, 418),
            66,
            Localization.Get("Đóng mà không áp dụng thay đổi"));
        var apply = CreateCommandButton(
            Localization.Get("Áp dụng"),
            new Point(246, 418),
            84,
            Localization.Get("Áp dụng các giá trị đã chọn"));
        var sortAscending = CreateCommandButton(
            Localization.Get("Sắp ↑"),
            new Point(10, 41),
            70,
            Localization.Get("Sắp xếp Table tăng dần theo cột này"));
        sortAscending.AccessibleName = Localization.Get("Sắp xếp tăng dần");
        var sortDescending = CreateCommandButton(
            Localization.Get("Sắp ↓"),
            new Point(84, 41),
            70,
            Localization.Get("Sắp xếp Table giảm dần theo cột này"));
        sortDescending.AccessibleName = Localization.Get("Sắp xếp giảm dần");
        var reapply = CreateCommandButton(
            Localization.Get("Áp dụng lại"),
            new Point(158, 41),
            92,
            Localization.Get("Áp dụng lại thứ tự sắp xếp hiện tại"));
        var clearSort = CreateCommandButton(
            Localization.Get("Xóa SX"),
            new Point(254, 41),
            76,
            Localization.Get("Xóa trạng thái sắp xếp nhưng giữ nguyên thứ tự hàng hiện tại"));
        panel.Controls.AddRange([
            title,
            sortAscending,
            sortDescending,
            reapply,
            clearSort,
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
        void RebuildItems(bool restoreValueFocus)
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

                var activeIndex = navigator.Capture().ActiveIndex;
                values.SelectedIndex = activeIndex >= 0 &&
                                       activeIndex < values.Items.Count
                    ? activeIndex
                    : -1;
            }
            finally
            {
                values.EndUpdate();
                rebuilding = false;
            }

            status.Text = menu.ValuesTruncated
                ? Localization.Format("Đã quét {0:N0} hàng; danh sách giá trị đã bị giới hạn.", menu.ScannedRowCount)
                : Localization.Format("{0:N0} giá trị khác nhau trong {1:N0} hàng.", menu.DistinctValueCount, menu.ScannedRowCount);
            status.AccessibleName = status.Text;
            apply.Enabled = menu.CanApplyValueSelection;
            if (restoreValueFocus)
            {
                BeginInvokeIfHandleCreated(() =>
                    FocusActiveValue(navigator));
            }
        }

        search.TextChanged += (_, _) =>
        {
            menu.SetSearchText(search.Text);
            RebuildItems(restoreValueFocus: false);
        };
        values.SelectedIndexChanged += (_, _) =>
        {
            if (!rebuilding && values.SelectedIndex >= 0)
            {
                navigator.SetActiveIndex(values.SelectedIndex);
            }
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
            navigator.Handle(
                SpreadsheetTableFilterNavigationCommand.SelectAllVisible);
            RebuildItems(restoreValueFocus: false);
        };
        selectNone.Click += (_, _) =>
        {
            navigator.Handle(
                SpreadsheetTableFilterNavigationCommand.ClearVisibleSelection);
            RebuildItems(restoreValueFocus: false);
        };
        sortAscending.Click += (_, _) =>
            SortAndClose(menu, descending: false);
        sortDescending.Click += (_, _) =>
            SortAndClose(menu, descending: true);
        reapply.Click += (_, _) =>
            ReapplyAndClose(menu);
        clearSort.Click += (_, _) =>
            ClearSortAndClose(menu);
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

        KeyEventHandler keyHandler = (_, args) =>
            OnDropDownKeyDown(
                args,
                menu,
                navigator,
                RebuildItems);
        search.KeyDown += keyHandler;
        values.KeyDown += keyHandler;
        selectAll.KeyDown += keyHandler;
        selectNone.KeyDown += keyHandler;
        clear.KeyDown += keyHandler;
        cancel.KeyDown += keyHandler;
        apply.KeyDown += keyHandler;

        RebuildItems(restoreValueFocus: false);
        return new DropDownContent(
            panel,
            search,
            values,
            apply);
    }

    private void SortAndClose(
        SpreadsheetTableFilterMenu menu,
        bool descending)
    {
        var session = _control.Session ??
            throw new InvalidOperationException("A spreadsheet session is required for sorting.");
        var target = ResolveCurrentTarget(session, menu);
        session.Sort.SortAutoFilter(
            target,
            new SpreadsheetFilterSortState([
                new SpreadsheetFilterSortCondition(
                    target.ColumnOffset,
                    descending),
            ]));
        CloseAndRefresh();
    }

    private void ReapplyAndClose(SpreadsheetTableFilterMenu menu)
    {
        var session = _control.Session ??
            throw new InvalidOperationException("A spreadsheet session is required for sorting.");
        session.Sort.ReapplyAutoFilter(ResolveCurrentTarget(session, menu));
        CloseAndRefresh();
    }

    private void ClearSortAndClose(SpreadsheetTableFilterMenu menu)
    {
        var session = _control.Session ??
            throw new InvalidOperationException("A spreadsheet session is required for sorting.");
        session.Sort.ClearAutoFilterSort(ResolveCurrentTarget(session, menu));
        CloseAndRefresh();
    }

    private static SpreadsheetAutoFilterTarget ResolveCurrentTarget(
        SpreadsheetSession session,
        SpreadsheetTableFilterMenu menu)
    {
        if (!session.ActiveWorksheet.TryGetTable(menu.TableId, out var table) ||
            table is null ||
            !table.TryGetColumn(menu.ColumnId, out _))
        {
            throw new InvalidOperationException(
                "The Table filter target no longer exists after a structural edit.");
        }
        var columnOffset = table.GetColumnIndex(menu.ColumnId);
        var header = new CellAddress(
            table.Range.Top,
            table.Range.Left + columnOffset);
        if (!session.TryResolveAutoFilterTarget(header, out var target))
        {
            throw new InvalidOperationException(
                "The Table filter target could not be resolved after a structural edit.");
        }
        return target;
    }

    private void OnDropDownKeyDown(
        KeyEventArgs e,
        SpreadsheetTableFilterMenu menu,
        SpreadsheetTableFilterNavigator navigator,
        Action<bool> rebuildItems)
    {
        var searchFocused = _searchBox?.Focused == true;
        var valuesFocused = _valuesList?.Focused == true;
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            Suppress(e);
            return;
        }

        if (e.Control && e.KeyCode == Keys.A && valuesFocused)
        {
            navigator.Handle(
                e.Shift
                    ? SpreadsheetTableFilterNavigationCommand.ClearVisibleSelection
                    : SpreadsheetTableFilterNavigationCommand.SelectAllVisible);
            rebuildItems(valuesFocused);
            Suppress(e);
            return;
        }

        SpreadsheetTableFilterNavigationCommand command;
        switch (e.KeyCode)
        {
            case Keys.Down when searchFocused:
                command = SpreadsheetTableFilterNavigationCommand.MoveFirst;
                break;
            case Keys.Up when searchFocused:
                command = SpreadsheetTableFilterNavigationCommand.MoveLast;
                break;
            case Keys.Down when valuesFocused:
                command = SpreadsheetTableFilterNavigationCommand.MoveNext;
                break;
            case Keys.Up when valuesFocused:
                command = SpreadsheetTableFilterNavigationCommand.MovePrevious;
                break;
            case Keys.Home when valuesFocused:
                command = SpreadsheetTableFilterNavigationCommand.MoveFirst;
                break;
            case Keys.End when valuesFocused:
                command = SpreadsheetTableFilterNavigationCommand.MoveLast;
                break;
            case Keys.PageUp when valuesFocused:
                command = SpreadsheetTableFilterNavigationCommand.PagePrevious;
                break;
            case Keys.PageDown when valuesFocused:
                command = SpreadsheetTableFilterNavigationCommand.PageNext;
                break;
            case Keys.Space when valuesFocused:
            case Keys.Enter when valuesFocused:
                navigator.Handle(
                    SpreadsheetTableFilterNavigationCommand.ToggleCurrent);
                rebuildItems(true);
                Suppress(e);
                return;
            case Keys.Enter when searchFocused:
                if (menu.CanApplyValueSelection)
                {
                    menu.ApplyValueSelection();
                    CloseAndRefresh();
                }
                Suppress(e);
                return;
            default:
                return;
        }

        navigator.Handle(command);
        FocusActiveValue(navigator);
        Suppress(e);
    }

    private bool FocusActiveValue(
        SpreadsheetTableFilterNavigator navigator)
    {
        if (_valuesList is null)
        {
            return false;
        }

        var index = navigator.Capture().ActiveIndex;
        if (index < 0 || index >= _valuesList.Items.Count)
        {
            return false;
        }

        _valuesList.SelectedIndex = index;
        return _valuesList.Focus();
    }

    private void FocusSearchBox(
        ToolStripDropDown dropDown,
        TextBox search)
    {
        BeginInvokeIfHandleCreated(() =>
        {
            if (!ReferenceEquals(_dropDown, dropDown) ||
                !dropDown.Visible)
            {
                return;
            }

            search.Focus();
            search.SelectAll();
        });
    }

    private void RestoreFocus()
    {
        var target = _focusBeforeOpen;
        _focusBeforeOpen = null;
        BeginInvokeIfHandleCreated(() =>
        {
            if (target is
                {
                    IsDisposed: false,
                    Visible: true,
                    Enabled: true,
                    CanFocus: true,
                })
            {
                target.Focus();
                return;
            }

            if (!_control.IsDisposed && _control.CanFocus)
            {
                _control.Focus();
            }
        });
    }

    private static void Suppress(KeyEventArgs e)
    {
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private static Button CreateCommandButton(
        string text,
        Point location,
        int width,
        string accessibleDescription) =>
        new()
        {
            Text = text,
            Location = location,
            Size = new Size(width, 28),
            FlatStyle = FlatStyle.System,
            AccessibleName = text,
            AccessibleDescription = accessibleDescription,
            AccessibleRole = AccessibleRole.PushButton,
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

    private void OnControlPreviewKeyDown(
        object? sender,
        PreviewKeyDownEventArgs e)
    {
        if (e.Alt && e.KeyCode == Keys.Down)
        {
            e.IsInputKey = true;
        }
    }

    private void OnControlKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsOpen &&
            e.Alt &&
            e.KeyCode == Keys.Down &&
            TryOpenForActiveCell())
        {
            Suppress(e);
        }
    }

    private void OnControlDisposed(object? sender, EventArgs e) => Dispose();

    private static Rectangle ToRectangle(
        NeraSpreadSheet.Foundation.RectD bounds) =>
        Rectangle.FromLTRB(
            checked((int)Math.Round(bounds.Left)),
            checked((int)Math.Round(bounds.Top)),
            checked((int)Math.Round(bounds.Right)),
            checked((int)Math.Round(bounds.Bottom)));

    private static Color ToColor(
        NeraSpreadSheet.Foundation.ColorRgba color) =>
        Color.FromArgb(
            color.Alpha,
            color.Red,
            color.Green,
            color.Blue);

    private string DisplayValue(CellValue value) =>
        value.IsBlank ? Localization.Get("(Trống)") : value.ToString();

    private sealed record DropDownContent(
        Panel Panel,
        TextBox Search,
        CheckedListBox Values,
        Button Apply);

    private sealed record FilterListItem(
        CellValue Value,
        string DisplayText,
        int Count)
    {
        public override string ToString() =>
            $"{DisplayText}  ({Count})";
    }
}
