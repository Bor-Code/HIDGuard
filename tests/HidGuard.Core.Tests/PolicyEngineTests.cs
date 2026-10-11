using Shouldly;
using Xunit;
using HidGuard.Core.Domain;
using HidGuard.Core.Policy;
using HidGuard.Core.Scoring;
using HidGuard.Core.Timing;
using HidGuard.Core.Trust;

namespace HidGuard.Core.Tests;

public sealed class PolicyEngineTests
{
    private static readonly DeviceIdentifier TestDevice = new(@"\\?\HID#VID_1234&PID_5678#0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}", 0x1234, 0x5678);

    private sealed class FakeTrustStore : ITrustStore
    {
        private readonly Dictionary<string, DeviceTrustLevel> levels = new(StringComparer.OrdinalIgnoreCase);

        public void SetLevel(DeviceIdentifier device, DeviceTrustLevel level)
        {
            levels[device.FormattedHardwareId] = level;
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<DeviceTrustLevel> GetTrustLevelAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
        {
            levels.TryGetValue(deviceId.FormattedHardwareId, out var level);
            return Task.FromResult(level);
        }

        public Task SetTrustLevelAsync(DeviceIdentifier deviceId, DeviceTrustLevel level, string enrolledBy, string? friendlyName = null, CancellationToken cancellationToken = default)
        {
            levels[deviceId.FormattedHardwareId] = level;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TrustRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TrustRecord>>(Array.Empty<TrustRecord>());
        }

        public Task<int> EnrollInitialDevicesAsync(IEnumerable<DeviceIdentifier> devices, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default) => Task.FromResult(levels.Count == 0);

        public void Dispose()
        {
        }
    }

    private static AnomalyScoreResult CreateAnomalyResult(bool isSuspicious, double score = 0.95)
    {
        var stats = new SlidingWindowStatistics
        {
            SampleCount = 20,
            MeanIntervalMs = 5.0,
            StandardDeviationMs = 0.5,
            SubHumanIntervalRatio = 1.0,
            CoefficientOfVariation = 0.1,
            MinimumIntervalMs = 4.0,
            BurstLength = 20
        };

        return new AnomalyScoreResult
        {
            Score = score,
            IsSuspicious = isSuspicious,
            TriggeredSignals = isSuspicious
                ? new[] { new DetectionSignal(DetectionSignalType.SubHumanIntervalRate, 1.0, 0.4, 0.5, "Sub-human rate") }
                : Array.Empty<DetectionSignal>(),
            Statistics = stats,
            EvaluatedAt = DateTimeOffset.UtcNow
        };
    }

    [Fact]
    public async Task EvaluateAsyncWhenDeviceIsBlockedAndMultipleKeyboardsShouldReturnBlockDevice()
    {
        using var store = new FakeTrustStore();
        store.SetLevel(TestDevice, DeviceTrustLevel.Blocked);
        var engine = new PolicyEngine(store);

        var decision = await engine.EvaluateAsync(TestDevice, anomalyResult: null, totalKeyboardsAttached: 2);

        decision.Action.ShouldBe(PolicyAction.BlockDevice);
        decision.DeviceId.ShouldBe(TestDevice);
        decision.TrustLevel.ShouldBe(DeviceTrustLevel.Blocked);
    }

    [Fact]
    public async Task EvaluateAsyncWhenDeviceIsBlockedAndSoleKeyboardAttachedShouldPreventLockoutWithAlertOnly()
    {
        using var store = new FakeTrustStore();
        store.SetLevel(TestDevice, DeviceTrustLevel.Blocked);
        var engine = new PolicyEngine(store, new PolicyOptions { EnforceMinimumOneActiveKeyboard = true });

        var decision = await engine.EvaluateAsync(TestDevice, anomalyResult: null, totalKeyboardsAttached: 1);

        decision.Action.ShouldBe(PolicyAction.AlertOnly);
        decision.Reason.ShouldContain("sole keyboard attached");
    }

    [Fact]
    public async Task EvaluateAsyncWhenAnomalyDetectedAndMultipleKeyboardsWithBlockPolicyShouldReturnBlockDevice()
    {
        using var store = new FakeTrustStore();
        store.SetLevel(TestDevice, DeviceTrustLevel.Trusted);
        var options = new PolicyOptions { DefaultAnomalyAction = AnomalyAction.BlockDevice };
        var engine = new PolicyEngine(store, options);
        var anomaly = CreateAnomalyResult(isSuspicious: true);

        var decision = await engine.EvaluateAsync(TestDevice, anomaly, totalKeyboardsAttached: 2);

        decision.Action.ShouldBe(PolicyAction.BlockDevice);
        decision.AnomalyResult.ShouldBe(anomaly);
    }

    [Fact]
    public async Task EvaluateAsyncWhenAnomalyDetectedAndSoleKeyboardAttachedWithBlockPolicyShouldDowngradeToLockWorkstation()
    {
        using var store = new FakeTrustStore();
        store.SetLevel(TestDevice, DeviceTrustLevel.Trusted);
        var options = new PolicyOptions { DefaultAnomalyAction = AnomalyAction.BlockDevice };
        var engine = new PolicyEngine(store, options);
        var anomaly = CreateAnomalyResult(isSuspicious: true);

        var decision = await engine.EvaluateAsync(TestDevice, anomaly, totalKeyboardsAttached: 1);

        decision.Action.ShouldBe(PolicyAction.LockWorkstation);
        decision.Reason.ShouldContain("Workstation locked instead of disabling the only remaining keyboard");
    }

