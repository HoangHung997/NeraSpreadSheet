#if IOS || MACCATALYST
using System.Runtime.CompilerServices;
using CoreGraphics;
using Foundation;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Rendering.Spreadsheet;
using ObjCRuntime;
using SkiaSharp.Views.Maui;
using UIKit;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// iOS/Mac Catalyst VoiceOver projection for floating analytics. The spreadsheet
/// remains one GPU-backed UIView; charts and pivots are virtual accessibility
/// elements rather than real UIKit subviews.
/// </summary>
internal static class NeraSpreadsheetAppleAnalyticsAccessibilityBridge
{
    private static readonly ConditionalWeakTable<NeraSpreadsheetView, AppleBridgeState> States = new();

    internal static void Update(
        NeraSpreadsheetView view,
        IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> nodes,
        SKPaintGLSurfaceEventArgs frame)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(frame);

        var state = States.GetValue(view, static key => new AppleBridgeState(key));
        state.Update(nodes, frame);
    }

    internal static void Detach(NeraSpreadsheetView view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (!States.TryGetValue(view, out var state))
        {
            return;
        }

        state.Dispose();
        States.Remove(view);
    }

#if !MACCATALYST
    private static IUIAccessibilityContainer WrapAccessibilityContainer(UIView platformView) =>
        Runtime.GetINativeObject<IUIAccessibilityContainer>(
            platformView.Handle,
            owns: false)
        ?? throw new InvalidOperationException(
            "The native UIView could not be wrapped as UIAccessibilityContainer.");
