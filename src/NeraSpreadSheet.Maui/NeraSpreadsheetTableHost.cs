using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Iconography;
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

    private readonly NeraMauiFilterResources _shellResources;

    /// <summary>Gets the resources scoped to this filter host.</summary>
    public PresentationLocalization Localization { get; private set; } = PresentationLocalization.Default;

    /// <summary>Gets the palette scoped to the filter sheet.</summary>
    public NeraIconTheme IconTheme { get; private set; }

    public NeraSpreadsheetTableHost() : this(PresentationLocalization.Default, NeraIconTheme.Light)
    {
    }

    /// <summary>Creates the existing Table host with scoped filter resources and palette.</summary>
    public NeraSpreadsheetTableHost(PresentationLocalization localization, NeraIconTheme iconTheme)
    {
        AutomationId = "NeraSpreadsheetTableHost";
        SemanticProperties.SetDescription(
            this,
            Localization.Get("Bảng tính Nera có bộ lọc Table tương tác."));
        SemanticProperties.SetHint(
            this,
            Localization.Get("Trên Windows, nhấn Alt và mũi tên xuống để mở bộ lọc của cột Table đang chọn."));

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
        _shellResources = new NeraMauiFilterResources(_sheetOverlay);
        SetPresentation(localization, iconTheme);

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
        VisualElement focusTarget =
            _buttons.TryGetValue(key, out var button) &&
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

    /// <summary>Sorts the open Table filter column and closes the sheet.</summary>
    public Task<bool> ApplyColumnSortAsync(
        bool descending,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(SortAndClose(descending));
    }

    /// <summary>Reapplies the current Table sort after resolving stable identities.</summary>
    public Task<bool> ReapplyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = RequireSessionForSort();
        var target = ResolveCurrentTarget(session);
        var changed = session.Sort.ReapplyAutoFilter(target);
        CloseFilterSheetCore(restoreFocus: true);
        RefreshAfterFilterMutation();
        return Task.FromResult(changed);
    }

    /// <summary>Clears sort metadata while preserving the current physical row order.</summary>
    public Task<bool> ClearSortAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var session = RequireSessionForSort();
        var target = ResolveCurrentTarget(session);
        var changed = session.Sort.ClearAutoFilterSort(target);
        CloseFilterSheetCore(restoreFocus: true);
        RefreshAfterFilterMutation();
        return Task.FromResult(changed);
    }

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
            Localization.Format("Lọc {0} trong Table {1}", _menu.ColumnName, _menu.TableName));
        SemanticProperties.SetHint(
            _sheetPanel,
            Localization.Get("Tìm kiếm hoặc chọn giá trị. Escape đóng, Enter áp dụng, các phím mũi tên duyệt danh sách trên Windows."));
        Dispatcher.Dispatch(() =>
        {
            FocusSearchEntry();
        });
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
            var table = _session.ActiveWorksheet.Tables.First(candidate =>
                candidate.Id == hit.TableId);
            var visual = SpreadsheetTableStyleVisuals.ResolveFilterButton(
                _session.Workbook,
                table,
                Spreadsheet.RenderTheme);
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
            button.Text = NeraMauiFilterHeaderGlyphs.Get(
                hit.HeaderState,
                hit.SortDescending);
            button.BackgroundColor = ToColor(
                hit.IsFiltered
                    ? visual.ActiveBackground
                    : visual.Background);
            button.TextColor = ToColor(
                visual.Glyph);
            button.BorderColor = ToColor(
                visual.Border);
            var automationId =
                $"NeraTableFilter_{hit.TableId:N}_{hit.ColumnId:N}";
            if (string.IsNullOrEmpty(button.AutomationId))
            {
                button.AutomationId = automationId;
            }
            else if (!string.Equals(
                         button.AutomationId,
                         automationId,
                         StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "A Table filter button changed its stable automation identity.");
            }
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
        var description = Localization.Get("Mở bộ lọc Table");
        if (_session?.ActiveWorksheet.TryGetTable(
                hit.TableId,
                out var table) == true &&
            table is not null &&
            table.TryGetColumn(hit.ColumnId, out var column) &&
            column is not null)
        {
            var state = hit.HeaderState switch
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
            description = Localization.Format("Cột {0} trong Table {1}, {2}", column.Name, table.Name, state);
        }

        SemanticProperties.SetDescription(button, description);
        SemanticProperties.SetHint(
            button,
            Localization.Get("Chạm hoặc nhấn Enter để mở. Trên Windows cũng có thể dùng Alt và mũi tên xuống từ ô đang chọn."));
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
            Localization.Get("Lớp phủ bộ lọc Table"));
        var backdrop = new BoxView
        {
            AutomationId = "NeraTableFilterBackdrop",
            Color = Color.FromRgba(0, 0, 0, 96),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        SemanticProperties.SetDescription(
            backdrop,
            Localization.Get("Đóng bộ lọc"));
        SemanticProperties.SetHint(
            backdrop,
            Localization.Get("Chạm bên ngoài bảng lọc để đóng."));
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
            Text = Localization.Get("Lọc Table"),
            FontSize = 18d,
            FontAttributes = FontAttributes.Bold,
        };
        SemanticProperties.SetHeadingLevel(
            title,
            SemanticHeadingLevel.Level2);
        panel.Children.Add(title);

        var sortCommands = new HorizontalStackLayout
        {
            AutomationId = "NeraTableFilterSortCommands",
            Spacing = 6d,
        };
        var sortAscending = CreateSheetButton(
            Localization.Get("Sắp ↑"),
            "NeraTableFilterSortAscending",
            Localization.Get("Sắp xếp Table tăng dần theo cột này"));
        var sortDescending = CreateSheetButton(
            Localization.Get("Sắp ↓"),
            "NeraTableFilterSortDescending",
            Localization.Get("Sắp xếp Table giảm dần theo cột này"));
        var reapply = CreateSheetButton(
            Localization.Get("Áp dụng lại"),
            "NeraTableFilterReapply",
            Localization.Get("Áp dụng lại thứ tự sắp xếp hiện tại"));
        var clearSort = CreateSheetButton(
            Localization.Get("Xóa SX"),
            "NeraTableFilterClearSort",
            Localization.Get("Xóa trạng thái sắp xếp nhưng giữ nguyên thứ tự hàng hiện tại"));
        sortCommands.Children.Add(sortAscending);
        sortCommands.Children.Add(sortDescending);
        sortCommands.Children.Add(reapply);
        sortCommands.Children.Add(clearSort);
        panel.Children.Add(sortCommands);

        var search = new Entry
        {
            AutomationId = "NeraTableFilterSearch",
            Placeholder = Localization.Get("Tìm giá trị"),
            ReturnType = ReturnType.Done,
        };
        SemanticProperties.SetDescription(
            search,
            Localization.Get("Tìm giá trị lọc"));
        SemanticProperties.SetHint(
            search,
            Localization.Get("Nhập nội dung tìm kiếm. Nhấn Enter để áp dụng nếu lựa chọn hợp lệ."));
        panel.Children.Add(search);

        var commands = new HorizontalStackLayout
        {
            AutomationId = "NeraTableFilterSelectionCommands",
            Spacing = 8d,
        };
        var selectAll = CreateSheetButton(
            Localization.Get("Chọn tất cả"),
            "NeraTableFilterSelectAll",
            Localization.Get("Chọn mọi giá trị đang hiển thị"));
        var selectNone = CreateSheetButton(
            Localization.Get("Bỏ chọn"),
            "NeraTableFilterSelectNone",
            Localization.Get("Bỏ chọn mọi giá trị đang hiển thị"));
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
            Localization.Get("Danh sách giá trị lọc"));
        SemanticProperties.SetHint(
            items,
            Localization.Get("Chọn hoặc bỏ chọn các giá trị cần hiển thị."));
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
            Localization.Get("Xóa lọc"),
            "NeraTableFilterClear",
            Localization.Get("Xóa bộ lọc của cột hiện tại"));
        var cancel = CreateSheetButton(
            Localization.Get("Hủy"),
            "NeraTableFilterCancel",
            Localization.Get("Đóng mà không áp dụng thay đổi"));
        var apply = CreateSheetButton(
            Localization.Get("Áp dụng"),
            "NeraTableFilterApply",
            Localization.Get("Áp dụng các giá trị đã chọn"));
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
        sortAscending.Clicked += (_, _) => SortAndClose(descending: false);
        sortDescending.Clicked += (_, _) => SortAndClose(descending: true);
        reapply.Clicked += (_, _) => ReapplyAsync();
        clearSort.Clicked += (_, _) => ClearSortAsync();
        clear.Clicked += (_, _) =>
            ClearCurrentFilterAndClose();
        cancel.Clicked += (_, _) => CloseFilterSheet();
        apply.Clicked += (_, _) =>
            ApplyCurrentFilterAndClose();

        return (overlay, panel, search, status, items, apply);
    }

    private bool SortAndClose(bool descending)
    {
        var session = RequireSessionForSort();
        var target = ResolveCurrentTarget(session);
        var changed = session.Sort.SortAutoFilter(
            target,
            new SpreadsheetFilterSortState([
                new SpreadsheetFilterSortCondition(
                    target.ColumnOffset,
                    descending),
            ]));
        CloseFilterSheetCore(restoreFocus: true);
        RefreshAfterFilterMutation();
        return changed;
    }

    private SpreadsheetSession RequireSessionForSort()
    {
        if (_session is null || _menu is null)
        {
            throw new InvalidOperationException(
                "Open a Table filter sheet before changing sort state.");
        }
        return _session;
    }

    private SpreadsheetAutoFilterTarget ResolveCurrentTarget(
        SpreadsheetSession session)
    {
        var menu = _menu ?? throw new InvalidOperationException(
            "Open a Table filter sheet before changing sort state.");
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
                Localization.Format("{0}; {1:N0} dòng", displayText, item.Count));
            SemanticProperties.SetHint(
                checkBox,
                Localization.Get("Chọn hoặc bỏ chọn giá trị này."));
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
            NeraMauiRibbonChrome.ConfigureFilter(row, NeraMauiRibbonPalette.For(IconTheme));
        }

        UpdateSheetText();
        if (focusActiveValue)
        {
            Dispatcher.Dispatch(() =>
            {
                FocusActiveValue();
            });
        }
    }

    private void UpdateSheetText()
    {
        if (_menu is null) return;
        _status.Text = _menu.ValuesTruncated
            ? Localization.Format("Đã quét {0:N0} hàng; danh sách giá trị đã bị giới hạn.", _menu.ScannedRowCount)
            : Localization.Format("{0:N0} giá trị khác nhau trong {1:N0} hàng.", _menu.DistinctValueCount, _menu.ScannedRowCount);
        SemanticProperties.SetDescription(_status, _status.Text);
        _apply.IsEnabled = _menu.CanApplyValueSelection;
        SemanticProperties.SetDescription(
            _sheetPanel,
            Localization.Format("Lọc {0} trong Table {1}", _menu.ColumnName, _menu.TableName));
    }

    /// <summary>Updates native labels and palette in place on the UI thread, preserving open filter edits and focus.</summary>
    public void SetPresentation(PresentationLocalization localization, NeraIconTheme iconTheme)
    {
        ArgumentNullException.ThrowIfNull(localization);
        ObjectDisposedException.ThrowIf(_disposed, this);
        _shellResources.Apply(localization);
        Localization = localization;
        IconTheme = iconTheme;
        NeraMauiRibbonChrome.ConfigureFilter(_sheetPanel, NeraMauiRibbonPalette.For(iconTheme));
        SemanticProperties.SetDescription(this, Localization.Get("Bảng tính Nera có bộ lọc Table tương tác."));
        SemanticProperties.SetHint(this, Localization.Get("Trên Windows, nhấn Alt và mũi tên xuống để mở bộ lọc của cột Table đang chọn."));
        UpdateSheetText();
        SemanticProperties.SetHint(_sheetPanel, Localization.Get("Tìm kiếm hoặc chọn giá trị. Escape đóng, Enter áp dụng, các phím mũi tên duyệt danh sách trên Windows."));
        var items = _menu?.GetVisibleItems();
        if (items is not null)
        {
            for (var index = 0; index < items.Count && index < _valueCheckBoxes.Count; index++)
            {
                var item = items[index];
                var text = DisplayValue(item.Value);
                SemanticProperties.SetDescription(_valueCheckBoxes[index], Localization.Format("{0}; {1:N0} dòng", text, item.Count));
                SemanticProperties.SetHint(_valueCheckBoxes[index], Localization.Get("Chọn hoặc bỏ chọn giá trị này."));
                if (_itemsPanel.Children[index] is HorizontalStackLayout row && row.Children[1] is Label label)
                    label.Text = $"{text}  ({item.Count})";
            }
        }
        foreach (var button in _buttons.Values)
            if (button.CommandParameter is SpreadsheetTableFilterButtonHit hit) SetFilterButtonSemantics(button, hit);
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

    private string DisplayValue(CellValue value) =>
        value.IsBlank ? Localization.Get("(Trống)") : value.ToString();

    private static Color ToColor(
        NeraSpreadSheet.Foundation.ColorRgba color) =>
        Color.FromRgba(
            color.Red,
            color.Green,
            color.Blue,
            color.Alpha);
}
