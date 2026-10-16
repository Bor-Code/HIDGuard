using HidGuard.Core.Audit;
using HidGuard.Core.Siem;
using Shouldly;
using Xunit;

namespace HidGuard.Core.Tests;

public sealed class CefFormatterTests
{
    [Fact]
    public void FormatShouldProduceValidCefHeaderAndExtensions()
    {
        var auditEvent = new AuditEvent
        {
            EventType = AuditEventType.AnomalyDetected,
            DeviceId = @"HID\VID_16C0&PID_05DF",
            ActionTaken = "BlockDevice",
            Outcome = "Neutralized",
            Details = "Interval 3.5ms exceeded threshold",
            UserContext = "TestUser"
        };

        var cef = CefFormatter.Format(auditEvent);

        cef.ShouldStartWith("CEF:0|HIDGuard|HIDGuard|1.0.0|1001|Hardware Keystroke Anomaly Detected|8|");
        cef.ShouldContain($"externalId={auditEvent.EventId}");
        cef.ShouldContain(@"sdev=HID\\VID_16C0&PID_05DF");
        cef.ShouldContain("act=BlockDevice");
        cef.ShouldContain("outcome=Neutralized");
        cef.ShouldContain("msg=Interval 3.5ms exceeded threshold");
        cef.ShouldContain("duser=TestUser");
    }

    [Theory]
    [InlineData(AuditEventType.AnomalyDetected, "1001", "8")]
    [InlineData(AuditEventType.CountermeasureExecuted, "1002", "9")]
    [InlineData(AuditEventType.DeviceTrustModified, "1003", "4")]
    [InlineData(AuditEventType.DeviceConnected, "1004", "3")]
    [InlineData(AuditEventType.DeviceDisconnected, "1005", "3")]
    [InlineData(AuditEventType.ConfigurationChanged, "1006", "4")]
    public void FormatShouldMapSeverityAndSignatureCorrectly(AuditEventType type, string expectedSig, string expectedSeverity)
    {
        var auditEvent = new AuditEvent
        {
            EventType = type,
            DeviceId = "DEV01",
            ActionTaken = "Action",
            Outcome = "Success",
            Details = "Details"
        };

        var cef = CefFormatter.Format(auditEvent);

        cef.ShouldContain($"|{expectedSig}|");
        cef.ShouldContain($"|{expectedSeverity}|");
    }

    [Fact]
    public void FormatShouldEscapeSpecialCharacters()
    {
        var auditEvent = new AuditEvent
        {
            EventType = AuditEventType.CountermeasureExecuted,
            DeviceId = @"DEV|1=2\3",
            ActionTaken = "Block=True|Immediate",
            Outcome = "Success=1",
            Details = "Found | character and = sign"
        };

        var cef = CefFormatter.Format(auditEvent, deviceVendor: "Vendor|Test", deviceProduct: "Prod\\Test");

        cef.ShouldContain(@"Vendor\|Test");
        cef.ShouldContain(@"Prod\\Test");
        cef.ShouldContain(@"sdev=DEV|1\=2\\3");
        cef.ShouldContain(@"act=Block\=True|Immediate");
    }
}
