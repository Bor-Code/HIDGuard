using HidGuard.Core.Domain;

namespace HidGuard.Core.Trust;

public interface ITrustStore : IDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<DeviceTrustLevel> GetTrustLevelAsync(DeviceIdentifier deviceId, CancellationToken cancellationToken = default);
    Task SetTrustLevelAsync(DeviceIdentifier deviceId, DeviceTrustLevel level, string enrolledBy, string? friendlyName = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TrustRecord>> GetAllRecordsAsync(CancellationToken cancellationToken = default);
    Task<int> EnrollInitialDevicesAsync(IEnumerable<DeviceIdentifier> devices, CancellationToken cancellationToken = default);
    Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default);
}
