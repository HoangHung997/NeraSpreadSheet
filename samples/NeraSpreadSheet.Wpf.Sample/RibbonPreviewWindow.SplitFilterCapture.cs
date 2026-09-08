using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Wpf.Sample;

public sealed partial class RibbonPreviewWindow
{
    private static async Task CaptureSplitFilterAsync(string directory, List<object> images)
    {
        foreach (var worksheetFilter in new[] { false, true })
        {
            foreach (var pane in new[] { -1, 0, 1, 2, 3 })
                await CaptureFilterCaseAsync(directory, images, worksheetFilter, pane, 1280, "initial");
            await CaptureFilterCaseAsync(directory, images, worksheetFilter, 3, 640, "narrow");
            await CaptureFilterCaseAsync(directory, images, worksheetFilter, 3, 1280,
                worksheetFilter ? "recreated" : "resized-hidden");
        }
    }

    private static async Task CaptureFilterCaseAsync(string directory, List<object> images, bool worksheetFilter,
        int paneIndex, int width, string transition)
    {
        var workbook = new Workbook();
        var worksheet = workbook.Worksheets[0];
        var header = new CellAddress(2, 1);
        worksheet.SetValue(header, "Giá trị");
        for (var index = 0; index < 250; index++) worksheet.SetValue(new CellAddress(index + 3, 1), $"Giá trị {index:000}");
        var range = new CellRange(header, new CellAddress(252, 1));
        if (worksheetFilter) worksheet.SetAutoFilter(new WorksheetAutoFilter(range));
        else worksheet.AddTable(new SpreadsheetTable(Guid.NewGuid(), "FilterCapture", range,
            [new SpreadsheetTableColumn(Guid.NewGuid(), "Giá trị")]));
        worksheet.Dimensions.SetColumnWidth(1, 160);
        worksheet.Dimensions.HideRows(0);
        worksheet.Dimensions.HideColumns(0);
        var session = new SpreadsheetSession(workbook);
        session.Selection.SetActiveCell(header);
        if (paneIndex >= 0) session.View.SetSplitState(new SpreadsheetSplitViewState(SpreadsheetSplitViewMode.Both,
            230.5, 150.25, (SpreadsheetSplitViewPane)paneIndex,
            new(0.25, 0.5), new(0.5, 0.75), new(0.75, 1.25), new(1.25, 1.5)));
        using var window = new RibbonPreviewWindow(session, "Bộ lọc · Phân trang và vùng hiển thị")
        {
            Width = width + 32, Height = 760, Left = -32000, Top = -32000, ShowInTaskbar = false,
        };
        try
        {
            window._root.CaptureFullLayout = true;
            window._root.Width = width;
            window._root.Height = 720;
            window.Show();
            await window.FlushCaptureAsync();
            window._splitShell?.RenderNow();
            await window.FlushCaptureAsync();
            if (!window._filterPopup!.TryOpenForActiveCell()) throw new InvalidOperationException("Filter capture did not open on the active native surface.");
            await FindFilterPopupAsync(window);
            if (transition == "resized-hidden")
            {
                window._splitShell!.SetSplit(250.75, 140.5);
                worksheet.Dimensions.HideRows(1);
                window._splitShell.RenderNow();
                await window.FlushCaptureAsync();
                if (!window._filterPopup.IsOpen) throw new InvalidOperationException("A visible filter anchor was lost during resize.");
            }
            else if (transition == "recreated")
            {
                var previous = window._splitShell;
                session.ActivateWorksheet(workbook.AddWorksheet("Other"));
                if (window._filterPopup.IsOpen) throw new InvalidOperationException("Sheet switch retained the old native filter popup.");
                await window.FlushCaptureAsync();
                session.ActivateWorksheet(worksheet);
                session.Selection.SetActiveCell(header);
                await window.FlushCaptureAsync();
                window._splitShell!.RenderNow();
                await window.FlushCaptureAsync();
                if (ReferenceEquals(previous, window._splitShell) || !window._filterPopup.TryOpenForActiveCell())
                    throw new InvalidOperationException("Filter capture did not bind to the recreated split surface.");
            }
            var popup = await FindFilterPopupAsync(window);
            var count = CaptureDescendants<CheckBox>(popup).Count();
            if (count != 100 || session.History.UndoCount != 0 || !session.TryResolveActiveAutoFilterTarget(out var target))
                throw new InvalidOperationException("Filter capture did not retain one bounded page and canonical target/history.");
            var chrome = SpreadsheetChromeGeometry.Calculate(window._sheet.ActualWidth, window._sheet.ActualHeight, window._sheet.RenderTheme);
            var paneClip = new RectD(chrome.RowHeaderWidth, chrome.ColumnHeaderHeight, chrome.BodyWidth, chrome.BodyHeight);
            RectD? buttonAnchor = null;
            var offset = new PointD(window._sheet.ScrollSnapshot.OffsetX, window._sheet.ScrollSnapshot.OffsetY);
            if (window._splitShell is { } split)
            {
                var frame = split.LastFrame!;
                var pane = frame.Panes.Single(item => item.Pane.PaneId == split.ActivePane);
                var right = frame.ScrollBars.TryGetBar(split.ActivePane, SpreadsheetScrollBarOrientation.Vertical, out var vertical)
                    ? Math.Min(pane.Pane.Bounds.Right, vertical.Bounds.Left) : pane.Pane.Bounds.Right;
                var bottom = frame.ScrollBars.TryGetBar(split.ActivePane, SpreadsheetScrollBarOrientation.Horizontal, out var horizontal)
                    ? Math.Min(pane.Pane.Bounds.Bottom, horizontal.Bounds.Top) : pane.Pane.Bounds.Bottom;
                paneClip = new RectD(pane.Pane.Bounds.X + chrome.RowHeaderWidth, pane.Pane.Bounds.Y + chrome.ColumnHeaderHeight,
                    Math.Max(0, right - pane.Pane.Bounds.X), Math.Max(0, bottom - pane.Pane.Bounds.Y));
                offset = split.GetPaneScroll(split.ActivePane);
                var hit = SpreadsheetAutoFilterButtonGeometry.GetVisibleButtons(worksheet.Tables, worksheet.AutoFilter,
                    pane.ViewportFrame.Layout, window._sheet.RenderTheme).Single();
                buttonAnchor = hit.Bounds.Translate(pane.Pane.Bounds.X + chrome.RowHeaderWidth, pane.Pane.Bounds.Y + chrome.ColumnHeaderHeight).Intersect(paneClip);
                if (buttonAnchor.Value.IsEmpty) throw new InvalidOperationException("The filter header is outside the visible pane body.");
                if (!window._filterPopup.TryOpenAt(buttonAnchor.Value.Left + buttonAnchor.Value.Width / 2,
                    buttonAnchor.Value.Top + buttonAnchor.Value.Height / 2))
                    throw new InvalidOperationException("The captured pane header is not a native filter hit.");
                popup = await FindFilterPopupAsync(window);
            }
            var owner = worksheetFilter ? "worksheet" : "table";
            var paneName = paneIndex < 0 ? "standalone" : ((SpreadsheetPaneId)paneIndex).ToString().ToLowerInvariant();
            var prefix = $"release009-split-filter-{owner}-{paneName}-{width}-{transition}";
            var popupPosition = window._sheet.PointFromScreen(popup.PointToScreen(new Point(0, 0)));
            var shellFile = prefix + ".png";
            SaveCapture(window._root, Path.Combine(directory, shellFile), 1d);
            images.Add(new { file = shellFile, tab = "split-filter", owner, pane = paneName, logicalWidth = width,
                transition, header = target.HeaderCell.ToString(), target.TableId, target.TableColumnId,
                coordinateSpace = "native-surface-dip", paneClip, buttonAnchor, offset, popupX = popupPosition.X, popupY = popupPosition.Y,
                pageOffset = 0, pageCount = count, sourceCount = 250, history = session.History.UndoCount, exportScale = 1d });
            var popupFile = prefix + "-popup.png";
            SaveCapture(popup, Path.Combine(directory, popupFile), 1d);
            images.Add(new { file = popupFile, tab = "split-filter-popup", owner, pane = paneName,
                transition, pageOffset = 0, pageCount = count, sourceCount = 250, exportScale = 1d });
            window._filterPopup.Close();
            await window.FlushCaptureAsync();
            if (window._filterPopup.IsOpen || session.History.UndoCount != 0)
                throw new InvalidOperationException("Filter capture close changed history or retained its popup.");
        }
        finally { window.Close(); }
    }

    private static async Task<FrameworkElement> FindFilterPopupAsync(RibbonPreviewWindow window)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await window.FlushCaptureAsync();
            var popup = PresentationSource.CurrentSources.Cast<PresentationSource>()
                .Where(source => source.Dispatcher == window.Dispatcher).Select(source => source.RootVisual)
                .OfType<FrameworkElement>().SelectMany(CaptureDescendants<Border>)
                .FirstOrDefault(element => AutomationProperties.GetAutomationId(element) == "NeraAutoFilterPagedPopup");
            if (popup is not null && CaptureDescendants<CheckBox>(popup).Count() == 100) return popup;
            await Task.Delay(20);
        }
        throw new InvalidOperationException("The native filter popup did not publish its 100-value page.");
    }
}
