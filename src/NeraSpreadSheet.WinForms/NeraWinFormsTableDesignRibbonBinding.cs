using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Ribbon.Core;

namespace NeraSpreadSheet.WinForms;

/// <summary>Synchronizes shared Table selection state with a WinForms Ribbon runtime.</summary>
public sealed class NeraWinFormsTableDesignRibbonBinding : IDisposable
{
    private readonly SpreadsheetTableDesignController _tableDesign;
    private readonly RibbonRuntimeController _ribbon;
    private readonly Control _owner;
    private bool _disposed;

    public NeraWinFormsTableDesignRibbonBinding(
        SpreadsheetSession session,
        RibbonRuntimeController ribbon,
        Control owner)
    {
        ArgumentNullException.ThrowIfNull(session);
        _tableDesign = session.TableDesign;
        _ribbon = ribbon ?? throw new ArgumentNullException(nameof(ribbon));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
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
        if (_disposed || _owner.IsDisposed)
        {
            return;
        }
        void Apply()
        {
            if (_disposed || _owner.IsDisposed) return;
            var snapshot = _tableDesign.Snapshot;
            _ribbon.SetSelectionContext(new RibbonSelectionContext(
                snapshot.HasSelection, snapshot.IsInTable));
        }
        if (_owner.IsHandleCreated && _owner.InvokeRequired)
        {
            _owner.BeginInvoke(Apply);
        }
        else
        {
            Apply();
        }
    }
}
