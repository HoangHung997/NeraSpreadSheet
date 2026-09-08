using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;
using global::Avalonia.Controls.Templates;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Formulas;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Avalonia;

/// <summary>The one native edit draft, including directed UTF-16 selection endpoints.</summary>
public sealed record SpreadsheetEditorDraft(CellAddress Address, string Text, int SelectionStart, int SelectionEnd, int CaretIndex);

public sealed partial class NeraSpreadsheetControl
{
    private readonly ListBox _formulaList = new() { MaxHeight = 200, MinWidth = 240, Focusable = false };
    private readonly TextBlock _formulaHelp = new() { MaxWidth = 470, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };
    private Popup? _formulaPopup;
    private Control? _formulaAnchor;
    private IReadOnlyList<FormulaFunctionSuggestion> _functionSuggestions = [];
    private IReadOnlyList<FormulaStructuredReferenceSuggestion> _structuredSuggestions = [];
    private IReadOnlyList<SpreadsheetFormulaReferenceHighlight> _formulaHighlights = [];
    private FormulaFunctionHelpContext? _helpContext;
    private FormulaTextSpan? _provisionalSpan;
    private FormulaDependency? _provisionalDependency;
    private CellAddress? _pointAnchor;
    private bool _updatingFormulaDraft;
    private bool _formulaRefreshPending;
    private bool _showFormulaHighlights = true;
    private string? _acknowledgedFormulaText;
    private SpreadsheetEditorDraft? _lastDraft;

    public SpreadsheetEditorDraft? CurrentEditorDraft => IsEditing
        ? new(_ownedEdit!.Address, _editor.Text ?? string.Empty, _editor.SelectionStart, _editor.SelectionEnd, _editor.CaretIndex) : null;
    public IReadOnlyList<FormulaFunctionSuggestion> CurrentFormulaSuggestions => _functionSuggestions;
    public IReadOnlyList<FormulaStructuredReferenceSuggestion> CurrentStructuredReferenceSuggestions => _structuredSuggestions;
    public FormulaFunctionHelpContext? CurrentFormulaHelp => _helpContext;
    public bool IsFormulaAssistanceOpen => _formulaPopup?.IsOpen == true;
    public bool IsFormulaPointMode => _pointAnchor is not null;
    public bool ShowFormulaReferenceHighlights
    {
        get => _showFormulaHighlights;
        set { VerifyUsable(); if (_showFormulaHighlights != value) { _showFormulaHighlights = value; RefreshFormulaHighlights(); } }
    }
    public IReadOnlyList<SpreadsheetFormulaReferenceHighlight> CurrentFormulaReferenceHighlights =>
        !_showFormulaHighlights ? [] : FormulaProjectionOwner?.Invoke() is { } owner && !ReferenceEquals(owner, this) ? owner._formulaHighlights : _formulaHighlights;
    internal Func<NeraSpreadsheetControl?>? FormulaProjectionOwner { get; set; }
    public event EventHandler? FormulaAssistanceChanged;

