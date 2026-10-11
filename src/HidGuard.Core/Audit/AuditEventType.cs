namespace HidGuard.Core.Audit;

public enum AuditEventType
{
    DeviceConnected,
    DeviceDisconnected,
    DeviceTrustModified,
    AnomalyDetected,
    CountermeasureExecuted,
    ConfigurationChanged
}
