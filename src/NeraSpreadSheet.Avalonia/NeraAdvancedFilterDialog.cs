using global::Avalonia.Controls;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Iconography;

namespace NeraSpreadSheet.Avalonia;

public sealed class NeraAdvancedFilterDialog : NeraSettingsDialog
{
    private readonly SpreadsheetAdvancedFilterController _controller;
    private readonly ComboBox _action;
    private readonly TextBox _listRange;
    private readonly TextBox _criteriaRange;
    private readonly TextBox _copyTo;
    private readonly CheckBox _unique;

    public NeraAdvancedFilterDialog(
        SpreadsheetSession session,
        PresentationLocalization? localization = null,
        NeraIconTheme theme = NeraIconTheme.Light)
        : base("Lọc nâng cao", "nera-advanced-filter-dialog", localization, theme)
    {
        ArgumentNullException.ThrowIfNull(session);
        Width = 640; Height = 500; MinWidth = 540; MinHeight = 420;
        _controller = new SpreadsheetAdvancedFilterController(session);
        var panel = Panel();
        _action = ChoiceField(panel, "advanced-filter-action", "Hành động",
            [("FilterInPlace", "Lọc danh sách tại chỗ"), ("CopyToAnotherLocation", "Sao chép sang vị trí khác")],
            SpreadsheetAdvancedFilterAction.FilterInPlace.ToString());
        var selected = session.Selection.Ranges.FirstOrDefault();
        _listRange = TextField(panel, "advanced-filter-list-range", "Vùng danh sách", selected.RowCount > 0 ? NeraDataReviewRangeParser.Format(selected) : null);
        _criteriaRange = TextField(panel, "advanced-filter-criteria-range", "Vùng điều kiện", null);
        _copyTo = TextField(panel, "advanced-filter-copy-to", "Sao chép đến", null);
        _unique = CheckField(panel, "advanced-filter-unique", "Chỉ lấy bản ghi duy nhất", false); _unique.IsThreeState = false;
        Note(panel, "Vùng điều kiện dùng hàng đầu làm tiêu đề; các hàng sau là điều kiện OR, các cột trên cùng một hàng là AND. Hỗ trợ =, <>, >, >=, <, <=.");
        DialogBody.Children.Add(panel);
        _action.SelectionChanged += (_, _) => RefreshState();
        RefreshState();
    }

    protected override bool TryApply()
    {
        var action = Enum.Parse<SpreadsheetAdvancedFilterAction>(Selected(_action)!);
        _controller.Apply(new SpreadsheetAdvancedFilterOptions
        {
            Action = action,
            ListRange = NeraDataReviewRangeParser.ParseRange(_listRange.Text),
            CriteriaRange = string.IsNullOrWhiteSpace(_criteriaRange.Text) ? null : NeraDataReviewRangeParser.ParseRange(_criteriaRange.Text),
            CopyTo = action == SpreadsheetAdvancedFilterAction.CopyToAnotherLocation ? NeraDataReviewRangeParser.ParseAddress(_copyTo.Text) : null,
            UniqueRecordsOnly = _unique.IsChecked == true,
        });
        return true;
    }

    private void RefreshState() =>
        _copyTo.IsEnabled = Selected(_action) == SpreadsheetAdvancedFilterAction.CopyToAnotherLocation.ToString();
}
