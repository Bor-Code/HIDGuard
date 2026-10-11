using HidGuard.Core.Timing;

namespace HidGuard.Core.Scoring;

public sealed class AnomalyScorer : IAnomalyScorer
{
    public AnomalyScoreResult Score(
        SlidingWindowStatistics statistics,
        TimingThresholds thresholds,
        bool isInObservationWindow,
        DateTimeOffset evaluationTime)
    {
        ArgumentNullException.ThrowIfNull(statistics);
        ArgumentNullException.ThrowIfNull(thresholds);
        thresholds.Validate();

        if (statistics.SampleCount < 5)
        {
            return new AnomalyScoreResult
            {
                Score = 0.0,
                IsSuspicious = false,
                TriggeredSignals = Array.Empty<DetectionSignal>(),
                Statistics = statistics,
                EvaluatedAt = evaluationTime
            };
        }

        var signals = new List<DetectionSignal>();
        var totalRawScore = 0.0;

        if (statistics.SubHumanIntervalRatio >= thresholds.SubHumanRatioThreshold)
        {
            var ratioFactor = Math.Clamp(statistics.SubHumanIntervalRatio, 0.0, 1.0);
            var subHumanContribution = Math.Round(35.0 * ratioFactor, 2);
            totalRawScore += subHumanContribution;
            signals.Add(new DetectionSignal(
                DetectionSignalType.SubHumanIntervalRate,
                statistics.SubHumanIntervalRatio,
                thresholds.SubHumanRatioThreshold,
                subHumanContribution,
                "Observed interval rate faster than human physical threshold"));
        }

        if (statistics.SampleCount >= 8 &&
            statistics.MeanIntervalMs <= thresholds.BurstPauseFloorMs &&
            statistics.CoefficientOfVariation < thresholds.CoefficientOfVariationThreshold)
        {
            var cvDistance = 1.0 - Math.Clamp(statistics.CoefficientOfVariation / thresholds.CoefficientOfVariationThreshold, 0.0, 1.0);
            var lowCvContribution = Math.Round(35.0 * cvDistance, 2);
            totalRawScore += lowCvContribution;
            signals.Add(new DetectionSignal(
                DetectionSignalType.LowCoefficientOfVariation,
                statistics.CoefficientOfVariation,
                thresholds.CoefficientOfVariationThreshold,
                lowCvContribution,
                "Keystroke intervals exhibit unnatural synthetic regularity"));
        }

        if (statistics.MinimumIntervalMs <= thresholds.MinimumIntervalThresholdMs)
        {
            var minDistance = 1.0 - Math.Clamp(statistics.MinimumIntervalMs / thresholds.MinimumIntervalThresholdMs, 0.0, 1.0);
            var minIntervalContribution = Math.Round(20.0 * minDistance, 2);
            totalRawScore += minIntervalContribution;
            signals.Add(new DetectionSignal(
                DetectionSignalType.ExtremeMinimumInterval,
                statistics.MinimumIntervalMs,
                thresholds.MinimumIntervalThresholdMs,
                minIntervalContribution,
                "Detected physical impossibly low minimum interval"));
        }

        if (statistics.BurstLength >= thresholds.BurstLengthThreshold)
        {
            var burstFactor = Math.Clamp((double)statistics.BurstLength / (thresholds.BurstLengthThreshold * 2.0), 0.5, 1.0);
            var burstContribution = Math.Round(15.0 * burstFactor, 2);
            totalRawScore += burstContribution;
            signals.Add(new DetectionSignal(
                DetectionSignalType.SustainedHighSpeedBurst,
                statistics.BurstLength,
                thresholds.BurstLengthThreshold,
                burstContribution,
                "Sustained rapid keystroke sequence without physiological pause"));
        }

        var finalScore = Math.Min(100.0, totalRawScore);

        if (isInObservationWindow && finalScore > 0.0)
        {
            var adjustedScore = Math.Min(100.0, Math.Round(finalScore * thresholds.ObservationWindowScoreMultiplier, 2));
            var multiplierDelta = Math.Round(adjustedScore - finalScore, 2);
            if (multiplierDelta > 0.0)
            {
                signals.Add(new DetectionSignal(
                    DetectionSignalType.ObservationWindowMultiplierApplied,
                    thresholds.ObservationWindowScoreMultiplier,
                    1.0,
                    multiplierDelta,
                    "Elevated sensitivity applied for recently attached device"));
            }

            finalScore = adjustedScore;
        }

        return new AnomalyScoreResult
        {
            Score = finalScore,
            IsSuspicious = finalScore >= thresholds.SuspicionScoreThreshold,
            TriggeredSignals = signals,
            Statistics = statistics,
            EvaluatedAt = evaluationTime
        };
    }
}
