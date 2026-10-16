using Shouldly;
using Xunit;
using HidGuard.Core.Audit;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Domain;
using HidGuard.Core.Policy;
using HidGuard.Core.Trust;

namespace HidGuard.Core.Tests;

public sealed class CountermeasureServiceTests
{
    private static readonly DeviceIdentifier MaliciousKeyboard = new(@"\\?\HID#VID_16C0&PID_05DF&MI_00#0000#{884b96c3-56ef-11d1-bc8c-00a0c91405dd}", 0x16C0, 0x05DF);

    private sealed class FakeWorkstationLocker : IWorkstationLocker
    {
        public bool LockAttempted { get; private set; }
        public bool ReturnValue { get; set; } = true;

        public bool LockWorkstation()
        {
            LockAttempted = true;
            return ReturnValue;
        }
    }

    private sealed class FakeDeviceDisabler : IDeviceDisabler
    {
        public DeviceIdentifier? DisabledDevice { get; private set; }
        public DeviceIdentifier? EnabledDevice { get; private set; }
        public bool ReturnValue { get; set; } = true;

        public Task<bool> DisableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
        {
            DisabledDevice = deviceId;
            return Task.FromResult(ReturnValue);
        }

        public Task<bool> EnableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
        {
            EnabledDevice = deviceId;
            return Task.FromResult(ReturnValue);
        }
    }

    private sealed class FakeKeystrokeInterceptor : IKeystrokeInterceptor
    {
        public bool IsSuppressionActive { get; set; }
        public bool IsStarted { get; private set; }
        public TimeSpan? LastTemporaryDuration { get; private set; }

        public event EventHandler<int>? KeySuppressed;

        public void StartInterception()
        {
            IsStarted = true;
        }

        public void StopInterception()
        {
            IsStarted = false;
        }

        public void SuppressTemporarily(TimeSpan duration)
        {
            LastTemporaryDuration = duration;
            IsSuppressionActive = true;
        }

        public void SimulateKeySuppression(int vkCode)
        {
            KeySuppressed?.Invoke(this, vkCode);
        }

        public void Dispose()
        {
        }
    }

    private sealed class FakeTrustStore : ITrustStore
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

        public Task<int> EnrollInitialDevicesAsync(IEnumerable<DeviceIdentifier> devices, CancellationToken cancellationToken = default) => Task.FromResult(0);

