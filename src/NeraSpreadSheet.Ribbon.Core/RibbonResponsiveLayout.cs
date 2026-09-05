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
    public double LargeItemWidth { get; init; } = 64d;

    /// <summary>Gets the logical width of a small item.</summary>
    public double SmallItemWidth { get; init; } = 64d;

    /// <summary>Gets the logical width of a compact item.</summary>
    public double CompactItemWidth { get; init; } = 28d;

    /// <summary>Gets the logical horizontal chrome width of each inline group.</summary>
    public double GroupChromeWidth { get; init; } = 8d;

    /// <summary>Gets the logical gap between commands and groups.</summary>
    public double Spacing { get; init; } = 2d;

    /// <summary>Gets the logical width reserved for the shared overflow affordance.</summary>
    public double OverflowWidth { get; init; } = 60d;

    /// <summary>Gets the number of command rows, between one and three.</summary>
    public int RowCount { get; init; } = 3;

    /// <summary>Gets one logical command row's height.</summary>
    public double RowHeight { get; init; } = 24d;

    /// <summary>Gets the logical vertical gap between command rows.</summary>
    public double RowSpacing { get; init; } = 2d;

    /// <summary>Gets logical padding inside each group.</summary>
    public double GroupPadding { get; init; } = 4d;

    /// <summary>Gets the logical height reserved below commands for the group caption.</summary>
    public double GroupCaptionHeight { get; init; } = 18d;
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
        ValidateMetrics(Metrics, scale);
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

    /// <summary>
    /// Gets an optional native icon-availability probe. A missing icon keeps its
    /// caption and measured caption width even in compact mode. If omitted,
    /// non-empty semantic icon keys are assumed to resolve.
    /// </summary>
    public Func<string, bool>? IsIconAvailable { get; init; }

    private static void ValidateMetrics(RibbonLayoutMetrics metrics, double scale)
    {
        double[] values =
        [
            metrics.LargeItemWidth,
            metrics.SmallItemWidth,
            metrics.CompactItemWidth,
            metrics.GroupChromeWidth,
            metrics.Spacing,
            metrics.OverflowWidth,
            metrics.RowHeight,
            metrics.RowSpacing,
            metrics.GroupPadding,
            metrics.GroupCaptionHeight,
        ];
        if (values.Any(value => !double.IsFinite(value) || value < 0d || !double.IsFinite(value * scale)))
        {
            throw new ArgumentOutOfRangeException(
                nameof(metrics),
                "Ribbon layout metrics must be finite and non-negative.");
        }
        if (metrics.RowCount is < 1 or > 3 || metrics.RowHeight <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(metrics),
                "Ribbon layout requires one to three positive-height command rows.");
        }
        var groupHeight = 2d * metrics.GroupPadding + metrics.RowCount * metrics.RowHeight +
            (metrics.RowCount - 1) * metrics.RowSpacing + metrics.GroupCaptionHeight;
        if (!double.IsFinite(groupHeight * scale))
        {
            throw new ArgumentOutOfRangeException(nameof(metrics),
                "Ribbon group geometry must remain finite after applying scale.");
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
    double Width)
{
    /// <summary>Gets the physical left coordinate relative to the group's bounds.</summary>
    public double X { get; init; }

    /// <summary>Gets the physical top coordinate relative to the group's bounds.</summary>
    public double Y { get; init; }

    /// <summary>Gets the occupied physical height.</summary>
    public double Height { get; init; }

    public int Row { get; init; }

    public int RowSpan { get; init; } = 1;

    public int Column { get; init; }

    /// <summary>Gets whether the inline caption must remain visible.</summary>
    public bool CaptionVisible { get; init; } = true;

    /// <summary>Gets the maximum inline caption line count; large items use two.</summary>
    public int CaptionMaxLines { get; init; } = 1;
}

