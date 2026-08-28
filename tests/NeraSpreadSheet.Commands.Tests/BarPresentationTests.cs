using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Bars.Core;
using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class BarPresentationTests
{
    [TestMethod]
    public void ProjectShouldPreserveTreeAndAggregateSubmenuAvailability()
    {
        var registry = new CommandRegistry();
        var openHandler = new CountingHandler(canExecute: true);
        var saveHandler = new CountingHandler(canExecute: false);
        registry.Register(
            new CommandDescriptor("file.open", "Mở", shortcut: "Ctrl+O"),
            openHandler);
        registry.Register(
            new CommandDescriptor("file.save", "Lưu"),
            saveHandler);
        var definition = new BarDefinition(
            "main",
            BarKind.MainMenu,
            [
                BarItemDefinition.Submenu(
                    "Tệp",
                    [
                        BarItemDefinition.Command("file.open"),
                        BarItemDefinition.Command("file.save", order: 1),
                        BarItemDefinition.Command("module.optional", order: 2),
                    ],
                    "file"),
                BarItemDefinition.Separator("split", order: 1),
                BarItemDefinition.Command("file.open", "quick.open", order: 2),
            ],
            "Menu chính");
        var projector = new BarPresentationProjector(registry);

        var presentation = projector.Project(definition);

        Assert.AreEqual("main", presentation.Id);
        Assert.AreEqual(BarKind.MainMenu, presentation.Kind);
        Assert.AreEqual("Menu chính", presentation.Caption);
        Assert.AreEqual(3, presentation.Items.Count);

        var submenu = presentation.Items[0];
        Assert.AreEqual(BarItemKind.Submenu, submenu.Kind);
        Assert.AreEqual("Tệp", submenu.Caption);
        Assert.IsTrue(submenu.IsEnabled);
        Assert.AreEqual(3, submenu.Children.Count);
        Assert.AreEqual("Mở", submenu.Children[0].Caption);
        Assert.AreEqual("Ctrl+O", submenu.Children[0].Command!.Shortcut);
        Assert.IsFalse(submenu.Children[1].IsEnabled);
        Assert.IsFalse(submenu.Children[2].Command!.IsRegistered);
        Assert.IsFalse(submenu.Children[2].IsEnabled);

        var separator = presentation.Items[1];
        Assert.AreEqual(BarItemKind.Separator, separator.Kind);
        Assert.IsFalse(separator.IsEnabled);
        Assert.IsNull(separator.Command);

        Assert.AreSame(
            submenu.Children[0].Command,
            presentation.Items[2].Command);
        Assert.AreEqual(1, openHandler.QueryCount);
        Assert.AreEqual(1, saveHandler.QueryCount);
    }

    [TestMethod]
    public void ProjectShouldDisableSubmenuWithoutEnabledDescendants()
    {
        var registry = new CommandRegistry();
        registry.Register(
            new CommandDescriptor("file.save", "Lưu"),
            new CountingHandler(canExecute: false));
        var definition = new BarDefinition(
            "context",
            BarKind.ContextMenu,
            [
                BarItemDefinition.Submenu(
                    "Không sẵn sàng",
                    [
                        BarItemDefinition.Command("file.save"),
                        BarItemDefinition.Command("module.optional", order: 1),
                    ],
                    "disabled"),
            ]);

        var presentation = new BarPresentationProjector(registry)
            .Project(definition);

        Assert.IsFalse(presentation.Items[0].IsEnabled);
    }

    private sealed class CountingHandler : ICommandHandler
    {
        private readonly bool _canExecute;

        public CountingHandler(bool canExecute)
        {
            _canExecute = canExecute;
        }

        public int QueryCount { get; private set; }

        public bool CanExecute(CommandContext context)
        {
            QueryCount++;
            return _canExecute;
        }

        public ValueTask ExecuteAsync(CommandContext context) =>
            ValueTask.CompletedTask;
    }
}