        public Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default) => Task.FromResult(Store.Count == 0);

        public void Dispose()
        {
        }
    }

    [Fact]
    public async Task ExecuteAsyncWhenActionIsAllowShouldNotLockOrBlock()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var service = new CountermeasureService(locker, disabler, interceptor, store);

        var decision = new PolicyDecision(PolicyAction.Allow, MaliciousKeyboard, DeviceTrustLevel.Trusted, "Normal keystroke");

        var result = await service.ExecuteAsync(decision, totalKeyboardsAttached: 2);

        result.Decision.ShouldBe(decision);
        result.WorkstationLockAttempted.ShouldBeFalse();
        result.DeviceDisableAttempted.ShouldBeFalse();
        result.Message.ShouldContain("Allowed");
        locker.LockAttempted.ShouldBeFalse();
        disabler.DisabledDevice.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsyncWhenActionIsAlertOnlyShouldNotLockOrBlock()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var service = new CountermeasureService(locker, disabler, interceptor, store);

        var decision = new PolicyDecision(PolicyAction.AlertOnly, MaliciousKeyboard, DeviceTrustLevel.Unknown, "Alert only");

        var result = await service.ExecuteAsync(decision, totalKeyboardsAttached: 2);

        result.WorkstationLockAttempted.ShouldBeFalse();
        result.DeviceDisableAttempted.ShouldBeFalse();
        result.Message.ShouldContain("Alert triggered");
        locker.LockAttempted.ShouldBeFalse();
        disabler.DisabledDevice.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsyncWhenActionIsPromptUserShouldReturnPromptMessage()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var service = new CountermeasureService(locker, disabler, interceptor, store);

        var decision = new PolicyDecision(PolicyAction.PromptUser, MaliciousKeyboard, DeviceTrustLevel.Unknown, "Prompt user");

        var result = await service.ExecuteAsync(decision, totalKeyboardsAttached: 2);

        result.WorkstationLockAttempted.ShouldBeFalse();
        result.DeviceDisableAttempted.ShouldBeFalse();
        result.Message.ShouldContain("User prompt requested");
    }

    [Fact]
    public async Task ExecuteAsyncWhenActionIsLockWorkstationShouldActivateSuppressionAndLock()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var service = new CountermeasureService(locker, disabler, interceptor, store);

        var decision = new PolicyDecision(PolicyAction.LockWorkstation, MaliciousKeyboard, DeviceTrustLevel.Unknown, "Attack detected");

        var result = await service.ExecuteAsync(decision, totalKeyboardsAttached: 1);

        result.WorkstationLockAttempted.ShouldBeTrue();
        result.WorkstationLockSucceeded.ShouldBeTrue();
        result.KeystrokeSuppressionActive.ShouldBeTrue();
        interceptor.IsSuppressionActive.ShouldBeTrue();
        locker.LockAttempted.ShouldBeTrue();
        disabler.DisabledDevice.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsyncWhenActionIsBlockDeviceAndMultipleKeyboardsShouldDisableDevice()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var service = new CountermeasureService(locker, disabler, interceptor, store);

        var decision = new PolicyDecision(PolicyAction.BlockDevice, MaliciousKeyboard, DeviceTrustLevel.Blocked, "Block device");

        var result = await service.ExecuteAsync(decision, totalKeyboardsAttached: 2);

        result.WorkstationLockAttempted.ShouldBeFalse();
        result.DeviceDisableAttempted.ShouldBeTrue();
        result.DeviceDisableSucceeded.ShouldBeTrue();
        result.KeystrokeSuppressionActive.ShouldBeTrue();
        disabler.DisabledDevice.ShouldBe(MaliciousKeyboard);
        (await store.GetTrustLevelAsync(MaliciousKeyboard)).ShouldBe(DeviceTrustLevel.Blocked);
    }

    [Fact]
    public async Task ExecuteAsyncWhenActionIsBlockDeviceAndSoleKeyboardAttachedShouldProtectKeyboardAndLockWorkstation()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var service = new CountermeasureService(locker, disabler, interceptor, store);

        var decision = new PolicyDecision(PolicyAction.BlockDevice, MaliciousKeyboard, DeviceTrustLevel.Blocked, "Block device");

        var result = await service.ExecuteAsync(decision, totalKeyboardsAttached: 1);

        result.WorkstationLockAttempted.ShouldBeTrue();
        result.WorkstationLockSucceeded.ShouldBeTrue();
        result.DeviceDisableAttempted.ShouldBeFalse();
        result.DeviceDisableSucceeded.ShouldBeFalse();
        result.Message.ShouldContain("Device disable skipped to prevent sole keyboard lockout");
        locker.LockAttempted.ShouldBeTrue();
        disabler.DisabledDevice.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsyncWhenActionIsLockAndBlockAndMultipleKeyboardsShouldLockAndDisable()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var service = new CountermeasureService(locker, disabler, interceptor, store);

        var decision = new PolicyDecision(PolicyAction.LockAndBlock, MaliciousKeyboard, DeviceTrustLevel.Blocked, "Lock and block");

        var result = await service.ExecuteAsync(decision, totalKeyboardsAttached: 2);

        result.WorkstationLockAttempted.ShouldBeTrue();
        result.WorkstationLockSucceeded.ShouldBeTrue();
        result.DeviceDisableAttempted.ShouldBeTrue();
        result.DeviceDisableSucceeded.ShouldBeTrue();
        locker.LockAttempted.ShouldBeTrue();
        disabler.DisabledDevice.ShouldBe(MaliciousKeyboard);
        (await store.GetTrustLevelAsync(MaliciousKeyboard)).ShouldBe(DeviceTrustLevel.Blocked);
    }

    [Fact]
    public async Task ExecuteAsyncWhenActionIsLockAndBlockAndSoleKeyboardShouldSkipDisable()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var service = new CountermeasureService(locker, disabler, interceptor, store);

        var decision = new PolicyDecision(PolicyAction.LockAndBlock, MaliciousKeyboard, DeviceTrustLevel.Blocked, "Lock and block");

        var result = await service.ExecuteAsync(decision, totalKeyboardsAttached: 1);

        result.WorkstationLockAttempted.ShouldBeTrue();
        result.WorkstationLockSucceeded.ShouldBeTrue();
        result.DeviceDisableAttempted.ShouldBeFalse();
        result.DeviceDisableSucceeded.ShouldBeFalse();
        result.Message.ShouldContain("Device disable skipped for sole attached keyboard");
        locker.LockAttempted.ShouldBeTrue();
        disabler.DisabledDevice.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsyncShouldRaiseCountermeasureExecutedEvent()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var service = new CountermeasureService(locker, disabler, interceptor, store);

        CountermeasureExecutionResult? capturedResult = null;
        service.CountermeasureExecuted += (_, r) => capturedResult = r;

        var decision = new PolicyDecision(PolicyAction.LockWorkstation, MaliciousKeyboard, DeviceTrustLevel.Blocked, "Injection");
        var result = await service.ExecuteAsync(decision, totalKeyboardsAttached: 2);

        capturedResult.ShouldNotBeNull();
        capturedResult.ShouldBe(result);
    }

    [Fact]
    public async Task ExecuteAsyncWithNullDecisionShouldThrow()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var service = new CountermeasureService(locker, disabler, interceptor, store);

        await Should.ThrowAsync<ArgumentNullException>(() =>
            service.ExecuteAsync(null!, 2));
    }

    private sealed class FakeWindowsEventLogService : IWindowsEventLogService
    {
        public List<(int EventId, SecurityEventSeverity Severity, string Message)> WrittenEvents { get; } = new();

        public bool WriteSecurityEvent(int eventId, SecurityEventSeverity severity, string message)
        {
            WrittenEvents.Add((eventId, severity, message));
            return true;
        }
    }

    [Fact]
    public async Task ExecuteAsyncWithEventLogShouldLogAttackDetectedOnLockWorkstation()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var eventLog = new FakeWindowsEventLogService();
        var service = new CountermeasureService(locker, disabler, interceptor, store, eventLog);

        var decision = new PolicyDecision(PolicyAction.LockWorkstation, MaliciousKeyboard, DeviceTrustLevel.Blocked, "Rapid injection detected");
        await service.ExecuteAsync(decision, totalKeyboardsAttached: 2);

        eventLog.WrittenEvents.Count.ShouldBe(1);
        eventLog.WrittenEvents[0].EventId.ShouldBe(SecurityEventIds.AttackDetected);
        eventLog.WrittenEvents[0].Severity.ShouldBe(SecurityEventSeverity.Warning);
        eventLog.WrittenEvents[0].Message.ShouldContain(MaliciousKeyboard.FormattedHardwareId);
    }

    [Fact]
    public async Task ExecuteAsyncWithEventLogShouldLogDeviceDisabledOnBlockDevice()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var eventLog = new FakeWindowsEventLogService();
        var service = new CountermeasureService(locker, disabler, interceptor, store, eventLog);

        var decision = new PolicyDecision(PolicyAction.BlockDevice, MaliciousKeyboard, DeviceTrustLevel.Blocked, "Persistent attack");
        await service.ExecuteAsync(decision, totalKeyboardsAttached: 2);

        eventLog.WrittenEvents.Count.ShouldBe(1);
        eventLog.WrittenEvents[0].EventId.ShouldBe(SecurityEventIds.DeviceDisabled);
        eventLog.WrittenEvents[0].Severity.ShouldBe(SecurityEventSeverity.Warning);
        eventLog.WrittenEvents[0].Message.ShouldContain("succeeded");
    }

    [Fact]
    public async Task ExecuteAsyncWithEventLogShouldLogAntiLockoutOnSoleKeyboard()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var eventLog = new FakeWindowsEventLogService();
        var service = new CountermeasureService(locker, disabler, interceptor, store, eventLog);

        var decision = new PolicyDecision(PolicyAction.BlockDevice, MaliciousKeyboard, DeviceTrustLevel.Blocked, "Attack on single keyboard");
        await service.ExecuteAsync(decision, totalKeyboardsAttached: 1);

        eventLog.WrittenEvents.Count.ShouldBe(1);
        eventLog.WrittenEvents[0].EventId.ShouldBe(SecurityEventIds.AntiLockoutTriggered);
        eventLog.WrittenEvents[0].Severity.ShouldBe(SecurityEventSeverity.Warning);
        eventLog.WrittenEvents[0].Message.ShouldContain("anti-lockout");
    }

    private sealed class FakeDevicePromptService : IDevicePromptService
    {
        public DeviceTrustLevel? DesiredResult { get; set; }
        public DeviceIdentifier? PromptedDevice { get; private set; }

        public Task<DeviceTrustLevel?> PromptDeviceTrustAsync(
            DeviceIdentifier deviceId,
            string? friendlyName = null,
            CancellationToken cancellationToken = default)
        {
            PromptedDevice = deviceId;
            return Task.FromResult(DesiredResult);
        }
    }

    [Fact]
    public async Task ExecuteAsyncWhenActionIsPromptUserAndUserSelectsTrustedShouldTrustDeviceAndLogReEnabled()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var eventLog = new FakeWindowsEventLogService();
        var promptService = new FakeDevicePromptService { DesiredResult = DeviceTrustLevel.Trusted };
        var service = new CountermeasureService(locker, disabler, interceptor, store, eventLog, promptService);

        var decision = new PolicyDecision(PolicyAction.PromptUser, MaliciousKeyboard, DeviceTrustLevel.Unknown, "Unknown device plugged in");
        var result = await service.ExecuteAsync(decision, totalKeyboardsAttached: 2);

        result.Message.ShouldContain("Device trusted");
        promptService.PromptedDevice.ShouldBe(MaliciousKeyboard);
        (await store.GetTrustLevelAsync(MaliciousKeyboard)).ShouldBe(DeviceTrustLevel.Trusted);
        disabler.DisabledDevice.ShouldBeNull();
        eventLog.WrittenEvents.ShouldContain(e => e.EventId == SecurityEventIds.DeviceReEnabled);
    }

    [Fact]
    public async Task ExecuteAsyncWhenActionIsPromptUserAndUserSelectsBlockedShouldBlockAndDisableDevice()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var eventLog = new FakeWindowsEventLogService();
        var promptService = new FakeDevicePromptService { DesiredResult = DeviceTrustLevel.Blocked };
        var service = new CountermeasureService(locker, disabler, interceptor, store, eventLog, promptService);

        var decision = new PolicyDecision(PolicyAction.PromptUser, MaliciousKeyboard, DeviceTrustLevel.Unknown, "Unknown device plugged in");
        var result = await service.ExecuteAsync(decision, totalKeyboardsAttached: 2);

        result.Message.ShouldContain("Device blocked and disabled");
        result.DeviceDisableAttempted.ShouldBeTrue();
        result.DeviceDisableSucceeded.ShouldBeTrue();
        promptService.PromptedDevice.ShouldBe(MaliciousKeyboard);
        (await store.GetTrustLevelAsync(MaliciousKeyboard)).ShouldBe(DeviceTrustLevel.Blocked);
        disabler.DisabledDevice.ShouldBe(MaliciousKeyboard);
        eventLog.WrittenEvents.ShouldContain(e => e.EventId == SecurityEventIds.DeviceDisabled);
    }

    [Fact]
    public async Task ExecuteAsyncWhenActionIsPromptUserAndUserCancelsShouldNotModifyTrustStore()
    {
        var locker = new FakeWorkstationLocker();
        var disabler = new FakeDeviceDisabler();
        var interceptor = new FakeKeystrokeInterceptor();
        using var store = new FakeTrustStore();
        var eventLog = new FakeWindowsEventLogService();
        var promptService = new FakeDevicePromptService { DesiredResult = null };
        var service = new CountermeasureService(locker, disabler, interceptor, store, eventLog, promptService);

        var decision = new PolicyDecision(PolicyAction.PromptUser, MaliciousKeyboard, DeviceTrustLevel.Unknown, "Unknown device plugged in");
        var result = await service.ExecuteAsync(decision, totalKeyboardsAttached: 2);

        result.Message.ShouldContain("User prompt requested");
        result.DeviceDisableAttempted.ShouldBeFalse();
        (await store.GetTrustLevelAsync(MaliciousKeyboard)).ShouldBe(DeviceTrustLevel.Unknown);
        disabler.DisabledDevice.ShouldBeNull();
        eventLog.WrittenEvents.ShouldBeEmpty();
    }
}
