using System.Globalization;
using HidGuard.App.Mvvm;
using HidGuard.Core.Audit;

namespace HidGuard.App.ViewModels;

public sealed class AuditEventItemViewModel : ObservableObject
{
    public Guid EventId { get; }
    public DateTimeOffset Timestamp { get; }
    public string FormattedTimestamp { get; }
    public AuditEventType EventType { get; }
    public string EventTypeName { get; }
    public string DeviceId { get; }
    public string ActionTaken { get; }
    public string Outcome { get; }
    public string Details { get; }
    public string BadgeColor { get; }

    public AuditEventItemViewModel(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        EventId = auditEvent.EventId;
        Timestamp = auditEvent.Timestamp;
        FormattedTimestamp = auditEvent.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        EventType = auditEvent.EventType;
        EventTypeName = auditEvent.EventType.ToString();
        DeviceId = string.IsNullOrWhiteSpace(auditEvent.DeviceId) ? "N/A" : auditEvent.DeviceId;
        ActionTaken = string.IsNullOrWhiteSpace(auditEvent.ActionTaken) ? "None" : auditEvent.ActionTaken;
        Outcome = string.IsNullOrWhiteSpace(auditEvent.Outcome) ? "Recorded" : auditEvent.Outcome;
        Details = auditEvent.Details;

        BadgeColor = auditEvent.EventType switch
        {
            AuditEventType.AnomalyDetected or AuditEventType.CountermeasureExecuted => "#EF4444",
            AuditEventType.DeviceConnected or AuditEventType.DeviceDisconnected => "#3B82F6",
            AuditEventType.DeviceTrustModified => "#F59E0B",
            _ => "#10B981"
        };
    }
}
