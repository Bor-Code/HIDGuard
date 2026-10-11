using HidGuard.Core.Domain;

namespace HidGuard.Core.Events;

public sealed record DeviceLifecycleEvent(
    DeviceIdentifier DeviceId,
    DeviceLifecycleEventType EventType,
    DateTimeOffset Timestamp,
    string? Details = null);