    [Fact]
    public async Task EvaluateAsyncWhenAnomalyDetectedAndSoleKeyboardAttachedWithLockAndBlockShouldDowngradeToLockWorkstation()
    {
        using var store = new FakeTrustStore();
        store.SetLevel(TestDevice, DeviceTrustLevel.Trusted);
        var options = new PolicyOptions { DefaultAnomalyAction = AnomalyAction.LockAndBlock };
        var engine = new PolicyEngine(store, options);
        var anomaly = CreateAnomalyResult(isSuspicious: true);

        var decision = await engine.EvaluateAsync(TestDevice, anomaly, totalKeyboardsAttached: 1);

        decision.Action.ShouldBe(PolicyAction.LockWorkstation);
        decision.Reason.ShouldContain("device disable skipped for sole attached keyboard");
    }

    [Fact]
    public async Task EvaluateAsyncWhenDeviceIsTrustedAndNoAnomalyShouldReturnAllow()
    {
        using var store = new FakeTrustStore();
        store.SetLevel(TestDevice, DeviceTrustLevel.Trusted);
        var engine = new PolicyEngine(store);

        var decision = await engine.EvaluateAsync(TestDevice, anomalyResult: null, totalKeyboardsAttached: 1);

        decision.Action.ShouldBe(PolicyAction.Allow);
        decision.TrustLevel.ShouldBe(DeviceTrustLevel.Trusted);
    }

    [Fact]
    public async Task EvaluateAsyncWhenDeviceIsUnknownAndDefaultActionIsAllowWithObservationShouldReturnAllow()
    {
        using var store = new FakeTrustStore();
        var options = new PolicyOptions { DefaultUnknownDeviceAction = UnknownDeviceAction.AllowWithObservation };
        var engine = new PolicyEngine(store, options);

        var decision = await engine.EvaluateAsync(TestDevice, anomalyResult: null, totalKeyboardsAttached: 2);

        decision.Action.ShouldBe(PolicyAction.Allow);
        decision.TrustLevel.ShouldBe(DeviceTrustLevel.Unknown);
    }

    [Fact]
    public async Task EvaluateAsyncWhenDeviceIsUnknownAndDefaultActionIsPromptUserShouldReturnPromptUser()
    {
        using var store = new FakeTrustStore();
        var options = new PolicyOptions { DefaultUnknownDeviceAction = UnknownDeviceAction.PromptUser };
        var engine = new PolicyEngine(store, options);

        var decision = await engine.EvaluateAsync(TestDevice, anomalyResult: null, totalKeyboardsAttached: 2);

        decision.Action.ShouldBe(PolicyAction.PromptUser);
    }

    [Fact]
    public async Task EvaluateAsyncWhenDeviceIsUnknownAndDefaultActionIsBlockImmediatelyAndMultipleKeyboardsShouldReturnBlockDevice()
    {
        using var store = new FakeTrustStore();
        var options = new PolicyOptions { DefaultUnknownDeviceAction = UnknownDeviceAction.BlockImmediately };
        var engine = new PolicyEngine(store, options);

        var decision = await engine.EvaluateAsync(TestDevice, anomalyResult: null, totalKeyboardsAttached: 2);

        decision.Action.ShouldBe(PolicyAction.BlockDevice);
    }

    [Fact]
    public async Task EvaluateAsyncWhenDeviceIsUnknownAndDefaultActionIsBlockImmediatelyAndSoleKeyboardShouldPromptUser()
    {
        using var store = new FakeTrustStore();
        var options = new PolicyOptions { DefaultUnknownDeviceAction = UnknownDeviceAction.BlockImmediately };
        var engine = new PolicyEngine(store, options);

        var decision = await engine.EvaluateAsync(TestDevice, anomalyResult: null, totalKeyboardsAttached: 1);

        decision.Action.ShouldBe(PolicyAction.PromptUser);
        decision.Reason.ShouldContain("Prompting user instead");
    }

    [Fact]
    public async Task EvaluateAsyncWithNullDeviceShouldThrow()
    {
        using var store = new FakeTrustStore();
        var engine = new PolicyEngine(store);

        await Should.ThrowAsync<ArgumentNullException>(() =>
            engine.EvaluateAsync(null!, null, 1));
    }

    [Fact]
    public void PolicyOptionsValidationShouldThrowForInvalidEnums()
    {
        var invalidUnknown = new PolicyOptions { DefaultUnknownDeviceAction = (UnknownDeviceAction)999 };
        Should.Throw<ArgumentOutOfRangeException>(() => invalidUnknown.Validate());

        var invalidAnomaly = new PolicyOptions { DefaultAnomalyAction = (AnomalyAction)999 };
        Should.Throw<ArgumentOutOfRangeException>(() => invalidAnomaly.Validate());
    }
}
