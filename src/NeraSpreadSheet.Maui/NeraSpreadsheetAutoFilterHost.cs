using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Responsive MAUI host that exposes the same paged AutoFilter presenter for
/// Table and direct worksheet filters. Only visible header buttons and one
/// value page are materialized as native controls.
/// </summary>
public sealed partial class NeraSpreadsheetAutoFilterHost : Grid, IDisposable
{
    private const int PageSize = 100;
    private static readonly TimeSpan SearchDelay =
        TimeSpan.FromMilliseconds(180d);

    public static readonly BindableProperty WorkbookProperty =
        BindableProperty.Create(
            nameof(Workbook),
            typeof(Workbook),
            typeof(NeraSpreadsheetAutoFilterHost),
            default(Workbook),
            propertyChanged: static (bindable, _, newValue) =>
                ((NeraSpreadsheetAutoFilterHost)bindable)
                    .OnWorkbookChanged((Workbook?)newValue));

    private readonly AbsoluteLayout _buttonLayer;
    private readonly Dictionary<FilterButtonKey, Button> _buttons = [];
    private readonly Grid _sheetOverlay;
    private readonly VerticalStackLayout _sheetPanel;
    private readonly Entry _search;
    private readonly Label _status;
    private readonly CollectionView _values;
    private readonly Button _previousButton;
    private readonly Button _nextButton;
    private readonly Button _applyButton;

    private SpreadsheetSession? _session;
    private SpreadsheetViewportEngine? _viewport;
    private Worksheet? _subscribedWorksheet;
    private NeraMauiAutoFilterPagedBinding? _binding;
    private CancellationTokenSource? _operationCancellation;
    private CancellationTokenSource? _searchCancellation;
    private VisualElement? _focusBeforeOpen;
    private bool _disposed;

    public NeraSpreadsheetAutoFilterHost()
    {
        Spreadsheet = new NeraSpreadsheetView
        {
            AutomationId = "NeraAutoFilterSpreadsheet",
        };
        _buttonLayer = new AbsoluteLayout
        {
            AutomationId = "NeraAutoFilterButtonLayer",
            InputTransparent = false,
        };
        var sheet = CreateSheet();
        _sheetOverlay = sheet.Overlay;
        _sheetPanel = sheet.Panel;
        _search = sheet.Search;
        _status = sheet.Status;
        _values = sheet.Values;
        _previousButton = sheet.Previous;
        _nextButton = sheet.Next;
        _applyButton = sheet.Apply;

        Children.Add(Spreadsheet);
        Children.Add(_buttonLayer);
        Children.Add(_sheetOverlay);
        Spreadsheet.SizeChanged += OnSpreadsheetLayoutChanged;
        Spreadsheet.HandlerChanged += OnSpreadsheetHandlerChanged;
        Spreadsheet.ScrollChanged += OnSpreadsheetScrollChanged;
        SizeChanged += OnHostSizeChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public Workbook? Workbook
    {
        get => (Workbook?)GetValue(WorkbookProperty);
        set => SetValue(WorkbookProperty, value);
    }

    public NeraSpreadsheetView Spreadsheet { get; }

    public bool IsFilterSheetOpen => _sheetOverlay.IsVisible;

    public bool TryOpenForActiveCell()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session is null ||
            !_session.TryResolveActiveAutoFilterTarget(out var target))
        {
            return false;
        }

