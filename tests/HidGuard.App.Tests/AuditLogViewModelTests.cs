using System.IO;
using Shouldly;
using Xunit;
using HidGuard.App.ViewModels;
using HidGuard.Core.Audit;
using HidGuard.Core.Configuration;
using HidGuard.Core.Incidents;

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

    [Fact]
    public async Task AuditLogViewModelShouldExportReportWhenPathSelected()
    {
        var audit = new StubAuditTrailService();
        var store = new StubIncidentStore();
        var tempFile = Path.Combine(Path.GetTempPath(), $"test-export-{Guid.NewGuid():N}.json");

        try
        {
            var vm = new AuditLogViewModel(audit, store, () => tempFile);
            vm.ExportReportCommand.CanExecute(null).ShouldBeTrue();
            await vm.ExportReportInteractiveAsync();

            store.ExportCallCount.ShouldBe(1);
            store.LastFormat.ShouldBe(IncidentExportFormat.Json);
            File.Exists(tempFile).ShouldBeTrue();
            (await File.ReadAllTextAsync(tempFile)).ShouldBe("stub-report");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task AuditLogViewModelShouldDoNothingWhenCancelledOrNoStore()
    {
        var audit = new StubAuditTrailService();
        var store = new StubIncidentStore();

        var vmWithCancelledDialog = new AuditLogViewModel(audit, store, () => null);
        await vmWithCancelledDialog.ExportReportInteractiveAsync();
        store.ExportCallCount.ShouldBe(0);

        var vmWithoutStore = new AuditLogViewModel(audit, incidentStore: null, () => "path.json");
        await vmWithoutStore.ExportReportInteractiveAsync();
        store.ExportCallCount.ShouldBe(0);
    }

    private sealed class StubIncidentStore : IIncidentStore
    {
        public int ExportCallCount { get; private set; }
        public IncidentExportFormat LastFormat { get; private set; }
        public event EventHandler<IncidentRecord>? IncidentRecorded { add { } remove { } }

        public void UpdateConfiguration(IncidentStoreConfiguration configuration) { }
        public Task<IncidentRecord> RecordIncidentAsync(CreateIncidentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<IncidentRecord>> GetIncidentsAsync(int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IncidentRecord>>(Array.Empty<IncidentRecord>());
        public Task<IncidentRecord?> GetIncidentByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IncidentRecord?>(null);
        public Task<int> PurgeExpiredIncidentsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(0);
        public Task ExportMetadataReportAsync(Stream outputStream, IncidentExportFormat format = IncidentExportFormat.Json, CancellationToken cancellationToken = default)
        {
            ExportCallCount++;
            LastFormat = format;
            var bytes = "stub-report"u8.ToArray();
            outputStream.Write(bytes, 0, bytes.Length);
            return Task.CompletedTask;
        }
        public Task<byte[]?> DecryptPayloadAsync(Guid incidentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]?>(null);
        public void Dispose() { }
    }
}
