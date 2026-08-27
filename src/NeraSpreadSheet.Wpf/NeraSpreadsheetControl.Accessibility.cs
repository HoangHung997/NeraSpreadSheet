using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using NeraSpreadSheet.Core;
using NeraSpreadSheet.Foundation;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.Wpf;

public sealed partial class NeraSpreadsheetControl
{
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new NeraSpreadsheetControlAutomationPeer(this);

    internal IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> GetNativeAnalyticsAccessibilityNodes()
    {
        if (_disposed || _session is null || _lastLayout is null || _analyticsInput is null)
        {
            return [];
        }

        return _analyticsInput.GetAccessibilityNodes(
            _lastLayout,
            ResolveAnalyticsAccessibilityName);
    }

    internal SpreadsheetAnalyticsAccessibleNode? GetNativeAnalyticsAccessibilityNode(
        SpreadsheetAnalyticsItemKey item) =>
        GetNativeAnalyticsAccessibilityNodes()
            .FirstOrDefault(node => node.Item == item);

    internal Rect GetNativeAnalyticsScreenBounds(SpreadsheetAnalyticsItemKey item)
    {
        var node = GetNativeAnalyticsAccessibilityNode(item);
        if (node is null || PresentationSource.FromVisual(this) is null)
        {
            return Rect.Empty;
        }

        var visible = node.ViewportBounds.Intersect(node.ClipBounds);
        if (visible.IsEmpty)
        {
            return Rect.Empty;
        }

        var chrome = GetChromeMetrics();
        var topLeft = PointToScreen(new Point(
            chrome.RowHeaderWidth + visible.Left,
            chrome.ColumnHeaderHeight + visible.Top));
        var bottomRight = PointToScreen(new Point(
            chrome.RowHeaderWidth + visible.Right,
            chrome.ColumnHeaderHeight + visible.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    internal bool SelectNativeAnalyticsItem(SpreadsheetAnalyticsItemKey item)
    {
        if (_session is null || GetNativeAnalyticsAccessibilityNode(item) is null)
        {
            return false;
        }

        var changed = _session.AnalyticsInteraction.Select(item);
        Focus();
        InvalidateVisual();
        return changed || _session.AnalyticsInteraction.SelectedItem == item;
    }

    internal bool MoveNativeAnalyticsItem(
        SpreadsheetAnalyticsItemKey item,
        double screenX,
        double screenY)
    {
        if (_session is null || !double.IsFinite(screenX) || !double.IsFinite(screenY))
        {
            return false;
        }

        var bounds = GetNativeAnalyticsScreenBounds(item);
        if (bounds.IsEmpty)
        {
            return false;
        }

        var currentLocal = PointFromScreen(bounds.TopLeft);
        var requestedLocal = PointFromScreen(new Point(screenX, screenY));
        return _session.AnalyticsPlacements.MoveBy(
            item,
            requestedLocal.X - currentLocal.X,
            requestedLocal.Y - currentLocal.Y);
    }

    internal bool ResizeNativeAnalyticsItem(
        SpreadsheetAnalyticsItemKey item,
        double screenWidth,
        double screenHeight)
    {
        if (_session is null ||
            !double.IsFinite(screenWidth) ||
            !double.IsFinite(screenHeight) ||
            screenWidth <= 0d ||
            screenHeight <= 0d)
        {
            return false;
        }

        var screenBounds = GetNativeAnalyticsScreenBounds(item);
        if (screenBounds.IsEmpty)
        {
            return false;
        }

        var localTopLeft = PointFromScreen(screenBounds.TopLeft);
        var localBottomRight = PointFromScreen(new Point(
            screenBounds.Left + screenWidth,
            screenBounds.Top + screenHeight));
        var width = localBottomRight.X - localTopLeft.X;
        var height = localBottomRight.Y - localTopLeft.Y;
        if (width <= 0d || height <= 0d)
        {
            return false;
        }

        var placement = _session.AnalyticsPlacements.GetPlacement(item);
        return _session.AnalyticsPlacements.SetBounds(
            item,
            new RectD(
                placement.DocumentBounds.X,
                placement.DocumentBounds.Y,
                width,
                height));
    }

    internal void RefreshNativeAnalyticsAccessibility()
    {
        if (_disposed)
        {
            return;
        }

        var peer = UIElementAutomationPeer.FromElement(this);
        if (peer is null)
        {
            return;
        }
        peer.ResetChildrenCache();
        peer.InvalidatePeer();
    }

    private string? ResolveAnalyticsAccessibilityName(SpreadsheetAnalyticsItemKey item)
    {
        if (_session is null)
        {
            return null;
        }

        var worksheet = _session.ActiveWorksheet;
        return item.Kind switch
        {
            SpreadsheetAnalyticsItemKind.Chart =>
                _session.Analytics.GetCharts(worksheet)
                    .FirstOrDefault(value => value.Id == item.Id)
                    ?.Name,
            SpreadsheetAnalyticsItemKind.Pivot =>
                _session.Analytics.GetPivots(worksheet)
                    .FirstOrDefault(value => value.Id == item.Id)
                    ?.Name,
            _ => null,
        };
    }
}

internal sealed class NeraSpreadsheetControlAutomationPeer : FrameworkElementAutomationPeer
{
    private readonly Dictionary<SpreadsheetAnalyticsItemKey, NeraSpreadsheetAnalyticsAutomationPeer>
        _analyticsPeers = [];

    public NeraSpreadsheetControlAutomationPeer(NeraSpreadsheetControl owner)
        : base(owner)
    {
    }

    private NeraSpreadsheetControl SpreadsheetOwner =>
        (NeraSpreadsheetControl)Owner;

    protected override string GetClassNameCore() => "NeraSpreadsheetControl";

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.DataGrid;

    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrWhiteSpace(name) ? "Spreadsheet" : name;
    }

    protected override List<AutomationPeer>? GetChildrenCore()
    {
        var baseChildren = base.GetChildrenCore();
        var nodes = SpreadsheetOwner.GetNativeAnalyticsAccessibilityNodes();
        if (nodes.Count == 0)
        {
            _analyticsPeers.Clear();
            return baseChildren;
        }

        var activeItems = nodes.Select(static node => node.Item).ToHashSet();
        foreach (var stale in _analyticsPeers.Keys
                     .Where(item => !activeItems.Contains(item))
                     .ToArray())
        {
            _analyticsPeers.Remove(stale);
        }

        var children = baseChildren is null
            ? new List<AutomationPeer>(nodes.Count)
            : new List<AutomationPeer>(baseChildren);
        foreach (var node in nodes)
        {
            if (!_analyticsPeers.TryGetValue(node.Item, out var peer))
            {
                peer = new NeraSpreadsheetAnalyticsAutomationPeer(
                    SpreadsheetOwner,
                    node.Item);
                _analyticsPeers.Add(node.Item, peer);
            }
            children.Add(peer);
        }
        return children;
    }
}

internal sealed class NeraSpreadsheetAnalyticsAutomationPeer : AutomationPeer, IInvokeProvider, ITransformProvider
{
    private readonly NeraSpreadsheetControl _owner;
    private readonly SpreadsheetAnalyticsItemKey _item;

    public NeraSpreadsheetAnalyticsAutomationPeer(
        NeraSpreadsheetControl owner,
        SpreadsheetAnalyticsItemKey item)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _item = item;
    }

    public override object? GetPattern(PatternInterface patternInterface) =>
        patternInterface is PatternInterface.Invoke or PatternInterface.Transform
            ? this
            : null;

    public bool CanMove => true;

    public bool CanResize => true;

    public bool CanRotate => false;

    void IInvokeProvider.Invoke() =>
        RunOnOwnerDispatcher(() => _owner.SelectNativeAnalyticsItem(_item));

    void ITransformProvider.Move(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        RunOnOwnerDispatcher(() =>
        {
            if (!_owner.MoveNativeAnalyticsItem(_item, x, y))
            {
                throw new InvalidOperationException("The analytics item could not be moved.");
            }
        });
    }

    void ITransformProvider.Resize(double width, double height)
    {
        if (!double.IsFinite(width) ||
            !double.IsFinite(height) ||
            width <= 0d ||
            height <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        RunOnOwnerDispatcher(() =>
        {
            if (!_owner.ResizeNativeAnalyticsItem(_item, width, height))
            {
                throw new InvalidOperationException("The analytics item could not be resized.");
            }
        });
    }

    void ITransformProvider.Rotate(double degrees)
    {
        throw new InvalidOperationException("Analytics items do not support rotation.");
    }

    protected override string GetAcceleratorKeyCore() => string.Empty;

    protected override string GetAccessKeyCore() => string.Empty;

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        ResolveNode()?.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable
            ? AutomationControlType.Table
            : AutomationControlType.Group;

    protected override string GetAutomationIdCore() =>
        ResolveNode()?.AutomationId ?? string.Empty;

    protected override Rect GetBoundingRectangleCore() =>
        RunOnOwnerDispatcher(() => _owner.GetNativeAnalyticsScreenBounds(_item));

    protected override List<AutomationPeer>? GetChildrenCore() => null;

    protected override string GetClassNameCore() =>
        ResolveNode()?.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable
            ? "SpreadsheetPivotTable"
            : "SpreadsheetChart";

    protected override Point GetClickablePointCore()
    {
        var bounds = GetBoundingRectangleCore();
        return bounds.IsEmpty
            ? new Point(double.NaN, double.NaN)
            : new Point(
                bounds.Left + (bounds.Width / 2d),
                bounds.Top + (bounds.Height / 2d));
    }

    protected override string GetHelpTextCore()
    {
        var node = ResolveNode();
        return node is null
            ? string.Empty
            : string.Join(", ", node.Actions);
    }

    protected override string GetItemStatusCore() =>
        ResolveNode()?.IsSelected == true ? "Selected" : string.Empty;

    protected override string GetItemTypeCore() =>
        ResolveNode()?.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable
            ? "Pivot table"
            : "Chart";

    protected override AutomationPeer? GetLabeledByCore() => null;

    protected override string GetNameCore() => ResolveNode()?.Name ?? string.Empty;

    protected override AutomationOrientation GetOrientationCore() =>
        AutomationOrientation.None;

    protected override bool HasKeyboardFocusCore() =>
        ResolveNode()?.IsSelected == true &&
        RunOnOwnerDispatcher(() => _owner.IsKeyboardFocusWithin);

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;

    protected override bool IsEnabledCore() =>
        RunOnOwnerDispatcher(() => _owner.IsEnabled);

    protected override bool IsKeyboardFocusableCore() => true;

    protected override bool IsOffscreenCore() => GetBoundingRectangleCore().IsEmpty;

    protected override bool IsPasswordCore() => false;

    protected override bool IsRequiredForFormCore() => false;

    protected override void SetFocusCore()
    {
        RunOnOwnerDispatcher(() =>
        {
            if (!_owner.SelectNativeAnalyticsItem(_item))
            {
                throw new InvalidOperationException("The analytics item could not receive focus.");
            }
        });
    }

    private SpreadsheetAnalyticsAccessibleNode? ResolveNode() =>
        RunOnOwnerDispatcher(() => _owner.GetNativeAnalyticsAccessibilityNode(_item));

    private void RunOnOwnerDispatcher(Action action)
    {
        if (_owner.Dispatcher.CheckAccess())
        {
            action();
            return;
        }
        _owner.Dispatcher.Invoke(action);
    }

    private T RunOnOwnerDispatcher<T>(Func<T> action)
    {
        if (_owner.Dispatcher.CheckAccess())
        {
            return action();
        }
        return _owner.Dispatcher.Invoke(action);
    }
}
