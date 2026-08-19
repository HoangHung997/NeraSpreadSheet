namespace NeraSpreadSheet.Maui;

/// <summary>
/// Immutable diagnostics for the native Skia GPU context owned by a
/// <see cref="NeraSpreadsheetView"/>.
/// </summary>
public readonly record struct NeraGpuContextDiagnostics(
    long ContextGeneration,
    long ContextCreatedCount,
    long ContextLostCount,
    long ContextRecreatedCount,
    long FramesStarted,
    long FramesCompleted,
    long FramesFailed,
    long FramesAbandoned,
    long StaleFrameTransitionsRejected,
    bool HasActiveContext,
    bool HasActiveFrame,
    bool IsDisposed);

internal readonly record struct NeraGpuFrameToken(
    long ContextGeneration,
    long FrameSequence)
{
    public bool IsValid => ContextGeneration > 0L && FrameSequence > 0L;
}

/// <summary>
/// Serializes GPU-context and frame transitions for one MAUI spreadsheet view.
/// A frame lease is valid only for the context generation that created it.
/// </summary>
internal sealed class NeraGpuContextLifecycle : IDisposable
{
    private readonly object _sync = new();
    private object? _context;
    private NeraGpuFrameToken _activeFrame;
    private long _contextGeneration;
    private long _contextCreatedCount;
    private long _contextLostCount;
    private long _contextRecreatedCount;
    private long _nextFrameSequence;
    private long _framesStarted;
    private long _framesCompleted;
    private long _framesFailed;
    private long _framesAbandoned;
    private long _staleFrameTransitionsRejected;
    private bool _disposed;

    public NeraGpuContextDiagnostics Diagnostics
    {
        get
        {
            lock (_sync)
            {
                return new NeraGpuContextDiagnostics(
                    _contextGeneration,
                    _contextCreatedCount,
                    _contextLostCount,
                    _contextRecreatedCount,
                    _framesStarted,
                    _framesCompleted,
                    _framesFailed,
                    _framesAbandoned,
                    _staleFrameTransitionsRejected,
                    _context is not null,
                    _activeFrame.IsValid,
                    _disposed);
            }
        }
    }

    public NeraGpuFrameToken BeginFrame(object context)
    {
        ArgumentNullException.ThrowIfNull(context);

        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!ReferenceEquals(_context, context))
            {
                ReplaceContextCore(context);
            }
            if (_activeFrame.IsValid)
            {
                throw new InvalidOperationException(
                    "A Nera MAUI GPU frame is already active for this view.");
            }

            var token = new NeraGpuFrameToken(
                _contextGeneration,
                checked(++_nextFrameSequence));
            _activeFrame = token;
            _framesStarted++;
            return token;
        }
    }

    public bool TryCompleteFrame(NeraGpuFrameToken token) =>
        TryTransitionFrame(token, FrameTransition.Completed);

    public bool TryFailFrame(NeraGpuFrameToken token) =>
        TryTransitionFrame(token, FrameTransition.Failed);

    public bool TryAbandonFrame(NeraGpuFrameToken token) =>
        TryTransitionFrame(token, FrameTransition.Abandoned);

    public void NotifyContextLost(object? expectedContext = null)
    {
        lock (_sync)
        {
            if (_context is null)
            {
                return;
            }
            if (expectedContext is not null &&
                !ReferenceEquals(_context, expectedContext))
            {
                return;
            }

            LoseContextCore();
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            if (_context is not null)
            {
                LoseContextCore();
            }
            else if (_activeFrame.IsValid)
            {
                _activeFrame = default;
                _framesAbandoned++;
            }
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    private void ReplaceContextCore(object context)
    {
        if (_context is not null)
        {
            LoseContextCore();
        }

        _contextGeneration = checked(_contextGeneration + 1L);
        _contextCreatedCount++;
        if (_contextGeneration > 1L)
        {
            _contextRecreatedCount++;
        }
        _context = context;
    }

    private void LoseContextCore()
    {
        if (_activeFrame.IsValid)
        {
            _activeFrame = default;
            _framesAbandoned++;
        }
        _context = null;
        _contextLostCount++;
    }

    private bool TryTransitionFrame(
        NeraGpuFrameToken token,
        FrameTransition transition)
    {
        lock (_sync)
        {
            if (!token.IsValid || token != _activeFrame)
            {
                _staleFrameTransitionsRejected++;
                return false;
            }

            _activeFrame = default;
            switch (transition)
            {
                case FrameTransition.Completed:
                    _framesCompleted++;
                    break;
                case FrameTransition.Failed:
                    _framesFailed++;
                    break;
                case FrameTransition.Abandoned:
                    _framesAbandoned++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(transition),
                        transition,
                        "Unknown Nera GPU frame transition.");
            }
            return true;
        }
    }

    private enum FrameTransition
    {
        Completed,
        Failed,
        Abandoned,
    }
}
