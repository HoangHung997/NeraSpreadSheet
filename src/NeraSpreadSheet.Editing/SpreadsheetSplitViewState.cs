namespace NeraSpreadSheet.Editing;

public enum SpreadsheetSplitViewMode
{
    None,
    Vertical,
    Horizontal,
    Both,
}

public enum SpreadsheetSplitViewPane
{
    TopLeft,
    TopRight,
    BottomLeft,
    BottomRight,
}

public readonly record struct SpreadsheetPaneScrollOffset
{
    public SpreadsheetPaneScrollOffset(double offsetX, double offsetY)
    {
        if (!double.IsFinite(offsetX))
        {
            throw new ArgumentOutOfRangeException(
                nameof(offsetX),
                offsetX,
                "Pane scroll offsets must be finite.");
        }
        if (!double.IsFinite(offsetY))
        {
            throw new ArgumentOutOfRangeException(
                nameof(offsetY),
                offsetY,
                "Pane scroll offsets must be finite.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(offsetX);
        ArgumentOutOfRangeException.ThrowIfNegative(offsetY);
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    public double OffsetX { get; }

    public double OffsetY { get; }
}

public readonly record struct SpreadsheetSplitViewState
{
    public SpreadsheetSplitViewState(
        SpreadsheetSplitViewMode mode,
        double? splitX,
        double? splitY,
        SpreadsheetSplitViewPane activePane = SpreadsheetSplitViewPane.TopLeft,
        SpreadsheetPaneScrollOffset topLeftScroll = default,
        SpreadsheetPaneScrollOffset topRightScroll = default,
        SpreadsheetPaneScrollOffset bottomLeftScroll = default,
        SpreadsheetPaneScrollOffset bottomRightScroll = default)
    {
        ValidateMode(mode);
        ValidateSplitCoordinates(mode, splitX, splitY);
        ValidatePane(activePane);

        Mode = mode;
        SplitX = splitX;
        SplitY = splitY;
        ActivePane = IsPaneVisible(mode, activePane)
            ? activePane
            : SpreadsheetSplitViewPane.TopLeft;
        TopLeftScroll = topLeftScroll;
        TopRightScroll = topRightScroll;
        BottomLeftScroll = bottomLeftScroll;
        BottomRightScroll = bottomRightScroll;
    }

    public SpreadsheetSplitViewMode Mode { get; }

    public double? SplitX { get; }

    public double? SplitY { get; }

    public SpreadsheetSplitViewPane ActivePane { get; }

    public SpreadsheetPaneScrollOffset TopLeftScroll { get; }

    public SpreadsheetPaneScrollOffset TopRightScroll { get; }

    public SpreadsheetPaneScrollOffset BottomLeftScroll { get; }

    public SpreadsheetPaneScrollOffset BottomRightScroll { get; }

    public bool HasSplitPanes => Mode != SpreadsheetSplitViewMode.None;

    public bool IsPaneVisible(SpreadsheetSplitViewPane pane) => IsPaneVisible(Mode, pane);

    public SpreadsheetPaneScrollOffset GetPaneScroll(SpreadsheetSplitViewPane pane)
    {
        ValidatePane(pane);
        return pane switch
        {
            SpreadsheetSplitViewPane.TopLeft => TopLeftScroll,
            SpreadsheetSplitViewPane.TopRight => TopRightScroll,
            SpreadsheetSplitViewPane.BottomLeft => BottomLeftScroll,
            SpreadsheetSplitViewPane.BottomRight => BottomRightScroll,
            _ => default,
        };
    }

    public SpreadsheetSplitViewState WithTopology(
        SpreadsheetSplitViewMode mode,
        double? splitX,
        double? splitY) =>
        new(
            mode,
            splitX,
            splitY,
            ActivePane,
            TopLeftScroll,
            TopRightScroll,
            BottomLeftScroll,
            BottomRightScroll);

    public SpreadsheetSplitViewState WithActivePane(SpreadsheetSplitViewPane activePane) =>
        new(
            Mode,
            SplitX,
            SplitY,
            activePane,
            TopLeftScroll,
            TopRightScroll,
            BottomLeftScroll,
            BottomRightScroll);

    public SpreadsheetSplitViewState WithPaneScroll(
        SpreadsheetSplitViewPane pane,
        double offsetX,
        double offsetY)
    {
        ValidatePane(pane);
        var next = new SpreadsheetPaneScrollOffset(offsetX, offsetY);
        return pane switch
        {
            SpreadsheetSplitViewPane.TopLeft => new(
                Mode,
                SplitX,
                SplitY,
                ActivePane,
                next,
                TopRightScroll,
                BottomLeftScroll,
                BottomRightScroll),
            SpreadsheetSplitViewPane.TopRight => new(
                Mode,
                SplitX,
                SplitY,
                ActivePane,
                TopLeftScroll,
                next,
                BottomLeftScroll,
                BottomRightScroll),
            SpreadsheetSplitViewPane.BottomLeft => new(
                Mode,
                SplitX,
                SplitY,
                ActivePane,
                TopLeftScroll,
                TopRightScroll,
                next,
                BottomRightScroll),
            SpreadsheetSplitViewPane.BottomRight => new(
                Mode,
                SplitX,
                SplitY,
                ActivePane,
                TopLeftScroll,
                TopRightScroll,
                BottomLeftScroll,
                next),
            _ => this,
        };
    }

    public static bool IsPaneVisible(
        SpreadsheetSplitViewMode mode,
        SpreadsheetSplitViewPane pane)
    {
        ValidateMode(mode);
        ValidatePane(pane);
        return mode switch
        {
            SpreadsheetSplitViewMode.None => pane == SpreadsheetSplitViewPane.TopLeft,
            SpreadsheetSplitViewMode.Vertical =>
                pane is SpreadsheetSplitViewPane.TopLeft or SpreadsheetSplitViewPane.TopRight,
            SpreadsheetSplitViewMode.Horizontal =>
                pane is SpreadsheetSplitViewPane.TopLeft or SpreadsheetSplitViewPane.BottomLeft,
            SpreadsheetSplitViewMode.Both => true,
            _ => false,
        };
    }

    private static void ValidateSplitCoordinates(
        SpreadsheetSplitViewMode mode,
        double? splitX,
        double? splitY)
    {
        ValidateFinite(splitX, nameof(splitX));
        ValidateFinite(splitY, nameof(splitY));

        var valid = mode switch
        {
            SpreadsheetSplitViewMode.None => splitX is null && splitY is null,
            SpreadsheetSplitViewMode.Vertical => splitX is not null && splitY is null,
            SpreadsheetSplitViewMode.Horizontal => splitX is null && splitY is not null,
            SpreadsheetSplitViewMode.Both => splitX is not null && splitY is not null,
            _ => false,
        };
        if (!valid)
        {
            throw new ArgumentException(
                "Split coordinates must match the selected split topology.",
                nameof(mode));
        }
    }

    private static void ValidateFinite(double? value, string parameterName)
    {
        if (value is { } coordinate && !double.IsFinite(coordinate))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                coordinate,
                "Split coordinates must be finite.");
        }
    }

    private static void ValidateMode(SpreadsheetSplitViewMode mode)
    {
        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
    }

    private static void ValidatePane(SpreadsheetSplitViewPane pane)
    {
        if (!Enum.IsDefined(pane))
        {
            throw new ArgumentOutOfRangeException(nameof(pane));
        }
    }
}
