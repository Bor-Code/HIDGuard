using Shouldly;
using Xunit;
using HidGuard.App.ViewModels;
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
}