/// <summary>Immutable responsive layout for one Ribbon group.</summary>
/// <param name="Presentation">Group presentation being placed.</param>
/// <param name="Mode">Responsive mode selected for the group.</param>
/// <param name="Items">Immutable item layouts in definition order.</param>
/// <param name="Width">Inline occupied width in physical pixels, or zero for overflow.</param>
public sealed record RibbonGroupLayout(
    RibbonGroupPresentation Presentation,
    RibbonGroupLayoutMode Mode,
    IReadOnlyList<RibbonItemLayout> Items,
    double Width)
{
    /// <summary>Gets the physical group height, including its bottom caption.</summary>
    public double Height { get; init; }

    /// <summary>Gets the caption's physical top coordinate relative to the group.</summary>
    public double CaptionY { get; init; }

    public double CaptionHeight { get; init; }
}

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
            Array.AsReadOnly(tabs),
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
            group, index, request)).ToArray();
        var collapseOrder = states
            .OrderBy(state => state.Presentation.CollapsePriority)
            .ThenByDescending(static state => state.Index)
            .ToArray();

        // Exhaust inline reductions before hiding a group. A small caption can
        // be wider than a two-line large caption, so accept only non-growing steps.
        for (var phase = 0; phase < 3; phase++)
        {
            foreach (var state in collapseOrder)
            {
                if (Measure(states, metrics, scale) <= request.AvailableWidth)
                {
                    break;
                }
                if (phase == 2)
                {
                    state.IsOverflow = true;
                }
                else
                {
                    state.Reduce(phase == 0 ? RibbonItemSize.Small : RibbonItemSize.Compact);
                }
            }
        }

        var groups = states.Select(state => state.ToLayout()).ToArray();
        return new RibbonTabLayout(
            tab,
            Array.AsReadOnly(groups),
            Measure(states, metrics, scale),
            states.Any(static state => state.IsOverflow));
    }

    private static double Measure(
        IReadOnlyList<GroupState> states,
        RibbonLayoutMetrics metrics,
        double scale)
    {
        var width = 0d;
        var inlineCount = 0;
        var hasOverflow = false;
        foreach (var state in states)
        {
            if (state.IsOverflow)
            {
                hasOverflow = true;
                continue;
            }
            width += state.Width * scale;
            inlineCount++;
        }
        if (inlineCount > 1)
        {
            width += (inlineCount - 1) * metrics.Spacing * scale;
        }
        if (hasOverflow)
        {
            width += metrics.OverflowWidth * scale;
            if (inlineCount > 0)
            {
                width += metrics.Spacing * scale;
            }
        }
        return width;
    }

    private sealed class GroupState
    {
        private readonly Dictionary<(int Index, RibbonItemSize Size), double> _measuredWidths = [];
        private readonly RibbonLayoutRequest _request;
        private readonly bool[] _hasIcons;
        private Packing _packing;

        public GroupState(
            RibbonGroupPresentation presentation,
            int index,
            RibbonLayoutRequest request)
        {
            Presentation = presentation;
            Index = index;
            _request = request;
            Sizes = presentation.Items.Select(static item => item.IsLarge
                ? RibbonItemSize.Large
                : RibbonItemSize.Small).ToArray();
            _hasIcons = presentation.Items.Select(item =>
                !string.IsNullOrWhiteSpace(item.Command.IconKey) &&
                (request.IsIconAvailable?.Invoke(item.Command.IconKey) ?? true)).ToArray();
            _packing = Pack(Sizes);
        }

        public RibbonGroupPresentation Presentation { get; }

        public int Index { get; }

        public RibbonItemSize[] Sizes { get; private set; }

        public double Width => _packing.Width;

        public bool IsOverflow { get; set; }

        public void Reduce(RibbonItemSize targetSize)
        {
            var reduced = (RibbonItemSize[])Sizes.Clone();
            var changed = false;
            for (var index = 0; index < Sizes.Length; index++)
            {
                if (Sizes[index] < targetSize)
                {
                    reduced[index] = targetSize;
                    changed = true;
                }
            }
            if (!changed)
            {
                return;
            }
            var candidate = Pack(reduced);
            if (candidate.Width <= _packing.Width)
            {
                Sizes = reduced;
                _packing = candidate;
            }
        }

        public RibbonGroupLayout ToLayout()
        {
            var metrics = _request.Metrics;
            var scale = _request.Scale;
            var contentHeight = metrics.RowCount * metrics.RowHeight +
                (metrics.RowCount - 1) * metrics.RowSpacing;
            var items = Presentation.Items.Select((item, index) =>
                new RibbonItemLayout(
                    item,
                    Sizes[index],
                    _packing.Placements[index].Width * scale)
                {
                    X = _packing.Placements[index].X * scale,
                    Y = (metrics.GroupPadding + _packing.Placements[index].Row *
                        (metrics.RowHeight + metrics.RowSpacing)) * scale,
                    Height = (_packing.Placements[index].RowSpan == metrics.RowCount
                        ? contentHeight
                        : metrics.RowHeight) * scale,
                    Row = _packing.Placements[index].Row,
                    RowSpan = _packing.Placements[index].RowSpan,
                    Column = _packing.Placements[index].Column,
                    CaptionVisible = HasCaption(index, Sizes[index]),
                    CaptionMaxLines = Sizes[index] == RibbonItemSize.Large ? 2 : 1,
                }).ToArray();
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
                Array.AsReadOnly(items),
                IsOverflow ? 0d : _packing.Width * scale)
            {
                Height = (2d * metrics.GroupPadding + contentHeight + metrics.GroupCaptionHeight) * scale,
                CaptionY = (metrics.GroupPadding + contentHeight) * scale,
                CaptionHeight = metrics.GroupCaptionHeight * scale,
            };
        }

        private Packing Pack(RibbonItemSize[] sizes)
        {
            var metrics = _request.Metrics;
            var placements = new Placement[sizes.Length];
            var column = 0;
            var row = 0;
            var columnStart = 0;
            var columnWidth = 0d;
            var x = metrics.GroupPadding;
            for (var index = 0; index < sizes.Length; index++)
            {
                var span = sizes[index] == RibbonItemSize.Large ||
                    Presentation.Items[index].Kind is RibbonItemKind.Gallery or RibbonItemKind.Separator
                        ? metrics.RowCount : 1;
                if (row > 0 && row + span > metrics.RowCount)
                {
                    FinishColumn(index);
                }
                var width = GetItemWidth(index, sizes[index]);
                placements[index] = new Placement(x, width, row, span, column);
                columnWidth = Math.Max(columnWidth, width);
                row += span;
                if (row == metrics.RowCount)
                {
                    FinishColumn(index + 1);
                }
            }
            if (row > 0)
            {
                FinishColumn(sizes.Length);
            }
            var commandWidth = sizes.Length == 0 ? 0d : x - metrics.GroupPadding - metrics.Spacing;
            var chromeWidth = Math.Max(metrics.GroupChromeWidth, 2d * metrics.GroupPadding);
            var widthWithCaption = Math.Max(commandWidth, MeasureCaption(Presentation.Caption));
            var groupWidth = widthWithCaption + chromeWidth;
            if (!double.IsFinite(groupWidth * _request.Scale))
            {
                throw new InvalidOperationException("Ribbon group geometry must remain finite after applying scale.");
            }
            return new Packing(placements, groupWidth);

            void FinishColumn(int end)
            {
                for (var itemIndex = columnStart; itemIndex < end; itemIndex++)
                {
                    placements[itemIndex] = placements[itemIndex] with { Width = columnWidth };
                }
                x += columnWidth + metrics.Spacing;
                row = 0;
                column++;
                columnStart = end;
                columnWidth = 0d;
            }
        }

        private bool HasCaption(int index, RibbonItemSize size) =>
            Presentation.Items[index].Kind != RibbonItemKind.Separator &&
            (size != RibbonItemSize.Compact || !_hasIcons[index] ||
                Presentation.Items[index].Kind is RibbonItemKind.ComboBox or RibbonItemKind.ColorPicker or RibbonItemKind.Gallery);

        private double GetItemWidth(int index, RibbonItemSize size)
        {
            var key = (index, size);
            if (_measuredWidths.TryGetValue(key, out var measuredWidth))
            {
                return measuredWidth;
            }

            var metrics = _request.Metrics;
            var item = Presentation.Items[index];
            var defaultWidth = size switch
            {
                RibbonItemSize.Large => metrics.LargeItemWidth,
                RibbonItemSize.Small => metrics.SmallItemWidth,
                RibbonItemSize.Compact => metrics.CompactItemWidth,
                _ => throw new ArgumentOutOfRangeException(nameof(size)),
            };
            var arrowWidth = item.Kind is RibbonItemKind.SplitButton or RibbonItemKind.DropDown or
                RibbonItemKind.Menu or RibbonItemKind.ComboBox or RibbonItemKind.ColorPicker ? 18d : 0d;
            defaultWidth += arrowWidth;
            if (item.Kind == RibbonItemKind.Gallery)
            {
                defaultWidth = size switch
                {
                    RibbonItemSize.Large => Math.Max(defaultWidth, 224d),
                    RibbonItemSize.Small => Math.Max(defaultWidth, 180d),
                    RibbonItemSize.Compact => Math.Max(defaultWidth, 120d),
                    _ => defaultWidth,
                };
            }
            else if (item.Kind != RibbonItemKind.Separator && HasCaption(index, size))
            {
                var caption = item.Command.Caption;
                if (item.Kind is RibbonItemKind.ComboBox or RibbonItemKind.ColorPicker)
                {
                    caption = item.Command.SelectableItems.FirstOrDefault(option =>
                        string.Equals(option.Value, item.Command.SelectedValue, StringComparison.Ordinal))?.Caption
                        ?? caption;
                }
                var textWidth = MeasureCaption(caption);
                var captionWidth = size == RibbonItemSize.Large
                    ? MeasureWrappedCaption(caption) + 12d + arrowWidth
                    : textWidth + 12d + (_hasIcons[index] ? 20d : 0d) + arrowWidth;
                defaultWidth = Math.Max(defaultWidth, captionWidth);
            }
            // Explicit item measurements remain authoritative. Consumers that
            // customize them own their typography/overflow minimums.
            var width = item.Definition.Measurement is { } measurement
                ? measurement(new RibbonItemMeasurementContext(item.Kind, size, item.Command, defaultWidth))
                : defaultWidth;
            if (!double.IsFinite(width) || width < 0d || !double.IsFinite(width * _request.Scale))
            {
                throw new InvalidOperationException(
                    $"Ribbon item measurement for '{item.Command.CommandId}' must be finite and non-negative after applying scale.");
            }
            _measuredWidths.Add(key, width);
            return width;
        }

        private readonly record struct Placement(double X, double Width, int Row, int RowSpan, int Column);

        private sealed record Packing(Placement[] Placements, double Width);
    }

    private static double MeasureWrappedCaption(string caption)
    {
        var width = MeasureCaption(caption);
        var best = width;
        var prefix = 0d;
        foreach (var rune in caption.EnumerateRunes())
        {
            var advance = GetCaptionAdvance(rune);
            if (System.Text.Rune.IsWhiteSpace(rune) && prefix > 0d && prefix + advance < width)
            {
                best = Math.Min(best, Math.Max(prefix, width - prefix - advance));
            }
            prefix += advance;
        }
        return best;
    }

    private static double MeasureCaption(string caption)
    {
        // Conservative 12px UI-font advance. Combining accents do not create
        // extra width, so Vietnamese NFC and decomposed text pack identically.
        var width = 0d;
        foreach (var rune in caption.EnumerateRunes())
        {
            width += GetCaptionAdvance(rune);
        }
        return width;
    }

    private static double GetCaptionAdvance(System.Text.Rune rune)
    {
        var category = System.Text.Rune.GetUnicodeCategory(rune);
        return category is System.Globalization.UnicodeCategory.NonSpacingMark or
            System.Globalization.UnicodeCategory.EnclosingMark ? 0d :
            System.Text.Rune.IsWhiteSpace(rune) ? 4d :
            rune.Value >= 0x2e80 ? 13d : 7.5d;
    }
}
