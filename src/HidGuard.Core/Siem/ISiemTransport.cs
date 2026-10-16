using HidGuard.Core.Configuration;

namespace HidGuard.Core.Siem;

public interface ISiemTransport
{
    Task SendAsync(
        byte[] payload,
        string host,
        int port,
        SiemProtocol protocol,
        int timeoutMilliseconds,
        CancellationToken cancellationToken = default);
}
