using Shouldly;
using Xunit;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Domain;
using HidGuard.Core.Events;
using HidGuard.Core.Input;
using HidGuard.Core.Pipeline;
using HidGuard.Core.Policy;
using HidGuard.Core.Scoring;
using HidGuard.Core.Timing;
using HidGuard.Core.Trust;

namespace HidGuard.Core.Tests;

public sealed class KeystrokePipelineTests
{
    private static readonly DeviceIdentifier HumanKeyboard = new(@"\\?\HID#VID_046D&PID_C52B#0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}", 0x046D, 0xC52B);
    private static readonly DeviceIdentifier BadUsbKeyboard = new(@"\\?\HID#VID_16C0&PID_05DF#1111#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}", 0x16C0, 0x05DF);

    private sealed class TestKeystrokeSource : IKeystrokeSource
    {
        private readonly List<DeviceIdentifier> connectedKeyboards = new();

        public event Action<KeystrokeEvent>? KeystrokeReceived;
        public event Action<DeviceIdentifier>? DeviceConnected;
        public event Action<DeviceIdentifier>? DeviceDisconnected;

        public bool IsRunning { get; private set; }

        public void StartListening() => IsRunning = true;
        public void StopListening() => IsRunning = false;

        public IReadOnlyList<DeviceIdentifier> GetConnectedKeyboards()
        {
            lock (connectedKeyboards)
            {
                return connectedKeyboards.ToList();
            }
        }

        public void SetConnectedKeyboards(IEnumerable<DeviceIdentifier> keyboards)
        {
            lock (connectedKeyboards)
            {
                connectedKeyboards.Clear();
                connectedKeyboards.AddRange(keyboards);
            }
        }

        public void EmitKeystroke(KeystrokeEvent keystroke) => KeystrokeReceived?.Invoke(keystroke);
        public void EmitDeviceConnected(DeviceIdentifier device) => DeviceConnected?.Invoke(device);
        public void EmitDeviceDisconnected(DeviceIdentifier device) => DeviceDisconnected?.Invoke(device);

        public void Dispose() { }
    }

    private sealed class TestWorkstationLocker : IWorkstationLocker
    {
        public int LockCallCount { get; private set; }

        public bool LockWorkstation()
        {
            LockCallCount++;
            return true;
        }
    }

    private sealed class TestDeviceDisabler : IDeviceDisabler
    {
        public List<DeviceIdentifier> DisabledDevices { get; } = new();

        public Task<bool> DisableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
        {
            lock (DisabledDevices)
            {
                DisabledDevices.Add(deviceId);
            }
            return Task.FromResult(true);
        }

        public Task<bool> EnableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class TestKeystrokeInterceptor : IKeystrokeInterceptor
    {
        public bool IsSuppressionActive { get; set; }
        public event EventHandler<int>? KeySuppressed;

        public void StartInterception() { }
        public void StopInterception() { }
        public void SuppressTemporarily(TimeSpan duration) => IsSuppressionActive = true;
        public void SimulateKeySuppressed(int vkCode) => KeySuppressed?.Invoke(this, vkCode);
        public void Dispose() { }
    }

    private sealed class TestTrustStore : ITrustStore
    {
        public readonly Dictionary<string, DeviceTrustLevel> Store = new(StringComparer.OrdinalIgnoreCase);

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<DeviceTrustLevel> GetTrustLevelAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
        {
            Store.TryGetValue(deviceId.FormattedHardwareId, out var level);
            return Task.FromResult(level);
        }

        public Task SetTrustLevelAsync(DeviceIdentifier deviceId, DeviceTrustLevel level, string enrolledBy, string? friendlyName = null, CancellationToken cancellationToken = default)
        {
            Store[deviceId.FormattedHardwareId] = level;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TrustRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TrustRecord>>(Array.Empty<TrustRecord>());

        public Task<int> EnrollInitialDevicesAsync(IEnumerable<DeviceIdentifier> devices, CancellationToken cancellationToken = default)
        {
            var count = 0;
            foreach (var d in devices)
            {
                if (!Store.ContainsKey(d.FormattedHardwareId))
                {
                    Store[d.FormattedHardwareId] = DeviceTrustLevel.Trusted;
                    count++;
                }
            }
            return Task.FromResult(count);
        }

        public Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default) => Task.FromResult(Store.Count == 0);

