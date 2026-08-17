using System.Drawing;
using System.Windows.Forms;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Editing;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Layout;
using NeraSpreadSheet.Rendering;
using NeraSpreadSheet.Rendering.Direct2D;
using NeraSpreadSheet.Rendering.Spreadsheet;
using NeraSpreadSheet.Scrolling;
using NeraSpreadSheet.Viewport;

namespace NeraSpreadSheet.WinForms;

internal sealed partial class NeraSpreadsheetSplitSurface : Control
{
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SynchronizeBackend();
        EnsureSelectedGpuRenderer();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DisposeGpuRenderers();
        base.OnHandleDestroyed(e);
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
        SynchronizeBackend();
        if (_activeBackend == WinFormsRenderingBackend.GdiPlus)
        {
            base.OnPaintBackground(pevent);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        SynchronizeSession();
        SynchronizeBackend();

        var frame = EnsureFrame();
        if (frame is null)
        {
            RenderEmptyBackground(e.Graphics);
            base.OnPaint(e);
            return;
        }

        var paneLayouts = new List<SpreadsheetSplitPaneChromeLayout>(frame.Panes.Count);
        foreach (var pane in frame.Panes)
        {
            paneLayouts.Add(new SpreadsheetSplitPaneChromeLayout(
                pane.Pane.PaneId,
                pane.Pane.Bounds,
                pane.ViewportFrame.Layout));
        }

        var displayList = SpreadsheetSplitChromeDisplayListComposer.Compose(
            frame.DisplayList,
            frame.Layout,
            paneLayouts,
            _session!.Selection.Capture(),
            _owner.RenderTheme);
        switch (_activeBackend)
        {
            case WinFormsRenderingBackend.Direct2D:
                EnsureDirect2DRenderer().Render(displayList);
                break;
            case WinFormsRenderingBackend.Direct2DSwapChain:
                EnsureSwapChainRenderer().Render(displayList);
                break;
            default:
                _displayListRenderer.Render(e.Graphics, displayList);
                break;
        }

        base.OnPaint(e);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _lastFrame = null;
        if (ClientSize.Width > 0 && ClientSize.Height > 0)
        {
            _direct2DRenderer?.Resize(ClientSize.Width, ClientSize.Height);
            _swapChainRenderer?.Resize(ClientSize.Width, ClientSize.Height);
        }
        UpdateEditorBounds();
        Invalidate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cellEditor?.Cancel();
            HideEditor();
            DetachSessionEvents();
            _frameTimer.Stop();
            _frameTimer.Tick -= OnFrameTick;
            _frameTimer.Dispose();
            _editor.KeyDown -= OnEditorKeyDown;
            DisposeGpuRenderers();
            _displayListRenderer.Dispose();
        }
        base.Dispose(disposing);
    }

    private void SynchronizeBackend()
    {
        var requested = _owner.RenderingBackend;
        if (_activeBackend != requested)
        {
            _activeBackend = requested;
            DisposeGpuRenderers();
            SetGdiPaintingStyles(requested == WinFormsRenderingBackend.GdiPlus);
        }

        if (_swapChainRenderer is not null)
        {
            _swapChainRenderer.VSync = _owner.SwapChainVSync;
        }
    }

    private void EnsureSelectedGpuRenderer()
    {
        if (!IsHandleCreated)
        {
            return;
        }

        switch (_activeBackend)
        {
            case WinFormsRenderingBackend.Direct2D:
                EnsureDirect2DRenderer();
                break;
            case WinFormsRenderingBackend.Direct2DSwapChain:
                EnsureSwapChainRenderer();
                break;
        }
    }

    private Direct2DHwndDisplayListRenderer EnsureDirect2DRenderer()
    {
        EnsureGpuPlatformAndHandle();
        if (_activeBackend != WinFormsRenderingBackend.Direct2D)
        {
            throw new InvalidOperationException(
                "The HWND Direct2D backend is not selected for this split surface.");
        }

        _direct2DRenderer ??= new Direct2DHwndDisplayListRenderer(
            Handle,
            Math.Max(1, ClientSize.Width),
            Math.Max(1, ClientSize.Height));
        return _direct2DRenderer;
    }

    private Direct2DSwapChainDisplayListRenderer EnsureSwapChainRenderer()
    {
        EnsureGpuPlatformAndHandle();
        if (_activeBackend != WinFormsRenderingBackend.Direct2DSwapChain)
        {
            throw new InvalidOperationException(
                "The D3D11/DXGI backend is not selected for this split surface.");
        }

        if (_swapChainRenderer is null)
        {
            _swapChainRenderer = new Direct2DSwapChainDisplayListRenderer(
                Handle,
                Math.Max(1, ClientSize.Width),
                Math.Max(1, ClientSize.Height))
            {
                VSync = _owner.SwapChainVSync,
            };
        }
        return _swapChainRenderer;
    }

    private void EnsureGpuPlatformAndHandle()
    {
        if (!Direct2DBackendDescriptor.IsPlatformSupported)
        {
            throw new PlatformNotSupportedException(
                "The Direct2D backends require Windows 10 version 2004 or later.");
        }
        if (!IsHandleCreated)
        {
            throw new InvalidOperationException(
                "The split surface handle must exist before GPU initialization.");
        }
    }

    private void DisposeGpuRenderers()
    {
        _direct2DRenderer?.Dispose();
        _direct2DRenderer = null;
        _swapChainRenderer?.Dispose();
        _swapChainRenderer = null;
    }

    private void RenderEmptyBackground(Graphics graphics)
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        if (_activeBackend == WinFormsRenderingBackend.GdiPlus)
        {
            graphics.Clear(_owner.BackColor);
            return;
        }

        var builder = new DisplayListBuilder();
        builder.FillRectangle(
            new RectD(0d, 0d, ClientSize.Width, ClientSize.Height),
            new ColorRgba(
                _owner.BackColor.R,
                _owner.BackColor.G,
                _owner.BackColor.B,
                _owner.BackColor.A));
        var displayList = builder.Build();
        if (_activeBackend == WinFormsRenderingBackend.Direct2D)
        {
            EnsureDirect2DRenderer().Render(displayList);
        }
        else
        {
            EnsureSwapChainRenderer().Render(displayList);
        }
    }

    private void SetGdiPaintingStyles(bool enabled)
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.ResizeRedraw,
            true);
        SetStyle(ControlStyles.OptimizedDoubleBuffer, enabled);
        if (IsHandleCreated)
        {
            UpdateStyles();
        }
    }

}
