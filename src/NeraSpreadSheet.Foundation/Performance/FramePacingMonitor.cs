using System.Diagnostics;

namespace NeraSpreadSheet.Foundation.Performance;

public readonly record struct FramePacingSnapshot(
    long TotalFrames,
    double FramesPerSecond,
    double AverageFrameIntervalMilliseconds,
    double P95FrameIntervalMilliseconds,
    double MaximumFrameIntervalMilliseconds,
    int Samples);

/// <summary>
/// Rolling frame-interval monitor intended for UI diagnostics, not application timing.
/// </summary>
public sealed class FramePacingMonitor
{
    public const int DefaultSampleCapacity = 240;

    private readonly double[] _samples;
    private int _nextSample;
    private int _sampleCount;
    private long _lastTimestamp;

    public FramePacingMonitor(int sampleCapacity = DefaultSampleCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleCapacity);
        _samples = new double[sampleCapacity];
    }

    public long TotalFrames { get; private set; }

    public void Reset()
    {
        Array.Clear(_samples);
        _nextSample = 0;
        _sampleCount = 0;
        _lastTimestamp = 0;
        TotalFrames = 0;
    }

    public void RecordFrame() => RecordFrame(Stopwatch.GetTimestamp());

    public void RecordFrame(long timestamp)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timestamp);

        TotalFrames++;
        if (_lastTimestamp != 0 && timestamp > _lastTimestamp)
        {
            var milliseconds = Stopwatch.GetElapsedTime(_lastTimestamp, timestamp).TotalMilliseconds;
            if (double.IsFinite(milliseconds) && milliseconds > 0d)
            {
                _samples[_nextSample] = milliseconds;
                _nextSample = (_nextSample + 1) % _samples.Length;
                _sampleCount = Math.Min(_sampleCount + 1, _samples.Length);
            }
        }
        _lastTimestamp = timestamp;
    }

    public FramePacingSnapshot Capture()
    {
        if (_sampleCount == 0)
        {
            return new FramePacingSnapshot(TotalFrames, 0d, 0d, 0d, 0d, 0);
        }

        var values = new double[_sampleCount];
        for (var index = 0; index < _sampleCount; index++)
        {
            var source = (_nextSample - _sampleCount + index + _samples.Length) % _samples.Length;
            values[index] = _samples[source];
        }
        Array.Sort(values);

        var sum = 0d;
        var maximum = 0d;
        foreach (var value in values)
        {
            sum += value;
            maximum = Math.Max(maximum, value);
        }
        var average = sum / values.Length;
        var p95Index = Math.Clamp((int)Math.Ceiling(values.Length * 0.95d) - 1, 0, values.Length - 1);
        var fps = average > 0d ? 1000d / average : 0d;
        return new FramePacingSnapshot(
            TotalFrames,
            fps,
            average,
            values[p95Index],
            maximum,
            values.Length);
    }
}
