namespace HidGuard.Core.Audit;

public interface IAuditTrailService : IDisposable
{
    Task RecordEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEvent>> QueryRecentEventsAsync(int limit = 100, CancellationToken cancellationToken = default);
    event EventHandler<AuditEvent>? AuditEventRecorded;
}
