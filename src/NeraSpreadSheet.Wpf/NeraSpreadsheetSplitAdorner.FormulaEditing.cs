using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Formulas;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.Wpf;

internal sealed partial class NeraSpreadsheetSplitAdorner
{
    private readonly ListBox _formulaSuggestionList = new()
    {
        DisplayMemberPath = nameof(FormulaStructuredReferenceSuggestion.DisplayText),
        Focusable = false,
        MaxHeight = 220d,
        MinWidth = 180d,
    };
    private readonly TextBlock _formulaHelpText = new()
    {
        MaxWidth = 460d,
        Margin = new Thickness(8d, 6d, 8d, 8d),
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brushes.Black,
    };
    private Popup? _formulaSuggestionPopup;
    private IReadOnlyList<FormulaFunctionSuggestion> _formulaSuggestions = [];
    private IReadOnlyList<FormulaStructuredReferenceSuggestion> _structuredReferenceSuggestions = [];
    private FormulaFunctionHelpContext? _formulaHelpContext;
    private FormulaTextSpan? _formulaReferenceSpan;
    private CellAddress? _formulaReferenceAnchor;
    private CellRange? _provisionalReferenceRange;
    private bool _updatingFormulaText;

    internal IReadOnlyList<FormulaFunctionSuggestion> CurrentFormulaSuggestions =>
        CurrentEditorDraft is null ? [] : _formulaSuggestions;

    internal IReadOnlyList<FormulaStructuredReferenceSuggestion> CurrentStructuredReferenceSuggestions =>
        CurrentEditorDraft is null ? [] : _structuredReferenceSuggestions;

    internal FormulaFunctionHelpContext? CurrentFormulaHelp =>
        CurrentEditorDraft is null ? null : _formulaHelpContext;

