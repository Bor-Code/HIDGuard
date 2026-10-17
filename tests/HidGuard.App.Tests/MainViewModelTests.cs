using System.Windows.Forms;
using Shouldly;
using Xunit;
using HidGuard.App.Services;
using HidGuard.App.ViewModels;
using HidGuard.Core.Audit;
using HidGuard.Core.Configuration;
using HidGuard.Core.Domain;
using HidGuard.Core.Events;
using HidGuard.Core.Pipeline;
using HidGuard.Core.Trust;

namespace HidGuard.App.Tests;

public sealed class MainViewModelTests
{
    private sealed class StubUacElevationService : IUacElevationService
    {
        public bool IsProcessElevated => true;
        public bool RestartElevated() => true;
    }

    private sealed class StubTrayIconService : ITrayIconService
    {
        public void Initialize(Action onOpen, Action onToggle, Action onSettings, Action onAudit, Action onExit) { }
        public void UpdateProtectionState(bool isProtected) { }
        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info) { }
        public void Dispose() { }
    }

    private sealed class StubTrustStore : ITrustStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<DeviceTrustLevel> GetTrustLevelAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default) => Task.FromResult(DeviceTrustLevel.Trusted);
        public Task SetTrustLevelAsync(DeviceIdentifier deviceId, DeviceTrustLevel level, string enrolledBy, string? friendlyName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TrustRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TrustRecord>>(Array.Empty<TrustRecord>());
        public Task<int> EnrollInitialDevicesAsync(IEnumerable<DeviceIdentifier> devices, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
        public void Dispose() { }
    }

    private sealed class StubKeystrokePipeline : IKeystrokePipeline
    {
        public bool IsRunning => true;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public PipelineStatistics GetStatistics() => new();
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubAuditTrailService : IAuditTrailService
    {
        public event EventHandler<AuditEvent>? AuditEventRecorded;
        public Task RecordEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            AuditEventRecorded?.Invoke(this, auditEvent);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<AuditEvent>> QueryRecentEventsAsync(int limit = 100, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AuditEvent>>(Array.Empty<AuditEvent>());
        public void Dispose() { }
    }

    private sealed class StubConfigurationManager : IConfigurationManager
    {
        public HidGuardConfiguration CurrentConfiguration { get; } = new();
        public event EventHandler<HidGuardConfiguration>? ConfigurationChanged;
        public Task<HidGuardConfiguration> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(CurrentConfiguration);
        public Task SaveAsync(HidGuardConfiguration configuration, CancellationToken cancellationToken = default)
        {
            ConfigurationChanged?.Invoke(this, configuration);
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }

    [Fact]
    public void MainViewModelShouldNavigateBetweenTabs()
    {
        var pipeline = new StubKeystrokePipeline();
        var audit = new StubAuditTrailService();
        var trust = new StubTrustStore();
        var config = new StubConfigurationManager();
        var eventAggregator = new PipelineEventAggregator();

        var overview = new OverviewViewModel(pipeline, audit);
        var devices = new DevicesViewModel(trust);
        var metrics = new MetricsViewModel();
        var simulator = new SimulatorViewModel();
        var auditLog = new AuditLogViewModel(audit);
        var settings = new SettingsViewModel(config);

        var vm = new MainViewModel(
            overview,
            devices,
            metrics,
            simulator,
            auditLog,
            settings,
            new StubUacElevationService(),
            new StubTrayIconService(),
            eventAggregator,
            audit);

        vm.CurrentTabName.ShouldBe("Overview");
        vm.CurrentView.ShouldBe(overview);

        vm.NavigateCommand.Execute("Devices");
        vm.CurrentTabName.ShouldBe("Devices");
        vm.CurrentView.ShouldBe(devices);

        vm.NavigateCommand.Execute("Metrics");
        vm.CurrentTabName.ShouldBe("Metrics");
        vm.CurrentView.ShouldBe(metrics);

        vm.NavigateCommand.Execute("Simulator");
        vm.CurrentTabName.ShouldBe("Simulator");
        vm.CurrentView.ShouldBe(simulator);

        vm.NavigateCommand.Execute("AuditLog");
        vm.CurrentTabName.ShouldBe("AuditLog");
        vm.CurrentView.ShouldBe(auditLog);

        vm.NavigateCommand.Execute("Settings");
        vm.CurrentTabName.ShouldBe("Settings");
        vm.CurrentView.ShouldBe(settings);

        vm.NavigateCommand.Execute("Overview");
        vm.CurrentTabName.ShouldBe("Overview");
        vm.CurrentView.ShouldBe(overview);
    }

    [Fact]
    public void MainViewModelElevationPropertyShouldReflectService()
    {
        var pipeline = new StubKeystrokePipeline();
        var audit = new StubAuditTrailService();
        var trust = new StubTrustStore();
        var config = new StubConfigurationManager();
        var eventAggregator = new PipelineEventAggregator();

        var vm = new MainViewModel(
            new OverviewViewModel(pipeline, audit),
            new DevicesViewModel(trust),
            new MetricsViewModel(),
            new SimulatorViewModel(),
            new AuditLogViewModel(audit),
            new SettingsViewModel(config),
            new StubUacElevationService(),
            new StubTrayIconService(),
            eventAggregator,
            audit);

        vm.IsElevated.ShouldBeTrue();
    }

    [Fact]
    public void MainViewModelChangeThemeCommandShouldUpdateCurrentTheme()
    {
        var pipeline = new StubKeystrokePipeline();
        var audit = new StubAuditTrailService();
        var trust = new StubTrustStore();
        var config = new StubConfigurationManager();
        var eventAggregator = new PipelineEventAggregator();

        var vm = new MainViewModel(
            new OverviewViewModel(pipeline, audit),
            new DevicesViewModel(trust),
            new MetricsViewModel(),
            new SimulatorViewModel(),
            new AuditLogViewModel(audit),
            new SettingsViewModel(config),
            new StubUacElevationService(),
            new StubTrayIconService(),
            eventAggregator,
            audit);

        vm.CurrentTheme.ShouldBe("RedBlack");
        vm.ChangeThemeCommand.Execute("GreenBlack");
        vm.CurrentTheme.ShouldBe("GreenBlack");
        vm.ChangeThemeCommand.Execute("BlueBlack");
        vm.CurrentTheme.ShouldBe("BlueBlack");
    }
}
