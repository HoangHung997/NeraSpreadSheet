using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Wpf.Sample;

public sealed partial class RibbonPreviewWindow
{
    private bool _synchronizingWorksheetTabs;

    private ListBox CreateWorksheetTabs()
    {
        var panel = new FrameworkElementFactory(typeof(VirtualizingStackPanel));
        panel.SetValue(VirtualizingStackPanel.OrientationProperty, Orientation.Horizontal);
        var itemStyle = new Style(typeof(ListBoxItem));
        itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(14, 5, 14, 5)));
        itemStyle.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 72d));
        itemStyle.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Center));
        var tabs = new ListBox
        {
            ItemsSource = _session.Workbook.Worksheets,
            DisplayMemberPath = nameof(Worksheet.Name),
            SelectedItem = _session.ActiveWorksheet,
            SelectionMode = SelectionMode.Single,
            ItemsPanel = new ItemsPanelTemplate(panel),
            ItemContainerStyle = itemStyle,
            BorderThickness = new Thickness(0, 1, 0, 0),
            MinHeight = 32,
        };
        ScrollViewer.SetHorizontalScrollBarVisibility(tabs, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(tabs, ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetIsVirtualizing(tabs, true);
        VirtualizingPanel.SetVirtualizationMode(tabs, VirtualizationMode.Recycling);
        AutomationProperties.SetName(tabs, Localization.Get("Trang tính hiện tại"));
        AutomationProperties.SetAutomationId(tabs, "preview-worksheet");
        tabs.SelectionChanged += OnWorksheetTabSelectionChanged;
        tabs.SizeChanged += OnWorksheetTabsSizeChanged;
        return tabs;
    }

    private void OnWorksheetTabsSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_disposed && e.WidthChanged && _worksheetTabs.SelectedItem is Worksheet worksheet)
            _worksheetTabs.ScrollIntoView(worksheet);
    }

    private void OnWorksheetTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_disposed || _synchronizingWorksheetTabs || _worksheetTabs.SelectedItem is not Worksheet worksheet) return;
        // Keep selection/history/editor semantics in the existing session, including
        // cancellation of an uncommitted cell draft when changing worksheets.
        _session.ActivateWorksheet(worksheet);
        _worksheetTabs.ScrollIntoView(worksheet);
    }

    private void SynchronizeWorksheetTabs()
    {
        if (ReferenceEquals(_worksheetTabs.SelectedItem, _session.ActiveWorksheet)) return;
        // The workbook list is not observable. Refresh only when activation targets
        // another worksheet, never rebuild the tab row on every cell selection.
        _synchronizingWorksheetTabs = true;
        try
        {
            _worksheetTabs.Items.Refresh();
            _worksheetTabs.SelectedItem = _session.ActiveWorksheet;
            _worksheetTabs.ScrollIntoView(_session.ActiveWorksheet);
        }
        finally { _synchronizingWorksheetTabs = false; }
    }
}
