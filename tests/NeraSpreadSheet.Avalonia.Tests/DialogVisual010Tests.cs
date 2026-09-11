using global::Avalonia;
using global::Avalonia.Automation;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.VisualTree;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;

namespace NeraSpreadSheet.Avalonia.Tests;

[TestClass]
public sealed class DialogVisual010Tests
{
    [TestMethod]
    public async Task SettingsDialogsShouldOwnInitialKeyboardFocusAndDefaultCancelButtons()
    {
        foreach (var factory in DialogFactories())
        {
            Fixture? fixture = null;
            await AvaloniaTestEnvironment.OnUiAsync(() => fixture = new Fixture(factory()));
            try
            {
                await AvaloniaTestEnvironment.OnUiAsync(() =>
                {
                    var dialog = fixture!.Dialog;
                    var focused = TopLevel.GetTopLevel(dialog)?.FocusManager?.GetFocusedElement() as Control;
                    Assert.IsNotNull(focused, dialog.GetType().Name + " should establish initial focus.");
                    Assert.IsTrue(dialog.GetVisualDescendants().Contains(focused), dialog.GetType().Name + " focus must remain inside the modal.");
                    var focusedId = AutomationProperties.GetAutomationId(focused);
                    Assert.AreNotEqual("dialog-ok", focusedId);
                    Assert.AreNotEqual("dialog-cancel", focusedId);
                    var ok = fixture.Find<Button>("dialog-ok");
                    var cancel = fixture.Find<Button>("dialog-cancel");
                    Assert.IsTrue(ok.IsDefault);
                    Assert.IsTrue(cancel.IsCancel);
                    Assert.AreEqual(KeyboardNavigationMode.Cycle, KeyboardNavigation.GetTabNavigation((Control)dialog.Content!));
                });
            }
            finally
            {
                await AvaloniaTestEnvironment.OnUiAsync(() => fixture?.Dispose());
            }
        }
    }

    [TestMethod]
    public async Task CompactDialogBoundsShouldKeepConfirmationFooterVisible()
    {
        foreach (var factory in DialogFactories())
        {
            Fixture? fixture = null;
            await AvaloniaTestEnvironment.OnUiAsync(() => fixture = new Fixture(factory()));
            try
            {
                await AvaloniaTestEnvironment.OnUiAsync(() =>
                {
                    var dialog = fixture!.Dialog;
                    dialog.Width = dialog.MinWidth;
                    dialog.Height = dialog.MinHeight;
                    dialog.UpdateLayout();
                    var client = (Control)dialog.Content!;
                    foreach (var id in new[] { "dialog-ok", "dialog-cancel" })
                    {
                        var button = fixture.Find<Button>(id);
                        var position = button.TranslatePoint(default, client)
                            ?? throw new AssertFailedException(id + " detached from dialog client.");
                        Assert.IsTrue(position.X >= -1 && position.Y >= -1, id + " starts outside compact client.");
                        Assert.IsTrue(position.X + button.Bounds.Width <= client.Bounds.Width + 1, id + " clips horizontally.");
                        Assert.IsTrue(position.Y + button.Bounds.Height <= client.Bounds.Height + 1, id + " clips vertically.");
                        Assert.IsTrue(button.Bounds.Width >= 80 && button.Bounds.Height >= 28, id + " touch/keyboard target is too small.");
                    }
                });
            }
            finally
            {
                await AvaloniaTestEnvironment.OnUiAsync(() => fixture?.Dispose());
            }
        }
    }

    [TestMethod]
    public async Task EscapeShouldCloseWithoutMutationAndValidationShouldRestoreInputFocus()
    {
        var session = new SpreadsheetSession(new Workbook());
        NeraFormatCellsDialog? dialog = null;
        Window? owner = null;
        await AvaloniaTestEnvironment.OnUiAsync(() =>
        {
            owner = new Window { Width = 900, Height = 700 };
            owner.Show();
            dialog = new NeraFormatCellsDialog(session);
            _ = dialog.ShowDialog<bool>(owner);
            dialog.UpdateLayout();
        });
        try
        {
            await AvaloniaTestEnvironment.OnUiAsync(() =>
            {
                var code = Find<TextBox>(dialog!, "format-code");
                code.Text = "\"unfinished";
                Find<Button>(dialog!, "dialog-ok").RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            });
            await AvaloniaTestEnvironment.OnUiAsync(() =>
            {
                Assert.IsTrue(dialog!.IsVisible);
                Assert.IsTrue(Find<TextBlock>(dialog, "dialog-validation").IsVisible);
                var focused = TopLevel.GetTopLevel(dialog)?.FocusManager?.GetFocusedElement() as Control;
                Assert.IsNotNull(focused);
                Assert.IsTrue(focused is TextBox or ComboBox);
                Assert.AreEqual(0, session.History.UndoCount);
                dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
                Assert.IsFalse(dialog.IsVisible);
                Assert.AreEqual(0, session.History.UndoCount);
            });
        }
        finally
        {
            await AvaloniaTestEnvironment.OnUiAsync(() =>
            {
                dialog?.Close();
                owner?.Close();
            });
        }
    }

    private static IEnumerable<Func<NeraSettingsDialog>> DialogFactories()
    {
        yield return () => new NeraFormatCellsDialog(new SpreadsheetSession(new Workbook()));
        yield return () => new NeraPageSetupDialog(new SpreadsheetSession(new Workbook()));
        yield return () => new NeraZoomDialog(1);
        yield return () => new NeraDataValidationDialog(new SpreadsheetSession(new Workbook()));
        yield return () => new NeraAdvancedFilterDialog(new SpreadsheetSession(new Workbook()));
        yield return () => new NeraConsolidateDialog(new SpreadsheetSession(new Workbook()));
        yield return () => new NeraProtectSheetDialog(new SpreadsheetSession(new Workbook()));
        yield return () => new NeraProtectWorkbookDialog(new SpreadsheetSession(new Workbook()));
    }

    private static T Find<T>(Window dialog, string id) where T : Control =>
        dialog.GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == id);

    private sealed class Fixture : IDisposable
    {
        private readonly Window _owner = new() { Width = 1000, Height = 780 };
        internal NeraSettingsDialog Dialog { get; }

        internal Fixture(NeraSettingsDialog dialog)
        {
            Dialog = dialog;
            _owner.Show();
            _ = Dialog.ShowDialog<bool>(_owner);
            Dialog.UpdateLayout();
        }

        internal T Find<T>(string id) where T : Control =>
            Dialog.GetVisualDescendants().OfType<T>().Single(control => AutomationProperties.GetAutomationId(control) == id);

        public void Dispose()
        {
            Dialog.Close();
            _owner.Close();
        }
    }
}
