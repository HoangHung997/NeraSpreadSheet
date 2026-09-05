using Microsoft.Maui.Dispatching;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Maui;

/// <summary>Synchronizes shared Table selection state with a MAUI Ribbon runtime.</summary>
public sealed class NeraMauiTableDesignRibbonBinding : IDisposable
{
    private readonly SpreadsheetTableDesignController _tableDesign;
    private readonly RibbonRuntimeController _ribbon;
    private readonly IDispatcher? _dispatcher;
    private bool _disposed;

    public NeraMauiTableDesignRibbonBinding(
        SpreadsheetSession session,
        RibbonRuntimeController ribbon,
        IDispatcher? dispatcher = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        _tableDesign = session.TableDesign;
        _ribbon = ribbon ?? throw new ArgumentNullException(nameof(ribbon));
        _dispatcher = dispatcher;
        _tableDesign.ContextChanged += OnContextChanged;
        Refresh();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _tableDesign.ContextChanged -= OnContextChanged;
        GC.SuppressFinalize(this);
    }

    private void OnContextChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        if (_disposed)
        {
            return;
        }
        void Apply()
        {
            if (_disposed) return;
            var snapshot = _tableDesign.Snapshot;
            _ribbon.SetSelectionContext(new RibbonSelectionContext(
                snapshot.HasSelection, snapshot.IsInTable));
        }
        if (_dispatcher is null || !_dispatcher.IsDispatchRequired)
        {
            Apply();
        }
        else if (!_dispatcher.Dispatch(Apply))
        {
            throw new InvalidOperationException(
                "The MAUI dispatcher rejected the Table Design refresh.");
        }
    }
}
