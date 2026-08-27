using System.Runtime.CompilerServices;
using Microsoft.Maui;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Rendering.Spreadsheet;
using MauiAutomationProperties = Microsoft.Maui.Controls.AutomationProperties;
using MauiPaintGLSurfaceEventArgs = SkiaSharp.Views.Maui.SKPaintGLSurfaceEventArgs;
using MauiSemanticProperties = Microsoft.Maui.Controls.SemanticProperties;
#if WINDOWS
using WinAccessibilityView = Microsoft.UI.Xaml.Automation.Peers.AccessibilityView;
using WinAutomationControlType = Microsoft.UI.Xaml.Automation.Peers.AutomationControlType;
using WinAutomationProperties = Microsoft.UI.Xaml.Automation.AutomationProperties;
using WinButton = Microsoft.UI.Xaml.Controls.Button;
using WinHorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using WinSwapChainPanel = SkiaSharp.Views.Windows.SKSwapChainPanel;
using WinThickness = Microsoft.UI.Xaml.Thickness;
using WinVerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment;
using WinVisibility = Microsoft.UI.Xaml.Visibility;
#endif

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Projects host-neutral analytics accessibility nodes into MAUI semantics and
/// platform-native virtual accessibility children while retaining the GPU surface.
/// </summary>
internal static class NeraSpreadsheetAnalyticsAccessibilityBridge
{
    private static readonly ConditionalWeakTable<NeraSpreadsheetView, BridgeState> States = new();

    internal static void Attach(NeraSpreadsheetView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        var state = States.GetValue(view, static key => new BridgeState(key));
        state.Attach();
    }

    internal static void Detach(NeraSpreadsheetView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!States.TryGetValue(view, out var state))
        {
            return;
        }

        state.Detach();
        States.Remove(view);
    }

    private sealed class BridgeState
    {
        private readonly NeraSpreadsheetView _view;
        private string? _lastDescription;
        private bool _attached;
#if WINDOWS
        private readonly Dictionary<SpreadsheetAnalyticsItemKey, WinButton> _windowsProxies = [];
        private WinSwapChainPanel? _windowsPanel;
#endif

        internal BridgeState(NeraSpreadsheetView view)
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
            UpdateMauiSemantics([]);
        }

        internal void Detach()
        {
            if (!_attached)
            {
                return;
            }

            _view.PaintSurface -= OnPaintSurface;
#if WINDOWS
            DetachWindowsPanel();
#endif
#if ANDROID
            NeraSpreadsheetAndroidAnalyticsAccessibilityBridge.Detach(_view);
#endif
            _attached = false;
        }

        private void OnPaintSurface(object? sender, MauiPaintGLSurfaceEventArgs e)
        {
            if (!ReferenceEquals(sender, _view))
            {
                return;
            }

            var nodes = _view.AnalyticsAccessibilityNodes;
            UpdateMauiSemantics(nodes);
#if WINDOWS
            UpdateWindowsAccessibility(nodes, e);
#endif
#if ANDROID
            NeraSpreadsheetAndroidAnalyticsAccessibilityBridge.Update(_view, nodes, e);
#endif
        }

        private void UpdateMauiSemantics(
            IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> nodes)
        {
            var description = BuildDescription(nodes);
            if (string.Equals(_lastDescription, description, StringComparison.Ordinal))
            {
                return;
            }

            _lastDescription = description;
            MauiSemanticProperties.SetDescription(_view, description);
            MauiSemanticProperties.SetHint(
                _view,
                "Biểu đồ và bảng tổng hợp có thể được chọn và thao tác bằng công cụ trợ năng của nền tảng.");
#if WINDOWS
            MauiAutomationProperties.SetIsInAccessibleTree(_view, true);
#endif
        }

        private static string BuildDescription(
            IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> nodes)
        {
            if (nodes.Count == 0)
            {
                return "Bảng tính.";
            }

            var selected = nodes.FirstOrDefault(static node => node.IsSelected);
            if (selected is not null)
            {
                return $"Bảng tính. Có {nodes.Count} đối tượng phân tích. Đang chọn {selected.Name}.";
            }

            if (nodes.Count == 1)
            {
                return $"Bảng tính. Có 1 đối tượng phân tích: {nodes[0].Name}.";
            }

            return $"Bảng tính. Có {nodes.Count} đối tượng phân tích.";
        }

