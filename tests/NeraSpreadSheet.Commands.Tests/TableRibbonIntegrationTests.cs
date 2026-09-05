using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Commands.Tests;

[TestClass]
public sealed class TableRibbonIntegrationTests
{
    [TestMethod]
    public async Task ParameterCancellationShouldLeaveTableIdentityAndHistoryUntouched()
    {
        var (session, runtime) = CreateRuntime();
        var table = session.ActiveWorksheet.Tables.Single();
        var history = session.History.UndoCount;
        var calls = 0;
        runtime.ActivationContextProvider = (_, _, _) => { calls++; return ValueTask.FromResult<CommandContext?>(null); };
        runtime.Refresh();
        Assert.AreEqual(0, calls);
        foreach (var id in new[] { "Table.Rename", "Table.Resize", "Table.CalculatedColumn", "Table.RemoveDuplicates", "Table.ConvertToRange" })
            Assert.IsFalse(await runtime.TryActivateAsync(id));
        Assert.IsFalse(await runtime.TryActivateItemAsync("Table.Style", "TableStyleDark1"));
        Assert.IsFalse(await runtime.TryActivateItemAsync("Table.TotalsFunction", "Custom"));
        Assert.AreEqual(7, calls);
        Assert.AreEqual(history, session.History.UndoCount);
        Assert.AreSame(table, session.ActiveWorksheet.Tables.Single());
    }

    [TestMethod]
    public async Task ParametersShouldFlowThroughPrimaryQatAndSelectableDispatchWithUndo()
    {
        var (session, runtime) = CreateRuntime();
        var table = session.ActiveWorksheet.Tables.Single();
        var history = session.History.UndoCount;
        runtime.SetCustomization(new RibbonCustomization([], [new RibbonQuickAccessItemCustomization("Table.Rename")]));
        runtime.ActivationContextProvider = (id, selected, context) => ValueTask.FromResult<CommandContext?>(context with
        {
            Parameter = id.Value switch
            {
                "Table.Rename" => "RenamedTable", "Table.Resize" => new CellRange(default, new CellAddress(5, 1)),
                "Table.CalculatedColumn" => "=[@Amount]*2", "Table.TotalsFunction" when selected == "Custom" => "=SUM(1,2)",
                "Table.RemoveDuplicates" => new[] { table.Columns[0].Id }, _ => context.Parameter,
            },
        });
        runtime.KeyTips.Enter();
        runtime.KeyTips.OpenQuickAccessToolbar();
        var tip = runtime.KeyTips.GetCommandTips().Single();
        Assert.IsTrue(await runtime.TryActivateAsync(runtime.KeyTips.Process(tip.Key).CommandId!.Value));
        Assert.AreEqual("RenamedTable", session.ActiveWorksheet.Tables.Single().Name);
        Assert.AreEqual(table.Id, session.ActiveWorksheet.Tables.Single().Id);
        Assert.AreEqual(history + 1, session.History.UndoCount);
        Assert.IsTrue(session.Undo());
        Assert.AreEqual(table.Name, session.ActiveWorksheet.Tables.Single().Name);
        Assert.IsTrue(await runtime.TryActivateAsync("Table.Resize"));
        Assert.AreEqual(5, session.ActiveWorksheet.Tables.Single().Range.Bottom);
        Assert.IsTrue(session.Undo());
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        Assert.IsTrue(await runtime.TryActivateAsync("Table.CalculatedColumn"));
        Assert.AreEqual("=[@Amount]*2", session.ActiveWorksheet.Tables.Single().Columns[0].CalculatedColumnFormula);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(await runtime.TryActivateItemAsync("Table.TotalsFunction", "Custom"));
        Assert.AreEqual("=SUM(1,2)", session.ActiveWorksheet.Tables.Single().Columns[0].TotalsRowFormula);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(await runtime.TryActivateItemAsync("Table.Style", "TableStyleDark1"));
        Assert.AreEqual("TableStyleDark1", session.ActiveWorksheet.Tables.Single().StyleName);
        Assert.IsTrue(session.Undo());
        Assert.IsTrue(session.Redo());
        runtime.Refresh();
        Assert.AreEqual("TableStyleDark1", GetCommand(runtime, "Table.Style").SelectedValue);
    }

