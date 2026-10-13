using HidGuard.Platform.Windows;
using HidGuard.Simulator.DuckyScript;

namespace HidGuard.Simulator.Injection;

public sealed class Win32KeystrokeInjector : IKeystrokeInjector
{
    private static readonly TimeSpan DefaultHoldDuration = TimeSpan.FromMilliseconds(2);

    public Task SendKeyDownAsync(ushort virtualKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SyntheticInputDispatcher.SendKey(virtualKey, false);
        return Task.CompletedTask;
    }

    public Task SendKeyUpAsync(ushort virtualKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SyntheticInputDispatcher.SendKey(virtualKey, true);
        return Task.CompletedTask;
    }

    public async Task SendKeyPressAsync(
        ushort virtualKey,
        bool shift = false,
        TimeSpan? holdDuration = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var duration = holdDuration ?? DefaultHoldDuration;

        if (shift)
        {
            SyntheticInputDispatcher.SendKey(VirtualKeyMap.VkShift, false);
        }

        SyntheticInputDispatcher.SendKey(virtualKey, false);

        if (duration > TimeSpan.Zero)
        {
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
        }

        SyntheticInputDispatcher.SendKey(virtualKey, true);

        if (shift)
        {
            SyntheticInputDispatcher.SendKey(VirtualKeyMap.VkShift, true);
        }
    }

    public async Task SendKeyCombinationAsync(
        IReadOnlyList<ushort> virtualKeys,
        TimeSpan? holdDuration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(virtualKeys);
        cancellationToken.ThrowIfCancellationRequested();

        var duration = holdDuration ?? DefaultHoldDuration;

        for (var i = 0; i < virtualKeys.Count; i++)
        {
            SyntheticInputDispatcher.SendKey(virtualKeys[i], false);
        }

        if (duration > TimeSpan.Zero)
        {
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
        }

        for (var i = virtualKeys.Count - 1; i >= 0; i--)
        {
            SyntheticInputDispatcher.SendKey(virtualKeys[i], true);
        }
    }

    public async Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration > TimeSpan.Zero)
        {
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
        }
    }
}
