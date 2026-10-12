using Shouldly;
using Xunit;
using HidGuard.App.ViewModels;
using HidGuard.Core.Audit;

namespace HidGuard.App.Tests;

public sealed class AuditLogViewModelTests
{
    private sealed class StubAuditTrailService : IAuditTrailService
    {
        private readonly List<AuditEvent> events = new();

        public event EventHandler<AuditEvent>? AuditEventRecorded;

        public Task RecordEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            events.Add(auditEvent);
            AuditEventRecorded?.Invoke(this, auditEvent);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AuditEvent>> QueryRecentEventsAsync(int limit = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<AuditEvent>>(events);
        }

        public void Dispose() { }
    }

    [Fact]
    public async Task AuditLogViewModelShouldFilterBySearchText()
    {
        var audit = new StubAuditTrailService();
        await audit.RecordEventAsync(new AuditEvent
        {
            EventType = AuditEventType.AnomalyDetected,
            DeviceId = "USB_BAD_DEVICE",
            ActionTaken = "LockWorkstation",
            Outcome = "Success",
            Details = "Injection rate 95%"
        });
        await audit.RecordEventAsync(new AuditEvent
        {
            EventType = AuditEventType.DeviceConnected,
            DeviceId = "GENUINE_KEYBOARD",
            ActionTaken = "Allow",
            Outcome = "Success",
            Details = "Standard keyboard connected"
        });

        var vm = new AuditLogViewModel(audit);
        await vm.LoadAuditEventsAsync();

        vm.FilteredEvents.Count.ShouldBe(2);

        vm.SearchText = "BAD_DEVICE";

        vm.FilteredEvents.Count.ShouldBe(1);
        vm.FilteredEvents[0].DeviceId.ShouldBe("USB_BAD_DEVICE");

        vm.ClearFilterCommand.Execute(null);

        vm.FilteredEvents.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AuditLogViewModelShouldFilterByEventType()
    {
        var audit = new StubAuditTrailService();
        await audit.RecordEventAsync(new AuditEvent
        {
            EventType = AuditEventType.AnomalyDetected,
            DeviceId = "DEV_1",
            ActionTaken = "Lock",
            Outcome = "Success",
            Details = "Alert"
        });
        await audit.RecordEventAsync(new AuditEvent
        {
            EventType = AuditEventType.DeviceConnected,
            DeviceId = "DEV_2",
            ActionTaken = "Enroll",
            Outcome = "Success",
            Details = "Enroll"
        });

        var vm = new AuditLogViewModel(audit);
        await vm.LoadAuditEventsAsync();

        vm.SelectedTypeFilter = AuditEventType.AnomalyDetected;

        vm.FilteredEvents.Count.ShouldBe(1);
        vm.FilteredEvents[0].EventType.ShouldBe(AuditEventType.AnomalyDetected);
    }
}
