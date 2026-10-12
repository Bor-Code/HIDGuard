using System.Collections.ObjectModel;
using System.Windows.Input;
using HidGuard.App.Mvvm;
using HidGuard.Core.Domain;
using HidGuard.Core.Events;
using HidGuard.Core.Trust;

namespace HidGuard.App.ViewModels;

public sealed class DevicesViewModel : ObservableObject
{
    private readonly ITrustStore trustStore;
    private DeviceItemViewModel? selectedDevice;
    private int totalDeviceCount;

    public ObservableCollection<DeviceItemViewModel> Devices { get; } = new();

    public DeviceItemViewModel? SelectedDevice
    {
        get => selectedDevice;
        set => SetProperty(ref selectedDevice, value);
    }

    public int TotalDeviceCount
    {
        get => totalDeviceCount;
        set => SetProperty(ref totalDeviceCount, value);
    }

    public ICommand RefreshCommand { get; }

    public DevicesViewModel(ITrustStore trustStore)
    {
        this.trustStore = trustStore ?? throw new ArgumentNullException(nameof(trustStore));
        RefreshCommand = new AsyncRelayCommand(RefreshDevicesAsync);
    }

    public async Task RefreshDevicesAsync()
    {
        var records = await trustStore.GetAllRecordsAsync().ConfigureAwait(false);

        DispatchToUi(() =>
        {
            Devices.Clear();
            foreach (var record in records)
            {
                var item = new DeviceItemViewModel(
                    record.DeviceId.Path,
                    record.DeviceId.FormattedHardwareId,
                    record.FriendlyName ?? record.DeviceId.FormattedHardwareId,
                    record.TrustLevel,
                    record.FirstSeenAt,
                    record.LastSeenAt,
                    OnDeviceTrustChangedAsync);

                Devices.Add(item);
            }

            TotalDeviceCount = Devices.Count;
        });
    }

    public void HandleDeviceLifecycleEvent(DeviceLifecycleEvent evt)
    {
        ArgumentNullException.ThrowIfNull(evt);

        DispatchToUi(() =>
        {
            var existing = Devices.FirstOrDefault(d =>
                string.Equals(d.DeviceId, evt.DeviceId.Path, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(d.HardwareId, evt.DeviceId.FormattedHardwareId, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                existing.LastSeen = evt.Timestamp;
            }
            else
            {
                var newItem = new DeviceItemViewModel(
                    evt.DeviceId.Path,
                    evt.DeviceId.FormattedHardwareId,
                    evt.DeviceId.FormattedHardwareId,
                    DeviceTrustLevel.Unknown,
                    evt.Timestamp,
                    evt.Timestamp,
                    OnDeviceTrustChangedAsync);

                Devices.Add(newItem);
                TotalDeviceCount = Devices.Count;
            }
        });
    }

    private async Task OnDeviceTrustChangedAsync(string devicePath, DeviceTrustLevel newTrustLevel)
    {
        var existing = Devices.FirstOrDefault(d => string.Equals(d.DeviceId, devicePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            var deviceIdentifier = ParseDeviceIdentifier(existing.DeviceId);
            await trustStore.SetTrustLevelAsync(deviceIdentifier, newTrustLevel, "User_UI", existing.DisplayName).ConfigureAwait(false);
        }
    }

    private static DeviceIdentifier ParseDeviceIdentifier(string path)
    {
        if (DeviceIdentifier.TryParseIds(path, out var vid, out var pid))
        {
            return new DeviceIdentifier(path, vid, pid);
        }

        return new DeviceIdentifier(path, 0, 0);
    }

    private static void DispatchToUi(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }
}
