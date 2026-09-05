using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Wpf.Sample;

public sealed partial class RibbonPreviewWindow
{
    private readonly ScrollBar _horizontalNavigation = new() { Orientation = Orientation.Horizontal };
    private readonly ScrollBar _verticalNavigation = new() { Orientation = Orientation.Vertical };
    private Worksheet? _navigationWorksheet;
    private DispatcherOperation? _navigationRefresh;
    private double? _pendingHorizontalOffset;
    private double? _pendingVerticalOffset;
    private bool _navigationFrameAttached;
    private bool _synchronizingNavigation;

    private Grid CreateWorksheetNavigation()
    {
        var body = new Grid();
        body.RowDefinitions.Add(new RowDefinition());
        body.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        body.ColumnDefinitions.Add(new ColumnDefinition());
        body.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        body.Children.Add(new AdornerDecorator { Child = _sheet });
        Grid.SetRow(_horizontalNavigation, 1);
        Grid.SetColumn(_verticalNavigation, 1);
        ConfigureNavigationBar(_horizontalNavigation, "preview-worksheet-scroll-horizontal");
        ConfigureNavigationBar(_verticalNavigation, "preview-worksheet-scroll-vertical");
        body.Children.Add(_horizontalNavigation);
        body.Children.Add(_verticalNavigation);
        _sheet.Loaded += OnNavigationLoaded;
        _sheet.Unloaded += OnNavigationUnloaded;
        _sheet.SizeChanged += OnNavigationStateChanged;
        _sheet.ZoomChanged += OnNavigationStateChanged;
        _sheet.ScrollChanged += OnNavigationStateChanged;
        _session.Selection.Changed += OnNavigationSelectionChanged;
        _session.ActiveWorksheetChanged += OnNavigationWorksheetChanged;
        _session.View.Changed += OnNavigationViewChanged;
        _session.View.SplitChanged += OnNavigationSplitChanged;
        return body;
    }

    private void ConfigureNavigationBar(ScrollBar bar, string id)
    {
        bar.Minimum = 0d;
        bar.SmallChange = _sheet.RenderTheme.ScrollBarLineStep;
        bar.Focusable = true;
        AutomationProperties.SetAutomationId(bar, id);
        RefreshNavigationName(bar);
        bar.ValueChanged += OnNavigationValueChanged;
    }

    private void OnNavigationLoaded(object sender, RoutedEventArgs e) => QueueNavigationRefresh();

    private void OnNavigationUnloaded(object sender, RoutedEventArgs e)
    {
        CancelNavigationInput();
        _navigationRefresh?.Abort();
        _navigationRefresh = null;
    }

    private void OnNavigationStateChanged(object? sender, EventArgs e) => QueueNavigationRefresh();

    private void OnNavigationViewChanged(object? sender, SpreadsheetViewChangedEventArgs e)
    {
        if (ReferenceEquals(e.Worksheet, _session.ActiveWorksheet)) QueueNavigationRefresh();
    }

    private void OnNavigationWorksheetChanged(object? sender, EventArgs e)
    {
        // A queued thumb position belongs to the worksheet that received it.
        CancelNavigationInput();
        QueueNavigationRefresh();
    }

    private void OnNavigationSelectionChanged(object? sender, EventArgs e)
    {
        // Later cell navigation takes precedence over a thumb position queued for
        // the previous selection; it may already have scrolled that cell into view.
        CancelNavigationInput();
        QueueNavigationRefresh();
    }

