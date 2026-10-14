using Shouldly;
using Xunit;
using HidGuard.Core.Configuration;
using HidGuard.Core.Incidents;

namespace HidGuard.Core.Tests;

public sealed class IncidentMaintenanceServiceTests
{
    private sealed class StubIncidentStore : IIncidentStore
    {
        private readonly TaskCompletionSource purgeInvokedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int purgeCallCount;

        public int PurgeCallCount => Volatile.Read(ref purgeCallCount);
        public IncidentStoreConfiguration CurrentConfiguration { get; private set; } = new();
        public event EventHandler<IncidentRecord>? IncidentRecorded { add { } remove { } }

        public Task WaitForPurgeAsync(TimeSpan timeout) => purgeInvokedTcs.Task.WaitAsync(timeout);

        public void UpdateConfiguration(IncidentStoreConfiguration configuration)
        {
            CurrentConfiguration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public Task<int> PurgeExpiredIncidentsAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref purgeCallCount);
            purgeInvokedTcs.TrySetResult();
            return Task.FromResult(0);
        }

        public Task<IncidentRecord> RecordIncidentAsync(CreateIncidentRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<IncidentRecord>> GetIncidentsAsync(int limit = 100, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IncidentRecord>>(Array.Empty<IncidentRecord>());
        public Task<IncidentRecord?> GetIncidentByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<IncidentRecord?>(null);
        public Task ExportMetadataReportAsync(Stream outputStream, IncidentExportFormat format = IncidentExportFormat.Json, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
        public Task<byte[]?> DecryptPayloadAsync(Guid incidentId, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]?>(null);
        public void Dispose() { }
    }

    private sealed class StubConfigurationManager : IConfigurationManager
    {
        public HidGuardConfiguration CurrentConfiguration { get; set; } = new();
        public event EventHandler<HidGuardConfiguration>? ConfigurationChanged;

        public Task<HidGuardConfiguration> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(CurrentConfiguration);

        public Task SaveAsync(HidGuardConfiguration configuration, CancellationToken cancellationToken = default)
        {
            CurrentConfiguration = configuration;
            ConfigurationChanged?.Invoke(this, configuration);
            return Task.CompletedTask;
        }

        public void TriggerConfigurationChanged(HidGuardConfiguration configuration)
        {
            CurrentConfiguration = configuration;
            ConfigurationChanged?.Invoke(this, configuration);
        }

        public void Dispose() { }
    }

    [Fact]
    public void ConstructorShouldThrowOnNullArguments()
    {
        var store = new StubIncidentStore();
        var buffer = new IncidentPayloadBuffer();
        var config = new StubConfigurationManager();

        Should.Throw<ArgumentNullException>(() => new IncidentMaintenanceService(null!, buffer, config));
        Should.Throw<ArgumentNullException>(() => new IncidentMaintenanceService(store, null!, config));
        Should.Throw<ArgumentNullException>(() => new IncidentMaintenanceService(store, buffer, null!));
    }

    [Fact]
    public void ConstructorShouldThrowOnNonPositivePurgeInterval()
    {
        var store = new StubIncidentStore();
        var buffer = new IncidentPayloadBuffer();
        var config = new StubConfigurationManager();

        Should.Throw<ArgumentOutOfRangeException>(() =>
            new IncidentMaintenanceService(store, buffer, config, purgeIntervalOverride: TimeSpan.Zero));
        Should.Throw<ArgumentOutOfRangeException>(() =>
            new IncidentMaintenanceService(store, buffer, config, purgeIntervalOverride: TimeSpan.FromSeconds(-1)));
    }

    [Fact]
    public async Task StartAppliesInitialConfigurationAndExecutesPurge()
    {
        var store = new StubIncidentStore();
        var buffer = new IncidentPayloadBuffer();
        var config = new StubConfigurationManager
        {
            CurrentConfiguration = new HidGuardConfiguration
            {
                Incidents = new IncidentStoreConfiguration
                {
                    EnableOptInPayloadCapture = true,
                    CaptureWindowKeystrokes = 42
                }
            }
        };

        var service = new IncidentMaintenanceService(
            store,
            buffer,
            config,
            purgeIntervalOverride: TimeSpan.FromMilliseconds(25));

        service.Start();
        service.IsRunning.ShouldBeTrue();

        store.CurrentConfiguration.CaptureWindowKeystrokes.ShouldBe(42);
        buffer.IsEnabled.ShouldBeTrue();

        await store.WaitForPurgeAsync(TimeSpan.FromSeconds(5));
        store.PurgeCallCount.ShouldBeGreaterThan(0);

        await service.StopAsync();
        service.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task StartIsIdempotent()
    {
        var store = new StubIncidentStore();
        var buffer = new IncidentPayloadBuffer();
        var config = new StubConfigurationManager();

        var service = new IncidentMaintenanceService(
            store,
            buffer,
            config,
            purgeIntervalOverride: TimeSpan.FromHours(1));

        service.Start();
        service.Start();
        service.IsRunning.ShouldBeTrue();

        await service.StopAsync();
        service.IsRunning.ShouldBeFalse();
    }

    [Fact]
    public async Task ConfigurationChangedUpdatesStoreAndPayloadBuffer()
    {
        var store = new StubIncidentStore();
        var buffer = new IncidentPayloadBuffer();
        var config = new StubConfigurationManager();

        var service = new IncidentMaintenanceService(
            store,
            buffer,
            config,
            purgeIntervalOverride: TimeSpan.FromHours(1));

        service.Start();

        var updated = new HidGuardConfiguration
        {
            Incidents = new IncidentStoreConfiguration
            {
                EnableOptInPayloadCapture = true,
                CaptureWindowKeystrokes = 128
            }
        };

        config.TriggerConfigurationChanged(updated);

        store.CurrentConfiguration.CaptureWindowKeystrokes.ShouldBe(128);
        buffer.IsEnabled.ShouldBeTrue();
        buffer.Capacity.ShouldBe(128);

        await service.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsyncStopsServiceAndPreventsRestart()
    {
        var store = new StubIncidentStore();
        var buffer = new IncidentPayloadBuffer();
        var config = new StubConfigurationManager();

        var service = new IncidentMaintenanceService(
            store,
            buffer,
            config,
            purgeIntervalOverride: TimeSpan.FromHours(1));

        service.Start();
        await service.DisposeAsync();
        await service.DisposeAsync();

        service.IsRunning.ShouldBeFalse();
        Should.Throw<ObjectDisposedException>(() => service.Start());
    }
}
