using HidGuard.Core.Domain;
using HidGuard.Core.Scoring;
using HidGuard.Core.Trust;

namespace HidGuard.Core.Policy;

public sealed record PolicyDecision
{
    public PolicyAction Action { get; init; }
    public DeviceIdentifier DeviceId { get; init; }
    public DeviceTrustLevel TrustLevel { get; init; }
    public string Reason { get; init; }
    public AnomalyScoreResult? AnomalyResult { get; init; }
    public DateTimeOffset DecidedAt { get; init; }

    public PolicyDecision(
        PolicyAction action,
        DeviceIdentifier deviceId,
        DeviceTrustLevel trustLevel,
        string reason,
        AnomalyScoreResult? anomalyResult = null)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Action = action;
        DeviceId = deviceId;
        TrustLevel = trustLevel;
        Reason = reason;
        AnomalyResult = anomalyResult;
        DecidedAt = DateTimeOffset.UtcNow;
    }
}
