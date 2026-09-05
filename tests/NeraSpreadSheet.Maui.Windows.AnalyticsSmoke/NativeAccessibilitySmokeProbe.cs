using System.Runtime.CompilerServices;
using Microsoft.Maui;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Maui;
using NeraSpreadSheet.Rendering.Spreadsheet;
using SkiaSharp.Views.Maui.Handlers;
using MauiAutomationProperties = Microsoft.Maui.Controls.AutomationProperties;
using MauiSemanticProperties = Microsoft.Maui.Controls.SemanticProperties;
using WinAccessibilityView = Microsoft.UI.Xaml.Automation.Peers.AccessibilityView;
using WinAutomationControlType = Microsoft.UI.Xaml.Automation.Peers.AutomationControlType;
using WinAutomationProperties = Microsoft.UI.Xaml.Automation.AutomationProperties;
using WinButton = Microsoft.UI.Xaml.Controls.Button;
using WinButtonAutomationPeer = Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer;
using WinPatternInterface = Microsoft.UI.Xaml.Automation.Peers.PatternInterface;
using WinSwapChainPanel = SkiaSharp.Views.Windows.SKSwapChainPanel;
using WinVisibility = Microsoft.UI.Xaml.Visibility;

namespace NeraSpreadSheet.Maui.Windows.AnalyticsSmoke;

internal static class NativeAccessibilitySmokeProbe
{
    private const string MapperKey = "NeraSpreadSheet.AnalyticsSmoke.NativeAccessibility";
    private const double BoundsTolerance = 2d;
    private static readonly ConditionalWeakTable<NeraSpreadsheetView, ProbeState> States = new();
    private static int s_registered;

    internal static void Register()
    {
        if (Interlocked.Exchange(ref s_registered, 1) != 0)
        {
            return;
        }

        SKGLViewHandler.SKGLViewMapper.AppendToMapping(
            MapperKey,
            static (_, virtualView) =>
            {
                if (virtualView is NeraSpreadsheetView view)
                {
                    var state = States.GetValue(view, static key => new ProbeState(key));
                    state.Attach();
                }
            });
    }

    private sealed class ProbeState
    {
        private readonly NeraSpreadsheetView _view;
        private bool _attached;
        private bool _validated;

        internal ProbeState(NeraSpreadsheetView view)
        {
            _view = view;
        }

        internal void Attach()
        {
            if (_attached)
            {
                return;
            }

            _attached = true;
            _view.PaintSurface += OnPaintSurface;
        }

        private void OnPaintSurface(object? sender, SkiaSharp.Views.Maui.SKPaintGLSurfaceEventArgs e)
        {
            if (_validated || !ReferenceEquals(sender, _view))
            {
                return;
            }

            var nodes = _view.AnalyticsAccessibilityNodes;
            if (nodes.Count == 0)
            {
                return;
            }

            var node = nodes.Single();
            Validate(
                _view,
                node,
                e.Info.Width,
                e.Info.Height,
                expectedSelected: node.IsSelected);

            // This probe must remain observational. The loaded analytics smoke owns
            // the interaction sequence; invoking the UIA child from PaintSurface
            // would select the chart before the smoke begins its touch phase.
            _validated = true;
        }
    }

