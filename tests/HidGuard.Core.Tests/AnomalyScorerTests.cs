using Shouldly;
using Xunit;
using HidGuard.Core.Scoring;
using HidGuard.Core.Timing;

namespace HidGuard.Core.Tests;

public sealed class AnomalyScorerTests
{
    private readonly AnomalyScorer scorer = new();
    private readonly TimingThresholds defaultThresholds = new();

    [Fact]
    public void ScoreShouldBeZeroWhenSampleCountBelowFive()
    {
        var stats = new SlidingWindowStatistics
        {
            SampleCount = 4,
            MeanIntervalMs = 5.0,
            MinimumIntervalMs = 5.0,
            SubHumanIntervalRatio = 1.0,
            CoefficientOfVariation = 0.01,
            BurstLength = 20
        };

        var result = scorer.Score(stats, defaultThresholds, false, DateTimeOffset.UtcNow);

        result.Score.ShouldBe(0.0);
        result.IsSuspicious.ShouldBeFalse();
        result.TriggeredSignals.ShouldBeEmpty();
    }

    [Fact]
    public void NormalHumanTypingShouldProduceLowScoreAndNotBeSuspicious()
    {
        var humanStats = new SlidingWindowStatistics
        {
            SampleCount = 30,
            MeanIntervalMs = 145.0,
            MedianIntervalMs = 135.0,
            VarianceMs = 2100.0,
            StandardDeviationMs = 45.8,
            CoefficientOfVariation = 0.3159,
            MinimumIntervalMs = 65.0,
            BurstLength = 6,
            SubHumanIntervalRatio = 0.0
        };

        var result = scorer.Score(humanStats, defaultThresholds, false, DateTimeOffset.UtcNow);

        result.Score.ShouldBe(0.0);
        result.IsSuspicious.ShouldBeFalse();
        result.TriggeredSignals.ShouldBeEmpty();
    }

    [Fact]
    public void BadUsbSyntheticTypingAttackShouldProduceHighScoreAndTriggerAlert()
    {
        var attackStats = new SlidingWindowStatistics
        {
            SampleCount = 30,
            MeanIntervalMs = 8.0,
            MedianIntervalMs = 8.0,
            VarianceMs = 0.01,
            StandardDeviationMs = 0.1,
            CoefficientOfVariation = 0.0125,
            MinimumIntervalMs = 7.9,
            BurstLength = 30,
            SubHumanIntervalRatio = 1.0
        };

        var result = scorer.Score(attackStats, defaultThresholds, false, DateTimeOffset.UtcNow);

        result.Score.ShouldBeGreaterThanOrEqualTo(defaultThresholds.SuspicionScoreThreshold);
        result.IsSuspicious.ShouldBeTrue();
        result.TriggeredSignals.Count.ShouldBeGreaterThanOrEqualTo(3);
        result.TriggeredSignals.ShouldContain(s => s.Type == DetectionSignalType.SubHumanIntervalRate);
        result.TriggeredSignals.ShouldContain(s => s.Type == DetectionSignalType.LowCoefficientOfVariation);
        result.TriggeredSignals.ShouldContain(s => s.Type == DetectionSignalType.ExtremeMinimumInterval);
    }

    [Fact]
    public void FastHumanTypistShouldNotTriggerAlert()
    {
        var fastTypistStats = new SlidingWindowStatistics
        {
            SampleCount = 30,
            MeanIntervalMs = 75.0,
            MedianIntervalMs = 72.0,
            VarianceMs = 450.0,
            StandardDeviationMs = 21.2,
            CoefficientOfVariation = 0.2827,
            MinimumIntervalMs = 38.0,
            BurstLength = 12,
            SubHumanIntervalRatio = 0.0
        };

        var result = scorer.Score(fastTypistStats, defaultThresholds, false, DateTimeOffset.UtcNow);

        result.Score.ShouldBe(0.0);
        result.IsSuspicious.ShouldBeFalse();
    }

    [Fact]
    public void ObservationWindowMultiplierShouldBoostScoreForNewDevice()
    {
        var stats = new SlidingWindowStatistics
        {
            SampleCount = 20,
            MeanIntervalMs = 22.0,
            MedianIntervalMs = 22.0,
            VarianceMs = 2.0,
            StandardDeviationMs = 1.41,
            CoefficientOfVariation = 0.064,
            MinimumIntervalMs = 18.0,
            BurstLength = 16,
            SubHumanIntervalRatio = 0.8
        };

        var normalResult = scorer.Score(stats, defaultThresholds, false, DateTimeOffset.UtcNow);
        var observationResult = scorer.Score(stats, defaultThresholds, true, DateTimeOffset.UtcNow);

        observationResult.Score.ShouldBeGreaterThan(normalResult.Score);
        observationResult.TriggeredSignals.ShouldContain(s => s.Type == DetectionSignalType.ObservationWindowMultiplierApplied);
    }

    [Fact]
    public void ScoreShouldBeCappedAtOneHundred()
    {
        var extremeStats = new SlidingWindowStatistics
        {
            SampleCount = 30,
            MeanIntervalMs = 1.0,
            MedianIntervalMs = 1.0,
            VarianceMs = 0.0,
            StandardDeviationMs = 0.0,
            CoefficientOfVariation = 0.0,
            MinimumIntervalMs = 1.0,
            BurstLength = 100,
            SubHumanIntervalRatio = 1.0
        };

        var thresholds = new TimingThresholds { ObservationWindowScoreMultiplier = 2.5 };
        var result = scorer.Score(extremeStats, thresholds, true, DateTimeOffset.UtcNow);

        result.Score.ShouldBe(100.0);
    }
}
