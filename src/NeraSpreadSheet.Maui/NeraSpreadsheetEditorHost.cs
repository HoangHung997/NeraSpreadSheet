using System.ComponentModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Graphics;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using SkiaSharp.Views.Maui;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Optional in-cell editing shell for an existing GPU view. Reuses exactly one
/// native Editor and the view's canonical Session.Editor; the caller owns the view.
/// </summary>
public sealed partial class NeraSpreadsheetEditorHost : Grid, IDisposable
{
    private readonly AbsoluteLayout _overlay = new() { InputTransparent = true, CascadeInputTransparent = false };
    private readonly Editor _editor = new NeraCellEditor { IsVisible = false, AutoSize = EditorAutoSizeOption.Disabled };
    private readonly VerticalStackLayout _suggestions = new() { IsVisible = false, Spacing = 0 };
    private readonly ScrollView _suggestionScroll = new() { IsVisible = false };
    private readonly TapGestureRecognizer _editGesture = new() { NumberOfTapsRequired = 2 };
    private readonly HorizontalStackLayout _actions = new() { IsVisible = false, Spacing = 4 };
    private readonly List<Button> _candidateButtons = [];
    private readonly Button _commit = new();
    private readonly Button _cancel = new();
    private readonly Button _newline = new();
    private SpreadsheetSession? _session;
    private IReadOnlyList<FormulaStructuredReferenceSuggestion> _candidates = [];
    private FormulaTextSpan? _referenceSpan;
    private bool _updating;
    private bool _disposed;
    private int _selectedCandidate;
    private double _editorFontSize;

    /// <summary>Wraps a view that has not yet been attached to another layout.</summary>
    public NeraSpreadsheetEditorHost(NeraSpreadsheetView spreadsheet)
    {
        Spreadsheet = spreadsheet ?? throw new ArgumentNullException(nameof(spreadsheet));
        ((NeraCellEditor)_editor).HandleKey = HandleEditorKey;
        if (spreadsheet.Parent is not null) throw new ArgumentException("The spreadsheet already has a parent.", nameof(spreadsheet));
        Children.Add(spreadsheet);
        Children.Add(_overlay);
        _overlay.Children.Add(_editor);
        _suggestionScroll.Content = _suggestions;
        _overlay.Children.Add(_suggestionScroll);
        _overlay.Children.Add(_actions);
        _actions.Children.Add(_commit);
        _actions.Children.Add(_newline);
        _actions.Children.Add(_cancel);
        _commit.Clicked += OnCommit;
        _cancel.Clicked += OnCancel;
        _newline.Clicked += OnNewline;
        for (var index = 0; index < 12; index++)
        {
            var button = new Button { IsVisible = false, Padding = new Thickness(8, 2), MinimumHeightRequest = 24 };
            button.Clicked += OnCandidateClicked;
            _candidateButtons.Add(button);
            _suggestions.Children.Add(button);
        }
        _editor.TextChanged += OnTextChanged;
        _editor.PropertyChanged += OnEditorPropertyChanged;
        _editor.HandlerChanged += OnEditorHandlerChanged;
        _editor.HandlerChanging += OnEditorHandlerChanging;
        spreadsheet.PropertyChanged += OnSpreadsheetPropertyChanged;
        spreadsheet.PaintSurface += OnFrame;
        _editGesture.Tapped += OnEditGesture;
        spreadsheet.GestureRecognizers.Add(_editGesture);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnHostSizeChanged;
        SetEnglishResources(false);
        SynchronizeSession();
    }

    /// <summary>Gets the unchanged GPU-backed spreadsheet view owned by the caller.</summary>
    public NeraSpreadsheetView Spreadsheet { get; }
    /// <summary>Gets whether this shell is displaying an active session edit.</summary>
    public bool IsEditing => _editor.IsVisible && _session?.Editor.IsEditing == true;
    /// <summary>Gets the native draft without committing it.</summary>
    public string? CurrentEditText => _session?.Editor.IsEditing == true ? _editor.Text : null;
    /// <summary>Gets at most twelve metadata-only Table/column candidates.</summary>
    public IReadOnlyList<FormulaStructuredReferenceSuggestion> CurrentStructuredReferenceSuggestions => _candidates;