    private void QueueNavigationRefresh()
    {
        if (_disposed || !_sheet.IsLoaded || _navigationRefresh is not null) return;
        // SDK model handlers and the render pass update the cached extent first.
        // No worksheet enumeration or recurring layout polling belongs in the shell.
        _navigationRefresh = Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, () =>
        {
            _navigationRefresh = null;
            if (_disposed || !_sheet.IsLoaded) return;
            SynchronizeNavigationWorksheet();
            SynchronizeSplitShell();
            RefreshNavigationBars();
        });
    }

    private void SynchronizeNavigationWorksheet()
    {
        if (ReferenceEquals(_navigationWorksheet, _session.ActiveWorksheet)) return;
        DetachNavigationWorksheet();
        _navigationWorksheet = _session.ActiveWorksheet;
        _navigationWorksheet.CellsChanged += OnNavigationStateChanged;
        _navigationWorksheet.Dimensions.Changed += OnNavigationStateChanged;
    }

    private void DetachNavigationWorksheet()
    {
        if (_navigationWorksheet is null) return;
        _navigationWorksheet.CellsChanged -= OnNavigationStateChanged;
        _navigationWorksheet.Dimensions.Changed -= OnNavigationStateChanged;
        _navigationWorksheet = null;
    }

    private void RefreshNavigationBars()
    {
        var chrome = SpreadsheetChromeGeometry.Calculate(_sheet.ActualWidth, _sheet.ActualHeight, _sheet.RenderTheme);
        var snapshot = _sheet.ScrollSnapshot;
        _synchronizingNavigation = true;
        try
        {
            UpdateNavigationBar(_horizontalNavigation, _sheet.ContentWidth, chrome.BodyWidth,
                _pendingHorizontalOffset ?? snapshot.OffsetX);
            UpdateNavigationBar(_verticalNavigation, _sheet.ContentHeight, chrome.BodyHeight,
                _pendingVerticalOffset ?? snapshot.OffsetY);
        }
        finally { _synchronizingNavigation = false; }
    }

    private void UpdateNavigationBar(ScrollBar bar, double content, double viewport, double offset)
    {
        RefreshNavigationName(bar);
        bar.Visibility = _splitShell is null ? Visibility.Visible : Visibility.Collapsed;
        bar.ViewportSize = viewport;
        bar.LargeChange = viewport * _sheet.RenderTheme.ScrollBarPageFactor;
        bar.Maximum = Math.Max(0d, content - viewport);
        bar.IsEnabled = bar.Maximum > 0d && _splitShell is null;
        bar.Value = Math.Clamp(offset, 0d, bar.Maximum);
    }

    private void RefreshNavigationName(ScrollBar bar) => AutomationProperties.SetName(bar,
        bar.Orientation == Orientation.Horizontal
            ? Localization.Get("Cuộn ngang trang tính")
            : Localization.Get("Cuộn dọc trang tính"));

    private void OnNavigationValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_disposed || _synchronizingNavigation || !_sheet.IsLoaded || _splitShell is not null ||
            _session.View.SplitState.HasSplitPanes || !ReferenceEquals(_navigationWorksheet, _session.ActiveWorksheet)) return;
        if (ReferenceEquals(sender, _horizontalNavigation)) _pendingHorizontalOffset = e.NewValue;
        else _pendingVerticalOffset = e.NewValue;
        if (_navigationFrameAttached) return;
        _navigationFrameAttached = true;
        CompositionTarget.Rendering += OnNavigationFrame;
    }

    private void OnNavigationFrame(object? sender, EventArgs e)
    {
        var horizontal = _pendingHorizontalOffset;
        var vertical = _pendingVerticalOffset;
        CancelNavigationInput();
        if (_disposed || !_sheet.IsLoaded || _splitShell is not null || _session.View.SplitState.HasSplitPanes) return;
        var chrome = SpreadsheetChromeGeometry.Calculate(_sheet.ActualWidth, _sheet.ActualHeight, _sheet.RenderTheme);
        var snapshot = _sheet.ScrollSnapshot;
        _sheet.ScrollTo(
            Math.Clamp(horizontal ?? snapshot.OffsetX, 0d, Math.Max(0d, _sheet.ContentWidth - chrome.BodyWidth)),
            Math.Clamp(vertical ?? snapshot.OffsetY, 0d, Math.Max(0d, _sheet.ContentHeight - chrome.BodyHeight)));
        QueueNavigationRefresh();
    }

    private void CancelNavigationInput()
    {
        if (_navigationFrameAttached) CompositionTarget.Rendering -= OnNavigationFrame;
        _navigationFrameAttached = false;
        _pendingHorizontalOffset = null;
        _pendingVerticalOffset = null;
    }

    private void DisposeWorksheetNavigation()
    {
        CancelNavigationInput();
        _navigationRefresh?.Abort();
        _navigationRefresh = null;
        _horizontalNavigation.ValueChanged -= OnNavigationValueChanged;
        _verticalNavigation.ValueChanged -= OnNavigationValueChanged;
        _sheet.Loaded -= OnNavigationLoaded;
        _sheet.Unloaded -= OnNavigationUnloaded;
        _sheet.SizeChanged -= OnNavigationStateChanged;
        _sheet.ZoomChanged -= OnNavigationStateChanged;
        _sheet.ScrollChanged -= OnNavigationStateChanged;
        _session.Selection.Changed -= OnNavigationSelectionChanged;
        _session.ActiveWorksheetChanged -= OnNavigationWorksheetChanged;
        _session.View.Changed -= OnNavigationViewChanged;
        _session.View.SplitChanged -= OnNavigationSplitChanged;
        DetachNavigationWorksheet();
        _sheet.DisableSplitPanes();
        _splitShell = null;
    }
}
