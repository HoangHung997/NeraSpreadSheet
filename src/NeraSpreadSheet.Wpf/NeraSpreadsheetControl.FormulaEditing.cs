using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Formulas;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.Wpf;

public sealed partial class NeraSpreadsheetControl
{
    private readonly ListBox _formulaSuggestionList = new()
    {
        DisplayMemberPath = nameof(FormulaFunctionSuggestion.DisplayText),
        MaxHeight = 220d,
        MinWidth = 180d,
        Background = Brushes.White,
        BorderBrush = Brushes.Gray,
        BorderThickness = new Thickness(1d),
    };
    private readonly TextBlock _formulaHelpText = new()
    {
        MaxWidth = 460d,
        Margin = new Thickness(8d, 6d, 8d, 8d),
        TextWrapping = TextWrapping.Wrap,
        Foreground = Brushes.Black,
    };
    private Popup? _formulaSuggestionPopup;
    private IReadOnlyList<FormulaFunctionSuggestion> _formulaSuggestions =
        Array.Empty<FormulaFunctionSuggestion>();
    private IReadOnlyList<FormulaStructuredReferenceSuggestion> _structuredReferenceSuggestions = [];
    private FormulaFunctionHelpContext? _formulaHelpContext;
    private CellAddress? _formulaReferenceAnchor;
    private FormulaTextSpan? _formulaReferenceSpan;
    private FormulaDependency? _provisionalReference;
    private bool _updatingFormulaText;

    /// <summary>
    /// Gets or sets whether selecting a formula cell outlines its visible
    /// precedent ranges on the active worksheet.
    /// </summary>
    public bool ShowFormulaReferenceHighlights { get; set; } = true;

    /// <summary>
    /// Gets the completion candidates currently shown for the in-cell editor.
    /// </summary>
    public IReadOnlyList<FormulaFunctionSuggestion> CurrentFormulaSuggestions =>
        this.TryGetSplitPaneController(out var split) && split is not null
            ? split.CurrentFormulaSuggestions : _formulaSuggestions;

    /// <summary>Gets the bounded Table/column candidates in the active editor popup.</summary>
    public IReadOnlyList<FormulaStructuredReferenceSuggestion> CurrentStructuredReferenceSuggestions =>
        this.TryGetSplitPaneController(out var split) && split is not null
            ? split.CurrentStructuredReferenceSuggestions : _structuredReferenceSuggestions;

    /// <summary>
    /// Gets help for the innermost function invocation at the editor caret.
    /// </summary>
    public FormulaFunctionHelpContext? CurrentFormulaHelp =>
        this.TryGetSplitPaneController(out var split) && split is not null
            ? split.CurrentFormulaHelp : _formulaHelpContext;

    /// <summary>
    /// Gets the current in-cell edit text, or <see langword="null"/> when the
    /// control is not editing.
    /// </summary>
    public string? CurrentEditText => CurrentEditorDraft?.Text;

    /// <summary>
    /// Gets the visible active-sheet precedent ranges for the formula cell that
    /// is selected or currently being edited.
    /// </summary>
    public IReadOnlyList<SpreadsheetFormulaReferenceHighlight>
        CurrentFormulaReferenceHighlights =>
        this.TryGetSplitPaneController(out var split) && split is not null
            ? split.CurrentFormulaReferenceHighlights : GetFormulaReferenceHighlights();

