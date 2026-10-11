using HidGuard.Core.Domain;

namespace HidGuard.Core.Countermeasures;

public interface IDeviceDisabler
{
    Task<bool> DisableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default);
    Task<bool> EnableDeviceAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default);
}
