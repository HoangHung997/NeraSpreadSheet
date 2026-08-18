using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class UndoRedoManagerTests
{
    [TestMethod]
    public void ExecuteUndoAndRedoMaintainExpectedState()
    {
        var value = 0;
        var manager = new UndoRedoManager();
        var operation = new DelegateOperation(
            "Increment",
            () => value++,
            () => value--);
        manager.Execute(operation);
        Assert.AreEqual(1, value);
        Assert.IsTrue(manager.Undo());
        Assert.AreEqual(0, value);
        Assert.IsTrue(manager.Redo());
        Assert.AreEqual(1, value);
    }

    [TestMethod]
    public void TryUndoAndTryRedoReturnTheExecutedOperation()
    {
        var value = 0;
        var manager = new UndoRedoManager();
        var operation = new DelegateOperation(
            "Increment",
            () => value++,
            () => value--);
        manager.Execute(operation);

        Assert.IsTrue(manager.TryUndo(out var undone));
        Assert.AreSame(operation, undone);
        Assert.AreEqual(0, value);
        Assert.IsTrue(manager.TryRedo(out var redone));
        Assert.AreSame(operation, redone);
        Assert.AreEqual(1, value);
    }

    [TestMethod]
    public void NewOperationClearsRedoHistory()
    {
        var value = 0;
        var manager = new UndoRedoManager();
        manager.Execute(new DelegateOperation(
            "One",
            () => value++,
            () => value--));
        manager.Undo();
        manager.Execute(new DelegateOperation(
            "Two",
            () => value += 2,
            () => value -= 2));
        Assert.IsFalse(manager.CanRedo);
        Assert.AreEqual(2, value);
    }

    [TestMethod]
    public void FailedUndoRestoresTheUndoStack()
    {
        var manager = new UndoRedoManager();
        var operation = new DelegateOperation(
            "Fail undo",
            static () => { },
            static () => throw new InvalidOperationException("expected"));
        manager.Execute(operation);

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            manager.TryUndo(out _));

        Assert.IsTrue(manager.CanUndo);
        Assert.IsFalse(manager.CanRedo);
        Assert.AreEqual(operation.Description, manager.NextUndoDescription);
    }

    [TestMethod]
    public void FailedRedoRestoresTheRedoStack()
    {
        var executeCount = 0;
        var manager = new UndoRedoManager();
        var operation = new DelegateOperation(
            "Fail redo",
            () =>
            {
                executeCount++;
                if (executeCount > 1)
                {
                    throw new InvalidOperationException("expected");
                }
            },
            static () => { });
        manager.Execute(operation);
        Assert.IsTrue(manager.Undo());

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            manager.TryRedo(out _));

        Assert.IsFalse(manager.CanUndo);
        Assert.IsTrue(manager.CanRedo);
        Assert.AreEqual(operation.Description, manager.NextRedoDescription);
    }

    private sealed class DelegateOperation : IUndoableOperation
    {
        private readonly Action _execute;
        private readonly Action _undo;

        public DelegateOperation(
            string description,
            Action execute,
            Action undo)
        {
            Description = description;
            _execute = execute;
            _undo = undo;
        }

        public string Description { get; }

        public void Execute() => _execute();

        public void Undo() => _undo();
    }
}
