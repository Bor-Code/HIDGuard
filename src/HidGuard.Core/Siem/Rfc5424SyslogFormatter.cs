using System.Globalization;
using System.Text;
using HidGuard.Core.Audit;

namespace HidGuard.Core.Siem;

public static class Rfc5424SyslogFormatter
{
    public static string Format(
        AuditEvent auditEvent,
        string appName = "HIDGuard",
        int facility = 10,
        string? hostName = null)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        var severity = MapSyslogSeverity(auditEvent.EventType);
        var pri = (facility * 8) + severity;
        var host = !string.IsNullOrWhiteSpace(hostName) ? hostName : Environment.MachineName;
        var timestamp = auditEvent.Timestamp.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture);
        var procId = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        var msgId = auditEvent.EventType.ToString();

        var sb = new StringBuilder();
        sb.Append('<').Append(pri.ToString(CultureInfo.InvariantCulture)).Append(">1 ");
        sb.Append(timestamp).Append(' ');
        sb.Append(host).Append(' ');
        sb.Append(appName).Append(' ');
        sb.Append(procId).Append(' ');
        sb.Append(msgId).Append(' ');

        sb.Append("[hidguard@32473");
        sb.Append(" eventId=\"").Append(EscapeSdValue(auditEvent.EventId.ToString())).Append('\"');
        sb.Append(" deviceId=\"").Append(EscapeSdValue(auditEvent.DeviceId)).Append('\"');
        sb.Append(" action=\"").Append(EscapeSdValue(auditEvent.ActionTaken)).Append('\"');
        sb.Append(" outcome=\"").Append(EscapeSdValue(auditEvent.Outcome)).Append('\"');
        sb.Append("] ");

        sb.Append(auditEvent.Details);

        return sb.ToString();
    }

    private static int MapSyslogSeverity(AuditEventType eventType)
    {
        return eventType switch
        {
            AuditEventType.CountermeasureExecuted => 2,
            AuditEventType.AnomalyDetected => 3,
            AuditEventType.DeviceTrustModified => 5,
            AuditEventType.ConfigurationChanged => 5,
            AuditEventType.DeviceConnected => 6,
            AuditEventType.DeviceDisconnected => 6,
            _ => 6
        };
    }

    private static string EscapeSdValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("]", @"\]", StringComparison.Ordinal);
    }
}
