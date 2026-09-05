using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Formulas;
using NeraSpreadSheet.Rendering.Spreadsheet;

namespace NeraSpreadSheet.WinForms;

public sealed partial class NeraSpreadsheetControl
{
    private readonly ListBox _formulaSuggestionList = new()
    {
        Visible = false,
        DisplayMember = nameof(FormulaStructuredReferenceSuggestion.DisplayText),
        TabStop = false,
        IntegralHeight = false,
    };
    private IReadOnlyList<FormulaStructuredReferenceSuggestion> _structuredReferenceSuggestions = [];
    private FormulaTextSpan? _formulaReferenceSpan;
    private FormulaDependency? _provisionalReference;
    private CellAddress? _formulaReferenceAnchor;
    private bool _updatingFormulaText;

    /// <summary>Gets the bounded Table/column candidates in the active native editor.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<FormulaStructuredReferenceSuggestion> CurrentStructuredReferenceSuggestions => _structuredReferenceSuggestions;

    /// <summary>Gets the active draft, or null when the control is not editing.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string? CurrentEditText => IsEditing ? _editor.Text : null;

    /// <summary>Controls visible precedent outlines for the selected or edited formula.</summary>
    [DefaultValue(true)]
    public bool ShowFormulaReferenceHighlights { get; set; } = true;

    /// <summary>Gets active-sheet precedent geometry without evaluating draft formulas.</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<SpreadsheetFormulaReferenceHighlight> CurrentFormulaReferenceHighlights => GetFormulaReferenceHighlights();

    private void InitializeFormulaEditingUi()
    {
        Controls.Add(_formulaSuggestionList);
        _editor.TextChanged += OnFormulaEditorTextChanged;
        _editor.KeyUp += OnFormulaEditorKeyUp;
        _editor.MouseUp += OnFormulaEditorMouseUp;
        _formulaSuggestionList.MouseDown += OnFormulaSuggestionMouseDown;
    }

    private void DisposeFormulaEditingUi()
    {
        _editor.TextChanged -= OnFormulaEditorTextChanged;
        _editor.KeyUp -= OnFormulaEditorKeyUp;
        _editor.MouseUp -= OnFormulaEditorMouseUp;
        _formulaSuggestionList.MouseDown -= OnFormulaSuggestionMouseDown;
        _formulaSuggestionList.Dispose();
        _editor.Region?.Dispose();
    }

    private void OnFormulaEditorTextChanged(object? sender, EventArgs e)
    {
        if (_updatingFormulaText) return;
        _formulaReferenceSpan = null;
        _provisionalReference = null;
        UpdateFormulaSuggestions();
        Invalidate();
    }

    private void OnFormulaEditorKeyUp(object? sender, KeyEventArgs e)
    {
        ClearMovedFormulaReferenceSpan();
        if (e.KeyCode is not (Keys.Up or Keys.Down or Keys.Tab or Keys.Escape or Keys.Enter))
            UpdateFormulaSuggestions();
    }

    private void ClearMovedFormulaReferenceSpan()
    {
        if (_formulaReferenceSpan is { } span &&
            (_editor.SelectionStart != span.End || _editor.SelectionLength != 0))
        {
            _formulaReferenceSpan = null;
            _provisionalReference = null;
        }
    }

    private void OnFormulaEditorMouseUp(object? sender, MouseEventArgs e)
    {
        _formulaReferenceSpan = null;
        _provisionalReference = null;
        UpdateFormulaSuggestions();
    }

    private void UpdateFormulaSuggestions()
    {
        if (_session is null || _cellEditor?.State is not { } state)
        {
            ResetFormulaEditingUi();
            return;
        }
        _structuredReferenceSuggestions = SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(
            _editor.Text, _editor.SelectionStart, _session.Workbook, _session.ActiveWorksheet, state.Address);
        _formulaSuggestionList.DataSource = _structuredReferenceSuggestions.Cast<object>()
            .Concat(_session.FormulaEditing.GetSuggestions(_editor.Text, _editor.SelectionStart)).ToArray();
        _formulaSuggestionList.Visible = _formulaSuggestionList.Items.Count > 0 && _editor.Visible;
        if (_formulaSuggestionList.Items.Count > 0) _formulaSuggestionList.SelectedIndex = 0;
        UpdateFormulaSuggestionBounds();
    }

