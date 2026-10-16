using System.Collections.ObjectModel;
using System.Windows.Input;
using HidGuard.App.Mvvm;
using HidGuard.Core.Audit;
using HidGuard.Core.Countermeasures;
using HidGuard.Core.Domain;
using HidGuard.Core.Events;
using HidGuard.Core.Trust;

namespace HidGuard.App.ViewModels;

public sealed class DevicesViewModel : ObservableObject
{
    private readonly ITrustStore trustStore;
    private readonly IDeviceDisabler? deviceDisabler;
    private readonly IWindowsEventLogService? eventLogService;
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

    public DevicesViewModel(
        ITrustStore trustStore,
        IDeviceDisabler? deviceDisabler = null,
        IWindowsEventLogService? eventLogService = null)
    {
        this.trustStore = trustStore ?? throw new ArgumentNullException(nameof(trustStore));
        this.deviceDisabler = deviceDisabler;
        this.eventLogService = eventLogService;
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
                    OnDeviceTrustChangedAsync,
                    OnDeviceReEnableRequestedAsync);

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
                    OnDeviceTrustChangedAsync,
                    OnDeviceReEnableRequestedAsync);

                Devices.Add(newItem);
                TotalDeviceCount = Devices.Count;
            }
        });
    }

    public async Task OnDeviceTrustChangedAsync(string devicePath, DeviceTrustLevel newTrustLevel)
    {
        var existing = Devices.FirstOrDefault(d => string.Equals(d.DeviceId, devicePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            var deviceIdentifier = ParseDeviceIdentifier(existing.DeviceId);
            await trustStore.SetTrustLevelAsync(deviceIdentifier, newTrustLevel, "User_UI", existing.DisplayName).ConfigureAwait(false);

            if (deviceDisabler != null)
            {
                try
                {
                    if (newTrustLevel == DeviceTrustLevel.Blocked)
                    {
                        var disabled = await deviceDisabler.DisableDeviceAsync(deviceIdentifier).ConfigureAwait(false);
                        if (disabled)
                        {
                            eventLogService?.WriteSecurityEvent(
                                SecurityEventIds.DeviceDisabled,
                                SecurityEventSeverity.Warning,
                                $"Hardware device disabled via administrative console: {deviceIdentifier.FormattedHardwareId} (Path: {deviceIdentifier.Path})");
                        }
                    }
                    else
                    {
                        var enabled = await deviceDisabler.EnableDeviceAsync(deviceIdentifier).ConfigureAwait(false);
                        if (enabled)
                        {
                            eventLogService?.WriteSecurityEvent(
                                SecurityEventIds.DeviceReEnabled,
                                SecurityEventSeverity.Information,
                                $"Hardware device re-enabled via administrative console: {deviceIdentifier.FormattedHardwareId} (Path: {deviceIdentifier.Path})");
                        }
                    }
                }
                catch
                {
                }
            }
        }
    }

    public async Task OnDeviceReEnableRequestedAsync(string devicePath)
    {
        var existing = Devices.FirstOrDefault(d => string.Equals(d.DeviceId, devicePath, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            var deviceIdentifier = ParseDeviceIdentifier(existing.DeviceId);
            if (deviceDisabler != null)
            {
                try
                {
                    var enabled = await deviceDisabler.EnableDeviceAsync(deviceIdentifier).ConfigureAwait(false);
                    if (enabled)
                    {
                        eventLogService?.WriteSecurityEvent(
                            SecurityEventIds.DeviceReEnabled,
                            SecurityEventSeverity.Information,
                            $"Hardware device re-enabled via administrative console: {deviceIdentifier.FormattedHardwareId} (Path: {deviceIdentifier.Path})");
                    }
                }
                catch
                {
                }
            }

            if (existing.TrustLevel == DeviceTrustLevel.Blocked)
            {
                await trustStore.SetTrustLevelAsync(deviceIdentifier, DeviceTrustLevel.Trusted, "User_UI", existing.DisplayName).ConfigureAwait(false);
                DispatchToUi(() => existing.TrustLevel = DeviceTrustLevel.Trusted);
            }
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