    /// <summary>Applies shell-local resources; hosts can replace entries in Resources.</summary>
    public void SetEnglishResources(bool english)
    {
        Resources["CellEditor.Commit"] = english ? "Commit" : "Hoàn tất";
        Resources["CellEditor.Cancel"] = english ? "Cancel" : "Hủy";
        Resources["CellEditor.Newline"] = english ? "New line" : "Xuống dòng";
        Resources["CellEditor.Name"] = english ? "Cell editor" : "Chỉnh sửa ô";
        _commit.SetDynamicResource(Button.TextProperty, "CellEditor.Commit");
        _cancel.SetDynamicResource(Button.TextProperty, "CellEditor.Cancel");
        _newline.SetDynamicResource(Button.TextProperty, "CellEditor.Newline");
        _editor.SetDynamicResource(SemanticProperties.DescriptionProperty, "CellEditor.Name");
    }

    /// <summary>Begins editing the active cell with one reused native overlay.</summary>
    public bool BeginEdit(string? replacementText = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        SynchronizeSession();
        if (_session is null) return false;
        var address = _session.Selection.ActiveCell;
        if (!Spreadsheet.TryGetEditorBounds(address, out _, out _)) return false;
        var state = _session.Editor.BeginEdit();
        var style = _session.ActiveWorksheet.GetEffectiveStyle(state.Address, _session.Workbook.Styles);
        _editor.FontFamily = style.Font.Family;
        _editorFontSize = style.Font.Size;
        _editor.FontSize = _editorFontSize * Spreadsheet.Zoom;
        _editor.FontAttributes = (style.Font.Weight >= 700 ? FontAttributes.Bold : FontAttributes.None) |
            (style.Font.Italic ? FontAttributes.Italic : FontAttributes.None);
        _editor.TextColor = Color.FromRgba(style.Font.Color.Red, style.Font.Color.Green, style.Font.Color.Blue, style.Font.Color.Alpha);
        _referenceSpan = null;
        SetDraft(replacementText ?? state.InitialText, (replacementText ?? state.InitialText).Length);
        _editor.IsVisible = true;
        _actions.IsVisible = true;
        UpdateBounds();
        UpdateSuggestions();
        _editor.Focus();
        return true;
    }