    private void InitializeFormulaEditingUi()
    {
        var content = new StackPanel();
        content.Children.Add(_formulaSuggestionList);
        content.Children.Add(_formulaHelpText);
        _formulaSuggestionPopup = new Popup
        {
            PlacementTarget = _editor,
            Placement = PlacementMode.Bottom,
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
        _editor.TextChanged += OnFormulaEditorTextChanged;
        _editor.SelectionChanged += OnFormulaEditorSelectionChanged;
        _editor.TextChanged += OnNativeEditorDraftChanged;
        _editor.SelectionChanged += OnNativeEditorDraftChanged;
        _formulaSuggestionList.Focusable = false;
        _formulaSuggestionList.PreviewMouseDown += OnFormulaSuggestionMouseClick;
        _formulaSuggestionList.SelectionChanged +=
            OnFormulaSuggestionSelectionChanged;
    }

    private void DisposeFormulaEditingUi()
    {
        HideFormulaSuggestions();
        _editor.TextChanged -= OnFormulaEditorTextChanged;
        _editor.SelectionChanged -= OnFormulaEditorSelectionChanged;
        _editor.TextChanged -= OnNativeEditorDraftChanged;
        _editor.SelectionChanged -= OnNativeEditorDraftChanged;
        _formulaSuggestionList.PreviewMouseDown -= OnFormulaSuggestionMouseClick;
        _formulaSuggestionList.SelectionChanged -=
            OnFormulaSuggestionSelectionChanged;
        if (_formulaSuggestionPopup is not null)
        {
            _formulaSuggestionPopup.Child = null;
            _formulaSuggestionPopup = null;
        }
    }

    private void ResetFormulaEditingUi()
    {
        _formulaReferenceAnchor = null;
        _formulaReferenceSpan = null;
        _provisionalReference = null;
        HideFormulaSuggestions();
    }

    private void OnFormulaEditorTextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (!_updatingFormulaText)
        {
            _formulaReferenceSpan = null;
            _provisionalReference = null;
        }
        UpdateFormulaSuggestions();
        InvalidateVisual();
    }

