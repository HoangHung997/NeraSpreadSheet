using NeraSpreadSheet.Commands;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Maui;

public sealed class NeraMauiRibbonCustomizationBinding
{
    private readonly RibbonRuntimeController _runtime;
    private readonly RibbonCustomizationSession _session;

    public NeraMauiRibbonCustomizationBinding(
        RibbonRuntimeController runtime,
        Func<CommandId, string>? commandCaption = null)
        : this(runtime, commandCaption, null)
    {
    }

    public NeraMauiRibbonCustomizationBinding(
        RibbonRuntimeController runtime,
        Func<CommandId, string>? commandCaption,
        RibbonCustomizationPolicy? policy)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _session = new RibbonCustomizationSession(
            runtime.Definition,
            runtime.CommandCatalog,
            runtime.Customization,
            commandCaption,
            policy);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<RibbonCustomizationEntry> Entries => _session.Entries;

    public RibbonCustomization CreateCustomization() =>
        _session.CreateCustomization();

    public RibbonDefinition Preview(CommandContext context = default)
    {
        Publish(context);
        return _session.Preview();
    }

    public void Apply(CommandContext context = default)
    {
        _runtime.SetCustomization(_session.Commit(), context);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Cancel(CommandContext context = default)
    {
        _session.Cancel();
        Publish(context);
    }

    public RibbonCustomizationTarget AddCustomTab(string tabId, string caption) => _session.AddTab(tabId, caption);
    public RibbonCustomizationTarget AddCustomGroup(string tabId, string groupId, string caption) => _session.AddGroup(tabId, groupId, caption);
    public RibbonCustomizationTarget MoveCommand(RibbonCustomizationTarget source, string tabId, string groupId, int index = int.MaxValue) => _session.MoveCommand(source, tabId, groupId, index);
    public bool Rename(RibbonCustomizationTarget target, string caption) => _session.Rename(target, caption);
    public bool Remove(RibbonCustomizationTarget target) => _session.Remove(target);
    public bool AddToQuickAccessToolbar(CommandId commandId, int index = int.MaxValue) => _session.AddToQuickAccessToolbar(commandId, index);
    public bool RemoveFromQuickAccessToolbar(CommandId commandId) => _session.RemoveFromQuickAccessToolbar(commandId);
    public bool MoveQuickAccessToolbar(CommandId commandId, int offset) => _session.MoveQuickAccessToolbar(commandId, offset);

    public string ExportJson() =>
        RibbonCustomizationJsonSerializer.Serialize(CreateCustomization());

    public void LoadJson(string json, CommandContext context = default)
    {
        var customization = RibbonCustomizationJsonSerializer.Deserialize(json);
        _session.ReplaceCustomization(customization);
        Publish(context);
    }

    public void Reset(CommandContext context = default)
    {
        _session.Reset();
        _runtime.SetCustomization(null, context);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool SetVisible(
        RibbonCustomizationTarget target,
        bool isVisible,
        CommandContext context = default)
    {
        if (!_session.SetVisible(target, isVisible))
        {
            return false;
        }

        Publish(context);
        return true;
    }

    public bool SetLarge(
        RibbonCustomizationTarget target,
        bool isLarge,
        CommandContext context = default)
    {
        if (!_session.SetLarge(target, isLarge))
        {
            return false;
        }

        Publish(context);
        return true;
    }

    public bool Move(
        RibbonCustomizationTarget target,
        int offset,
        CommandContext context = default)
    {
        if (!_session.Move(target, offset))
        {
            return false;
        }

        Publish(context);
        return true;
    }

    private void Publish(CommandContext context)
    {
        _runtime.SetCustomization(CreateCustomization(), context);
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
