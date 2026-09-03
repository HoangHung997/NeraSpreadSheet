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
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _session = new RibbonCustomizationSession(
            runtime.Definition,
            runtime.Customization,
            commandCaption);
    }

    public event EventHandler? Changed;

    public IReadOnlyList<RibbonCustomizationEntry> Entries => _session.Entries;

    public RibbonCustomization CreateCustomization() =>
        _session.CreateCustomization();

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
