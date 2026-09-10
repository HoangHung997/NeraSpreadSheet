using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Layout;
using global::Avalonia.Media;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.OpenXml;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    private readonly TextBlock _compatibilityText = new() { TextWrapping = TextWrapping.Wrap, MaxHeight = 76, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _compatibilityDetails = new() { Content = "Chi tiết tương thích", VerticalAlignment = VerticalAlignment.Center };
    private readonly Border _compatibilityNotice = new() { IsVisible = false, Padding = new Thickness(8), Background = new SolidColorBrush(Color.Parse("#FFF4CE")) };
    private string _loadedDocument = "Workbook mẫu — chưa mở file";
    private string? _loadFailure;

    private void InstallCompatibilityNotice(DockPanel root)
    {
        var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
        _compatibilityText.Foreground = Brushes.Black;
        _compatibilityDetails.Foreground = Brushes.Black;
        content.Children.Add(_compatibilityText); Grid.SetColumn(_compatibilityDetails, 1); content.Children.Add(_compatibilityDetails);
        _compatibilityNotice.Child = content;
        AutomationProperties.SetAutomationId(_compatibilityNotice, "nera-compatibility-notice");
        AutomationProperties.SetAutomationId(_compatibilityText, "nera-compatibility-message");
        AutomationProperties.SetAutomationId(_compatibilityDetails, "nera-compatibility-details");
        _compatibilityDetails.Click += (_, _) =>
        {
            var report = OpenXmlImportDiagnostics.Get(Session.Workbook);
            var detail = _loadFailure ?? string.Join(Environment.NewLine, report.Warnings.Select(warning =>
                $"{warning.Code} · {warning.WorksheetName ?? "styles.xml"} · {warning.Reference ?? "—"} · dxf={warning.DifferentialStyleId}\n{warning.Feature}: {warning.Message}"));
            ShowInformation("Khả năng tương thích XLSX — " + _loadedDocument, detail);
        };
        DockPanel.SetDock(_compatibilityNotice, Dock.Top); root.Children.Add(_compatibilityNotice);
    }

    // The file picker and the native evidence test share this exact import path.
    private async Task OpenCompatibleStreamAsync(Stream source, string displayName)
    {
        var previous = Session;
        try
        {
            var loaded = await Task.Run(async () => await _serializer.LoadSessionAsync(source,
                OpenXmlImportOptions.ForMode(OpenXmlImportMode.Compatibility)));
            if (_closed || !ReferenceEquals(Session, previous)) return;
            _split.Session = loaded;
            _loadedDocument = displayName; _loadFailure = null;
            Title = "NeraSpreadSheet — " + displayName;
            var report = OpenXmlImportDiagnostics.Get(loaded.Workbook);
            _compatibilityNotice.IsVisible = report.RequiresMetadataPreservation;
            _compatibilityText.Text = $"Đã mở {displayName}. Có {report.Warnings.Count + report.OmittedWarningCount} cảnh báo: " +
                "một số định dạng chỉ được giữ để lưu lại, chưa hiển thị đầy đủ. Có thể sửa ô; không đổi cấu trúc/bảng/bộ lọc trong chế độ này.";
            _runtime.Refresh(); _menu.Runtime.Refresh();
        }
        catch (Exception exception)
        {
            // UI boundary reports the failed operation, then rethrows. Never
            // return an empty workbook or replace the previous document on failure.
            if (!_closed)
            {
                _loadFailure = exception.Message;
                _compatibilityNotice.IsVisible = true;
                _compatibilityText.Text = $"Không mở được {displayName}. Vẫn đang hiển thị: {_loadedDocument}. File nguồn chưa thay đổi.";
            }
            throw;
        }
    }

    private Task SaveCompatibleStreamAsync(NeraSpreadSheet.Editing.SpreadsheetSession session, Stream destination) =>
        _serializer.SaveSessionAsync(session, destination, new OpenXmlExportOptions { PreserveUnknownParts = true });

    private bool RestrictMetadataCommand(CommandId id) =>
        OpenXmlImportDiagnostics.Get(Session.Workbook).RequiresMetadataPreservation &&
        (id.Value.StartsWith("Structure.", StringComparison.Ordinal) || id.Value.StartsWith("Data.", StringComparison.Ordinal) ||
         id.Value.StartsWith("Table.", StringComparison.Ordinal) || id.Value is "Cell.Merge" or "Cell.Unmerge");
}
