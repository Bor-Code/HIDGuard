using System.Text;
using HidGuard.Core.Audit;
using HidGuard.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace HidGuard.Core.Siem;

public sealed class SecurityEventForwarder : ISecurityEventForwarder
{
    private static readonly Action<ILogger, Guid, string, int, Exception?> LogForwardFailedDelegate =
        LoggerMessage.Define<Guid, string, int>(
            LogLevel.Warning,
            new EventId(1, nameof(SecurityEventForwarder)),
            "Failed to forward security event {EventId} to SIEM at {Host}:{Port}");

    private static readonly Action<ILogger, Guid, Exception?> LogForwardBackgroundErrorDelegate =
        LoggerMessage.Define<Guid>(
            LogLevel.Warning,
            new EventId(2, nameof(SecurityEventForwarder)),
            "Unexpected error in background SIEM forwarder listener for event {EventId}");

    private readonly ISiemTransport transport;
    private readonly IAuditTrailService? auditTrailService;
    private readonly ILogger<SecurityEventForwarder> logger;
    private long totalEventsForwarded;
    private long totalForwardingErrors;
    private bool disposed;

    public SiemConfiguration Configuration { get; }
    public long TotalEventsForwarded => Interlocked.Read(ref totalEventsForwarded);
    public long TotalForwardingErrors => Interlocked.Read(ref totalForwardingErrors);

    public SecurityEventForwarder(
        SiemConfiguration configuration,
        ISiemTransport? transport = null,
        IAuditTrailService? auditTrailService = null,
        ILogger<SecurityEventForwarder>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();

        Configuration = configuration;
        this.transport = transport ?? new NetworkSiemTransport();
        this.auditTrailService = auditTrailService;
        this.logger = logger ?? NullLogger<SecurityEventForwarder>.Instance;

        if (this.auditTrailService != null)
        {
            this.auditTrailService.AuditEventRecorded += OnAuditEventRecorded;
        }
    }

    public async Task<bool> ForwardEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        if (!Configuration.Enabled)
        {
            return true;
        }

        try
        {
            var message = Configuration.Format == SiemFormat.Cef
                ? CefFormatter.Format(auditEvent)
                : Rfc5424SyslogFormatter.Format(auditEvent, Configuration.AppName, Configuration.Facility);

            var payload = Encoding.UTF8.GetBytes(message);

            await transport.SendAsync(
                payload,
                Configuration.Host,
                Configuration.Port,
                Configuration.Protocol,
                Configuration.TimeoutMilliseconds,
                cancellationToken).ConfigureAwait(false);

            Interlocked.Increment(ref totalEventsForwarded);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Interlocked.Increment(ref totalForwardingErrors);
            LogForwardFailedDelegate(logger, auditEvent.EventId, Configuration.Host, Configuration.Port, ex);
            return false;
        }
    }

    private void OnAuditEventRecorded(object? sender, AuditEvent auditEvent)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await ForwardEventAsync(auditEvent).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogForwardBackgroundErrorDelegate(logger, auditEvent.EventId, ex);
            }
        });
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (auditTrailService != null)
        {
            auditTrailService.AuditEventRecorded -= OnAuditEventRecorded;
        }

        if (transport is IDisposable disposableTransport)
        {
            disposableTransport.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        if (auditTrailService != null)
        {
            auditTrailService.AuditEventRecorded -= OnAuditEventRecorded;
        }

        if (transport is IAsyncDisposable asyncDisposableTransport)
        {
            await asyncDisposableTransport.DisposeAsync().ConfigureAwait(false);
        }
        else if (transport is IDisposable disposableTransport)
        {
            disposableTransport.Dispose();
        }
    }
}
