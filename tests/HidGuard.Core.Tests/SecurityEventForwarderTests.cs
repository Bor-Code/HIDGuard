using System.Net.Sockets;
using System.Text;
using HidGuard.Core.Audit;
using HidGuard.Core.Configuration;
using HidGuard.Core.Siem;
using Shouldly;
using Xunit;

namespace HidGuard.Core.Tests;

public sealed class SecurityEventForwarderTests
{
    private sealed class FakeSiemTransport : ISiemTransport
    {
        public List<(byte[] Payload, string Host, int Port, SiemProtocol Protocol, int TimeoutMs)> Sent { get; } = new();
        public bool ShouldThrow { get; set; }

        public Task SendAsync(
            byte[] payload,
            string host,
            int port,
            SiemProtocol protocol,
            int timeoutMilliseconds,
            CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new SocketException((int)SocketError.HostUnreachable);
            }

            Sent.Add((payload, host, port, protocol, timeoutMilliseconds));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuditTrailService : IAuditTrailService
    {
        public event EventHandler<AuditEvent>? AuditEventRecorded;

        public Task RecordEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            AuditEventRecorded?.Invoke(this, auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEvent>> QueryRecentEventsAsync(int limit = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AuditEvent>>(Array.Empty<AuditEvent>());
        }

        public void Dispose()
        {
        }
    }

    [Fact]
    public async Task ForwardEventAsyncWhenDisabledShouldReturnTrueWithoutTransport()
    {
        var transport = new FakeSiemTransport();
        var config = new SiemConfiguration { Enabled = false };
        await using var forwarder = new SecurityEventForwarder(config, transport);

        var auditEvent = new AuditEvent
        {
            EventType = AuditEventType.AnomalyDetected,
            DeviceId = "DEV",
            ActionTaken = "Alert",
            Outcome = "Done"
        };

        var result = await forwarder.ForwardEventAsync(auditEvent);

        result.ShouldBeTrue();
        transport.Sent.Count.ShouldBe(0);
        forwarder.TotalEventsForwarded.ShouldBe(0);
        forwarder.TotalForwardingErrors.ShouldBe(0);
    }

    [Fact]
    public async Task ForwardEventAsyncInCefFormatShouldDispatchToTransport()
    {
        var transport = new FakeSiemTransport();
        var config = new SiemConfiguration
        {
            Enabled = true,
            Host = "10.0.0.50",
            Port = 514,
            Format = SiemFormat.Cef,
            Protocol = SiemProtocol.Udp
        };
        await using var forwarder = new SecurityEventForwarder(config, transport);

        var auditEvent = new AuditEvent
        {
            EventType = AuditEventType.AnomalyDetected,
            DeviceId = "USB_BAD",
            ActionTaken = "Block",
            Outcome = "Neutralized",
            Details = "Micro-burst detected"
        };

        var result = await forwarder.ForwardEventAsync(auditEvent);

        result.ShouldBeTrue();
        transport.Sent.Count.ShouldBe(1);
        transport.Sent[0].Host.ShouldBe("10.0.0.50");
        transport.Sent[0].Port.ShouldBe(514);
        transport.Sent[0].Protocol.ShouldBe(SiemProtocol.Udp);

        var text = Encoding.UTF8.GetString(transport.Sent[0].Payload);
        text.ShouldStartWith("CEF:0|HIDGuard|HIDGuard|1.0.0|1001|");
        text.ShouldContain("sdev=USB_BAD");
        forwarder.TotalEventsForwarded.ShouldBe(1);
        forwarder.TotalForwardingErrors.ShouldBe(0);
    }

    [Fact]
    public async Task ForwardEventAsyncInRfc5424FormatShouldDispatchToTransport()
    {
        var transport = new FakeSiemTransport();
        var config = new SiemConfiguration
        {
            Enabled = true,
            Host = "10.0.0.55",
            Port = 1514,
            Format = SiemFormat.Rfc5424,
            Protocol = SiemProtocol.Tcp
        };
        await using var forwarder = new SecurityEventForwarder(config, transport);

        var auditEvent = new AuditEvent
        {
            EventType = AuditEventType.CountermeasureExecuted,
            DeviceId = "USB_ATTACK",
            ActionTaken = "BlockDevice",
            Outcome = "Success",
            Details = "Device blocked"
        };

        var result = await forwarder.ForwardEventAsync(auditEvent);

        result.ShouldBeTrue();
        transport.Sent.Count.ShouldBe(1);
        transport.Sent[0].Protocol.ShouldBe(SiemProtocol.Tcp);

        var text = Encoding.UTF8.GetString(transport.Sent[0].Payload);
        text.ShouldStartWith("<82>1 ");
        text.ShouldContain(" [hidguard@32473 ");
        forwarder.TotalEventsForwarded.ShouldBe(1);
    }

    [Fact]
    public async Task ForwardEventAsyncWhenTransportThrowsShouldHandleGracefully()
    {
        var transport = new FakeSiemTransport { ShouldThrow = true };
        var config = new SiemConfiguration
        {
            Enabled = true,
            Host = "unreachable.local",
            Port = 514
        };
        await using var forwarder = new SecurityEventForwarder(config, transport);

        var auditEvent = new AuditEvent
        {
            EventType = AuditEventType.CountermeasureExecuted,
            DeviceId = "DEV",
            ActionTaken = "Act",
            Outcome = "Out"
        };

        var result = await forwarder.ForwardEventAsync(auditEvent);

        result.ShouldBeFalse();
        forwarder.TotalEventsForwarded.ShouldBe(0);
        forwarder.TotalForwardingErrors.ShouldBe(1);
    }

    [Fact]
    public async Task SecurityEventForwarderShouldListenToAuditTrailService()
    {
        var transport = new FakeSiemTransport();
        var auditTrail = new FakeAuditTrailService();
        var config = new SiemConfiguration
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = 514
        };
        await using var forwarder = new SecurityEventForwarder(config, transport, auditTrail);

        var auditEvent = new AuditEvent
        {
            EventType = AuditEventType.AnomalyDetected,
            DeviceId = "DEV1",
            ActionTaken = "Block",
            Outcome = "Neutralized"
        };

        await auditTrail.RecordEventAsync(auditEvent);

        for (var i = 0; i < 20 && transport.Sent.Count == 0; i++)
        {
            await Task.Delay(20);
        }

        transport.Sent.Count.ShouldBe(1);
    }

    [Fact]
    public async Task DisposeShouldUnsubscribeFromAuditTrailService()
    {
        var transport = new FakeSiemTransport();
        var auditTrail = new FakeAuditTrailService();
        var config = new SiemConfiguration
        {
            Enabled = true,
            Host = "127.0.0.1",
            Port = 514
        };
        var forwarder = new SecurityEventForwarder(config, transport, auditTrail);

        forwarder.Dispose();

        var auditEvent = new AuditEvent
        {
            EventType = AuditEventType.AnomalyDetected,
            DeviceId = "DEV1",
            ActionTaken = "Block",
            Outcome = "Neutralized"
        };

        await auditTrail.RecordEventAsync(auditEvent);
        await Task.Delay(50);

        transport.Sent.Count.ShouldBe(0);
    }
}