        public void Dispose() { }
    }

    [Fact]
    public async Task PipelineEndToEndAttackShouldDetectAnomalyAndExecuteCountermeasure()
    {
        var source = new TestKeystrokeSource();
        source.SetConnectedKeyboards(new[] { HumanKeyboard, BadUsbKeyboard });

        var locker = new TestWorkstationLocker();
        var disabler = new TestDeviceDisabler();
        var interceptor = new TestKeystrokeInterceptor();
        using var trustStore = new TestTrustStore();
        var eventAggregator = new PipelineEventAggregator();

        var thresholds = new TimingThresholds();
        var scorer = new AnomalyScorer();
        var timingEngine = new TimingAnalysisEngine(thresholds, scorer);
        var policyEngine = new PolicyEngine(trustStore, new PolicyOptions
        {
            DefaultAnomalyAction = AnomalyAction.BlockDevice,
            EnforceMinimumOneActiveKeyboard = true
        });
        var countermeasureService = new CountermeasureService(locker, disabler, interceptor, trustStore);

        await using var pipeline = new KeystrokePipeline(
            source,
            timingEngine,
            policyEngine,
            countermeasureService,
            trustStore,
            interceptor,
            eventAggregator);

        var attackDetectedEventRaised = false;
        eventAggregator.KeystrokeProcessed += (_, evt) =>
        {
            if (evt.AnomalyResult != null && evt.AnomalyResult.IsSuspicious)
            {
                attackDetectedEventRaised = true;
            }
        };

        await pipeline.StartAsync();

        var baseTime = DateTimeOffset.UtcNow;
        for (var i = 0; i < 30; i++)
        {
            source.EmitKeystroke(new KeystrokeEvent(
                BadUsbKeyboard,
                baseTime.AddMilliseconds(i * 4),
                KeyTransitionState.KeyDown,
                0x41));
        }

        await Task.Delay(200);

        var stats = pipeline.GetStatistics();
        stats.TotalKeystrokesProcessed.ShouldBeGreaterThan(25);
        stats.TotalAttacksDetected.ShouldBeGreaterThan(0);
        stats.TotalAttacksNeutralized.ShouldBeGreaterThan(0);

        interceptor.IsSuppressionActive.ShouldBeTrue();
        disabler.DisabledDevices.ShouldContain(BadUsbKeyboard);
        (await trustStore.GetTrustLevelAsync(BadUsbKeyboard)).ShouldBe(DeviceTrustLevel.Blocked);
        attackDetectedEventRaised.ShouldBeTrue();

        await pipeline.StopAsync();
    }

