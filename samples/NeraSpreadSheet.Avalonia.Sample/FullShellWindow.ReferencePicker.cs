using global::Avalonia.Controls;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Avalonia.Sample;

public sealed partial class FullShellWindow
{
    /// <summary>Browses another sheet in a read-only projection. The editing
    /// session remains on the worksheet owning the formula until commit/cancel.</summary>
    public void ShowFormulaReferencePicker(Worksheet? worksheet = null)
    {
        if (_closed || _busy) return;
        var editor = DraftOwner;
        if (!editor.IsEditing) editor.BeginEdit("=");
        if (!editor.EditorText.StartsWith('='))
        {
            _status.Text = "Bắt đầu công thức bằng dấu = trước khi chọn tham chiếu.";
            return;
        }
        PushFormulaDraft();
        var picker = new NeraFormulaReferencePicker(editor);
        if (worksheet is not null) picker.SelectWorksheet(worksheet);
        var window = new Window { Title = "Chọn tham chiếu từ trang tính", Width = 960, Height = 720, MinWidth = 500, MinHeight = 400, Content = picker };
        picker.Applied += (_, _) => window.Close();
        picker.Cancelled += (_, _) => window.Close();
        _dialogs.Add(window);
        window.Closed += (_, _) =>
        {
            _dialogs.Remove(window); picker.Dispose();
            if (!_closed && editor.IsEditing) { RefreshSelection(); _formula.Focus(); }
        };
        window.Show(this);
    }
}