#if WINDOWS
        private void UpdateWindowsAccessibility(
            IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> nodes,
            MauiPaintGLSurfaceEventArgs frame)
        {
            if (_view.Handler?.PlatformView is not WinSwapChainPanel panel)
            {
                DetachWindowsPanel();
                return;
            }

            if (!ReferenceEquals(panel, _windowsPanel))
            {
                DetachWindowsPanel();
                _windowsPanel = panel;
            }

            var activeItems = nodes.Select(static node => node.Item).ToHashSet();
            foreach (var staleItem in _windowsProxies.Keys
                         .Where(item => !activeItems.Contains(item))
                         .ToArray())
            {
                var staleProxy = _windowsProxies[staleItem];
                panel.Children.Remove(staleProxy);
                _windowsProxies.Remove(staleItem);
            }

            var chrome = SpreadsheetChromeGeometry.Calculate(
                frame.Info.Width / _view.Zoom,
                frame.Info.Height / _view.Zoom,
                _view.RenderTheme);
            var viewportWidth = ResolveViewportDimension(_view.Width, frame.Info.Width);
            var viewportHeight = ResolveViewportDimension(_view.Height, frame.Info.Height);
            var canvasUnitsPerViewportUnitX = frame.Info.Width / viewportWidth;
            var canvasUnitsPerViewportUnitY = frame.Info.Height / viewportHeight;

            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                if (!_windowsProxies.TryGetValue(node.Item, out var proxy))
                {
                    proxy = CreateWindowsProxy(node.Item);
                    _windowsProxies.Add(node.Item, proxy);
                    panel.Children.Add(proxy);
                }

                UpdateWindowsProxyMetadata(proxy, node, index + 1, nodes.Count);
                UpdateWindowsProxyBounds(
                    proxy,
                    node,
                    chrome,
                    canvasUnitsPerViewportUnitX,
                    canvasUnitsPerViewportUnitY);
            }
        }

        private WinButton CreateWindowsProxy(SpreadsheetAnalyticsItemKey item)
        {
            var proxy = new WinButton
            {
                HorizontalAlignment = WinHorizontalAlignment.Left,
                VerticalAlignment = WinVerticalAlignment.Top,
                IsHitTestVisible = false,
                IsTabStop = false,
                MinWidth = 0d,
                MinHeight = 0d,
                Opacity = 0.001d,
            };
            proxy.Click += (_, _) => SelectWindowsAnalyticsItem(item);
            WinAutomationProperties.SetAccessibilityView(
                proxy,
                WinAccessibilityView.Content);
            return proxy;
        }

        private void SelectWindowsAnalyticsItem(SpreadsheetAnalyticsItemKey item)
        {
            var session = _view.Session;
            if (session is null)
            {
                return;
            }

            session.AnalyticsInteraction.Select(item);
            _view.InvalidateSurface();
        }

        private static void UpdateWindowsProxyMetadata(
            WinButton proxy,
            SpreadsheetAnalyticsAccessibleNode node,
            int position,
            int size)
        {
            WinAutomationProperties.SetName(proxy, node.Name);
            WinAutomationProperties.SetAutomationId(proxy, node.AutomationId);
            WinAutomationProperties.SetAutomationControlType(
                proxy,
                node.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable
                    ? WinAutomationControlType.Table
                    : WinAutomationControlType.Group);
            WinAutomationProperties.SetLocalizedControlType(
                proxy,
                node.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable
                    ? "bảng tổng hợp"
                    : "biểu đồ");
            WinAutomationProperties.SetHelpText(proxy, BuildHelpText(node));
            WinAutomationProperties.SetPositionInSet(proxy, position);
            WinAutomationProperties.SetSizeOfSet(proxy, size);
        }

        private static string BuildHelpText(SpreadsheetAnalyticsAccessibleNode node)
        {
            var actions = node.Actions
                .Select(static action => action switch
                {
                    "Select" => "Chọn",
                    "Move" => "Di chuyển",
                    "Resize" => "Thay đổi kích thước",
                    "Delete" => "Xóa",
                    _ => action,
                });
            var actionText = string.Join(", ", actions);
            return node.IsSelected
                ? $"Đang chọn. Thao tác: {actionText}."
                : $"Thao tác: {actionText}.";
        }

        private void UpdateWindowsProxyBounds(
            WinButton proxy,
            SpreadsheetAnalyticsAccessibleNode node,
            SpreadsheetChromeMetrics chrome,
            double canvasUnitsPerViewportUnitX,
            double canvasUnitsPerViewportUnitY)
        {
            var visible = node.ViewportBounds.Intersect(node.ClipBounds);
            if (visible.IsEmpty)
            {
                proxy.Visibility = WinVisibility.Collapsed;
                return;
            }

            var zoom = _view.Zoom;
            var left = ((chrome.RowHeaderWidth + visible.Left) * zoom) /
                canvasUnitsPerViewportUnitX;
            var top = ((chrome.ColumnHeaderHeight + visible.Top) * zoom) /
                canvasUnitsPerViewportUnitY;
            var width = (visible.Width * zoom) / canvasUnitsPerViewportUnitX;
            var height = (visible.Height * zoom) / canvasUnitsPerViewportUnitY;

            if (!double.IsFinite(left) ||
                !double.IsFinite(top) ||
                !double.IsFinite(width) ||
                !double.IsFinite(height) ||
                width <= 0d ||
                height <= 0d)
            {
                proxy.Visibility = WinVisibility.Collapsed;
                return;
            }

            proxy.Margin = new WinThickness(left, top, 0d, 0d);
            proxy.Width = width;
            proxy.Height = height;
            proxy.Visibility = WinVisibility.Visible;
        }

        private void DetachWindowsPanel()
        {
            if (_windowsPanel is null)
            {
                return;
            }

            foreach (var proxy in _windowsProxies.Values)
            {
                _windowsPanel.Children.Remove(proxy);
            }
            _windowsProxies.Clear();
            _windowsPanel = null;
        }

        private static double ResolveViewportDimension(
            double viewportDimension,
            int fallbackCanvasDimension) =>
            double.IsFinite(viewportDimension) && viewportDimension > 0d
                ? viewportDimension
                : fallbackCanvasDimension;
#endif
    }
}
