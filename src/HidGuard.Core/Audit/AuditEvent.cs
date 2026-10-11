namespace HidGuard.Core.Audit;

public sealed record AuditEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public required AuditEventType EventType { get; init; }
    public string DeviceId { get; init; } = string.Empty;
    public string ActionTaken { get; init; } = string.Empty;
    public string Outcome { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public string UserContext { get; init; } = Environment.UserName;
}
