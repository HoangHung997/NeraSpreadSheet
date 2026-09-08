using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Commands;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

/// <summary>
/// Paged native WPF AutoFilter presenter for both Table and direct worksheet
/// filter owners. Only the current page is materialized into native checkboxes.
/// </summary>
public sealed partial class NeraAutoFilterPagedPopupPresenter : IDisposable
{
    /// <summary>Resources used when the filter surface is next opened or refreshed.</summary>
    public PresentationLocalization Localization { get; set; } = PresentationLocalization.Default;

    /// <summary>Gets or sets the palette used the next time the filter opens.</summary>
    public NeraIconTheme IconTheme { get; set; } = NeraIconTheme.Light;

    private const double PopupWidth = 340d;
    private const double PopupMaximumHeight = 540d;
    private const int PageSize = 100;
    private static readonly TimeSpan SearchDelay =
        TimeSpan.FromMilliseconds(150d);

    private readonly NeraSpreadsheetControl _control;
    private readonly List<CheckBox> _valueCheckBoxes = [];
    private readonly object _operationStateGate = new();
    private readonly HashSet<CancellationTokenSource> _operationCancellations = [];
    private SpreadsheetSession? _viewportSession;
    private SpreadsheetViewportEngine? _viewport;
    private FilterButtonAdorner? _adorner;
    private Popup? _popup;
    private TextBox? _searchBox;
    private ComboBox? _menuKindBox;
    private TextBox? _criterionInput;
    private TextBox? _secondCriterionInput;
    private ComboBox? _conditionJoinBox;
    private UIElement? _customConditionPanel;
    private WrapPanel? _selectionCommands;
    private Button? _dateBackButton;
    private TextBlock? _status;
    private StackPanel? _itemsPanel;
    private ScrollViewer? _itemsScroller;
    private Button? _previousButton;
    private Button? _nextButton;
    private Button? _applyButton;
    private NeraWpfAutoFilterPagedBinding? _binding;
    private Task _operationTail = Task.CompletedTask;
    private CancellationTokenSource? _searchCancellation;
    private SpreadsheetAutoFilterDateParent _dateParent = new(null, null);
    private SpreadsheetAutoFilterDatePage? _datePage;
    private readonly HashSet<SpreadsheetFilterDateGroup> _selectedDateGroups = [];
    private IInputElement? _focusBeforeOpen;
    private bool _rebuilding;
    private bool _disposed;

    public NeraAutoFilterPagedPopupPresenter(
        NeraSpreadsheetControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
        _control.Loaded += OnLoaded;
        _control.Unloaded += OnUnloaded;
        _control.LayoutUpdated += OnLayoutUpdated;
        _control.SizeChanged += OnSizeChanged;
        _control.ScrollChanged += OnScrollChanged;
        NeraSpreadsheetSplitExtensions.ControllerChanged += OnControllerChanged;
        SynchronizeHost();
        if (_control.IsLoaded)
        {
            AttachAdorner();
            RefreshHostPresentation();
        }
    }

    public bool IsOpen => _popup?.IsOpen == true;

    public Task<SpreadsheetAutoFilterDatePage> GetDatePageAsync(
        SpreadsheetAutoFilterDateParent parent,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        (_binding ?? throw new InvalidOperationException(
            "Open the AutoFilter popup before requesting date nodes."))
        .GetDatePageAsync(parent, offset, pageSize, cancellationToken);

    public async Task<long> ApplyRichFilterAsync(
        SpreadsheetAutoFilterRichCriterion criterion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterion);
        var binding = _binding ?? throw new InvalidOperationException(
            "Open the AutoFilter popup before applying a rich criterion.");
        if (!IsCurrentBinding(binding)) throw new InvalidOperationException("The filter host has changed.");
        var generation = await binding.ApplyRichFilterAsync(
            criterion,
            cancellationToken);
        if (IsCurrentBinding(binding)) CloseAndRefresh();
        return generation;
    }

    public void Close()
    {
        if (_popup is not null)
        {
            _popup.IsOpen = false;
        }
    }

    public bool TryOpenForActiveCell()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SynchronizeHost();
        var session = _control.Session;
        if (!IsHostReady || _control.CurrentEditorDraft is not null || session is null ||
            !session.TryResolveActiveAutoFilterTarget(out var target))
        {
            return false;
        }

