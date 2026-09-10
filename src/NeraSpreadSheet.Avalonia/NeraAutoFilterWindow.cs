using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Layout;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia;

/// <summary>
/// Native Avalonia projection of the shared paged AutoFilter presenter. Search,
/// paging and pending value selection stay in the presenter until Apply; closing
/// the window does not mutate the filter. No cell controls are created outside the
/// bounded current page.
/// </summary>
public sealed class NeraAutoFilterWindow : Window, IDisposable
{
    public const int PageSize = 100;
    private readonly SpreadsheetAutoFilterPagedPresenter _presenter;
    private readonly PresentationLocalization _localization;
    private readonly TextBox _search = new() { MinWidth = 260 };
    private readonly StackPanel _values = new() { Spacing = 2 };
    private readonly TextBlock _summary = new() { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
    private readonly Button _previous = new() { Content = "◀" };
    private readonly Button _next = new() { Content = "▶" };
    private readonly Button _apply = new() { Content = "Áp dụng" };
    private readonly Button _clear = new() { Content = "Xóa bộ lọc" };
    private readonly Button _sortAscending = new() { Content = "Sắp xếp tăng" };
    private readonly Button _sortDescending = new() { Content = "Sắp xếp giảm" };
    private CancellationTokenSource? _searchCancellation;
    private bool _updating;
    private bool _disposed;
    private bool _initialized;

    public NeraAutoFilterWindow(
        SpreadsheetSession session,
        SpreadsheetAutoFilterTarget target,
        PresentationLocalization? localization = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        _localization = localization ?? PresentationLocalization.Default;
        _presenter = new SpreadsheetAutoFilterPagedPresenter(session, target, PageSize);
        Title = $"Bộ lọc — {target.ColumnName}";
        Width = 520;
        Height = 650;
        MinWidth = 420;
        MinHeight = 430;

        var root = new DockPanel { Margin = new global::Avalonia.Thickness(10) };
        var top = new StackPanel { Spacing = 7 };
        top.Children.Add(_summary);
        _search.PlaceholderText = _localization.Get("Tìm kiếm");
        SetIdentity(_search, "filter-search", _localization.Get("Tìm kiếm"));
        top.Children.Add(_search);
        var sorting = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        sorting.Children.Add(_sortAscending);
        sorting.Children.Add(_sortDescending);
        top.Children.Add(sorting);
        DockPanel.SetDock(top, Dock.Top);
        root.Children.Add(top);

        var bottom = new StackPanel { Spacing = 7, Margin = new global::Avalonia.Thickness(0, 8, 0, 0) };
        var paging = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
        paging.Children.Add(_previous);
        paging.Children.Add(_next);
        bottom.Children.Add(paging);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
        var selectAll = new Button { Content = "Chọn tất cả" };
        var selectNone = new Button { Content = "Bỏ chọn tất cả" };
        var cancel = new Button { Content = _localization.Get("Hủy") };
        actions.Children.Add(selectAll);
        actions.Children.Add(selectNone);
        actions.Children.Add(_clear);
        actions.Children.Add(cancel);
        actions.Children.Add(_apply);
        bottom.Children.Add(actions);
        DockPanel.SetDock(bottom, Dock.Bottom);
        root.Children.Add(bottom);

        var scroll = new ScrollViewer
        {
            Content = _values,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Margin = new global::Avalonia.Thickness(0, 8),
        };
        root.Children.Add(scroll);
        Content = root;

        SetIdentity(this, "nera-auto-filter-window", Title);
        SetIdentity(_previous, "filter-previous", "Trang trước");
        SetIdentity(_next, "filter-next", "Trang sau");
        SetIdentity(_apply, "filter-apply", "Áp dụng bộ lọc");
        SetIdentity(_clear, "filter-clear", "Xóa bộ lọc");
        SetIdentity(_sortAscending, "filter-sort-ascending", "Sắp xếp tăng");
        SetIdentity(_sortDescending, "filter-sort-descending", "Sắp xếp giảm");
        SetIdentity(selectAll, "filter-select-all", "Chọn tất cả giá trị đang hiển thị");
        SetIdentity(selectNone, "filter-select-none", "Bỏ chọn tất cả giá trị đang hiển thị");
        SetIdentity(cancel, "filter-cancel", _localization.Get("Hủy"));

        Opened += OnOpened;
        Closed += OnClosed;
        _search.TextChanged += OnSearchChanged;
        _previous.Click += async (_, _) => await RunAsync(async () => { await _presenter.MovePreviousPageAsync(); RefreshPage(); });
        _next.Click += async (_, _) => await RunAsync(async () => { await _presenter.MoveNextPageAsync(); RefreshPage(); });
        selectAll.Click += async (_, _) => await RunAsync(async () => { await _presenter.SelectAllVisibleAsync(); RefreshPage(); });
        selectNone.Click += async (_, _) => await RunAsync(async () => { await _presenter.ClearVisibleSelectionAsync(); RefreshPage(); });
        _apply.Click += async (_, _) => await RunAsync(async () => { await _presenter.ApplyValueSelectionAsync(); Close(true); });
        _clear.Click += async (_, _) => await RunAsync(async () => { await _presenter.ClearColumnFilterAsync(); Close(true); });
        _sortAscending.Click += async (_, _) => await RunAsync(async () => { await _presenter.ApplyColumnSortAsync(false); Close(true); });
        _sortDescending.Click += async (_, _) => await RunAsync(async () => { await _presenter.ApplyColumnSortAsync(true); Close(true); });
        cancel.Click += (_, _) => Close(false);
        AddHandler(KeyDownEvent, OnKeyDown, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    public SpreadsheetAutoFilterPagedPresenterSnapshot Snapshot => _presenter.Capture();

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized) return;
        await _presenter.InitializeAsync(cancellationToken);
        _initialized = true;
        RefreshPage();
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        try { await InitializeAsync(); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _summary.Text = exception.Message;
            SetControlsEnabled(false);
        }
    }

    private async void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updating || !_initialized || _disposed) return;
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        var cancellation = _searchCancellation = new CancellationTokenSource();
        try
        {
            await Task.Delay(150, cancellation.Token);
            await _presenter.SetSearchTextAsync(_search.Text, cancellation.Token);
            if (!cancellation.IsCancellationRequested && !_disposed) RefreshPage();
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception) { if (!_disposed) _summary.Text = exception.Message; }
    }

    private void RefreshPage()
    {
        if (_disposed) return;
        VerifyAccess();
        var snapshot = _presenter.Capture();
        _updating = true;
        try
        {
            _values.Children.Clear();
            for (var index = 0; index < snapshot.Values.Count; index++)
            {
                var pageIndex = index;
                var item = snapshot.Values[index];
                var check = new CheckBox
                {
                    IsChecked = item.IsSelected,
                    Content = $"{(item.Value.IsBlank ? "(Trống)" : item.DisplayText)}  ({item.Count:N0})",
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                };
                SetIdentity(check, $"filter-value-{index}", item.Value.IsBlank ? "(Trống)" : item.DisplayText);
                check.IsCheckedChanged += async (_, _) =>
                {
                    if (_updating || _disposed) return;
                    await RunAsync(async () =>
                    {
                        await _presenter.SetSelectedAsync(pageIndex, check.IsChecked == true);
                        RefreshPage();
                    });
                };
                _values.Children.Add(check);
            }
            var end = snapshot.Values.Count == 0 ? 0 : snapshot.PageOffset + snapshot.Values.Count;
            _summary.Text = $"{snapshot.Target.OwnerName} · {snapshot.Target.ColumnName} · " +
                $"{snapshot.PageOffset + (snapshot.Values.Count == 0 ? 0 : 1):N0}–{end:N0}/{snapshot.TotalItemCount:N0} · " +
                snapshot.AccessibilityAnnouncement;
            _previous.IsEnabled = snapshot.HasPreviousPage;
            _next.IsEnabled = snapshot.HasNextPage;
            _apply.IsEnabled = snapshot.IsInitialized;
            _clear.IsEnabled = snapshot.IsInitialized;
            _sortAscending.IsEnabled = snapshot.IsInitialized;
            _sortDescending.IsEnabled = snapshot.IsInitialized;
        }
        finally { _updating = false; }
    }

    private async Task RunAsync(Func<Task> operation)
    {
        if (_disposed) return;
        try
        {
            SetControlsEnabled(false);
            await operation();
        }
        catch (OperationCanceledException) { }
        catch (Exception exception) { if (!_disposed) _summary.Text = exception.Message; }
        finally { if (!_disposed) SetControlsEnabled(true); }
    }

    private void SetControlsEnabled(bool enabled)
    {
        _search.IsEnabled = enabled;
        _values.IsEnabled = enabled;
        if (!enabled)
        {
            _previous.IsEnabled = _next.IsEnabled = _apply.IsEnabled = _clear.IsEnabled = false;
            _sortAscending.IsEnabled = _sortDescending.IsEnabled = false;
        }
        else if (_initialized) RefreshPage();
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        e.Handled = true;
        Close(false);
    }

    private void OnClosed(object? sender, EventArgs e) => Dispose();

    private static void SetIdentity(Control control, string id, string name)
    {
        AutomationProperties.SetAutomationId(control, id);
        AutomationProperties.SetName(control, name);
    }

    public void Dispose()
    {
        if (_disposed) return;
        VerifyAccess();
        _disposed = true;
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = null;
        Opened -= OnOpened;
        Closed -= OnClosed;
        _search.TextChanged -= OnSearchChanged;
        RemoveHandler(KeyDownEvent, OnKeyDown);
        _presenter.Dispose();
        GC.SuppressFinalize(this);
    }
}
