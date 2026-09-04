using NeraSpreadSheet.Commands;

namespace NeraSpreadSheet.Ribbon.Core;

/// <summary>
/// Identifies the command chrome size selected by responsive Ribbon layout.
/// </summary>
public enum RibbonItemSize
{
    /// <summary>Large icon above the caption.</summary>
    Large,

    /// <summary>Small icon beside the full caption.</summary>
    Small,

    /// <summary>Compact icon-first command chrome.</summary>
    Compact,
}

/// <summary>
/// Identifies how a Ribbon group is presented at the current width.
/// </summary>
public enum RibbonGroupLayoutMode
{
    /// <summary>The group uses the preferred command sizes.</summary>
    Expanded,

    /// <summary>The group remains inline with reduced command sizes.</summary>
    Compact,

    /// <summary>The group is available through the tab overflow surface.</summary>
    Overflow,
}

/// <summary>
/// Defines deterministic logical measurements used by responsive layout.
/// </summary>
public sealed record RibbonLayoutMetrics
{
    /// <summary>Gets the default layout metrics.</summary>
    public static RibbonLayoutMetrics Default { get; } = new();

    /// <summary>Gets the logical width of a large item.</summary>
    public double LargeItemWidth { get; init; } = 84d;

    /// <summary>Gets the logical width of a small item.</summary>
    public double SmallItemWidth { get; init; } = 72d;

    /// <summary>Gets the logical width of a compact item.</summary>
    public double CompactItemWidth { get; init; } = 40d;

    /// <summary>Gets the logical horizontal chrome width of each inline group.</summary>
    public double GroupChromeWidth { get; init; } = 20d;

    /// <summary>Gets the logical gap between commands and groups.</summary>
    public double Spacing { get; init; } = 8d;

    /// <summary>Gets the logical width reserved for the shared overflow affordance.</summary>
    public double OverflowWidth { get; init; } = 60d;
}

/// <summary>
/// Describes one host-neutral responsive Ribbon layout operation.
/// </summary>
public sealed record RibbonLayoutRequest
{
    /// <summary>Creates a responsive layout request.</summary>
    /// <param name="availableWidth">Available physical width in pixels.</param>
    /// <param name="scale">Physical pixels per logical unit.</param>
    /// <param name="selectedTabId">Stable selected tab identity, when known.</param>
    /// <param name="focusedCommandId">Stable focused command identity, when known.</param>
    /// <param name="metrics">Optional logical measurement metrics.</param>
    public RibbonLayoutRequest(
        double availableWidth,
        double scale = 1d,
        string? selectedTabId = null,
        CommandId? focusedCommandId = null,
        RibbonLayoutMetrics? metrics = null)
    {
        if (double.IsNaN(availableWidth) || availableWidth < 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(availableWidth));
        }
        if (!double.IsFinite(scale) || scale <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(scale));
        }

        AvailableWidth = availableWidth;
        Scale = scale;
        SelectedTabId = selectedTabId;
        FocusedCommandId = focusedCommandId;
        Metrics = metrics ?? RibbonLayoutMetrics.Default;
        ValidateMetrics(Metrics);
    }

    /// <summary>Gets the available physical width.</summary>
    public double AvailableWidth { get; }

    /// <summary>Gets the physical scale.</summary>
    public double Scale { get; }

    /// <summary>Gets the requested selected tab identity.</summary>
    public string? SelectedTabId { get; }

    /// <summary>Gets the requested focused command identity.</summary>
    public CommandId? FocusedCommandId { get; }

    /// <summary>Gets the logical measurement metrics.</summary>
    public RibbonLayoutMetrics Metrics { get; }

    private static void ValidateMetrics(RibbonLayoutMetrics metrics)
    {
        double[] values =
        [
            metrics.LargeItemWidth,
            metrics.SmallItemWidth,
            metrics.CompactItemWidth,
            metrics.GroupChromeWidth,
            metrics.Spacing,
            metrics.OverflowWidth,
        ];
        if (values.Any(static value => !double.IsFinite(value) || value < 0d))
        {
            throw new ArgumentOutOfRangeException(
                nameof(metrics),
                "Ribbon layout metrics must be finite and non-negative.");
        }
        if (metrics.LargeItemWidth < metrics.SmallItemWidth ||
            metrics.SmallItemWidth < metrics.CompactItemWidth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(metrics),
                "Ribbon item widths must be ordered large, small, then compact.");
        }
    }
}

