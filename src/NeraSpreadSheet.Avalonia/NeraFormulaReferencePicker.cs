using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Input;
using global::Avalonia.Layout;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia;

/// <summary>Read-only worksheet/range chooser for an existing formula draft.
/// Browsing never activates another worksheet in the editing session. Only Apply
/// inserts a reference into the same native draft; it never commits workbook data.</summary>
public sealed class NeraFormulaReferencePicker : UserControl, IDisposable
{
    private readonly NeraSpreadsheetControl _owner;
    private readonly SpreadsheetSession _session;
    private readonly CellEditState _editState;
    private readonly SpreadsheetEditorDraft _draft;
    private readonly long _workbookVersion;
    private readonly ComboBox _worksheets = new() { MinWidth = 220 };
    private readonly TextBlock _address = new();
    private readonly TextBlock _message = new() { TextWrapping = global::Avalonia.Media.TextWrapping.Wrap };
    private readonly FormulaReferenceSelectionSurface _surface;
    private bool _completed;
    private bool _disposed;

    public NeraFormulaReferencePicker(NeraSpreadsheetControl owner)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        if (!owner.IsFormulaDraft || owner.Session is not { } session || owner.CurrentEditorDraft is not { } draft)
            throw new InvalidOperationException("Start a formula draft before choosing a worksheet reference.");
        _session = session;
        _draft = draft;
        _editState = session.Editor.State!;
        _workbookVersion = session.Workbook.Version;
        _surface = new FormulaReferenceSelectionSurface(session.Workbook, session.ActiveWorksheet);
        var root = new DockPanel { Margin = new Thickness(10) };
        var top = new StackPanel { Spacing = 6, Margin = new Thickness(0, 0, 0, 8) };
        top.Children.Add(new TextBlock { Text = "Chọn trang tính rồi kéo vùng tham chiếu. Công thức đang nhập được giữ nguyên." });
        _worksheets.ItemsSource = session.Workbook.Worksheets;
        _worksheets.ItemTemplate = new FuncDataTemplate<Worksheet>((sheet, _) => new TextBlock { Text = sheet?.Name });
        _worksheets.SelectedItem = session.ActiveWorksheet;
        _worksheets.SelectionChanged += OnWorksheetSelected;
        top.Children.Add(_worksheets);
        top.Children.Add(_address);
        DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        var bottom = new StackPanel { Spacing = 6, Margin = new Thickness(0, 8, 0, 0) };
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var apply = new Button { Content = "Chèn tham chiếu" };
        var cancel = new Button { Content = "Hủy" };
        var zoomOut = new Button { Content = "−", Focusable = false };
        var zoomIn = new Button { Content = "+", Focusable = false };
        apply.Click += (_, _) => TryApply();
        cancel.Click += (_, _) => Cancel();
        zoomOut.Click += (_, _) => _surface.Zoom = Math.Max(0.25, _surface.Zoom - 0.1);
        zoomIn.Click += (_, _) => _surface.Zoom = Math.Min(4, _surface.Zoom + 0.1);
        actions.Children.Add(apply); actions.Children.Add(cancel); actions.Children.Add(zoomOut); actions.Children.Add(zoomIn);
        bottom.Children.Add(_message); bottom.Children.Add(actions);
        DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        root.Children.Add(_surface); Content = root;
        _surface.RangeChanged += OnRangeChanged;
        AutomationProperties.SetAutomationId(this, "nera-formula-reference-picker");
        AutomationProperties.SetAutomationId(_worksheets, "nera-reference-worksheet");
        AutomationProperties.SetAutomationId(apply, "nera-reference-apply");
        AutomationProperties.SetAutomationId(cancel, "nera-reference-cancel");
        OnRangeChanged(this, EventArgs.Empty);
    }

    public Worksheet SelectedWorksheet => _surface.Worksheet;
    public CellRange SelectedRange => _surface.SelectedRange;
    public long PreviewRenderCount => _surface.RenderCount;
    internal FormulaReferenceSelectionSurface PreviewSurface => _surface;
    public event EventHandler? Applied;
    public event EventHandler? Cancelled;

    public void SelectWorksheet(Worksheet worksheet)
    {
        VerifyUsable();
        ArgumentNullException.ThrowIfNull(worksheet);
        if (!_session.Workbook.Worksheets.Contains(worksheet)) throw new ArgumentException("Worksheet is not in this workbook.", nameof(worksheet));
        _worksheets.SelectedItem = worksheet;
        if (!ReferenceEquals(_surface.Worksheet, worksheet)) _surface.SetWorksheet(worksheet);
        OnRangeChanged(this, EventArgs.Empty);
    }
    public void SelectRange(CellRange range) { VerifyUsable(); _surface.SelectRange(range); }

    /// <summary>Returns false for a stale editor/workbook or invalid insertion
    /// position. It never resumes or creates a replacement editor automatically.</summary>
    public bool TryApply()
    {
        VerifyUsable();
        if (_completed || !_owner.IsEditing || !ReferenceEquals(_owner.Session, _session) ||
            !ReferenceEquals(_session.Editor.State, _editState) || _owner.CurrentEditorDraft != _draft ||
            _session.Workbook.Version != _workbookVersion || !_session.Workbook.Worksheets.Contains(SelectedWorksheet))
        {
            _message.Text = "Ngữ cảnh đã thay đổi. Đóng hộp chọn và mở lại từ công thức hiện tại.";
            return false;
        }
        if (!_owner.InsertFormulaReference(SelectedRange, SelectedWorksheet.Name))
        {
            _message.Text = "Đặt con trỏ tại vị trí nhận tham chiếu trong công thức rồi mở lại hộp chọn.";
            return false;
        }
        _completed = true;
        Applied?.Invoke(this, EventArgs.Empty);
        return true;
    }
    public void Cancel()
    {
        VerifyUsable();
        if (_completed) return;
        _completed = true;
        Cancelled?.Invoke(this, EventArgs.Empty);
    }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || _disposed) return;
        if (e.Key == Key.Escape) { e.Handled = true; Cancel(); }
    }
    private void OnWorksheetSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_disposed || _worksheets.SelectedItem is not Worksheet worksheet) return;
        _surface.SetWorksheet(worksheet); OnRangeChanged(this, EventArgs.Empty);
    }
    private void OnRangeChanged(object? sender, EventArgs e) =>
        _address.Text = $"{SelectedWorksheet.Name}: {SelectedRange.TopLeft.ToA1()}:{SelectedRange.BottomRight.ToA1()} · Cuộn chuột để di chuyển; giữ và kéo sát mép để mở rộng vùng.";
    private void VerifyUsable() { VerifyAccess(); ObjectDisposedException.ThrowIf(_disposed, this); }
    public void Dispose()
    {
        VerifyAccess(); if (_disposed) return; _disposed = true;
        _worksheets.SelectionChanged -= OnWorksheetSelected;
        _surface.RangeChanged -= OnRangeChanged; _surface.Dispose(); Content = null;
    }
}
