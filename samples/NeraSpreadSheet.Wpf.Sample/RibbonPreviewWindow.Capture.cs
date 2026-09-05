using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Iconography;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Wpf.Sample;

public sealed partial class RibbonPreviewWindow
{
    private static readonly JsonSerializerOptions CaptureJsonOptions = new() { WriteIndented = true };
    /// <summary>
    /// Captures the real SDK presenter at reference widths and palettes. Export DPI
    /// is raster sampling, not a claim that the operating system changed monitor DPI.
    /// </summary>
    public async Task CaptureMatrixAsync(string outputDirectory, bool tableDesignOnly = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        Directory.CreateDirectory(outputDirectory);
        ShowInTaskbar = false;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        Left = -32000;
        Top = -32000;
        _root.CaptureFullLayout = true;
        _root.Height = 600;
        var images = new List<object>();
        var layouts = new List<object>();
        var selection = _session.Selection.ActiveCell;
        var before = _session.Styles.ActiveCellStyle.Font.Weight;
        if (!await _runtime.TryActivateAsync("Cell.Format.Bold") || _session.Styles.ActiveCellStyle.Font.Weight == before)
            throw new InvalidOperationException("Preview Bold command did not change the selected cell.");
        if (!await _runtime.TryActivateAsync("Edit.Undo") || _session.Styles.ActiveCellStyle.Font.Weight != before)
            throw new InvalidOperationException("Preview Undo did not restore the selected cell.");
        var tableBefore = CurrentTable ?? throw new InvalidOperationException("Preview table is not selected.");
        _session.Selection.SetActiveCell(new CellAddress(1, tableBefore.Range.Right));
        if (!await _runtime.TryActivateItemAsync("Table.TotalsFunction", "Average") || _session.TableDesign.Snapshot.TotalsFunction != SpreadsheetTableTotalsFunction.Average)
            throw new InvalidOperationException("Preview totals state did not reflect the real table formula.");
        if (!await _runtime.TryActivateAsync("Edit.Undo") || CurrentTable?.Columns[^1].TotalsRowFormula != tableBefore.Columns[^1].TotalsRowFormula)
            throw new InvalidOperationException("Preview Undo did not restore the table formula.");
        if (!await _runtime.TryActivateItemAsync("Table.Style", "TableStyleMedium3") || CurrentTable?.StyleName != "TableStyleMedium3")
            throw new InvalidOperationException("Production gallery did not change the table style.");
        if (!await _runtime.TryActivateAsync("Edit.Undo") || CurrentTable?.StyleName != tableBefore.StyleName)
            throw new InvalidOperationException("Undo did not restore the table style.");
        _session.Selection.SetActiveCell(selection);
        await CaptureTableDialogSmokeAsync(outputDirectory, images);
        _runtime.Refresh();
        SetStatus("Sẵn sàng · Lệnh chỉnh sửa dùng lịch sử Hoàn tác của workbook");
        var tabs = _runtime.Snapshot.Tabs.Select(tab => tab.Id).Where(id => !tableDesignOnly || id == "table-design").ToArray();
        foreach (var theme in Enum.GetValues<NeraIconTheme>())
        {
            SetTheme(theme);
            foreach (var width in new[] { 1920, 1600, 1280, 1024 })
            {
                _root.Width = width;
                Width = width + 32;
                await FlushCaptureAsync();
                _ribbon.Rebuild();
                await FlushCaptureAsync();
                foreach (var tabId in tabs)
                {
                    _ribbon.NativeTabControl.SelectedItem = _ribbon.NativeTabControl.Items.OfType<TabItem>()
                        .Single(tab => string.Equals(tab.Tag as string, tabId, StringComparison.Ordinal));
                    await FlushCaptureAsync();
                    _ribbon.Rebuild();
                    await FlushCaptureAsync();
                    if (_session.Selection.ActiveCell != selection)
                        throw new InvalidOperationException("Ribbon layout changed worksheet selection.");
                    var nativeLayout = _ribbon.LayoutSnapshot;
                    if (Math.Abs(_root.ActualWidth - width) > 0.01 ||
                        Math.Abs(_ribbon.ActualWidth - width) > 0.01 ||
                        Math.Abs(nativeLayout.AvailableWidth / nativeLayout.Scale - width) > 0.01 ||
                        LayoutInformation.GetLayoutClip(_root) is not null)
                        throw new InvalidOperationException("The loaded capture surface does not match the requested logical width.");
                    ValidateCaptureLayout(nativeLayout);
                    var filename = $"{theme.ToString().ToLowerInvariant()}-{width}-{tabId}.png";
                    SaveCapture(_root, Path.Combine(outputDirectory, filename), 1);
                    images.Add(new { file = filename, theme = theme.ToString(), logicalWidth = width, tab = tabId, exportScale = 1d });
                    layouts.Add(new
                    {
                        theme = theme.ToString(), logicalWidth = width, tab = tabId,
                        windowWidth = ActualWidth, rootWidth = _root.ActualWidth, ribbonWidth = _ribbon.ActualWidth,
                        rootClipWidth = LayoutInformation.GetLayoutClip(_root)?.Bounds.Width,
                        nativeScale = nativeLayout.Scale, ribbonHeight = _ribbon.ActualHeight,
                        groups = nativeLayout.Tabs.Single(tab => tab.Presentation.Id == tabId).Groups.Select(group => new
                        {
                            id = group.Presentation.Id, mode = group.Mode.ToString(),
                            width = group.Width / nativeLayout.Scale, captionY = group.CaptionY / nativeLayout.Scale,
                            items = group.Items.Select(item => new
                            {
                                id = item.Presentation.Command.CommandId.Value, size = item.Size.ToString(), item.Row, item.Column, item.RowSpan,
                                x = item.X / nativeLayout.Scale, y = item.Y / nativeLayout.Scale,
                                width = item.Width / nativeLayout.Scale, height = item.Height / nativeLayout.Scale,
                            }).ToArray(),
                        }).ToArray(),
                    });
                    if (width == 1280 && tabId is "home" or "table-design")
                    {
                        foreach (var scale in new[] { 1.25, 1.5, 2d })
                        {
                            var scaledName = FormattableString.Invariant($"{theme.ToString().ToLowerInvariant()}-{width}-{tabId}-export-{scale:0.##}x.png");
                            SaveCapture(_root, Path.Combine(outputDirectory, scaledName), scale);
                            images.Add(new { file = scaledName, theme = theme.ToString(), logicalWidth = width, tab = tabId, exportScale = scale });
                        }
                    }
                    if (width == 1280 && tabId == "table-design")
                    {
                        var more = CaptureDescendants<Button>(_ribbon).Single(button =>
                            AutomationProperties.GetAutomationId(button) == "ribbon-command-Table.Style-more");
                        more.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        await FlushCaptureAsync();
                        var popup = PresentationSource.CurrentSources.Cast<PresentationSource>()
                            .Where(source => source.Dispatcher == Dispatcher)
                            .Select(source => source.RootVisual).OfType<FrameworkElement>()
                            .Single(root => CaptureDescendants<ToggleButton>(root).Any(button =>
                                AutomationProperties.GetAutomationId(button) == "ribbon-command-Table.Style-popup-choice-TableStyleMedium2"));
                        var popupName = $"{theme.ToString().ToLowerInvariant()}-gallery-more.png";
                        SaveCapture(popup, Path.Combine(outputDirectory, popupName), 1);
                        images.Add(new { file = popupName, theme = theme.ToString(), tab = "gallery-more", exportScale = 1d });
                        if (theme == NeraIconTheme.Light)
                        {
                            var history = _session.History.UndoCount;
                            CaptureDescendants<ToggleButton>(popup).Single(button =>
                                AutomationProperties.GetAutomationId(button) == "ribbon-command-Table.Style-popup-choice-TableStyleDark1")
                                .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                            await FlushCaptureAsync();
                            if (CurrentTable?.StyleName != "TableStyleDark1" || _session.History.UndoCount != history + 1)
                                throw new InvalidOperationException("Native gallery choice did not dispatch one style mutation.");
                            if (!await _runtime.TryActivateAsync("Edit.Undo") || CurrentTable?.StyleName != tableBefore.StyleName)
                                throw new InvalidOperationException("Native gallery Undo failed.");
                            _session.Selection.SetActiveCell(new CellAddress(1, tableBefore.Range.Right));
                            await FlushCaptureAsync();
                            CaptureDescendants<ComboBox>(_ribbon).Single(combo =>
                                AutomationProperties.GetAutomationId(combo) == "ribbon-command-Table.TotalsFunction").SelectedValue = "Average";
                            await FlushCaptureAsync();
                            if (_session.TableDesign.Snapshot.TotalsFunction != SpreadsheetTableTotalsFunction.Average ||
                                _session.History.UndoCount != history + 1)
                                throw new InvalidOperationException("Native totals selection did not dispatch one mutation.");
                            if (!await _runtime.TryActivateAsync("Edit.Undo")) throw new InvalidOperationException("Native totals Undo failed.");
                            _session.Selection.SetActiveCell(selection);
                        }
                        else more.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                        await FlushCaptureAsync();
                    }
                }
                if (tableDesignOnly) continue;
                CaptureDescendants<Button>(_ribbon).Single(button => AutomationProperties.GetAutomationId(button) == "ribbon-file")
                    .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await FlushCaptureAsync();
                var fileName = $"{theme.ToString().ToLowerInvariant()}-{width}-file.png";
                SaveCapture(_root, Path.Combine(outputDirectory, fileName), 1);
                images.Add(new { file = fileName, theme = theme.ToString(), logicalWidth = width, tab = "file", exportScale = 1d });
                CaptureDescendants<Button>(_ribbon).Single(button => AutomationProperties.GetAutomationId(button) == "ribbon-file")
                    .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await FlushCaptureAsync();
            }
            if (tableDesignOnly) continue;
            var customization = new NeraRibbonCustomizationDialog(_runtime)
            {
                Owner = this, IconTheme = theme, ShowInTaskbar = false, Left = -32000, Top = -32000,
                WindowStartupLocation = WindowStartupLocation.Manual,
            };
            try
            {
                customization.Show();
                await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
                customization.UpdateLayout();
                var customizationName = $"{theme.ToString().ToLowerInvariant()}-customization.png";
                SaveCapture((FrameworkElement)customization.Content, Path.Combine(outputDirectory, customizationName), 1);
                images.Add(new { file = customizationName, theme = theme.ToString(), tab = "customization", exportScale = 1d });
            }
            finally { customization.Close(); }
        }
        var engine = new RibbonResponsiveLayoutEngine();
        foreach (var width in new[] { 1920d, 1600d, 1280d, 1024d, 820d })
        {
            string? baseline = null;
            foreach (var scale in new[] { 1d, 1.25, 1.5, 2d })
            {
                var layout = engine.Layout(_runtime.Snapshot, new RibbonLayoutRequest(width * scale, scale));
                ValidateCaptureLayout(layout);
                var modes = string.Join(";", layout.Tabs.SelectMany(tab => tab.Groups.Select(group =>
                    $"{tab.Presentation.Id}/{group.Presentation.Id}/{group.Mode}/{string.Join(',', group.Items.Select(item => item.Size))}")));
                baseline ??= modes;
                if (!string.Equals(baseline, modes, StringComparison.Ordinal))
                    throw new InvalidOperationException("Ribbon modes changed at equivalent logical width and export scale.");
            }
        }
        await CaptureLocalizationAsync(outputDirectory, images);
        var manifest = new
        {
            schemaVersion = 2, status = "success", preview = "Production Table Design commands; Nera-generated synthetic workbook",
            note = "Loaded offscreen logical-surface capture. OS-capped native window width is reported separately from arranged root/Ribbon width; this is not physical visible-window verification. Export scales are raster sampling; native DPI and 1/1.25/1.5/2 layout checks are separate.",
            commandSmoke = "Bold/Undo; Table.TotalsFunction/Average/Undo; Table.Style/mutation/Undo; dialog cancellation/validation; Create/Rename/Resize/CalculatedColumn/CustomTotals/RemoveDuplicates with Undo; ConvertToRange evaluated values and structured references/Undo", selection = selection.ToString(), images, layouts,
        };
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "manifest.json"),
            JsonSerializer.Serialize(manifest, CaptureJsonOptions));
    }

    private async Task FlushCaptureAsync()
    {
        _root.UpdateLayout();
        await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.Render);
        await Dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
        _root.UpdateLayout();
    }

    private static void SaveCapture(FrameworkElement element, string filename, double scale)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * scale),
            (int)Math.Ceiling(element.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawRectangle(Window.GetWindow(element)?.Background ?? Brushes.White, null,
                new Rect(0, 0, element.ActualWidth, element.ActualHeight));
            drawing.DrawRectangle(new VisualBrush(element)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                Viewbox = new Rect((Point)VisualTreeHelper.GetOffset(element), new Size(element.ActualWidth, element.ActualHeight)),
                Stretch = Stretch.Fill,
            }, null,
                new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        }
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(filename);
        encoder.Save(output);
    }

    private static void ValidateCaptureLayout(RibbonLayoutSnapshot layout)
    {
        const double tolerance = 0.01;
        foreach (var tab in layout.Tabs)
        {
            if (tab.InlineWidth > layout.AvailableWidth + tolerance)
                throw new InvalidOperationException($"Ribbon tab {tab.Presentation.Id} exceeds its available width.");
            foreach (var group in tab.Groups.Where(group => group.Mode != RibbonGroupLayoutMode.Overflow))
            {
                for (var index = 0; index < group.Items.Count; index++)
                {
                    var item = group.Items[index];
                    if (item.X < 0 || item.Y < 0 || item.X + item.Width > group.Width + tolerance ||
                        item.Y + item.Height > group.CaptionY + tolerance)
                        throw new InvalidOperationException($"Ribbon command {item.Presentation.Command.CommandId} escapes its bounds.");
                    foreach (var other in group.Items.Skip(index + 1))
                    {
                        if (item.X < other.X + other.Width - tolerance && other.X < item.X + item.Width - tolerance &&
                            item.Y < other.Y + other.Height - tolerance && other.Y < item.Y + item.Height - tolerance)
                            throw new InvalidOperationException("Ribbon command rectangles overlap.");
                    }
                }
            }
        }
    }

    private static IEnumerable<T> CaptureDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match) yield return match;
            foreach (var descendant in CaptureDescendants<T>(child)) yield return descendant;
        }
    }
}
