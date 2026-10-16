using Shouldly;
using Xunit;
using HidGuard.App.ViewModels;
using HidGuard.Core.Audit;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Domain;
using HidGuard.Core.Events;
using HidGuard.Core.Trust;

namespace HidGuard.App.Tests;

public sealed class DevicesViewModelTests
{
    private sealed class StubTrustStore : ITrustStore
    {
        private readonly List<TrustRecord> records = new();

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<DeviceTrustLevel> GetTrustLevelAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
        {
            var existing = records.FirstOrDefault(r => r.DeviceId.Equals(deviceId));
            return Task.FromResult(existing?.TrustLevel ?? DeviceTrustLevel.Unknown);
        }

        public Task SetTrustLevelAsync(DeviceIdentifier deviceId, DeviceTrustLevel level, string enrolledBy, string? friendlyName = null, CancellationToken cancellationToken = default)
        {
            records.RemoveAll(r => r.DeviceId.Equals(deviceId));
            records.Add(new TrustRecord(deviceId, level, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, enrolledBy, friendlyName));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TrustRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<TrustRecord>>(records);
        }

        public Task<int> EnrollInitialDevicesAsync(IEnumerable<DeviceIdentifier> devices, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(0);
        }

        public Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(records.Count == 0);
        }

        public void Dispose() { }
    }

    private sealed class StubDeviceDisabler : IDeviceDisabler
    {
        public List<DeviceIdentifier> DisabledDevices { get; } = new();
        public List<DeviceIdentifier> EnabledDevices { get; } = new();

        public Task<bool> DisableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
        {
            DisabledDevices.Add(deviceId);
            return Task.FromResult(true);
        }

        public Task<bool> EnableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default)
        {
            EnabledDevices.Add(deviceId);
            return Task.FromResult(true);
        }
    }

    private sealed class StubEventLogService : IWindowsEventLogService
    {
        public List<(int EventId, SecurityEventSeverity Severity, string Message)> LoggedEntries { get; } = new();

        public bool WriteSecurityEvent(int eventId, SecurityEventSeverity severity, string message)
        {
            LoggedEntries.Add((eventId, severity, message));
            return true;
        }
    }

    [Fact]
    public async Task DevicesViewModelShouldLoadDevicesAndHandleTrustChange()
    {
        var store = new StubTrustStore();
        var dev = new DeviceIdentifier("HID\\VID_046D&PID_C31C\\0", 0x046D, 0xC31C);
        await store.SetTrustLevelAsync(dev, DeviceTrustLevel.Trusted, "Admin", "Logitech Keyboard");

        var vm = new DevicesViewModel(store);
        await vm.RefreshDevicesAsync();

        vm.Devices.Count.ShouldBe(1);
        var item = vm.Devices[0];
        item.TrustLevel.ShouldBe(DeviceTrustLevel.Trusted);
        item.HardwareId.ShouldBe("VID_046D&PID_C31C");

        item.BlockCommand.Execute(null);

        await Task.Delay(50);

        item.TrustLevel.ShouldBe(DeviceTrustLevel.Blocked);
    }

    [Fact]
    public void HandleDeviceLifecycleEventShouldAddNewDeviceWhenUnknown()
    {
        var store = new StubTrustStore();
        var vm = new DevicesViewModel(store);

        var dev = new DeviceIdentifier("HID\\VID_16C0&PID_05DF\\0", 0x16C0, 0x05DF);
        var lifecycleEvt = new DeviceLifecycleEvent(dev, DeviceLifecycleEventType.Arrived, DateTimeOffset.UtcNow);

        vm.HandleDeviceLifecycleEvent(lifecycleEvt);

        vm.Devices.Count.ShouldBe(1);
        vm.Devices[0].HardwareId.ShouldBe("VID_16C0&PID_05DF");
        vm.Devices[0].TrustLevel.ShouldBe(DeviceTrustLevel.Unknown);
    }

    [Fact]
    public async Task DevicesViewModelShouldDisableDeviceAndLogWhenBlocked()
    {
        var store = new StubTrustStore();
        var disabler = new StubDeviceDisabler();
        var eventLog = new StubEventLogService();
        var dev = new DeviceIdentifier("HID\\VID_046D&PID_C31C\\0", 0x046D, 0xC31C);
        await store.SetTrustLevelAsync(dev, DeviceTrustLevel.Trusted, "Admin", "Logitech Keyboard");

        var vm = new DevicesViewModel(store, disabler, eventLog);
        await vm.RefreshDevicesAsync();

        await vm.OnDeviceTrustChangedAsync(dev.Path, DeviceTrustLevel.Blocked);

        disabler.DisabledDevices.ShouldContain(dev);
        eventLog.LoggedEntries.ShouldContain(e => e.EventId == SecurityEventIds.DeviceDisabled && e.Severity == SecurityEventSeverity.Warning);
    }

    [Fact]
    public async Task DevicesViewModelShouldEnableDeviceAndLogWhenTrusted()
    {
        var store = new StubTrustStore();
        var disabler = new StubDeviceDisabler();
        var eventLog = new StubEventLogService();
        var dev = new DeviceIdentifier("HID\\VID_046D&PID_C31C\\0", 0x046D, 0xC31C);
        await store.SetTrustLevelAsync(dev, DeviceTrustLevel.Blocked, "Admin", "Logitech Keyboard");

        var vm = new DevicesViewModel(store, disabler, eventLog);
        await vm.RefreshDevicesAsync();

        await vm.OnDeviceTrustChangedAsync(dev.Path, DeviceTrustLevel.Trusted);

        disabler.EnabledDevices.ShouldContain(dev);
        eventLog.LoggedEntries.ShouldContain(e => e.EventId == SecurityEventIds.DeviceReEnabled && e.Severity == SecurityEventSeverity.Information);
    }

    [Fact]
    public async Task DevicesViewModelShouldReEnableBlockedDeviceAndSetTrustToTrusted()
    {
        var store = new StubTrustStore();
        var disabler = new StubDeviceDisabler();
        var eventLog = new StubEventLogService();
        var dev = new DeviceIdentifier("HID\\VID_046D&PID_C31C\\0", 0x046D, 0xC31C);
        await store.SetTrustLevelAsync(dev, DeviceTrustLevel.Blocked, "Admin", "Logitech Keyboard");

        var vm = new DevicesViewModel(store, disabler, eventLog);
        await vm.RefreshDevicesAsync();

        var item = vm.Devices[0];
        item.TrustLevel.ShouldBe(DeviceTrustLevel.Blocked);

        await vm.OnDeviceReEnableRequestedAsync(dev.Path);

        disabler.EnabledDevices.ShouldContain(dev);
        eventLog.LoggedEntries.ShouldContain(e => e.EventId == SecurityEventIds.DeviceReEnabled && e.Severity == SecurityEventSeverity.Information);
        var record = (await store.GetAllRecordsAsync())[0];
        record.TrustLevel.ShouldBe(DeviceTrustLevel.Trusted);
        item.TrustLevel.ShouldBe(DeviceTrustLevel.Trusted);
    }

    [Fact]
    public async Task DeviceItemViewModelReEnableCommandShouldInvokeReEnableCallback()
    {
        var reEnabledPath = string.Empty;
        var item = new DeviceItemViewModel(
            "HID\\VID_1234&PID_5678\\0",
            "VID_1234&PID_5678",
            "Test Device",
            DeviceTrustLevel.Blocked,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null,
            path =>
            {
                reEnabledPath = path;
                return Task.CompletedTask;
            });

        item.ReEnableCommand.Execute(null);
        await Task.Delay(50);

        reEnabledPath.ShouldBe("HID\\VID_1234&PID_5678\\0");
    }
}
