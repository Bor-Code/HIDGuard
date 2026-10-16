using HidGuard.Core.Domain;

namespace HidGuard.Core.Trust;

public interface IDevicePromptService
{
    Task<DeviceTrustLevel?> PromptDeviceTrustAsync(
        DeviceIdentifier deviceId,
        string? friendlyName = null,
        CancellationToken cancellationToken = default);
}
