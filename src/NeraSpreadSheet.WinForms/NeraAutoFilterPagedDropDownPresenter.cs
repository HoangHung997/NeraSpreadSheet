using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Commands;
using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

/// <summary>
/// Native WinForms presenter for paged Table and direct worksheet AutoFilter
/// menus. Only the current page is materialized into the checked list.
/// </summary>
public sealed partial class NeraAutoFilterPagedDropDownPresenter : IDisposable
{
    /// <summary>Resources used when the filter surface is next opened or refreshed.</summary>
    public PresentationLocalization Localization { get; set; } = PresentationLocalization.Default;

    /// <summary>Gets or sets the palette used the next time the filter opens.</summary>
    public NeraIconTheme IconTheme { get; set; } = NeraIconTheme.Light;

    private const int DropDownWidth = 350;
    private const int DropDownHeight = 540;
    private const int PageSize = 100;
    private static readonly TimeSpan SearchDelay =
        TimeSpan.FromMilliseconds(150d);

    private readonly NeraSpreadsheetControl _control;
    private readonly Dictionary<FilterButtonKey, Button> _buttons = [];
    private readonly object _operationStateGate = new();
    private readonly HashSet<CancellationTokenSource> _operationCancellations = [];
    private SpreadsheetSession? _viewportSession;
    private SpreadsheetViewportEngine? _viewport;
    private ToolStripDropDown? _dropDown;
    private TextBox? _searchBox;
    private ComboBox? _menuKindBox;
    private TextBox? _criterionInput;
    private TextBox? _secondCriterionInput;
    private ComboBox? _conditionJoinBox;
    private Button? _selectAllButton;
    private Button? _selectNoneButton;
    private Button? _dateBackButton;
    private CheckedListBox? _valuesList;
    private Label? _status;
    private Button? _previousButton;
    private Button? _nextButton;
    private Button? _applyButton;
    private NeraWinFormsAutoFilterPagedBinding? _binding;
    private Task _operationTail = Task.CompletedTask;
    private CancellationTokenSource? _searchCancellation;
    private SpreadsheetAutoFilterDateParent _dateParent = new(null, null);
    private SpreadsheetAutoFilterDatePage? _datePage;
    private readonly HashSet<SpreadsheetFilterDateGroup> _selectedDateGroups = [];
    private Control? _focusBeforeOpen;
    private bool _rebuilding;
    private bool _disposed;

    public NeraAutoFilterPagedDropDownPresenter(
        NeraSpreadsheetControl control)
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

    public Task<SpreadsheetAutoFilterDatePage> GetDatePageAsync(
        SpreadsheetAutoFilterDateParent parent,
        int offset,
        int pageSize,
        CancellationToken cancellationToken = default) =>
        (_binding ?? throw new InvalidOperationException(
            "Open the AutoFilter dropdown before requesting date nodes."))
        .GetDatePageAsync(parent, offset, pageSize, cancellationToken);

    public async Task<long> ApplyRichFilterAsync(
        SpreadsheetAutoFilterRichCriterion criterion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criterion);
        var binding = _binding ?? throw new InvalidOperationException(
            "Open the AutoFilter dropdown before applying a rich criterion.");
        var generation = await binding.ApplyRichFilterAsync(
            criterion,
            cancellationToken);
        if (ReferenceEquals(_binding, binding)) CloseAndRefresh();
        return generation;
    }

    public void Close()
    {
        _dropDown?.Close(ToolStripDropDownCloseReason.CloseCalled);
    }

    public void Refresh() => UpdateButtons();

    public bool TryOpenForActiveCell()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var session = _control.Session;
        if (session is null ||
            !session.TryResolveActiveAutoFilterTarget(out var target))
        {
            return false;
        }

        UpdateButtons();
        var button = _buttons.Values.FirstOrDefault(candidate =>
            candidate.Visible &&
            candidate.Tag is SpreadsheetAutoFilterButtonHit hit &&
            hit.HeaderCell == target.HeaderCell &&
            hit.OwnerKind == ToGeometryOwner(target.OwnerKind));
        if (button?.Tag is not SpreadsheetAutoFilterButtonHit targetHit)
        {
            return false;
        }

        Open(button, targetHit, target);
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
        _dropDown?.Dispose();
        _dropDown = null;
        GC.SuppressFinalize(this);
    }

    internal SpreadsheetAutoFilterButtonHit[] GetVisibleButtons()
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

    private readonly record struct FilterButtonKey(
        SpreadsheetAutoFilterButtonOwnerKind OwnerKind,
        Guid? TableId,
        Guid? TableColumnId,
        int WorksheetColumnIndex);
}
