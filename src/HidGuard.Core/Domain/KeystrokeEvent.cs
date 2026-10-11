namespace HidGuard.Core.Domain;

public sealed record KeystrokeEvent
{
    public DeviceIdentifier DeviceId { get; }
    public DateTimeOffset Timestamp { get; }
    public KeyTransitionState State { get; }
    public uint VirtualKey { get; }
    public bool IsInjected { get; }

    public KeystrokeEvent(
        DeviceIdentifier deviceId,
        DateTimeOffset timestamp,
        KeyTransitionState state,
        uint virtualKey,
        bool isInjected = false)
    {
        ArgumentNullException.ThrowIfNull(deviceId);
        DeviceId = deviceId;
        Timestamp = timestamp;
        State = state;
        VirtualKey = virtualKey;
        IsInjected = isInjected;
    }
}
