using System.Globalization;
using System.Text;
using HidGuard.Core.Audit;

namespace HidGuard.Core.Siem;

public static class CefFormatter
{
    public static string Format(
        AuditEvent auditEvent,
        string deviceVendor = "HIDGuard",
        string deviceProduct = "HIDGuard",
        string deviceVersion = "1.0.0")
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var (sigId, name, severity) = MapEventDetails(auditEvent.EventType);

        var sb = new StringBuilder();
        sb.Append("CEF:0|");
        sb.Append(EscapeHeader(deviceVendor)).Append('|');
        sb.Append(EscapeHeader(deviceProduct)).Append('|');
        sb.Append(EscapeHeader(deviceVersion)).Append('|');
        sb.Append(EscapeHeader(sigId)).Append('|');
        sb.Append(EscapeHeader(name)).Append('|');
        sb.Append(severity.ToString(CultureInfo.InvariantCulture)).Append('|');

        sb.Append("externalId=").Append(EscapeExtension(auditEvent.EventId.ToString()));
        sb.Append(" rt=").Append(auditEvent.Timestamp.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
        sb.Append(" sdev=").Append(EscapeExtension(auditEvent.DeviceId));
        sb.Append(" act=").Append(EscapeExtension(auditEvent.ActionTaken));
        sb.Append(" outcome=").Append(EscapeExtension(auditEvent.Outcome));
        sb.Append(" msg=").Append(EscapeExtension(auditEvent.Details));
        sb.Append(" duser=").Append(EscapeExtension(auditEvent.UserContext));

        return sb.ToString();
    }

    private static (string SigId, string Name, int Severity) MapEventDetails(AuditEventType eventType)
    {
        return eventType switch
        {
            AuditEventType.AnomalyDetected => ("1001", "Hardware Keystroke Anomaly Detected", 8),
            AuditEventType.CountermeasureExecuted => ("1002", "Hardware Countermeasure Executed", 9),
            AuditEventType.DeviceTrustModified => ("1003", "Device Trust Level Modified", 4),
            AuditEventType.DeviceConnected => ("1004", "New HID Device Connected", 3),
            AuditEventType.DeviceDisconnected => ("1005", "HID Device Disconnected", 3),
            AuditEventType.ConfigurationChanged => ("1006", "Security Configuration Changed", 4),
            _ => ("1000", "Generic Security Event", 5)
        };
    }

    private static string EscapeHeader(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("|", @"\|", StringComparison.Ordinal);
    }

    private static string EscapeExtension(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("=", @"\=", StringComparison.Ordinal)
            .Replace("\n", @"\n", StringComparison.Ordinal)
            .Replace("\r", @"\r", StringComparison.Ordinal);
    }
}