    private void UpdateFormulaSuggestionBounds()
    {
        if (!_formulaSuggestionList.Visible) return;
        var width = Math.Min(360, ClientSize.Width);
        var height = Math.Min(220, _formulaSuggestionList.Items.Count * _formulaSuggestionList.ItemHeight + 4);
        var top = Math.Clamp(_editor.Bottom, 0, Math.Max(0, ClientSize.Height - height));
        _formulaSuggestionList.Bounds = new Rectangle(Math.Clamp(_editor.Left, 0, Math.Max(0, ClientSize.Width - width)),
            top, width, height);
        _formulaSuggestionList.BringToFront();
    }

    private void ResetFormulaEditingUi()
    {
        _structuredReferenceSuggestions = [];
        _formulaSuggestionList.DataSource = null;
        _formulaSuggestionList.Visible = false;
        _formulaReferenceSpan = null;
        _provisionalReference = null;
        _formulaReferenceAnchor = null;
    }

    private bool TryHandleFormulaSuggestionKey(KeyEventArgs e)
    {
        if (!_formulaSuggestionList.Visible || _formulaSuggestionList.Items.Count == 0 || e.Modifiers != Keys.None)
            return false;
        if (e.KeyCode is Keys.Down or Keys.Up)
        {
            _formulaSuggestionList.SelectedIndex = Math.Clamp(_formulaSuggestionList.SelectedIndex +
                (e.KeyCode == Keys.Down ? 1 : -1), 0, _formulaSuggestionList.Items.Count - 1);
            return true;
        }
        return e.KeyCode == Keys.Tab && ApplySelectedFormulaSuggestion();
    }

    private void OnFormulaSuggestionMouseDown(object? sender, MouseEventArgs e)
    {
        var index = _formulaSuggestionList.IndexFromPoint(e.Location);
        if (e.Button != MouseButtons.Left || index < 0) return;
        _formulaSuggestionList.SelectedIndex = index;
        ApplySelectedFormulaSuggestion();
    }

    private bool ApplySelectedFormulaSuggestion()
    {
        if (_session is null || _cellEditor?.State is not { } state) return false;
        FormulaTextEditResult edit;
        if (_formulaSuggestionList.SelectedItem is FormulaStructuredReferenceSuggestion structured)
        {
            var table = _session.Workbook.Tables.FirstOrDefault(table => table.Id == structured.TableId);
            if (structured.SourceText != _editor.Text || table is null ||
                structured.ColumnId is { } columnId && !table.TryGetColumn(columnId, out _) ||
                structured.Area == TableReferenceArea.ThisRow &&
                (!_session.ActiveWorksheet.Tables.Any(candidate => candidate.Id == table.Id) ||
                 table.DataRange?.Contains(state.Address) != true))
            {
                UpdateFormulaSuggestions();
                _editor.Focus();
                return true;
            }
            edit = SpreadsheetFormulaEditingAssistant.ApplyStructuredReferenceSuggestion(
                _editor.Text, _session.Workbook, _session.ActiveWorksheet, state.Address, structured);
        }
        else if (_formulaSuggestionList.SelectedItem is FormulaFunctionSuggestion function)
            edit = SpreadsheetFormulaEditingAssistant.ApplySuggestion(_editor.Text, _editor.SelectionStart, function);
        else return false;
        SetFormulaEditText(edit);
        _formulaReferenceSpan = null;
        _provisionalReference = null;
        _editor.Focus();
        return true;
    }

