using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Editing.Tests;

[TestClass]
public sealed class SpreadsheetSessionStateModelFuzzTests
{
    private static readonly int[] Seeds =
    [
        0x10203,
        0x5EED,
        0x1BADB002,
        0x13579B,
        0x2468AC,
        0x314159,
        0x271828,
        0x0C0FFEE,
        0x00C0DE,
        0x0BADC0D,
        0x1234567,
        0x7654321,
    ];

    private static readonly CellAddress[] Addresses =
    [
        new CellAddress(0, 0),
        new CellAddress(0, 1),
        new CellAddress(0, 2),
        new CellAddress(0, 3),
        new CellAddress(1, 0),
        new CellAddress(1, 1),
        new CellAddress(1, 2),
        new CellAddress(1, 3),
        new CellAddress(2, 0),
        new CellAddress(2, 1),
        new CellAddress(2, 2),
        new CellAddress(2, 3),
        new CellAddress(3, 0),
        new CellAddress(3, 1),
        new CellAddress(3, 2),
        new CellAddress(3, 3),
    ];

    private static readonly object?[] Values =
    [
        0d,
        -0d,
        1d,
        -1d,
        0.125d,
        -987654.25d,
        true,
        false,
        "Nera",
        "Việt Nam",
        "  giữ khoảng trắng  ",
        "<xml>&\"quoted\"",
        "",
        new DateTime(2026, 8, 27, 3, 4, 5, DateTimeKind.Utc),
    ];

