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
        DisplayMemberPath = nameof(FormulaFunctionSuggestion.Name),
        MaxHeight = 220d,
        MinWidth = 180d,
        Background = Brushes.White,
        BorderBrush = Brushes.Gray,
        BorderThickness = new Thickness(1d),
    };
    private Popup? _formulaSuggestionPopup;
    private IReadOnlyList<FormulaFunctionSuggestion> _formulaSuggestions =
        Array.Empty<FormulaFunctionSuggestion>();
    private CellAddress? _formulaReferenceAnchor;
    private FormulaTextSpan? _formulaReferenceSpan;
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
        _formulaSuggestions;

    /// <summary>
    /// Gets the current in-cell edit text, or <see langword="null"/> when the
    /// control is not editing.
    /// </summary>
    public string? CurrentEditText => IsEditing ? _editor.Text : null;

    /// <summary>
    /// Gets the visible active-sheet precedent ranges for the formula cell that
    /// is selected or currently being edited.
    /// </summary>
    public IReadOnlyList<SpreadsheetFormulaReferenceHighlight>
        CurrentFormulaReferenceHighlights => GetFormulaReferenceHighlights();

    private void InitializeFormulaEditingUi()
    {
        _formulaSuggestionPopup = new Popup
        {
            PlacementTarget = _editor,
            Placement = PlacementMode.Bottom,
            StaysOpen = true,
            AllowsTransparency = true,
            Child = _formulaSuggestionList,
        };
        _editor.TextChanged += OnFormulaEditorTextChanged;
        _formulaSuggestionList.MouseDoubleClick +=
            OnFormulaSuggestionMouseDoubleClick;
    }

    private void DisposeFormulaEditingUi()
    {
        HideFormulaSuggestions();
        _editor.TextChanged -= OnFormulaEditorTextChanged;
        _formulaSuggestionList.MouseDoubleClick -=
            OnFormulaSuggestionMouseDoubleClick;
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
        HideFormulaSuggestions();
    }

    private void OnFormulaEditorTextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (!_updatingFormulaText)
        {
            _formulaReferenceSpan = null;
        }
        UpdateFormulaSuggestions();
    }

    private void UpdateFormulaSuggestions(int? caretIndex = null)
    {
        if (_session is null || !IsEditing)
        {
            HideFormulaSuggestions();
            return;
        }

        _formulaSuggestions = _session.FormulaEditing.GetSuggestions(
            _editor.Text,
            caretIndex ?? _editor.CaretIndex);
        _formulaSuggestionList.ItemsSource = _formulaSuggestions;
        if (_formulaSuggestionPopup is null || _formulaSuggestions.Count == 0)
        {
            HideFormulaSuggestions();
            return;
        }

        _formulaSuggestionList.SelectedIndex = 0;
        if (IsLoaded)
        {
            _formulaSuggestionPopup.IsOpen = true;
        }
    }

    private void HideFormulaSuggestions()
    {
        _formulaSuggestions = Array.Empty<FormulaFunctionSuggestion>();
        _formulaSuggestionList.ItemsSource = null;
        if (_formulaSuggestionPopup is not null)
        {
            _formulaSuggestionPopup.IsOpen = false;
        }
    }

    private bool TryHandleFormulaSuggestionKey(KeyEventArgs e)
    {
        if (_formulaSuggestionPopup?.IsOpen != true ||
            _formulaSuggestions.Count == 0)
        {
            return false;
        }

        if (e.Key == Key.Down)
        {
            _formulaSuggestionList.SelectedIndex = Math.Min(
                _formulaSuggestions.Count - 1,
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
        if (e.Key is Key.Enter or Key.Return or Key.Tab)
        {
            return ApplySelectedFormulaSuggestion();
        }
        if (e.Key == Key.Escape)
        {
            HideFormulaSuggestions();
            return true;
        }
        return false;
    }

    private void OnFormulaSuggestionMouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (ApplySelectedFormulaSuggestion())
        {
            e.Handled = true;
        }
    }

    private bool ApplySelectedFormulaSuggestion()
    {
        if (_formulaSuggestionList.SelectedItem is not
            FormulaFunctionSuggestion suggestion)
        {
            return false;
        }

        var edit = SpreadsheetFormulaEditingAssistant.ApplySuggestion(
            _editor.Text,
            _editor.CaretIndex,
            suggestion);
        SetFormulaEditText(edit.Text, edit.CaretIndex);
        _formulaReferenceSpan = null;
        HideFormulaSuggestions();
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
        if (!IsEditing || !_editor.Text.StartsWith('='))
        {
            return false;
        }

        var edit = SpreadsheetFormulaEditingAssistant.InsertReference(
            _editor.Text,
            _editor.CaretIndex,
            range,
            worksheetName,
            _formulaReferenceSpan);
        SetFormulaEditText(edit.Text, edit.CaretIndex);
        _formulaReferenceSpan = edit.InsertedSpan;
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
        if (!ShowFormulaReferenceHighlights || _session is null)
        {
            return Array.Empty<SpreadsheetFormulaReferenceHighlight>();
        }

        var target = _cellEditor?.State?.Address ??
            _session.Selection.ActiveCell;
        var formula = _session.ActiveWorksheet.GetCell(target).Formula;
        if (formula is null)
        {
            return Array.Empty<SpreadsheetFormulaReferenceHighlight>();
        }

        var colors = RenderTheme.FormulaReferenceColors;
        if (colors.Count == 0)
        {
            return Array.Empty<SpreadsheetFormulaReferenceHighlight>();
        }

        var dependencies = _session.Calculation.DependencyGraph.GetDependencies(
            new FormulaCellKey(_session.ActiveWorksheet.Name, target));
        if (dependencies.Count == 0)
        {
            FormulaReferenceAnalyzer.TryGetReferences(
                formula,
                out dependencies);
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
