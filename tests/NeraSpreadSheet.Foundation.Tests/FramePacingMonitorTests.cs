using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeraSpreadSheet.Foundation.Performance;

namespace NeraSpreadSheet.Foundation.Tests;

[TestClass]
public sealed class FramePacingMonitorTests
{
    [TestMethod]
    public void CaptureReportsAverageP95AndFps()
    {
        var monitor = new FramePacingMonitor(16);
        var timestamp = Stopwatch.Frequency;
        monitor.RecordFrame(timestamp);
        timestamp += MillisecondsToTicks(10d);
        monitor.RecordFrame(timestamp);
        timestamp += MillisecondsToTicks(20d);
        monitor.RecordFrame(timestamp);
        timestamp += MillisecondsToTicks(30d);
        monitor.RecordFrame(timestamp);

        var snapshot = monitor.Capture();

        Assert.AreEqual(4L, snapshot.TotalFrames);
        Assert.AreEqual(3, snapshot.Samples);
        Assert.AreEqual(20d, snapshot.AverageFrameIntervalMilliseconds, 0.1d);
        Assert.AreEqual(30d, snapshot.P95FrameIntervalMilliseconds, 0.1d);
        Assert.AreEqual(30d, snapshot.MaximumFrameIntervalMilliseconds, 0.1d);
        Assert.AreEqual(50d, snapshot.FramesPerSecond, 0.5d);
    }

    [TestMethod]
    public void RollingWindowKeepsNewestIntervalsOnly()
    {
        var monitor = new FramePacingMonitor(2);
        var timestamp = Stopwatch.Frequency;
        monitor.RecordFrame(timestamp);
        timestamp += MillisecondsToTicks(100d);
        monitor.RecordFrame(timestamp);
        timestamp += MillisecondsToTicks(20d);
        monitor.RecordFrame(timestamp);
        timestamp += MillisecondsToTicks(10d);
        monitor.RecordFrame(timestamp);

        var snapshot = monitor.Capture();

        Assert.AreEqual(2, snapshot.Samples);
        Assert.AreEqual(15d, snapshot.AverageFrameIntervalMilliseconds, 0.1d);
        Assert.AreEqual(20d, snapshot.MaximumFrameIntervalMilliseconds, 0.1d);
    }

    [TestMethod]
    public void ResetClearsSamplesAndFrameCount()
    {
        var monitor = new FramePacingMonitor();
        monitor.RecordFrame(Stopwatch.Frequency);
        monitor.RecordFrame(Stopwatch.Frequency + MillisecondsToTicks(16d));

        monitor.Reset();
        var snapshot = monitor.Capture();

        Assert.AreEqual(0L, snapshot.TotalFrames);
        Assert.AreEqual(0, snapshot.Samples);
        Assert.AreEqual(0d, snapshot.FramesPerSecond);
    }

    private static long MillisecondsToTicks(double milliseconds) =>
        (long)Math.Round((milliseconds / 1000d) * Stopwatch.Frequency, MidpointRounding.AwayFromZero);
}