    /// <summary>Commits through the session's validation/history/incremental calculation path.</summary>
    public bool CommitEditor()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session?.Editor.State is not { } state || !_session.Editor.Commit(_editor.Text ?? string.Empty)) return false;
        HideEditor();
        _session.Selection.SetActiveCell(SpreadsheetVisibleCellNavigation.GetNextVisibleCell(
            _session.ActiveWorksheet, state.Address, 1, 0));
        Spreadsheet.Focus();
        return true;
    }

    /// <summary>Cancels the draft without adding a history operation.</summary>
    public bool CancelEditor()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session?.Editor.Cancel() != true) return false;
        HideEditor();
        Spreadsheet.Focus();
        return true;
    }

    /// <summary>Inserts a newline at the native caret, replacing its selection.</summary>
    public void InsertNewline()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session?.Editor.IsEditing != true) return;
        var text = _editor.Text ?? string.Empty;
        var start = Math.Clamp(_editor.CursorPosition, 0, text.Length);
        var length = Math.Clamp(_editor.SelectionLength, 0, text.Length - start);
        SetDraft(string.Concat(text.AsSpan(0, start), "\n", text.AsSpan(start + length)), start + 1);
        _referenceSpan = null;
        UpdateSuggestions();
    }

    /// <summary>Applies a displayed candidate; stale UI state is consumed without committing.</summary>
    public bool AcceptStructuredReferenceSuggestion(int index)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session?.Editor.State is not { } state || index < 0 || index >= _candidates.Count) return false;
        if (SpreadsheetFormulaEditingAssistant.TryApplyStructuredReferenceSuggestion(_editor.Text ?? string.Empty,
                _editor.CursorPosition, _editor.SelectionLength, _session.Workbook, _session.ActiveWorksheet,
                state.Address, _candidates[index], out var edit))
            SetDraft(edit!.Text, edit.CaretIndex);
        _referenceSpan = null;
        UpdateSuggestions();
        _editor.Focus();
        return true;
    }

    /// <summary>Inserts an exact Table area or A1 range into the existing native draft.</summary>
    public bool InsertFormulaReference(CellRange range)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_referenceSpan is { } span && (_editor.CursorPosition != span.End || _editor.SelectionLength != 0)) _referenceSpan = null;
        if (_session?.Editor.State is not { } state || !SpreadsheetFormulaEditingAssistant.CanInsertReference(
                _editor.Text ?? string.Empty, _editor.CursorPosition, _referenceSpan)) return false;
        var edit = SpreadsheetFormulaEditingAssistant.InsertReference(_editor.Text ?? string.Empty, _editor.CursorPosition,
            _session.Workbook, _session.ActiveWorksheet, state.Address, _session.ActiveWorksheet, range, _referenceSpan);
        SetDraft(edit.Text, edit.CaretIndex);
        _referenceSpan = edit.InsertedSpan;
        UpdateSuggestions();
        return true;
    }

    private void SetDraft(string text, int caret)
    {
        _updating = true;
        try { _editor.Text = text; _editor.CursorPosition = caret; _editor.SelectionLength = 0; }
        finally { _updating = false; }
    }

    private void UpdateSuggestions()
    {
        _candidates = _session?.Editor.State is { } state && _editor.SelectionLength == 0
            ? SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(_editor.Text ?? string.Empty,
                Math.Clamp(_editor.CursorPosition, 0, (_editor.Text ?? string.Empty).Length), _session.Workbook,
                _session.ActiveWorksheet, state.Address) : [];
        _selectedCandidate = 0;
        for (var index = 0; index < _candidateButtons.Count; index++)
        {
            _candidateButtons[index].IsVisible = index < _candidates.Count;
            if (index < _candidates.Count) _candidateButtons[index].Text = _candidates[index].DisplayText;
        }
        _suggestions.IsVisible = _editor.IsVisible && _candidates.Count > 0;
        UpdateBounds();
    }

    private void UpdateBounds()
    {
        if (_disposed) return;
        if (_session?.Editor.State is not { } state) return;
        _editor.FontSize = _editorFontSize * Spreadsheet.Zoom;
        if (!Spreadsheet.TryGetEditorBounds(state.Address, out var raw, out var clip))
        {
            _editor.IsVisible = _suggestions.IsVisible = _suggestionScroll.IsVisible = _actions.IsVisible = false;
            return;
        }
        AbsoluteLayout.SetLayoutBounds(_editor, new Rect(raw.X, raw.Y, raw.Width, raw.Height));
        var clipRect = new Rect(clip.X - raw.X, clip.Y - raw.Y, clip.Width, clip.Height);
        if (_editor.Clip is not RectangleGeometry currentClip || currentClip.Rect != clipRect)
            _editor.Clip = new RectangleGeometry { Rect = clipRect };
        _editor.IsVisible = true;
        _actions.IsVisible = true;
        _suggestions.IsVisible = _candidates.Count > 0;
        _suggestionScroll.IsVisible = _suggestions.IsVisible;
        var width = Math.Min(300d, Math.Max(0, Width));
        var listHeight = Math.Min(240d, _candidates.Count * 32d);
        var x = Math.Clamp(clip.X, 0, Math.Max(0, Width - width));
        var y = Math.Clamp(clip.Bottom, 0, Math.Max(0, Height - listHeight - 44d));
        AbsoluteLayout.SetLayoutBounds(_suggestionScroll, new Rect(x, y, width, listHeight));
        AbsoluteLayout.SetLayoutBounds(_actions, new Rect(x, y + listHeight, width, 44d));
    }

    private void HideEditor()
    {
        _editor.Unfocus();
        _editor.IsVisible = _suggestions.IsVisible = _suggestionScroll.IsVisible = _actions.IsVisible = false;
        _candidates = [];
        _referenceSpan = null;
        SetDraft(string.Empty, 0);
    }

    private void SynchronizeSession()
    {
        if (ReferenceEquals(_session, Spreadsheet.Session)) return;
        if (_session is not null)
        {
            _session.Editor.Cancel();
            _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
        }
        HideEditor();
        _session = Spreadsheet.Session;
        if (_session is not null) _session.ActiveWorksheetChanged += OnActiveWorksheetChanged;
    }

    private void OnActiveWorksheetChanged(object? sender, EventArgs e) { _session?.Editor.Cancel(); HideEditor(); }
    private void OnSpreadsheetPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NeraSpreadsheetView.Workbook)) SynchronizeSession();
    }
    private void OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_updating) return;
        _referenceSpan = null;
        UpdateSuggestions();
    }
    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_updating || e.PropertyName is not (nameof(Editor.CursorPosition) or nameof(Editor.SelectionLength))) return;
        if (_referenceSpan is { } span && (_editor.CursorPosition != span.End || _editor.SelectionLength != 0)) _referenceSpan = null;
        UpdateSuggestions();
    }
    private void OnFrame(object? sender, SKPaintGLSurfaceEventArgs e)
    {
        if (!_disposed && _session?.Editor.IsEditing == true) Dispatcher.Dispatch(UpdateBounds);
    }
    private void OnCandidateClicked(object? sender, EventArgs e)
    {
        if (sender is Button button) AcceptStructuredReferenceSuggestion(_candidateButtons.IndexOf(button));
    }
    private void OnCommit(object? sender, EventArgs e) => CommitEditor();
    private void OnCancel(object? sender, EventArgs e) => CancelEditor();
    private void OnNewline(object? sender, EventArgs e) => InsertNewline();
    private void OnEditGesture(object? sender, TappedEventArgs e) => BeginEdit();
    private void OnLoaded(object? sender, EventArgs e) => AttachNativeEditor();
    private void OnHostSizeChanged(object? sender, EventArgs e) => UpdateBounds();
    private void OnUnloaded(object? sender, EventArgs e) { _session?.Editor.Cancel(); HideEditor(); DetachNativeEditor(); }
    private void OnEditorHandlerChanged(object? sender, EventArgs e) => AttachNativeEditor();
    private void OnEditorHandlerChanging(object? sender, HandlerChangingEventArgs e) => DetachNativeEditor();
    partial void AttachNativeEditor();
    partial void DetachNativeEditor();

    private bool HandleEditorKey(string key, bool alt, bool shift, bool control)
    {
        if (_disposed || _session?.Editor.IsEditing != true || control) return false;
        switch (key)
        {
            case "Enter":
                if (alt) InsertNewline();
                else CommitEditor();
                return true;
            case "Escape":
                CancelEditor();
                return true;
            case "Tab" when !alt && !shift:
                return AcceptStructuredReferenceSuggestion(_selectedCandidate);
            default:
                return false;
        }
    }

    /// <summary>Detaches editor subscriptions without disposing the caller-owned spreadsheet.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _session?.Editor.Cancel();
        HideEditor();
        DetachNativeEditor();
        _disposed = true;
        ((NeraCellEditor)_editor).HandleKey = null;
        if (_session is not null) _session.ActiveWorksheetChanged -= OnActiveWorksheetChanged;
        Spreadsheet.PropertyChanged -= OnSpreadsheetPropertyChanged;
        Spreadsheet.PaintSurface -= OnFrame;
        _editGesture.Tapped -= OnEditGesture;
        Spreadsheet.GestureRecognizers.Remove(_editGesture);
        _editor.TextChanged -= OnTextChanged;
        _editor.PropertyChanged -= OnEditorPropertyChanged;
        _editor.HandlerChanged -= OnEditorHandlerChanged;
        _editor.HandlerChanging -= OnEditorHandlerChanging;
        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        SizeChanged -= OnHostSizeChanged;
        _commit.Clicked -= OnCommit;
        _cancel.Clicked -= OnCancel;
        _newline.Clicked -= OnNewline;
        foreach (var button in _candidateButtons) button.Clicked -= OnCandidateClicked;
        GC.SuppressFinalize(this);
    }
}
