namespace HidGuard.Simulator.Injection;

public sealed record InjectedKeyRecord(
    ushort VirtualKey,
    bool IsKeyUp,
    DateTimeOffset Timestamp);