    private static void Validate(
        NeraSpreadsheetView view,
        SpreadsheetAnalyticsAccessibleNode node,
        int surfaceWidth,
        int surfaceHeight,
        bool expectedSelected)
    {
        Require(
            MauiAutomationProperties.GetIsInAccessibleTree(view) == true,
            "The loaded MAUI spreadsheet was not exposed through the accessibility tree.");
        var semanticDescription = MauiSemanticProperties.GetDescription(view);
        Require(
            semanticDescription.Contains(node.Name, StringComparison.Ordinal),
            "The loaded MAUI spreadsheet semantic description omitted the analytics item name.");
        Require(
            !expectedSelected || semanticDescription.Contains("Đang chọn", StringComparison.Ordinal),
            "The loaded MAUI spreadsheet semantic description did not reflect analytics selection.");

        var panel = view.Handler?.PlatformView as WinSwapChainPanel
            ?? throw new InvalidOperationException(
                "The loaded analytics view did not resolve to the native Windows SKSwapChainPanel.");
        var proxy = panel.Children
            .OfType<WinButton>()
            .SingleOrDefault(child =>
                string.Equals(
                    WinAutomationProperties.GetAutomationId(child),
                    node.AutomationId,
                    StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"The native accessibility child {node.AutomationId} was not found.");

        Require(!proxy.IsHitTestVisible,
            "The native analytics accessibility proxy must not intercept spreadsheet pointer input.");
        Require(!proxy.IsTabStop,
            "The native analytics accessibility proxy must not create a duplicate tab stop.");
        Require(proxy.Visibility == WinVisibility.Visible,
            "The visible analytics item was collapsed in the native accessibility layer.");
        Require(
            WinAutomationProperties.GetAccessibilityView(proxy) == WinAccessibilityView.Content,
            "The native analytics accessibility proxy was not exposed in the content view.");
        Require(
            WinAutomationProperties.GetName(proxy) == node.Name,
            "The native analytics accessibility name did not match the host-neutral node.");
        Require(
            WinAutomationProperties.GetAutomationControlType(proxy) ==
                (node.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable
                    ? WinAutomationControlType.Table
                    : WinAutomationControlType.Group),
            "The native analytics accessibility role did not match the host-neutral node.");
        Require(
            WinAutomationProperties.GetPositionInSet(proxy) == 1 &&
            WinAutomationProperties.GetSizeOfSet(proxy) == 1,
            "The native analytics accessibility child did not expose deterministic set metadata.");
        Require(
            WinAutomationProperties.GetHelpText(proxy).Contains(
                expectedSelected ? "Đang chọn" : "Thao tác",
                StringComparison.Ordinal),
            "The native analytics accessibility help text did not reflect the expected state.");

        ValidateBounds(view, node, proxy, surfaceWidth, surfaceHeight);

        var peer = new WinButtonAutomationPeer(proxy);
        Require(peer.GetName() == node.Name,
            "The native UI Automation peer returned the wrong analytics name.");
        Require(peer.GetAutomationId() == node.AutomationId,
            "The native UI Automation peer returned the wrong automation ID.");
        Require(
            peer.GetAutomationControlType() ==
                (node.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable
                    ? WinAutomationControlType.Table
                    : WinAutomationControlType.Group),
            "The native UI Automation peer returned the wrong analytics control type.");
        Require(
            peer.GetPattern(WinPatternInterface.Invoke) is not null,
            "The native analytics accessibility child did not expose the Invoke pattern.");
    }

    private static void ValidateBounds(
        NeraSpreadsheetView view,
        SpreadsheetAnalyticsAccessibleNode node,
        WinButton proxy,
        int surfaceWidth,
        int surfaceHeight)
    {
        Require(surfaceWidth > 0 && surfaceHeight > 0,
            "The native accessibility smoke requires completed surface dimensions.");
        var visible = node.ViewportBounds.Intersect(node.ClipBounds);
        Require(!visible.IsEmpty,
            "The native accessibility smoke item did not have a visible viewport fragment.");

        var zoom = view.Zoom;
        var chrome = SpreadsheetChromeGeometry.Calculate(
            surfaceWidth / zoom,
            surfaceHeight / zoom,
            view.RenderTheme);
        var viewportWidth = ResolveViewportDimension(view.Width, surfaceWidth);
        var viewportHeight = ResolveViewportDimension(view.Height, surfaceHeight);
        var scaleX = surfaceWidth / viewportWidth;
        var scaleY = surfaceHeight / viewportHeight;
        var expected = new RectD(
            ((chrome.RowHeaderWidth + visible.Left) * zoom) / scaleX,
            ((chrome.ColumnHeaderHeight + visible.Top) * zoom) / scaleY,
            (visible.Width * zoom) / scaleX,
            (visible.Height * zoom) / scaleY);

        Require(
            AreClose(proxy.Margin.Left, expected.X) &&
            AreClose(proxy.Margin.Top, expected.Y) &&
            AreClose(proxy.Width, expected.Width) &&
            AreClose(proxy.Height, expected.Height),
            $"The native analytics accessibility bounds were not aligned with the rendered item. " +
            $"actual=({proxy.Margin.Left:R},{proxy.Margin.Top:R},{proxy.Width:R},{proxy.Height:R}) " +
            $"expected=({expected.X:R},{expected.Y:R},{expected.Width:R},{expected.Height:R}).");
    }

    private static double ResolveViewportDimension(double value, int fallback) =>
        double.IsFinite(value) && value > 0d ? value : fallback;

    private static bool AreClose(double actual, double expected) =>
        Math.Abs(actual - expected) <= BoundsTolerance;

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
