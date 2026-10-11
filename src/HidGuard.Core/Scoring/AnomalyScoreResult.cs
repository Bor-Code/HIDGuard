using HidGuard.Core.Timing;

namespace HidGuard.Core.Scoring;

public sealed record AnomalyScoreResult
{
    public double Score { get; init; }
    public bool IsSuspicious { get; init; }
    public IReadOnlyList<DetectionSignal> TriggeredSignals { get; init; } = Array.Empty<DetectionSignal>();
    public SlidingWindowStatistics Statistics { get; init; } = SlidingWindowStatistics.Empty;
    public DateTimeOffset EvaluatedAt { get; init; }
}
