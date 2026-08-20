using SkiaSharp.Views.Maui;

namespace NeraSpreadSheet.Maui;

public enum NeraSurfaceOrientation
{
    Unknown,
    Portrait,
    Landscape,
    Square,
}

public enum NeraSurfaceWidthClass
{
    Unknown,
    Compact,
    Medium,
    Expanded,
}

/// <summary>
/// Describes the logical MAUI viewport, renderer canvas and raw backing pixels
/// observed after one successfully completed <see cref="NeraSpreadsheetView"/>
/// GPU frame.
/// </summary>
public readonly record struct NeraSurfaceMetrics
{
    public const double CompactWidthUpperBound = 600d;
    public const double MediumWidthUpperBound = 840d;

    private NeraSurfaceMetrics(
        long contextGeneration,
        long frameSequence,
        double viewportWidth,
        double viewportHeight,
        int canvasWidth,
        int canvasHeight,
        int rawPixelWidth,
        int rawPixelHeight,
        bool ignorePixelScaling)
    {
        ValidatePositive(contextGeneration, nameof(contextGeneration));
        ValidatePositive(frameSequence, nameof(frameSequence));
        ValidateFinitePositive(viewportWidth, nameof(viewportWidth));
        ValidateFinitePositive(viewportHeight, nameof(viewportHeight));
        ValidatePositive(canvasWidth, nameof(canvasWidth));
        ValidatePositive(canvasHeight, nameof(canvasHeight));
        ValidatePositive(rawPixelWidth, nameof(rawPixelWidth));
        ValidatePositive(rawPixelHeight, nameof(rawPixelHeight));

        ContextGeneration = contextGeneration;
        FrameSequence = frameSequence;
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        CanvasWidth = canvasWidth;
        CanvasHeight = canvasHeight;
        RawPixelWidth = rawPixelWidth;
        RawPixelHeight = rawPixelHeight;
        IgnorePixelScaling = ignorePixelScaling;
        CanvasUnitsPerViewportUnitX = canvasWidth / viewportWidth;
        CanvasUnitsPerViewportUnitY = canvasHeight / viewportHeight;
        RawPixelsPerViewportUnitX = rawPixelWidth / viewportWidth;
        RawPixelsPerViewportUnitY = rawPixelHeight / viewportHeight;
        RawPixelsPerCanvasUnitX = (double)rawPixelWidth / canvasWidth;
        RawPixelsPerCanvasUnitY = (double)rawPixelHeight / canvasHeight;
        Orientation = ClassifyOrientation(viewportWidth, viewportHeight);
        WidthClass = ClassifyWidth(viewportWidth);
    }

    public long ContextGeneration { get; }

    public long FrameSequence { get; }

    public double ViewportWidth { get; }

    public double ViewportHeight { get; }

    public int CanvasWidth { get; }

    public int CanvasHeight { get; }

    public int RawPixelWidth { get; }

    public int RawPixelHeight { get; }

    public bool IgnorePixelScaling { get; }

    public double CanvasUnitsPerViewportUnitX { get; }

    public double CanvasUnitsPerViewportUnitY { get; }

    public double RawPixelsPerViewportUnitX { get; }

    public double RawPixelsPerViewportUnitY { get; }

    public double RawPixelsPerCanvasUnitX { get; }

    public double RawPixelsPerCanvasUnitY { get; }

    public NeraSurfaceOrientation Orientation { get; }

    public NeraSurfaceWidthClass WidthClass { get; }

    public bool IsAvailable =>
        ContextGeneration > 0L &&
        FrameSequence > 0L &&
        ViewportWidth > 0d &&
        ViewportHeight > 0d &&
        CanvasWidth > 0 &&
        CanvasHeight > 0 &&
        RawPixelWidth > 0 &&
        RawPixelHeight > 0;

    /// <summary>
    /// Captures scale and size diagnostics from a completed production paint.
    /// Call this from the public <c>PaintSurface</c> event, after Nera has closed
    /// its GPU frame lease.
    /// </summary>
    public static NeraSurfaceMetrics Capture(
        NeraSpreadsheetView view,
        SKPaintGLSurfaceEventArgs frame)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(frame);

        var diagnostics = view.GpuContextDiagnostics;
        if (!diagnostics.HasActiveContext ||
            diagnostics.HasActiveFrame ||
            diagnostics.ContextGeneration <= 0L ||
            diagnostics.FramesStarted <= 0L)
        {
            throw new InvalidOperationException(
                "Surface metrics can only be captured after a completed Nera GPU frame.");
        }

        var viewportWidth = ResolveViewportDimension(
            view.Width,
            frame.Info.Width,
            nameof(view.Width));
        var viewportHeight = ResolveViewportDimension(
            view.Height,
            frame.Info.Height,
            nameof(view.Height));

        return Create(
            diagnostics.ContextGeneration,
            diagnostics.FramesStarted,
            viewportWidth,
            viewportHeight,
            frame.Info.Width,
            frame.Info.Height,
            frame.RawInfo.Width,
            frame.RawInfo.Height,
            view.IgnorePixelScaling);
    }

    public static NeraSurfaceOrientation ClassifyOrientation(
        double viewportWidth,
        double viewportHeight)
    {
        ValidateFinitePositive(viewportWidth, nameof(viewportWidth));
        ValidateFinitePositive(viewportHeight, nameof(viewportHeight));

        if (Math.Abs(viewportWidth - viewportHeight) <= 0.5d)
        {
            return NeraSurfaceOrientation.Square;
        }

        return viewportWidth > viewportHeight
            ? NeraSurfaceOrientation.Landscape
            : NeraSurfaceOrientation.Portrait;
    }

    public static NeraSurfaceWidthClass ClassifyWidth(double viewportWidth)
    {
        ValidateFinitePositive(viewportWidth, nameof(viewportWidth));

        if (viewportWidth < CompactWidthUpperBound)
        {
            return NeraSurfaceWidthClass.Compact;
        }

        return viewportWidth < MediumWidthUpperBound
            ? NeraSurfaceWidthClass.Medium
            : NeraSurfaceWidthClass.Expanded;
    }

    public bool IsRawPixelScaleUniform(double tolerance = 0.02d)
    {
        ValidateTolerance(tolerance);
        return IsAvailable &&
            Math.Abs(
                RawPixelsPerViewportUnitX -
                RawPixelsPerViewportUnitY) <= tolerance;
    }

    public bool IsCanvasScaleUniform(double tolerance = 0.02d)
    {
        ValidateTolerance(tolerance);
        return IsAvailable &&
            Math.Abs(
                CanvasUnitsPerViewportUnitX -
                CanvasUnitsPerViewportUnitY) <= tolerance;
    }

    internal static NeraSurfaceMetrics Create(
        long contextGeneration,
        long frameSequence,
        double viewportWidth,
        double viewportHeight,
        int canvasWidth,
        int canvasHeight,
        int rawPixelWidth,
        int rawPixelHeight,
        bool ignorePixelScaling) =>
        new(
            contextGeneration,
            frameSequence,
            viewportWidth,
            viewportHeight,
            canvasWidth,
            canvasHeight,
            rawPixelWidth,
            rawPixelHeight,
            ignorePixelScaling);

    private static double ResolveViewportDimension(
        double viewportDimension,
        int fallbackCanvasDimension,
        string parameterName)
    {
        if (double.IsFinite(viewportDimension) && viewportDimension > 0d)
        {
            return viewportDimension;
        }

        ValidatePositive(fallbackCanvasDimension, parameterName);
        return fallbackCanvasDimension;
    }

    private static void ValidatePositive(long value, string parameterName)
    {
        if (value <= 0L)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be greater than zero.");
        }
    }

    private static void ValidatePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be greater than zero.");
        }
    }

    private static void ValidateFinitePositive(
        double value,
        string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0d)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Value must be finite and greater than zero.");
        }
    }

    private static void ValidateTolerance(double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tolerance),
                tolerance,
                "Tolerance must be finite and non-negative.");
        }
    }
}
