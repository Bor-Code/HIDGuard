using HidGuard.Core.Domain;
using HidGuard.Core.Scoring;

namespace HidGuard.Core.Policy;

public interface IPolicyEngine
{
    Task<PolicyDecision> EvaluateAsync(
        DeviceIdentifier deviceId,
        AnomalyScoreResult? anomalyResult,
        int totalKeyboardsAttached,
        CancellationToken cancellationToken = default);
}
