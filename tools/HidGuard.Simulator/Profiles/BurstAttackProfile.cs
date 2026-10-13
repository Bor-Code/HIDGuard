using HidGuard.Simulator.DuckyScript;
using HidGuard.Simulator.Injection;

namespace HidGuard.Simulator.Profiles;

public sealed class BurstAttackProfile : IAttackProfile
{
    public AttackProfileType ProfileType => AttackProfileType.Burst;
    public string Name => "Rapid Burst Keystroke Injection";

    public int KeystrokeCount { get; }
    public TimeSpan Interval { get; }
    public string? PayloadText { get; }

    public BurstAttackProfile(
        int keystrokeCount = 50,
        TimeSpan? interval = null,
        string? payloadText = null)
    {
        KeystrokeCount = Math.Max(1, keystrokeCount);
        Interval = interval ?? TimeSpan.FromMilliseconds(4);
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
                    await injector.SendKeyPressAsync(vk, shift, TimeSpan.FromMilliseconds(1), cancellationToken).ConfigureAwait(false);
                    await injector.DelayAsync(Interval, cancellationToken).ConfigureAwait(false);
                }
            }
            return;
        }

        for (var i = 0; i < KeystrokeCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var vk = (ushort)(0x41 + (i % 26));
            await injector.SendKeyPressAsync(vk, false, TimeSpan.FromMilliseconds(1), cancellationToken).ConfigureAwait(false);
            await injector.DelayAsync(Interval, cancellationToken).ConfigureAwait(false);
        }
    }
}
