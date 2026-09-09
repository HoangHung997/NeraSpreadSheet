using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;

namespace NeraSpreadSheet.Windows.Rendering.Tests;

public sealed partial class Release009RibbonCommandSmokeTests
{
    [TestMethod]
    [DataRow("Edit.PasteValues")]
    [DataRow("Edit.PasteFormulas")]
    [DataRow("Edit.PasteFormats")]
    [Timeout(60_000)]
    public void LoadedShellShouldRouteIntegratedPasteModesWithoutLosingUndo(string command) => RunLoaded(
        (window, session, runtime, registry, _) =>
        {
            var sheet = session.ActiveWorksheet;
            var style = session.Workbook.Styles.Intern(new CellStyle
            {
                Alignment = new CellAlignmentStyle { WrapText = true },
            });
            sheet.SetFormula(default, "=40+2");
            sheet.SetStyle(default, style);
            session.Recalculate();
            var destination = new CellAddress(3, 3);
            sheet.SetValue(destination, 9d);
            var before = sheet.GetCell(destination);
            var history = session.History.UndoCount;
            session.Clipboard.CopyPrimarySelection();
            session.Selection.SetActiveCell(destination);
            runtime.Refresh();
            Pump(window);
            Assert.IsTrue(registry.TryResolve(command, out var descriptor, out var handler));
            Assert.IsNotNull(descriptor);
            Assert.IsNotNull(handler);
            Activate(window, runtime, command);
            Assert.AreEqual(history + 1, session.History.UndoCount);
            Assert.AreEqual(command == "Edit.PasteFormats" ? 9d : 42d, sheet.GetValue(destination));
            Assert.AreEqual(command == "Edit.PasteFormulas" ? "=40+2" : null, sheet.GetFormula(destination));
            Assert.AreEqual(command == "Edit.PasteFormats" ? style : before.StyleId, sheet.GetCell(destination).StyleId);
            Activate(window, runtime, "Edit.Undo");
            Assert.AreEqual(before, sheet.GetCell(destination));
            Assert.AreEqual(history, session.History.UndoCount);
        });

    [TestMethod]
    [Timeout(60_000)]
    public void LoadedShellShouldCancelCopyModeWithoutChangingCellsOrHistory() => RunLoaded(
        (window, session, runtime, registry, _) =>
        {
            var before = session.ActiveWorksheet.GetCell(default);
            var history = session.History.UndoCount;
            session.Clipboard.CopyPrimarySelection();
            Assert.IsTrue(session.Clipboard.CanCancelCopyMode);
            Assert.IsTrue(registry.TryResolve("Edit.CancelCopyMode", out var descriptor, out var handler));
            Assert.IsNotNull(descriptor);
            Assert.IsNotNull(handler);
            Assert.IsNull(descriptor.Shortcut);
            runtime.Refresh();
            Pump(window);
            Activate(window, runtime, "Edit.CancelCopyMode");
            Assert.IsFalse(session.Clipboard.CanCancelCopyMode);
            Assert.AreEqual(before, session.ActiveWorksheet.GetCell(default));
            Assert.AreEqual(history, session.History.UndoCount);
        });
}
