using HidGuard.Simulator.DuckyScript;
using HidGuard.Simulator.Injection;

namespace HidGuard.Simulator.Profiles;

public sealed class JitterAttackProfile : IAttackProfile
{
    private readonly Random random = new(42);

    public AttackProfileType ProfileType => AttackProfileType.Jitter;
    public string Name => "Low-Jitter Microcontroller Loop Attack";

    public int KeystrokeCount { get; }
    public TimeSpan BaseInterval { get; }
    public double MaxJitterMilliseconds { get; }
    public string? PayloadText { get; }

    public JitterAttackProfile(
        int keystrokeCount = 50,
        TimeSpan? baseInterval = null,
        double maxJitterMilliseconds = 0.5,
        string? payloadText = null)
    {
        KeystrokeCount = Math.Max(1, keystrokeCount);
        BaseInterval = baseInterval ?? TimeSpan.FromMilliseconds(15);
        MaxJitterMilliseconds = Math.Max(0.0, maxJitterMilliseconds);
        PayloadText = payloadText;
    }

    public async Task ExecuteAsync(IKeystrokeInjector injector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(injector);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrEmpty(PayloadText))
        {
            foreach (var ch in PayloadText)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var (vk, shift) = VirtualKeyMap.CharacterToVirtualKey(ch);
                if (vk != 0)
                {
                    await injector.SendKeyPressAsync(vk, shift, TimeSpan.FromMilliseconds(2), cancellationToken).ConfigureAwait(false);
                    var delay = CalculateNextInterval();
                    await injector.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
                }
            }
            return;
        }

        for (var i = 0; i < KeystrokeCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vk = (ushort)(0x41 + (i % 26));
            await injector.SendKeyPressAsync(vk, false, TimeSpan.FromMilliseconds(2), cancellationToken).ConfigureAwait(false);
            var delay = CalculateNextInterval();
            await injector.DelayAsync(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private TimeSpan CalculateNextInterval()
    {
        if (MaxJitterMilliseconds <= 0.0)
        {
            return BaseInterval;
        }

        var jitter = (random.NextDouble() * 2.0 - 1.0) * MaxJitterMilliseconds;
        var totalMs = Math.Max(1.0, BaseInterval.TotalMilliseconds + jitter);
        return TimeSpan.FromMilliseconds(totalMs);
    }
}
