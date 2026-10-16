using HidGuard.Core.Audit;
using HidGuard.Core.Configuration;

namespace HidGuard.Core.Siem;

public interface ISecurityEventForwarder : IAsyncDisposable, IDisposable
{
    SiemConfiguration Configuration { get; }
    long TotalEventsForwarded { get; }
    long TotalForwardingErrors { get; }
    Task<bool> ForwardEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default);
}
