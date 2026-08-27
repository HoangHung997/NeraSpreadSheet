using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetStructureStateModelFuzzTests
{
    private static readonly int[] Seeds =
    [
        73,
        211,
        577,
        1223,
        4099,
        8191,
        16381,
        32749,
    ];

    [TestMethod]
    public void SeededRowColumnTransformsMatchIndependentStateAndHistoryModel()
    {
        foreach (var seed in Seeds)
        {
            var workbook = new Workbook();
            var worksheet = workbook.Worksheets[0];
            var initial = CreateInitialState(seed);
            worksheet.SetCells(initial.Select(static pair =>
                new KeyValuePair<CellAddress, CellData>(
                    pair.Key,
                    new CellData(pair.Value))));

            var session = new SpreadsheetSession(workbook);
            var model = new ReferenceModel(initial);
            var random = new DeterministicRandom((uint)seed);

            AssertMatchesModel(session, model, $"seed={seed}, initial");

            for (var step = 0; step < 128; step++)
            {
                var action = random.Next(100);
                var context = $"seed={seed}, step={step}, action={action}";

                if (action < 22)
                {
                    var rowIndex = random.Next(32);
                    var count = 1 + random.Next(3);
                    if (model.CanInsertRows(rowIndex, count))
                    {
                        session.Structure.InsertRows(rowIndex, count);
                        model.InsertRows(rowIndex, count);
                    }
                    else
                    {
                        Assert.ThrowsExactly<InvalidOperationException>(
                            () => session.Structure.InsertRows(rowIndex, count));
                    }
                }
                else if (action < 44)
                {
                    var rowIndex = random.Next(32);
                    var count = 1 + random.Next(3);
                    session.Structure.DeleteRows(rowIndex, count);
                    model.DeleteRows(rowIndex, count);
                }
                else if (action < 62)
                {
                    var columnIndex = random.Next(24);
                    var count = 1 + random.Next(3);
                    if (model.CanInsertColumns(columnIndex, count))
                    {
                        session.Structure.InsertColumns(columnIndex, count);
                        model.InsertColumns(columnIndex, count);
                    }
                    else
                    {
                        Assert.ThrowsExactly<InvalidOperationException>(
                            () => session.Structure.InsertColumns(columnIndex, count));
                    }
                }
                else if (action < 80)
                {
                    var columnIndex = random.Next(24);
                    var count = 1 + random.Next(3);
                    session.Structure.DeleteColumns(columnIndex, count);
                    model.DeleteColumns(columnIndex, count);
                }
                else if (action < 90)
                {
                    var expected = model.Undo();
                    var actual = session.Undo();
                    Assert.AreEqual(expected, actual, context);
                }
                else
                {
                    var expected = model.Redo();
                    var actual = session.Redo();
                    Assert.AreEqual(expected, actual, context);
                }

                AssertMatchesModel(session, model, context);
            }

            var undoDrain = 0;
            while (model.CanUndo)
            {
                var context = $"seed={seed}, undo-drain={undoDrain++}";
                Assert.IsTrue(session.Undo(), context);
                Assert.IsTrue(model.Undo(), context);
                AssertMatchesModel(session, model, context);
            }
            Assert.IsFalse(session.Undo(), $"seed={seed}, undo must be exhausted");

            var redoDrain = 0;
            while (model.CanRedo)
            {
                var context = $"seed={seed}, redo-drain={redoDrain++}";
                Assert.IsTrue(session.Redo(), context);
                Assert.IsTrue(model.Redo(), context);
                AssertMatchesModel(session, model, context);
            }
            Assert.IsFalse(session.Redo(), $"seed={seed}, redo must be exhausted");
        }
    }

    private static Dictionary<CellAddress, CellValue> CreateInitialState(int seed)
    {
        var cells = new Dictionary<CellAddress, CellValue>();
        for (var row = 0; row < 6; row++)
        {
            for (var column = 0; column < 6; column++)
            {
                if ((row + column + seed) % 4 == 0)
                {
                    continue;
                }

                var address = new CellAddress(row, column);
                cells[address] = CellValue.FromText(
                    $"seed={seed};r={row};c={column}");
            }
        }

        cells[new CellAddress(31, 23)] = CellValue.FromText("local-boundary");
        cells[new CellAddress(1024, 128)] = CellValue.FromText("sparse-middle");
        cells[new CellAddress(SpreadsheetLimits.MaxRows - 1, 0)] =
            CellValue.FromText("last-row");
        cells[new CellAddress(0, SpreadsheetLimits.MaxColumns - 1)] =
            CellValue.FromText("last-column");
        cells[new CellAddress(
            SpreadsheetLimits.MaxRows - 1,
            SpreadsheetLimits.MaxColumns - 1)] =
            CellValue.FromText("last-cell");
        return cells;
    }

    private static void AssertMatchesModel(
        SpreadsheetSession session,
        ReferenceModel model,
        string context)
    {
        var worksheet = session.ActiveWorksheet;
        Assert.AreEqual(model.Cells.Count, worksheet.UsedCellCount, context);
        Assert.AreEqual(model.UndoCount, session.History.UndoCount, context);
        Assert.AreEqual(model.RedoCount, session.History.RedoCount, context);
        Assert.AreEqual(model.CanUndo, session.History.CanUndo, context);
        Assert.AreEqual(model.CanRedo, session.History.CanRedo, context);

        var actual = worksheet.EnumerateUsedCells()
            .OrderBy(static pair => pair.Key.RowIndex)
            .ThenBy(static pair => pair.Key.ColumnIndex)
            .ToArray();
        var expected = model.Cells
            .OrderBy(static pair => pair.Key.RowIndex)
            .ThenBy(static pair => pair.Key.ColumnIndex)
            .ToArray();
        Assert.AreEqual(expected.Length, actual.Length, context);

        for (var index = 0; index < expected.Length; index++)
        {
            var expectedPair = expected[index];
            var actualPair = actual[index];
            var cellContext = $"{context}, index={index}, expected={expectedPair.Key.ToA1()}";
            Assert.AreEqual(expectedPair.Key, actualPair.Key, cellContext);
            Assert.AreEqual(expectedPair.Value.Kind, actualPair.Value.Value.Kind, cellContext);
            Assert.AreEqual(expectedPair.Value.RawValue, actualPair.Value.Value.RawValue, cellContext);
            Assert.IsNull(actualPair.Value.Formula, cellContext);
            Assert.AreEqual(
                CellStyleCatalog.DefaultStyleId,
                actualPair.Value.StyleId,
                cellContext);
        }
    }

    private sealed class ReferenceModel
    {
        private Dictionary<CellAddress, CellValue> _cells;
        private readonly Stack<StateTransition> _undo = [];
        private readonly Stack<StateTransition> _redo = [];

        public ReferenceModel(Dictionary<CellAddress, CellValue> initial)
        {
            _cells = Copy(initial);
        }

        public Dictionary<CellAddress, CellValue> Cells => _cells;

        public int UndoCount => _undo.Count;

        public int RedoCount => _redo.Count;

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        public bool CanInsertRows(int index, int count) =>
            _cells.Keys.All(address =>
                address.RowIndex < index ||
                (long)address.RowIndex + count < SpreadsheetLimits.MaxRows);

        public bool CanInsertColumns(int index, int count) =>
            _cells.Keys.All(address =>
                address.ColumnIndex < index ||
                (long)address.ColumnIndex + count < SpreadsheetLimits.MaxColumns);

        public void InsertRows(int index, int count) =>
            Record(TransformRows(_cells, index, count, insert: true));

        public void DeleteRows(int index, int count) =>
            Record(TransformRows(_cells, index, count, insert: false));

        public void InsertColumns(int index, int count) =>
            Record(TransformColumns(_cells, index, count, insert: true));

        public void DeleteColumns(int index, int count) =>
            Record(TransformColumns(_cells, index, count, insert: false));

        public bool Undo()
        {
            if (!_undo.TryPop(out var transition))
            {
                return false;
            }

            _cells = Copy(transition.Before);
            _redo.Push(transition);
            return true;
        }

        public bool Redo()
        {
            if (!_redo.TryPop(out var transition))
            {
                return false;
            }

            _cells = Copy(transition.After);
            _undo.Push(transition);
            return true;
        }

        private void Record(Dictionary<CellAddress, CellValue> after)
        {
            var transition = new StateTransition(Copy(_cells), Copy(after));
            _cells = after;
            _undo.Push(transition);
            _redo.Clear();
        }

        private static Dictionary<CellAddress, CellValue> TransformRows(
            IReadOnlyDictionary<CellAddress, CellValue> source,
            int index,
            int count,
            bool insert)
        {
            var result = new Dictionary<CellAddress, CellValue>();
            var deleteEnd = index + count - 1;
            foreach (var (address, value) in source)
            {
                if (insert)
                {
                    if (address.RowIndex < index)
                    {
                        result[address] = value;
                        continue;
                    }

                    var shifted = (long)address.RowIndex + count;
                    if (shifted < SpreadsheetLimits.MaxRows)
                    {
                        result[new CellAddress((int)shifted, address.ColumnIndex)] = value;
                    }
                    continue;
                }

                if (address.RowIndex >= index && address.RowIndex <= deleteEnd)
                {
                    continue;
                }

                var targetRow = address.RowIndex > deleteEnd
                    ? address.RowIndex - count
                    : address.RowIndex;
                result[new CellAddress(targetRow, address.ColumnIndex)] = value;
            }
            return result;
        }

        private static Dictionary<CellAddress, CellValue> TransformColumns(
            IReadOnlyDictionary<CellAddress, CellValue> source,
            int index,
            int count,
            bool insert)
        {
            var result = new Dictionary<CellAddress, CellValue>();
            var deleteEnd = index + count - 1;
            foreach (var (address, value) in source)
            {
                if (insert)
                {
                    if (address.ColumnIndex < index)
                    {
                        result[address] = value;
                        continue;
                    }

                    var shifted = (long)address.ColumnIndex + count;
                    if (shifted < SpreadsheetLimits.MaxColumns)
                    {
                        result[new CellAddress(address.RowIndex, (int)shifted)] = value;
                    }
                    continue;
                }

                if (address.ColumnIndex >= index && address.ColumnIndex <= deleteEnd)
                {
                    continue;
                }

                var targetColumn = address.ColumnIndex > deleteEnd
                    ? address.ColumnIndex - count
                    : address.ColumnIndex;
                result[new CellAddress(address.RowIndex, targetColumn)] = value;
            }
            return result;
        }

        private static Dictionary<CellAddress, CellValue> Copy(
            IReadOnlyDictionary<CellAddress, CellValue> source) =>
            source.ToDictionary(static pair => pair.Key, static pair => pair.Value);

        private sealed record StateTransition(
            Dictionary<CellAddress, CellValue> Before,
            Dictionary<CellAddress, CellValue> After);
    }

    private sealed class DeterministicRandom
    {
        private uint _state;

        public DeterministicRandom(uint seed)
        {
            _state = seed == 0 ? 0x6D2B79F5u : seed;
        }

        public int Next(int exclusiveMaximum)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(exclusiveMaximum);
            var value = NextUInt32();
            return (int)(value % (uint)exclusiveMaximum);
        }

        private uint NextUInt32()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value;
            return value;
        }
    }
}
