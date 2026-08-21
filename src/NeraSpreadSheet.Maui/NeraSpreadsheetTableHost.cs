using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Responsive MAUI host that layers native Table filter buttons and a bottom-sheet
/// presenter over the single GPU-backed <see cref="NeraSpreadsheetView"/>.
/// </summary>
public sealed partial class NeraSpreadsheetTableHost : Grid, IDisposable
{
    public static readonly BindableProperty WorkbookProperty =
        BindableProperty.Create(
            nameof(Workbook),
            typeof(Workbook),
            typeof(NeraSpreadsheetTableHost),
            default(Workbook),
            propertyChanged: OnWorkbookChanged);

    private readonly AbsoluteLayout _buttonLayer;
    private readonly Grid _sheetOverlay;
    private readonly VerticalStackLayout _sheetPanel;
    private readonly Entry _search;
    private readonly Label _status;
    private readonly VerticalStackLayout _itemsPanel;
    private readonly Button _apply;
    private readonly List<CheckBox> _valueCheckBoxes = [];
    private readonly Dictionary<(Guid TableId, Guid ColumnId), Button>
        _buttons = [];
    private SpreadsheetSession? _session;
    private Worksheet? _subscribedWorksheet;
    private SpreadsheetViewportEngine? _viewport;
    private SpreadsheetTableFilterMenu? _menu;
    private SpreadsheetTableFilterNavigator? _navigator;
    private VisualElement? _focusReturnTarget;
    private bool _disposed;

    public NeraSpreadsheetTableHost()
    {
        AutomationId = "NeraSpreadsheetTableHost";
        SemanticProperties.SetDescription(
            this,
            "Bảng tính Nera có bộ lọc Table tương tác.");
        SemanticProperties.SetHint(
            this,
            "Trên Windows, nhấn Alt và mũi tên xuống để mở bộ lọc của cột Table đang chọn.");

        Spreadsheet = new NeraSpreadsheetView
        {
            AutomationId = "NeraSpreadsheetTableSurface",
        };
        _buttonLayer = new AbsoluteLayout
        {
            AutomationId = "NeraTableFilterButtonLayer",
            InputTransparent = true,
            CascadeInputTransparent = false,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        (_sheetOverlay,
            _sheetPanel,
            _search,
            _status,
            _itemsPanel,
            _apply) = CreateSheet();

        Children.Add(Spreadsheet);
        Children.Add(_buttonLayer);
        Children.Add(_sheetOverlay);

        Spreadsheet.SizeChanged += OnSpreadsheetVisualChanged;
        Spreadsheet.ScrollChanged += OnSpreadsheetVisualChanged;
        Spreadsheet.ZoomChanged += OnSpreadsheetVisualChanged;
        Loaded += OnHostLoaded;
        Unloaded += OnHostUnloaded;
        HandlerChanged += OnHostHandlerChanged;
    }

    public Workbook? Workbook
    {
        get => (Workbook?)GetValue(WorkbookProperty);
        set => SetValue(WorkbookProperty, value);
    }

    public NeraSpreadsheetView Spreadsheet { get; }

    public SpreadsheetSession? Session => Spreadsheet.Session;

    public SpreadsheetRenderTheme RenderTheme
    {
        get => Spreadsheet.RenderTheme;
        set
        {
            Spreadsheet.RenderTheme = value;
            RefreshFilterButtons();
        }
    }

    public bool IsFilterSheetOpen => _sheetOverlay.IsVisible;

    public void RefreshFilterButtons()
    {
        if (_disposed)
        {
            return;
        }

        AttachSession();
        UpdateButtons();
    }

    public bool TryOpenFilter(Guid tableId, Guid columnId) =>
        TryOpenFilterCore(
            tableId,
            columnId,
            focusReturnTarget: null);

    public bool TryOpenForActiveCell()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AttachSession();
        if (_session is null ||
            !_session.TryResolveActiveTableFilterTarget(out var target))
        {
            return false;
        }

        var key = (target.TableId, target.ColumnId);
        var focusTarget = _buttons.TryGetValue(key, out var button) &&
                          button.IsVisible
            ? button
            : Spreadsheet;
        return TryOpenFilterCore(
            target.TableId,
            target.ColumnId,
            focusTarget);
    }

