using HidGuard.Core.Domain;

namespace HidGuard.Core.Trust;

public sealed record TrustRecord
{
    public DeviceIdentifier DeviceId { get; init; }
    public DeviceTrustLevel TrustLevel { get; init; }
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
    public string? FriendlyName { get; init; }
    public string EnrolledBy { get; init; }

    public TrustRecord(
        DeviceIdentifier deviceId,
        DeviceTrustLevel trustLevel,
        DateTimeOffset firstSeenAt,
        DateTimeOffset lastSeenAt,
        string enrolledBy,
        string? friendlyName = null)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(enrolledBy);

        DeviceId = deviceId;
        TrustLevel = trustLevel;
        FirstSeenAt = firstSeenAt;
        LastSeenAt = lastSeenAt;
        EnrolledBy = enrolledBy;
        FriendlyName = friendlyName;
    }
}
