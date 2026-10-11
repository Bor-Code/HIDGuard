namespace HidGuard.Core.Events;

public sealed record PipelineStatistics
{
    public long TotalKeystrokesProcessed { get; init; }
    public long TotalKeystrokesSuppressed { get; init; }
    public long TotalAttacksDetected { get; init; }
    public long TotalAttacksNeutralized { get; init; }
    public int ActiveTrackedDevices { get; init; }
    public DateTimeOffset StartedAt { get; init; }
}
