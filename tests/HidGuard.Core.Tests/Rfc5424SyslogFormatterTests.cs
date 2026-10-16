using HidGuard.Core.Audit;
using HidGuard.Core.Siem;
using Shouldly;
using Xunit;

namespace HidGuard.Core.Tests;

public sealed class Rfc5424SyslogFormatterTests
{
    [Fact]
    public void FormatShouldProduceValidSyslogHeaderAndStructuredData()
    {
        var auditEvent = new AuditEvent
        {
            EventType = AuditEventType.CountermeasureExecuted,
            DeviceId = @"HID\VID_16C0&PID_05DF",
            ActionTaken = "BlockDevice",
            Outcome = "Neutralized",
            Details = "Device blocked by policy"
        };

        var syslog = Rfc5424SyslogFormatter.Format(auditEvent, appName: "HIDGuard", facility: 10, hostName: "SEC-HOST");

        syslog.ShouldStartWith("<82>1 ");
        syslog.ShouldContain(" SEC-HOST HIDGuard ");
        syslog.ShouldContain(" CountermeasureExecuted [hidguard@32473 ");
        syslog.ShouldContain($"eventId=\"{auditEvent.EventId}\"");
        syslog.ShouldContain(@"deviceId=""HID\\VID_16C0&PID_05DF""");
        syslog.ShouldContain("action=\"BlockDevice\"");
        syslog.ShouldContain("outcome=\"Neutralized\"");
        syslog.ShouldEndWith("] Device blocked by policy");
    }

    [Theory]
    [InlineData(AuditEventType.CountermeasureExecuted, 10, 82)]
    [InlineData(AuditEventType.AnomalyDetected, 10, 83)]
    [InlineData(AuditEventType.DeviceTrustModified, 10, 85)]
    [InlineData(AuditEventType.ConfigurationChanged, 10, 85)]
    [InlineData(AuditEventType.DeviceConnected, 10, 86)]
    [InlineData(AuditEventType.DeviceDisconnected, 10, 86)]
    public void FormatShouldCalculatePriCorrectly(AuditEventType type, int facility, int expectedPri)
    {
        var auditEvent = new AuditEvent
        {
            EventType = type,
            DeviceId = "DEV",
            ActionTaken = "Act",
            Outcome = "Out",
            Details = "Det"
        };

        var syslog = Rfc5424SyslogFormatter.Format(auditEvent, facility: facility);

        syslog.ShouldStartWith($"<{expectedPri}>1 ");
    }

    [Fact]
    public void FormatShouldEscapeStructuredDataValues()
    {
        var auditEvent = new AuditEvent
        {
            EventType = AuditEventType.AnomalyDetected,
            DeviceId = "DEV\"1\\]",
            ActionTaken = "Action\"Quoted\"",
            Outcome = "Outcome]Bracket",
            Details = "Message body"
        };

        var syslog = Rfc5424SyslogFormatter.Format(auditEvent);

        syslog.ShouldContain(@"deviceId=""DEV\""1\\\]""");
        syslog.ShouldContain(@"action=""Action\""Quoted\""""");
        syslog.ShouldContain(@"outcome=""Outcome\]Bracket""");
    }
}
