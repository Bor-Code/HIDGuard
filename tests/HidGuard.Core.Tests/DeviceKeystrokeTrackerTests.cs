using Shouldly;
using Xunit;
using HidGuard.Core.Domain;
using HidGuard.Core.Timing;

namespace HidGuard.Core.Tests;

public sealed class DeviceKeystrokeTrackerTests
{
    private static readonly DeviceIdentifier TestDevice = new("TEST_PATH", 0x1234, 0x5678);

    [Fact]
    public void FirstKeyStrokeShouldNotGenerateIntervals()
    {
        var tracker = new DeviceKeystrokeTracker(TestDevice, new TimingThresholds());
        var stats = tracker.RecordKeyDown(DateTimeOffset.UtcNow);

        stats.SampleCount.ShouldBe(0);
        stats.BurstLength.ShouldBe(1);
        tracker.TotalKeyCount.ShouldBe(1);
    }

    [Fact]
    public void SequentialKeystrokesShouldCalculateCorrectIntervals()
    {
        var thresholds = new TimingThresholds { WindowSize = 10 };
        var tracker = new DeviceKeystrokeTracker(TestDevice, thresholds);
        var baseTime = DateTimeOffset.UtcNow;

        tracker.RecordKeyDown(baseTime);
        tracker.RecordKeyDown(baseTime.AddMilliseconds(50));
        var stats = tracker.RecordKeyDown(baseTime.AddMilliseconds(100));

        stats.SampleCount.ShouldBe(2);
        stats.MeanIntervalMs.ShouldBe(50.0);
        stats.MedianIntervalMs.ShouldBe(50.0);
        stats.MinimumIntervalMs.ShouldBe(50.0);
        stats.VarianceMs.ShouldBe(0.0);
        stats.StandardDeviationMs.ShouldBe(0.0);
        stats.CoefficientOfVariation.ShouldBe(0.0);
        stats.SubHumanIntervalRatio.ShouldBe(0.0);
        stats.BurstLength.ShouldBe(3);
    }

    [Fact]
    public void MedianShouldCalculateCorrectlyForEvenAndOddSamples()
    {
        var thresholds = new TimingThresholds { WindowSize = 10 };
        var tracker = new DeviceKeystrokeTracker(TestDevice, thresholds);
        var baseTime = DateTimeOffset.UtcNow;

        tracker.RecordKeyDown(baseTime);
        tracker.RecordKeyDown(baseTime.AddMilliseconds(10));
        tracker.RecordKeyDown(baseTime.AddMilliseconds(30));
        var oddStats = tracker.RecordKeyDown(baseTime.AddMilliseconds(70));

        oddStats.SampleCount.ShouldBe(3);
        oddStats.MedianIntervalMs.ShouldBe(20.0);

        var evenStats = tracker.RecordKeyDown(baseTime.AddMilliseconds(120));

        evenStats.SampleCount.ShouldBe(4);
        evenStats.MedianIntervalMs.ShouldBe(30.0);
    }

    [Fact]
    public void BurstLengthShouldResetWhenIntervalExceedsPauseThreshold()
    {
        var thresholds = new TimingThresholds { BurstPauseFloorMs = 70.0 };
        var tracker = new DeviceKeystrokeTracker(TestDevice, thresholds);
        var baseTime = DateTimeOffset.UtcNow;

        tracker.RecordKeyDown(baseTime);
        tracker.RecordKeyDown(baseTime.AddMilliseconds(50));
        tracker.RecordKeyDown(baseTime.AddMilliseconds(100));

        var stats1 = tracker.GetCurrentStatistics();
        stats1.BurstLength.ShouldBe(3);

        tracker.RecordKeyDown(baseTime.AddMilliseconds(250));

        var stats2 = tracker.GetCurrentStatistics();
        stats2.BurstLength.ShouldBe(1);
    }

    [Fact]
    public void SlidingWindowShouldEvictOldestSamplesWhenCapacityExceeded()
    {
        var thresholds = new TimingThresholds { WindowSize = 5 };
        var tracker = new DeviceKeystrokeTracker(TestDevice, thresholds);
        var baseTime = DateTimeOffset.UtcNow;

        tracker.RecordKeyDown(baseTime);
        tracker.RecordKeyDown(baseTime.AddMilliseconds(10));
        tracker.RecordKeyDown(baseTime.AddMilliseconds(20));
        tracker.RecordKeyDown(baseTime.AddMilliseconds(30));
        tracker.RecordKeyDown(baseTime.AddMilliseconds(40));
        tracker.RecordKeyDown(baseTime.AddMilliseconds(50));
        var stats = tracker.RecordKeyDown(baseTime.AddMilliseconds(100));

        stats.SampleCount.ShouldBe(5);
        stats.MinimumIntervalMs.ShouldBe(10.0);
        stats.MeanIntervalMs.ShouldBe(18.0);
    }

    [Fact]
    public void NegativeTimestampDeltaShouldBeIgnoredGracefully()
    {
        var tracker = new DeviceKeystrokeTracker(TestDevice, new TimingThresholds());
        var baseTime = DateTimeOffset.UtcNow;

        tracker.RecordKeyDown(baseTime);
        tracker.RecordKeyDown(baseTime.AddMilliseconds(50));
        var stats = tracker.RecordKeyDown(baseTime.AddMilliseconds(30));

        stats.SampleCount.ShouldBe(1);
        stats.MeanIntervalMs.ShouldBe(50.0);
    }

    [Fact]
    public void ResetShouldRestoreCleanInitialState()
    {
        var tracker = new DeviceKeystrokeTracker(TestDevice, new TimingThresholds());
        var baseTime = DateTimeOffset.UtcNow;

        tracker.RecordKeyDown(baseTime);
        tracker.RecordKeyDown(baseTime.AddMilliseconds(50));
        tracker.RecordKeyDown(baseTime.AddMilliseconds(100));

        tracker.Reset();

        var stats = tracker.GetCurrentStatistics();
        stats.SampleCount.ShouldBe(0);
        stats.BurstLength.ShouldBe(0);
        tracker.TotalKeyCount.ShouldBe(0);
    }

    [Fact]
    public void IsInObservationWindowShouldReflectKeyCountThreshold()
    {
        var thresholds = new TimingThresholds { ObservationWindowKeyCount = 20 };
        var tracker = new DeviceKeystrokeTracker(TestDevice, thresholds);
        var baseTime = DateTimeOffset.UtcNow;

        for (var i = 0; i < 20; i++)
        {
            tracker.RecordKeyDown(baseTime.AddMilliseconds(i * 100));
            tracker.IsInObservationWindow.ShouldBeTrue();
        }

        tracker.RecordKeyDown(baseTime.AddMilliseconds(2100));
        tracker.IsInObservationWindow.ShouldBeFalse();
    }
}