    [TestMethod]
    public void SeededMixedEditingMatchesIndependentStateAndHistoryModel()
    {
        foreach (var seed in Seeds)
        {
            var workbook = new Workbook();
            var session = new SpreadsheetSession(workbook);
            var model = new ReferenceModel();
            var random = new DeterministicRandom((uint)seed);

            for (var step = 0; step < 192; step++)
            {
                var action = random.Next(100);
                var address = Addresses[random.Next(Addresses.Length)];
                var context = $"seed={seed}, step={step}, action={action}, cell={address.ToA1()}";

                if (action < 55)
                {
                    var value = Values[random.Next(Values.Length)];
                    session.SetValue(address, value);
                    model.SetValue(address, value);
                }
                else if (action < 70)
                {
                    session.Selection.Select(new CellRange(address, address));
                    var expectedChanged = model.Clear(address);
                    var actualChanged = session.ClearSelection();
                    Assert.AreEqual(expectedChanged, actualChanged, context);
                }
                else if (action < 85)
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

            var drainStep = 0;
            while (model.CanUndo)
            {
                var context = $"seed={seed}, undo-drain={drainStep++}";
                Assert.IsTrue(session.Undo(), context);
                Assert.IsTrue(model.Undo(), context);
                AssertMatchesModel(session, model, context);
            }
            Assert.IsFalse(session.Undo(), $"seed={seed}, undo must be exhausted");

            drainStep = 0;
            while (model.CanRedo)
            {
                var context = $"seed={seed}, redo-drain={drainStep++}";
                Assert.IsTrue(session.Redo(), context);
                Assert.IsTrue(model.Redo(), context);
                AssertMatchesModel(session, model, context);
            }
            Assert.IsFalse(session.Redo(), $"seed={seed}, redo must be exhausted");
        }
    }

    [TestMethod]
    public void NewEditAfterGeneratedUndoPrefixAlwaysInvalidatesRedoBranch()
    {
        foreach (var seed in Seeds)
        {
            var workbook = new Workbook();
            var session = new SpreadsheetSession(workbook);
            var model = new ReferenceModel();
            var random = new DeterministicRandom((uint)seed ^ 0xA5A5A5A5u);

            for (var edit = 0; edit < 48; edit++)
            {
                var address = Addresses[random.Next(Addresses.Length)];
                var value = Values[random.Next(Values.Length)];
                session.SetValue(address, value);
                model.SetValue(address, value);
            }

            var undoCount = 1 + random.Next(24);
            for (var index = 0; index < undoCount; index++)
            {
                Assert.IsTrue(session.Undo(), $"seed={seed}, undo={index}");
                Assert.IsTrue(model.Undo(), $"seed={seed}, undo={index}");
            }
            Assert.IsTrue(session.History.CanRedo, $"seed={seed}");
            Assert.IsTrue(model.CanRedo, $"seed={seed}");

            var replacementAddress = Addresses[random.Next(Addresses.Length)];
            var replacementValue = Values[random.Next(Values.Length)];
            session.SetValue(replacementAddress, replacementValue);
            model.SetValue(replacementAddress, replacementValue);

            Assert.IsFalse(session.History.CanRedo, $"seed={seed}");
            Assert.IsFalse(session.Redo(), $"seed={seed}");
            Assert.IsFalse(model.Redo(), $"seed={seed}");
            AssertMatchesModel(session, model, $"seed={seed}, divergent edit");
        }
    }

    private static void AssertMatchesModel(
        SpreadsheetSession session,
        ReferenceModel model,
        string context)
    {
        var worksheet = session.ActiveWorksheet;
        Assert.AreEqual(model.UsedCellCount, worksheet.UsedCellCount, context);
        Assert.AreEqual(model.UndoCount, session.History.UndoCount, context);
        Assert.AreEqual(model.RedoCount, session.History.RedoCount, context);
        Assert.AreEqual(model.CanUndo, session.History.CanUndo, context);
        Assert.AreEqual(model.CanRedo, session.History.CanRedo, context);

        foreach (var address in Addresses)
        {
            var expected = model.GetCell(address);
            var actual = worksheet.GetCell(address);
            var cellContext = $"{context}, compare={address.ToA1()}";
            Assert.AreEqual(expected.Value.Kind, actual.Value.Kind, cellContext);
            Assert.AreEqual(expected.Value.RawValue, actual.Value.RawValue, cellContext);
            Assert.IsNull(actual.Formula, cellContext);
            Assert.AreEqual(CellStyleCatalog.DefaultStyleId, actual.StyleId, cellContext);
            Assert.AreEqual(expected.IsEmpty, actual.IsEmpty, cellContext);
        }
    }

    private readonly record struct ModelCell(CellValue Value)
    {
        public static ModelCell Empty => new(CellValue.Blank);

        public bool IsEmpty => Value.IsBlank;
    }

    private readonly record struct ModelEdit(
        CellAddress Address,
        ModelCell Before,
        ModelCell After);

    private sealed class ReferenceModel
    {
        private readonly Dictionary<CellAddress, ModelCell> _cells = [];
        private readonly Stack<ModelEdit> _undo = [];
        private readonly Stack<ModelEdit> _redo = [];

        public int UsedCellCount => _cells.Count;

        public int UndoCount => _undo.Count;

        public int RedoCount => _redo.Count;

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        public ModelCell GetCell(CellAddress address) =>
            _cells.GetValueOrDefault(address, ModelCell.Empty);

        public void SetValue(CellAddress address, object? value)
        {
            var before = GetCell(address);
            var after = new ModelCell(CellValue.FromObject(value));
            Record(new ModelEdit(address, before, after));
        }

        public bool Clear(CellAddress address)
        {
            var before = GetCell(address);
            if (before.IsEmpty)
            {
                return false;
            }

            Record(new ModelEdit(address, before, ModelCell.Empty));
            return true;
        }

        public bool Undo()
        {
            if (!_undo.TryPop(out var edit))
            {
                return false;
            }

            Apply(edit.Address, edit.Before);
            _redo.Push(edit);
            return true;
        }

        public bool Redo()
        {
            if (!_redo.TryPop(out var edit))
            {
                return false;
            }

            Apply(edit.Address, edit.After);
            _undo.Push(edit);
            return true;
        }

        private void Record(ModelEdit edit)
        {
            Apply(edit.Address, edit.After);
            _undo.Push(edit);
            _redo.Clear();
        }

        private void Apply(CellAddress address, ModelCell cell)
        {
            if (cell.IsEmpty)
            {
                _cells.Remove(address);
                return;
            }

            _cells[address] = cell;
        }
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
