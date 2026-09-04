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
    private const double PopupWidth = 340d;
    private const double PopupMaximumHeight = 470d;
    private const int PageSize = 100;
    private static readonly TimeSpan SearchDelay =
        TimeSpan.FromMilliseconds(150d);

    private readonly NeraSpreadsheetControl _control;
    private readonly List<CheckBox> _valueCheckBoxes = [];
    private SpreadsheetSession? _viewportSession;
    private SpreadsheetViewportEngine? _viewport;
    private FilterButtonAdorner? _adorner;
    private Popup? _popup;
    private TextBox? _searchBox;
    private ComboBox? _menuKindBox;
    private TextBox? _criterionInput;
    private TextBlock? _status;
    private StackPanel? _itemsPanel;
    private Button? _previousButton;
    private Button? _nextButton;
    private Button? _applyButton;
    private NeraWpfAutoFilterPagedBinding? _binding;
    private CancellationTokenSource? _operationCancellation;
    private CancellationTokenSource? _searchCancellation;
    private IInputElement? _focusBeforeOpen;
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
        _control.PreviewKeyDown += OnControlPreviewKeyDown;
        _control.PreviewMouseMove += OnPreviewMouseMove;
        _control.PreviewMouseLeftButtonDown +=
            OnPreviewMouseLeftButtonDown;
        if (_control.IsLoaded)
        {
            AttachAdorner();
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
        var generation = await binding.ApplyRichFilterAsync(
            criterion,
            cancellationToken);
        if (ReferenceEquals(_binding, binding)) CloseAndRefresh();
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
        var session = _control.Session;
        if (session is null ||
            !session.TryResolveActiveAutoFilterTarget(out var target))
        {
            return false;
        }

        var hit = GetVisibleButtons().FirstOrDefault(candidate =>
            candidate.HeaderCell == target.HeaderCell &&
            candidate.OwnerKind == ToGeometryOwner(target.OwnerKind));
        if (hit.Bounds.IsEmpty)
        {
            return false;
        }

        Open(hit, target);
        return true;
    }

    public bool TryOpenAt(double x, double y)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryHitTest(x, y, out var hit) ||
            _control.Session is not { } session ||
            !session.TryResolveAutoFilterTarget(
                hit.HeaderCell,
                out var target))
        {
            return false;
        }

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
        _control.PreviewKeyDown -= OnControlPreviewKeyDown;
        _control.PreviewMouseMove -= OnPreviewMouseMove;
        _control.PreviewMouseLeftButtonDown -=
            OnPreviewMouseLeftButtonDown;
        GC.SuppressFinalize(this);
    }

    internal SpreadsheetAutoFilterButtonHit[] GetVisibleButtons()
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

    private bool TryHitTest(
        double x,
        double y,
        out SpreadsheetAutoFilterButtonHit hit)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            hit = default;
            return false;
        }
        var point = new PointD(x, y);
        foreach (var candidate in GetVisibleButtons())
        {
            if (candidate.Bounds.Contains(point))
            {
                hit = candidate;
                return true;
            }
        }
        hit = default;
        return false;
    }

    private void Open(
        SpreadsheetAutoFilterButtonHit hit,
        SpreadsheetAutoFilterTarget target)
    {
        var session = _control.Session
            ?? throw new InvalidOperationException(
                "A spreadsheet session is required before opening AutoFilter.");
        Close();
        CancelOperations();
        DisposeBinding();
        _focusBeforeOpen = Keyboard.FocusedElement;
        var presenter = new SpreadsheetAutoFilterPagedPresenter(
            session,
            target,
            PageSize);
        var binding = new NeraWpfAutoFilterPagedBinding(
            presenter,
            _control.Dispatcher);
        _binding = binding;

        var popup = new Popup
        {
            PlacementTarget = _control,
            Placement = PlacementMode.RelativePoint,
            HorizontalOffset = Math.Max(0d, hit.Bounds.Left),
            VerticalOffset = Math.Max(0d, hit.Bounds.Bottom),
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
