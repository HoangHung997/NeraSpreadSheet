using BenchmarkDotNet.Attributes;
using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Benchmarks;

/// <summary>Measures dense packing and collapse against a large reusable command snapshot.</summary>
[MemoryDiagnoser]
public class RibbonLayoutBenchmarks
{
    private readonly RibbonResponsiveLayoutEngine _engine = new();
    private RibbonPresentationSnapshot _presentation = null!;

    [Params(1536d, 1280d, 1024d, 820d)]
    public double Width { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var registry = new CommandRegistry();
        var tabs = new List<RibbonTabDefinition>();
        for (var tab = 0; tab < 9; tab++)
        {
            var groups = new List<RibbonGroupDefinition>();
            for (var group = 0; group < 8; group++)
            {
                var items = new List<RibbonItemDefinition>();
                for (var item = 0; item < 10; item++)
                {
                    var id = $"tab{tab}.group{group}.item{item}";
                    registry.Register(new CommandDescriptor(id, "Định dạng ô", iconKey: "format.cells"), new Handler());
                    items.Add(new RibbonItemDefinition(id, item == 0));
                }
                groups.Add(new RibbonGroupDefinition($"group{group}", "Định dạng", items, order: group, collapsePriority: 8 - group));
            }
            tabs.Add(new RibbonTabDefinition($"tab{tab}", "Trang đầu", groups));
        }
        _presentation = new RibbonPresentationProjector(registry).Project(new RibbonDefinition(tabs));
    }

    [Benchmark]
    public RibbonLayoutSnapshot PackAndCollapseSevenHundredTwentyCommands() =>
        _engine.Layout(_presentation, new RibbonLayoutRequest(Width, selectedTabId: "tab0"));

    private sealed class Handler : ICommandHandler
    {
        public bool CanExecute(CommandContext context) => true;

        public ValueTask ExecuteAsync(CommandContext context) => ValueTask.CompletedTask;
    }
}
