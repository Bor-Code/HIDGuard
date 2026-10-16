namespace HidGuard.Core.Audit;

public static class SecurityEventIds
{
    public const int ServiceStarted = 1000;
    public const int AttackDetected = 1001;
    public const int DeviceDisabled = 1002;
    public const int AntiLockoutTriggered = 1003;
    public const int KeystrokeSuppressionActive = 1004;
    public const int DeviceReEnabled = 1005;
}