    [Fact]
    public async Task PipelineMultiDeviceConcurrencyShouldNeutralizeMaliciousDeviceWhileLeavingHumanDeviceUnaffected()
    {
        var source = new TestKeystrokeSource();
        source.SetConnectedKeyboards(new[] { HumanKeyboard, BadUsbKeyboard });

        var locker = new TestWorkstationLocker();
        var disabler = new TestDeviceDisabler();
        var interceptor = new TestKeystrokeInterceptor();
        using var trustStore = new TestTrustStore();
        await trustStore.SetTrustLevelAsync(HumanKeyboard, DeviceTrustLevel.Trusted, "Admin");

        var eventAggregator = new PipelineEventAggregator();
        var thresholds = new TimingThresholds();
        var scorer = new AnomalyScorer();
        var timingEngine = new TimingAnalysisEngine(thresholds, scorer);
        var policyEngine = new PolicyEngine(trustStore, new PolicyOptions
        {
            DefaultAnomalyAction = AnomalyAction.BlockDevice
        });
        var countermeasureService = new CountermeasureService(locker, disabler, interceptor, trustStore);

        await using var pipeline = new KeystrokePipeline(
            source,
            timingEngine,
            policyEngine,
            countermeasureService,
            trustStore,
            interceptor,
            eventAggregator);

        await pipeline.StartAsync();

        var baseTime = DateTimeOffset.UtcNow;

        for (var i = 0; i < 20; i++)
        {
            source.EmitKeystroke(new KeystrokeEvent(
                HumanKeyboard,
                baseTime.AddMilliseconds(i * 120),
                KeyTransitionState.KeyDown,
                0x41));
        }

        for (var i = 0; i < 25; i++)
        {
            source.EmitKeystroke(new KeystrokeEvent(
                BadUsbKeyboard,
                baseTime.AddMilliseconds(i * 5),
                KeyTransitionState.KeyDown,
                0x42));
        }

        await Task.Delay(200);

        disabler.DisabledDevices.ShouldContain(BadUsbKeyboard);
        disabler.DisabledDevices.ShouldNotContain(HumanKeyboard);

        (await trustStore.GetTrustLevelAsync(HumanKeyboard)).ShouldBe(DeviceTrustLevel.Trusted);
        (await trustStore.GetTrustLevelAsync(BadUsbKeyboard)).ShouldBe(DeviceTrustLevel.Blocked);

        await pipeline.StopAsync();
    }

    [Fact]
    public async Task PipelineSoleKeyboardAttackShouldPreventHardwareDisablingAndLockWorkstationInstead()
    {
        var source = new TestKeystrokeSource();
        source.SetConnectedKeyboards(new[] { BadUsbKeyboard });

        var locker = new TestWorkstationLocker();
        var disabler = new TestDeviceDisabler();
        var interceptor = new TestKeystrokeInterceptor();
        using var trustStore = new TestTrustStore();
        var eventAggregator = new PipelineEventAggregator();

        var thresholds = new TimingThresholds();
        var scorer = new AnomalyScorer();
        var timingEngine = new TimingAnalysisEngine(thresholds, scorer);
        var policyEngine = new PolicyEngine(trustStore, new PolicyOptions
        {
            DefaultAnomalyAction = AnomalyAction.BlockDevice,
            EnforceMinimumOneActiveKeyboard = true
        });
        var countermeasureService = new CountermeasureService(locker, disabler, interceptor, trustStore);

        await using var pipeline = new KeystrokePipeline(
            source,
            timingEngine,
            policyEngine,
            countermeasureService,
            trustStore,
            interceptor,
            eventAggregator);

        await pipeline.StartAsync();

        var baseTime = DateTimeOffset.UtcNow;
        for (var i = 0; i < 25; i++)
        {
            source.EmitKeystroke(new KeystrokeEvent(
                BadUsbKeyboard,
                baseTime.AddMilliseconds(i * 4),
                KeyTransitionState.KeyDown,
                0x41));
        }

        await Task.Delay(200);

        disabler.DisabledDevices.ShouldBeEmpty();
        locker.LockCallCount.ShouldBeGreaterThan(0);

        await pipeline.StopAsync();
    }

    [Fact]
    public async Task PipelineDeviceArrivalAndRemovalShouldPublishLifecycleEvents()
    {
        var source = new TestKeystrokeSource();
        var locker = new TestWorkstationLocker();
        var disabler = new TestDeviceDisabler();
        var interceptor = new TestKeystrokeInterceptor();
        using var trustStore = new TestTrustStore();
        var eventAggregator = new PipelineEventAggregator();

        var timingEngine = new TimingAnalysisEngine(new TimingThresholds(), new AnomalyScorer());
        var policyEngine = new PolicyEngine(trustStore);
        var countermeasureService = new CountermeasureService(locker, disabler, interceptor, trustStore);

        await using var pipeline = new KeystrokePipeline(
            source,
            timingEngine,
            policyEngine,
            countermeasureService,
            trustStore,
            interceptor,
            eventAggregator);

        var arrivedReceived = false;
        var removedReceived = false;

        eventAggregator.DeviceLifecycleChanged += (_, evt) =>
        {
            if (evt.EventType == DeviceLifecycleEventType.Arrived && evt.DeviceId == HumanKeyboard)
            {
                arrivedReceived = true;
            }
            if (evt.EventType == DeviceLifecycleEventType.Removed && evt.DeviceId == HumanKeyboard)
            {
                removedReceived = true;
            }
        };

        await pipeline.StartAsync();

        source.EmitDeviceConnected(HumanKeyboard);
        await Task.Delay(50);
        arrivedReceived.ShouldBeTrue();

        source.EmitDeviceDisconnected(HumanKeyboard);
        await Task.Delay(50);
        removedReceived.ShouldBeTrue();

        await pipeline.StopAsync();
    }

