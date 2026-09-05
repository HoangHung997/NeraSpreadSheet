using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Formulas;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

internal sealed partial class NeraSpreadsheetSplitSurface
{
    private readonly ListBox _formulaSuggestionList = new()
    {
        DisplayMember = nameof(FormulaStructuredReferenceSuggestion.DisplayText),
        TabStop = false, IntegralHeight = false, Visible = false,
    };
    private FormulaTextSpan? _formulaReferenceSpan;
    private CellAddress? _formulaReferenceAnchor;
    private CellRange? _provisionalReferenceRange;
    private bool _updatingFormulaText;

    private void InitializeFormulaEditingUi()
    {
        Controls.Add(_formulaSuggestionList);
        _editor.TextChanged += OnFormulaTextChanged;
        _editor.KeyUp += OnFormulaKeyUp;
        _editor.MouseUp += OnFormulaMouseUp;
        _formulaSuggestionList.MouseDown += OnFormulaSuggestionMouseDown;
    }

    private void DisposeFormulaEditingUi()
    {
        _editor.TextChanged -= OnFormulaTextChanged;
        _editor.KeyUp -= OnFormulaKeyUp;
        _editor.MouseUp -= OnFormulaMouseUp;
        _formulaSuggestionList.MouseDown -= OnFormulaSuggestionMouseDown;
        _formulaSuggestionList.Dispose();
        _editor.Region?.Dispose();
    }

    private void ResetFormulaEditingUi()
    {
        var releaseCapture = _formulaReferenceAnchor is not null && Capture;
        _formulaReferenceSpan = null;
        _formulaReferenceAnchor = null;
        if (releaseCapture) Capture = false;
        _provisionalReferenceRange = null;
        _formulaSuggestionList.DataSource = null;
        _formulaSuggestionList.Visible = false;
    }

    private void OnFormulaTextChanged(object? sender, EventArgs e)
    {
        if (_updatingFormulaText) return;
        _formulaReferenceSpan = null;
        _provisionalReferenceRange = null;
        UpdateFormulaSuggestions();
        Invalidate();
    }

    private void ClearMovedFormulaReferenceSpan()
    {
        if (_formulaReferenceSpan is { } span && (_editor.SelectionStart != span.End || _editor.SelectionLength != 0))
        {
            _formulaReferenceSpan = null;
            _provisionalReferenceRange = null;
        }
    }

    private void OnFormulaKeyUp(object? sender, KeyEventArgs e)
    {
        ClearMovedFormulaReferenceSpan();
        if (e.KeyCode is not (Keys.Up or Keys.Down or Keys.Tab or Keys.Escape or Keys.Enter)) UpdateFormulaSuggestions();
    }

    private void OnFormulaMouseUp(object? sender, MouseEventArgs e)
    {
        ClearMovedFormulaReferenceSpan();
        UpdateFormulaSuggestions();
    }

    private void UpdateFormulaSuggestions()
    {
        if (_session is null || _cellEditor?.State is not { } state || _editor.SelectionLength != 0)
        {
            ResetFormulaEditingUi();
            return;
        }
        _formulaSuggestionList.DataSource = SpreadsheetFormulaEditingAssistant.GetStructuredReferenceSuggestions(
            _editor.Text, _editor.SelectionStart, _session.Workbook, _session.ActiveWorksheet, state.Address)
            .Cast<object>().Concat(_session.FormulaEditing.GetSuggestions(_editor.Text, _editor.SelectionStart)).ToArray();
        if (_formulaSuggestionList.Items.Count > 0) _formulaSuggestionList.SelectedIndex = 0;
        _formulaSuggestionList.Visible = _editor.Visible && _formulaSuggestionList.Items.Count > 0;
        UpdateFormulaSuggestionBounds();
    }

    private void UpdateFormulaSuggestionBounds()
    {
        if (!_formulaSuggestionList.Visible) return;
        var width = Math.Min(360, ClientSize.Width);
        var height = Math.Min(Math.Min(220, ClientSize.Height), _formulaSuggestionList.Items.Count * _formulaSuggestionList.ItemHeight + 4);
        _formulaSuggestionList.Bounds = new Rectangle(Math.Clamp(_editor.Left, 0, Math.Max(0, ClientSize.Width - width)),
            Math.Clamp(_editor.Bottom, 0, Math.Max(0, ClientSize.Height - height)), width, height);
        _formulaSuggestionList.BringToFront();
    }

    private bool TryHandleFormulaSuggestionKey(KeyEventArgs e)
    {
        if (!_formulaSuggestionList.Visible || _formulaSuggestionList.Items.Count == 0 || e.Modifiers != Keys.None) return false;
        if (e.KeyCode is Keys.Up or Keys.Down)
        {
            _formulaSuggestionList.SelectedIndex = Math.Clamp(_formulaSuggestionList.SelectedIndex + (e.KeyCode == Keys.Down ? 1 : -1),
                0, _formulaSuggestionList.Items.Count - 1);
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
        FormulaTextEditResult? edit;
        if (_formulaSuggestionList.SelectedItem is FormulaStructuredReferenceSuggestion structured)
        {
            if (!SpreadsheetFormulaEditingAssistant.TryApplyStructuredReferenceSuggestion(_editor.Text, _editor.SelectionStart,
                    _editor.SelectionLength, _session.Workbook, _session.ActiveWorksheet, state.Address, structured, out edit))
            {
                UpdateFormulaSuggestions();
                _editor.Focus();
                return true;
            }
        }
        else if (_formulaSuggestionList.SelectedItem is FormulaFunctionSuggestion function)
            edit = SpreadsheetFormulaEditingAssistant.ApplySuggestion(_editor.Text, _editor.SelectionStart, function);
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
        Invalidate();
    }

    private bool TryInsertFormulaReference(int x, int y)
    {
        ClearMovedFormulaReferenceSpan();
        if (_session is null || _cellEditor?.State is not { } state || !_editor.Text.StartsWith('=')) return false;
        if (!SpreadsheetFormulaEditingAssistant.CanInsertReference(_editor.Text, _editor.SelectionStart, _formulaReferenceSpan)) return true;
        if (!TryHitTest(x, y, out _, out var address)) return true;
        _formulaReferenceAnchor = address;
        Capture = true;
        return UpdateFormulaReferencePointer(x, y);
    }

    private bool UpdateFormulaReferencePointer(int x, int y, bool release = false)
    {
        if (_formulaReferenceAnchor is not { } anchor || _session is null || _cellEditor?.State is not { } state) return false;
        if (TryHitTest(x, y, out _, out var address))
        {
            var range = new CellRange(anchor, address);
            var edit = SpreadsheetFormulaEditingAssistant.InsertReference(_editor.Text, _editor.SelectionStart,
                _session.Workbook, _session.ActiveWorksheet, state.Address, _session.ActiveWorksheet,
                range, _formulaReferenceSpan);
            SetFormulaEditText(edit);
            _formulaReferenceSpan = edit.InsertedSpan;
            _provisionalReferenceRange = range;
        }
        if (release)
        {
            _formulaReferenceAnchor = null;
            Capture = false;
            _editor.Focus();
        }
        Invalidate();
        return true;
    }

    private DisplayList ComposeFormulaHighlights(SpreadsheetSplitViewportFrame frame)
    {
        if (_session is null || _cellEditor?.State is not { } state || !_editor.Text.StartsWith('=') ||
            _owner.RenderTheme.FormulaReferenceColors.Count == 0) return frame.DisplayList;
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
