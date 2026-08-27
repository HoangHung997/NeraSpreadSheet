#if ANDROID
using System.Runtime.CompilerServices;
using Android.OS;
using Android.Views;
using Android.Views.Accessibility;
using AndroidX.Core.View;
using AndroidX.Core.View.Accessibility;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;
using NeraSpreadSheet.Rendering.Spreadsheet;
using SkiaSharp.Views.Maui;
using AndroidRect = Android.Graphics.Rect;

namespace NeraSpreadSheet.Maui;

/// <summary>
/// Android/TalkBack projection for floating analytics. The spreadsheet stays a
/// single GPU surface; charts and pivots are exposed as virtual accessibility
/// descendants instead of real Android Views.
/// </summary>
internal static class NeraSpreadsheetAndroidAnalyticsAccessibilityBridge
{
    private static readonly ConditionalWeakTable<NeraSpreadsheetView, AndroidBridgeState> States = new();

    internal static void Update(
        NeraSpreadsheetView view,
        IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> nodes,
        SKPaintGLSurfaceEventArgs frame)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(frame);

        var state = States.GetValue(view, static key => new AndroidBridgeState(key));
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

    private sealed class AndroidBridgeState : IDisposable
    {
        private readonly NeraSpreadsheetView _view;
        private Android.Views.View? _platformView;
        private NeraAccessibilityDelegate? _delegate;
        private NeraAccessibilityNodeProvider? _provider;
        private bool _disposed;

        internal AndroidBridgeState(NeraSpreadsheetView view)
        {
            _view = view;
        }

        internal void Update(
            IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> nodes,
            SKPaintGLSurfaceEventArgs frame)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_view.Handler?.PlatformView is not Android.Views.View platformView)
            {
                DetachPlatformView();
                return;
            }

            EnsurePlatformView(platformView);
            _provider!.Update(nodes, frame, _view.Zoom, _view.RenderTheme);
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

        private void EnsurePlatformView(Android.Views.View platformView)
        {
            if (ReferenceEquals(platformView, _platformView))
            {
                return;
            }

            DetachPlatformView();
            _platformView = platformView;

            var previousDelegate = ViewCompat.GetAccessibilityDelegate(platformView);
            _provider = new NeraAccessibilityNodeProvider(
                platformView,
                _view,
                previousDelegate);
            _delegate = new NeraAccessibilityDelegate(previousDelegate, _provider);

            ViewCompat.SetAccessibilityDelegate(platformView, _delegate);
            platformView.ImportantForAccessibility = ImportantForAccessibility.Yes;
            platformView.SetOnHoverListener(_provider);
        }

