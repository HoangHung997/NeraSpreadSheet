using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Interactivity;
using global::Avalonia.Layout;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

public sealed class NeraConsolidateDialog : NeraSettingsDialog
{
    private readonly SpreadsheetSession _session;
    private readonly SpreadsheetConsolidationController _controller;
    private readonly ComboBox _function;
    private readonly TextBox _reference;
    private readonly ListBox _references;
    private readonly TextBox _destination;
    private readonly CheckBox _topRow;
    private readonly CheckBox _leftColumn;
    private readonly CheckBox _links;
    private readonly List<SpreadsheetConsolidationSource> _sources = [];

    public NeraConsolidateDialog(
        SpreadsheetSession session,
        PresentationLocalization? localization = null,
        NeraIconTheme theme = NeraIconTheme.Light)
        : base("Hợp nhất", "nera-consolidate-dialog", localization, theme)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _controller = new SpreadsheetConsolidationController(session);
        Width = 700; Height = 620; MinWidth = 580; MinHeight = 500;
        var panel = Panel();
        _function = ChoiceField(panel, "consolidate-function", "Hàm",
            Enum.GetValues<SpreadsheetConsolidationFunction>().Select(value => (value.ToString(), FunctionCaption(value))),
            SpreadsheetConsolidationFunction.Sum.ToString());
        var selected = session.Selection.Ranges.FirstOrDefault();
        _reference = TextField(panel, "consolidate-reference", "Tham chiếu", selected.RowCount > 0 ? NeraDataReviewRangeParser.Format(new SpreadsheetConsolidationSource(session.ActiveWorksheet, selected)) : null);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var add = new Button { Content = L("Thêm"), MinWidth = 80 };
        AutomationProperties.SetAutomationId(add, "consolidate-add-reference");
        var remove = new Button { Content = L("Xóa"), MinWidth = 80 };
        AutomationProperties.SetAutomationId(remove, "consolidate-remove-reference");
        buttons.Children.Add(add); buttons.Children.Add(remove); panel.Children.Add(buttons);
        panel.Children.Add(new TextBlock { Text = L("Tất cả tham chiếu"), Margin = new Thickness(0, 8, 0, 4) });
        _references = new ListBox { MinHeight = 130, MaxHeight = 180 };
        AutomationProperties.SetAutomationId(_references, "consolidate-reference-list");
        panel.Children.Add(_references);
        _destination = TextField(panel, "consolidate-destination", "Ô đích", session.Selection.ActiveCell.ToA1());
        _topRow = CheckField(panel, "consolidate-top-row", "Dùng nhãn hàng trên", false); _topRow.IsThreeState = false;
        _leftColumn = CheckField(panel, "consolidate-left-column", "Dùng nhãn cột trái", false); _leftColumn.IsThreeState = false;
        _links = CheckField(panel, "consolidate-create-links", "Tạo liên kết đến dữ liệu nguồn", false); _links.IsThreeState = false;
        Note(panel, "Có thể nhập Sheet1!A1:D20 hoặc 'Tên sheet'!A1:D20. Nhãn hàng/cột được ghép theo nội dung; không dùng nhãn thì ghép theo vị trí tương đối.");
        DialogBody.Children.Add(panel);

        add.Click += (_, _) => AddReference();
        remove.Click += (_, _) => RemoveReference();
    }

    protected override bool TryApply()
    {
        if (_sources.Count == 0) AddReference();
        _controller.Consolidate(new SpreadsheetConsolidationOptions
        {
            Function = Enum.Parse<SpreadsheetConsolidationFunction>(Selected(_function)!),
            Sources = _sources.ToArray(),
            Destination = NeraDataReviewRangeParser.ParseAddress(_destination.Text),
            UseTopRowLabels = _topRow.IsChecked == true,
            UseLeftColumnLabels = _leftColumn.IsChecked == true,
            CreateLinksToSource = _links.IsChecked == true,
        });
        return true;
    }

    private void AddReference()
    {
        var source = NeraDataReviewRangeParser.ParseSource(_session.Workbook, _session.ActiveWorksheet, _reference.Text);
        if (_sources.Any(existing => ReferenceEquals(existing.Worksheet, source.Worksheet) && existing.Range == source.Range)) return;
        _sources.Add(source);
        RefreshReferences();
    }

    private void RemoveReference()
    {
        if (_references.SelectedIndex is < 0 or >= int.MaxValue || _references.SelectedIndex >= _sources.Count) return;
        _sources.RemoveAt(_references.SelectedIndex);
        RefreshReferences();
    }

    private void RefreshReferences()
    {
        _references.Items.Clear();
        foreach (var source in _sources) _references.Items.Add(NeraDataReviewRangeParser.Format(source));
        if (_sources.Count != 0) _references.SelectedIndex = _sources.Count - 1;
    }

    private string FunctionCaption(SpreadsheetConsolidationFunction value) => value switch
    {
        SpreadsheetConsolidationFunction.Sum => L("Tổng"),
        SpreadsheetConsolidationFunction.Average => L("Trung bình"),
        SpreadsheetConsolidationFunction.Count => L("Đếm"),
        SpreadsheetConsolidationFunction.Maximum => L("Lớn nhất"),
        _ => L("Nhỏ nhất"),
    };
}
