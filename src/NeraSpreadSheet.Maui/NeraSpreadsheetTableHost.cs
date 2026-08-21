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
public sealed class NeraSpreadsheetTableHost : Grid, IDisposable
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
    private readonly Dictionary<(Guid TableId, Guid ColumnId), Button>
        _buttons = [];
    private SpreadsheetSession? _session;
    private Worksheet? _subscribedWorksheet;
    private SpreadsheetViewportEngine? _viewport;
    private SpreadsheetTableFilterMenu? _menu;
    private bool _disposed;

    public NeraSpreadsheetTableHost()
    {
        Spreadsheet = new NeraSpreadsheetView();
        _buttonLayer = new AbsoluteLayout
        {
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

    public bool TryOpenFilter(Guid tableId, Guid columnId)
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

        _menu = new SpreadsheetTablePresenterController(_session)
            .OpenFilterMenu(tableId, columnId);
        _search.Text = string.Empty;
        RebuildSheetItems();
        _sheetOverlay.IsVisible = true;
        return true;
    }

    public void CloseFilterSheet()
    {
        _sheetOverlay.IsVisible = false;
        _menu = null;
        _itemsPanel.Children.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        DetachSession();
        Spreadsheet.SizeChanged -= OnSpreadsheetVisualChanged;
        Spreadsheet.ScrollChanged -= OnSpreadsheetVisualChanged;
        Spreadsheet.ZoomChanged -= OnSpreadsheetVisualChanged;
        foreach (var button in _buttons.Values)
        {
            button.Clicked -= OnFilterButtonClicked;
        }
        _buttons.Clear();
        _buttonLayer.Children.Clear();
        Spreadsheet.Dispose();
        _disposed = true;
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
            })
        {
            TryOpenFilter(hit.TableId, hit.ColumnId);
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
            IsVisible = false,
            BackgroundColor = Colors.Transparent,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        var backdrop = new BoxView
        {
            Color = Color.FromRgba(0, 0, 0, 96),
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
        };
        var backdropTap = new TapGestureRecognizer();
        backdropTap.Tapped += (_, _) => CloseFilterSheet();
        backdrop.GestureRecognizers.Add(backdropTap);
        overlay.Children.Add(backdrop);

        var panel = new VerticalStackLayout
        {
            BackgroundColor = Colors.White,
            Padding = new Thickness(16d, 14d, 16d, 18d),
            Spacing = 8d,
            VerticalOptions = LayoutOptions.End,
            MaximumHeightRequest = 520d,
        };
        overlay.Children.Add(panel);

        var title = new Label
        {
            Text = "Lọc Table",
            FontSize = 18d,
            FontAttributes = FontAttributes.Bold,
        };
        panel.Children.Add(title);

        var search = new Entry
        {
            Placeholder = "Tìm giá trị",
        };
        panel.Children.Add(search);

        var commands = new HorizontalStackLayout
        {
            Spacing = 8d,
        };
        var selectAll = new Button
        {
            Text = "Chọn tất cả",
            HorizontalOptions = LayoutOptions.Fill,
        };
        var selectNone = new Button
        {
            Text = "Bỏ chọn",
            HorizontalOptions = LayoutOptions.Fill,
        };
        commands.Children.Add(selectAll);
        commands.Children.Add(selectNone);
        panel.Children.Add(commands);

        var status = new Label
        {
            FontSize = 12d,
            TextColor = Colors.Gray,
        };
        panel.Children.Add(status);

        var items = new VerticalStackLayout
        {
            Spacing = 2d,
        };
        panel.Children.Add(new ScrollView
        {
            Content = items,
            MaximumHeightRequest = 280d,
        });

        var footer = new HorizontalStackLayout
        {
            Spacing = 8d,
            HorizontalOptions = LayoutOptions.End,
        };
        var clear = new Button { Text = "Xóa lọc" };
        var cancel = new Button { Text = "Hủy" };
        var apply = new Button { Text = "Áp dụng" };
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
            RebuildSheetItems();
        };
        selectAll.Clicked += (_, _) =>
        {
            _menu?.SelectAllVisible();
            RebuildSheetItems();
        };
        selectNone.Clicked += (_, _) =>
        {
            _menu?.ClearVisibleSelection();
            RebuildSheetItems();
        };
        clear.Clicked += (_, _) =>
        {
            _menu?.ClearColumnFilter();
            CloseFilterSheet();
            _viewport?.InvalidateMetrics();
            Spreadsheet.InvalidateSurface();
            UpdateButtons();
        };
        cancel.Clicked += (_, _) => CloseFilterSheet();
        apply.Clicked += (_, _) =>
        {
            _menu?.ApplyValueSelection();
            CloseFilterSheet();
            _viewport?.InvalidateMetrics();
            Spreadsheet.InvalidateSurface();
            UpdateButtons();
        };

        return (overlay, panel, search, status, items, apply);
    }

    private void RebuildSheetItems()
    {
        _itemsPanel.Children.Clear();
        if (_menu is null)
        {
            _apply.IsEnabled = false;
            return;
        }

        foreach (var item in _menu.GetVisibleItems())
        {
            var value = item.Value;
            var checkBox = new CheckBox
            {
                IsChecked = item.IsSelected,
                VerticalOptions = LayoutOptions.Center,
            };
            checkBox.CheckedChanged += (_, args) =>
            {
                _menu?.SelectValue(value, args.Value);
                if (_menu is not null)
                {
                    _apply.IsEnabled = _menu.CanApplyValueSelection;
                }
            };
            var row = new HorizontalStackLayout
            {
                Spacing = 8d,
            };
            row.Children.Add(checkBox);
            row.Children.Add(new Label
            {
                Text = $"{DisplayValue(value)}  ({item.Count})",
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.TailTruncation,
                HorizontalOptions = LayoutOptions.Fill,
            });
            _itemsPanel.Children.Add(row);
        }

        _status.Text = _menu.ValuesTruncated
            ? $"Đã quét {_menu.ScannedRowCount:N0} hàng; danh sách giá trị đã bị giới hạn."
            : $"{_menu.DistinctValueCount:N0} giá trị khác nhau trong {_menu.ScannedRowCount:N0} hàng.";
        _apply.IsEnabled = _menu.CanApplyValueSelection;
        _sheetPanel.SemanticProperties.Description =
            $"Lọc {_menu.ColumnName} trong Table {_menu.TableName}";
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
        CloseFilterSheet();
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