        private void DetachPlatformView()
        {
            if (_platformView is not null)
            {
                _platformView.SetOnHoverListener(null);
                ViewCompat.SetAccessibilityDelegate(_platformView, null);
            }

            _delegate?.Dispose();
            _provider?.Dispose();
            _provider = null;
            _delegate = null;
            _platformView = null;
        }
    }

    private sealed class NeraAccessibilityDelegate : AccessibilityDelegateCompat
    {
        private readonly AccessibilityDelegateCompat? _previous;
        private readonly NeraAccessibilityNodeProvider _provider;

        internal NeraAccessibilityDelegate(
            AccessibilityDelegateCompat? previous,
            NeraAccessibilityNodeProvider provider)
        {
            _previous = previous;
            _provider = provider;
        }

        public override AccessibilityNodeProviderCompat GetAccessibilityNodeProvider(
            Android.Views.View? host) => _provider;

        public override void OnInitializeAccessibilityNodeInfo(
            Android.Views.View? host,
            AccessibilityNodeInfoCompat? info)
        {
            if (host is null || info is null)
            {
                return;
            }

            if (_previous is not null)
            {
                _previous.OnInitializeAccessibilityNodeInfo(host, info);
            }
            else
            {
                base.OnInitializeAccessibilityNodeInfo(host, info);
            }
        }

        public override bool PerformAccessibilityAction(
            Android.Views.View? host,
            int action,
            Bundle? arguments)
        {
            if (host is null)
            {
                return false;
            }

            if (_previous is not null &&
                _previous.PerformAccessibilityAction(host, action, arguments))
            {
                return true;
            }

            return base.PerformAccessibilityAction(host, action, arguments);
        }
    }

    private sealed class NeraAccessibilityNodeProvider :
        AccessibilityNodeProviderCompat,
        Android.Views.View.IOnHoverListener
    {
        private const int RootViewId = Android.Views.View.NoId;
        private const int ActionClickId = 16;
        private const int ActionAccessibilityFocusId = 64;
        private const int ActionClearAccessibilityFocusId = 128;

        private readonly Android.Views.View _host;
        private readonly NeraSpreadsheetView _view;
        private readonly AccessibilityDelegateCompat? _rootDelegate;
        private readonly Dictionary<SpreadsheetAnalyticsItemKey, int> _virtualIds = [];
        private readonly Dictionary<int, VirtualNodeSnapshot> _nodesById = [];
        private int _nextVirtualId = 1;
        private int _accessibilityFocusedId = RootViewId;
        private int _hoveredId = RootViewId;

        internal NeraAccessibilityNodeProvider(
            Android.Views.View host,
            NeraSpreadsheetView view,
            AccessibilityDelegateCompat? rootDelegate)
        {
            _host = host;
            _view = view;
            _rootDelegate = rootDelegate;
        }

        internal void Update(
            IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> nodes,
            SKPaintGLSurfaceEventArgs frame,
            double zoom,
            SpreadsheetRenderTheme theme)
        {
            var nextNodes = new Dictionary<int, VirtualNodeSnapshot>();
            var chrome = SpreadsheetChromeGeometry.Calculate(
                frame.Info.Width / zoom,
                frame.Info.Height / zoom,
                theme);
            var nativeScaleX = ResolveNativeScale(_host.Width, frame.Info.Width);
            var nativeScaleY = ResolveNativeScale(_host.Height, frame.Info.Height);

            foreach (var node in nodes)
            {
                var visible = node.ViewportBounds.Intersect(node.ClipBounds);
                if (visible.IsEmpty)
                {
                    continue;
                }

                var bounds = ToNativeBounds(
                    visible,
                    chrome,
                    zoom,
                    nativeScaleX,
                    nativeScaleY,
                    _host.Width,
                    _host.Height);
                if (bounds.IsEmpty)
                {
                    continue;
                }

                var virtualId = GetOrCreateVirtualId(node.Item);
                nextNodes[virtualId] = new VirtualNodeSnapshot(
                    virtualId,
                    node,
                    bounds,
                    BuildContentDescription(node));
            }

            var contentChanged = !SnapshotSetsEquivalent(_nodesById, nextNodes);
            _nodesById.Clear();
            foreach (var pair in nextNodes)
            {
                _nodesById.Add(pair.Key, pair.Value);
            }

            if (_accessibilityFocusedId != RootViewId &&
                !_nodesById.ContainsKey(_accessibilityFocusedId))
            {
                _accessibilityFocusedId = RootViewId;
            }
            if (_hoveredId != RootViewId && !_nodesById.ContainsKey(_hoveredId))
            {
                _hoveredId = RootViewId;
            }

            if (contentChanged)
            {
                SendEvent(RootViewId, EventTypes.WindowContentChanged, null);
            }
        }

        public override AccessibilityNodeInfoCompat? CreateAccessibilityNodeInfo(int virtualViewId)
        {
            if (virtualViewId == RootViewId)
            {
                return CreateHostNode();
            }

            return _nodesById.TryGetValue(virtualViewId, out var snapshot)
                ? CreateVirtualNode(snapshot)
                : null;
        }

        public override AccessibilityNodeInfoCompat? FindFocus(int focus)
        {
            return _accessibilityFocusedId != RootViewId &&
                   _nodesById.TryGetValue(_accessibilityFocusedId, out var snapshot)
                ? CreateVirtualNode(snapshot)
                : null;
        }

        public override bool PerformAction(
            int virtualViewId,
            int action,
            Bundle? arguments)
        {
            if (virtualViewId == RootViewId)
            {
                return _rootDelegate?.PerformAccessibilityAction(
                           _host,
                           action,
                           arguments) ??
                       base.PerformAction(virtualViewId, action, arguments);
            }

            if (!_nodesById.TryGetValue(virtualViewId, out var snapshot))
            {
                return false;
            }

            return action switch
            {
                ActionClickId => Select(snapshot),
                ActionAccessibilityFocusId => RequestAccessibilityFocus(virtualViewId, snapshot),
                ActionClearAccessibilityFocusId => ClearAccessibilityFocus(virtualViewId, snapshot),
                _ => false,
            };
        }

        public bool OnHover(Android.Views.View? view, MotionEvent? motionEvent)
        {
            if (motionEvent is null)
            {
                return false;
            }

            var virtualViewId = FindVirtualNodeAt(motionEvent.GetX(), motionEvent.GetY());
            switch (motionEvent.Action)
            {
                case MotionEventActions.HoverEnter:
                case MotionEventActions.HoverMove:
                    UpdateHoveredNode(virtualViewId);
                    return virtualViewId != RootViewId;
                case MotionEventActions.HoverExit:
                    if (_hoveredId != RootViewId)
                    {
                        UpdateHoveredNode(RootViewId);
                        return true;
                    }
                    return false;
                default:
                    return false;
            }
        }

        private AccessibilityNodeInfoCompat? CreateHostNode()
        {
#pragma warning disable CS0618
            var node = AccessibilityNodeInfoCompat.Obtain(_host);
#pragma warning restore CS0618
            if (node is null)
            {
                return null;
            }

            if (_rootDelegate is not null)
            {
                _rootDelegate.OnInitializeAccessibilityNodeInfo(_host, node);
            }
            else
            {
#pragma warning disable CS0618
                ViewCompat.OnInitializeAccessibilityNodeInfo(_host, node);
#pragma warning restore CS0618
            }

            foreach (var virtualId in _nodesById.Keys.Order())
            {
                node.AddChild(_host, virtualId);
            }
            return node;
        }

        private AccessibilityNodeInfoCompat? CreateVirtualNode(VirtualNodeSnapshot snapshot)
        {
#pragma warning disable CS0618
            var node = AccessibilityNodeInfoCompat.Obtain();
#pragma warning restore CS0618
            if (node is null)
            {
                return null;
            }

            node.Enabled = true;
            node.VisibleToUser = true;
            node.Focusable = true;
            node.Clickable = true;
            node.Selected = snapshot.Node.IsSelected;
            node.PackageName = _host.Context?.PackageName;
            node.ClassName = snapshot.Node.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable
                ? "android.widget.TableLayout"
                : "android.view.View";
            node.ContentDescription = snapshot.ContentDescription;
            node.SetParent(_host);
            node.SetSource(_host, snapshot.VirtualId);
#pragma warning disable CS0618
            node.SetBoundsInParent(snapshot.BoundsInParent);
#pragma warning restore CS0618

            var screenBounds = new AndroidRect(snapshot.BoundsInParent);
            var location = new int[2];
            _host.GetLocationOnScreen(location);
            screenBounds.Offset(location[0], location[1]);
            node.SetBoundsInScreen(screenBounds);

            node.AddAction(AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionClick);
            if (_accessibilityFocusedId == snapshot.VirtualId)
            {
                node.AccessibilityFocused = true;
                node.AddAction(
                    AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionClearAccessibilityFocus);
            }
            else
            {
                node.AccessibilityFocused = false;
                node.AddAction(
                    AccessibilityNodeInfoCompat.AccessibilityActionCompat.ActionAccessibilityFocus);
            }

            return node;
        }

        private bool Select(VirtualNodeSnapshot snapshot)
        {
            var session = _view.Session;
            if (session is null)
            {
                return false;
            }

            session.AnalyticsInteraction.Select(snapshot.Node.Item);
            _view.InvalidateSurface();
            SendEvent(snapshot.VirtualId, EventTypes.ViewClicked, snapshot);
            return true;
        }

        private bool RequestAccessibilityFocus(
            int virtualViewId,
            VirtualNodeSnapshot snapshot)
        {
            if (_accessibilityFocusedId == virtualViewId)
            {
                return false;
            }

            if (_accessibilityFocusedId != RootViewId &&
                _nodesById.TryGetValue(_accessibilityFocusedId, out var previous))
            {
                var previousId = _accessibilityFocusedId;
                _accessibilityFocusedId = RootViewId;
                SendEvent(previousId, EventTypes.ViewAccessibilityFocusCleared, previous);
            }

            _accessibilityFocusedId = virtualViewId;
            SendEvent(virtualViewId, EventTypes.ViewAccessibilityFocused, snapshot);
            return true;
        }

        private bool ClearAccessibilityFocus(
            int virtualViewId,
            VirtualNodeSnapshot snapshot)
        {
            if (_accessibilityFocusedId != virtualViewId)
            {
                return false;
            }

            _accessibilityFocusedId = RootViewId;
            SendEvent(virtualViewId, EventTypes.ViewAccessibilityFocusCleared, snapshot);
            return true;
        }

        private void UpdateHoveredNode(int virtualViewId)
        {
            if (_hoveredId == virtualViewId)
            {
                return;
            }

            if (_hoveredId != RootViewId &&
                _nodesById.TryGetValue(_hoveredId, out var previous))
            {
                SendEvent(_hoveredId, EventTypes.ViewHoverExit, previous);
            }

            _hoveredId = virtualViewId;
            if (virtualViewId != RootViewId &&
                _nodesById.TryGetValue(virtualViewId, out var current))
            {
                SendEvent(virtualViewId, EventTypes.ViewHoverEnter, current);
            }
        }

        private int FindVirtualNodeAt(float x, float y)
        {
            foreach (var snapshot in _nodesById.Values
                         .OrderByDescending(static value => value.Node.ZIndex))
            {
                if (snapshot.BoundsInParent.Contains((int)x, (int)y))
                {
                    return snapshot.VirtualId;
                }
            }
            return RootViewId;
        }

        private void SendEvent(
            int virtualViewId,
            EventTypes eventType,
            VirtualNodeSnapshot? snapshot)
        {
            var parent = _host.Parent;
            if (!_host.IsShown || parent is null)
            {
                return;
            }

            using var accessibilityEvent = new AccessibilityEvent((int)eventType)
            {
                PackageName = _host.Context?.PackageName,
                ClassName = snapshot?.Node.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable
                    ? "android.widget.TableLayout"
                    : "android.view.View",
            };
            if (snapshot is not null)
            {
                accessibilityEvent.ContentDescription = snapshot.ContentDescription;
            }
            accessibilityEvent.SetSource(_host, virtualViewId);
            parent.RequestSendAccessibilityEvent(_host, accessibilityEvent);
        }

        private int GetOrCreateVirtualId(SpreadsheetAnalyticsItemKey item)
        {
            if (_virtualIds.TryGetValue(item, out var virtualId))
            {
                return virtualId;
            }

            virtualId = _nextVirtualId++;
            _virtualIds.Add(item, virtualId);
            return virtualId;
        }

        private static bool SnapshotSetsEquivalent(
            IReadOnlyDictionary<int, VirtualNodeSnapshot> left,
            IReadOnlyDictionary<int, VirtualNodeSnapshot> right)
        {
            if (left.Count != right.Count)
            {
                return false;
            }

            foreach (var pair in left)
            {
                if (!right.TryGetValue(pair.Key, out var other) ||
                    !SnapshotsEquivalent(pair.Value, other))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool SnapshotsEquivalent(
            VirtualNodeSnapshot left,
            VirtualNodeSnapshot right) =>
            left.VirtualId == right.VirtualId &&
            left.Node.Item == right.Node.Item &&
            string.Equals(left.Node.Name, right.Node.Name, StringComparison.Ordinal) &&
            left.Node.Role == right.Node.Role &&
            left.Node.ZIndex == right.Node.ZIndex &&
            left.Node.IsSelected == right.Node.IsSelected &&
            string.Equals(left.ContentDescription, right.ContentDescription, StringComparison.Ordinal) &&
            left.BoundsInParent.Left == right.BoundsInParent.Left &&
            left.BoundsInParent.Top == right.BoundsInParent.Top &&
            left.BoundsInParent.Right == right.BoundsInParent.Right &&
            left.BoundsInParent.Bottom == right.BoundsInParent.Bottom;

        private static string BuildContentDescription(SpreadsheetAnalyticsAccessibleNode node)
        {
            var role = node.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable
                ? "Bảng tổng hợp"
                : "Biểu đồ";
            var selection = node.IsSelected ? " Đang chọn." : string.Empty;
            return $"{role}: {node.Name}.{selection} Chạm hai lần để chọn.";
        }

        private static AndroidRect ToNativeBounds(
            RectD visible,
            SpreadsheetChromeMetrics chrome,
            double zoom,
            double nativeScaleX,
            double nativeScaleY,
            int hostWidth,
            int hostHeight)
        {
            var left = (chrome.RowHeaderWidth + visible.Left) * zoom * nativeScaleX;
            var top = (chrome.ColumnHeaderHeight + visible.Top) * zoom * nativeScaleY;
            var right = (chrome.RowHeaderWidth + visible.Right) * zoom * nativeScaleX;
            var bottom = (chrome.ColumnHeaderHeight + visible.Bottom) * zoom * nativeScaleY;

            if (!double.IsFinite(left) ||
                !double.IsFinite(top) ||
                !double.IsFinite(right) ||
                !double.IsFinite(bottom))
            {
                return new AndroidRect();
            }

            var nativeLeft = Math.Clamp((int)Math.Floor(left), 0, Math.Max(0, hostWidth));
            var nativeTop = Math.Clamp((int)Math.Floor(top), 0, Math.Max(0, hostHeight));
            var nativeRight = Math.Clamp((int)Math.Ceiling(right), 0, Math.Max(0, hostWidth));
            var nativeBottom = Math.Clamp((int)Math.Ceiling(bottom), 0, Math.Max(0, hostHeight));
            return nativeRight > nativeLeft && nativeBottom > nativeTop
                ? new AndroidRect(nativeLeft, nativeTop, nativeRight, nativeBottom)
                : new AndroidRect();
        }

        private static double ResolveNativeScale(int nativeDimension, int logicalDimension) =>
            nativeDimension > 0 && logicalDimension > 0
                ? (double)nativeDimension / logicalDimension
                : 1d;

        private sealed record VirtualNodeSnapshot(
            int VirtualId,
            SpreadsheetAnalyticsAccessibleNode Node,
            AndroidRect BoundsInParent,
            string ContentDescription);
    }
}
#endif
