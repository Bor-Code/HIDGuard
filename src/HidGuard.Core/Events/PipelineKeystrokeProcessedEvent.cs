using HidGuard.Core.Countermeasures;
using HidGuard.Core.Domain;
using HidGuard.Core.Policy;
using HidGuard.Core.Scoring;

namespace HidGuard.Core.Events;

public sealed record PipelineKeystrokeProcessedEvent
{
    public required KeystrokeEvent Keystroke { get; init; }
    public AnomalyScoreResult? AnomalyResult { get; init; }
    public PolicyDecision? Decision { get; init; }
    public CountermeasureExecutionResult? CountermeasureResult { get; init; }
    public TimeSpan Latency { get; init; }
}
