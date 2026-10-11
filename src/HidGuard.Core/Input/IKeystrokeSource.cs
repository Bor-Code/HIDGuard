using HidGuard.Core.Domain;

namespace HidGuard.Core.Input;

public interface IKeystrokeSource : IDisposable
{
    event Action<KeystrokeEvent>? KeystrokeReceived;
    event Action<DeviceIdentifier>? DeviceConnected;
    event Action<DeviceIdentifier>? DeviceDisconnected;
    void StartListening();
    void StopListening();
    bool IsRunning { get; }
    IReadOnlyList<DeviceIdentifier> GetConnectedKeyboards();
}