        UpdateButtons();
        var button = _buttons.Values.FirstOrDefault(candidate =>
            candidate.IsVisible &&
            candidate.CommandParameter is SpreadsheetAutoFilterButtonHit hit &&
            hit.HeaderCell == target.HeaderCell &&
            hit.OwnerKind == ToGeometryOwner(target.OwnerKind));
        return TryOpenFilterCore(target, button ?? Spreadsheet);
    }

    public bool TryOpenFilter(CellAddress address)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return _session?.TryResolveAutoFilterTarget(address, out var target) == true &&
               TryOpenFilterCore(target, Spreadsheet);
    }

    public void CloseFilterSheet()
    {
        if (!_sheetOverlay.IsVisible)
        {
            return;
        }
        CancelOperations();
        DisposeBinding();
        _values.ItemsSource = null;
        _sheetOverlay.IsVisible = false;
        _search.Text = string.Empty;
        _search.Unfocus();
        RestoreFocus();
        OnSheetClosedPlatform();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        CloseFilterSheet();
        DetachPlatformKeyboard();
        DetachSession();
        Spreadsheet.SizeChanged -= OnSpreadsheetLayoutChanged;
        Spreadsheet.HandlerChanged -= OnSpreadsheetHandlerChanged;
        Spreadsheet.ScrollChanged -= OnSpreadsheetScrollChanged;
        SizeChanged -= OnHostSizeChanged;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        foreach (var button in _buttons.Values)
        {
            button.Clicked -= OnFilterButtonClicked;
        }
        _buttons.Clear();
        GC.SuppressFinalize(this);
    }

    private void OnWorkbookChanged(Workbook? workbook)
    {
        Spreadsheet.Workbook = workbook;
        AttachSession(Spreadsheet.Session);
        CloseFilterSheet();
        UpdateButtons();
    }

    private void AttachSession(SpreadsheetSession? session)
    {
        if (ReferenceEquals(_session, session))
        {
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
        }
        EnsureWorksheetSubscription();
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
        var hits = SpreadsheetAutoFilterButtonGeometry.GetVisibleButtons(
            WorksheetSnapshot.Capture(_session.ActiveWorksheet),
            frame.Layout,
            Spreadsheet.RenderTheme);
        var visible = new HashSet<FilterButtonKey>();
        foreach (var hit in hits)
        {
            var key = CreateButtonKey(hit);
            visible.Add(key);
            if (!_buttons.TryGetValue(key, out var button))
            {
                button = CreateFilterButton(key);
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
            SetFilterButtonSemantics(button, hit);
            AbsoluteLayout.SetLayoutBounds(button, scaled);
            AbsoluteLayout.SetLayoutFlags(button, AbsoluteLayoutFlags.None);
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

    private Button CreateFilterButton(FilterButtonKey key)
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
            AutomationId = CreateAutomationId(key),
        };
        button.Clicked += OnFilterButtonClicked;
        return button;
    }

    private void OnFilterButtonClicked(object? sender, EventArgs e)
    {
        if (sender is Button
            {
                CommandParameter: SpreadsheetAutoFilterButtonHit hit,
            } button &&
            _session?.TryResolveAutoFilterTarget(
                hit.HeaderCell,
                out var target) == true)
        {
            TryOpenFilterCore(target, button);
        }
    }

    private bool TryOpenFilterCore(
        SpreadsheetAutoFilterTarget target,
        VisualElement focusBeforeOpen)
    {
        if (_session is null)
        {
            return false;
        }
        CloseFilterSheet();
        _focusBeforeOpen = focusBeforeOpen;
        var presenter = new SpreadsheetAutoFilterPagedPresenter(
            _session,
            target,
            PageSize);
        var binding = new NeraMauiAutoFilterPagedBinding(
            presenter,
            Dispatcher);
        _binding = binding;
        _values.ItemsSource = binding.Items;
        _sheetOverlay.IsVisible = true;
        SemanticProperties.SetDescription(
            _sheetPanel,
            $"Lọc {target.ColumnName} trong {target.OwnerName}");
        _search.Text = string.Empty;
        StartOperation(async token =>
        {
            await binding.InitializeAsync(token);
            UpdateSheetState();
            FocusSearch();
            OnSheetOpenedPlatform();
        });
        return true;
    }

    private void SetFilterButtonSemantics(
        Button button,
        SpreadsheetAutoFilterButtonHit hit)
    {
        var description = "Mở bộ lọc bảng tính";
        if (_session?.TryResolveAutoFilterTarget(
                hit.HeaderCell,
                out var target) == true)
        {
            description = $"Lọc cột {target.ColumnName} trong {target.OwnerName}";
        }
        SemanticProperties.SetDescription(button, description);
        SemanticProperties.SetHint(
            button,
            "Chạm hoặc nhấn Enter để mở danh sách giá trị phân trang.");
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

    private void OnSpreadsheetLayoutChanged(object? sender, EventArgs e) =>
        UpdateButtons();

    private void OnSpreadsheetHandlerChanged(object? sender, EventArgs e)
    {
        AttachSession(Spreadsheet.Session);
        AttachPlatformKeyboard();
        UpdateButtons();
    }

    private void OnSpreadsheetScrollChanged(
        object? sender,
        ScrollChangedEventArgs e) =>
        UpdateButtons();

    private void OnHostSizeChanged(object? sender, EventArgs e) =>
        UpdateButtons();

    private void OnLoaded(object? sender, EventArgs e)
    {
        AttachSession(Spreadsheet.Session);
        AttachPlatformKeyboard();
        UpdateButtons();
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        CloseFilterSheet();
        DetachPlatformKeyboard();
    }

    private static FilterButtonKey CreateButtonKey(
        SpreadsheetAutoFilterButtonHit hit) =>
        new(
            hit.OwnerKind,
            hit.TableId,
            hit.TableColumnId,
            hit.WorksheetColumnIndex);

    private static string CreateAutomationId(FilterButtonKey key) =>
        key.OwnerKind == SpreadsheetAutoFilterButtonOwnerKind.Table
            ? $"NeraAutoFilter_Table_{key.TableId?.ToString("N")}_{key.TableColumnId?.ToString("N")}" 
            : $"NeraAutoFilter_Worksheet_{key.WorksheetColumnIndex}";

    private static SpreadsheetAutoFilterButtonOwnerKind ToGeometryOwner(
        SpreadsheetAutoFilterOwnerKind ownerKind) =>
        ownerKind switch
        {
            SpreadsheetAutoFilterOwnerKind.Table =>
                SpreadsheetAutoFilterButtonOwnerKind.Table,
            SpreadsheetAutoFilterOwnerKind.Worksheet =>
                SpreadsheetAutoFilterButtonOwnerKind.Worksheet,
            _ => throw new ArgumentOutOfRangeException(nameof(ownerKind)),
        };

    private static Color ToColor(ColorRgba color) =>
        Color.FromRgba(
            color.Red,
            color.Green,
            color.Blue,
            color.Alpha);

    partial void AttachPlatformKeyboard();

    partial void DetachPlatformKeyboard();

    partial void OnSheetOpenedPlatform();

    partial void OnSheetClosedPlatform();

    private readonly record struct FilterButtonKey(
        SpreadsheetAutoFilterButtonOwnerKind OwnerKind,
        Guid? TableId,
        Guid? TableColumnId,
        int WorksheetColumnIndex);
}
