using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Wpf.Sample;

public sealed partial class RibbonPreviewWindow
{
    private async Task CaptureLocalizationAsync(string outputDirectory, List<object> images)
    {
        var selection = _session.Selection.ActiveCell;
        var history = _session.History.UndoCount;
        var resources = _runtime.Localization;
        try
        {
            _session.Selection.SetActiveCell(new CellAddress(1, 0));
            foreach (var theme in Enum.GetValues<NeraIconTheme>())
            {
                Console.Error.WriteLine($"Capture: localization {theme}.");
                SetTheme(theme);
                foreach (var width in new[] { 1024, 1920 })
                {
                    _root.Width = width;
                    Width = width + 32;
                    _runtime.SetLocalization(new PresentationLocalization(CultureInfo.GetCultureInfo("vi-VN"),
                        static (key, _) => key == "Đổi tên Bảng" ? "Đổi tên bảng dữ liệu trong sổ tính" : null));
                    await FlushCaptureAsync();
                    _ribbon.NativeTabControl.SelectedItem = _ribbon.NativeTabControl.Items.OfType<TabItem>()
                        .Single(tab => tab.Tag as string == "table-design");
                    await FlushCaptureAsync();
                    ValidateCaptureLayout(_ribbon.LayoutSnapshot);
                    foreach (var scale in new[] { 1d, 1.25d, 1.5d, 2d })
                    {
                        var file = $"ux006-{theme.ToString().ToLowerInvariant()}-{width}-long-label-{scale.ToString(CultureInfo.InvariantCulture)}.png";
                        SaveCapture(_root, Path.Combine(outputDirectory, file), scale);
                        images.Add(new { file, theme = theme.ToString(), logicalWidth = width, tab = "localized-table-design", exportScale = scale });
                    }
                }
                _runtime.SetLocalization(resources);
                Console.Error.WriteLine($"Capture: Filter {theme}.");
                _filterPopup ??= new NeraAutoFilterPagedPopupPresenter(_sheet);
                _filterPopup.Localization = resources;
                _filterPopup.IconTheme = theme;
                if (!_filterPopup.TryOpenForActiveCell()) throw new InvalidOperationException("Localized Filter popup could not open.");
                FrameworkElement? popup = null;
                for (var attempt = 0; attempt < 100; attempt++)
                {
                    await FlushCaptureAsync();
                    popup = PresentationSource.CurrentSources.Cast<PresentationSource>()
                        .Where(source => source.Dispatcher == Dispatcher)
                        .Select(source => source.RootVisual).OfType<FrameworkElement>()
                        .SelectMany(CaptureDescendants<Border>)
                        .FirstOrDefault(element => AutomationProperties.GetAutomationId(element) == "NeraAutoFilterPagedPopup");
                    if (popup is not null && CaptureDescendants<CheckBox>(popup).Any()) break;
                    await Task.Delay(20);
                }
                if (popup is null || !CaptureDescendants<CheckBox>(popup).Any())
                    throw new InvalidOperationException("Localized Filter popup did not publish its native value page.");
                foreach (var scale in new[] { 1d, 1.25d, 1.5d, 2d })
                {
                    var file = $"ux006-{theme.ToString().ToLowerInvariant()}-filter-{scale.ToString(CultureInfo.InvariantCulture)}.png";
                    SaveCapture(popup, Path.Combine(outputDirectory, file), scale);
                    images.Add(new { file, theme = theme.ToString(), tab = "localized-filter", exportScale = scale });
                }
                CaptureDescendants<Button>(popup).Single(button =>
                    AutomationProperties.GetAutomationId(button) == "NeraAutoFilterPagedCancel")
                    .RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                await FlushCaptureAsync();
                if (_filterPopup.IsOpen || _session.History.UndoCount != history)
                    throw new InvalidOperationException("Filter capture cancellation changed history or left the popup open.");
            }
            _runtime.SetLocalization(new PresentationLocalization(CultureInfo.GetCultureInfo("en-GB")));
            await FlushCaptureAsync();
            if (_ribbon.FileCaption != "File") throw new InvalidOperationException("The native File caption did not switch culture.");
            const string cultureFile = "ux006-en-gb-table-design.png";
            SaveCapture(_root, Path.Combine(outputDirectory, cultureFile), 1d);
            images.Add(new { file = cultureFile, culture = "en-GB", tab = "localized-table-design", exportScale = 1d });
        }
        finally
        {
            _filterPopup?.Close();
            _runtime.SetLocalization(resources);
            _session.Selection.SetActiveCell(selection);
        }
    }
}
