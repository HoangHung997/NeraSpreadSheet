using global::Avalonia;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Formulas;

namespace NeraSpreadSheet.Avalonia;

public sealed partial class NeraSpreadsheetControl
{
    private ReferenceBorderDrag? _referenceBorderDrag;
    public bool IsDraggingFormulaReferenceBorder => _referenceBorderDrag is not null;

    internal void BeginReferenceGesture(NeraSpreadsheetControl target, Point point, CellAddress address)
    {
        if (!TryBeginReferenceBorder(target, point, address)) BeginPointReference(address);
    }
    private bool TryBeginReferenceBorder(NeraSpreadsheetControl target, Point point, CellAddress address)
    {
        if (!IsFormulaDraft || _session is null || !ReferenceEquals(_session, target._session) || target._viewport is null || !target._showFormulaHighlights) return false;
        var chrome = target.Chrome;
        if (chrome.BodyWidth <= 0 || chrome.BodyHeight <= 0) return false;
        var scroll = target.ScrollSnapshot;
        var layout = target._viewport.Compose(scroll.OffsetX, scroll.OffsetY, chrome.BodyWidth, chrome.BodyHeight, 64, target._renderTheme).Layout;
        var bodyPoint = new PointD(point.X / target._zoom - chrome.RowHeaderWidth, point.Y / target._zoom - chrome.ColumnHeaderHeight);
        var tolerance = 5d / target._zoom;
        var references = EditableFormulaReference.Locate(EditorText)
            .Where(reference => reference.WorksheetName is null || string.Equals(reference.WorksheetName, _session.ActiveWorksheet.Name, StringComparison.OrdinalIgnoreCase))
            .OrderBy(reference => Math.Abs(reference.Span.End - _editor.CaretIndex));
        foreach (var reference in references)
        {
            CellAddress? opposite = null;
            var handleHit = false;
            foreach (var handle in FormulaReferenceOutlineGeometry.Handles(layout, reference.Range))
                if (Math.Abs(bodyPoint.X - handle.Position.X) <= tolerance && Math.Abs(bodyPoint.Y - handle.Position.Y) <= tolerance)
                {
                    opposite = handle.IsTopLeft ? reference.Range.BottomRight : reference.Range.TopLeft;
                    handleHit = true; break;
                }
            if (!handleHit && !FormulaReferenceOutlineGeometry.Edges(layout, reference.Range).Any(edge => FormulaReferenceOutlineGeometry.Distance(bodyPoint, edge) <= tolerance)) continue;
            _referenceBorderDrag = new ReferenceBorderDrag(CurrentEditorDraft!, reference, address, opposite, _session.Workbook.Version);
            _pointAnchor = address;
            return true;
        }
        return false;
    }
    private bool UpdateReferenceBorder(CellAddress address)
    {
        if (_referenceBorderDrag is not { } drag || _session is null || !IsEditing) return false;
        if (_session.Workbook.Version != drag.WorkbookVersion) { EndReferenceBorder(false); EndPointReference(false); return false; }
        CellRange range;
        if (drag.OppositeCorner is { } anchor) range = new CellRange(anchor, address);
        else
        {
            var source = drag.Reference.Range;
            var row = Math.Clamp(address.RowIndex - drag.StartCell.RowIndex, -source.Top, SpreadsheetLimits.MaxRows - 1 - source.Bottom);
            var column = Math.Clamp(address.ColumnIndex - drag.StartCell.ColumnIndex, -source.Left, SpreadsheetLimits.MaxColumns - 1 - source.Right);
            range = new CellRange(new CellAddress(source.Top + row, source.Left + column), new CellAddress(source.Bottom + row, source.Right + column));
        }
        var replacement = drag.Reference.Format(range);
        var span = drag.Reference.Span;
        var text = string.Concat(drag.Draft.Text.AsSpan(0, span.Start), replacement, drag.Draft.Text.AsSpan(span.End));
        var nextSpan = new FormulaTextSpan(span.Start, replacement.Length);
        ApplyFormulaText(new FormulaTextEditResult(text, nextSpan.End, nextSpan));
        _provisionalSpan = nextSpan; _provisionalDependency = new FormulaDependency(drag.Reference.WorksheetName, range);
        RefreshFormulaAssistance(); RefreshFormulaHighlights(); NotifyDraftChanged();
        return true;
    }
    private void EndReferenceBorder(bool keepChanges)
    {
        var drag = _referenceBorderDrag; _referenceBorderDrag = null;
        if (keepChanges || drag is null || !IsEditing) return;
        UpdateEditorDraft(drag.Draft.Text, drag.Draft.SelectionStart, drag.Draft.SelectionEnd);
    }
    private sealed record ReferenceBorderDrag(SpreadsheetEditorDraft Draft, EditableFormulaReference Reference,
        CellAddress StartCell, CellAddress? OppositeCorner, long WorkbookVersion);
}
