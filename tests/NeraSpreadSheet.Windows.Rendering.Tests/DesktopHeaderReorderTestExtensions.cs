namespace NeraSpreadSheet.Windows.Rendering.Tests;

internal static class DesktopHeaderReorderTestExtensions
{
    internal static NeraSpreadSheet.WinForms.NeraSpreadsheetHeaderReorderController
        EnableHeaderReordering(
            this NeraSpreadSheet.WinForms.NeraSpreadsheetControl control) =>
        NeraSpreadSheet.WinForms.NeraSpreadsheetHeaderReorderExtensions
            .EnableHeaderReordering(control);

    internal static NeraSpreadSheet.Wpf.NeraSpreadsheetHeaderReorderController
        EnableHeaderReordering(
            this NeraSpreadSheet.Wpf.NeraSpreadsheetControl control) =>
        NeraSpreadSheet.Wpf.NeraSpreadsheetHeaderReorderExtensions
            .EnableHeaderReordering(control);
}