        var hit = _nativeButtons.FirstOrDefault(candidate =>
            candidate.Pane == (_splitHost?.ActivePane) &&
            candidate.Hit.HeaderCell == target.HeaderCell &&
            candidate.Hit.TableId == target.TableId && candidate.Hit.TableColumnId == target.TableColumnId &&
            candidate.Hit.OwnerKind == ToGeometryOwner(target.OwnerKind));
        if (hit.Hit.Bounds.IsEmpty)
        {
            return false;
        }

        Open(hit, target);
        return true;
    }

    public bool TryOpenAt(double x, double y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SynchronizeHost();
        if (!IsHostReady || _control.CurrentEditorDraft is not null || !TryHitTest(x, y, out var hit) ||
            _control.Session is not { } session ||
            !session.TryResolveAutoFilterTarget(
                hit.Hit.HeaderCell,
                out var target) || hit.Hit.OwnerKind != ToGeometryOwner(target.OwnerKind) ||
            hit.Hit.TableId != target.TableId || hit.Hit.TableColumnId != target.TableColumnId)
        {
            return false;
        }

        if (hit.Pane is { } pane) _splitHost?.ActivatePresentationPane(pane);
        Open(hit, target);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Close();
        CancelOperations();
        DisposeBinding();
        DetachAdorner();
        _control.Loaded -= OnLoaded;
        _control.Unloaded -= OnUnloaded;
        _control.LayoutUpdated -= OnLayoutUpdated;
        _control.SizeChanged -= OnSizeChanged;
        _control.ScrollChanged -= OnScrollChanged;
        NeraSpreadsheetSplitExtensions.ControllerChanged -= OnControllerChanged;
        DetachHost();
        GC.SuppressFinalize(this);
    }

    internal SpreadsheetAutoFilterButtonHit[] GetVisibleButtons() =>
        _nativeButtons.Select(static button => button.Hit).ToArray();

    private SpreadsheetAutoFilterButtonHit[] ComposeStandaloneButtons()
    {
        var session = _control.Session;
        if (session is null ||
            _control.ActualWidth <= 0d ||
            _control.ActualHeight <= 0d ||
            !_control.RenderTheme.ShowTableFilterButtons)
        {
            return [];
        }

        var chrome = SpreadsheetChromeGeometry.Calculate(
            _control.ActualWidth,
            _control.ActualHeight,
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
        return SpreadsheetAutoFilterButtonGeometry.GetVisibleButtons(
                session.ActiveWorksheet.Tables,
                session.ActiveWorksheet.AutoFilter,
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

    private bool TryHitTest(
        double x,
        double y,
        out NativeFilterButton hit)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            hit = default;
            return false;
        }
        var point = new PointD(x, y);
        foreach (var candidate in _nativeButtons)
        {
            if (candidate.Hit.Bounds.Contains(point))
            {
                hit = candidate;
                return true;
            }
        }
        hit = default;
        return false;
    }

    private void Open(
        NativeFilterButton button,
        SpreadsheetAutoFilterTarget target)
    {
        var session = _control.Session
            ?? throw new InvalidOperationException(
                "A spreadsheet session is required before opening AutoFilter.");
        _openGeneration++;
        Close();
        CancelOperations();
        DisposeBinding();
        _focusBeforeOpen = Keyboard.FocusedElement;
        _openContext = new FilterOpenContext(_openGeneration, session, session.ActiveWorksheet,
            _inputSurface!, _splitHost, button);
        var presenter = new SpreadsheetAutoFilterPagedPresenter(
            session,
            target,
            PageSize);
        var binding = new NeraWpfAutoFilterPagedBinding(
            presenter,
            _control.Dispatcher);
        binding.Localization = Localization;
        _binding = binding;

        var popup = new Popup
        {
            PlacementTarget = _inputSurface,
            Placement = PlacementMode.RelativePoint,
            HorizontalOffset = Math.Max(0d, button.Hit.Bounds.Left),
            VerticalOffset = Math.Max(0d, button.Hit.Bounds.Bottom),
            StaysOpen = false,
            AllowsTransparency = true,
            Child = BuildPopupContent(target),
        };
        popup.Opened += OnPopupOpened;
        popup.Closed += OnPopupClosed;
        _popup = popup;
        popup.IsOpen = true;
    }
}
