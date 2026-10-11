using System.Collections.Concurrent;
using HidGuard.Core.Domain;
using HidGuard.Core.Scoring;

namespace HidGuard.Core.Timing;

public sealed class TimingAnalysisEngine : ITimingAnalysisEngine
{
    private readonly ConcurrentDictionary<DeviceIdentifier, DeviceKeystrokeTracker> trackers = new();
    private readonly TimingThresholds thresholds;
    private readonly IAnomalyScorer scorer;

    public TimingAnalysisEngine(TimingThresholds thresholds, IAnomalyScorer scorer)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        ArgumentNullException.ThrowIfNull(scorer);
        thresholds.Validate();

        this.thresholds = thresholds;
        this.scorer = scorer;
    }

    public IReadOnlyCollection<DeviceIdentifier> TrackedDevices => trackers.Keys.ToList();

    public AnomalyScoreResult ProcessKeystroke(KeystrokeEvent keystrokeEvent)
    {
        ArgumentNullException.ThrowIfNull(keystrokeEvent);

        var tracker = trackers.GetOrAdd(
            keystrokeEvent.DeviceId,
            id => new DeviceKeystrokeTracker(id, thresholds));

        if (keystrokeEvent.State != KeyTransitionState.KeyDown)
        {
            var existingStats = tracker.GetCurrentStatistics();
            return new AnomalyScoreResult
            {
                Score = 0.0,
                IsSuspicious = false,
                TriggeredSignals = Array.Empty<DetectionSignal>(),
                Statistics = existingStats,
                EvaluatedAt = keystrokeEvent.Timestamp
            };
        }

        var statistics = tracker.RecordKeyDown(keystrokeEvent.Timestamp);
        var inObservation = tracker.IsInObservationWindow;

        return scorer.Score(statistics, thresholds, inObservation, keystrokeEvent.Timestamp);
    }

    public SlidingWindowStatistics? GetStatistics(DeviceIdentifier deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        return trackers.TryGetValue(deviceId, out var tracker) ? tracker.GetCurrentStatistics() : null;
    }

    public void ResetDevice(DeviceIdentifier deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        if (trackers.TryGetValue(deviceId, out var tracker))
        {
            tracker.Reset();
        }
    }

    public void RemoveDevice(DeviceIdentifier deviceId)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        trackers.TryRemove(deviceId, out _);
    }

    public void PruneInactiveDevices(TimeSpan maxInactivity)
    {
        var cutoff = DateTimeOffset.UtcNow - maxInactivity;
        foreach (var pair in trackers)
        {
            if (pair.Value.LastSeen < cutoff)
            {
                trackers.TryRemove(pair.Key, out _);
            }
        }
    }
}
