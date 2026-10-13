namespace HidGuard.Simulator.Injection;

public interface IKeystrokeInjector
{
    Task SendKeyDownAsync(ushort virtualKey, CancellationToken cancellationToken = default);
    Task SendKeyUpAsync(ushort virtualKey, CancellationToken cancellationToken = default);
    Task SendKeyPressAsync(ushort virtualKey, bool shift = false, TimeSpan? holdDuration = null, CancellationToken cancellationToken = default);
    Task SendKeyCombinationAsync(IReadOnlyList<ushort> virtualKeys, TimeSpan? holdDuration = null, CancellationToken cancellationToken = default);
    Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default);
}
