using HidGuard.App.ViewModels;
using HidGuard.Core.Domain;
using HidGuard.Core.Events;
using HidGuard.Core.Scoring;
using HidGuard.Core.Timing;
using Shouldly;
using Xunit;

namespace HidGuard.App.Tests;

public sealed class MetricsViewModelTests
{
    private static readonly DeviceIdentifier TestDevice = new(
        @"\\?\HID#VID_046D&PID_C52B#TEST#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}",
        0x046D,
        0xC52B);

    [Fact]
    public void InitialStateShouldHaveExpectedDefaults()
    {
        var vm = new MetricsViewModel();

        vm.CurrentWpm.ShouldBe(0);
        vm.CurrentMeanIntervalMs.ShouldBe(0);
        vm.CurrentStdDevIntervalMs.ShouldBe(0);
        vm.CoefficientOfVariation.ShouldBe(0);
        vm.SubHumanRatioPercent.ShouldBe(0);
        vm.MinimumIntervalMs.ShouldBe(0);
        vm.BurstLength.ShouldBe(0);
        vm.AnomalyScore.ShouldBe(0);
        vm.HumanConfidenceScore.ShouldBe(100.0);
        vm.BotProbabilityScore.ShouldBe(0);
        vm.ThreatLevelText.ShouldBe("Normal");
        vm.ThreatLevelColor.ShouldBe("#10B981");
        vm.ActiveDeviceId.ShouldBe("None");
        vm.TotalEvaluations.ShouldBe(0);
        vm.AnomalyCount.ShouldBe(0);
        vm.HistogramBuckets.Count.ShouldBe(6);
        vm.RecentSamples.Count.ShouldBe(0);
        vm.RecentIntervals.Count.ShouldBe(0);
    }

    [Fact]
    public void RecordEvaluationWithNormalKeystrokeShouldUpdateMetricsAndHistogram()
    {
        var vm = new MetricsViewModel();

        var stats = new SlidingWindowStatistics
        {
            SampleCount = 30,
            MeanIntervalMs = 125.0,
            StandardDeviationMs = 32.5,
            CoefficientOfVariation = 0.26,
            SubHumanIntervalRatio = 0.0,
            MinimumIntervalMs = 80.0,
            BurstLength = 0
        };

        var anomaly = new AnomalyScoreResult
        {
            Score = 12.0,
            IsSuspicious = false,
            TriggeredSignals = Array.Empty<DetectionSignal>(),
            Statistics = stats,
            EvaluatedAt = DateTimeOffset.UtcNow
        };

        var evt = new PipelineKeystrokeProcessedEvent
        {
            Keystroke = new KeystrokeEvent(TestDevice, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x41),
            AnomalyResult = anomaly
        };

        vm.RecordEvaluation(evt);

        vm.CurrentMeanIntervalMs.ShouldBe(125.0);
        vm.CurrentStdDevIntervalMs.ShouldBe(32.5);
        vm.CoefficientOfVariation.ShouldBe(0.26);
        vm.CurrentWpm.ShouldBeGreaterThan(0);
        vm.AnomalyScore.ShouldBe(12.0);
        vm.HumanConfidenceScore.ShouldBe(88.0);
        vm.BotProbabilityScore.ShouldBe(12.0);
        vm.ThreatLevelText.ShouldBe("Normal Human Keystroke Dynamics");
        vm.ThreatLevelColor.ShouldBe("#10B981");
        vm.TotalEvaluations.ShouldBe(1);
        vm.AnomalyCount.ShouldBe(0);
        vm.RecentSamples.Count.ShouldBe(1);
        vm.RecentIntervals.Count.ShouldBe(1);
        vm.HistogramBuckets[3].Count.ShouldBe(1);
    }

    [Fact]
    public void RecordEvaluationWithAttackKeystrokeShouldFlagThreatAndBurstBucket()
    {
        var vm = new MetricsViewModel();

        var stats = new SlidingWindowStatistics
        {
            SampleCount = 30,
            MeanIntervalMs = 4.0,
            StandardDeviationMs = 0.2,
            CoefficientOfVariation = 0.05,
            SubHumanIntervalRatio = 0.95,
            MinimumIntervalMs = 3.2,
            BurstLength = 25
        };

        var anomaly = new AnomalyScoreResult
        {
            Score = 92.5,
            IsSuspicious = true,
            TriggeredSignals = Array.Empty<DetectionSignal>(),
            Statistics = stats,
            EvaluatedAt = DateTimeOffset.UtcNow
        };

        var evt = new PipelineKeystrokeProcessedEvent
        {
            Keystroke = new KeystrokeEvent(TestDevice, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x41),
            AnomalyResult = anomaly
        };

        vm.RecordEvaluation(evt);

        vm.ThreatLevelText.ShouldBe("Injection Attack Detected");
        vm.ThreatLevelColor.ShouldBe("#EF4444");
        vm.AnomalyCount.ShouldBe(1);
        vm.BurstLength.ShouldBe(25);
        vm.BotProbabilityScore.ShouldBe(92.5);
        vm.HumanConfidenceScore.ShouldBe(7.5);
        vm.SubHumanRatioPercent.ShouldBe(95.0);
        vm.HistogramBuckets[0].Count.ShouldBe(1);
        vm.RecentSamples[0].IsAnomaly.ShouldBeTrue();
    }

    [Fact]
    public void ResetMetricsCommandShouldClearBuffersAndMetrics()
    {
        var vm = new MetricsViewModel();

        var stats = new SlidingWindowStatistics
        {
            SampleCount = 10,
            MeanIntervalMs = 8.0,
            StandardDeviationMs = 0.5,
            CoefficientOfVariation = 0.06,
            SubHumanIntervalRatio = 1.0,
            MinimumIntervalMs = 5.0,
            BurstLength = 10
        };

        var evt = new PipelineKeystrokeProcessedEvent
        {
            Keystroke = new KeystrokeEvent(TestDevice, DateTimeOffset.UtcNow, KeyTransitionState.KeyDown, 0x41),
            AnomalyResult = new AnomalyScoreResult
            {
                Score = 80.0,
                IsSuspicious = true,
                TriggeredSignals = Array.Empty<DetectionSignal>(),
                Statistics = stats,
                EvaluatedAt = DateTimeOffset.UtcNow
            }
        };

        vm.RecordEvaluation(evt);
        vm.TotalEvaluations.ShouldBe(1);

        vm.ResetMetricsCommand.Execute(null);

        vm.TotalEvaluations.ShouldBe(0);
        vm.AnomalyCount.ShouldBe(0);
        vm.CurrentWpm.ShouldBe(0);
        vm.CurrentMeanIntervalMs.ShouldBe(0);
        vm.RecentSamples.Count.ShouldBe(0);
        vm.RecentIntervals.Count.ShouldBe(0);
        vm.HistogramBuckets.All(b => b.Count == 0 && b.Percentage == 0).ShouldBeTrue();
    }
}