    /// <summary>Bridges a formula bar to the existing draft without focus/history changes.
    /// Repeating the same text and directed selection is a true no-op.</summary>
    public bool UpdateEditorDraft(string text, int selectionStart, int selectionEnd)
    {
        VerifyUsable(); ArgumentNullException.ThrowIfNull(text);
        ArgumentOutOfRangeException.ThrowIfNegative(selectionStart); ArgumentOutOfRangeException.ThrowIfNegative(selectionEnd);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(selectionStart, text.Length); ArgumentOutOfRangeException.ThrowIfGreaterThan(selectionEnd, text.Length);
        if (!IsEditing) return false;
        var textChanged = !string.Equals(_editor.Text, text, StringComparison.Ordinal);
        if (!textChanged && _editor.SelectionStart == selectionStart && _editor.SelectionEnd == selectionEnd) return true;
        _updatingFormulaDraft = true;
        try
        {
            _acknowledgedFormulaText = text;
            if (textChanged) _editor.Text = text;
            _editor.CaretIndex = selectionEnd; _editor.SelectionStart = selectionStart; _editor.SelectionEnd = selectionEnd;
            if (textChanged || _provisionalSpan is { } span && (selectionStart != selectionEnd || selectionEnd != span.End)) ResetProvisionalReference();
        }
        finally { _updatingFormulaDraft = false; }
        RefreshFormulaAssistance(); RefreshFormulaHighlights(); NotifyDraftChanged(); return true;
    }
    public void SetFormulaAssistanceAnchor(Control? anchor)
    {
        VerifyUsable(); _formulaAnchor = anchor; RefreshFormulaAssistance();
    }
    public bool FocusEditor()
    {
        VerifyUsable(); if (!IsEditing) return false; _formulaAnchor = null; RefreshFormulaAssistance(); return _editor.Focus();
    }
    public bool TryHandleFormulaAssistanceKey(Key key, KeyModifiers modifiers)
    {
        VerifyUsable();
        if (!IsEditing || modifiers != KeyModifiers.None || _formulaList.ItemCount == 0 || !IsFormulaAssistanceOpen) return false;
        if (key is Key.Up or Key.Down)
        {
            _formulaList.SelectedIndex = Math.Clamp(_formulaList.SelectedIndex + (key == Key.Down ? 1 : -1), 0, _formulaList.ItemCount - 1);
            _formulaList.ScrollIntoView(_formulaList.SelectedItem!); UpdateFormulaHelpText(); return true;
        }
        return key == Key.Tab && ApplyFormulaSuggestion(_formulaList.SelectedIndex);
    }
    public bool ApplyFormulaSuggestion(int index)
    {
        VerifyUsable();
        if (!IsEditing || _session is null || index < 0 || index >= _formulaList.ItemCount || _editor.SelectionStart != _editor.SelectionEnd) return false;
        var text = _editor.Text ?? string.Empty; FormulaTextEditResult edit;
        if (_formulaList.Items[index] is FormulaStructuredReferenceSuggestion structured)
        {
            if (!SpreadsheetFormulaEditingAssistant.TryApplyStructuredReferenceSuggestion(text, _editor.CaretIndex, 0,
                _session.Workbook, _session.ActiveWorksheet, _ownedEdit!.Address, structured, out var accepted))
            { RefreshFormulaAssistance(); return false; }
            edit = accepted!;
        }
        else if (_formulaList.Items[index] is FormulaFunctionSuggestion function)
            edit = SpreadsheetFormulaEditingAssistant.ApplySuggestion(text, _editor.CaretIndex, function);
        else return false;
        ApplyFormulaText(edit); ResetProvisionalReference(); RefreshFormulaAssistance(); RefreshFormulaHighlights(); NotifyDraftChanged(); return true;
    }

    /// <summary>Inserts A1 or structured references using the shared assistant. Repeated
    /// drag updates replace only the provisional span; no workbook operation is created.</summary>
    public bool InsertFormulaReference(CellRange range, string? worksheetName = null)
    {
        VerifyUsable();
        if (!CanInsertPointReference || _session is null) return false;
        var worksheet = worksheetName is null ? _session.ActiveWorksheet : _session.Workbook.Worksheets
            .FirstOrDefault(item => string.Equals(item.Name, worksheetName, StringComparison.OrdinalIgnoreCase));
        if (worksheet is null) return false;
        var edit = SpreadsheetFormulaEditingAssistant.InsertReference(_editor.Text ?? string.Empty, _editor.CaretIndex,
            _session.Workbook, _session.ActiveWorksheet, _ownedEdit!.Address, worksheet, range, _provisionalSpan);
        ApplyFormulaText(edit); _provisionalSpan = edit.InsertedSpan; _provisionalDependency = new FormulaDependency(worksheet.Name, range);
        RefreshFormulaAssistance(); RefreshFormulaHighlights(); NotifyDraftChanged(); return true;
    }
    internal bool IsFormulaDraft => IsEditing && (_editor.Text?.StartsWith('=') ?? false);
    private bool CanInsertPointReference => IsFormulaDraft && _editor.SelectionStart == _editor.SelectionEnd &&
        SpreadsheetFormulaEditingAssistant.CanInsertReference(_editor.Text ?? string.Empty, _editor.CaretIndex, _provisionalSpan);
    internal bool TryHitFormulaCell(Point point, out CellAddress address)
    {
        address = default; if (_viewport is null) return false;
        var hit = SpreadsheetChromeGeometry.HitTest(point.X / _zoom, point.Y / _zoom, DocumentWidth, DocumentHeight, _renderTheme);
        var scroll = _scroll.Snapshot;
        return hit.Region == SpreadsheetChromeRegion.Body && _viewport.TryHitTest(hit.BodyX, hit.BodyY, scroll.OffsetX, scroll.OffsetY, out address);
    }
    internal bool BeginPointReference(CellAddress address)
    {
        if (!IsFormulaDraft) return false;
        if (CanInsertPointReference && InsertFormulaReference(new CellRange(address, address))) _pointAnchor = address;
        // A formula click outside a valid insertion position must not commit it by accident.
        return true;
    }
    internal bool UpdatePointReference(CellAddress address) => _pointAnchor is { } anchor && InsertFormulaReference(new CellRange(anchor, address));
    internal void EndPointReference(bool focus = true)
    {
        if (_pointAnchor is null) return; _pointAnchor = null;
        if (focus) { if (_formulaAnchor is { } anchor) anchor.Focus(); else FocusEditor(); }
    }
    private bool TryBeginFormulaPointer(Point point, IPointer pointer)
    {
        if (!IsFormulaDraft || !TryHitFormulaCell(point, out var address)) return false;
        BeginPointReference(address); if (_pointAnchor is not null) CapturePointer(pointer); return true;
    }

