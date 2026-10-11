using Shouldly;
using Xunit;
using HidGuard.Core.Timing;

namespace HidGuard.Core.Tests;

public sealed class TimingThresholdsTests
{
    [Fact]
    public void DefaultThresholdsShouldPassValidation()
    {
        var thresholds = new TimingThresholds();

        Should.NotThrow(() => thresholds.Validate());
        thresholds.WindowSize.ShouldBe(30);
        thresholds.HumanFeasibilityFloorMs.ShouldBe(25.0);
        thresholds.SubHumanRatioThreshold.ShouldBe(0.35);
        thresholds.CoefficientOfVariationThreshold.ShouldBe(0.18);
        thresholds.MinimumIntervalThresholdMs.ShouldBe(8.0);
        thresholds.BurstLengthThreshold.ShouldBe(15);
        thresholds.BurstPauseFloorMs.ShouldBe(70.0);
        thresholds.ObservationWindowKeyCount.ShouldBe(60);
        thresholds.ObservationWindowScoreMultiplier.ShouldBe(1.25);
        thresholds.SuspicionScoreThreshold.ShouldBe(65.0);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(501)]
    public void ValidateShouldThrowWhenWindowSizeOutOfRange(int invalidWindowSize)
    {
        var thresholds = new TimingThresholds { WindowSize = invalidWindowSize };
        Should.Throw<ArgumentOutOfRangeException>(() => thresholds.Validate());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-10.0)]
    [InlineData(201.0)]
    public void ValidateShouldThrowWhenHumanFeasibilityFloorMsOutOfRange(double invalidFloor)
    {
        var thresholds = new TimingThresholds { HumanFeasibilityFloorMs = invalidFloor };
        Should.Throw<ArgumentOutOfRangeException>(() => thresholds.Validate());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.01)]
    public void ValidateShouldThrowWhenSubHumanRatioThresholdOutOfRange(double invalidRatio)
    {
        var thresholds = new TimingThresholds { SubHumanRatioThreshold = invalidRatio };
        Should.Throw<ArgumentOutOfRangeException>(() => thresholds.Validate());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-0.1)]
    [InlineData(1.01)]
    public void ValidateShouldThrowWhenCoefficientOfVariationThresholdOutOfRange(double invalidCv)
    {
        var thresholds = new TimingThresholds { CoefficientOfVariationThreshold = invalidCv };
        Should.Throw<ArgumentOutOfRangeException>(() => thresholds.Validate());
    }

    [Fact]
    public void ValidateShouldThrowWhenMinimumIntervalExceedsHumanFloor()
    {
        var thresholds = new TimingThresholds
        {
            HumanFeasibilityFloorMs = 25.0,
            MinimumIntervalThresholdMs = 30.0
        };
        Should.Throw<ArgumentOutOfRangeException>(() => thresholds.Validate());
    }

    [Theory]
    [InlineData(2)]
    [InlineData(201)]
    public void ValidateShouldThrowWhenBurstLengthThresholdOutOfRange(int invalidBurst)
    {
        var thresholds = new TimingThresholds { BurstLengthThreshold = invalidBurst };
        Should.Throw<ArgumentOutOfRangeException>(() => thresholds.Validate());
    }

    [Fact]
    public void ValidateShouldThrowWhenBurstPauseFloorLessThanHumanFloor()
    {
        var thresholds = new TimingThresholds
        {
            HumanFeasibilityFloorMs = 30.0,
            BurstPauseFloorMs = 25.0
        };
        Should.Throw<ArgumentOutOfRangeException>(() => thresholds.Validate());
    }

    [Theory]
    [InlineData(9)]
    [InlineData(1001)]
    public void ValidateShouldThrowWhenObservationWindowKeyCountOutOfRange(int invalidCount)
    {
        var thresholds = new TimingThresholds { ObservationWindowKeyCount = invalidCount };
        Should.Throw<ArgumentOutOfRangeException>(() => thresholds.Validate());
    }

    [Theory]
    [InlineData(0.99)]
    [InlineData(3.01)]
    public void ValidateShouldThrowWhenObservationWindowMultiplierOutOfRange(double invalidMultiplier)
    {
        var thresholds = new TimingThresholds { ObservationWindowScoreMultiplier = invalidMultiplier };
        Should.Throw<ArgumentOutOfRangeException>(() => thresholds.Validate());
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(100.1)]
    public void ValidateShouldThrowWhenSuspicionScoreThresholdOutOfRange(double invalidThreshold)
    {
        var thresholds = new TimingThresholds { SuspicionScoreThreshold = invalidThreshold };
        Should.Throw<ArgumentOutOfRangeException>(() => thresholds.Validate());
    }
}
