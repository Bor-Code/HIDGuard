using HidGuard.Core.Domain;
using HidGuard.Core.Scoring;
using HidGuard.Core.Trust;

namespace HidGuard.Core.Policy;

public sealed class PolicyEngine : IPolicyEngine
{
    private readonly ITrustStore trustStore;
    private readonly PolicyOptions options;

    public PolicyEngine(ITrustStore trustStore, PolicyOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(trustStore);
        this.trustStore = trustStore;
        this.options = options ?? new PolicyOptions();
        this.options.Validate();
    }

    public async Task<PolicyDecision> EvaluateAsync(
        DeviceIdentifier deviceId,
        AnomalyScoreResult? anomalyResult,
        int totalKeyboardsAttached,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(deviceId);

        var trustLevel = await trustStore.GetTrustLevelAsync(deviceId, cancellationToken).ConfigureAwait(false);

        if (trustLevel == DeviceTrustLevel.Blocked)
        {
            if (options.EnforceMinimumOneActiveKeyboard && totalKeyboardsAttached <= 1)
            {
                return new PolicyDecision(
                    PolicyAction.AlertOnly,
                    deviceId,
                    trustLevel,
                    "Device is marked blocked but cannot be disabled because it is the sole keyboard attached.",
                    anomalyResult);
            }

            return new PolicyDecision(
                PolicyAction.BlockDevice,
                deviceId,
                trustLevel,
                "Device is explicitly untrusted and blocked by policy.",
                anomalyResult);
        }

        if (anomalyResult != null && anomalyResult.IsSuspicious)
        {
            var candidateAction = MapAnomalyAction(options.DefaultAnomalyAction);

            if (options.EnforceMinimumOneActiveKeyboard && totalKeyboardsAttached <= 1)
            {
                if (candidateAction == PolicyAction.BlockDevice)
                {
                    return new PolicyDecision(
                        PolicyAction.LockWorkstation,
                        deviceId,
                        trustLevel,
                        "Keystroke injection detected. Workstation locked instead of disabling the only remaining keyboard.",
                        anomalyResult);
                }

                if (candidateAction == PolicyAction.LockAndBlock)
                {
                    return new PolicyDecision(
                        PolicyAction.LockWorkstation,
                        deviceId,
                        trustLevel,
                        "Keystroke injection detected. Workstation locked but device disable skipped for sole attached keyboard.",
                        anomalyResult);
                }
            }

            return new PolicyDecision(
                candidateAction,
                deviceId,
                trustLevel,
                "Keystroke injection attack detected by timing anomaly engine.",
                anomalyResult);
        }

        if (trustLevel == DeviceTrustLevel.Trusted)
        {
            return new PolicyDecision(
                PolicyAction.Allow,
                deviceId,
                trustLevel,
                "Device is enrolled in trust store and typing rhythm is normal.",
                anomalyResult);
        }

        return options.DefaultUnknownDeviceAction switch
        {
            UnknownDeviceAction.AllowWithObservation => new PolicyDecision(
                PolicyAction.Allow,
                deviceId,
                trustLevel,
                "Device is unrecognized but allowed under observation window.",
                anomalyResult),

            UnknownDeviceAction.PromptUser => new PolicyDecision(
                PolicyAction.PromptUser,
                deviceId,
                trustLevel,
                "Unrecognized device requires user trust confirmation.",
                anomalyResult),

            UnknownDeviceAction.BlockImmediately => EvaluateUnknownBlock(deviceId, trustLevel, totalKeyboardsAttached, anomalyResult),

            _ => new PolicyDecision(
                PolicyAction.Allow,
                deviceId,
                trustLevel,
                "Default fallback policy applied.",
                anomalyResult)
        };
    }

    private PolicyDecision EvaluateUnknownBlock(
        DeviceIdentifier deviceId,
        DeviceTrustLevel trustLevel,
        int totalKeyboardsAttached,
        AnomalyScoreResult? anomalyResult)
    {
        if (options.EnforceMinimumOneActiveKeyboard && totalKeyboardsAttached <= 1)
        {
            return new PolicyDecision(
                PolicyAction.PromptUser,
                deviceId,
                trustLevel,
                "Unrecognized device would be blocked but is the sole keyboard attached. Prompting user instead.",
                anomalyResult);
        }

        return new PolicyDecision(
            PolicyAction.BlockDevice,
            deviceId,
            trustLevel,
            "Unrecognized device blocked immediately under strict policy.",
            anomalyResult);
    }

    private static PolicyAction MapAnomalyAction(AnomalyAction anomalyAction) => anomalyAction switch
    {
        AnomalyAction.AlertOnly => PolicyAction.AlertOnly,
        AnomalyAction.LockWorkstation => PolicyAction.LockWorkstation,
        AnomalyAction.BlockDevice => PolicyAction.BlockDevice,
        AnomalyAction.LockAndBlock => PolicyAction.LockAndBlock,
        _ => PolicyAction.LockWorkstation
    };
}
