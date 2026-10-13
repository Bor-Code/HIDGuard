using HidGuard.Simulator.DuckyScript;
using HidGuard.Simulator.Injection;

namespace HidGuard.Simulator.Profiles;

public sealed class HumanTypingProfile : IAttackProfile
{
    private readonly Random random = new(1337);

    public AttackProfileType ProfileType => AttackProfileType.HumanTyping;
    public string Name => "Realistic Human Keystroke Emulation";

    public string PayloadText { get; }
    public TimeSpan MeanInterval { get; }
    public TimeSpan StandardDeviation { get; }

    public HumanTypingProfile(
        string? payloadText = null,
        TimeSpan? meanInterval = null,
        TimeSpan? standardDeviation = null)
    {
        PayloadText = !string.IsNullOrWhiteSpace(payloadText)
            ? payloadText
            : "The quick brown fox jumps over the lazy dog";
        MeanInterval = meanInterval ?? TimeSpan.FromMilliseconds(130);
        StandardDeviation = standardDeviation ?? TimeSpan.FromMilliseconds(35);
    }

    public async Task ExecuteAsync(IKeystrokeInjector injector, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(injector);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var ch in PayloadText)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (vk, shift) = VirtualKeyMap.CharacterToVirtualKey(ch);
            if (vk != 0)
            {
                var holdMs = Math.Clamp(50.0 + random.NextDouble() * 30.0, 30.0, 90.0);
                await injector.SendKeyPressAsync(vk, shift, TimeSpan.FromMilliseconds(holdMs), cancellationToken).ConfigureAwait(false);

                var interval = GenerateHumanInterval(ch);
                await injector.DelayAsync(interval, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private TimeSpan GenerateHumanInterval(char character)
    {
        var u1 = 1.0 - random.NextDouble();
        var u2 = 1.0 - random.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);

        var sampleMs = MeanInterval.TotalMilliseconds + (StandardDeviation.TotalMilliseconds * randStdNormal);

        if (character is ' ' or '.' or ',' or '!' or '?')
        {
            sampleMs += 100.0 + (random.NextDouble() * 150.0);
        }

        var clamped = Math.Clamp(sampleMs, 65.0, 450.0);
        return TimeSpan.FromMilliseconds(clamped);
    }
}