    private void InitializeFormulaEditingUi()
    {
        var content = new StackPanel();
        content.Children.Add(_formulaSuggestionList);
        content.Children.Add(_formulaHelpText);
        _formulaSuggestionPopup = new Popup
        {
            PlacementTarget = _editor, Placement = PlacementMode.Bottom,
            StaysOpen = true,
            AllowsTransparency = true,
            Child = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1d),
                Child = content,
            },
        };
        _editor.TextChanged += OnFormulaTextChanged;
        _editor.SelectionChanged += OnFormulaSelectionChanged;
        _editor.TextChanged += OnNativeEditorDraftChanged;
        _editor.SelectionChanged += OnNativeEditorDraftChanged;
        _formulaSuggestionList.PreviewMouseDown += OnFormulaSuggestionMouseDown;
        _formulaSuggestionList.SelectionChanged += OnFormulaSuggestionSelectionChanged;
    }

    private void DisposeFormulaEditingUi()
    {
        ResetFormulaEditingUi();
        _editor.TextChanged -= OnFormulaTextChanged;
        _editor.SelectionChanged -= OnFormulaSelectionChanged;
        _editor.TextChanged -= OnNativeEditorDraftChanged;
        _editor.SelectionChanged -= OnNativeEditorDraftChanged;
        _formulaSuggestionList.PreviewMouseDown -= OnFormulaSuggestionMouseDown;
        _formulaSuggestionList.SelectionChanged -= OnFormulaSuggestionSelectionChanged;
        if (_formulaSuggestionPopup is { } popup) popup.Child = null;
        _formulaSuggestionPopup = null;
    }

    private void ResetFormulaEditingUi()
    {
        var releaseCapture = _formulaReferenceAnchor is not null && IsMouseCaptured;
        _formulaReferenceSpan = null;
        _formulaReferenceAnchor = null;
        if (releaseCapture) ReleaseMouseCapture();
        _provisionalReferenceRange = null;
        HideFormulaSuggestions();
    }

    private void HideFormulaSuggestions()
    {
        _formulaSuggestions = [];
        _structuredReferenceSuggestions = [];
        _formulaHelpContext = null;
        _formulaSuggestionList.ItemsSource = null;
        _formulaHelpText.Text = string.Empty;
        if (_formulaSuggestionPopup is { } popup) popup.IsOpen = false;
    }

    private void OnFormulaTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_updatingFormulaText) return;
        _formulaReferenceSpan = null;
        _provisionalReferenceRange = null;
        UpdateFormulaSuggestions();
        InvalidateVisual();
    }

    private void OnFormulaSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingFormulaText) return;
        if (_formulaReferenceSpan is { } span && (_editor.CaretIndex != span.End || _editor.SelectionLength != 0))
        {
            _formulaReferenceSpan = null;
            _provisionalReferenceRange = null;
        }
        UpdateFormulaSuggestions();
    }

    private void UpdateFormulaSuggestions()
    {
        if (CurrentEditorDraft is null || _session is null || _cellEditor?.State is not { } state || _editor.SelectionLength != 0)
        {
            HideFormulaSuggestions();
            return;
        }
        _structuredReferenceSuggestions = SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(
            _editor.Text, _editor.CaretIndex, _session.Workbook, _session.ActiveWorksheet, state.Address);
        _formulaSuggestions = _session.FormulaEditing.GetSuggestions(_editor.Text, _editor.CaretIndex);
        _formulaHelpContext = _session.FormulaEditing.GetFunctionHelp(_editor.Text, _editor.CaretIndex);
        _formulaSuggestionList.ItemsSource = _structuredReferenceSuggestions.Cast<object>().Concat(_formulaSuggestions).ToArray();
        var count = _formulaSuggestionList.Items.Count;
        _formulaSuggestionList.Visibility = count == 0 ? Visibility.Collapsed : Visibility.Visible;
        _formulaSuggestionList.SelectedIndex = count == 0 ? -1 : 0;
        UpdateFormulaHelpText();
        if (_formulaSuggestionPopup is { } popup)
            popup.IsOpen = IsLoaded && _editor.Visibility == Visibility.Visible && (count > 0 || _formulaHelpContext is not null);
    }

    private void OnFormulaSuggestionSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateFormulaHelpText();

    private void UpdateFormulaHelpText()
    {
        if (_formulaSuggestionList.SelectedItem is FormulaStructuredReferenceSuggestion structured)
        {
            _formulaHelpText.Text = structured.DisplayText;
        }
        else if (_formulaSuggestionList.SelectedItem is FormulaFunctionSuggestion suggestion)
        {
            _formulaHelpText.Text = $"{suggestion.Signature}\n{suggestion.Description}";
        }
        else if (_formulaHelpContext is { } context)
        {
            var argument = context.ActiveArgument;
            _formulaHelpText.Text = argument is null
                ? $"{context.Function.Signature}\n{context.Function.Description}"
                : $"{context.Function.Signature}\n{context.Function.Description}\n" +
                  $"Đối số {context.ActiveArgumentIndex + 1}: {argument.Name} — {argument.Description}";
        }
        else
        {
            _formulaHelpText.Text = string.Empty;
        }
    }

    private bool TryHandleFormulaSuggestionKey(KeyEventArgs e)
    {
        if (_formulaSuggestionPopup?.IsOpen != true || _formulaSuggestionList.Items.Count == 0 || Keyboard.Modifiers != ModifierKeys.None)
            return false;
        if (e.Key is Key.Down or Key.Up)
        {
            _formulaSuggestionList.SelectedIndex = Math.Clamp(_formulaSuggestionList.SelectedIndex + (e.Key == Key.Down ? 1 : -1),
                0, _formulaSuggestionList.Items.Count - 1);
            _formulaSuggestionList.ScrollIntoView(_formulaSuggestionList.SelectedItem);
            return true;
        }
        return e.Key == Key.Tab && ApplySelectedFormulaSuggestion();
    }

    private void OnFormulaSuggestionMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement(_formulaSuggestionList, source) is not ListBoxItem item) return;
        _formulaSuggestionList.SelectedItem = item.Content;
        ApplySelectedFormulaSuggestion();
        e.Handled = true;
    }

    private bool ApplySelectedFormulaSuggestion()
    {
        if (_session is null || _cellEditor?.State is not { } state) return false;
        FormulaTextEditResult? edit;
        if (_formulaSuggestionList.SelectedItem is FormulaStructuredReferenceSuggestion structured)
        {
            if (!SpreadsheetFormulaEditingAssistant.TryApplyStructuredReferenceSuggestion(_editor.Text, _editor.CaretIndex,
                    _editor.SelectionLength, _session.Workbook, _session.ActiveWorksheet, state.Address, structured, out edit))
            {
                UpdateFormulaSuggestions();
                _editor.Focus();
                return true;
            }
        }
        else if (_formulaSuggestionList.SelectedItem is FormulaFunctionSuggestion function)
            edit = SpreadsheetFormulaEditingAssistant.ApplySuggestion(_editor.Text, _editor.CaretIndex, function);
        else return false;
        _formulaReferenceSpan = null;
        _provisionalReferenceRange = null;
        SetFormulaEditText(edit!);
        return true;
    }

    private void SetFormulaEditText(FormulaTextEditResult edit)
    {
        _updatingFormulaText = true;
        try
        {
            _editor.Text = edit.Text;
            _editor.Select(edit.CaretIndex, 0);
        }
        finally { _updatingFormulaText = false; }
        UpdateFormulaSuggestions();
        if (_formulaReferenceAnchor is null) _editor.Focus();
        InvalidateVisual();
    }

    private bool TryInsertFormulaReference(Point point)
    {
        if (_session is null || _cellEditor?.State is not { } state || !_editor.Text.StartsWith('=')) return false;
        if (!SpreadsheetFormulaEditingAssistant.CanInsertReference(_editor.Text, _editor.CaretIndex, _formulaReferenceSpan)) return true;
        if (!TryHitTest(point.X, point.Y, out _, out var address)) return true;
        _formulaReferenceAnchor = address;
        CaptureMouse();
        return UpdateFormulaReferencePointer(point);
    }

    private bool UpdateFormulaReferencePointer(Point point, bool release = false)
    {
        if (_formulaReferenceAnchor is not { } anchor || _session is null || _cellEditor?.State is not { } state) return false;
        if (TryHitTest(point.X, point.Y, out _, out var address))
        {
            var range = new CellRange(anchor, address);
            var edit = SpreadsheetFormulaEditingAssistant.InsertReference(_editor.Text, _editor.CaretIndex,
                _session.Workbook, _session.ActiveWorksheet, state.Address, _session.ActiveWorksheet,
                range, _formulaReferenceSpan);
            SetFormulaEditText(edit);
            _formulaReferenceSpan = edit.InsertedSpan;
            _provisionalReferenceRange = range;
        }
        if (release)
        {
            _formulaReferenceAnchor = null;
            ReleaseMouseCapture();
            _editor.Focus();
        }
        InvalidateVisual();
        return true;
    }

    internal IReadOnlyList<SpreadsheetFormulaReferenceHighlight> GetFormulaReferenceHighlights()
    {
        if (!_owner.ShowFormulaReferenceHighlights || _session is null || !ReferenceEquals(_session, _owner.Session) ||
            (IsEditing && !_hasEditorDraft) || _owner.RenderTheme.FormulaReferenceColors.Count == 0) return [];
        var address = _cellEditor?.State?.Address ?? _session.Selection.ActiveCell;
        var formula = IsEditing ? _editor.Text : _session.ActiveWorksheet.GetCell(address).Formula;
        if (formula is null || !formula.StartsWith('=')) return [];
        IReadOnlyList<FormulaDependency> references;
        if (IsEditing)
        {
            if (!FormulaReferenceAnalyzer.TryGetReferences(formula, _session.Workbook, _session.ActiveWorksheet,
                    address, out references) && _provisionalReferenceRange is { } range)
                references = [new FormulaDependency(_session.ActiveWorksheet.Name, range)];
        }
        else
        {
            references = _session.Calculation.DependencyGraph.GetDependencies(new FormulaCellKey(_session.ActiveWorksheet.Name, address));
            if (references.Count == 0)
                FormulaReferenceAnalyzer.TryGetReferences(formula, _session.Workbook, _session.ActiveWorksheet, address, out references);
        }
        return references.Where(reference => reference.WorksheetName is null ||
                string.Equals(reference.WorksheetName, _session.ActiveWorksheet.Name, StringComparison.OrdinalIgnoreCase))
            .Select((reference, index) => new SpreadsheetFormulaReferenceHighlight(reference.Range,
                _owner.RenderTheme.FormulaReferenceColors[index % _owner.RenderTheme.FormulaReferenceColors.Count])).ToArray();
    }

    private DisplayList ComposeFormulaHighlights(SpreadsheetSplitViewportFrame frame)
    {
        var highlights = GetFormulaReferenceHighlights();
        if (highlights.Count == 0) return frame.DisplayList;
        var builder = new DisplayListBuilder();
        builder.Append(frame.DisplayList);
        var empty = new DisplayListBuilder().Build();
        foreach (var pane in frame.Panes)
        {
            builder.PushClip(pane.Pane.Bounds);
            builder.PushTranslation(pane.Pane.Bounds.X, pane.Pane.Bounds.Y);
            builder.Append(SpreadsheetFormulaReferenceDisplayListComposer.Compose(empty,
                pane.ViewportFrame.Layout, highlights, _owner.RenderTheme.FormulaReferenceStrokeWidth));
            builder.PopTranslation();
            builder.PopClip();
        }
        return builder.Build();
    }
}