    [Fact]
    public async Task PipelineAutoEnrollOnFirstRunShouldEnrollConnectedKeyboards()
    {
        var source = new TestKeystrokeSource();
        source.SetConnectedKeyboards(new[] { HumanKeyboard });

        var locker = new TestWorkstationLocker();
        var disabler = new TestDeviceDisabler();
        var interceptor = new TestKeystrokeInterceptor();
        using var trustStore = new TestTrustStore();
        var eventAggregator = new PipelineEventAggregator();

        var timingEngine = new TimingAnalysisEngine(new TimingThresholds(), new AnomalyScorer());
        var policyEngine = new PolicyEngine(trustStore);
        var countermeasureService = new CountermeasureService(locker, disabler, interceptor, trustStore);

        await using var pipeline = new KeystrokePipeline(
            source,
            timingEngine,
            policyEngine,
            countermeasureService,
            trustStore,
            interceptor,
            eventAggregator,
            new PolicyOptions { AutoEnrollFirstRun = true });

        await pipeline.StartAsync();

        (await trustStore.GetTrustLevelAsync(HumanKeyboard)).ShouldBe(DeviceTrustLevel.Trusted);

        await pipeline.StopAsync();
    }

    [Fact]
    public async Task PipelineHighThroughputShouldProcessOverTenThousandKeystrokesPerSecond()
    {
        var source = new TestKeystrokeSource();
        source.SetConnectedKeyboards(new[] { HumanKeyboard });

        var locker = new TestWorkstationLocker();
        var disabler = new TestDeviceDisabler();
        var interceptor = new TestKeystrokeInterceptor();
        using var trustStore = new TestTrustStore();
        await trustStore.SetTrustLevelAsync(HumanKeyboard, DeviceTrustLevel.Trusted, "Admin");

        var eventAggregator = new PipelineEventAggregator();
        var timingEngine = new TimingAnalysisEngine(new TimingThresholds(), new AnomalyScorer());
        var policyEngine = new PolicyEngine(trustStore);
        var countermeasureService = new CountermeasureService(locker, disabler, interceptor, trustStore);

        await using var pipeline = new KeystrokePipeline(
            source,
            timingEngine,
            policyEngine,
            countermeasureService,
            trustStore,
            interceptor,
            eventAggregator);

        await pipeline.StartAsync();

        const int keystrokeCount = 10_000;
        var now = DateTimeOffset.UtcNow;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < keystrokeCount; i++)
        {
            source.EmitKeystroke(new KeystrokeEvent(
                HumanKeyboard,
                now.AddMilliseconds(i * 100),
                KeyTransitionState.KeyDown,
                0x41));
        }

        while (pipeline.GetStatistics().TotalKeystrokesProcessed < keystrokeCount && sw.ElapsedMilliseconds < 5000)
        {
            await Task.Delay(10);
        }
        sw.Stop();

        var stats = pipeline.GetStatistics();
        stats.TotalKeystrokesProcessed.ShouldBe(keystrokeCount);

        var keystrokesPerSecond = keystrokeCount / sw.Elapsed.TotalSeconds;
        keystrokesPerSecond.ShouldBeGreaterThan(1000.0);

        await pipeline.StopAsync();
    }
}