#endif

    private sealed class AppleBridgeState : IDisposable
    {
        private readonly NeraSpreadsheetView _view;
        private readonly Dictionary<SpreadsheetAnalyticsItemKey, NeraAccessibilityElement> _elements = [];
        private UIView? _platformView;
#if MACCATALYST
        private NeraMacCatalystAccessibilityContainerView? _macCatalystContainer;
#else
        private IUIAccessibilityContainer? _accessibilityContainer;
        private NSObject? _previousAccessibilityElements;
#endif
        private bool _previousIsAccessibilityElement;
        private string? _lastSemanticSignature;
        private bool _disposed;

        internal AppleBridgeState(NeraSpreadsheetView view)
        {
            _view = view;
        }

        internal void Update(
            IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> nodes,
            SKPaintGLSurfaceEventArgs frame)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_view.Handler?.PlatformView is not UIView platformView)
            {
                DetachPlatformView();
                return;
            }

            // The first GPU frame normally has no floating analytics yet. Avoid
            // touching the optional native accessibility container selectors until
            // there is actually something to project. If a previously attached
            // projection becomes empty we still run the update so stale elements
            // are removed.
            if (nodes.Count == 0 && _platformView is null)
            {
                return;
            }

            EnsurePlatformView(platformView);
            UpdateElements(nodes, frame);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            DetachPlatformView();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        private void EnsurePlatformView(UIView platformView)
        {
            if (ReferenceEquals(platformView, _platformView))
            {
                return;
            }

            DetachPlatformView();
#if MACCATALYST
            var macCatalystContainer = platformView as NeraMacCatalystAccessibilityContainerView
                ?? throw new InvalidOperationException(
                    "The Mac Catalyst native spreadsheet host is not the dedicated accessibility container view.");
            _macCatalystContainer = macCatalystContainer;
#else
            var accessibilityContainer = WrapAccessibilityContainer(platformView);
            _accessibilityContainer = accessibilityContainer;
            _previousAccessibilityElements = accessibilityContainer.GetAccessibilityElements();
#endif
            _platformView = platformView;
            _previousIsAccessibilityElement = platformView.IsAccessibilityElement;
            platformView.IsAccessibilityElement = false;
        }

        private void UpdateElements(
            IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> nodes,
            SKPaintGLSurfaceEventArgs frame)
        {
            var platformView = _platformView;
            if (platformView is null)
            {
                return;
            }
#if MACCATALYST
            var macCatalystContainer = _macCatalystContainer;
            if (macCatalystContainer is null)
            {
                return;
            }
#else
            var accessibilityContainer = _accessibilityContainer;
            if (accessibilityContainer is null)
            {
                return;
            }
#endif

            var activeItems = nodes.Select(static node => node.Item).ToHashSet();
            foreach (var staleItem in _elements.Keys
                         .Where(item => !activeItems.Contains(item))
                         .ToArray())
            {
                _elements[staleItem].Dispose();
                _elements.Remove(staleItem);
            }

            var zoom = _view.Zoom;
            var chrome = SpreadsheetChromeGeometry.Calculate(
                frame.Info.Width / zoom,
                frame.Info.Height / zoom,
                _view.RenderTheme);
            var canvasUnitsPerPointX = ResolveCanvasUnitsPerPoint(
                platformView.Bounds.Width,
                frame.Info.Width);
            var canvasUnitsPerPointY = ResolveCanvasUnitsPerPoint(
                platformView.Bounds.Height,
                frame.Info.Height);

            var orderedElements = new List<NSObject>(nodes.Count);
            for (var index = 0; index < nodes.Count; index++)
            {
                var node = nodes[index];
                var visible = node.ViewportBounds.Intersect(node.ClipBounds);
                if (visible.IsEmpty)
                {
                    continue;
                }

                var frameInView = ToViewFrame(
                    visible,
                    chrome,
                    zoom,
                    canvasUnitsPerPointX,
                    canvasUnitsPerPointY);
                if (frameInView.IsEmpty ||
                    !double.IsFinite(frameInView.X) ||
                    !double.IsFinite(frameInView.Y) ||
                    !double.IsFinite(frameInView.Width) ||
                    !double.IsFinite(frameInView.Height))
                {
                    continue;
                }

                if (!_elements.TryGetValue(node.Item, out var element))
                {
                    element = new NeraAccessibilityElement(
                        platformView,
                        node.Item,
                        SelectAnalyticsItem);
                    _elements.Add(node.Item, element);
                }

                UpdateElementMetadata(element, node, index + 1, nodes.Count);
                element.AccessibilityFrame = UIAccessibility.ConvertFrameToScreenCoordinates(
                    frameInView,
                    platformView);
                orderedElements.Add(element);
            }

#if MACCATALYST
            // The dedicated native host owns the NSArray and the Objective-C
            // accessibilityElements selectors. This keeps both the protocol
            // identity and the array lifetime anchored to the actual UIView.
            macCatalystContainer.ReplaceAccessibilityElements(orderedElements);
#else
            using var accessibilityElements = NSArray.FromNSObjects(orderedElements.ToArray());
            accessibilityContainer.SetAccessibilityElements(accessibilityElements);
#endif

            var signature = BuildSemanticSignature(nodes);
            if (!string.Equals(signature, _lastSemanticSignature, StringComparison.Ordinal))
            {
                _lastSemanticSignature = signature;
                if (UIAccessibility.IsVoiceOverRunning)
                {
                    UIAccessibility.PostNotification(
                        UIAccessibilityPostNotification.LayoutChanged,
                        null);
                }
            }
        }

        private bool SelectAnalyticsItem(SpreadsheetAnalyticsItemKey item)
        {
            var session = _view.Session;
            if (session is null)
            {
                return false;
            }

            session.AnalyticsInteraction.Select(item);
            _view.InvalidateSurface();
            return true;
        }

        private static void UpdateElementMetadata(
            NeraAccessibilityElement element,
            SpreadsheetAnalyticsAccessibleNode node,
            int position,
            int size)
        {
            var role = node.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable
                ? "Bảng tổng hợp"
                : "Biểu đồ";
            element.AccessibilityLabel = node.Name;
            element.AccessibilityValue = node.IsSelected
                ? $"{role}, mục {position} trên {size}, đang chọn"
                : $"{role}, mục {position} trên {size}";
            element.AccessibilityHint = BuildHint(node);
            element.AccessibilityIdentifier = node.AutomationId;

            var traits = node.Role == SpreadsheetAnalyticsAccessibleRole.Chart
                ? UIAccessibilityTrait.Image | UIAccessibilityTrait.Button
                : UIAccessibilityTrait.Button;
            if (node.IsSelected)
            {
                traits |= UIAccessibilityTrait.Selected;
            }
            element.AccessibilityTraits = (ulong)traits;
        }

        private static string BuildHint(SpreadsheetAnalyticsAccessibleNode node)
        {
            var actions = node.Actions.Select(static action => action switch
            {
                "Select" => "chọn",
                "Move" => "di chuyển",
                "Resize" => "thay đổi kích thước",
                "Delete" => "xóa",
                _ => action,
            });
            return $"Chạm hai lần để chọn. Thao tác hỗ trợ: {string.Join(", ", actions)}.";
        }

        private static string BuildSemanticSignature(
            IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> nodes) =>
            string.Join(
                "|",
                nodes.Select(static node =>
                    $"{node.Item}:{node.Role}:{node.Name}:{node.AutomationId}:{node.IsSelected}:{string.Join(',', node.Actions)}"));

        private static CGRect ToViewFrame(
            RectD visible,
            SpreadsheetChromeMetrics chrome,
            double zoom,
            double canvasUnitsPerPointX,
            double canvasUnitsPerPointY)
        {
            var left = ((chrome.RowHeaderWidth + visible.Left) * zoom) /
                canvasUnitsPerPointX;
            var top = ((chrome.ColumnHeaderHeight + visible.Top) * zoom) /
                canvasUnitsPerPointY;
            var width = (visible.Width * zoom) / canvasUnitsPerPointX;
            var height = (visible.Height * zoom) / canvasUnitsPerPointY;
            return width > 0d && height > 0d
                ? new CGRect(left, top, width, height)
                : CGRect.Empty;
        }

        private static double ResolveCanvasUnitsPerPoint(
            nfloat nativeDimension,
            int canvasDimension) =>
            double.IsFinite((double)nativeDimension) && nativeDimension > 0
                ? canvasDimension / (double)nativeDimension
                : 1d;

        private void DetachPlatformView()
        {
#if MACCATALYST
            _macCatalystContainer?.SetAccessibilityElements(null);
#else
            if (_accessibilityContainer is not null)
            {
                _accessibilityContainer.SetAccessibilityElements(_previousAccessibilityElements);
            }
#endif
            if (_platformView is not null)
            {
                _platformView.IsAccessibilityElement = _previousIsAccessibilityElement;
            }

            foreach (var element in _elements.Values)
            {
                element.Dispose();
            }
            _elements.Clear();
            _lastSemanticSignature = null;
#if MACCATALYST
            _macCatalystContainer = null;
#else
            _previousAccessibilityElements = null;
            _accessibilityContainer = null;
#endif
            _platformView = null;
        }
    }

    private sealed class NeraAccessibilityElement : UIAccessibilityElement
    {
        private readonly SpreadsheetAnalyticsItemKey _item;
        private readonly Func<SpreadsheetAnalyticsItemKey, bool> _activate;

        internal NeraAccessibilityElement(
            NSObject container,
            SpreadsheetAnalyticsItemKey item,
            Func<SpreadsheetAnalyticsItemKey, bool> activate)
            : base(container)
        {
            _item = item;
            _activate = activate;
        }

        [Export("accessibilityActivate")]
        public bool Activate() => _activate(_item);
    }
}
#endif