/// <summary>Immutable responsive layout for one Ribbon command.</summary>
/// <param name="Presentation">Command presentation being placed.</param>
/// <param name="Size">Responsive size selected for the command.</param>
/// <param name="Width">Measured occupied width in physical pixels.</param>
public sealed record RibbonItemLayout(
    RibbonItemPresentation Presentation,
    RibbonItemSize Size,
    double Width);

/// <summary>Immutable responsive layout for one Ribbon group.</summary>
/// <param name="Presentation">Group presentation being placed.</param>
/// <param name="Mode">Responsive mode selected for the group.</param>
/// <param name="Items">Immutable item layouts in definition order.</param>
/// <param name="Width">Inline occupied width in physical pixels, or zero for overflow.</param>
public sealed record RibbonGroupLayout(
    RibbonGroupPresentation Presentation,
    RibbonGroupLayoutMode Mode,
    IReadOnlyList<RibbonItemLayout> Items,
    double Width);

/// <summary>Immutable responsive layout for one Ribbon tab.</summary>
/// <param name="Presentation">Tab presentation being placed.</param>
/// <param name="Groups">Immutable group layouts in definition order.</param>
/// <param name="InlineWidth">Total occupied inline width in physical pixels.</param>
/// <param name="HasOverflow">Whether the shared overflow affordance is required.</param>
public sealed record RibbonTabLayout(
    RibbonTabPresentation Presentation,
    IReadOnlyList<RibbonGroupLayout> Groups,
    double InlineWidth,
    bool HasOverflow);

/// <summary>
/// Immutable layout snapshot consumed identically by all platform presenters.
/// </summary>
/// <param name="Tabs">Responsive tab layouts in definition order.</param>
/// <param name="SelectedTabId">Resolved stable selected tab identity.</param>
/// <param name="FocusedCommandId">Retained stable command focus identity.</param>
/// <param name="AvailableWidth">Available physical width used for measurement.</param>
/// <param name="Scale">Physical pixels per logical unit.</param>
public sealed record RibbonLayoutSnapshot(
    IReadOnlyList<RibbonTabLayout> Tabs,
    string? SelectedTabId,
    CommandId? FocusedCommandId,
    double AvailableWidth,
    double Scale);

/// <summary>
/// Measures and collapses a presentation snapshot without platform dependencies.
/// </summary>
public sealed class RibbonResponsiveLayoutEngine
{
    private readonly StringComparer _identityComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Produces deterministic large, small, compact and overflow group states.
    /// </summary>
    public RibbonLayoutSnapshot Layout(
        RibbonPresentationSnapshot presentation,
        RibbonLayoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(request);

        var selectedTab = presentation.Tabs.FirstOrDefault(tab =>
                _identityComparer.Equals(tab.Id, request.SelectedTabId));
        if (selectedTab is null && presentation.Tabs.Count > 0)
        {
            selectedTab = presentation.Tabs[0];
        }
        CommandId? focusedCommand = request.FocusedCommandId is { } requestedFocus &&
            presentation.Tabs.SelectMany(static tab => tab.Groups)
                .SelectMany(static group => group.Items)
                .Any(item => item.Command.CommandId == requestedFocus)
                ? requestedFocus
                : (CommandId?)null;
        var tabs = presentation.Tabs
            .Select(tab => LayoutTab(tab, request))
            .ToArray();
        return new RibbonLayoutSnapshot(
            tabs,
            selectedTab?.Id,
            focusedCommand,
            request.AvailableWidth,
            request.Scale);
    }

