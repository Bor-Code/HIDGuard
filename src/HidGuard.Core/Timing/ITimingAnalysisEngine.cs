using HidGuard.Core.Domain;
using HidGuard.Core.Scoring;

namespace HidGuard.Core.Timing;

public interface ITimingAnalysisEngine
{
    AnomalyScoreResult ProcessKeystroke(KeystrokeEvent keystrokeEvent);
    SlidingWindowStatistics? GetStatistics(DeviceIdentifier deviceId);
    void ResetDevice(DeviceIdentifier deviceId);
    void RemoveDevice(DeviceIdentifier deviceId);
    void PruneInactiveDevices(TimeSpan maxInactivity);
    IReadOnlyCollection<DeviceIdentifier> TrackedDevices { get; }
}
