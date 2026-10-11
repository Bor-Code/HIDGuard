using HidGuard.Core.Timing;

namespace HidGuard.Core.Scoring;

public interface IAnomalyScorer
{
    AnomalyScoreResult Score(SlidingWindowStatistics statistics, TimingThresholds thresholds, bool isInObservationWindow, DateTimeOffset evaluationTime);
}
