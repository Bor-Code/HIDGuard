using System.Net.Sockets;
using HidGuard.Core.Configuration;

namespace HidGuard.Core.Siem;

public sealed class NetworkSiemTransport : ISiemTransport
{
    public async Task SendAsync(
        byte[] payload,
        string host,
        int port,
        SiemProtocol protocol,
        int timeoutMilliseconds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMilliseconds);

        if (protocol == SiemProtocol.Udp)
        {
            using var udpClient = new UdpClient();
            await udpClient.SendAsync(payload, payload.Length, host, port).WaitAsync(cts.Token).ConfigureAwait(false);
            return;
        }

        using var tcpClient = new TcpClient();
        await tcpClient.ConnectAsync(host, port, cts.Token).ConfigureAwait(false);
        await using var stream = tcpClient.GetStream();
        await stream.WriteAsync(payload, cts.Token).ConfigureAwait(false);
        await stream.WriteAsync(new[] { (byte)'\n' }, cts.Token).ConfigureAwait(false);
        await stream.FlushAsync(cts.Token).ConfigureAwait(false);
    }
}
