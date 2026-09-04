using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing;

/// <summary>
/// Applies undoable row and column visibility changes without materializing
/// every entry in a hidden range.
/// </summary>
public sealed class SpreadsheetAxisVisibilityController
{
    private readonly SpreadsheetSession _session;

    /// <summary>Creates a controller bound to a spreadsheet editing session.</summary>
    public SpreadsheetAxisVisibilityController(SpreadsheetSession session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
    }

    /// <summary>Hides a row range as one undoable edit.</summary>
    public void HideRows(int rowIndex, int count = 1) =>
        Execute(WorksheetAxis.Row, rowIndex, count, hidden: true);

    /// <summary>Unhides a row range as one undoable edit.</summary>
    public void UnhideRows(int rowIndex, int count = 1) =>
        Execute(WorksheetAxis.Row, rowIndex, count, hidden: false);

    /// <summary>Hides a column range as one undoable edit.</summary>
    public void HideColumns(int columnIndex, int count = 1) =>
        Execute(WorksheetAxis.Column, columnIndex, count, hidden: true);

    /// <summary>Unhides a column range as one undoable edit.</summary>
    public void UnhideColumns(int columnIndex, int count = 1) =>
        Execute(WorksheetAxis.Column, columnIndex, count, hidden: false);

    private void Execute(
        WorksheetAxis axis,
        int index,
        int count,
        bool hidden)
    {
        _session.Execute(new AxisVisibilityOperation(
            _session.ActiveWorksheet,
            axis,
            index,
            count,
            hidden));
    }

    private sealed class AxisVisibilityOperation : ISpreadsheetEditOperation
    {
        private readonly WorksheetAxis _axis;
        private readonly int _index;
        private readonly int _count;
        private readonly bool _hidden;
        private WorksheetAxisInterval[]? _before;

        public AxisVisibilityOperation(
            Worksheet worksheet,
            WorksheetAxis axis,
            int index,
            int count,
            bool hidden)
        {
            Worksheet = worksheet ?? throw new ArgumentNullException(nameof(worksheet));
            var axisLength = axis == WorksheetAxis.Row
                ? SpreadsheetLimits.MaxRows
                : SpreadsheetLimits.MaxColumns;
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, axisLength);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
            ArgumentOutOfRangeException.ThrowIfGreaterThan(count, axisLength - index);
            _axis = axis;
            _index = index;
            _count = count;
            _hidden = hidden;
            AffectedRange = axis == WorksheetAxis.Row
                ? new CellRange(
                    new CellAddress(index, 0),
                    new CellAddress(index + count - 1, SpreadsheetLimits.MaxColumns - 1))
                : new CellRange(
                    new CellAddress(0, index),
                    new CellAddress(SpreadsheetLimits.MaxRows - 1, index + count - 1));
        }

        public string Description => (_axis, _hidden) switch
        {
            (WorksheetAxis.Row, true) => "Ẩn hàng",
            (WorksheetAxis.Row, false) => "Hiện hàng",
            (WorksheetAxis.Column, true) => "Ẩn cột",
            _ => "Hiện cột",
        };

        public Worksheet Worksheet { get; }

        public CellRange AffectedRange { get; }

        public bool AffectsCalculation => false;

        public void Execute()
        {
            _before ??= _axis == WorksheetAxis.Row
                ? [.. Worksheet.Dimensions.GetHiddenRowRanges()]
                : [.. Worksheet.Dimensions.GetHiddenColumnRanges()];
            Apply(_hidden);
        }

        public void Undo()
        {
            var before = _before ?? throw new InvalidOperationException(
                "The visibility operation has not been executed yet.");
            Worksheet.Dimensions.RestoreHiddenRanges(
                _axis,
                before,
                _index,
                _count);
        }

        private void Apply(bool hidden)
        {
            if (_axis == WorksheetAxis.Row)
            {
                if (hidden)
                {
                    Worksheet.Dimensions.HideRows(_index, _count);
                }
                else
                {
                    Worksheet.Dimensions.UnhideRows(_index, _count);
                }
            }
            else if (hidden)
            {
                Worksheet.Dimensions.HideColumns(_index, _count);
            }
            else
            {
                Worksheet.Dimensions.UnhideColumns(_index, _count);
            }
        }
    }
}
