using HidGuard.Simulator.DuckyScript;

namespace HidGuard.Simulator.Injection;

public sealed class SimulatedKeystrokeInjector : IKeystrokeInjector
{
    private readonly List<InjectedKeyRecord> injectedRecords = new();
    private readonly object syncLock = new();
    private readonly bool executeRealDelays;
    private DateTimeOffset currentTime;

    public event Action<ushort, bool>? KeyInjected;
    public event Action<TimeSpan>? Delayed;

    public IReadOnlyList<InjectedKeyRecord> InjectedRecords
    {
        get
        {
            lock (syncLock)
            {
                return injectedRecords.ToList();
            }
        }
    }

    public int TotalKeyEvents
    {
        get
        {
            lock (syncLock)
            {
                return injectedRecords.Count;
            }
        }
    }

    public SimulatedKeystrokeInjector(bool executeRealDelays = false, DateTimeOffset? initialTime = null)
    {
        this.executeRealDelays = executeRealDelays;
        currentTime = initialTime ?? DateTimeOffset.UtcNow;
    }

    public void Reset(DateTimeOffset? newInitialTime = null)
    {
        lock (syncLock)
        {
            injectedRecords.Clear();
            currentTime = newInitialTime ?? DateTimeOffset.UtcNow;
        }
    }

    public Task SendKeyDownAsync(ushort virtualKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RecordKey(virtualKey, false);
        return Task.CompletedTask;
    }

    public Task SendKeyUpAsync(ushort virtualKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RecordKey(virtualKey, true);
        return Task.CompletedTask;
    }

    public async Task SendKeyPressAsync(
        ushort virtualKey,
        bool shift = false,
        TimeSpan? holdDuration = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var hold = holdDuration ?? TimeSpan.FromMilliseconds(2);

        if (shift)
        {
            RecordKey(VirtualKeyMap.VkShift, false);
        }

        RecordKey(virtualKey, false);

        if (hold > TimeSpan.Zero)
        {
            await DelayAsync(hold, cancellationToken).ConfigureAwait(false);
        }

        RecordKey(virtualKey, true);

        if (shift)
        {
            RecordKey(VirtualKeyMap.VkShift, true);
        }
    }

    public async Task SendKeyCombinationAsync(
        IReadOnlyList<ushort> virtualKeys,
        TimeSpan? holdDuration = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(virtualKeys);
        cancellationToken.ThrowIfCancellationRequested();

        var hold = holdDuration ?? TimeSpan.FromMilliseconds(2);

        for (var i = 0; i < virtualKeys.Count; i++)
        {
            RecordKey(virtualKeys[i], false);
        }

        if (hold > TimeSpan.Zero)
        {
            await DelayAsync(hold, cancellationToken).ConfigureAwait(false);
        }

        for (var i = virtualKeys.Count - 1; i >= 0; i--)
        {
            RecordKey(virtualKeys[i], true);
        }
    }

    public async Task DelayAsync(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (duration <= TimeSpan.Zero)
        {
            return;
        }

        lock (syncLock)
        {
            currentTime = currentTime.Add(duration);
        }

        Delayed?.Invoke(duration);

        if (executeRealDelays)
        {
            await Task.Delay(duration, cancellationToken).ConfigureAwait(false);
        }
    }

    private void RecordKey(ushort virtualKey, bool isKeyUp)
    {
        DateTimeOffset timestamp;
        lock (syncLock)
        {
            timestamp = executeRealDelays ? DateTimeOffset.UtcNow : currentTime;
            injectedRecords.Add(new InjectedKeyRecord(virtualKey, isKeyUp, timestamp));
        }

        KeyInjected?.Invoke(virtualKey, isKeyUp);
    }
}