    /// <summary>
    /// Inserts an exact Table area or A1 reference using the shared assistant.
    /// Repeated calls replace the provisional span; invalid draft contexts return false.
    /// </summary>
    public bool InsertFormulaReference(CellRange range, string? worksheetName = null)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        // WinForms has no TextBox SelectionChanged event. Validate here as well
        // as on KeyUp so programmatic caret/selection changes cannot reuse a stale span.
        ClearMovedFormulaReferenceSpan();
        if (_session is null || _cellEditor?.State is not { } state ||
            !SpreadsheetFormulaEditingAssistant.CanInsertReference(_editor.Text, _editor.SelectionStart, _formulaReferenceSpan))
            return false;
        var referenceWorksheet = worksheetName is null ? _session.ActiveWorksheet :
            _session.Workbook.Worksheets.FirstOrDefault(sheet => string.Equals(sheet.Name, worksheetName, StringComparison.OrdinalIgnoreCase));
        if (referenceWorksheet is null) return false;
        var edit = SpreadsheetFormulaEditingAssistant.InsertReference(_editor.Text, _editor.SelectionStart,
            _session.Workbook, _session.ActiveWorksheet, state.Address, referenceWorksheet, range, _formulaReferenceSpan);
        SetFormulaEditText(edit);
        _formulaReferenceSpan = edit.InsertedSpan;
        _provisionalReference = new FormulaDependency(referenceWorksheet.Name, range);
        Invalidate();
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
        Invalidate();
    }

    private bool TryBeginFormulaReferencePointer(int x, int y)
    {
        ClearMovedFormulaReferenceSpan();
        if (IsEditing && _editor.Text.StartsWith('=') &&
            !SpreadsheetFormulaEditingAssistant.CanInsertReference(_editor.Text, _editor.SelectionStart, _formulaReferenceSpan))
            return true;
        if (!TryHitFormulaReference(x, y, out var address) ||
            !InsertFormulaReference(new CellRange(address, address))) return false;
        _formulaReferenceAnchor = address;
        _session!.Selection.Select(new CellRange(address, address));
        Capture = true;
        return true;
    }

    private bool UpdateFormulaReferencePointer(int x, int y, bool release = false)
    {
        if (_formulaReferenceAnchor is not { } anchor) return false;
        if (TryHitFormulaReference(x, y, out var address))
        {
            var range = new CellRange(anchor, address);
            InsertFormulaReference(range);
            _session!.Selection.Select(range);
        }
        if (release)
        {
            _formulaReferenceAnchor = null;
            Capture = false;
            _editor.Focus();
        }
        return true;
    }

    private bool TryHitFormulaReference(int x, int y, out CellAddress address)
    {
        address = default;
        if (!IsEditing || _viewport is null) return false;
        var hit = SpreadsheetChromeGeometry.HitTest(x, y, ClientSize.Width, ClientSize.Height, RenderTheme);
        var scroll = _scrollController.Snapshot;
        return hit.Region == SpreadsheetChromeRegion.Body &&
            _viewport.TryHitTest(hit.BodyX, hit.BodyY, scroll.OffsetX, scroll.OffsetY, out address);
    }

    private List<SpreadsheetFormulaReferenceHighlight> GetFormulaReferenceHighlights()
    {
        if (!ShowFormulaReferenceHighlights || _session is null || RenderTheme.FormulaReferenceColors.Count == 0) return [];
        var address = _cellEditor?.State?.Address ?? _session.Selection.ActiveCell;
        var formula = IsEditing ? _editor.Text : _session.ActiveWorksheet.GetFormula(address);
        if (formula is null) return [];
        IReadOnlyList<FormulaDependency> references = IsEditing ? [] :
            _session.Calculation.DependencyGraph.GetDependencies(new FormulaCellKey(_session.ActiveWorksheet.Name, address));
        if (references.Count == 0 && !FormulaReferenceAnalyzer.TryGetReferences(formula, _session.Workbook, _session.ActiveWorksheet, address, out references) &&
            IsEditing && _provisionalReference is { } provisional) references = [provisional];
        var result = new List<SpreadsheetFormulaReferenceHighlight>();
        foreach (var reference in references)
        {
            if (reference.WorksheetName is not null && !string.Equals(reference.WorksheetName, _session.ActiveWorksheet.Name, StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(new SpreadsheetFormulaReferenceHighlight(reference.Range,
                RenderTheme.FormulaReferenceColors[result.Count % RenderTheme.FormulaReferenceColors.Count]));
        }
        return result;
    }
}
