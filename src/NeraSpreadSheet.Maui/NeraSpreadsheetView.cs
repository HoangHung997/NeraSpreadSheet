using Microsoft.Maui.Controls;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Maui;

public sealed class NeraSpreadsheetView : View
{
    public static readonly BindableProperty WorkbookProperty = BindableProperty.Create(
        nameof(Workbook),
        typeof(Workbook),
        typeof(NeraSpreadsheetView),
        default(Workbook));

    public Workbook? Workbook
    {
        get => (Workbook?)GetValue(WorkbookProperty);
        set => SetValue(WorkbookProperty, value);
    }
}
