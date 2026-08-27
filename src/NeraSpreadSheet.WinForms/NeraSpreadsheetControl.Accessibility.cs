using NeraSpreadSheet.Core;
using NeraSpreadSheet.Interaction;

namespace NeraSpreadSheet.WinForms;

public sealed partial class NeraSpreadsheetControl
{
    protected override AccessibleObject CreateAccessibilityInstance() =>
        new NeraSpreadsheetAccessibleObject(this);

    internal IReadOnlyList<SpreadsheetAnalyticsAccessibleNode> GetNativeAnalyticsAccessibilityNodes()
    {
        if (_session is null || _lastLayout is null || _analyticsInput is null)
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

    internal Rectangle GetNativeAnalyticsScreenBounds(SpreadsheetAnalyticsItemKey item)
    {
        var node = GetNativeAnalyticsAccessibilityNode(item);
        if (node is null)
        {
            return Rectangle.Empty;
        }

        var visible = node.ViewportBounds.Intersect(node.ClipBounds);
        if (visible.IsEmpty)
        {
            return Rectangle.Empty;
        }

        var chrome = GetChromeMetrics();
        var clientLeft = chrome.RowHeaderWidth + visible.Left;
        var clientTop = chrome.ColumnHeaderHeight + visible.Top;
        var clientRight = chrome.RowHeaderWidth + visible.Right;
        var clientBottom = chrome.ColumnHeaderHeight + visible.Bottom;
        var origin = PointToScreen(Point.Empty);
        return Rectangle.FromLTRB(
            checked(origin.X + (int)Math.Floor(clientLeft)),
            checked(origin.Y + (int)Math.Floor(clientTop)),
            checked(origin.X + (int)Math.Ceiling(clientRight)),
            checked(origin.Y + (int)Math.Ceiling(clientBottom)));
    }

    internal bool SelectNativeAnalyticsItem(SpreadsheetAnalyticsItemKey item)
    {
        if (_session is null || GetNativeAnalyticsAccessibilityNode(item) is null)
        {
            return false;
        }

        var changed = _session.AnalyticsInteraction.Select(item);
        Focus();
        Invalidate();
        return changed || _session.AnalyticsInteraction.SelectedItem == item;
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

internal sealed class NeraSpreadsheetAccessibleObject : Control.ControlAccessibleObject
{
    private readonly NeraSpreadsheetControl _owner;
    private readonly Dictionary<SpreadsheetAnalyticsItemKey, NeraSpreadsheetAnalyticsAccessibleObject>
        _analyticsChildren = [];

    public NeraSpreadsheetAccessibleObject(NeraSpreadsheetControl owner)
        : base(owner)
    {
        _owner = owner;
    }

    public override string? Name
    {
        get => string.IsNullOrWhiteSpace(base.Name) ? "Spreadsheet" : base.Name;
        set => base.Name = value;
    }

    public override AccessibleRole Role => AccessibleRole.Table;

    public override int GetChildCount() => GetChildren().Length;

    public override AccessibleObject? GetChild(int index)
    {
        var children = GetChildren();
        return index >= 0 && index < children.Length
            ? children[index]
            : null;
    }

    public override AccessibleObject? GetFocused() => GetSelected();

    public override AccessibleObject? GetSelected()
    {
        var selected = _owner.Session?.AnalyticsInteraction.SelectedItem;
        return selected.HasValue &&
               _analyticsChildren.TryGetValue(selected.Value, out var child)
            ? child
            : null;
    }

    public override AccessibleObject? HitTest(int x, int y)
    {
        foreach (var child in GetChildren().Reverse())
        {
            if (child.Bounds.Contains(x, y))
            {
                return child;
            }
        }
        return base.HitTest(x, y);
    }

    private NeraSpreadsheetAnalyticsAccessibleObject[] GetChildren()
    {
        var nodes = _owner.GetNativeAnalyticsAccessibilityNodes();
        if (nodes.Count == 0)
        {
            _analyticsChildren.Clear();
            return [];
        }

        var activeItems = nodes.Select(static node => node.Item).ToHashSet();
        foreach (var stale in _analyticsChildren.Keys
                     .Where(item => !activeItems.Contains(item))
                     .ToArray())
        {
            _analyticsChildren.Remove(stale);
        }

        var children = new NeraSpreadsheetAnalyticsAccessibleObject[nodes.Count];
        for (var index = 0; index < nodes.Count; index++)
        {
            var item = nodes[index].Item;
            if (!_analyticsChildren.TryGetValue(item, out var child))
            {
                child = new NeraSpreadsheetAnalyticsAccessibleObject(
                    _owner,
                    this,
                    item);
                _analyticsChildren.Add(item, child);
            }
            children[index] = child;
        }
        return children;
    }
}

internal sealed class NeraSpreadsheetAnalyticsAccessibleObject : AccessibleObject
{
    private readonly NeraSpreadsheetControl _owner;
    private readonly NeraSpreadsheetAccessibleObject _parent;
    private readonly SpreadsheetAnalyticsItemKey _item;

    public NeraSpreadsheetAnalyticsAccessibleObject(
        NeraSpreadsheetControl owner,
        NeraSpreadsheetAccessibleObject parent,
        SpreadsheetAnalyticsItemKey item)
    {
        _owner = owner;
        _parent = parent;
        _item = item;
    }

    public override AccessibleObject? Parent => _parent;

    public override string? Name
    {
        get => ResolveNode()?.Name ?? string.Empty;
        set { }
    }

    public override string? Description =>
        ResolveNode() is { } node
            ? $"{GetRoleLabel(node.Role)}. Actions: {string.Join(", ", node.Actions)}."
            : null;

    public override string? DefaultAction => "Select";

    public override AccessibleRole Role =>
        ResolveNode()?.Role == SpreadsheetAnalyticsAccessibleRole.PivotTable
            ? AccessibleRole.Table
            : AccessibleRole.Chart;

    public override Rectangle Bounds => _owner.GetNativeAnalyticsScreenBounds(_item);

    public override AccessibleStates State
    {
        get
        {
            var node = ResolveNode();
            if (node is null)
            {
                return AccessibleStates.Unavailable | AccessibleStates.Offscreen;
            }

            var state = AccessibleStates.Selectable | AccessibleStates.Focusable;
            if (node.IsSelected)
            {
                state |= AccessibleStates.Selected | AccessibleStates.Focused;
            }
            if (Bounds.IsEmpty)
            {
                state |= AccessibleStates.Offscreen;
            }
            return state;
        }
    }

    public override int GetChildCount() => 0;

    public override void DoDefaultAction()
    {
        _owner.SelectNativeAnalyticsItem(_item);
    }

    public override void Select(AccessibleSelection flags)
    {
        if ((flags & (AccessibleSelection.TakeSelection | AccessibleSelection.TakeFocus)) != 0)
        {
            _owner.SelectNativeAnalyticsItem(_item);
        }
        else if ((flags & AccessibleSelection.RemoveSelection) != 0 &&
                 _owner.Session?.AnalyticsInteraction.SelectedItem == _item)
        {
            _owner.Session.AnalyticsInteraction.ClearSelection();
            _owner.Invalidate();
        }
    }

    private SpreadsheetAnalyticsAccessibleNode? ResolveNode() =>
        _owner.GetNativeAnalyticsAccessibilityNode(_item);

    private static string GetRoleLabel(SpreadsheetAnalyticsAccessibleRole role) =>
        role == SpreadsheetAnalyticsAccessibleRole.PivotTable
            ? "Pivot table"
            : "Chart";
}
