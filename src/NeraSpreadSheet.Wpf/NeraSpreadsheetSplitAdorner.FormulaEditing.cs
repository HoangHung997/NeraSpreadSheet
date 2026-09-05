using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
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
    private Popup? _formulaSuggestionPopup;
    private FormulaTextSpan? _formulaReferenceSpan;
    private CellAddress? _formulaReferenceAnchor;
    private CellRange? _provisionalReferenceRange;
    private bool _updatingFormulaText;

    private void InitializeFormulaEditingUi()
    {
        _formulaSuggestionPopup = new Popup
        {
            PlacementTarget = _editor, Placement = PlacementMode.Bottom,
            StaysOpen = true, Child = _formulaSuggestionList,
        };
        _editor.TextChanged += OnFormulaTextChanged;
        _editor.SelectionChanged += OnFormulaSelectionChanged;
        _formulaSuggestionList.PreviewMouseDown += OnFormulaSuggestionMouseDown;
    }

    private void DisposeFormulaEditingUi()
    {
        ResetFormulaEditingUi();
        _editor.TextChanged -= OnFormulaTextChanged;
        _editor.SelectionChanged -= OnFormulaSelectionChanged;
        _formulaSuggestionList.PreviewMouseDown -= OnFormulaSuggestionMouseDown;
        if (_formulaSuggestionPopup is { } popup) popup.Child = null;
        _formulaSuggestionPopup = null;
    }

    private void ResetFormulaEditingUi()
    {
        _formulaReferenceSpan = null;
        _formulaReferenceAnchor = null;
        _provisionalReferenceRange = null;
        _formulaSuggestionList.ItemsSource = null;
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
        if (_session is null || _cellEditor?.State is not { } state || _editor.SelectionLength != 0)
        {
            ResetFormulaEditingUi();
            return;
        }
        _formulaSuggestionList.ItemsSource = SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(
            _editor.Text, _editor.CaretIndex, _session.Workbook, _session.ActiveWorksheet, state.Address)
            .Cast<object>().Concat(_session.FormulaEditing.GetSuggestions(_editor.Text, _editor.CaretIndex)).ToArray();
        if (_formulaSuggestionList.Items.Count > 0) _formulaSuggestionList.SelectedIndex = 0;
        if (_formulaSuggestionPopup is { } popup)
            popup.IsOpen = IsLoaded && _editor.Visibility == Visibility.Visible && _formulaSuggestionList.Items.Count > 0;
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
        SetFormulaEditText(edit!);
        _formulaReferenceSpan = null;
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
        _editor.Focus();
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

    private DisplayList ComposeFormulaHighlights(SpreadsheetSplitViewportFrame frame)
    {
        if (_session is null || _cellEditor?.State is not { } state || _owner.RenderTheme.FormulaReferenceColors.Count == 0) return frame.DisplayList;
        if (!FormulaReferenceAnalyzer.TryGetReferences(_editor.Text, _session.Workbook, _session.ActiveWorksheet,
                state.Address, out var references) && _provisionalReferenceRange is { } range)
            references = [new FormulaDependency(_session.ActiveWorksheet.Name, range)];
        var highlights = references.Where(reference => reference.WorksheetName is null ||
            string.Equals(reference.WorksheetName, _session.ActiveWorksheet.Name, StringComparison.OrdinalIgnoreCase))
            .Select((reference, index) => new SpreadsheetFormulaReferenceHighlight(reference.Range,
                _owner.RenderTheme.FormulaReferenceColors[index % _owner.RenderTheme.FormulaReferenceColors.Count])).ToArray();
        if (highlights.Length == 0) return frame.DisplayList;
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
