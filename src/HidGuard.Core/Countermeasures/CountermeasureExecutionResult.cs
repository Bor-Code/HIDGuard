using HidGuard.Core.Policy;

namespace HidGuard.Core.Countermeasures;

public sealed record CountermeasureExecutionResult
{
    public required PolicyDecision Decision { get; init; }
    public bool WorkstationLockAttempted { get; init; }
    public bool WorkstationLockSucceeded { get; init; }
    public bool DeviceDisableAttempted { get; init; }
    public bool DeviceDisableSucceeded { get; init; }
    public bool KeystrokeSuppressionActive { get; init; }
    public DateTimeOffset ExecutedAt { get; init; }
    public string Message { get; init; } = string.Empty;
}