    private void InitializeFormulaAssistance()
    {
        _formulaList.ItemTemplate = new FuncDataTemplate<object>((value, _) => new TextBlock
        {
            Text = value switch { FormulaFunctionSuggestion function => function.DisplayText, FormulaStructuredReferenceSuggestion structured => structured.DisplayText, _ => string.Empty },
            Margin = new Thickness(5, 2),
        });
        _formulaList.AddHandler(PointerPressedEvent, OnSuggestionPointer, RoutingStrategies.Tunnel);
        _formulaList.SelectionChanged += OnSuggestionSelection;
        _editor.PropertyChanged += OnEditorCaretChanged;
        var panel = new StackPanel { Spacing = 2 }; panel.Children.Add(_formulaList); panel.Children.Add(_formulaHelp);
        var border = new Border { Child = panel, Padding = new Thickness(2), BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1) };
        border.Bind(Border.BackgroundProperty, this.GetResourceObservable("SystemControlBackgroundAltHighBrush"));
        _formulaPopup = new Popup { PlacementTarget = _editor, Placement = PlacementMode.Bottom, IsLightDismissEnabled = false, Child = border };
        LogicalChildren.Add(_formulaPopup);
        AutomationProperties.SetAutomationId(_formulaList, "nera-formula-suggestions"); AutomationProperties.SetName(_formulaList, "Gợi ý hàm và tham chiếu");
        AutomationProperties.SetAutomationId(_formulaHelp, "nera-formula-argument-help");
    }
    private void DisposeFormulaAssistance()
    {
        ResetFormulaAssistance();
        _editor.PropertyChanged -= OnEditorCaretChanged; _formulaList.RemoveHandler(PointerPressedEvent, OnSuggestionPointer); _formulaList.SelectionChanged -= OnSuggestionSelection;
        if (_formulaPopup is not null) { _formulaPopup.Child = null; _formulaPopup.PlacementTarget = null; LogicalChildren.Remove(_formulaPopup); _formulaPopup = null; }
        _formulaAnchor = null; FormulaProjectionOwner = null;
    }
    private void OnSuggestionPointer(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled || !e.GetCurrentPoint(_formulaList).Properties.IsLeftButtonPressed) return;
        var row = e.Source as ListBoxItem ?? (e.Source as Visual)?.GetVisualAncestors().OfType<ListBoxItem>().FirstOrDefault();
        if (row is null) return;
        var index = _formulaList.IndexFromContainer(row); if (index < 0) return;
        e.Handled = true; ApplyFormulaSuggestion(index);
    }
    private void OnSuggestionSelection(object? sender, SelectionChangedEventArgs e) => UpdateFormulaHelpText();
    private void OnEditorCaretChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_updatingFormulaDraft || !IsEditing || e.Property != TextBox.CaretIndexProperty && e.Property != TextBox.SelectionStartProperty && e.Property != TextBox.SelectionEndProperty) return;
        if (_provisionalSpan is { } span && (_editor.CaretIndex != span.End || _editor.SelectionStart != _editor.SelectionEnd)) ResetProvisionalReference();
        QueueFormulaRefresh(); NotifyDraftChanged();
    }
    private void OnFormulaTextChanged()
    {
        if (_updatingFormulaDraft || !IsEditing) return;
        var text = _editor.Text ?? string.Empty;
        // Avalonia TextChanged may be queued after a programmatic completion/drag update.
        if (string.Equals(text, _acknowledgedFormulaText, StringComparison.Ordinal)) return;
        _acknowledgedFormulaText = text; ResetProvisionalReference(); QueueFormulaRefresh(); RefreshFormulaHighlights();
    }
    private void QueueFormulaRefresh()
    {
        if (_formulaRefreshPending || _disposed) return; _formulaRefreshPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _formulaRefreshPending = false;
            if (!_disposed) { RefreshFormulaAssistance(); NotifyDraftChanged(); }
        }, DispatcherPriority.Background);
    }
    internal void RefreshFormulaAssistance()
    {
        if (_disposed) return;
        if (!IsEditing || _session is null) { HideFormulaAssistance(); return; }
        var text = _editor.Text ?? string.Empty; var caret = Math.Clamp(_editor.CaretIndex, 0, text.Length);
        var collapsed = _editor.SelectionStart == _editor.SelectionEnd;
        _functionSuggestions = collapsed ? _session.FormulaEditing.GetSuggestions(text, caret) : [];
        _structuredSuggestions = collapsed ? SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(text, caret,
            _session.Workbook, _session.ActiveWorksheet, _ownedEdit!.Address) : [];
        _helpContext = _session.FormulaEditing.GetFunctionHelp(text, caret);
        var previous = _formulaList.SelectedItem;
        var items = _structuredSuggestions.Cast<object>().Concat(_functionSuggestions).ToArray();
        _formulaList.ItemsSource = items; _formulaList.IsVisible = items.Length > 0;
        _formulaList.SelectedIndex = items.Length == 0 ? -1 : Math.Max(0, Array.IndexOf(items, previous));
        UpdateFormulaHelpText();
        if (_formulaPopup is not null)
        {
            var target = _formulaAnchor ?? _editor; _formulaPopup.PlacementTarget = target;
            _formulaPopup.IsOpen = _attached && target.IsVisible && (items.Length > 0 || _helpContext is not null);
        }
        FormulaAssistanceChanged?.Invoke(this, EventArgs.Empty);
    }
    private void HideFormulaAssistance()
    {
        _functionSuggestions = []; _structuredSuggestions = []; _helpContext = null; _formulaList.ItemsSource = null; _formulaHelp.Text = string.Empty;
        if (_formulaPopup is not null) _formulaPopup.IsOpen = false;
    }
    private void ResetFormulaAssistance()
    {
        _pointAnchor = null; _acknowledgedFormulaText = null; ResetProvisionalReference(); HideFormulaAssistance(); RefreshFormulaHighlights();
    }
    private void ResetProvisionalReference() { _provisionalSpan = null; _provisionalDependency = null; }
    private void ApplyFormulaText(FormulaTextEditResult edit)
    {
        _updatingFormulaDraft = true;
        try
        {
            _acknowledgedFormulaText = edit.Text; _editor.Text = edit.Text;
            _editor.CaretIndex = edit.CaretIndex; _editor.SelectionStart = edit.CaretIndex; _editor.SelectionEnd = edit.CaretIndex;
        }
        finally { _updatingFormulaDraft = false; }
    }
    private void NotifyDraftChanged()
    {
        if (_updatingFormulaDraft) return;
        var current = CurrentEditorDraft; if (current == _lastDraft) return;
        _lastDraft = current; EditorDraftChanged?.Invoke(this, EventArgs.Empty);
    }
    private void UpdateFormulaHelpText()
    {
        _formulaHelp.Text = _formulaList.SelectedItem switch
        {
            FormulaFunctionSuggestion suggestion => $"{suggestion.Signature}\n{suggestion.Description}",
            FormulaStructuredReferenceSuggestion structured => structured.DisplayText,
            _ when _helpContext is { } help => help.ActiveArgument is { } argument
                ? $"{help.Function.Signature}\n{help.Function.Description}\nĐối số {help.ActiveArgumentIndex + 1}: {argument.Name} — {argument.Description}" : $"{help.Function.Signature}\n{help.Function.Description}",
            _ => string.Empty,
        };
        _formulaHelp.IsVisible = _formulaHelp.Text.Length > 0;
    }
    private void RefreshFormulaHighlights()
    {
        if (_disposed) return; _formulaHighlights = [];
        if (_showFormulaHighlights && _session is { } session && _renderTheme.FormulaReferenceColors.Count > 0)
        {
            var address = IsEditing ? _ownedEdit!.Address : session.Selection.ActiveCell;
            var formula = IsEditing ? _editor.Text : session.ActiveWorksheet.GetCell(address).Formula;
            if (formula?.StartsWith('=') == true)
            {
                if (!FormulaReferenceAnalyzer.TryGetReferences(formula, session.Workbook, session.ActiveWorksheet, address, out var references) && _provisionalDependency is { } provisional)
                    references = [provisional];
                var colors = _renderTheme.FormulaReferenceColors;
                _formulaHighlights = references.Where(reference => reference.WorksheetName is null || string.Equals(reference.WorksheetName, session.ActiveWorksheet.Name, StringComparison.OrdinalIgnoreCase))
                    .Select((reference, index) => new SpreadsheetFormulaReferenceHighlight(reference.Range, colors[index % colors.Count])).ToArray();
            }
        }
        InvalidateVisual(); FormulaAssistanceChanged?.Invoke(this, EventArgs.Empty);
    }
}
