using Shouldly;
using Xunit;
using HidGuard.Core.Audit;

namespace HidGuard.Core.Tests;

public sealed class AuditTrailTests
{
    [Fact]
    public async Task RecordEventAsyncShouldAppendJsonLinesAndRaiseEvent()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_AuditTest_" + Guid.NewGuid().ToString("N"));

        try
        {
            using var auditService = new FileAuditTrailService(tempDir);

            AuditEvent? capturedEvent = null;
            auditService.AuditEventRecorded += (_, e) => capturedEvent = e;

            var auditEvent = new AuditEvent
            {
                EventType = AuditEventType.AnomalyDetected,
                DeviceId = "HID\\VID_16C0&PID_05DF\\0",
                ActionTaken = "WorkstationLock",
                Outcome = "Success",
                Details = "Typing interval anomaly score exceeded 85%"
            };

            await auditService.RecordEventAsync(auditEvent);

            capturedEvent.ShouldNotBeNull();
            capturedEvent.EventType.ShouldBe(AuditEventType.AnomalyDetected);

            var files = Directory.GetFiles(tempDir, "audit-*.jsonl");
            files.Length.ShouldBe(1);

            var content = await File.ReadAllTextAsync(files[0]);
            content.ShouldContain("AnomalyDetected");
            content.ShouldContain("VID_16C0");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task QueryRecentEventsAsyncShouldReturnRecentEventsDescending()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_AuditQuery_" + Guid.NewGuid().ToString("N"));

        try
        {
            using var auditService = new FileAuditTrailService(tempDir);

            for (var i = 1; i <= 5; i++)
            {
                await auditService.RecordEventAsync(new AuditEvent
                {
                    EventType = AuditEventType.DeviceConnected,
                    DeviceId = $"DEV_{i}",
                    ActionTaken = "Enroll",
                    Outcome = "Allowed",
                    Details = $"Connected sequence {i}"
                });
            }

            var recent = await auditService.QueryRecentEventsAsync(limit: 3);

            recent.Count.ShouldBe(3);
            recent[0].DeviceId.ShouldBe("DEV_5");
            recent[1].DeviceId.ShouldBe("DEV_4");
            recent[2].DeviceId.ShouldBe("DEV_3");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task QueryRecentEventsAsyncWhenNoEventsShouldReturnEmpty()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "HIDGuard_AuditEmpty_" + Guid.NewGuid().ToString("N"));

        try
        {
            using var auditService = new FileAuditTrailService(tempDir);

            var events = await auditService.QueryRecentEventsAsync();

            events.ShouldBeEmpty();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void AuditEventSchemaMustNotContainVirtualKeysOrKeystrokeCharacters()
    {
        var properties = typeof(AuditEvent).GetProperties().Select(p => p.Name.ToLowerInvariant()).ToList();

        properties.ShouldNotContain("key");
        properties.ShouldNotContain("vkcode");
        properties.ShouldNotContain("virtualkey");
        properties.ShouldNotContain("char");
        properties.ShouldNotContain("character");
        properties.ShouldNotContain("typedtext");
    }
}