    public void CloseFilterSheet() =>
        CloseFilterSheetCore(restoreFocus: true);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DetachPlatformKeyboard();
        CloseFilterSheetCore(restoreFocus: false);
        DetachSession();
        Spreadsheet.SizeChanged -= OnSpreadsheetVisualChanged;
        Spreadsheet.ScrollChanged -= OnSpreadsheetVisualChanged;
        Spreadsheet.ZoomChanged -= OnSpreadsheetVisualChanged;
        Loaded -= OnHostLoaded;
        Unloaded -= OnHostUnloaded;
        HandlerChanged -= OnHostHandlerChanged;
        foreach (var button in _buttons.Values)
        {
            button.Clicked -= OnFilterButtonClicked;
        }
        _buttons.Clear();
        _buttonLayer.Children.Clear();
        Spreadsheet.Dispose();
        _disposed = true;
    }

    private bool TryOpenFilterCore(
        Guid tableId,
        Guid columnId,
        VisualElement? focusReturnTarget)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AttachSession();
        if (_session is null ||
            !_session.ActiveWorksheet.TryGetTable(tableId, out var table) ||
            table is null ||
            !table.TryGetColumn(columnId, out _))
        {
            return false;
        }

        CloseFilterSheetCore(restoreFocus: false);
        _focusReturnTarget = focusReturnTarget ?? Spreadsheet;
        _menu = new SpreadsheetTablePresenterController(_session)
            .OpenFilterMenu(tableId, columnId);
        _navigator = new SpreadsheetTableFilterNavigator(_menu);
        _search.Text = string.Empty;
        RebuildSheetItems(focusActiveValue: false);
        _sheetOverlay.IsVisible = true;
        SemanticProperties.SetDescription(
            _sheetPanel,
            $"Lọc {_menu.ColumnName} trong Table {_menu.TableName}");
        SemanticProperties.SetHint(
            _sheetPanel,
            "Tìm kiếm hoặc chọn giá trị. Escape đóng, Enter áp dụng, các phím mũi tên duyệt danh sách trên Windows.");
        Dispatcher.Dispatch(FocusSearchEntry);
        return true;
    }

    private void CloseFilterSheetCore(bool restoreFocus)
    {
        var focusTarget = _focusReturnTarget;
        _focusReturnTarget = null;
        _search.Unfocus();
        _sheetOverlay.IsVisible = false;
        _navigator?.Dispose();
        _navigator = null;
        _menu = null;
        _valueCheckBoxes.Clear();
        _itemsPanel.Children.Clear();
        _apply.IsEnabled = false;
        if (restoreFocus && !_disposed)
        {
            Dispatcher.Dispatch(() => RestoreFocus(focusTarget));
        }
    }

    private static void OnWorkbookChanged(
        BindableObject bindable,
        object? oldValue,
        object? newValue)
    {
        if (bindable is not NeraSpreadsheetTableHost host)
        {
            return;
        }

        host.CloseFilterSheetCore(restoreFocus: false);
        host.Spreadsheet.Workbook = (Workbook?)newValue;
        host.AttachSession(force: true);
        host.UpdateButtons();
    }

    private void AttachSession(bool force = false)
    {
        var session = Spreadsheet.Session;
        if (!force && ReferenceEquals(_session, session))
        {
            EnsureWorksheetSubscription();
            return;
        }

        DetachSession();
        _session = session;
        _viewport = session is null
            ? null
            : new SpreadsheetViewportEngine(session);
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
            EnsureWorksheetSubscription();
        }
    }

    private void DetachSession()
    {
        DetachWorksheetSubscription();
        if (_session is not null)
        {
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
        }
        _session = null;
        _viewport = null;
    }

    private void EnsureWorksheetSubscription()
    {
        var worksheet = _session?.ActiveWorksheet;
        if (ReferenceEquals(worksheet, _subscribedWorksheet))
        {
            return;
        }

        DetachWorksheetSubscription();
        _subscribedWorksheet = worksheet;
        if (_subscribedWorksheet is not null)
        {
            _subscribedWorksheet.CellsChanged += OnWorksheetChanged;
            _subscribedWorksheet.Dimensions.Changed += OnWorksheetChanged;
        }
    }

    private void DetachWorksheetSubscription()
    {
        if (_subscribedWorksheet is null)
        {
            return;
        }

        _subscribedWorksheet.CellsChanged -= OnWorksheetChanged;
        _subscribedWorksheet.Dimensions.Changed -= OnWorksheetChanged;
        _subscribedWorksheet = null;
    }

    private void UpdateButtons()
    {
        if (_disposed ||
            _session is null ||
            _viewport is null ||
            Spreadsheet.Width <= 0d ||
            Spreadsheet.Height <= 0d ||
            !Spreadsheet.RenderTheme.ShowTableFilterButtons)
        {
            HideAllButtons();
            return;
        }

        EnsureWorksheetSubscription();
        var zoom = Spreadsheet.Zoom;
        var fullWidth = Spreadsheet.Width / zoom;
        var fullHeight = Spreadsheet.Height / zoom;
        var chrome = SpreadsheetChromeGeometry.Calculate(
            fullWidth,
            fullHeight,
            Spreadsheet.RenderTheme);
        if (chrome.BodyWidth <= 0d || chrome.BodyHeight <= 0d)
        {
            HideAllButtons();
            return;
        }

        var scroll = Spreadsheet.ScrollSnapshot;
        var frame = _viewport.Compose(
            scroll.OffsetX,
            scroll.OffsetY,
            chrome.BodyWidth,
            chrome.BodyHeight,
            overscan: 0d,
            Spreadsheet.RenderTheme);
        var hits = SpreadsheetTableFilterButtonGeometry.GetVisibleButtons(
            WorksheetSnapshot.Capture(_session.ActiveWorksheet),
            frame.Layout,
            Spreadsheet.RenderTheme);
        var visible = new HashSet<(Guid, Guid)>();
        foreach (var hit in hits)
        {
            var key = (hit.TableId, hit.ColumnId);
            visible.Add(key);
            if (!_buttons.TryGetValue(key, out var button))
            {
                button = CreateFilterButton();
                _buttons.Add(key, button);
                _buttonLayer.Children.Add(button);
            }

            var fullBounds = hit.Bounds.Translate(
                chrome.RowHeaderWidth,
                chrome.ColumnHeaderHeight);
            var scaled = new Rect(
                fullBounds.X * zoom,
                fullBounds.Y * zoom,
                fullBounds.Width * zoom,
                fullBounds.Height * zoom);
            button.CommandParameter = hit;
            button.BackgroundColor = ToColor(
                hit.IsFiltered
                    ? Spreadsheet.RenderTheme.TableFilterButtonActiveBackground
                    : Spreadsheet.RenderTheme.TableFilterButtonBackground);
            button.TextColor = ToColor(
                Spreadsheet.RenderTheme.TableFilterButtonGlyph);
            button.BorderColor = ToColor(
                Spreadsheet.RenderTheme.TableFilterButtonBorder);
            button.AutomationId =
                $"NeraTableFilter_{hit.TableId:N}_{hit.ColumnId:N}";
            SetFilterButtonSemantics(button, hit);
            AbsoluteLayout.SetLayoutBounds(button, scaled);
            AbsoluteLayout.SetLayoutFlags(
                button,
                AbsoluteLayoutFlags.None);
            button.IsVisible = true;
        }

        foreach (var (key, button) in _buttons)
        {
            if (!visible.Contains(key))
            {
                button.IsVisible = false;
            }
        }
    }

    private void SetFilterButtonSemantics(
        Button button,
        SpreadsheetTableFilterButtonHit hit)
    {
        var description = "Mở bộ lọc Table";
        if (_session?.ActiveWorksheet.TryGetTable(
                hit.TableId,
                out var table) == true &&
            table is not null &&
            table.TryGetColumn(hit.ColumnId, out var column) &&
            column is not null)
        {
            description = $"Lọc cột {column.Name} trong Table {table.Name}";
        }

        SemanticProperties.SetDescription(button, description);
        SemanticProperties.SetHint(
            button,
            "Chạm hoặc nhấn Enter để mở. Trên Windows cũng có thể dùng Alt và mũi tên xuống từ ô đang chọn.");
    }

    private Button CreateFilterButton()
    {
        var button = new Button
        {
            Text = "▼",
            FontSize = 8d,
            Padding = new Thickness(0d),
            Margin = new Thickness(0d),
            CornerRadius = 2,
            BorderWidth = 1d,
            InputTransparent = false,
        };
        button.Clicked += OnFilterButtonClicked;
        return button;
    }

    private void OnFilterButtonClicked(object? sender, EventArgs e)
    {
        if (sender is Button
            {
                CommandParameter: SpreadsheetTableFilterButtonHit hit,
            } button)
        {
            TryOpenFilterCore(
                hit.TableId,
                hit.ColumnId,
                button);
        }
    }

    private (
        Grid Overlay,
        VerticalStackLayout Panel,
        Entry Search,
        Label Status,
        VerticalStackLayout Items,
        Button Apply) CreateSheet()
    {
        var overlay = new Grid
        {
            AutomationId = "NeraTableFilterOverlay",
            IsVisible = false,
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        SemanticProperties.SetDescription(
            overlay,
            "Lớp phủ bộ lọc Table");
        var backdrop = new BoxView
        {
            AutomationId = "NeraTableFilterBackdrop",
            Color = Color.FromRgba(0, 0, 0, 96),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        SemanticProperties.SetDescription(
            backdrop,
            "Đóng bộ lọc");
        SemanticProperties.SetHint(
            backdrop,
            "Chạm bên ngoài bảng lọc để đóng.");
        var backdropTap = new TapGestureRecognizer();
        backdropTap.Tapped += (_, _) => CloseFilterSheet();
        backdrop.GestureRecognizers.Add(backdropTap);
        overlay.Children.Add(backdrop);

        var panel = new VerticalStackLayout
        {
            AutomationId = "NeraTableFilterSheet",
            BackgroundColor = Colors.White,
            Padding = new Thickness(16d, 14d, 16d, 18d),
            Spacing = 8d,
            VerticalOptions = LayoutOptions.End,
            MaximumHeightRequest = 520d,
        };
        overlay.Children.Add(panel);

        var title = new Label
        {
            AutomationId = "NeraTableFilterTitle",
            Text = "Lọc Table",
            FontSize = 18d,
            FontAttributes = FontAttributes.Bold,
        };
        SemanticProperties.SetHeadingLevel(
            title,
            SemanticHeadingLevel.Level2);
        panel.Children.Add(title);

        var search = new Entry
        {
            AutomationId = "NeraTableFilterSearch",
            Placeholder = "Tìm giá trị",
            ReturnType = ReturnType.Done,
        };
        SemanticProperties.SetDescription(
            search,
            "Tìm giá trị lọc");
        SemanticProperties.SetHint(
            search,
            "Nhập nội dung tìm kiếm. Nhấn Enter để áp dụng nếu lựa chọn hợp lệ.");
        panel.Children.Add(search);

        var commands = new HorizontalStackLayout
        {
            AutomationId = "NeraTableFilterSelectionCommands",
            Spacing = 8d,
        };
        var selectAll = CreateSheetButton(
            "Chọn tất cả",
            "NeraTableFilterSelectAll",
            "Chọn mọi giá trị đang hiển thị");
        var selectNone = CreateSheetButton(
            "Bỏ chọn",
            "NeraTableFilterSelectNone",
            "Bỏ chọn mọi giá trị đang hiển thị");
        selectAll.HorizontalOptions = LayoutOptions.Fill;
        selectNone.HorizontalOptions = LayoutOptions.Fill;
        commands.Children.Add(selectAll);
        commands.Children.Add(selectNone);
        panel.Children.Add(commands);

        var status = new Label
        {
            AutomationId = "NeraTableFilterStatus",
            FontSize = 12d,
            TextColor = Colors.Gray,
        };
        panel.Children.Add(status);

        var items = new VerticalStackLayout
        {
            AutomationId = "NeraTableFilterValues",
            Spacing = 2d,
        };
        SemanticProperties.SetDescription(
            items,
            "Danh sách giá trị lọc");
        SemanticProperties.SetHint(
            items,
            "Chọn hoặc bỏ chọn các giá trị cần hiển thị.");
        panel.Children.Add(new ScrollView
        {
            AutomationId = "NeraTableFilterValuesScroll",
            Content = items,
            MaximumHeightRequest = 280d,
        });

        var footer = new HorizontalStackLayout
        {
            AutomationId = "NeraTableFilterFooter",
            Spacing = 8d,
            HorizontalOptions = LayoutOptions.End,
        };
        var clear = CreateSheetButton(
            "Xóa lọc",
            "NeraTableFilterClear",
            "Xóa bộ lọc của cột hiện tại");
        var cancel = CreateSheetButton(
            "Hủy",
            "NeraTableFilterCancel",
            "Đóng mà không áp dụng thay đổi");
        var apply = CreateSheetButton(
            "Áp dụng",
            "NeraTableFilterApply",
            "Áp dụng các giá trị đã chọn");
        footer.Children.Add(clear);
        footer.Children.Add(cancel);
        footer.Children.Add(apply);
        panel.Children.Add(footer);

        search.TextChanged += (_, args) =>
        {
            if (_menu is null)
            {
                return;
            }
            _menu.SetSearchText(args.NewTextValue);
            RebuildSheetItems(focusActiveValue: false);
        };
        search.Completed += (_, _) =>
            ApplyCurrentFilterAndClose();
        selectAll.Clicked += (_, _) =>
        {
            _navigator?.Handle(
                SpreadsheetTableFilterNavigationCommand.SelectAllVisible);
            RebuildSheetItems(focusActiveValue: false);
        };
        selectNone.Clicked += (_, _) =>
        {
            _navigator?.Handle(
                SpreadsheetTableFilterNavigationCommand.ClearVisibleSelection);
            RebuildSheetItems(focusActiveValue: false);
        };
        clear.Clicked += (_, _) =>
            ClearCurrentFilterAndClose();
        cancel.Clicked += (_, _) => CloseFilterSheet();
        apply.Clicked += (_, _) =>
            ApplyCurrentFilterAndClose();

        return (overlay, panel, search, status, items, apply);
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
        };
        SemanticProperties.SetDescription(button, text);
        SemanticProperties.SetHint(button, hint);
        return button;
    }

    private void RebuildSheetItems(bool focusActiveValue)
    {
        _valueCheckBoxes.Clear();
        _itemsPanel.Children.Clear();
        if (_menu is null || _navigator is null)
        {
            _apply.IsEnabled = false;
            return;
        }

        var items = _menu.GetVisibleItems();
        for (var index = 0; index < items.Count; index++)
        {
            var item = items[index];
            var value = item.Value;
            var displayText = DisplayValue(value);
            var checkBox = new CheckBox
            {
                AutomationId = $"NeraTableFilterValue_{index}",
                IsChecked = item.IsSelected,
                VerticalOptions = LayoutOptions.Center,
            };
            SemanticProperties.SetDescription(
                checkBox,
                $"{displayText}; {item.Count:N0} dòng");
            SemanticProperties.SetHint(
                checkBox,
                "Chọn hoặc bỏ chọn giá trị này.");
            checkBox.Focused += (_, _) =>
                _navigator?.SetActiveValue(value);
            checkBox.CheckedChanged += (_, args) =>
            {
                _menu?.SelectValue(value, args.Value);
                if (_menu is not null)
                {
                    _apply.IsEnabled =
                        _menu.CanApplyValueSelection;
                }
            };
            var row = new HorizontalStackLayout
            {
                AutomationId = $"NeraTableFilterValueRow_{index}",
                Spacing = 8d,
            };
            row.Children.Add(checkBox);
            row.Children.Add(new Label
            {
                Text = $"{displayText}  ({item.Count})",
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                HorizontalOptions = LayoutOptions.Fill,
                InputTransparent = true,
            });
            _valueCheckBoxes.Add(checkBox);
            _itemsPanel.Children.Add(row);
        }

        _status.Text = _menu.ValuesTruncated
            ? $"Đã quét {_menu.ScannedRowCount:N0} hàng; danh sách giá trị đã bị giới hạn."
            : $"{_menu.DistinctValueCount:N0} giá trị khác nhau trong {_menu.ScannedRowCount:N0} hàng.";
        SemanticProperties.SetDescription(_status, _status.Text);
        _apply.IsEnabled = _menu.CanApplyValueSelection;
        SemanticProperties.SetDescription(
            _sheetPanel,
            $"Lọc {_menu.ColumnName} trong Table {_menu.TableName}");
        if (focusActiveValue)
        {
            Dispatcher.Dispatch(FocusActiveValue);
        }
    }

    private bool HandleFilterNavigation(
        SpreadsheetTableFilterNavigationCommand command)
    {
        if (_navigator is null)
        {
            return false;
        }

        var handled = _navigator.Handle(command);
        switch (command)
        {
            case SpreadsheetTableFilterNavigationCommand.ToggleCurrent:
                RebuildSheetItems(focusActiveValue: true);
                break;
            case SpreadsheetTableFilterNavigationCommand.SelectAllVisible:
            case SpreadsheetTableFilterNavigationCommand.ClearVisibleSelection:
                RebuildSheetItems(focusActiveValue: IsValueListFocused());
                break;
            case SpreadsheetTableFilterNavigationCommand.MovePrevious:
            case SpreadsheetTableFilterNavigationCommand.MoveNext:
            case SpreadsheetTableFilterNavigationCommand.MoveFirst:
            case SpreadsheetTableFilterNavigationCommand.MoveLast:
            case SpreadsheetTableFilterNavigationCommand.PagePrevious:
            case SpreadsheetTableFilterNavigationCommand.PageNext:
                FocusActiveValue();
                break;
        }
        return handled;
    }

    private bool ApplyCurrentFilterAndClose()
    {
        if (_menu is null || !_menu.CanApplyValueSelection)
        {
            return false;
        }

        _menu.ApplyValueSelection();
        CloseFilterSheetCore(restoreFocus: true);
        RefreshAfterFilterMutation();
        return true;
    }

    private bool ClearCurrentFilterAndClose()
    {
        if (_menu is null)
        {
            return false;
        }

        _menu.ClearColumnFilter();
        CloseFilterSheetCore(restoreFocus: true);
        RefreshAfterFilterMutation();
        return true;
    }

    private void RefreshAfterFilterMutation()
    {
        _viewport?.InvalidateMetrics();
        Spreadsheet.InvalidateSurface();
        UpdateButtons();
    }

    private bool FocusSearchEntry()
    {
        if (!_sheetOverlay.IsVisible)
        {
            return false;
        }

        var focused = _search.Focus();
        if (focused)
        {
            _search.CursorPosition = 0;
            _search.SelectionLength = _search.Text?.Length ?? 0;
        }
        return focused;
    }

    private bool FocusActiveValue()
    {
        var index = _navigator?.Capture().ActiveIndex ?? -1;
        if (index < 0 || index >= _valueCheckBoxes.Count)
        {
            return false;
        }

        return _valueCheckBoxes[index].Focus();
    }

    private bool IsSearchFocused() =>
        _search.IsFocused;

    private bool IsValueListFocused() =>
        _valueCheckBoxes.Any(static item => item.IsFocused);

    private void RestoreFocus(VisualElement? target)
    {
        if (target is
            {
                IsVisible: true,
                IsEnabled: true,
            } &&
            target.Focus())
        {
            return;
        }

        Spreadsheet.Focus();
    }

    private void HideAllButtons()
    {
        foreach (var button in _buttons.Values)
        {
            button.IsVisible = false;
        }
    }

    private void OnActiveWorksheetChanged(object? sender, EventArgs e)
    {
        CloseFilterSheetCore(restoreFocus: false);
        EnsureWorksheetSubscription();
        _viewport?.InvalidateMetrics();
        UpdateButtons();
    }

    private void OnWorksheetChanged(object? sender, EventArgs e)
    {
        _viewport?.InvalidateMetrics();
        UpdateButtons();
    }

    private void OnSpreadsheetVisualChanged(object? sender, EventArgs e) =>
        UpdateButtons();

    private void OnHostLoaded(object? sender, EventArgs e) =>
        AttachPlatformKeyboard();

    private void OnHostUnloaded(object? sender, EventArgs e)
    {
        DetachPlatformKeyboard();
        CloseFilterSheetCore(restoreFocus: false);
    }

    private void OnHostHandlerChanged(object? sender, EventArgs e)
    {
        DetachPlatformKeyboard();
        AttachPlatformKeyboard();
    }

    partial void AttachPlatformKeyboard();

    partial void DetachPlatformKeyboard();

    private static string DisplayValue(CellValue value) =>
        value.IsBlank ? "(Trống)" : value.ToString();

    private static Color ToColor(
        NeraSpreadSheet.Foundation.ColorRgba color) =>
        Color.FromRgba(
            color.Red,
            color.Green,
            color.Blue,
            color.Alpha);
}
