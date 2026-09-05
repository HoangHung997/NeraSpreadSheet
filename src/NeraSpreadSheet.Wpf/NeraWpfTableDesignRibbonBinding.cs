using System.Windows.Threading;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.Wpf;

/// <summary>Synchronizes shared Table selection state with a WPF Ribbon runtime.</summary>
public sealed class NeraWpfTableDesignRibbonBinding : IDisposable
{
    private readonly SpreadsheetTableDesignController _tableDesign;
    private readonly RibbonRuntimeController _ribbon;
    private readonly Dispatcher _dispatcher;
    private bool _disposed;

    public NeraWpfTableDesignRibbonBinding(
        SpreadsheetSession session,
        RibbonRuntimeController ribbon,
        Dispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(session);
        _tableDesign = session.TableDesign;
        _ribbon = ribbon ?? throw new ArgumentNullException(nameof(ribbon));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
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
        if (_dispatcher.CheckAccess())
        {
            Apply();
        }
        else
        {
            _dispatcher.BeginInvoke(Apply);
        }
    }
}