    private void OnFormulaEditorSelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_updatingFormulaText) return;
        if (_formulaReferenceSpan is { } span &&
            (_editor.CaretIndex != span.End || _editor.SelectionLength != 0))
        {
            _formulaReferenceSpan = null;
            _provisionalReference = null;
        }
        UpdateFormulaSuggestions();
    }

    private void UpdateFormulaSuggestions(int? caretIndex = null)
    {
        if (_session is null || !_hasEditorDraft || !IsEditing)
        {
            HideFormulaSuggestions();
            return;
        }

        _formulaSuggestions = _session.FormulaEditing.GetSuggestions(
            _editor.Text,
            caretIndex ?? _editor.CaretIndex);
        _formulaHelpContext = _session.FormulaEditing.GetFunctionHelp(
            _editor.Text,
            caretIndex ?? _editor.CaretIndex);
        _structuredReferenceSuggestions = SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(
            _editor.Text, caretIndex ?? _editor.CaretIndex, _session.Workbook,
            _session.ActiveWorksheet, _cellEditor!.State!.Address);
        _formulaSuggestionList.ItemsSource = _structuredReferenceSuggestions.Cast<object>()
            .Concat(_formulaSuggestions).ToArray();
        var count = _formulaSuggestionList.Items.Count;
        _formulaSuggestionList.Visibility = count == 0
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (_formulaSuggestionPopup is null ||
            (count == 0 && _formulaHelpContext is null))
        {
            HideFormulaSuggestions();
            return;
        }

        _formulaSuggestionList.SelectedIndex =
            count == 0 ? -1 : 0;
        UpdateFormulaHelpText();
        if (IsLoaded)
        {
            _formulaSuggestionPopup.IsOpen = true;
        }
    }

    private void HideFormulaSuggestions()
    {
        _formulaSuggestions = Array.Empty<FormulaFunctionSuggestion>();
        _structuredReferenceSuggestions = [];
        _formulaHelpContext = null;
        _formulaSuggestionList.ItemsSource = null;
        _formulaHelpText.Text = string.Empty;
        if (_formulaSuggestionPopup is not null)
        {
            _formulaSuggestionPopup.IsOpen = false;
        }
    }

    private bool TryHandleFormulaSuggestionKey(KeyEventArgs e)
    {
        if (_formulaSuggestionPopup?.IsOpen != true ||
            _formulaSuggestionList.Items.Count == 0 || Keyboard.Modifiers != ModifierKeys.None)
        {
            return false;
        }

        if (e.Key == Key.Down)
        {
            _formulaSuggestionList.SelectedIndex = Math.Min(
                _formulaSuggestionList.Items.Count - 1,
                _formulaSuggestionList.SelectedIndex + 1);
            _formulaSuggestionList.ScrollIntoView(
                _formulaSuggestionList.SelectedItem);
            return true;
        }
        if (e.Key == Key.Up)
        {
            _formulaSuggestionList.SelectedIndex = Math.Max(
                0,
                _formulaSuggestionList.SelectedIndex - 1);
            _formulaSuggestionList.ScrollIntoView(
                _formulaSuggestionList.SelectedItem);
            return true;
        }
        if (e.Key == Key.Tab)
        {
            return ApplySelectedFormulaSuggestion();
        }
        return false;
    }

    private void OnFormulaSuggestionMouseClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && e.OriginalSource is DependencyObject source &&
            ItemsControl.ContainerFromElement(_formulaSuggestionList, source) is ListBoxItem item)
        {
            _formulaSuggestionList.SelectedItem = item.Content;
            ApplySelectedFormulaSuggestion();
            e.Handled = true;
        }
    }

    private void OnFormulaSuggestionSelectionChanged(
        object sender,
        SelectionChangedEventArgs e) =>
        UpdateFormulaHelpText();

    private void UpdateFormulaHelpText()
    {
        if (_formulaSuggestionList.SelectedItem is FormulaStructuredReferenceSuggestion structured)
        {
            _formulaHelpText.Text = structured.DisplayText;
            return;
        }
        if (_formulaSuggestionList.SelectedItem is
            FormulaFunctionSuggestion suggestion)
        {
            _formulaHelpText.Text =
                $"{suggestion.Signature}\n{suggestion.Description}";
            return;
        }
        if (_formulaHelpContext is not { } context)
        {
            _formulaHelpText.Text = string.Empty;
            return;
        }

        var argument = context.ActiveArgument;
        _formulaHelpText.Text = argument is null
            ? $"{context.Function.Signature}\n{context.Function.Description}"
            : $"{context.Function.Signature}\n{context.Function.Description}\n" +
              $"Đối số {context.ActiveArgumentIndex + 1}: " +
              $"{argument.Name} — {argument.Description}";
    }

    private bool ApplySelectedFormulaSuggestion()
    {
        if (_session is null || _cellEditor?.State is not { } state) return false;
        FormulaTextEditResult edit;
        if (_formulaSuggestionList.SelectedItem is FormulaStructuredReferenceSuggestion structured)
        {
            if (!SpreadsheetFormulaEditingAssistant.TryApplyStructuredReferenceSuggestion(
                    _editor.Text, _editor.CaretIndex, _editor.SelectionLength,
                    _session.Workbook, _session.ActiveWorksheet, state.Address, structured, out var accepted))
            {
                // A metadata mutation can invalidate an open popup. Consume acceptance
                // without committing the stale fragment or changing workbook history.
                UpdateFormulaSuggestions();
                _editor.Focus();
                return true;
            }
            edit = accepted!;
        }
        else if (_formulaSuggestionList.SelectedItem is FormulaFunctionSuggestion suggestion)
        {
            edit = SpreadsheetFormulaEditingAssistant.ApplySuggestion(
                _editor.Text, _editor.CaretIndex, suggestion);
        }
        else
        {
            return false;
        }
        SetFormulaEditText(edit.Text, edit.CaretIndex);
        _formulaReferenceSpan = null;
        _provisionalReference = null;
        UpdateFormulaSuggestions(edit.CaretIndex);
        _editor.Focus();
        return true;
    }

    /// <summary>
    /// Inserts or updates a point-mode reference in the active formula editor.
    /// Returns false when the control is not editing formula text.
    /// </summary>
    public bool InsertFormulaReference(
        CellRange range,
        string? worksheetName = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_session is null || _cellEditor?.State is not { } state ||
            !SpreadsheetFormulaEditingAssistant.CanInsertReference(
                _editor.Text, _editor.CaretIndex, _formulaReferenceSpan))
        {
            return false;
        }

        var referenceWorksheet = worksheetName is null ? _session.ActiveWorksheet :
            _session.Workbook.Worksheets.FirstOrDefault(sheet => string.Equals(
                sheet.Name, worksheetName, StringComparison.OrdinalIgnoreCase));
        if (referenceWorksheet is null) return false;
        var edit = SpreadsheetFormulaEditingAssistant.InsertReference(
            _editor.Text, _editor.CaretIndex, _session.Workbook,
            _session.ActiveWorksheet, state.Address, referenceWorksheet, range, _formulaReferenceSpan);
        SetFormulaEditText(edit.Text, edit.CaretIndex);
        _formulaReferenceSpan = edit.InsertedSpan;
        _provisionalReference = new FormulaDependency(referenceWorksheet.Name, range);
        InvalidateVisual();
        return true;
    }

    private void SetFormulaEditText(string text, int caretIndex)
    {
        _updatingFormulaText = true;
        try
        {
            _editor.Text = text;
            _editor.CaretIndex = caretIndex;
        }
        finally
        {
            _updatingFormulaText = false;
        }
    }

    private bool TryBeginFormulaReferencePointer(Point point)
    {
        if (_session is null ||
            _viewport is null ||
            !IsEditing ||
            !_editor.Text.StartsWith('='))
        {
            return false;
        }
        if (!SpreadsheetFormulaEditingAssistant.CanInsertReference(_editor.Text, _editor.CaretIndex, _formulaReferenceSpan))
            return true;

        var hit = SpreadsheetChromeGeometry.HitTest(
            point.X,
            point.Y,
            ActualWidth,
            ActualHeight,
            RenderTheme);
        var scroll = _scrollController.Snapshot;
        if (hit.Region != SpreadsheetChromeRegion.Body ||
            !_viewport.TryHitTest(
                hit.BodyX,
                hit.BodyY,
                scroll.OffsetX,
                scroll.OffsetY,
                out var address))
        {
            return false;
        }

        _formulaReferenceAnchor = address;
        var range = new CellRange(address, address);
        if (!InsertFormulaReference(range))
        {
            _formulaReferenceAnchor = null;
            return false;
        }
        _session.Selection.Select(range);
        CaptureMouse();
        InvalidateVisual();
        return true;
    }

    private bool UpdateFormulaReferencePointer(Point point)
    {
        if (_formulaReferenceAnchor is not { } anchor ||
            _session is null ||
            _viewport is null)
        {
            return false;
        }

        var hit = SpreadsheetChromeGeometry.HitTest(
            point.X,
            point.Y,
            ActualWidth,
            ActualHeight,
            RenderTheme);
        var scroll = _scrollController.Snapshot;
        if (hit.Region != SpreadsheetChromeRegion.Body ||
            !_viewport.TryHitTest(
                hit.BodyX,
                hit.BodyY,
                scroll.OffsetX,
                scroll.OffsetY,
                out var address))
        {
            return true;
        }

        var range = new CellRange(anchor, address);
        InsertFormulaReference(range);
        _session.Selection.Select(range);
        InvalidateVisual();
        return true;
    }

    private bool EndFormulaReferencePointer()
    {
        if (_formulaReferenceAnchor is null)
        {
            return false;
        }

        _formulaReferenceAnchor = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }
        _editor.Focus();
        return true;
    }

    private IReadOnlyList<SpreadsheetFormulaReferenceHighlight>
        GetFormulaReferenceHighlights()
    {
        if (!ShowFormulaReferenceHighlights || _session is null || (IsEditing && !_hasEditorDraft))
        {
            return Array.Empty<SpreadsheetFormulaReferenceHighlight>();
        }

        var target = _cellEditor?.State?.Address ??
            _session.Selection.ActiveCell;
        var formula = IsEditing ? _editor.Text : _session.ActiveWorksheet.GetCell(target).Formula;
        if (formula is null || !formula.StartsWith('='))
        {
            return Array.Empty<SpreadsheetFormulaReferenceHighlight>();
        }

        var colors = RenderTheme.FormulaReferenceColors;
        if (colors.Count == 0)
        {
            return Array.Empty<SpreadsheetFormulaReferenceHighlight>();
        }

        IReadOnlyList<FormulaDependency> dependencies;
        if (IsEditing)
        {
            if (!FormulaReferenceAnalyzer.TryGetReferences(formula, _session.Workbook,
                    _session.ActiveWorksheet, target, out dependencies) &&
                _provisionalReference is { } provisional)
                dependencies = [provisional];
        }
        else
        {
            dependencies = _session.Calculation.DependencyGraph.GetDependencies(
                new FormulaCellKey(_session.ActiveWorksheet.Name, target));
            if (dependencies.Count == 0)
                FormulaReferenceAnalyzer.TryGetReferences(formula, _session.Workbook,
                    _session.ActiveWorksheet, target, out dependencies);
        }
        var result = new List<SpreadsheetFormulaReferenceHighlight>();
        foreach (var dependency in dependencies)
        {
            var worksheetName = dependency.WorksheetName ??
                _session.ActiveWorksheet.Name;
            if (!string.Equals(
                    worksheetName,
                    _session.ActiveWorksheet.Name,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            result.Add(new SpreadsheetFormulaReferenceHighlight(
                dependency.Range,
                colors[result.Count % colors.Count]));
        }
        return result;
    }
}