    private static RibbonTabLayout LayoutTab(
        RibbonTabPresentation tab,
        RibbonLayoutRequest request)
    {
        var metrics = request.Metrics;
        var scale = request.Scale;
        var states = tab.Groups.Select((group, index) => new GroupState(
            group,
            index,
            group.Items.Select(item => item.IsLarge
                ? RibbonItemSize.Large
                : RibbonItemSize.Small).ToArray())).ToArray();
        var collapseOrder = states
            .OrderBy(state => state.Presentation.CollapsePriority)
            .ThenByDescending(static state => state.Index)
            .ToArray();

        foreach (var state in collapseOrder)
        {
            if (Measure(states, metrics, scale) <= request.AvailableWidth)
            {
                break;
            }
            state.ReduceLargeItems();
            if (Measure(states, metrics, scale) <= request.AvailableWidth)
            {
                continue;
            }
            state.MakeCompact();
            if (Measure(states, metrics, scale) <= request.AvailableWidth)
            {
                continue;
            }
            state.IsOverflow = true;
        }

        var groups = states.Select(state => state.ToLayout(metrics, scale)).ToArray();
        return new RibbonTabLayout(
            tab,
            groups,
            Measure(states, metrics, scale),
            states.Any(static state => state.IsOverflow));
    }

    private static double Measure(
        IReadOnlyList<GroupState> states,
        RibbonLayoutMetrics metrics,
        double scale)
    {
        var inline = states.Where(static state => !state.IsOverflow).ToArray();
        var width = inline.Sum(state => state.Measure(metrics, scale));
        if (inline.Length > 1)
        {
            width += (inline.Length - 1) * metrics.Spacing * scale;
        }
        if (states.Any(static state => state.IsOverflow))
        {
            width += metrics.OverflowWidth * scale;
            if (inline.Length > 0)
            {
                width += metrics.Spacing * scale;
            }
        }
        return width;
    }

    private sealed class GroupState
    {
        public GroupState(
            RibbonGroupPresentation presentation,
            int index,
            RibbonItemSize[] sizes)
        {
            Presentation = presentation;
            Index = index;
            Sizes = sizes;
        }

        public RibbonGroupPresentation Presentation { get; }

        public int Index { get; }

        public RibbonItemSize[] Sizes { get; }

        public bool IsOverflow { get; set; }

        public void ReduceLargeItems()
        {
            for (var index = 0; index < Sizes.Length; index++)
            {
                if (Sizes[index] == RibbonItemSize.Large)
                {
                    Sizes[index] = RibbonItemSize.Small;
                }
            }
        }

        public void MakeCompact()
        {
            for (var index = 0; index < Sizes.Length; index++)
            {
                Sizes[index] = RibbonItemSize.Compact;
            }
        }

        public double Measure(RibbonLayoutMetrics metrics, double scale) =>
            (metrics.GroupChromeWidth +
             Presentation.Items.Select((item, index) =>
                 GetItemWidth(item, Sizes[index], metrics)).Sum() +
             Math.Max(0, Sizes.Length - 1) * metrics.Spacing) * scale;

        public RibbonGroupLayout ToLayout(RibbonLayoutMetrics metrics, double scale)
        {
            var items = Presentation.Items.Select((item, index) =>
                new RibbonItemLayout(
                    item,
                    Sizes[index],
                    GetItemWidth(item, Sizes[index], metrics) * scale)).ToArray();
            var mode = IsOverflow
                ? RibbonGroupLayoutMode.Overflow
                : Sizes.SequenceEqual(Presentation.Items.Select(item => item.IsLarge
                    ? RibbonItemSize.Large
                    : RibbonItemSize.Small))
                    ? RibbonGroupLayoutMode.Expanded
                    : RibbonGroupLayoutMode.Compact;
            return new RibbonGroupLayout(
                Presentation,
                mode,
                items,
                IsOverflow ? 0d : Measure(metrics, scale));
        }

        private static double GetItemWidth(
            RibbonItemPresentation item,
            RibbonItemSize size,
            RibbonLayoutMetrics metrics)
        {
            var defaultWidth = size switch
            {
                RibbonItemSize.Large => metrics.LargeItemWidth,
                RibbonItemSize.Small => metrics.SmallItemWidth,
                RibbonItemSize.Compact => metrics.CompactItemWidth,
                _ => throw new ArgumentOutOfRangeException(nameof(size)),
            };
            if (item.Definition.Measurement is not { } measurement)
            {
                return defaultWidth;
            }

            var width = measurement(new RibbonItemMeasurementContext(
                item.Kind,
                size,
                item.Command,
                defaultWidth));
            if (!double.IsFinite(width) || width < 0d)
            {
                throw new InvalidOperationException(
                    $"Ribbon item measurement for '{item.Command.CommandId}' must be finite and non-negative.");
            }
            return width;
        }
    }
}
