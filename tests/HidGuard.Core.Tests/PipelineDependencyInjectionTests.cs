using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.DependencyInjection;
using HidGuard.Core.Domain;
using HidGuard.Core.Events;
using HidGuard.Core.Input;
using HidGuard.Core.Pipeline;
using HidGuard.Core.Policy;
using HidGuard.Core.Timing;
using HidGuard.Core.Trust;

namespace HidGuard.Core.Tests;

public sealed class PipelineDependencyInjectionTests
{
    private sealed class DummyKeystrokeSource : IKeystrokeSource
    {
        public event Action<KeystrokeEvent>? KeystrokeReceived { add { } remove { } }
        public event Action<DeviceIdentifier>? DeviceConnected { add { } remove { } }
        public event Action<DeviceIdentifier>? DeviceDisconnected { add { } remove { } }

        public bool IsRunning { get; private set; }

        public void StartListening() => IsRunning = true;
        public void StopListening() => IsRunning = false;
        public IReadOnlyList<DeviceIdentifier> GetConnectedKeyboards() => Array.Empty<DeviceIdentifier>();
        public void Dispose() { }
    }

    private sealed class DummyInterceptor : IKeystrokeInterceptor
    {
        public bool IsSuppressionActive { get; set; }
        public event EventHandler<int>? KeySuppressed { add { } remove { } }
        public void StartInterception() { }
        public void StopInterception() { }
        public void SuppressTemporarily(TimeSpan duration) { }
        public void Dispose() { }
    }

    private sealed class DummyWorkstationLocker : IWorkstationLocker
    {
        public bool LockWorkstation() => true;
    }

    private sealed class DummyDeviceDisabler : IDeviceDisabler
    {
        public Task<bool> DisableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task<bool> EnableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class DummyTrustStore : ITrustStore
    {
        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<DeviceTrustLevel> GetTrustLevelAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default) => Task.FromResult(DeviceTrustLevel.Trusted);
        public Task SetTrustLevelAsync(DeviceIdentifier deviceId, DeviceTrustLevel level, string enrolledBy, string? friendlyName = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TrustRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TrustRecord>>(Array.Empty<TrustRecord>());
        public Task<int> EnrollInitialDevicesAsync(IEnumerable<DeviceIdentifier> devices, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default) => Task.FromResult(false);
        public void Dispose() { }
    }

    [Fact]
    public void AddHidGuardCoreShouldResolveAllCoreDependencies()
    {
        var services = new ServiceCollection();
        var policyOptions = new PolicyOptions { EnforceMinimumOneActiveKeyboard = true };
        var timingThresholds = new TimingThresholds { WindowSize = 20 };

        services.AddHidGuardCore(policyOptions, timingThresholds);

        services.AddSingleton<IKeystrokeSource, DummyKeystrokeSource>();
        services.AddSingleton<IKeystrokeInterceptor, DummyInterceptor>();
        services.AddSingleton<IWorkstationLocker, DummyWorkstationLocker>();
        services.AddSingleton<IDeviceDisabler, DummyDeviceDisabler>();
        services.AddSingleton<ITrustStore, DummyTrustStore>();

        using var provider = services.BuildServiceProvider();

        var timingEngine = provider.GetService<ITimingAnalysisEngine>();
        timingEngine.ShouldNotBeNull();

        var policyEngine = provider.GetService<IPolicyEngine>();
        policyEngine.ShouldNotBeNull();

        var countermeasureService = provider.GetService<ICountermeasureService>();
        countermeasureService.ShouldNotBeNull();

        var eventAggregator = provider.GetService<IPipelineEventAggregator>();
        eventAggregator.ShouldNotBeNull();

        var pipeline = provider.GetService<IKeystrokePipeline>();
        pipeline.ShouldNotBeNull();
    }
}