    [TestMethod]
    public async Task PendingParameterCollectionShouldRecheckVisibilityAndCancellationBeforeDispatch()
    {
        var (session, runtime) = CreateRuntime();
        var history = session.History.UndoCount;
        var pending = new TaskCompletionSource<CommandContext?>();
        runtime.ActivationContextProvider = (_, _, _) => new ValueTask<CommandContext?>(pending.Task);
        var activation = runtime.TryActivateAsync("Table.Rename");
        runtime.SetSelectionContext(new RibbonSelectionContext(true, false));
        pending.SetResult(new CommandContext(Parameter: "ShouldNotApply"));
        Assert.IsFalse(await activation);
        Assert.AreEqual(history, session.History.UndoCount);
        runtime.SetSelectionContext(new RibbonSelectionContext(true, true));
        using var cancellation = new CancellationTokenSource();
        runtime.ActivationContextProvider = (_, _, context) =>
        {
            cancellation.Cancel();
            return ValueTask.FromResult<CommandContext?>(context with { Parameter = "ShouldNotApply" });
        };
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await runtime.TryActivateAsync("Table.Rename", new CommandContext(CancellationToken: cancellation.Token)));
        Assert.AreEqual(history, session.History.UndoCount);
    }

    [TestMethod]
    public async Task DisabledOrReadOnlyHandlerShouldNeverCollectParametersOrMutate()
    {
        var registry = new CommandRegistry();
        var handler = new ReadOnlyHandler();
        registry.Register(new CommandDescriptor("Table.Rename", "Đổi tên", shortcut: "Ctrl+R"), handler);
        var runtime = new RibbonRuntimeController(RibbonProductionCommandCatalog.CreateDefaultDefinition(), registry);
        runtime.SetSelectionContext(new RibbonSelectionContext(true, true));
        runtime.ActivationContextProvider = (_, _, _) => throw new AssertFailedException("Disabled commands must not prompt.");
        Assert.IsFalse(await runtime.TryActivateAsync("Table.Rename"));
        Assert.IsFalse(await runtime.TryActivateShortcutAsync("Ctrl+R"));
        Assert.IsFalse(GetCommand(runtime, "Table.Rename").IsEnabled);
    }

    [TestMethod]
    public void ProductionTableLayoutShouldRetainAllCommandsAcrossWidthsScalesAndCustomization()
    {
        var (_, runtime) = CreateRuntime();
        var ids = runtime.Snapshot.Tabs.Single(tab => tab.Id == "table-design").Groups.SelectMany(group => group.Items)
            .Select(item => item.Command.CommandId).ToArray();
        var engine = new RibbonResponsiveLayoutEngine();
        foreach (var width in new[] { 1920d, 1600d, 1280d, 1024d, 820d })
        foreach (var scale in new[] { 1d, 1.25, 1.5, 2d })
        {
            var layout = engine.Layout(runtime.Snapshot, new RibbonLayoutRequest(width * scale, scale, "table-design", "Table.Style"));
            var tab = layout.Tabs.Single(tab => tab.Presentation.Id == "table-design");
            Assert.IsLessThanOrEqualTo(width * scale, tab.InlineWidth);
            CollectionAssert.AreEquivalent(ids, tab.Groups.SelectMany(group => group.Items).Select(item => item.Presentation.Command.CommandId).ToArray());
            foreach (var group in tab.Groups.Where(group => group.Mode != RibbonGroupLayoutMode.Overflow))
            foreach (var item in group.Items)
            {
                Assert.IsLessThanOrEqualTo(group.Width + 0.001, item.X + item.Width);
                Assert.IsLessThanOrEqualTo(group.CaptionY + 0.001, item.Y + item.Height);
            }
        }
        runtime.SetCustomization(new RibbonCustomization([new RibbonTabCustomization("table-design", groups:
            [new RibbonGroupCustomization("table-styles", items: [new RibbonItemCustomization("Table.Style", IsLarge: false)])])]));
        Assert.AreEqual(RibbonItemKind.Gallery, runtime.EffectiveDefinition.Tabs.Single(tab => tab.Id == "table-design")
            .Groups.Single(group => group.Id == "table-styles").Items.Single().Kind);
    }

    private static CommandPresentation GetCommand(RibbonRuntimeController runtime, string id) => runtime.Snapshot.Tabs
        .SelectMany(tab => tab.Groups).SelectMany(group => group.Items).Single(item => item.Command.CommandId.Value == id).Command;

    private static (SpreadsheetSession Session, RibbonRuntimeController Runtime) CreateRuntime()
    {
        var workbook = new Workbook();
        var sheet = workbook.Worksheets[0];
        sheet.SetValue(default, "Item");
        sheet.SetValue(new CellAddress(0, 1), "Amount");
        sheet.SetValue(new CellAddress(1, 0), "A");
        sheet.SetValue(new CellAddress(1, 1), 2d);
        var session = new SpreadsheetSession(workbook);
        session.Tables.Create(new CellRange(default, new CellAddress(2, 1)), "Sales");
        session.Tables.SetTotalsRow(sheet.Tables.Single().Id, true);
        session.Selection.SetActiveCell(new CellAddress(1, 0));
        var runtime = new RibbonRuntimeController(RibbonProductionCommandCatalog.CreateDefaultDefinition(), session.Commands);
        runtime.SetSelectionContext(new RibbonSelectionContext(true, true));
        return (session, runtime);
    }

    private sealed class ReadOnlyHandler : IStatefulCommandHandler
    {
        public bool CanExecute(CommandContext context) => false;
        public CommandState GetState(CommandContext context) => CommandState.Disabled;
        public ValueTask ExecuteAsync(CommandContext context) => throw new AssertFailedException("Read-only command executed.");
    }
}
